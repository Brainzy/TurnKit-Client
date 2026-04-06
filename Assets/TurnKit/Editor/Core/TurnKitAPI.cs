using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit.Editor
{
    public static class TurnKitAPI
    {
        public const string AUTH_URL_SUFFIX = "/unity-auth";

        internal static string BaseUrl => TurnKitConfig.Instance.serverUrl.TrimEnd('/');

        // Request/Response DTOs
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
            public List<TurnKitConfig.RelayConfig> relayConfigs;
        }

        [Serializable]
        public class GameKeyInfo
        {
            public string id;
            public string name;
        }

        [Serializable]
        public class RelayConfigDto
        {
            public string id;
            public string slug;
            public int maxPlayers;
            public string turnEnforcement;
            public bool ignoreAllOwnership;
            public bool votingEnabled;
            public string votingMode;
            public int votesRequired;
            public int votesToFail;
            public string failAction;
            public int matchTimeoutMinutes;
            public int turnTimeoutSeconds;
            public int waitReconnectSeconds;
            public List<RelayListConfigDto> lists;
        }

        [Serializable]
        public class RelayListConfigDto
        {
            public string name;
            public string tag;
            public List<int> ownerSlots;
            public List<int> visibleToSlots;
        }

        [Serializable]
        public class RelayConfigListWrapper
        {
            public List<TurnKitConfig.RelayConfig> configs;
        }

        public static IEnumerator ExchangeAuthCode(string authCode, Action<AuthResponse> onSuccess, Action<string> onError)
        {
            var request = new UnityWebRequest($"{BaseUrl}/v1/dev/exchange-auth-code", "POST");

            var requestData = new ExchangeAuthCodeRequest {authCode = authCode};
            var json = JsonUtility.ToJson(requestData);

            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler.text;

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<AuthResponse>(responseText);
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TurnKit API] JSON parsing failed: {e.Message}");
                    Debug.LogError($"[TurnKit API] Stack trace: {e.StackTrace}");
                    onError?.Invoke($"Failed to parse response: {e.Message}");
                }
            }
            else
            {
                onError?.Invoke($"Request failed: {request.error}\nCode: {request.responseCode}\nResponse: {responseText}");
            }
        }

        public static IEnumerator FetchRelayConfigs(string gameId,
            string jwt,
            Action<List<TurnKitConfig.RelayConfig>> onSuccess,
            Action<string> onError)
        {
            var req = UnityWebRequest.Get($"{BaseUrl}/v1/dev/relay-configs?gameKeyId={gameId}");
            return SendRequest(req, jwt, (json) =>
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<RelayConfigListWrapper>("{\"configs\":" + json + "}");
                    onSuccess?.Invoke(wrapper.configs);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"FetchRelayConfigs Parse Error: {e.Message}");
                }
            }, onError);
        }

        public static IEnumerator PushRelayConfig(string gameKeyId,
            TurnKitConfig.RelayConfig relay,
            string jwt,
            Action<TurnKitConfig.RelayConfig> onSuccess,
            Action<string> onError)
        {
            var dto = new RelayConfigDto
            {
                id = relay.id,
                slug = relay.slug,
                maxPlayers = relay.maxPlayers,
                turnEnforcement = relay.turnEnforcement.ToString(),
                ignoreAllOwnership = relay.ignoreAllOwnership,
                votingEnabled = relay.votingEnabled,
                votingMode = relay.votingMode.ToString(),
                votesRequired = relay.votesRequired,
                votesToFail = relay.votesToFail,
                failAction = relay.failAction.ToString(),
                matchTimeoutMinutes = relay.matchTimeoutMinutes,
                turnTimeoutSeconds = relay.turnTimeoutSeconds,
                waitReconnectSeconds = relay.waitReconnectSeconds,
                lists = relay.lists.ConvertAll(list => new RelayListConfigDto
                {
                    name = list.name,
                    tag = list.tag,
                    ownerSlots = list.ownerSlots.ConvertAll(slot => (int) slot),
                    visibleToSlots = list.visibleToSlots.ConvertAll(slot => (int) slot)
                })
            };

            string jsonBody = JsonUtility.ToJson(dto);
            string url = string.IsNullOrEmpty(relay.id)
                ? $"{BaseUrl}/v1/dev/relay-configs?gameKeyId={gameKeyId}"
                : $"{BaseUrl}/v1/dev/relay-configs/{UnityWebRequest.EscapeURL(relay.slug)}?gameKeyId={gameKeyId}";

            string method = string.IsNullOrEmpty(relay.id) ? "POST" : "PUT";
            var request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            return SendRequest(request, jwt, (responseText) =>
            {
                try
                {
                    var responseDto = JsonUtility.FromJson<RelayConfigDto>(responseText);
                    var updatedRelay = new TurnKitConfig.RelayConfig
                    {
                        id = responseDto.id,
                        slug = responseDto.slug,
                        maxPlayers = responseDto.maxPlayers,
                        turnEnforcement = (TurnKitConfig.TurnEnforcement) Enum.Parse(typeof(TurnKitConfig.TurnEnforcement), responseDto.turnEnforcement),
                        ignoreAllOwnership = responseDto.ignoreAllOwnership,
                        votingEnabled = responseDto.votingEnabled,
                        votingMode = (TurnKitConfig.VotingMode) Enum.Parse(typeof(TurnKitConfig.VotingMode), responseDto.votingMode),
                        votesRequired = responseDto.votesRequired,
                        votesToFail = responseDto.votesToFail,
                        failAction = (TurnKitConfig.FailAction) Enum.Parse(typeof(TurnKitConfig.FailAction), responseDto.failAction),
                        matchTimeoutMinutes = responseDto.matchTimeoutMinutes,
                        turnTimeoutSeconds = responseDto.turnTimeoutSeconds,
                        waitReconnectSeconds = responseDto.waitReconnectSeconds,
                        lists = responseDto.lists.ConvertAll(listDto => new TurnKitConfig.RelayListConfig
                        {
                            name = listDto.name,
                            ownerSlots = listDto.ownerSlots.ConvertAll(slot => (TurnKitConfig.PlayerSlot) slot),
                            visibleToSlots = listDto.visibleToSlots.ConvertAll(slot => (TurnKitConfig.PlayerSlot) slot)
                        })
                    };

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

        public static IEnumerator DeleteRelayConfig(string gameKeyId, string slug, string jwt,
            Action onSuccess, Action<string> onError)
        {
            Debug.Log($"[TurnKit API] Deleting relay config: {slug}");
            string url = $"{BaseUrl}/v1/dev/relay-configs/{UnityWebRequest.EscapeURL(slug)}?gameKeyId={gameKeyId}";
            var request = UnityWebRequest.Delete(url);
            return SendRequest(request, jwt, 
                _ => {
                    Debug.Log($"[TurnKit API] Successfully deleted: {slug}");
                    onSuccess?.Invoke();
                }, 
                onError
            );
        }

        private static IEnumerator SendRequest(UnityWebRequest req, string jwt, Action<string> onSuccess, Action<string> onError)
        {
            using (req)
            {
                if (!string.IsNullOrEmpty(jwt))
                    req.SetRequestHeader("Authorization", $"Bearer {jwt}");

                var op = req.SendWebRequest();
                while (!op.isDone) yield return null;

                if (req.result == UnityWebRequest.Result.Success)
                    onSuccess?.Invoke(req.downloadHandler?.text);
                else
                    onError?.Invoke(req.error);
            }
        }
    }
}
