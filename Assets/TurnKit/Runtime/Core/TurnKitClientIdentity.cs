using System;

namespace TurnKit
{
    internal struct TurnKitClientIdentity
    {
        public string PlayerId { get; }
        public string PlayerToken { get; }
        public TurnKitSignedPlayer SignedPlayer { get; }
        public bool IsOpen => string.IsNullOrWhiteSpace(PlayerToken) && SignedPlayer == null;

        private TurnKitClientIdentity(string playerId, string playerToken, TurnKitSignedPlayer signedPlayer)
        {
            PlayerId = playerId ?? "";
            PlayerToken = playerToken ?? "";
            SignedPlayer = signedPlayer;
        }

        public static TurnKitClientIdentity Open(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("PlayerId is required.", nameof(playerId));
            }

            return new TurnKitClientIdentity(playerId, "", null);
        }

        public static TurnKitClientIdentity Authenticated(TurnKitPlayerSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (!session.IsAuthenticated)
            {
                throw new ArgumentException("Session must contain PlayerId and PlayerToken.", nameof(session));
            }

            return new TurnKitClientIdentity(session.PlayerId, session.PlayerToken, null);
        }

        public static TurnKitClientIdentity Signed(TurnKitSignedPlayer player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (!player.IsValid)
            {
                throw new ArgumentException("Signed player must contain PlayerId, Timestamp, Nonce and Signature.", nameof(player));
            }

            return new TurnKitClientIdentity(player.PlayerId, "", player);
        }
    }
}
