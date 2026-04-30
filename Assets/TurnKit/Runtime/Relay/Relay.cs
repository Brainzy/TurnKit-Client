using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    public class Relay : MonoBehaviour
    {
        private const bool AutoReconnectEnabled = true;
        private const int AutoReconnectMaxAttempts = 5;
        private const float AutoReconnectInitialDelaySeconds = 1f;
        private const float AutoReconnectMaxDelaySeconds = 8f;

        private static Relay _instance;

        public static Relay Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("TurnKitRelay");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Relay>();
                    Application.runInBackground = true;
                }

                return _instance;
            }
        }

        private readonly RelaySessionState _state = new RelaySessionState();
        private readonly RelayValidator _validator = new RelayValidator();

        private RelayCommandQueue _commandQueue;
        private RelayTransport _transport;
        private RelayMessageRouter _messageRouter;
        private string _relayToken;
        private string _sessionId;
        private string _myPlayerId;
        private TurnKitConfig.PlayerSlot _mySlot;
        private bool _allowReconnect;
        private bool _sessionTerminated;
        private bool _disconnectEventRaised;
        private bool _reconnectScheduled;
        private bool _reconnectInProgress;
        private float _reconnectTimer;
        private int _reconnectAttempts;
        private float _turnTimerRemaining;
        private float _turnTimerDuration;
        private bool _turnTimerRunning;

        public static event Action<MatchStartedMessage, IReadOnlyList<RelayList>> OnMatchStarted;
        public static event Action<MoveMadeMessage, IReadOnlyList<RelayList>> OnMoveMade;
        public static event Action<TurnStartedMessage> OnTurnStarted;
        public static event Action<MoveRequestedForPlayerMessage> OnMoveRequestedForPlayer;
        public static event Action<PrivateListsRevealedMessage> OnPrivateListsRevealed;
        public static event Action<float, float> OnTurnTimerChanged;
        public static event Action OnTurnTimerExpired;
        public static event Action<VoteFailedMessage> OnVoteFailed;
        public static event Action<GameEndedMessage> OnGameEnded;
        public static event Action<ErrorMessage> OnError;
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action OnSyncComplete;
        public static event Action<ErrorMessage> OnReconnectFailed;
        public static event Action<RelayList, ListChangeType> OnListChanged;
        public static float TurnTimerRemainingSeconds => Instance._turnTimerRemaining;
        public static float TurnTimerDurationSeconds => Instance._turnTimerDuration;
        public static bool IsTurnTimerRunning => Instance._turnTimerRunning;

        private void Awake()
        {
            _commandQueue = new RelayCommandQueue(_validator);
            _transport = new RelayTransport(HandleConnected, HandleDisconnected, HandleTransportError, HandleIncomingMessage);
            _messageRouter = new RelayMessageRouter(_state, DispatchListChanged);
        }

        public static Task<bool> MatchWithAnyone(string playerId, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.NoAuth(playerId), slug, items);
        }

        public static Task<bool> MatchWithAnyone(TurnKitPlayerSession session, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.Authenticated(session), slug, items);
        }

        public static Task<bool> MatchWithAnyone(TurnKitYourBackendProof proof, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.YourBackend(proof), slug, items);
        }

        private static async Task<bool> MatchWithAnyone(TurnKitClientIdentity identity, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                throw new ArgumentException("Relay slug is required.", nameof(slug));
            }

            if (Registry.Initializers.TryGetValue(slug, out var initAction))
            {
                initAction.Invoke();
            }
            else
            {
                Debug.LogError($"[TurnKit] No config found for slug: {slug}");
                return false;
            }

            var body = BuildQueueRequestJson(slug, items);
            using var req = TurnKitClientRequest.CreateJson("/v1/client/relay/queue", "POST", body);
            await TurnKitClientRequest.PrepareIdentity(req, identity);

            try
            {
                var response = await TurnKitClientRequest.SendJson<QueueResponse>(req);
                Instance._relayToken = response.relayToken;
                Instance._sessionId = response.sessionId;
                Instance._mySlot = (TurnKitConfig.PlayerSlot)response.slot;
                Instance._myPlayerId = identity.PlayerId;
                Instance._state.SetLocalPlayer(identity.PlayerId);
                Instance._sessionTerminated = false;
                Instance._allowReconnect = true;
                Instance.ResetReconnectBackoff();
                Instance.CacheTurnTimerDuration(slug);
                Instance.ResetTurnTimer();

                await Instance._transport.Connect(Instance._relayToken, Instance._state.LastAcknowledgedMoveNumber);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TurnKit] Queue join failed: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> Reconnect()
        {
            if (_instance == null)
            {
                return false;
            }

            return await Instance.ReconnectInternal(manual: true);
        }

        public static async Task<bool> Resume(string playerId, string slug, string relayToken, int lastMoveNumber)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(relayToken))
            {
                return false;
            }

            if (Registry.Initializers.TryGetValue(slug, out var initAction))
            {
                initAction.Invoke();
            }
            else
            {
                Debug.LogError($"[TurnKit] No config found for slug: {slug}");
                return false;
            }

            Instance._relayToken = relayToken;
            Instance._myPlayerId = playerId;
            Instance._state.SetLocalPlayer(playerId);
            Instance._sessionTerminated = false;
            Instance._allowReconnect = true;
            Instance.ResetReconnectBackoff();
            Instance.CacheTurnTimerDuration(slug);
            Instance.ResetTurnTimer();

            return await Instance.ResumeInternal(relayToken, lastMoveNumber);
        }

        public static void SendJson(string json)
        {
            Instance._commandQueue.QueueJson(json);
        }

        public static void Commit()
        {
            Instance.ExecuteQueuedActions(false, false, null);
        }

        public static void EndMyTurn()
        {
            Instance.ExecuteQueuedActions(true, false, null);
        }

        public static void CommitForPlayer(TurnKitConfig.PlayerSlot slot)
        {
            string playerId = Instance.ResolvePlayerId(slot);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError($"[TurnKit] Cannot commit delegated move to slot {slot}: no player is assigned to that slot.");
                Instance._commandQueue.Clear();
                return;
            }

            Instance.ExecuteQueuedActions(false, true, playerId);
        }

        public static void EndTurnForPlayer(TurnKitConfig.PlayerSlot slot)
        {
            string playerId = Instance.ResolvePlayerId(slot);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError($"[TurnKit] Cannot end delegated turn for slot {slot}: no player is assigned to that slot.");
                Instance._commandQueue.Clear();
                return;
            }

            Instance.ExecuteQueuedActions(true, true, playerId);
        }

        public static void PassTurnTo(TurnKitConfig.PlayerSlot slot)
        {
            var playerId = Instance.ResolvePlayerId(slot);
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogError($"[TurnKit] Cannot pass turn to slot {slot}: no player is assigned to that slot.");
                return;
            }

            Instance._commandQueue.QueuePassTurn(playerId);
            Instance.ExecuteQueuedActions(false, false, null);
        }

        public static TBuilder Stat<TValue, TBuilder>(MatchStatToken<TValue, TBuilder> token)
        {
            return Instance.CreateStatBuilder<TBuilder>(token.GetMetadata(), null);
        }

        public static PlayerStatTargetBuilder<TValue, TBuilder> Stat<TValue, TBuilder>(PlayerStatToken<TValue, TBuilder> token)
        {
            return new PlayerStatTargetBuilder<TValue, TBuilder>(token.GetMetadata());
        }

        internal bool TryGetTrackedStatValue<TValue>(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot, out TValue value)
        {
            if (metadata == null)
            {
                value = default;
                return false;
            }

            bool expectsPlayer = metadata.Scope == TurnKitConfig.TrackedStatScope.PER_PLAYER;
            if (expectsPlayer != slot.HasValue)
            {
                value = default;
                return false;
            }

            string playerId = ResolvePlayerId(slot);
            return _state.TryGetTrackedStatValue(metadata.Name, playerId, out value);
        }

        private string ResolvePlayerId(TurnKitConfig.PlayerSlot? slot)
        {
            if (!slot.HasValue)
            {
                return null;
            }

            string playerId = _state.ResolvePlayerId(slot.Value);
            return string.IsNullOrEmpty(playerId) ? null : playerId;
        }

        public static RelayList List<T>(T listEnum) where T : Enum
        {
            string name = listEnum.ToString();
            return !Instance._state.TryGetListByName(name, out var list) ? throw new KeyNotFoundException($"[TurnKit] List not found: {name}") : list;
        }

        public static List<RelayList> GetMyLists<T>(T tag) where T : Enum
        {
            return !Instance._state.TryGetListsByTag(tag.ToString(), out var lists) ? null : lists.Where(list => list.IsOwnedByMe).ToList();
        }
		
        public static List<RelayList> GetOpponentsLists<T>(T tag) where T : Enum
        {
            return !Instance._state.TryGetListsByTag(tag.ToString(), out var lists) ? null : lists.Where(list => !list.IsOwnedByMe).ToList();
        }
		
		public static RelayList GetList<T>(T tag, TurnKitConfig.PlayerSlot slot) where T : Enum
        {
            return !Instance._state.TryGetListsByTag(tag.ToString(), out var lists) ? null : lists.First(list => list._ownerSlots.Contains(slot));
        }

        public static RelayList GetList<T>(T name)
        {
            return !Instance._state.TryGetListByName(name.ToString(), out var list) ? null : list;
        }

        public static IReadOnlyList<RelayList> AllLists => Instance._state.AllLists;

        public static void Vote(int moveNumber, bool isValid)
        {
            if (!Instance._transport.IsConnected)
            {
                return;
            }

            var msg = new JSONObject();
            msg["type"] = "VOTE";
            msg["moveNumber"] = moveNumber;
            msg["isValid"] = isValid;
            Instance._transport.Send(msg.ToString());
        }

        public static void EndGame()
        {
            if (!Instance._transport.IsConnected)
            {
                return;
            }

            Instance._transport.Send("{\"type\":\"END_GAME\"}");
        }

        public static Task LeaveQueue(string playerId, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.NoAuth(playerId), slug);
        }

        public static Task LeaveQueue(TurnKitPlayerSession session, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.Authenticated(session), slug);
        }

        public static Task LeaveQueue(TurnKitYourBackendProof proof, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.YourBackend(proof), slug);
        }

        private static async Task LeaveQueue(TurnKitClientIdentity identity, string slug)
        {
            if (_instance != null)
            {
                _instance.DisableReconnect();
                await _instance._transport.Close();
            }

            using var req = TurnKitClientRequest.Create($"/v1/client/relay/queue/{slug}", "DELETE");
            await TurnKitClientRequest.PrepareIdentity(req, identity);
            await TurnKitClientRequest.Send(req);
        }

        public void InitializeFromMetadata<TList>(
            Dictionary<TList, TurnKitConfig.RelayListConfig> listMetadata,
            Dictionary<string, TrackedStatMetadata> statMetadata)
            where TList : Enum
        {
            _state.InitializeFromMetadata(listMetadata, statMetadata);
        }

        public static bool IsReady => Instance._transport.IsConnected && !Instance._state.IsInSyncWindow;
        public static bool IsMyTurn => Instance._state.IsMyTurn;
        public static bool IsWaitingForDelegatedMove => Instance._state.IsWaitingForDelegatedMove;
        public static string DelegatedMoveRequestedForPlayerId => Instance._state.DelegatedMoveRequestedForPlayerId;
        public static int LastAcknowledgedMoveNumber => Instance._state.LastAcknowledgedMoveNumber;
        public static string MyPlayerId => Instance._myPlayerId;
        public static TurnKitConfig.PlayerSlot MySlot => Instance._mySlot;
        public static string CurrentPlayerId => Instance._state.CurrentTurnPlayerId;
        public static IReadOnlyList<PlayerInfo> AllPlayers => Instance._state.AllPlayers;

        public static PlayerInfo GetPlayerBySlot(TurnKitConfig.PlayerSlot slot)
        {
            return Instance._state.GetPlayerBySlot(slot);
        }

        internal void EnqueueSpawn(RelayList toList, ItemSpec itemSpec)
        {
            _commandQueue.QueueSpawn(toList, itemSpec);
        }

        internal void EnqueueShuffle(RelayList list)
        {
            _commandQueue.QueueShuffle(list);
        }

        internal void EnqueuePassTurnTo(TurnKitConfig.PlayerSlot slot)
        {
            var playerId = ResolvePlayerId(slot);
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogError($"[TurnKit] Cannot pass turn to slot {slot}: no player is assigned to that slot.");
                return;
            }

            _commandQueue.QueuePassTurn(playerId);
        }

        internal MoveBuilder CreateMoveBuilder(RelayList fromList, SelectorType selector, string[] data)
        {
            var builder = new MoveBuilder(fromList, selector, data);
            _commandQueue.RegisterPendingBuilder(builder);
            return builder;
        }

        internal RemoveBuilder CreateRemoveBuilder(RelayList fromList, SelectorType selector, string[] data)
        {
            var builder = new RemoveBuilder(fromList, selector, data);
            _commandQueue.RegisterPendingBuilder(builder);
            return builder;
        }

        internal void CompleteMoveBuilder(MoveBuilder builder, RelayList fromList, RelayList targetList, SelectorType selector, string[] data, int repeat, bool ignoreOwnership)
        {
            _commandQueue.QueueMove(builder, fromList, targetList, selector, data, repeat, ignoreOwnership);
        }

        internal void ExecuteRemoveBuilder(RemoveBuilder builder, RelayList fromList, SelectorType selector, string[] data, int repeat, bool ignoreOwnership)
        {
            _commandQueue.QueueRemove(builder, fromList, selector, data, repeat, ignoreOwnership);
        }

        internal void QueueSetStat(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot, JSONNode value)
        {
            if (!_validator.ValidateTrackedStatMetadata(metadata?.Name, metadata))
            {
                return;
            }

            _commandQueue.QueueSetStat(metadata, slot, ResolvePlayerId(slot), value);
        }

        internal void QueueAddStat(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot, double? delta, string[] values)
        {
            if (!_validator.ValidateTrackedStatMetadata(metadata?.Name, metadata))
            {
                return;
            }

            _commandQueue.QueueAddStat(metadata, slot, ResolvePlayerId(slot), delta, values);
        }

        internal TBuilder CreateStatBuilder<TBuilder>(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot)
        {
            if (metadata == null)
            {
                throw new InvalidOperationException("[TurnKit] Stat token is not initialized.");
            }

            bool expectsPlayer = metadata.Scope == TurnKitConfig.TrackedStatScope.PER_PLAYER;
            if (expectsPlayer != slot.HasValue)
            {
                string targetKind = expectsPlayer ? "requires" : "must not target";
                string targetName = expectsPlayer ? "a player slot" : "a player";
                throw new InvalidOperationException($"[TurnKit] Stat '{metadata.Name}' {targetKind} {targetName}.");
            }

            object builder = metadata.DataType switch
            {
                TurnKitConfig.TrackedStatDataType.DOUBLE => new DoubleStatBuilder(metadata, slot),
                TurnKitConfig.TrackedStatDataType.STRING => new StringStatBuilder(metadata, slot),
                TurnKitConfig.TrackedStatDataType.LIST_STRING => new ListStringStatBuilder(metadata, slot),
                _ => throw new InvalidOperationException($"[TurnKit] Unsupported tracked stat type '{metadata.DataType}' for '{metadata.Name}'.")
            };

            if (builder is TBuilder typedBuilder)
            {
                return typedBuilder;
            }

            throw new InvalidOperationException($"[TurnKit] Stat '{metadata.Name}' does not map to builder type '{typeof(TBuilder).Name}'.");
        }

        private void ExecuteQueuedActions(bool shouldEndTurn, bool delegated, string delegateForPlayerId)
        {
            _validator.ValidateReadyToSend(_transport.IsConnected, _state.IsInSyncWindow);

            if (!_state.IsMyTurn && !delegated)
            {
                Debug.LogError("[TurnKit] Not your turn. Actions not sent.");
                _commandQueue.Clear();
                return;
            }

            _transport.Send(_commandQueue.BuildMovePayload(shouldEndTurn, delegated, delegateForPlayerId));
            _commandQueue.Clear();
        }

        private void Update()
        {
            _transport?.Tick(Time.deltaTime);
            TickAutoReconnect(Time.deltaTime);
            TickTurnTimer(Time.deltaTime);
        }

        private async void OnDestroy()
        {
            DisableReconnect();
            if (_transport != null)
            {
                await _transport.Close();
            }
        }

        private void HandleConnected()
        {
            _state.MarkConnected();
            _disconnectEventRaised = false;
            ResetReconnectBackoff();
            OnConnected?.Invoke();

            if (TurnKitConfig.Instance != null && TurnKitConfig.Instance.enableLogging)
            {
                Debug.Log("TurnKit - WebSocket Connected");
            }
        }

        private void HandleDisconnected(ulong code)
        {
            HandleDisconnectInternal($"closed ({code})");
        }

        private void HandleTransportError(string err)
        {
            HandleDisconnectInternal("error");
            Debug.LogError($"TurnKit - WebSocket error: {err}");
        }

        private void HandleIncomingMessage(string raw)
        {
            var outcome = _messageRouter.Process(raw);
            if (outcome == null)
            {
                return;
            }

            switch (outcome.EventType)
            {
                case RelayEventType.MatchStarted:
                    _sessionId = outcome.MatchStarted.sessionId;
                    var localPlayer = outcome.MatchStarted.players?.FirstOrDefault(player => player.playerId == _myPlayerId);
                    if (localPlayer != null)
                    {
                        _mySlot = localPlayer.slot;
                    }

                    if (outcome.MatchStarted.yourTurn)
                    {
                        StartTurnTimer();
                    }
                    else
                    {
                        StopTurnTimer();
                    }
                    OnMatchStarted?.Invoke(outcome.MatchStarted, _state.AllLists);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log(outcome.MatchStarted.ToString(_state.AllLists.Count));
                    }

                    break;
                case RelayEventType.MoveMade:
                    OnMoveMade?.Invoke(outcome.MoveMade, _state.AllLists);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - MoveMade #{outcome.MoveMade.moveNumber} from {outcome.MoveMade.actingPlayerId}");
                    }

                    break;
                case RelayEventType.SyncComplete:
                    OnSyncComplete?.Invoke();
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - Sync complete (move: {outcome.SyncComplete.moveNumber})");
                    }

                    break;
                case RelayEventType.TurnStarted:
                    StartTurnTimer();
                    OnTurnStarted?.Invoke(outcome.TurnStarted);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - TurnStarted: {outcome.TurnStarted.activePlayerId}");
                    }

                    break;
                case RelayEventType.MoveRequestedForPlayer:
                    StopTurnTimer();
                    OnMoveRequestedForPlayer?.Invoke(outcome.MoveRequestedForPlayer);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - MoveRequestedForPlayer: {outcome.MoveRequestedForPlayer.playerId}");
                    }

                    break;
                case RelayEventType.PrivateListsRevealed:
                    OnPrivateListsRevealed?.Invoke(outcome.PrivateListsRevealed);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - PrivateListsRevealed for: {outcome.PrivateListsRevealed.playerId}");
                    }

                    break;
                case RelayEventType.VoteFailed:
                    OnVoteFailed?.Invoke(outcome.VoteFailed);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - OnVoteFailed: {outcome.VoteFailed.failAction} - {outcome.VoteFailed.moveNumber}");
                    }

                    break;
                case RelayEventType.Error:
                    OnError?.Invoke(outcome.Error);
                    if (IsReconnectTerminalError(outcome.Error))
                    {
                        DisableReconnect();
                        OnReconnectFailed?.Invoke(outcome.Error);
                    }
                    else if (ShouldReconnectOnError(outcome.Error))
                    {
                        _ = ReconnectFromStaleSocketError();
                    }
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - OnError: {outcome.Error.code} - {outcome.Error.message}");
                    }

                    break;
                case RelayEventType.GameEnded:
                    OnGameEnded?.Invoke(outcome.GameEnded);
                    StopTurnTimer();
                    DisableReconnect();
                    _transport.MarkDisconnected();
                    _state.MarkDisconnected();
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - OnGameEnded: {outcome.GameEnded.reason}");
                    }
                    break;
            }
        }

        private void DispatchListChanged(RelayList list, ListChangeType changeType)
        {
            OnListChanged?.Invoke(list, changeType);
        }

        private static string BuildQueueRequestJson(string slug, Dictionary<string, List<RelayItem>> items)
        {
            var request = new JSONObject();
            request["slug"] = slug;
            request["items"] = BuildItemsNode(items);
            return request.ToString();
        }

        private static JSONNode BuildItemsNode(Dictionary<string, List<RelayItem>> items)
        {
            var obj = new JSONObject();
            if (items == null)
            {
                return obj;
            }

            foreach (var kvp in items)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null)
                {
                    continue;
                }

                var arr = new JSONArray();
                foreach (var item in kvp.Value)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    var itemNode = new JSONObject();
                    itemNode["id"] = item.Id;
                    itemNode["slug"] = item.Slug;
                    itemNode["creatorSlot"] = (int)item.CreatorSlot;
                    arr.Add(itemNode);
                }

                obj.Add(kvp.Key, arr);
            }

            return obj;
        }

        private static bool HasQueueItems(Dictionary<string, List<RelayItem>> items)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            foreach (var kvp in items)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null || kvp.Value.Count == 0)
                {
                    continue;
                }

                foreach (var item in kvp.Value)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Slug))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void HandleDisconnectInternal(string reason)
        {
            _state.MarkDisconnected();
            StopTurnTimer();
            if (!_disconnectEventRaised)
            {
                _disconnectEventRaised = true;
                OnDisconnected?.Invoke();
            }

            if (TurnKitConfig.Instance != null && TurnKitConfig.Instance.enableLogging)
            {
                Debug.Log($"TurnKit - WebSocket disconnected ({reason})");
            }

            ScheduleAutoReconnect();
        }

        private void TickAutoReconnect(float deltaTime)
        {
            if (!_reconnectScheduled || _reconnectInProgress || _transport == null || _transport.IsConnected)
            {
                return;
            }

            _reconnectTimer -= deltaTime;
            if (_reconnectTimer > 0f)
            {
                return;
            }

            _ = AttemptAutoReconnectAsync();
        }

        private async Task AttemptAutoReconnectAsync()
        {
            if (_reconnectInProgress || _transport.IsConnected || _sessionTerminated)
            {
                return;
            }

            if (_reconnectAttempts >= AutoReconnectMaxAttempts)
            {
                _reconnectScheduled = false;
                return;
            }

            _reconnectScheduled = false;
            _reconnectAttempts++;

            if (TurnKitConfig.Instance != null && TurnKitConfig.Instance.enableLogging)
            {
                Debug.Log($"TurnKit - Reconnect attempt {_reconnectAttempts}/{AutoReconnectMaxAttempts}");
            }

            var success = await ReconnectInternal(manual: false);
            if (success || _sessionTerminated || _transport.IsConnected)
            {
                return;
            }

            if (_reconnectAttempts >= AutoReconnectMaxAttempts)
            {
                if (TurnKitConfig.Instance != null && TurnKitConfig.Instance.enableLogging)
                {
                    Debug.LogWarning("TurnKit - Reconnect retries exhausted.");
                }

                return;
            }

            _reconnectScheduled = true;
            _reconnectTimer = CalculateReconnectDelay(_reconnectAttempts);
        }

        private async Task<bool> ReconnectInternal(bool manual)
        {
            if (_sessionTerminated || _transport == null)
            {
                return false;
            }

            if (_transport.IsConnected && !manual)
            {
                return true;
            }

            if (manual)
            {
                _reconnectScheduled = false;
            }

            _reconnectInProgress = true;
            try
            {
                return await _transport.Reconnect(_state.LastAcknowledgedMoveNumber, force: manual);
            }
            finally
            {
                _reconnectInProgress = false;
            }
        }

        private async Task<bool> ResumeInternal(string relayToken, int lastMoveNumber)
        {
            if (_sessionTerminated || _transport == null)
            {
                return false;
            }

            _reconnectScheduled = false;
            _reconnectInProgress = true;
            try
            {
                return await _transport.Resume(relayToken, lastMoveNumber);
            }
            finally
            {
                _reconnectInProgress = false;
            }
        }

        private void ScheduleAutoReconnect()
        {
            if (!AutoReconnectEnabled || !_allowReconnect || _sessionTerminated || _transport == null || _transport.IsConnected)
            {
                return;
            }

            if (_reconnectAttempts >= AutoReconnectMaxAttempts)
            {
                return;
            }

            _reconnectScheduled = true;
            _reconnectTimer = CalculateReconnectDelay(_reconnectAttempts);
        }

        private void ResetReconnectBackoff()
        {
            _reconnectScheduled = false;
            _reconnectInProgress = false;
            _reconnectAttempts = 0;
            _reconnectTimer = 0f;
        }

        private void StartTurnTimer()
        {
            if (_turnTimerDuration <= 0f)
            {
                StopTurnTimer();
                return;
            }

            _turnTimerRemaining = _turnTimerDuration;
            _turnTimerRunning = true;
            OnTurnTimerChanged?.Invoke(_turnTimerRemaining, _turnTimerDuration);
        }

        private void TickTurnTimer(float deltaTime)
        {
            if (!_turnTimerRunning)
            {
                return;
            }

            _turnTimerRemaining -= deltaTime;
            if (_turnTimerRemaining > 0f)
            {
                OnTurnTimerChanged?.Invoke(_turnTimerRemaining, _turnTimerDuration);
                return;
            }

            _turnTimerRemaining = 0f;
            _turnTimerRunning = false;
            OnTurnTimerChanged?.Invoke(_turnTimerRemaining, _turnTimerDuration);
            OnTurnTimerExpired?.Invoke();
        }

        private void StopTurnTimer()
        {
            if (!_turnTimerRunning && _turnTimerRemaining <= 0f)
            {
                return;
            }

            _turnTimerRunning = false;
            _turnTimerRemaining = 0f;
            OnTurnTimerChanged?.Invoke(_turnTimerRemaining, _turnTimerDuration);
        }

        private void ResetTurnTimer()
        {
            _turnTimerRunning = false;
            _turnTimerRemaining = 0f;
        }

        private void CacheTurnTimerDuration(string slug)
        {
            _turnTimerDuration = 0f;

            if (TurnKitConfig.Instance?.relayConfigs == null || string.IsNullOrWhiteSpace(slug))
            {
                return;
            }

            var relayConfig = TurnKitConfig.Instance.relayConfigs.FirstOrDefault(relay => relay != null && relay.slug == slug);
            if (relayConfig != null && relayConfig.turnTimeoutSeconds > 0)
            {
                _turnTimerDuration = relayConfig.turnTimeoutSeconds;
            }
        }

        private void DisableReconnect()
        {
            _allowReconnect = false;
            _sessionTerminated = true;
            StopTurnTimer();
            ResetReconnectBackoff();
        }

        private static float CalculateReconnectDelay(int failedAttempts)
        {
            var delay = AutoReconnectInitialDelaySeconds * Mathf.Pow(2f, failedAttempts);
            return Mathf.Min(delay, AutoReconnectMaxDelaySeconds);
        }

        private static bool ShouldReconnectOnError(ErrorMessage error)
        {
            return error != null && string.Equals(error.code, "STALE_SOCKET", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReconnectTerminalError(ErrorMessage error)
        {
            if (error == null)
            {
                return false;
            }

            return string.Equals(error.code, "RECONNECT_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(error.code, "RECONNECT_MOVE_GAP_TOO_LARGE", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ReconnectFromStaleSocketError()
        {
            if (_reconnectInProgress || _sessionTerminated || _transport == null)
            {
                return;
            }

            _reconnectScheduled = false;
            await ReconnectInternal(manual: true);
        }

        [Serializable]
        private class QueueResponse
        {
            public string relayToken;
            public string sessionId;
            public int slot;
        }
    }
}
