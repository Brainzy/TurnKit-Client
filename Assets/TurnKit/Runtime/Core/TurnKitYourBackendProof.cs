using System;
using System.Text.RegularExpressions;

namespace TurnKit
{
    [Serializable]
    public sealed class TurnKitYourBackendProof
    {
        private static readonly Regex NoncePattern = new("^[A-Za-z0-9_-]{16,128}$", RegexOptions.Compiled);

        public string PlayerId { get; }
        public string Timestamp { get; }
        public string Nonce { get; }
        public string Signature { get; }
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(PlayerId) &&
            !string.IsNullOrWhiteSpace(Timestamp) &&
            NoncePattern.IsMatch(Nonce ?? "") &&
            !string.IsNullOrWhiteSpace(Signature);

        public TurnKitYourBackendProof(string playerId, string timestamp, string nonce, string signature)
        {
            PlayerId = playerId ?? "";
            Timestamp = timestamp ?? "";
            Nonce = nonce ?? "";
            Signature = signature ?? "";
        }

        public string BuildSignaturePayload()
        {
            return $"{PlayerId}\n{Timestamp}\n{Nonce}";
        }
    }
}
