using System;
using System.Collections.Generic;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayMessageRouter
    {
        private readonly RelaySessionState _state;
        private readonly Action<RelayList, ListChangeType> _notifyListChanged;

        public RelayMessageRouter(RelaySessionState state, Action<RelayList, ListChangeType> notifyListChanged)
        {
            _state = state;
            _notifyListChanged = notifyListChanged;
        }

        public RelayMessageOutcome Process(string raw)
        {
            var node = JSON.Parse(raw);
            string type = node["type"];
            if (string.IsNullOrEmpty(type))
            {
                return null;
            }

            switch (type)
            {
                case "MATCH_STARTED":
                    return HandleMatchStarted(node);
                case "MOVE_MADE":
                    return HandleMoveMade(node);
                case "SYNC_COMPLETE":
                    return HandleSyncComplete(raw);
                case "TURN_STARTED":
                    return HandleTurnStarted(raw);
                case "MOVE_REQUESTED_FOR_PLAYER":
                    return HandleMoveRequestedForPlayer(node);
                case "VOTE_FAILED":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.VoteFailed,
                        VoteFailed = ParseVoteFailed(node)
                    };
                case "ERROR":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.Error,
                        Error = ParseError(node)
                    };
                case "GAME_ENDED":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.GameEnded,
                        GameEnded = ParseGameEnded(node)
                    };
                default:
                    return null;
            }
        }

        private RelayMessageOutcome HandleMatchStarted(JSONNode node)
        {
            var msg = new MatchStartedMessage
            {
                type = node["type"],
                sessionId = node["sessionId"],
                players = ParsePlayers(node["players"]),
                delegatedSlots = ParseIntArray(node["delegatedSlots"]),
                yourTurn = node["yourTurn"].AsBool,
                activePlayerId = node["activePlayer"],
                turnTimerKind = ParseTurnTimerKind(node),
                turnTimerSeconds = ParseTurnTimerSeconds(node),
                serverNowUtcMs = ReadOptionalLong(node, "serverNowUtcMs"),
                timerEndUtcMs = ReadOptionalNullableLong(node, "timerEndUtcMs"),
                lists = ParseListDefinitions(node["lists"]),
                contents = ParseListSnapshots(node["contents"]),
                randomSeed = (long)node["seed"].AsDouble,
                moveNumber = GetMoveNumber(node)
            };
            ApplyDelegatedSlots(msg.players, msg.delegatedSlots);
            _state.ApplyMatchStarted(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.MatchStarted,
                MatchStarted = msg
            };
        }

        private RelayMessageOutcome HandleMoveMade(JSONNode node)
        {
            var msg = new MoveMadeMessage
            {
                type = node["type"],
                actingPlayerId = node["actor"],
                moveNumber = GetMoveNumber(node),
                payload = ParseEmbeddedJson(node["payload"]),
                changes = ParseVisibleChanges(node["changes"])
            };

            ParseStatChanges(node["stats"], msg);

            _state.ApplyVisibleChanges(msg.changes, _notifyListChanged);
            _state.ApplyStatChanges(msg.statChanges.allChanges);

            _state.ApplyMoveMade(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.MoveMade,
                MoveMade = msg
            };
        }

        private VisibleChange[] ParseVisibleChanges(JSONNode node)
        {
            var changesArray = node?.AsArray;
            if (changesArray == null)
            {
                return Array.Empty<VisibleChange>();
            }

            var changes = new VisibleChange[changesArray.Count];
            for (int i = 0; i < changesArray.Count; i++)
            {
                changes[i] = ParseVisibleChange(changesArray[i]);
            }

            return changes;
        }

        private void ParseStatChanges(JSONNode node, MoveMadeMessage message)
        {
            var statChangesArray = node?.AsArray;
            if (statChangesArray == null)
            {
                return;
            }

            for (int i = 0; i < statChangesArray.Count; i++)
            {
                var change = ParseStatChange(statChangesArray[i]);
                if (change != null)
                {
                    message.statChanges.AddStatChange(change);
                }
            }
        }

        private RelayMessageOutcome HandleSyncComplete(string raw)
        {
            var node = JSON.Parse(raw);
            var msg = new SyncCompleteMessage
            {
                type = node["type"],
                moveNumber = GetMoveNumber(node),
                serverNowUtcMs = ReadOptionalLong(node, "serverNowUtcMs"),
                timerEndUtcMs = ReadOptionalNullableLong(node, "timerEndUtcMs")
            };
            _state.ApplySyncComplete(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.SyncComplete,
                SyncComplete = msg
            };
        }

        private RelayMessageOutcome HandleTurnStarted(string raw)
        {
            var node = JSON.Parse(raw);
            var msg = new TurnStartedMessage
            {
                type = node["type"],
                activePlayerId = node["activePlayer"],
                turnTimerKind = ParseTurnTimerKind(node),
                turnTimerSeconds = ParseTurnTimerSeconds(node),
                moveNumber = GetMoveNumber(node),
                serverNowUtcMs = ReadOptionalLong(node, "serverNowUtcMs"),
                timerEndUtcMs = ReadOptionalNullableLong(node, "timerEndUtcMs")
            };
            _state.ApplyTurnStarted(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.TurnStarted,
                TurnStarted = msg
            };
        }

        private RelayMessageOutcome HandleMoveRequestedForPlayer(JSONNode node)
        {
            string playerId = node["playerId"];
            var lists = ParsePrivateListReveals(node["lists"]);
            int moveNumber = GetMoveNumber(node);
            var updatedLists = _state.ApplyMoveRequestedForPlayer(playerId, lists, moveNumber, _notifyListChanged);

            var msg = new MoveRequestedForPlayerMessage
            {
                type = node["type"],
                playerSlot = _state.ResolvePlayerSlot(playerId),
                updatedLists = updatedLists,
                moveNumber = moveNumber,
                serverNowUtcMs = ReadOptionalLong(node, "serverNowUtcMs"),
                timerEndUtcMs = ReadOptionalNullableLong(node, "timerEndUtcMs")
            };

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.MoveRequestedForPlayer,
                MoveRequestedForPlayer = msg
            };
        }

        private VisibleChange ParseVisibleChange(JSONNode node)
        {
            var change = new VisibleChange
            {
                type = ParseChangeType(node),
                fromList = ResolveList(node["from"]?.Value),
                toList = ResolveList(node["to"]?.Value),
                actingSlot = node["actorSlot"]?.Value,
                ids = ParseStringArray(node["ids"]),
                slugs = ParseStringArray(node["slugs"]),
                creators = ParseIntArray(node["creators"])
            };

            return change;
        }

        private StatChange ParseStatChange(JSONNode node)
        {
            string statName = node["stat"];
            if (!_state.TryGetTrackedStatMetadata(statName, out var metadata))
            {
                Debug.LogWarning($"[TurnKit] Unknown tracked stat '{statName}' in MOVE_MADE. Skipping stat change.");
                return null;
            }

            var playerIdNode = node["player"];
            var oldValueNode = node["old"];
            string playerId = playerIdNode == null || playerIdNode.IsNull ? null : playerIdNode.Value;

            switch (metadata.DataType)
            {
                case TurnKitConfig.TrackedStatDataType.DOUBLE:
                    return new StatChange<double>
                    {
                        StatName = statName,
                        PlayerId = playerId,
                        OldValue = ParseDouble(oldValueNode),
                        Value = ParseDouble(node["value"])
                    };
                case TurnKitConfig.TrackedStatDataType.STRING:
                    return new StatChange<string>
                    {
                        StatName = statName,
                        PlayerId = playerId,
                        OldValue = ParseString(oldValueNode),
                        Value = ParseString(node["value"])
                    };
                case TurnKitConfig.TrackedStatDataType.LIST_STRING:
                    return new StatChange<IReadOnlyList<string>>
                    {
                        StatName = statName,
                        PlayerId = playerId,
                        OldValue = ParseStringList(oldValueNode),
                        Value = ParseStringList(node["value"])
                    };
                default:
                    Debug.LogWarning($"[TurnKit] Unsupported tracked stat type '{metadata.DataType}' for '{statName}'. Skipping stat change.");
                    return null;
            }
        }

        private static double ParseDouble(JSONNode node)
        {
            return node is JSONNumber jsonNumber ? jsonNumber.AsDouble : default;
        }

        private static string ParseString(JSONNode node)
        {
            return node != null && !node.IsNull && node.Tag == JSONNodeType.String ? node.Value : default;
        }

        private static IReadOnlyList<string> ParseStringList(JSONNode node)
        {
            var array = node?.AsArray;
            if (array == null)
            {
                return default;
            }

            var values = new string[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] == null || array[i].IsNull || array[i].Tag != JSONNodeType.String)
                {
                    return default;
                }

                values[i] = array[i].Value;
            }

            return values;
        }

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
            if (!string.IsNullOrWhiteSpace(kind))
            {
                return kind;
            }

            return null;
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
                    // Delegated/bot slots should not surface as disconnected in client UI.
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
