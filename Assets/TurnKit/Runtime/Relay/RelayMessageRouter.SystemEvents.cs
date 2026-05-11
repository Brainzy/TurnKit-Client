using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    internal sealed partial class RelayMessageRouter
    {
        private static RelayMessageOutcome HandleVoteFailed(JSONNode node)
        {
            return new RelayMessageOutcome
            {
                EventType = RelayEventType.VoteFailed,
                VoteFailed = ParseVoteFailed(node)
            };
        }

        private static RelayMessageOutcome HandleError(JSONNode node)
        {
            return new RelayMessageOutcome
            {
                EventType = RelayEventType.Error,
                Error = ParseError(node)
            };
        }

        private static RelayMessageOutcome HandleGameEnded(JSONNode node)
        {
            return new RelayMessageOutcome
            {
                EventType = RelayEventType.GameEnded,
                GameEnded = ParseGameEnded(node)
            };
        }
    }
}
