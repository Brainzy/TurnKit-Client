using System.Collections.Generic;
using System.Text;
using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    internal sealed class RelayCommandQueue
    {
        private readonly List<RelayAction> _queuedActions = new List<RelayAction>();
        private readonly List<object> _pendingBuilders = new List<object>();
        private readonly RelayValidator _validator;
        private string _queuedJson;

        public RelayCommandQueue(RelayValidator validator)
        {
            _validator = validator;
        }

        public void QueueJson(string json)
        {
            _queuedJson = json;
        }

        public void RegisterPendingBuilder(object builder)
        {
            _pendingBuilders.Add(builder);
        }

        public void QueueSpawn(RelayList toList, ItemSpec itemSpec)
        {
            if (_validator.ValidateSpawn(toList))
            {
                var existingAction = GetLastSpawnActionForList(toList.Name);
                if (existingAction != null)
                {
                    existingAction.items.Add(itemSpec);
                    return;
                }

                _queuedActions.Add(new RelayAction
                {
                    action = ActionType.SPAWN,
                    toList = toList.Name,
                    items = new List<ItemSpec> { itemSpec }
                });
            }
        }

        public void QueueMove(MoveBuilder builder, RelayList fromList, RelayList toList, SelectorType selector, string[] data, int repeat, bool ignoreOwnership)
        {
            var action = CreateListAction(ActionType.MOVE, fromList, toList, selector, data, repeat, ignoreOwnership);
            UnregisterPendingBuilder(builder);

            if (_validator.ValidateMove(fromList, toList, ignoreOwnership))
            {
                _queuedActions.Add(action);
            }
        }

        public void QueueRemove(RemoveBuilder builder, RelayList fromList, SelectorType selector, string[] data, int repeat, bool ignoreOwnership)
        {
            var action = CreateListAction(ActionType.REMOVE, fromList, null, selector, data, repeat, ignoreOwnership);
            UnregisterPendingBuilder(builder);

            if (_validator.ValidateRemove(fromList, ignoreOwnership))
            {
                _queuedActions.Add(action);
            }
        }

        public void QueueShuffle(RelayList list)
        {
            var action = new RelayAction
            {
                action = ActionType.SHUFFLE,
                list = list.Name
            };

            if (_validator.ValidateShuffle(list))
            {
                _queuedActions.Add(action);
            }
        }

        public void QueuePassTurn(string playerId)
        {
            _queuedActions.Add(new RelayAction
            {
                action = ActionType.PASS_TURN,
                playerId = playerId
            });
        }

        public void QueueSetStat(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot, string playerId, JSONNode value)
        {
            if (!_validator.ValidateTrackedStatMetadata(metadata?.Name, metadata) ||
                !_validator.ValidateStatTarget(metadata, slot, playerId) ||
                !_validator.ValidateSetStat(metadata, value))
            {
                return;
            }

            _queuedActions.Add(new RelayAction
            {
                action = ActionType.SET_STAT,
                statName = metadata.Name,
                playerId = playerId,
                value = value
            });
        }

        public void QueueAddStat(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot, string playerId, double? delta, string[] values)
        {
            if (!_validator.ValidateTrackedStatMetadata(metadata?.Name, metadata) ||
                !_validator.ValidateStatTarget(metadata, slot, playerId) ||
                !_validator.ValidateAddStat(metadata, delta, values))
            {
                return;
            }

            _queuedActions.Add(new RelayAction
            {
                action = ActionType.ADD_STAT,
                statName = metadata.Name,
                playerId = playerId,
                delta = delta,
                values = values
            });
        }

        public string BuildMovePayload(
            bool shouldEndTurn,
            bool delegated = false,
            string delegateForPlayerId = null,
            bool isAfk = false,
            int? intendedMoveNumber = null)
        {
            ExecutePendingBuilders();

            var msg = new JSONObject
            {
                ["type"] = "MOVE",
                ["endTurn"] = shouldEndTurn
            };

            if (!string.IsNullOrEmpty(_queuedJson) && _queuedJson != "null")
            {
                msg["payload"] = JSON.Parse(_queuedJson);
            }

            if (_queuedActions.Count > 0)
            {
                var actionArray = new JSONArray();
                foreach (var action in BuildSerializedActions())
                {
                    actionArray.Add(action.ToNode());
                }

                msg["actions"] = actionArray;
            }

            if (delegated && !string.IsNullOrWhiteSpace(delegateForPlayerId))
            {
                msg["delegateFor"] = delegateForPlayerId;
            }

            if (intendedMoveNumber.HasValue)
            {
                msg["intendedMoveNumber"] = intendedMoveNumber.Value;
            }

            if (isAfk)
            {
                msg["isAfk"] = true;
            }

            return msg.ToString();
        }

        public void Clear()
        {
            _queuedActions.Clear();
            _queuedJson = null;
            _pendingBuilders.Clear();
        }

        private void ExecutePendingBuilders()
        {
            var pending = _pendingBuilders.ToArray();
            foreach (var builder in pending)
            {
                if (builder is MoveBuilder moveBuilder)
                {
                    moveBuilder.CheckComplete();
                }
                else if (builder is RemoveBuilder removeBuilder)
                {
                    removeBuilder.Execute();
                }
            }
        }

        private RelayAction CreateListAction(
            ActionType actionType,
            RelayList fromList,
            RelayList toList,
            SelectorType selector,
            string[] data,
            int repeat,
            bool ignoreOwnership)
        {
            var action = new RelayAction
            {
                action = actionType,
                fromList = fromList.Name,
                toList = toList?.Name,
                selector = selector,
                repeat = repeat,
                ignoreOwnership = ignoreOwnership
            };

            if (selector == SelectorType.BY_ITEM_IDS)
            {
                action.itemIds = data;
                action.repeat = data?.Length ?? 0;
            }
            else if (selector == SelectorType.BY_SLUGS)
            {
                action.slugs = data;
                action.repeat = data?.Length ?? 0;
            }

            return action;
        }

        private void UnregisterPendingBuilder(object builder)
        {
            for (int i = _pendingBuilders.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_pendingBuilders[i], builder))
                {
                    _pendingBuilders.RemoveAt(i);
                    break;
                }
            }
        }

        private RelayAction GetLastSpawnActionForList(string listName)
        {
            if (_queuedActions.Count == 0)
            {
                return null;
            }

            var lastAction = _queuedActions[_queuedActions.Count - 1];
            if (lastAction.action != ActionType.SPAWN || lastAction.toList != listName)
            {
                return null;
            }

            lastAction.items ??= new List<ItemSpec>();
            return lastAction;
        }

        private IEnumerable<RelayAction> BuildSerializedActions()
        {
            for (int i = 0; i < _queuedActions.Count;)
            {
                var action = _queuedActions[i];
                if (action.action != ActionType.SET_STAT)
                {
                    yield return action;
                    i++;
                    continue;
                }

                int blockStart = i;
                while (i < _queuedActions.Count && _queuedActions[i].action == ActionType.SET_STAT)
                {
                    i++;
                }

                foreach (var batchedAction in BuildBatchedSetStatActions(blockStart, i))
                {
                    yield return batchedAction;
                }
            }
        }

        private IEnumerable<RelayAction> BuildBatchedSetStatActions(int startInclusive, int endExclusive)
        {
            var matchValues = new Dictionary<string, JSONNode>();
            var perPlayerValues = new Dictionary<string, Dictionary<string, JSONNode>>();

            for (int index = startInclusive; index < endExclusive; index++)
            {
                var action = _queuedActions[index];
                if (string.IsNullOrWhiteSpace(action.statName))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(action.playerId))
                {
                    matchValues[action.statName] = action.value;
                    continue;
                }

                if (!perPlayerValues.TryGetValue(action.playerId, out var statsForPlayer))
                {
                    statsForPlayer = new Dictionary<string, JSONNode>();
                    perPlayerValues[action.playerId] = statsForPlayer;
                }

                statsForPlayer[action.statName] = action.value;
            }

            if (matchValues.Count > 0)
            {
                yield return new RelayAction
                {
                    action = ActionType.SET_STATS,
                    statValues = matchValues
                };
            }

            var playerGroups = new Dictionary<string, List<string>>();
            var signatureToValues = new Dictionary<string, Dictionary<string, JSONNode>>();
            foreach (var kvp in perPlayerValues)
            {
                string playerId = kvp.Key;
                var statValues = kvp.Value;
                if (statValues.Count == 0)
                {
                    continue;
                }

                string signature = BuildStatValueSignature(statValues);
                if (!playerGroups.TryGetValue(signature, out var players))
                {
                    players = new List<string>();
                    playerGroups[signature] = players;
                    signatureToValues[signature] = statValues;
                }

                players.Add(playerId);
            }

            foreach (var kvp in playerGroups)
            {
                var players = kvp.Value;
                var values = signatureToValues[kvp.Key];
                if (players.Count == 1)
                {
                    yield return new RelayAction
                    {
                        action = ActionType.SET_STATS,
                        playerId = players[0],
                        statValues = values
                    };
                    continue;
                }

                yield return new RelayAction
                {
                    action = ActionType.SET_PLAYER_STATS,
                    players = players,
                    statValues = values
                };
            }
        }

        private static string BuildStatValueSignature(Dictionary<string, JSONNode> statValues)
        {
            var keys = new List<string>(statValues.Keys);
            keys.Sort(System.StringComparer.Ordinal);

            var builder = new StringBuilder();
            foreach (var key in keys)
            {
                builder.Append(key);
                builder.Append(':');
                builder.Append(statValues[key]?.ToString() ?? "null");
                builder.Append(';');
            }

            return builder.ToString();
        }
    }
}
