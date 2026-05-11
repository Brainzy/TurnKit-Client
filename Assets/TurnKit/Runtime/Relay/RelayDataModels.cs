using System;

namespace TurnKit
{
    [Serializable]
    internal sealed class RelayQueueResponse
    {
        public string relayToken;
        public string sessionId;
        public int slot;
    }

    internal sealed class RelayReconnectSnapshot
    {
        public RelayReconnectSnapshot(string playerId, string slug, string relayToken, int lastMoveNumber)
        {
            PlayerId = playerId;
            Slug = slug;
            RelayToken = relayToken;
            LastMoveNumber = lastMoveNumber;
        }

        public string PlayerId { get; }
        public string Slug { get; }
        public string RelayToken { get; }
        public int LastMoveNumber { get; }
    }
}
