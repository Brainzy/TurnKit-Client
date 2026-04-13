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
                    return HandleMatchStarted(raw, node);
                case "MOVE_MADE":
                    return HandleMoveMade(raw, node);
                case "SYNC_COMPLETE":
                    return HandleSyncComplete(raw);
                case "TURN_CHANGED":
                    return HandleTurnChanged(raw);
                case "VOTE_FAILED":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.VoteFailed,
                        VoteFailed = JsonUtility.FromJson<VoteFailedMessage>(raw)
                    };
                case "ERROR":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.Error,
                        Error = JsonUtility.FromJson<ErrorMessage>(raw)
                    };
                case "GAME_ENDED":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.GameEnded,
                        GameEnded = JsonUtility.FromJson<GameEndedMessage>(raw)
                    };
                default:
                    return null;
            }
        }

        private RelayMessageOutcome HandleMatchStarted(string raw, JSONNode node)
        {
            var msg = JsonUtility.FromJson<MatchStartedMessage>(raw);
            _state.ApplyMatchStarted(msg, node);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.MatchStarted,
                MatchStarted = msg
            };
        }

        private RelayMessageOutcome HandleMoveMade(string raw, JSONNode node)
        {
            var msg = new MoveMadeMessage
            {
                type = node["type"],
                actingPlayerId = GetActingPlayerId(node),
                moveNumber = node["moveNumber"].AsInt,
                json = ParseEmbeddedJson(node["json"]),
                changes = ParseVisibleChanges(node["changes"])
            };

            ParseStatChanges(node["statChanges"], msg);

            _state.ApplyVisibleChanges(msg.changes, _notifyListChanged);

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
            var msg = JsonUtility.FromJson<SyncCompleteMessage>(raw);
            _state.ApplySyncComplete(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.SyncComplete,
                SyncComplete = msg
            };
        }

        private RelayMessageOutcome HandleTurnChanged(string raw)
        {
            var msg = JsonUtility.FromJson<TurnChangedMessage>(raw);
            _state.ApplyTurnChanged(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.TurnChanged,
                TurnChanged = msg
            };
        }

        private VisibleChange ParseVisibleChange(JSONNode node)
        {
            var change = new VisibleChange
            {
                type = (ChangeType)Enum.Parse(typeof(ChangeType), node["type"].Value.ToUpper()),
                fromList = ResolveList(node["fromList"]?.Value),
                toList = ResolveList(node["toList"]?.Value),
                actingSlot = node["actingSlot"]?.Value
            };

            var itemsArray = node["items"]?.AsArray;
            if (itemsArray == null)
            {
                change.items = Array.Empty<RelayItem>();
                return change;
            }

            change.items = new RelayItem[itemsArray.Count];
            for (int i = 0; i < itemsArray.Count; i++)
            {
                var itemNode = itemsArray[i];
                change.items[i] = new RelayItem(
                    itemNode["id"],
                    itemNode["slug"],
                    (TurnKitConfig.PlayerSlot)itemNode["creatorSlot"].AsInt
                );
            }

            return change;
        }

        private StatChange ParseStatChange(JSONNode node)
        {
            string statName = node["statName"];
            if (!_state.TryGetTrackedStatMetadata(statName, out var metadata))
            {
                Debug.LogWarning($"[TurnKit] Unknown tracked stat '{statName}' in MOVE_MADE. Skipping stat change.");
                return null;
            }

            var playerIdNode = node["playerId"];
            var oldValueNode = node["oldValue"];
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

        private static string GetActingPlayerId(JSONNode node)
        {
            var actingPlayerId = node["actingPlayerId"];
            if (actingPlayerId != null && !actingPlayerId.IsNull)
            {
                return actingPlayerId.Value;
            }

            var legacyPlayerId = node["playerId"];
            return legacyPlayerId == null || legacyPlayerId.IsNull ? null : legacyPlayerId.Value;
        }

        private static string ParseEmbeddedJson(JSONNode node)
        {
            if (node == null || node.IsNull)
            {
                return null;
            }

            return node.Tag == JSONNodeType.String ? node.Value : node.ToString();
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
