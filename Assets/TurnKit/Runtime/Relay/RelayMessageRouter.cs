using System;
using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    internal sealed partial class RelayMessageRouter
    {
        private readonly RelaySessionState _state;
        private readonly Action<RelayList, ListChangeType> _notifyListChanged;

        public RelayMessageRouter(RelaySessionState state, Action<RelayList, ListChangeType> notifyListChanged)
        {
            _state = state;
            _notifyListChanged = notifyListChanged;
        }

        public RelayMessageOutcome Process(string raw)
        {
            var node = JSON.Parse(raw);
            string type = node["type"];
            if (string.IsNullOrEmpty(type))
            {
                return null;
            }

            switch (type)
            {
                case "MATCH_STARTED":
                    return HandleMatchStarted(node);
                case "MOVE_MADE":
                    return HandleMoveMade(node);
                case "SYNC_COMPLETE":
                    return HandleSyncComplete(raw);
                case "TURN_STARTED":
                    return HandleTurnStarted(raw);
                case "MOVE_REQUESTED_FOR_PLAYER":
                    return HandleMoveRequestedForPlayer(node);
                case "VOTE_FAILED":
                    return HandleVoteFailed(node);
                case "ERROR":
                    return HandleError(node);
                case "GAME_ENDED":
                    return HandleGameEnded(node);
                default:
                    return null;
            }
        }
    }
}
