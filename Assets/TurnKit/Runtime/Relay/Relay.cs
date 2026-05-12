using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    public enum FillPolicy
    {
        REQUIRE_ALL_PLAYERS,
        ALLOW_DELEGATED_SLOTS
    }

    public partial class Relay : MonoBehaviour
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
        private RelayTurnTimer _turnTimer;
        private RelayCommandExecutor _commandExecutor;
        private RelayReconnectStore _reconnectStore;
        private RelayReconnectController _reconnectController;
        private string _relayToken;
        private string _relaySlug;
        private string _sessionId;
        private string _myPlayerId;
        private TurnKitConfig.PlayerSlot _mySlot;
        private bool _disconnectEventRaised;
        private bool _isAfk;

        public static event Action<MatchStartedMessage, IReadOnlyList<RelayList>> OnMatchStarted;
        public static event Action<MoveMadeMessage, IReadOnlyList<RelayList>> OnMoveMade;
        public static event Action<TurnStartedMessage> OnTurnStarted;
        public static event Action<MoveRequestedForPlayerMessage> OnMoveRequestedForPlayer;
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
        public static float TurnTimerRemainingSeconds => Instance._turnTimer.RemainingSeconds;
        public static float TurnTimerDurationSeconds => Instance._turnTimer.DurationSeconds;
        public static bool IsTurnTimerRunning => Instance._turnTimer.IsRunning;
        public static bool isAfk
        {
            get => Instance._isAfk;
            set => Instance._isAfk = value;
        }

        private void Awake()
        {
            _commandQueue = new RelayCommandQueue(_validator);
            _transport = new RelayTransport(HandleConnected, HandleDisconnected, HandleTransportError, HandleIncomingMessage);
            _messageRouter = new RelayMessageRouter(_state, DispatchListChanged);
            _turnTimer = new RelayTurnTimer(
                (remaining, duration) => OnTurnTimerChanged?.Invoke(remaining, duration),
                () => OnTurnTimerExpired?.Invoke());
            _commandExecutor = new RelayCommandExecutor(
                _validator,
                _state,
                _commandQueue,
                _transport,
                () => _isAfk,
                subscribeMoveMade: handler => OnMoveMade += handler,
                unsubscribeMoveMade: handler => OnMoveMade -= handler,
                subscribeError: handler => OnError += handler,
                unsubscribeError: handler => OnError -= handler,
                subscribeDisconnected: handler => OnDisconnected += handler,
                unsubscribeDisconnected: handler => OnDisconnected -= handler);
            _reconnectStore = new RelayReconnectStore();
            _reconnectController = new RelayReconnectController(
                _transport,
                lastAcknowledgedMoveNumber: () => _state.LastAcknowledgedMoveNumber,
                onReconnectFailedExhausted: () => { },
                isLoggingEnabled: () => TurnKitConfig.Instance != null && TurnKitConfig.Instance.enableLogging,
                autoReconnectEnabled: AutoReconnectEnabled,
                autoReconnectMaxAttempts: AutoReconnectMaxAttempts,
                autoReconnectInitialDelaySeconds: AutoReconnectInitialDelaySeconds,
                autoReconnectMaxDelaySeconds: AutoReconnectMaxDelaySeconds);
        }

        public static Task<bool> MatchWithAnyone(
            string playerId,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.NoAuth(playerId), slug, items, fillPolicy, delegatedFillAfterSeconds);
        }

        public static Task<bool> MatchWithAnyone(
            TurnKitPlayerSession session,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.Authenticated(session), slug, items, fillPolicy, delegatedFillAfterSeconds);
        }

        public static Task<bool> MatchWithAnyone(
            TurnKitYourBackendProof proof,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.YourBackend(proof), slug, items, fillPolicy, delegatedFillAfterSeconds);
        }

        private static async Task<bool> MatchWithAnyone(
            TurnKitClientIdentity identity,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
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

            var body = BuildQueueRequestJson(slug, items, fillPolicy, delegatedFillAfterSeconds);
            using var req = TurnKitClientRequest.CreateJson("/v1/client/relay/queue", "POST", body);
            await TurnKitClientRequest.PrepareIdentity(req, identity);

            try
            {
                var response = await TurnKitClientRequest.SendJson<RelayQueueResponse>(req);
                Instance._relayToken = response.relayToken;
                Instance._relaySlug = slug;
                Instance._sessionId = response.sessionId;
                Instance._mySlot = (TurnKitConfig.PlayerSlot)response.slot;
                Instance._myPlayerId = identity.PlayerId;
                Instance._state.SetLocalPlayer(identity.PlayerId);
                Instance._reconnectController.MarkSessionActive();
                Instance.ResetTurnTimer();
                Instance._commandExecutor.ResetDelegatedIntendedMoveTracking();

                await Instance._transport.Connect(Instance._relayToken, Instance._state.LastAcknowledgedMoveNumber);
                Instance._reconnectStore.Save(identity.PlayerId, slug, Instance._relayToken, Instance._state.LastAcknowledgedMoveNumber);
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

            return await Instance._reconnectController.Reconnect(manual: true);
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
            Instance._relaySlug = slug;
            Instance._myPlayerId = playerId;
            Instance._state.SetLocalPlayer(playerId);
            Instance._reconnectController.MarkSessionActive();
            Instance.ResetTurnTimer();
            Instance._commandExecutor.ResetDelegatedIntendedMoveTracking();

            bool resumed = await Instance._reconnectController.Resume(relayToken, lastMoveNumber);
            if (resumed)
            {
                Instance._reconnectStore.Save(playerId, slug, relayToken, Instance._state.LastAcknowledgedMoveNumber);
            }

            return resumed;
        }

        public static async Task<bool> ResumeLastMatch()
        {
            RelayReconnectSnapshot snapshot = Instance._reconnectStore.Load();
            if (snapshot == null)
            {
                return false;
            }

            bool success = await Resume(snapshot.PlayerId, snapshot.Slug, snapshot.RelayToken, snapshot.LastMoveNumber);
            if (!success)
            {
                ClearSavedReconnect();
            }

            return success;
        }

        public static void ClearSavedReconnect()
        {
            Instance._reconnectStore.Clear();
        }

        public static void SendJson(string json)
        {
            Instance._commandQueue.QueueJson(json);
        }

        public static Task Commit()
        {
            return Instance._commandExecutor.ExecuteQueuedActionsAndWait(false, false, null);
        }

        public static Task EndMyTurn()
        {
            return Instance._commandExecutor.ExecuteQueuedActionsAndWait(true, false, null);
        }

        public static Task CommitForPlayer(TurnKitConfig.PlayerSlot slot)
        {
            string playerId = Instance.ResolvePlayerId(slot);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError($"[TurnKit] Cannot commit delegated move to slot {slot}: no player is assigned to that slot.");
                Instance._commandQueue.Clear();
                return Task.CompletedTask;
            }

            return Instance._commandExecutor.ExecuteQueuedActionsAndWait(false, true, playerId);
        }

        public static Task EndTurnForPlayer(TurnKitConfig.PlayerSlot slot)
        {
            string playerId = Instance.ResolvePlayerId(slot);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError($"[TurnKit] Cannot end delegated turn for slot {slot}: no player is assigned to that slot.");
                Instance._commandQueue.Clear();
                return Task.CompletedTask;
            }

            return Instance._commandExecutor.ExecuteQueuedActionsAndWait(true, true, playerId);
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
            ClearSavedReconnect();

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

        public static bool IsMyTurn => Instance._state.IsMyTurn;
        public static bool IsWaitingForDelegatedMove => Instance._state.IsWaitingForDelegatedMove;
        public static int LastAcknowledgedMoveNumber => Instance._state.LastAcknowledgedMoveNumber;
        public static string MyPlayerId => Instance._myPlayerId;
        public static TurnKitConfig.PlayerSlot MySlot => Instance._mySlot;
        public static string CurrentPlayerId => Instance._state.CurrentTurnPlayerId;
        public static TurnKitConfig.PlayerSlot CurrentTurnSlot => Instance._state.CurrentTurnSlot;
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

        private void Update()
        {
            _transport?.Tick(Time.deltaTime);
            _reconnectController?.Tick(Time.deltaTime);
            _turnTimer.Tick(Time.deltaTime);
        }

        private async void OnDestroy()
        {
            DisableReconnect();
            if (_transport != null)
            {
                await _transport.Close();
            }
        }


        private static string BuildQueueRequestJson(
            string slug,
            Dictionary<string, List<RelayItem>> items,
            FillPolicy fillPolicy,
            int? delegatedFillAfterSeconds)
        {
            var request = new JSONObject();
            request["slug"] = slug;
            request["items"] = BuildItemsNode(items);
            request["fillPolicy"] = fillPolicy.ToString();

            if (fillPolicy == FillPolicy.ALLOW_DELEGATED_SLOTS && delegatedFillAfterSeconds.HasValue)
            {
                request["delegatedFillAfterSeconds"] = Math.Max(0, delegatedFillAfterSeconds.Value);
            }

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

    }
}
