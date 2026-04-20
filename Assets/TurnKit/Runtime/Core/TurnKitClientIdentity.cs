using System;

namespace TurnKit
{
    internal struct TurnKitClientIdentity
    {
        public string PlayerId { get; }
        public string PlayerToken { get; }
        public TurnKitYourBackendProof YourBackendProof { get; }
        public bool IsNoAuth => string.IsNullOrWhiteSpace(PlayerToken) && YourBackendProof == null;

        private TurnKitClientIdentity(string playerId, string playerToken, TurnKitYourBackendProof yourBackendProof)
        {
            PlayerId = playerId ?? "";
            PlayerToken = playerToken ?? "";
            YourBackendProof = yourBackendProof;
        }

        public static TurnKitClientIdentity NoAuth(string playerId)
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

        public static TurnKitClientIdentity YourBackend(TurnKitYourBackendProof proof)
        {
            if (proof == null)
            {
                throw new ArgumentNullException(nameof(proof));
            }

            if (!proof.IsValid)
            {
                throw new ArgumentException("YOUR_BACKEND proof must contain PlayerId, Timestamp, Nonce and Signature.", nameof(proof));
            }

            return new TurnKitClientIdentity(proof.PlayerId, "", proof);
        }

    }
}
