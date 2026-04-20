using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit.Editor
{
    public static class TurnKitAPI
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

        private static IEnumerator SendWebhookWrite(
            string url,
            string method,
            string body,
            string jwt,
            Action<TurnKitConfig.WebhookConfig> onSuccess,
            Action<string> onError)
        {
            var request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            return SendRequest(request, jwt, responseText =>
            {
                try
                {
                    onSuccess?.Invoke(ParseWebhook(JSON.Parse(responseText)));
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Webhook Parse Error: {e.Message}");
                }
            }, onError);
        }

        private static IEnumerator SendRequest(UnityWebRequest req, string jwt, Action<string> onSuccess, Action<string> onError)
        {
            using (req)
            {
                if (!string.IsNullOrEmpty(jwt))
                {
                    req.SetRequestHeader("Authorization", $"Bearer {jwt}");
                }

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    yield return null;
                }

                if (req.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(req.downloadHandler?.text ?? string.Empty);
                }
                else
                {
                    string details = string.IsNullOrEmpty(req.downloadHandler?.text)
                        ? req.error
                        : $"{req.error}\nCode: {req.responseCode}\nResponse: {req.downloadHandler.text}";
                    onError?.Invoke(details);
                }
            }
        }

        private static List<TurnKitConfig.RelayConfig> ParseRelayConfigList(string json)
        {
            var list = new List<TurnKitConfig.RelayConfig>();
            var array = JSON.Parse(json).AsArray;
            if (array == null)
            {
                return list;
            }

            foreach (JSONNode item in array)
            {
                list.Add(ParseRelayConfig(item));
            }

            return list;
        }

        private static List<TurnKitConfig.LeaderboardConfig> ParseLeaderboardConfigList(JSONNode node)
        {
            var list = new List<TurnKitConfig.LeaderboardConfig>();
            var array = node?.AsArray;
            if (array == null)
            {
                return list;
            }

            foreach (JSONNode item in array)
            {
                list.Add(new TurnKitConfig.LeaderboardConfig
                {
                    slug = item["slug"],
                    displayName = item["displayName"],
                    sortOrder = item["sortOrder"],
                    scoreStrategy = item["scoreStrategy"],
                    minScore = item["minScore"].IsNull ? 0d : item["minScore"].AsDouble,
                    maxScore = item["maxScore"].IsNull ? 0d : item["maxScore"].AsDouble,
                    resetFrequency = item["resetFrequency"],
                    archiveOnReset = item["archiveOnReset"].AsBool,
                    nextResetAt = item["nextResetAt"].IsNull ? null : item["nextResetAt"].Value
                });
            }

            return list;
        }

        private static TurnKitConfig.RelayConfig ParseRelayConfig(JSONNode node)
        {
            var relay = new TurnKitConfig.RelayConfig
            {
                id = node["id"],
                slug = node["slug"],
                maxPlayers = node["maxPlayers"].AsInt,
                turnEnforcement = ParseEnum(node["turnEnforcement"], TurnKitConfig.TurnEnforcement.ROUND_ROBIN),
                ignoreAllOwnership = node["ignoreAllOwnership"].AsBool,
                votingEnabled = node["votingEnabled"].AsBool,
                votingMode = ParseNullableEnum(node["votingMode"], TurnKitConfig.VotingMode.SYNC),
                votesRequired = node["votesRequired"].IsNull ? 0 : node["votesRequired"].AsInt,
                votesToFail = node["votesToFail"].IsNull ? 0 : node["votesToFail"].AsInt,
                failAction = ParseNullableEnum(node["failAction"], TurnKitConfig.FailAction.SKIP_TURN),
                matchTimeoutMinutes = node["matchTimeoutMinutes"].AsInt,
                turnTimeoutSeconds = node["turnTimeoutSeconds"].AsInt,
                waitReconnectSeconds = node["waitReconnectSeconds"].AsInt,
                lists = ParseRelayLists(node["lists"]),
                trackedStats = ParseTrackedStats(node["trackedStats"])
            };

            if (!relay.votingEnabled)
            {
                relay.votingMode = TurnKitConfig.VotingMode.SYNC;
                relay.votesRequired = 0;
                relay.votesToFail = 0;
                relay.failAction = TurnKitConfig.FailAction.SKIP_TURN;
            }

            return relay;
        }

        private static List<TurnKitConfig.RelayListConfig> ParseRelayLists(JSONNode node)
        {
            var lists = new List<TurnKitConfig.RelayListConfig>();
            var array = node.AsArray;
            if (array == null)
            {
                return lists;
            }

            foreach (JSONNode listNode in array)
            {
                lists.Add(new TurnKitConfig.RelayListConfig
                {
                    id = listNode["id"],
                    name = listNode["name"],
                    tag = listNode["tag"],
                    ownerSlots = ParseSlotList(listNode["ownerSlots"]),
                    visibleToSlots = ParseSlotList(listNode["visibleToSlots"])
                });
            }

            return lists;
        }

        private static List<TurnKitConfig.TrackedStatConfig> ParseTrackedStats(JSONNode node)
        {
            var stats = new List<TurnKitConfig.TrackedStatConfig>();
            var array = node.AsArray;
            if (array == null)
            {
                return stats;
            }

            foreach (JSONNode statNode in array)
            {
                var stat = new TurnKitConfig.TrackedStatConfig
                {
                    id = statNode["id"],
                    name = statNode["name"],
                    dataType = ParseEnum(statNode["dataType"], TurnKitConfig.TrackedStatDataType.DOUBLE),
                    scope = ParseEnum(statNode["scope"], TurnKitConfig.TrackedStatScope.MATCH),
                    syncTo = ParseSyncTargets(statNode["syncTo"])
                };

                ApplyInitialValue(stat, statNode["initialValue"]);
                stats.Add(stat);
            }

            return stats;
        }

        private static void ApplyInitialValue(TurnKitConfig.TrackedStatConfig stat, JSONNode node)
        {
            stat.initialDouble = 0d;
            stat.initialString = string.Empty;
            stat.initialList = new List<string>();

            switch (stat.dataType)
            {
                case TurnKitConfig.TrackedStatDataType.DOUBLE:
                    stat.initialDouble = node.AsDouble;
                    break;
                case TurnKitConfig.TrackedStatDataType.STRING:
                    stat.initialString = node.Value;
                    break;
                case TurnKitConfig.TrackedStatDataType.LIST_STRING:
                    var array = node.AsArray;
                    if (array != null)
                    {
                        foreach (JSONNode item in array)
                        {
                            stat.initialList.Add(item.Value);
                        }
                    }
                    break;
            }
        }

        private static List<TurnKitConfig.TrackedStatSyncTargetConfig> ParseSyncTargets(JSONNode node)
        {
            var syncTargets = new List<TurnKitConfig.TrackedStatSyncTargetConfig>();
            var array = node.AsArray;
            if (array == null)
            {
                return syncTargets;
            }

            foreach (JSONNode syncNode in array)
            {
                syncTargets.Add(new TurnKitConfig.TrackedStatSyncTargetConfig
                {
                    id = syncNode["id"],
                    destinationType = ParseEnum(syncNode["destinationType"], TurnKitConfig.TrackedStatSyncDestinationType.LEADERBOARD),
                    destinationId = syncNode["destinationId"]
                });
            }

            return syncTargets;
        }

        private static List<TurnKitConfig.PlayerSlot> ParseSlotList(JSONNode node)
        {
            var slots = new List<TurnKitConfig.PlayerSlot>();
            var array = node.AsArray;
            if (array == null)
            {
                return slots;
            }

            foreach (JSONNode slotNode in array)
            {
                slots.Add((TurnKitConfig.PlayerSlot)slotNode.AsInt);
            }

            return slots;
        }

        private static List<TurnKitConfig.WebhookConfig> ParseWebhookList(string json)
        {
            var webhooks = new List<TurnKitConfig.WebhookConfig>();
            var array = JSON.Parse(json).AsArray;
            if (array == null)
            {
                return webhooks;
            }

            foreach (JSONNode webhookNode in array)
            {
                webhooks.Add(ParseWebhook(webhookNode));
            }

            return webhooks;
        }

        private static List<TurnKitConfig.PlayerAuthMethod> ParseAuthMethodList(JSONNode node)
        {
            var methods = new List<TurnKitConfig.PlayerAuthMethod>();
            var array = node?.AsArray;
            if (array == null)
            {
                return methods;
            }

            foreach (JSONNode item in array)
            {
                methods.Add(ParseEnum(item.Value, TurnKitConfig.PlayerAuthMethod.YOUR_BACKEND));
            }

            return methods;
        }

        private static TurnKitConfig.WebhookConfig ParseWebhook(JSONNode node)
        {
            var webhook = new TurnKitConfig.WebhookConfig
            {
                entityId = node["entityId"],
                id = node["id"],
                url = node["url"],
                createdAt = node["createdAt"],
                updatedAt = node["updatedAt"]
            };

            var headersNode = node["headers"].AsObject;
            if (headersNode != null)
            {
                foreach (string key in headersNode.Keys)
                {
                    webhook.headers.Add(new TurnKitConfig.WebhookHeader
                    {
                        key = key,
                        value = headersNode[key]
                    });
                }
            }

            return webhook;
        }

        private static string BuildRelayConfigJson(TurnKitConfig.RelayConfig relay, bool includeSlug)
        {
            var node = new JSONObject();
            if (includeSlug)
            {
                node["slug"] = relay.slug;
            }

            node["maxPlayers"] = relay.maxPlayers;
            node["turnEnforcement"] = relay.turnEnforcement.ToString();
            node["ignoreAllOwnership"] = relay.ignoreAllOwnership;
            node["votingEnabled"] = relay.votingEnabled;
            node["votingMode"] = relay.votingEnabled ? relay.votingMode.ToString() : JSONNull.CreateOrGet();
            node["votesRequired"] = relay.votingEnabled ? relay.votesRequired : JSONNull.CreateOrGet();
            node["votesToFail"] = relay.votingEnabled ? relay.votesToFail : JSONNull.CreateOrGet();
            node["failAction"] = relay.votingEnabled ? relay.failAction.ToString() : JSONNull.CreateOrGet();
            node["matchTimeoutMinutes"] = relay.matchTimeoutMinutes;
            node["turnTimeoutSeconds"] = relay.turnTimeoutSeconds;
            node["waitReconnectSeconds"] = relay.waitReconnectSeconds;
            node["lists"] = BuildRelayListsNode(relay.lists);
            node["trackedStats"] = BuildTrackedStatsNode(relay.trackedStats);
            return node.ToString();
        }

        private static JSONNode BuildRelayListsNode(List<TurnKitConfig.RelayListConfig> lists)
        {
            var array = new JSONArray();
            if (lists == null)
            {
                return array;
            }

            foreach (var list in lists)
            {
                var node = new JSONObject
                {
                    ["name"] = list.name,
                    ["tag"] = list.tag,
                    ["ownerSlots"] = BuildSlotArray(list.ownerSlots),
                    ["visibleToSlots"] = BuildSlotArray(list.visibleToSlots)
                };
                array.Add(node);
            }

            return array;
        }

        private static JSONNode BuildTrackedStatsNode(List<TurnKitConfig.TrackedStatConfig> trackedStats)
        {
            var array = new JSONArray();
            if (trackedStats == null)
            {
                return array;
            }

            foreach (var stat in trackedStats)
            {
                var node = new JSONObject
                {
                    ["name"] = stat.name,
                    ["dataType"] = stat.dataType.ToString(),
                    ["initialValue"] = BuildInitialValueNode(stat),
                    ["scope"] = stat.scope.ToString(),
                    ["syncTo"] = BuildSyncTargetsNode(stat.syncTo)
                };
                array.Add(node);
            }

            return array;
        }

        private static JSONNode BuildInitialValueNode(TurnKitConfig.TrackedStatConfig stat)
        {
            switch (stat.dataType)
            {
                case TurnKitConfig.TrackedStatDataType.DOUBLE:
                    return new JSONNumber(stat.initialDouble);
                case TurnKitConfig.TrackedStatDataType.STRING:
                    return new JSONString(stat.initialString ?? string.Empty);
                case TurnKitConfig.TrackedStatDataType.LIST_STRING:
                    var array = new JSONArray();
                    if (stat.initialList != null)
                    {
                        foreach (var item in stat.initialList)
                        {
                            array.Add(item ?? string.Empty);
                        }
                    }

                    return array;
                default:
                    return JSONNull.CreateOrGet();
            }
        }

        private static JSONNode BuildSyncTargetsNode(List<TurnKitConfig.TrackedStatSyncTargetConfig> syncTargets)
        {
            var array = new JSONArray();
            if (syncTargets == null)
            {
                return array;
            }

            foreach (var sync in syncTargets)
            {
                var node = new JSONObject
                {
                    ["destinationType"] = sync.destinationType.ToString(),
                    ["destinationId"] = sync.destinationId
                };
                array.Add(node);
            }

            return array;
        }

        private static JSONNode BuildSlotArray(List<TurnKitConfig.PlayerSlot> slots)
        {
            var array = new JSONArray();
            if (slots == null)
            {
                return array;
            }

            foreach (var slot in slots)
            {
                array.Add((int)slot);
            }

            return array;
        }

        private static string BuildWebhookCreateJson(TurnKitConfig.WebhookConfig webhook)
        {
            var node = new JSONObject
            {
                ["id"] = webhook.id,
                ["url"] = webhook.url,
                ["headers"] = BuildHeadersNode(webhook.headers)
            };
            return node.ToString();
        }

        private static string BuildWebhookUpdateJson(TurnKitConfig.WebhookConfig webhook)
        {
            var node = new JSONObject
            {
                ["url"] = webhook.url,
                ["headers"] = BuildHeadersNode(webhook.headers)
            };
            return node.ToString();
        }

        private static JSONNode BuildHeadersNode(List<TurnKitConfig.WebhookHeader> headers)
        {
            var node = new JSONObject();
            if (headers == null)
            {
                return node;
            }

            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header?.key))
                {
                    continue;
                }

                node[header.key] = header.value ?? string.Empty;
            }

            return node;
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return Enum.TryParse(value, true, out TEnum parsed) ? parsed : fallback;
        }

        private static TEnum ParseNullableEnum<TEnum>(JSONNode node, TEnum fallback) where TEnum : struct
        {
            return node == null || node.IsNull ? fallback : ParseEnum(node.Value, fallback);
        }
    }
}
