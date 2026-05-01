using System;
using System.Collections.Generic;
using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    public enum ActionType
    {
        SPAWN,
        MOVE,
        REMOVE,
        SHUFFLE,
        PASS_TURN,
        SET_STAT,
        ADD_STAT
    }

    public enum SelectorType
    {
        TOP,
        BOTTOM,
        RANDOM,
        ALL,
        BY_ITEM_IDS,
        BY_SLUGS
    }

    public enum ListChangeType
    {
        ItemsAdded,
        ItemsRemoved,
        ItemsMoved,
        Shuffled
    }

    public enum ChangeType
    {
        SPAWN,
        MOVE,
        REMOVE,
        SHUFFLE
    }

    public sealed class RelayItem
    {
        public string Id { get; }
        public string Slug { get; }
        public TurnKitConfig.PlayerSlot CreatorSlot { get; }

        internal RelayItem(string id, string slug, TurnKitConfig.PlayerSlot creatorSlot)
        {
            Id = id;
            Slug = slug;
            CreatorSlot = creatorSlot;
        }
    }

    [Serializable]
    public class ItemSpec
    {
        public string itemId;
        public string slug;

        public ItemSpec(string itemId, string slug)
        {
            this.itemId = itemId;
            this.slug = slug;
        }

        public ItemSpec(string slug)
        {
            this.slug = slug;
        }
    }

    public sealed class TrackedStatMetadata
    {
        public string Name;
        public TurnKitConfig.TrackedStatDataType DataType;
        public TurnKitConfig.TrackedStatScope Scope;
        public double InitialDouble;
        public string InitialString;
        public IReadOnlyList<string> InitialList;
    }

    [Serializable]
    internal class RelayAction
    {
        public ActionType action;
        public string toList;
        public List<ItemSpec> items;
        public string fromList;
        public SelectorType selector;
        public string[] itemIds;
        public string[] slugs;
        public int repeat = 1;
        public bool ignoreOwnership;
        public string list;
        public string statName;
        public string playerId;
        public JSONNode value;
        public double? delta;
        public string[] values;

        public JSONObject ToNode()
        {
            var node = new JSONObject { ["action"] = action.ToString() };

            switch (action)
            {
                case ActionType.SPAWN:
                    node["to"] = toList;
                    if (ShouldSerializeSpawnAsSlugs())
                    {
                        var slugsArray = new JSONArray();
                        foreach (var item in items)
                        {
                            if (!string.IsNullOrWhiteSpace(item?.slug))
                            {
                                slugsArray.Add(item.slug);
                            }
                        }

                        node["slugs"] = slugsArray;
                    }
                    else
                    {
                        var itemsArray = new JSONArray();
                        foreach (var item in items)
                        {
                            if (item == null || string.IsNullOrWhiteSpace(item.slug))
                            {
                                continue;
                            }

                            var itemNode = new JSONObject();
                            itemNode["slug"] = item.slug;
                            if (!string.IsNullOrWhiteSpace(item.itemId))
                            {
                                itemNode["id"] = item.itemId;
                            }

                            itemsArray.Add(itemNode);
                        }

                        node["items"] = itemsArray;
                    }
                    break;

                case ActionType.MOVE:
                    node["from"] = fromList;
                    node["to"] = toList;
                    node["selector"] = SerializeSelector();
                    node["repeat"] = repeat;
                    node["ignoreOwner"] = ignoreOwnership;
                    break;

                case ActionType.REMOVE:
                    node["from"] = fromList;
                    node["selector"] = SerializeSelector();
                    node["repeat"] = repeat;
                    node["ignoreOwner"] = ignoreOwnership;
                    break;

                case ActionType.SHUFFLE:
                    node["list"] = list;
                    break;

                case ActionType.PASS_TURN:
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        node["player"] = playerId;
                    }

                    break;

                case ActionType.SET_STAT:
                    node["stat"] = statName;
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        node["player"] = playerId;
                    }

                    node["value"] = value ?? JSONNull.CreateOrGet();
                    break;

                case ActionType.ADD_STAT:
                    node["stat"] = statName;
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        node["player"] = playerId;
                    }

                    if (delta.HasValue)
                    {
                        node["delta"] = delta.Value;
                    }
                    else if (values != null)
                    {
                        var valuesArray = new JSONArray();
                        foreach (var item in values)
                        {
                            valuesArray.Add(item ?? string.Empty);
                        }

                        node["values"] = valuesArray;
                    }
                    else
                    {
                        node["values"] = JSONNull.CreateOrGet();
                    }
                    break;
            }

            return node;
        }

        private JSONObject SerializeSelector()
        {
            var selectorNode = new JSONObject();
            selectorNode["selector"] = selector.ToString();

            switch (selector)
            {
                case SelectorType.BY_ITEM_IDS:
                    var idsArray = new JSONArray();
                    foreach (var id in itemIds)
                    {
                        idsArray.Add(id);
                    }

                    selectorNode["ids"] = idsArray;
                    break;
                case SelectorType.BY_SLUGS:
                    var slugsArray = new JSONArray();
                    foreach (var slug in slugs)
                    {
                        slugsArray.Add(slug);
                    }

                    selectorNode["slugs"] = slugsArray;
                    break;
            }

            return selectorNode;
        }

        private bool ShouldSerializeSpawnAsSlugs()
        {
            if (items == null || items.Count <= 1)
            {
                return false;
            }

            foreach (var item in items)
            {
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.slug) ||
                    !string.IsNullOrWhiteSpace(item.itemId))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [Serializable]
    public class MatchStartedMessage
    {
        public string type;
        public string sessionId;
        public PlayerInfo[] players;
        public int[] delegatedSlots;
        public bool yourTurn;
        public string activePlayerId;
        public string turnTimerKind;
        public int turnTimerSeconds;
        public long serverNowUtcMs;
        public long? timerEndUtcMs;
        public ListDefinition[] lists;
        public ListSnapshot[] contents;
        public long randomSeed;
        public int moveNumber;

        public string ToString(int listCount)
        {
            string playersList = "";
            foreach (var p in players)
            {
                playersList += $"[ID: {p.playerId}, Slot: {p.slot}] ";
            }

            return $"TurnKit - MatchStarted: {type}\n" +
                   $"Session: {sessionId}\n" +
                   $"Players: {playersList}\n" +
                   $"Your Turn: {yourTurn}\n" +
                   $"Active Player: {activePlayerId}\n" +
                   $"Seed: {randomSeed}\n" +
                   $"Move: {moveNumber}\n" +
                   $"Lists: {listCount}";
        }
    }

    [Serializable]
    public class PlayerInfo
    {
        public string playerId;
        public TurnKitConfig.PlayerSlot slot;
        public bool isConnected = true;
        public bool isDelegated;
    }

    [Serializable]
    public class ListDefinition
    {
        public string name;
        public string tag;
        public List<string> ownerPlayerIds;
        public List<string> visibleToPlayerIds;
        public List<TurnKitConfig.PlayerSlot> ownerSlots;
        public List<TurnKitConfig.PlayerSlot> visibleToSlots;
    }

    [Serializable]
    public class ListSnapshot
    {
        public string[] ids;
        public string[] slugs;
    }

    [Serializable]
    public class VisibleChange
    {
        public ChangeType type;
        public RelayList fromList;
        public RelayList toList;
        public string[] ids;
        public string[] slugs;
        public int[] creators;
        public string actingSlot;
    }

    [Serializable]
    public abstract class StatChange
    {
        public string StatName { get; internal set; }
        public string PlayerId { get; internal set; }

        public bool IsMatchStat => string.IsNullOrEmpty(PlayerId);
        public abstract object OldValueObject { get; }
        public abstract object ValueObject { get; }
    }

    [Serializable]
    public sealed class StatChange<T> : StatChange
    {
        public T OldValue { get; internal set; }
        public T Value { get; internal set; }

        public override object OldValueObject => OldValue;
        public override object ValueObject => Value;
    }

    [Serializable]
    public sealed class TypedStatChangeCollection
    {
        private readonly List<StatChange<double>> _doubleChanges = new();
        private readonly List<StatChange<string>> _stringChanges = new();
        private readonly List<StatChange<IReadOnlyList<string>>> _listChanges = new();
        private readonly List<StatChange> _allChanges = new();
        private readonly Dictionary<string, StatChange> _changesByName = new();

        public IReadOnlyList<StatChange<double>> doubleChanges => _doubleChanges;
        public IReadOnlyList<StatChange<string>> stringChanges => _stringChanges;
        public IReadOnlyList<StatChange<IReadOnlyList<string>>> listChanges => _listChanges;
        public IReadOnlyList<StatChange> allChanges => _allChanges;

        internal void AddStatChange(StatChange change)
        {
            if (change == null)
            {
                return;
            }

            _allChanges.Add(change);
            _changesByName[change.StatName] = change;

            switch (change)
            {
                case StatChange<double> numberChange:
                    _doubleChanges.Add(numberChange);
                    break;
                case StatChange<string> stringChange:
                    _stringChanges.Add(stringChange);
                    break;
                case StatChange<IReadOnlyList<string>> listChange:
                    _listChanges.Add(listChange);
                    break;
            }
        }

        private bool TryGetInternal<T>(string statName, out StatChange<T> change)
        {
            if (_changesByName.TryGetValue(statName, out var storedChange) && storedChange is StatChange<T> typedChange)
            {
                change = typedChange;
                return true;
            }

            change = null;
            return false;
        }

        public bool TryGet<T>(IStatToken<T> token, out StatChange<T> change)
        {
            return TryGetInternal(token.Name, out change);
        }
    }

    [Serializable]
    public class MoveMadeMessage
    {
        public string type;
        public string actingPlayerId;
        public int moveNumber;
        public string payload;
        public VisibleChange[] changes;
        public readonly TypedStatChangeCollection statChanges = new();

        public string playerId => actingPlayerId;
    }

    [Serializable]
    public class SyncCompleteMessage
    {
        public string type;
        public int moveNumber;
        public long serverNowUtcMs;
        public long? timerEndUtcMs;
    }

    [Serializable]
    public class TurnStartedMessage
    {
        public string type;
        public string activePlayerId;
        public string turnTimerKind;
        public int turnTimerSeconds;
        public int moveNumber;
        public long serverNowUtcMs;
        public long? timerEndUtcMs;
    }

    [Serializable]
    public class MoveRequestedForPlayerMessage
    {
        public string type;
        public TurnKitConfig.PlayerSlot playerSlot;
        public IReadOnlyList<RelayList> updatedLists;
        public int moveNumber;
        public long serverNowUtcMs;
        public long? timerEndUtcMs;
    }

    [Serializable]
    internal class PrivateListRevealMessage
    {
        public string name;
        public string[] ids;
        public string[] slugs;
    }

    [Serializable]
    public class VoteFailedMessage
    {
        public string type;
        public int moveNumber;
        public string failAction;
    }

    [Serializable]
    public class GameEndedMessage
    {
        public string type;
        public string reason;
    }

    [Serializable]
    public class ErrorMessage
    {
        public string type;
        public string code;
        public string message;
    }

    internal enum RelayEventType
    {
        MatchStarted,
        MoveMade,
        SyncComplete,
        TurnStarted,
        MoveRequestedForPlayer,
        VoteFailed,
        Error,
        GameEnded
    }

    internal sealed class RelayMessageOutcome
    {
        public RelayEventType EventType;
        public MatchStartedMessage MatchStarted;
        public MoveMadeMessage MoveMade;
        public SyncCompleteMessage SyncComplete;
        public TurnStartedMessage TurnStarted;
        public MoveRequestedForPlayerMessage MoveRequestedForPlayer;
        public VoteFailedMessage VoteFailed;
        public ErrorMessage Error;
        public GameEndedMessage GameEnded;
    }
}
