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
            itemId = Guid.NewGuid().ToString();
            this.slug = slug;
        }
    }

    public sealed class TrackedStatMetadata
    {
        public string Name;
        public TurnKitConfig.TrackedStatDataType DataType;
        public TurnKitConfig.TrackedStatScope Scope;
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
                    node["toList"] = toList;
                    var itemsArray = new JSONArray();
                    foreach (var item in items)
                    {
                        var itemNode = new JSONObject();
                        itemNode["itemId"] = item.itemId;
                        itemNode["slug"] = item.slug;
                        itemsArray.Add(itemNode);
                    }

                    node["items"] = itemsArray;
                    break;

                case ActionType.MOVE:
                    node["fromList"] = fromList;
                    node["toList"] = toList;
                    node["selector"] = SerializeSelector();
                    node["repeat"] = repeat;
                    node["ignoreOwnership"] = ignoreOwnership;
                    break;

                case ActionType.REMOVE:
                    node["fromList"] = fromList;
                    node["selector"] = SerializeSelector();
                    node["repeat"] = repeat;
                    node["ignoreOwnership"] = ignoreOwnership;
                    break;

                case ActionType.SHUFFLE:
                    node["list"] = list;
                    break;

                case ActionType.SET_STAT:
                    node["statName"] = statName;
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        node["playerId"] = playerId;
                    }

                    node["value"] = value ?? JSONNull.CreateOrGet();
                    break;

                case ActionType.ADD_STAT:
                    node["statName"] = statName;
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        node["playerId"] = playerId;
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

                    selectorNode["itemIds"] = idsArray;
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
    }

    [Serializable]
    public class MatchStartedMessage
    {
        public string type;
        public string sessionId;
        public PlayerInfo[] players;
        public bool yourTurn;
        public string activePlayerId;
        public ListDefinition[] lists;
        public Dictionary<string, RelayItem[]> listContents;
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
    public class VisibleChange
    {
        public ChangeType type;
        public RelayList fromList;
        public RelayList toList;
        public RelayItem[] items;
        public string actingSlot;
    }

    [Serializable]
    public class MoveMadeMessage
    {
        public string type;
        public string playerId;
        public int moveNumber;
        public string json;
        public VisibleChange[] changes;
    }

    [Serializable]
    public class SyncCompleteMessage
    {
        public string type;
        public int moveNumber;
    }

    [Serializable]
    public class TurnChangedMessage
    {
        public string type;
        public string activePlayerId;
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
        TurnChanged,
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
        public TurnChangedMessage TurnChanged;
        public VoteFailedMessage VoteFailed;
        public ErrorMessage Error;
        public GameEndedMessage GameEnded;
    }
}
