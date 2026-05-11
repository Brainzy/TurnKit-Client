using System;
using System.Collections.Generic;
using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    internal sealed partial class RelayMessageRouter
    {
        private static int GetMoveNumber(JSONNode node)
        {
            var moveNode = node["move"];
            return moveNode?.AsInt ?? 0;
        }

        private static string ParseTurnTimerKind(JSONNode node)
        {
            string kind = ReadOptionalString(node, "turnTimerKind");
            if (!string.IsNullOrWhiteSpace(kind))
            {
                return kind;
            }

            kind = ReadOptionalString(node, "timerKind");
            return string.IsNullOrWhiteSpace(kind) ? null : kind;
        }

        private static int ParseTurnTimerSeconds(JSONNode node)
        {
            int seconds = ReadOptionalInt(node, "turnTimerSeconds");
            if (seconds > 0)
            {
                return seconds;
            }

            seconds = ReadOptionalInt(node, "turnTimeoutSeconds");
            return seconds > 0 ? seconds : 0;
        }

        private static string ReadOptionalString(JSONNode node, string key)
        {
            var valueNode = node?[key];
            if (valueNode == null || valueNode.IsNull)
            {
                return null;
            }

            string value = valueNode.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static int ReadOptionalInt(JSONNode node, string key)
        {
            var valueNode = node?[key];
            if (valueNode == null || valueNode.IsNull)
            {
                return 0;
            }

            return valueNode.AsInt;
        }

        private static long ReadOptionalLong(JSONNode node, string key)
        {
            var valueNode = node?[key];
            if (valueNode == null || valueNode.IsNull)
            {
                return 0L;
            }

            return (long)valueNode.AsDouble;
        }

        private static long? ReadOptionalNullableLong(JSONNode node, string key)
        {
            var valueNode = node?[key];
            if (valueNode == null || valueNode.IsNull)
            {
                return null;
            }

            return (long)valueNode.AsDouble;
        }

        private static ChangeType ParseChangeType(JSONNode node)
        {
            var typeValue = node["kind"]?.Value;
            return string.IsNullOrWhiteSpace(typeValue)
                ? default
                : (ChangeType)Enum.Parse(typeof(ChangeType), typeValue.ToUpperInvariant());
        }

        private static string[] ParseStringArray(JSONNode node)
        {
            var array = node?.AsArray;
            if (array == null)
            {
                return Array.Empty<string>();
            }

            var values = new string[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                values[i] = array[i]?.Value;
            }

            return values;
        }

        private static int[] ParseIntArray(JSONNode node)
        {
            var array = node?.AsArray;
            if (array == null)
            {
                return Array.Empty<int>();
            }

            var values = new int[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                values[i] = array[i].AsInt;
            }

            return values;
        }

        private static string ParseEmbeddedJson(JSONNode node)
        {
            if (node == null || node.IsNull)
            {
                return null;
            }

            return node.Tag == JSONNodeType.String ? node.Value : node.ToString();
        }

        private static PlayerInfo[] ParsePlayers(JSONNode node)
        {
            var array = node?.AsArray;
            if (array == null)
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = new PlayerInfo[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                players[i] = new PlayerInfo
                {
                    playerId = array[i]["playerId"],
                    slot = (TurnKitConfig.PlayerSlot)array[i]["slot"].AsInt,
                    isConnected = ParseOptionalConnected(array[i]),
                    isDelegated = ReadOptionalBool(array[i], "delegated")
                };
            }

            return players;
        }

        private static ListDefinition[] ParseListDefinitions(JSONNode node)
        {
            var array = node?.AsArray;
            if (array == null)
            {
                return Array.Empty<ListDefinition>();
            }

            var lists = new ListDefinition[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                var listNode = array[i];
                lists[i] = new ListDefinition
                {
                    name = listNode["name"],
                    tag = listNode["tag"],
                    ownerPlayerIds = ParseStringListNode(listNode["owners"]),
                    visibleToPlayerIds = ParseStringListNode(listNode["visibleTo"])
                };
            }

            return lists;
        }

        private static ListSnapshot[] ParseListSnapshots(JSONNode node)
        {
            var array = node?.AsArray;
            if (array == null)
            {
                return Array.Empty<ListSnapshot>();
            }

            var snapshots = new ListSnapshot[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                snapshots[i] = new ListSnapshot
                {
                    ids = ParseStringArray(array[i]["ids"]),
                    slugs = ParseStringArray(array[i]["slugs"])
                };
            }

            return snapshots;
        }

        private static void ApplyDelegatedSlots(PlayerInfo[] players, int[] delegatedSlots)
        {
            if (players == null || players.Length == 0 || delegatedSlots == null || delegatedSlots.Length == 0)
            {
                return;
            }

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                for (int slotIndex = 0; slotIndex < delegatedSlots.Length; slotIndex++)
                {
                    if ((int)player.slot != delegatedSlots[slotIndex])
                    {
                        continue;
                    }

                    player.isDelegated = true;
                    player.isConnected = true;
                    break;
                }
            }
        }

        private static bool ParseOptionalConnected(JSONNode playerNode)
        {
            if (TryReadOptionalBool(playerNode, "connected", out bool connected))
            {
                return connected;
            }

            if (TryReadOptionalBool(playerNode, "disconnected", out bool disconnected))
            {
                return !disconnected;
            }

            return true;
        }

        private static bool ReadOptionalBool(JSONNode node, string key)
        {
            return TryReadOptionalBool(node, key, out bool value) && value;
        }

        private static bool TryReadOptionalBool(JSONNode node, string key, out bool value)
        {
            var valueNode = node?[key];
            if (valueNode == null || valueNode.IsNull)
            {
                value = false;
                return false;
            }

            value = valueNode.AsBool;
            return true;
        }

        private static PrivateListRevealMessage[] ParsePrivateListReveals(JSONNode node)
        {
            var array = node?.AsArray;
            if (array == null)
            {
                return Array.Empty<PrivateListRevealMessage>();
            }

            var reveals = new PrivateListRevealMessage[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                reveals[i] = new PrivateListRevealMessage
                {
                    name = array[i]["name"],
                    ids = ParseStringArray(array[i]["ids"]),
                    slugs = ParseStringArray(array[i]["slugs"])
                };
            }

            return reveals;
        }

        private static List<string> ParseStringListNode(JSONNode node)
        {
            var values = ParseStringArray(node);
            return values.Length == 0 ? new List<string>() : new List<string>(values);
        }

        private static VoteFailedMessage ParseVoteFailed(JSONNode node)
        {
            return new VoteFailedMessage
            {
                type = node["type"],
                moveNumber = node["failedMove"].AsInt,
                failAction = node["failAction"]
            };
        }

        private static ErrorMessage ParseError(JSONNode node)
        {
            return new ErrorMessage
            {
                type = node["type"],
                code = node["code"],
                message = node["msg"]
            };
        }

        private static GameEndedMessage ParseGameEnded(JSONNode node)
        {
            return new GameEndedMessage
            {
                type = node["type"],
                reason = node["reason"]
            };
        }

        private RelayList ResolveList(string listName)
        {
            if (string.IsNullOrEmpty(listName))
            {
                return null;
            }

            return Relay.GetList(listName);
        }
    }
}
