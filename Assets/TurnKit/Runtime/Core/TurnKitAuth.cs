using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TurnKit
{
    public static class TurnKitAuth
    {
        public static async Task RequestEmailOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            using var request = TurnKitClientRequest.CreateJson("/v1/client/auth/email-otp/request", "POST", JsonUtility.ToJson(new OtpRequest(email)));
            await TurnKitClientRequest.Send(request);
        }

        public static async Task<TurnKitPlayerSession> VerifyEmailOtp(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                throw new ArgumentException("OTP is required.", nameof(otp));
            }

            using var request = TurnKitClientRequest.CreateJson("/v1/client/auth/email-otp/verify", "POST", JsonUtility.ToJson(new OtpVerifyRequest(email, otp)));
            var response = await TurnKitClientRequest.SendJson<OtpVerifyResponse>(request);
            return new TurnKitPlayerSession(response.playerId, response.token, email);
        }

        public static Task<TurnKitPlayerSession> ExchangeUgs(string idToken)
        {
            return ExchangeUgs(idToken, null);
        }

        public static async Task<TurnKitPlayerSession> ExchangeUgs(string idToken, string serverProof)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new ArgumentException("UGS idToken is required.", nameof(idToken));
            }

            using var request = TurnKitClientRequest.CreateJson(
                "/v1/client/auth/ugs/exchange",
                "POST",
                JsonUtility.ToJson(new UgsExchangeRequest(idToken, serverProof)));
            var response = await TurnKitClientRequest.SendJson<UgsExchangeResponse>(request);

            if (string.IsNullOrWhiteSpace(response.token))
            {
                throw new Exception("TurnKit UGS exchange did not return a player token.");
            }

            if (string.IsNullOrWhiteSpace(response.playerId))
            {
                throw new Exception("TurnKit UGS exchange did not return a player id.");
            }

            return new TurnKitPlayerSession(response.playerId, response.token);
        }

        [Serializable]
        private sealed class OtpRequest
        {
            public string email;

            public OtpRequest(string email)
            {
                this.email = email;
            }
        }

        [Serializable]
        private sealed class OtpVerifyRequest
        {
            public string email;
            public string otp;

            public OtpVerifyRequest(string email, string otp)
            {
                this.email = email;
                this.otp = otp;
            }
        }

        [Serializable]
        private sealed class OtpVerifyResponse
        {
            public string token;
            public string playerId;
        }

        [Serializable]
        private sealed class UgsExchangeRequest
        {
            public string idToken;
            public string serverProof;

            public UgsExchangeRequest(string idToken, string serverProof)
            {
                this.idToken = idToken;
                this.serverProof = serverProof;
            }
        }

        [Serializable]
        private sealed class UgsExchangeResponse
        {
            public string token;
            public string playerId;
        }
    }
}
