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
            var response = await TurnKitClientRequest.SendJson<PlayerAuthResponse>(request);
            return CreateSession(response, email);
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
            var response = await TurnKitClientRequest.SendJson<PlayerAuthResponse>(request);

            return CreateSession(response);
        }

        public static async Task<TurnKitPlayerSession> ExchangeGooglePlayGames(string serverAuthCode)
        {
            if (string.IsNullOrWhiteSpace(serverAuthCode))
            {
                throw new ArgumentException("Google Play Games serverAuthCode is required.", nameof(serverAuthCode));
            }

            using var request = TurnKitClientRequest.CreateJson(
                "/v1/client/auth/google-play/exchange",
                "POST",
                JsonUtility.ToJson(new GooglePlayExchangeRequest(serverAuthCode)));
            var response = await TurnKitClientRequest.SendJson<PlayerAuthResponse>(request);

            return CreateSession(response);
        }

        public static async Task<TurnKitPlayerSession> RefreshPlayer(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
            }

            using var request = TurnKitClientRequest.CreateJson(
                "/v1/client/auth/refresh",
                "POST",
                JsonUtility.ToJson(new RefreshRequest(refreshToken)));
            var response = await TurnKitClientRequest.SendJson<PlayerAuthResponse>(request);

            return CreateSession(response);
        }

        public static async Task Logout(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
            }

            using var request = TurnKitClientRequest.CreateJson(
                "/v1/client/auth/logout",
                "POST",
                JsonUtility.ToJson(new RefreshRequest(refreshToken)));
            await TurnKitClientRequest.Send(request);
        }

        private static TurnKitPlayerSession CreateSession(PlayerAuthResponse response, string email = null)
        {
            if (string.IsNullOrWhiteSpace(response.token))
            {
                throw new Exception("TurnKit auth exchange did not return a player token.");
            }

            if (string.IsNullOrWhiteSpace(response.playerId))
            {
                throw new Exception("TurnKit auth exchange did not return a player id.");
            }

            return new TurnKitPlayerSession(response.playerId, response.token, response.refreshToken, email);
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
        private sealed class PlayerAuthResponse
        {
            public string token;
            public string playerId;
            public string refreshToken;
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
        private sealed class GooglePlayExchangeRequest
        {
            public string serverAuthCode;

            public GooglePlayExchangeRequest(string serverAuthCode)
            {
                this.serverAuthCode = serverAuthCode;
            }
        }

        [Serializable]
        private sealed class RefreshRequest
        {
            public string refreshToken;

            public RefreshRequest(string refreshToken)
            {
                this.refreshToken = refreshToken;
            }
        }
    }
}
