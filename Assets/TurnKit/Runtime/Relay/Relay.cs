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

        public static event Action<MatchStartedMessage, IReadOnlyList<RelayList>> OnMatchStarted;
        public static event Action<MoveMadeMessage, IReadOnlyList<RelayList>> OnMoveMade;
        public static event Action<TurnChangedMessage> OnTurnChanged;
        public static event Action<VoteFailedMessage> OnVoteFailed;
        public static event Action<GameEndedMessage> OnGameEnded;
        public static event Action<ErrorMessage> OnError;
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action OnSyncComplete;
        public static event Action<RelayList, ListChangeType> OnListChanged;

        private void Awake()
        {
            _commandQueue = new RelayCommandQueue(_validator);
            _transport = new RelayTransport(HandleConnected, HandleDisconnected, HandleTransportError, HandleIncomingMessage);
            _messageRouter = new RelayMessageRouter(_state, DispatchListChanged);
        }

        public static Task<bool> MatchWithAnyone(string playerId, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.Open(playerId), slug, items);
        }

        public static Task<bool> MatchWithAnyone(TurnKitPlayerSession session, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.Authenticated(session), slug, items);
        }

        public static Task<bool> MatchWithAnyone(TurnKitSignedPlayer player, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.Signed(player), slug, items);
        }

        private static async Task<bool> MatchWithAnyone(TurnKitClientIdentity identity, string slug, Dictionary<string, List<RelayItem>> items = null)
        {
            if (Registry.Initializers.TryGetValue(slug, out var initAction))
            {
                initAction.Invoke();
            }
            else
            {
                Debug.LogError($"[TurnKit] No config found for slug: {slug}");
                return false;
            }

            var itemsJson = BuildItemsJson(items);
            var body = $"{{\"slug\":\"{slug}\",\"items\":{itemsJson}}}";

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

        public static void SendJson(string json)
        {
            Instance._commandQueue.QueueJson(json);
        }

        public static void Commit()
        {
            Instance.ExecuteQueuedActions(false);
        }

        public static void EndMyTurn()
        {
            Instance.ExecuteQueuedActions(true);
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
            return LeaveQueue(TurnKitClientIdentity.Open(playerId), slug);
        }

        public static Task LeaveQueue(TurnKitPlayerSession session, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.Authenticated(session), slug);
        }

        public static Task LeaveQueue(TurnKitSignedPlayer player, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.Signed(player), slug);
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

        public void InitializeFromMetadata<T>(Dictionary<T, TurnKitConfig.RelayListConfig> metadata) where T : Enum
        {
            _state.InitializeFromMetadata(metadata);
        }

        public static bool IsReady => Instance._transport.IsConnected && !Instance._state.IsInSyncWindow;
        public static bool IsMyTurn => Instance._state.IsMyTurn;
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

        private void ExecuteQueuedActions(bool shouldEndTurn)
        {
            _validator.ValidateReadyToSend(_transport.IsConnected, _state.IsInSyncWindow);

            if (!_state.IsMyTurn)
            {
                Debug.LogError("[TurnKit] Not your turn. Actions not sent.");
                _commandQueue.Clear();
                return;
            }

            _transport.Send(_commandQueue.BuildMovePayload(shouldEndTurn));
            _commandQueue.Clear();
        }

        private void Update()
        {
            _transport?.Tick(Time.deltaTime);
            TickAutoReconnect(Time.deltaTime);
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
                        Debug.Log($"TurnKit - MoveMade #{outcome.MoveMade.moveNumber} from {outcome.MoveMade.playerId}");
                    }

                    break;
                case RelayEventType.SyncComplete:
                    OnSyncComplete?.Invoke();
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - Sync complete (move: {outcome.SyncComplete.moveNumber})");
                    }

                    break;
                case RelayEventType.TurnChanged:
                    OnTurnChanged?.Invoke(outcome.TurnChanged);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - TurnChanged: {outcome.TurnChanged.activePlayerId}");
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
                    if (ShouldReconnectOnError(outcome.Error))
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

        private static string BuildItemsJson(Dictionary<string, List<RelayItem>> items)
        {
            if (items == null)
            {
                return "{}";
            }

            var obj = new JSONObject();
            foreach (var kvp in items)
            {
                var arr = new JSONArray();
                foreach (var item in kvp.Value)
                {
                    var itemNode = new JSONObject();
                    itemNode["id"] = item.Id;
                    itemNode["slug"] = item.Slug;
                    arr.Add(itemNode);
                }

                obj.Add(kvp.Key, arr);
            }

            return obj.ToString();
        }

        private void HandleDisconnectInternal(string reason)
        {
            _state.MarkDisconnected();
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

        private void DisableReconnect()
        {
            _allowReconnect = false;
            _sessionTerminated = true;
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
