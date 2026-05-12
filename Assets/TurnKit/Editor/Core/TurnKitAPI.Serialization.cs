using System;
using System.Collections.Generic;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit.Editor
{
    public static partial class TurnKitAPI
    {
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
                afkTurnTimerSeconds = node["afkTurnTimerSeconds"].IsNull ? 0 : node["afkTurnTimerSeconds"].AsInt,
                disconnectedTurnTimerSeconds = node["disconnectedTurnTimerSeconds"].IsNull ? 0 : node["disconnectedTurnTimerSeconds"].AsInt,
                waitReconnectSeconds = node["waitReconnectSeconds"].AsInt,
                reconnectMoveHistorySize = node["reconnectMoveHistorySize"].AsInt,
                onTurnTimeout = ParseNullableEnum(node["onTurnTimeout"], TurnKitConfig.OnTurnTimeout.CHANGE_TO_NEXT_PLAYER),
                revealPrivateListsOnTimeout = node["revealPrivateListsOnTimeout"].AsBool,
                lists = ParseRelayLists(node["lists"]),
                trackedStats = ParseTrackedStats(node["trackedStats"]),
                queueRequirements = ParseQueueRequirements(node["queueRequirements"]),
                playerStoreMutations = ParsePlayerStoreMutations(node["playerStoreMutations"])
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

        private static List<TurnKitConfig.QueueRequirementConfig> ParseQueueRequirements(JSONNode node)
        {
            var requirements = new List<TurnKitConfig.QueueRequirementConfig>();
            var array = node.AsArray;
            if (array == null)
            {
                return requirements;
            }

            foreach (JSONNode requirementNode in array)
            {
                requirements.Add(new TurnKitConfig.QueueRequirementConfig
                {
                    name = requirementNode["name"].IsNull ? null : requirementNode["name"].Value,
                    combinator = ParseEnum(requirementNode["combinator"], TurnKitConfig.RuleCombinator.AND),
                    conditions = ParseRelayConditions(requirementNode["conditions"])
                });
            }

            return requirements;
        }

        private static List<TurnKitConfig.PlayerStoreMutationConfig> ParsePlayerStoreMutations(JSONNode node)
        {
            var mutations = new List<TurnKitConfig.PlayerStoreMutationConfig>();
            var array = node.AsArray;
            if (array == null)
            {
                return mutations;
            }

            foreach (JSONNode mutationNode in array)
            {
                var mutation = new TurnKitConfig.PlayerStoreMutationConfig
                {
                    mutationId = mutationNode["mutationId"].IsNull ? null : mutationNode["mutationId"].Value,
                    phase = ParseEnum(mutationNode["phase"], TurnKitConfig.RulePhase.ON_MATCH_START),
                    target = ParseEnum(mutationNode["target"], TurnKitConfig.MutationTarget.ACTING_PLAYER),
                    storeKey = mutationNode["storeKey"],
                    operation = ParseEnum(mutationNode["operation"], TurnKitConfig.MutationOperation.SET),
                    combinator = ParseEnum(mutationNode["combinator"], TurnKitConfig.RuleCombinator.AND),
                    conditions = ParseRelayConditions(mutationNode["conditions"])
                };

                ApplyMutationValue(mutation, mutationNode["value"]);
                mutations.Add(mutation);
            }

            return mutations;
        }

        private static List<TurnKitConfig.RelayConditionConfig> ParseRelayConditions(JSONNode node)
        {
            var conditions = new List<TurnKitConfig.RelayConditionConfig>();
            var array = node.AsArray;
            if (array == null)
            {
                return conditions;
            }

            foreach (JSONNode conditionNode in array)
            {
                conditions.Add(new TurnKitConfig.RelayConditionConfig
                {
                    source = ParseEnum(conditionNode["source"], TurnKitConfig.ConditionSource.STORE),
                    key = conditionNode["key"],
                    @operator = ParseEnum(conditionNode["operator"], TurnKitConfig.ConditionOperator.EQ),
                    value = conditionNode["value"].IsNull ? null : conditionNode["value"].Value
                });
            }

            return conditions;
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

        private static void ApplyMutationValue(TurnKitConfig.PlayerStoreMutationConfig mutation, JSONNode node)
        {
            mutation.valueType = TurnKitConfig.PlayerStoreValueType.STRING;
            mutation.stringValue = string.Empty;
            mutation.numberValue = 0d;
            mutation.stringListValue = new List<string>();

            if (node == null || node.IsNull || mutation.operation == TurnKitConfig.MutationOperation.LIST_CLEAR)
            {
                return;
            }

            if (node.Tag == JSONNodeType.Number)
            {
                mutation.valueType = TurnKitConfig.PlayerStoreValueType.NUMBER;
                mutation.numberValue = node.AsDouble;
                return;
            }

            if (node.Tag == JSONNodeType.Array)
            {
                mutation.valueType = TurnKitConfig.PlayerStoreValueType.STRING_LIST;
                foreach (JSONNode item in node.AsArray)
                {
                    mutation.stringListValue.Add(item.Value);
                }

                return;
            }

            mutation.valueType = TurnKitConfig.PlayerStoreValueType.STRING;
            mutation.stringValue = node.Value;
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

        private static List<TurnKitConfig.PlayerStoreDefConfig> ParsePlayerStoreDefList(string json)
        {
            var defs = new List<TurnKitConfig.PlayerStoreDefConfig>();
            var array = JSON.Parse(json).AsArray;
            if (array == null)
            {
                return defs;
            }

            foreach (JSONNode node in array)
            {
                defs.Add(ParsePlayerStoreDef(node));
            }

            return defs;
        }

        private static List<TurnKitConfig.PlayerStoreDefConfig> ParsePlayerStoreDefsNode(JSONNode node)
        {
            var defs = new List<TurnKitConfig.PlayerStoreDefConfig>();
            var array = node?.AsArray;
            if (array == null)
            {
                return defs;
            }

            foreach (JSONNode item in array)
            {
                defs.Add(ParsePlayerStoreDef(item));
            }

            return defs;
        }

        private static TurnKitConfig.PlayerStoreDefConfig ParsePlayerStoreDef(JSONNode node)
        {
            double? numberMin = node["numberMin"] == null || node["numberMin"].IsNull ? (double?)null : node["numberMin"].AsDouble;
            double? numberMax = node["numberMax"] == null || node["numberMax"].IsNull ? (double?)null : node["numberMax"].AsDouble;
            return new TurnKitConfig.PlayerStoreDefConfig
            {
                storeKey = node["storeKey"],
                valueType = ParseEnum(node["valueType"], TurnKitConfig.PlayerStoreValueType.STRING),
                clientWritable = node["clientWritable"].AsBool,
                clientReadable = node["clientReadable"].AsBool,
                numberMin = numberMin,
                numberMax = numberMax
            };
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
            node["afkTurnTimerSeconds"] = relay.afkTurnTimerSeconds;
            node["disconnectedTurnTimerSeconds"] = relay.disconnectedTurnTimerSeconds;
            node["waitReconnectSeconds"] = relay.waitReconnectSeconds;
            node["reconnectMoveHistorySize"] = Mathf.Clamp(relay.reconnectMoveHistorySize, 0, 20);
            node["onTurnTimeout"] = relay.onTurnTimeout.ToString();
            node["revealPrivateListsOnTimeout"] = relay.revealPrivateListsOnTimeout;
            node["lists"] = BuildRelayListsNode(relay.lists);
            node["trackedStats"] = BuildTrackedStatsNode(relay.trackedStats);
            node["queueRequirements"] = BuildQueueRequirementsNode(relay.queueRequirements);
            node["playerStoreMutations"] = BuildPlayerStoreMutationsNode(relay.playerStoreMutations);
            return node.ToString();
        }

        private static JSONNode BuildQueueRequirementsNode(List<TurnKitConfig.QueueRequirementConfig> requirements)
        {
            var array = new JSONArray();
            if (requirements == null)
            {
                return array;
            }

            foreach (var requirement in requirements)
            {
                var node = new JSONObject
                {
                    ["name"] = string.IsNullOrWhiteSpace(requirement.name) ? JSONNull.CreateOrGet() : requirement.name,
                    ["combinator"] = requirement.combinator.ToString(),
                    ["conditions"] = BuildRelayConditionsNode(requirement.conditions)
                };
                array.Add(node);
            }

            return array;
        }

        private static JSONNode BuildPlayerStoreMutationsNode(List<TurnKitConfig.PlayerStoreMutationConfig> mutations)
        {
            var array = new JSONArray();
            if (mutations == null)
            {
                return array;
            }

            foreach (var mutation in mutations)
            {
                var node = new JSONObject
                {
                    ["mutationId"] = string.IsNullOrWhiteSpace(mutation.mutationId) ? JSONNull.CreateOrGet() : mutation.mutationId,
                    ["phase"] = mutation.phase.ToString(),
                    ["target"] = mutation.target.ToString(),
                    ["storeKey"] = mutation.storeKey,
                    ["operation"] = mutation.operation.ToString(),
                    ["combinator"] = mutation.combinator.ToString(),
                    ["conditions"] = BuildRelayConditionsNode(mutation.conditions),
                    ["value"] = BuildMutationValueNode(mutation)
                };
                array.Add(node);
            }

            return array;
        }

        private static JSONNode BuildRelayConditionsNode(List<TurnKitConfig.RelayConditionConfig> conditions)
        {
            var array = new JSONArray();
            if (conditions == null)
            {
                return array;
            }

            foreach (var condition in conditions)
            {
                var node = new JSONObject
                {
                    ["source"] = condition.source.ToString(),
                    ["key"] = condition.key,
                    ["operator"] = condition.@operator.ToString(),
                    ["value"] = condition.value ?? string.Empty
                };
                array.Add(node);
            }

            return array;
        }

        private static JSONNode BuildMutationValueNode(TurnKitConfig.PlayerStoreMutationConfig mutation)
        {
            switch (mutation.operation)
            {
                case TurnKitConfig.MutationOperation.ADD:
                case TurnKitConfig.MutationOperation.SUB:
                    return new JSONNumber(mutation.numberValue);
                case TurnKitConfig.MutationOperation.LIST_SET:
                case TurnKitConfig.MutationOperation.LIST_ADD:
                case TurnKitConfig.MutationOperation.LIST_REMOVE:
                    var listArray = new JSONArray();
                    if (mutation.stringListValue != null)
                    {
                        foreach (var item in mutation.stringListValue)
                        {
                            listArray.Add(item ?? string.Empty);
                        }
                    }

                    return listArray;
                case TurnKitConfig.MutationOperation.LIST_CLEAR:
                    return JSONNull.CreateOrGet();
                case TurnKitConfig.MutationOperation.SET:
                default:
                    switch (mutation.valueType)
                    {
                        case TurnKitConfig.PlayerStoreValueType.NUMBER:
                            return new JSONNumber(mutation.numberValue);
                        case TurnKitConfig.PlayerStoreValueType.STRING_LIST:
                            var setArray = new JSONArray();
                            if (mutation.stringListValue != null)
                            {
                                foreach (var item in mutation.stringListValue)
                                {
                                    setArray.Add(item ?? string.Empty);
                                }
                            }

                            return setArray;
                        default:
                            return new JSONString(mutation.stringValue ?? string.Empty);
                    }
            }
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

        private static string BuildPlayerStoreDefJson(TurnKitConfig.PlayerStoreDefConfig def)
        {
            var node = new JSONObject
            {
                ["storeKey"] = def.storeKey,
                ["valueType"] = def.valueType.ToString(),
                ["clientWritable"] = def.clientWritable,
                ["clientReadable"] = def.clientReadable
            };
            node["numberMin"] = def.numberMin.HasValue ? new JSONNumber(def.numberMin.Value) : JSONNull.CreateOrGet();
            node["numberMax"] = def.numberMax.HasValue ? new JSONNumber(def.numberMax.Value) : JSONNull.CreateOrGet();
            return node.ToString();
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
