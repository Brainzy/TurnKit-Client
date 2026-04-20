using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit
{
    internal static class TurnKitClientRequest
    {
        private const int TimeoutSeconds = 10;

        public static UnityWebRequest CreateJson(string path, string method, string json)
        {
            var request = new UnityWebRequest(BuildUrl(path), method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json ?? "{}")),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {TurnKitConfig.Instance.clientKey}");
            return request;
        }

        public static UnityWebRequest Create(string path, string method)
        {
            var request = new UnityWebRequest(BuildUrl(path), method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };
            request.SetRequestHeader("Authorization", $"Bearer {TurnKitConfig.Instance.clientKey}");
            return request;
        }

        public static UnityWebRequest CreateGet(string path)
        {
            var request = UnityWebRequest.Get(BuildUrl(path));
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Authorization", $"Bearer {TurnKitConfig.Instance.clientKey}");
            return request;
        }

        public static async Task PrepareIdentity(UnityWebRequest request, TurnKitClientIdentity identity)
        {
            if (identity.IsNoAuth)
            {
                request.SetRequestHeader("X-Player-Id", identity.PlayerId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(identity.PlayerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {identity.PlayerToken}");
                return;
            }

            var token = await ExchangeYourBackendProof(identity.YourBackendProof);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        public static async Task Send(UnityWebRequest request)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"TurnKit [{request.responseCode}]: {request.downloadHandler.text}");
            }
        }

        public static async Task<T> SendJson<T>(UnityWebRequest request) where T : new()
        {
            await Send(request);
            return JsonUtility.FromJson<T>(request.downloadHandler.text) ?? new T();
        }

        private static string BuildUrl(string path)
        {
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return $"{TurnKitConfig.Instance.serverUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }

        private static async Task<string> ExchangeYourBackendProof(TurnKitYourBackendProof proof)
        {
            using var request = CreateJson(
                "/v1/client/auth/your-backend/exchange",
                "POST",
                JsonUtility.ToJson(new YourBackendExchangeRequest(proof)));
            var response = await SendJson<SignedExchangeResponse>(request);

            if (string.IsNullOrWhiteSpace(response.token))
            {
                throw new Exception("TurnKit YOUR_BACKEND exchange did not return a player token.");
            }

            return response.token;
        }

        [Serializable]
        private sealed class YourBackendExchangeRequest
        {
            public string playerId;
            public string timestamp;
            public string nonce;
            public string signature;

            public YourBackendExchangeRequest(TurnKitYourBackendProof proof)
            {
                playerId = proof.PlayerId;
                timestamp = proof.Timestamp;
                nonce = proof.Nonce;
                signature = proof.Signature;
            }
        }

        [Serializable]
        private sealed class SignedExchangeResponse
        {
            public string token;
        }
    }
}
