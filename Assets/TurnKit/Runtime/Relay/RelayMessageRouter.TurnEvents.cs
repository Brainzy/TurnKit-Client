using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    internal sealed partial class RelayMessageRouter
    {
        private RelayMessageOutcome HandleSyncComplete(string raw)
        {
            var node = JSON.Parse(raw);
            var msg = new SyncCompleteMessage
            {
                type = node["type"],
                moveNumber = GetMoveNumber(node),
                serverNowUtcMs = ReadOptionalLong(node, "serverNowUtcMs"),
                timerEndUtcMs = ReadOptionalNullableLong(node, "timerEndUtcMs")
            };
            _state.ApplySyncComplete(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.SyncComplete,
                SyncComplete = msg
            };
        }

        private RelayMessageOutcome HandleTurnStarted(string raw)
        {
            var node = JSON.Parse(raw);
            var msg = new TurnStartedMessage
            {
                type = node["type"],
                activePlayerId = node["activePlayer"],
                turnTimerKind = ParseTurnTimerKind(node),
                turnTimerSeconds = ParseTurnTimerSeconds(node),
                moveNumber = GetMoveNumber(node),
                serverNowUtcMs = ReadOptionalLong(node, "serverNowUtcMs"),
                timerEndUtcMs = ReadOptionalNullableLong(node, "timerEndUtcMs")
            };
            _state.ApplyTurnStarted(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.TurnStarted,
                TurnStarted = msg
            };
        }

        private RelayMessageOutcome HandleMoveRequestedForPlayer(JSONNode node)
        {
            string playerId = node["playerId"];
            var lists = ParsePrivateListReveals(node["lists"]);
            int moveNumber = GetMoveNumber(node);
            var updatedLists = _state.ApplyMoveRequestedForPlayer(playerId, lists, moveNumber, _notifyListChanged);

            var msg = new MoveRequestedForPlayerMessage
            {
                type = node["type"],
                playerSlot = _state.ResolvePlayerSlot(playerId),
                updatedLists = updatedLists,
                moveNumber = moveNumber,
                serverNowUtcMs = ReadOptionalLong(node, "serverNowUtcMs"),
                timerEndUtcMs = ReadOptionalNullableLong(node, "timerEndUtcMs")
            };

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.MoveRequestedForPlayer,
                MoveRequestedForPlayer = msg
            };
        }
    }
}
