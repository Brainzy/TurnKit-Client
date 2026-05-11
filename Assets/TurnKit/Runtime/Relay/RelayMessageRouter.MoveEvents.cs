using System;
using System.Collections.Generic;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    internal sealed partial class RelayMessageRouter
    {
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

        private VisibleChange ParseVisibleChange(JSONNode node)
        {
            return new VisibleChange
            {
                type = ParseChangeType(node),
                fromList = ResolveList(node["from"]?.Value),
                toList = ResolveList(node["to"]?.Value),
                actingSlot = node["actorSlot"]?.Value,
                ids = ParseStringArray(node["ids"]),
                slugs = ParseStringArray(node["slugs"]),
                creators = ParseIntArray(node["creators"])
            };
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
                    return new StatChange<double> { StatName = statName, PlayerId = playerId, OldValue = ParseDouble(oldValueNode), Value = ParseDouble(node["value"]) };
                case TurnKitConfig.TrackedStatDataType.STRING:
                    return new StatChange<string> { StatName = statName, PlayerId = playerId, OldValue = ParseString(oldValueNode), Value = ParseString(node["value"]) };
                case TurnKitConfig.TrackedStatDataType.LIST_STRING:
                    return new StatChange<IReadOnlyList<string>> { StatName = statName, PlayerId = playerId, OldValue = ParseStringList(oldValueNode), Value = ParseStringList(node["value"]) };
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
    }
}
