using System;

namespace TurnKit
{
    [Serializable]
    public sealed class TurnKitPlayerSession
    {
        public string PlayerId { get; }
        public string PlayerToken { get; }
        public string RefreshToken { get; }
        public string Email { get; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(PlayerId) && !string.IsNullOrWhiteSpace(PlayerToken);
        public bool HasRefreshToken => !string.IsNullOrWhiteSpace(RefreshToken);

        public TurnKitPlayerSession(string playerId, string playerToken, string email = null)
            : this(playerId, playerToken, null, email)
        {
        }

        public TurnKitPlayerSession(string playerId, string playerToken, string refreshToken, string email)
        {
            PlayerId = playerId ?? "";
            PlayerToken = playerToken ?? "";
            RefreshToken = refreshToken ?? "";
            Email = email ?? playerId ?? "";
        }
    }
}
