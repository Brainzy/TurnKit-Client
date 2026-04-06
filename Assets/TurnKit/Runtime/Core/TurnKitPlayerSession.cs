using System;

namespace TurnKit
{
    [Serializable]
    public sealed class TurnKitPlayerSession
    {
        public string PlayerId { get; }
        public string PlayerToken { get; }
        public string Email { get; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(PlayerId) && !string.IsNullOrWhiteSpace(PlayerToken);

        public TurnKitPlayerSession(string playerId, string playerToken, string email = null)
        {
            PlayerId = playerId ?? "";
            PlayerToken = playerToken ?? "";
            Email = email ?? playerId ?? "";
        }
    }
}
