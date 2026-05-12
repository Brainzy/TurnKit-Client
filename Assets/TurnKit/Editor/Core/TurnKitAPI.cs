using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit.Editor
{
    public static partial class TurnKitAPI
    {
        public const string AUTH_URL_SUFFIX = "/unity-auth";

        internal static string BaseUrl => TurnKitConfig.Instance.serverUrl.TrimEnd('/');

        [Serializable]
        public class ExchangeAuthCodeRequest
        {
            public string authCode;
        }

        [Serializable]
        public class AuthResponse
        {
            public string jwt;
            public string email;
            public GameKeyInfo gameKey;
            public string selectedClientKey;
            public List<TurnKitConfig.LeaderboardConfig> leaderboards;
            public List<TurnKitConfig.RelayConfig> relayConfigs;
            public List<TurnKitConfig.PlayerStoreDefConfig> playerStoreDefs;
            public TurnKitConfig.PlayerAuthPolicy playerAuthPolicy;
            public List<TurnKitConfig.PlayerAuthMethod> playerAuthMethods;
        }

        [Serializable]
        public class GameKeyInfo
        {
            public string id;
            public string name;
        }

        public static IEnumerator ExchangeAuthCode(string authCode, Action<AuthResponse> onSuccess, Action<string> onError)
        {
            var request = new UnityWebRequest($"{BaseUrl}/v1/dev/exchange-auth-code", "POST");
            var requestData = new ExchangeAuthCodeRequest { authCode = authCode };
            var json = JsonUtility.ToJson(requestData);

            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Request failed: {request.error}\nCode: {request.responseCode}\nResponse: {responseText}");
                yield break;
            }

            try
            {
                onSuccess?.Invoke(ParseAuthResponse(responseText));
            }
            catch (Exception e)
            {
                Debug.LogError($"[TurnKit API] JSON parsing failed: {e.Message}");
                Debug.LogError($"[TurnKit API] Stack trace: {e.StackTrace}");
                onError?.Invoke($"Failed to parse response: {e.Message}");
            }
        }

        internal static AuthResponse ParseAuthResponse(string json)
        {
            var node = JSON.Parse(json).AsObject;
            JSONNode root = node;
            JSONNode dashboard = node["dashboard"];
            JSONNode source = dashboard != null && !dashboard.IsNull ? dashboard : root;
            JSONNode gameNode = source["game"];
            JSONNode gameKeyNode = gameNode != null && !gameNode.IsNull ? gameNode : source["gameKey"];
            JSONNode authNode = source["auth"];

            return new AuthResponse
            {
                jwt = node["jwt"],
                email = node["email"],
                selectedClientKey = node["selectedClientKey"],
                gameKey = new GameKeyInfo
                {
                    id = gameKeyNode["id"],
                    name = gameKeyNode["name"]
                },
                leaderboards = ParseLeaderboardConfigList(source["leaderboards"]),
                relayConfigs = ParseRelayConfigList(source["relayConfigs"].ToString()),
                playerStoreDefs = ParsePlayerStoreDefsNode(source["playerStoreDefs"]),
                playerAuthPolicy = ParseNullableEnum(gameNode["playerAuthPolicy"], ParseNullableEnum(authNode["policy"], TurnKitConfig.PlayerAuthPolicy.NO_AUTH)),
                playerAuthMethods = ParseAuthMethodList(authNode["methods"])
            };
        }

        public static IEnumerator FetchRelayConfigs(
            string gameId,
            string jwt,
            Action<List<TurnKitConfig.RelayConfig>> onSuccess,
            Action<string> onError)
        {
            var req = UnityWebRequest.Get($"{BaseUrl}/v1/dev/relay-configs?gameKeyId={gameId}");
            return SendRequest(req, jwt, json =>
            {
                try
                {
                    onSuccess?.Invoke(ParseRelayConfigList(json));
                }
                catch (Exception e)
                {
                    onError?.Invoke($"FetchRelayConfigs Parse Error: {e.Message}");
                }
            }, onError);
        }

        public static IEnumerator PushRelayConfig(
            string gameKeyId,
            TurnKitConfig.RelayConfig relay,
            string jwt,
            Action<TurnKitConfig.RelayConfig> onSuccess,
            Action<string> onError)
        {
            string jsonBody = BuildRelayConfigJson(relay, includeSlug: string.IsNullOrEmpty(relay.id));
            string url = string.IsNullOrEmpty(relay.id)
                ? $"{BaseUrl}/v1/dev/relay-configs?gameKeyId={gameKeyId}"
                : $"{BaseUrl}/v1/dev/relay-configs/{UnityWebRequest.EscapeURL(relay.slug)}?gameKeyId={gameKeyId}";

            string method = string.IsNullOrEmpty(relay.id) ? "POST" : "PUT";
            var request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[TurnKit API] PushRelayConfig {method} {url}\n{jsonBody}");

            return SendRequest(request, jwt, responseText =>
            {
                try
                {
                    var updatedRelay = ParseRelayConfig(JSON.Parse(responseText));
                    Debug.Log($"[TurnKit API] Successfully pushed: {updatedRelay.slug}");
                    onSuccess?.Invoke(updatedRelay);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TurnKit API] Parse error: {e.Message}");
                    onError?.Invoke($"Failed to parse response: {e.Message}");
                }
            }, onError);
        }

        public static IEnumerator DeleteRelayConfig(
            string gameKeyId,
            string slug,
            string jwt,
            Action onSuccess,
            Action<string> onError)
        {
            string url = $"{BaseUrl}/v1/dev/relay-configs/{UnityWebRequest.EscapeURL(slug)}?gameKeyId={gameKeyId}";
            var request = UnityWebRequest.Delete(url);
            return SendRequest(request, jwt, _ =>
            {
                Debug.Log($"[TurnKit API] Successfully deleted: {slug}");
                onSuccess?.Invoke();
            }, onError);
        }

        public static IEnumerator FetchWebhooks(
            string gameKeyId,
            string jwt,
            Action<List<TurnKitConfig.WebhookConfig>> onSuccess,
            Action<string> onError)
        {
            var req = UnityWebRequest.Get($"{BaseUrl}/v1/dev/webhooks?gameKeyId={gameKeyId}");
            return SendRequest(req, jwt, responseText =>
            {
                try
                {
                    onSuccess?.Invoke(ParseWebhookList(responseText));
                }
                catch (Exception e)
                {
                    onError?.Invoke($"FetchWebhooks Parse Error: {e.Message}");
                }
            }, onError);
        }

        public static IEnumerator CreateWebhook(
            string gameKeyId,
            TurnKitConfig.WebhookConfig webhook,
            string jwt,
            Action<TurnKitConfig.WebhookConfig> onSuccess,
            Action<string> onError)
        {
            return SendWebhookWrite(
                $"{BaseUrl}/v1/dev/webhooks?gameKeyId={gameKeyId}",
                "POST",
                BuildWebhookCreateJson(webhook),
                jwt,
                onSuccess,
                onError);
        }

        public static IEnumerator UpdateWebhook(
            string gameKeyId,
            TurnKitConfig.WebhookConfig webhook,
            string jwt,
            Action<TurnKitConfig.WebhookConfig> onSuccess,
            Action<string> onError)
        {
            return SendWebhookWrite(
                $"{BaseUrl}/v1/dev/webhooks/{UnityWebRequest.EscapeURL(webhook.id)}?gameKeyId={gameKeyId}",
                "PUT",
                BuildWebhookUpdateJson(webhook),
                jwt,
                onSuccess,
                onError);
        }

        public static IEnumerator DeleteWebhook(
            string gameKeyId,
            string webhookId,
            string jwt,
            Action onSuccess,
            Action<string> onError)
        {
            var request = UnityWebRequest.Delete($"{BaseUrl}/v1/dev/webhooks/{UnityWebRequest.EscapeURL(webhookId)}?gameKeyId={gameKeyId}");
            return SendRequest(request, jwt, _ => onSuccess?.Invoke(), onError);
        }

        public static IEnumerator FetchPlayerStoreDefs(
            string gameKeyId,
            string jwt,
            Action<List<TurnKitConfig.PlayerStoreDefConfig>> onSuccess,
            Action<string> onError)
        {
            var req = UnityWebRequest.Get($"{BaseUrl}/v1/dev/player-store-defs?gameKeyId={gameKeyId}");
            return SendRequest(req, jwt, responseText =>
            {
                try
                {
                    onSuccess?.Invoke(ParsePlayerStoreDefList(responseText));
                }
                catch (Exception e)
                {
                    onError?.Invoke($"FetchPlayerStoreDefs Parse Error: {e.Message}");
                }
            }, onError);
        }

        public static IEnumerator CreatePlayerStoreDef(
            string gameKeyId,
            TurnKitConfig.PlayerStoreDefConfig def,
            string jwt,
            Action<TurnKitConfig.PlayerStoreDefConfig> onSuccess,
            Action<string> onError)
        {
            var request = new UnityWebRequest($"{BaseUrl}/v1/dev/player-store-defs?gameKeyId={gameKeyId}", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(BuildPlayerStoreDefJson(def)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            return SendRequest(request, jwt, responseText =>
            {
                try
                {
                    onSuccess?.Invoke(ParsePlayerStoreDef(JSON.Parse(responseText)));
                }
                catch (Exception e)
                {
                    onError?.Invoke($"CreatePlayerStoreDef Parse Error: {e.Message}");
                }
            }, onError);
        }

        public static IEnumerator DeletePlayerStoreDef(
            string gameKeyId,
            string storeKey,
            string jwt,
            Action onSuccess,
            Action<string> onError)
        {
            var request = UnityWebRequest.Delete($"{BaseUrl}/v1/dev/player-store-defs/{UnityWebRequest.EscapeURL(storeKey)}?gameKeyId={gameKeyId}");
            return SendRequest(request, jwt, _ => onSuccess?.Invoke(), onError);
        }


    }
}

