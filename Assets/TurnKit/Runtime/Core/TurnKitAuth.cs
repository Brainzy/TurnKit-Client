using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TurnKit
{
    public static class TurnKitAuth
    {
        public static async Task RequestOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            using var request = TurnKitClientRequest.CreateJson("/v1/client/auth/otp/request", "POST", JsonUtility.ToJson(new OtpRequest(email)));
            await TurnKitClientRequest.Send(request);
        }

        public static async Task<TurnKitPlayerSession> VerifyOtp(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                throw new ArgumentException("Otp is required.", nameof(otp));
            }

            using var request = TurnKitClientRequest.CreateJson("/v1/client/auth/otp/verify", "POST", JsonUtility.ToJson(new OtpVerifyRequest(email, otp)));
            var response = await TurnKitClientRequest.SendJson<OtpVerifyResponse>(request);
            return new TurnKitPlayerSession(response.playerId, response.token, email);
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
    }
}
