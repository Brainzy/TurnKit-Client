using System;
using System.Collections.Generic;
using System.Linq;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelaySessionState
    {
        private readonly struct TrackedStatKey : IEquatable<TrackedStatKey>
        {
            public readonly string StatName;
            public readonly string PlayerId;

            public TrackedStatKey(string statName, string playerId)
            {
                StatName = statName;
                PlayerId = playerId;
            }

            public bool Equals(TrackedStatKey other)
            {
                return string.Equals(StatName, other.StatName, StringComparison.Ordinal) &&
                       string.Equals(PlayerId, other.PlayerId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TrackedStatKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((StatName != null ? StringComparer.Ordinal.GetHashCode(StatName) : 0) * 397) ^
                           (PlayerId != null ? StringComparer.Ordinal.GetHashCode(PlayerId) : 0);
                }
            }
        }

        private readonly List<RelayList> _allLists = new();
        private readonly Dictionary<string, RelayList> _listsByName = new();
        private readonly Dictionary<string, List<RelayList>> _listsByTag = new();
        private readonly Dictionary<string, TrackedStatMetadata> _trackedStatsByName = new();
        private readonly Dictionary<TrackedStatKey, object> _trackedStatValues = new();
        private readonly List<PlayerInfo> _players = new();

        private string _myPlayerId;

        public IReadOnlyList<RelayList> AllLists => _allLists.AsReadOnly();
        public IReadOnlyList<PlayerInfo> AllPlayers => _players.Count == 0 ? null : _players.AsReadOnly();
        public string CurrentTurnPlayerId { get; private set; }
        public bool IsMyTurn { get; private set; }
        public bool IsInSyncWindow { get; private set; }
        public int LastAcknowledgedMoveNumber { get; private set; }

        public void SetLocalPlayer(string playerId)
        {
            _myPlayerId = playerId;
        }

        public void InitializeFromMetadata<TList>(
            Dictionary<TList, TurnKitConfig.RelayListConfig> listMetadata,
            Dictionary<string, TrackedStatMetadata> statMetadata)
            where TList : Enum
        {
            ClearMetadata();

            foreach (var kvp in listMetadata)
            {
                var relayList = new RelayList
                {
                    Name = kvp.Key.ToString(),
                    Tag = kvp.Value.tag,
                    _ownerSlots = new List<TurnKitConfig.PlayerSlot>(kvp.Value.ownerSlots),
                    _visibleToSlots = new List<TurnKitConfig.PlayerSlot>(kvp.Value.visibleToSlots)
                };

                RegisterList(relayList);
            }

            foreach (var kvp in statMetadata)
            {
                _trackedStatsByName[kvp.Key] = kvp.Value;
            }
        }

        public void ApplyMatchStarted(MatchStartedMessage msg, JSONNode node)
        {
            _players.Clear();
            if (msg.players != null)
            {
                _players.AddRange(msg.players);
            }

            CurrentTurnPlayerId = msg.activePlayerId;
            IsMyTurn = msg.yourTurn;
            LastAcknowledgedMoveNumber = msg.moveNumber;
            IsInSyncWindow = false;

            var contentsNode = node["listContents"].AsObject;
            if (contentsNode == null)
            {
                return;
            }

            foreach (var key in contentsNode.Keys)
            {
                if (!_listsByName.TryGetValue(key, out var relayList))
                {
                    continue;
                }

                var arrayNode = contentsNode[key].AsArray;
                if (arrayNode == null)
                {
                    continue;
                }

                foreach (JSONNode itemNode in arrayNode)
                {
                    relayList.AddItem(CreateRelayItem(itemNode));
                }
            }
        }

        public void ApplyMoveMade(MoveMadeMessage msg)
        {
            LastAcknowledgedMoveNumber = msg.moveNumber;
        }

        public void ApplySyncComplete(SyncCompleteMessage msg)
        {
            LastAcknowledgedMoveNumber = msg.moveNumber;
            IsInSyncWindow = false;
        }

        public void ApplyTurnChanged(TurnChangedMessage msg)
        {
            CurrentTurnPlayerId = msg.activePlayerId;
            IsMyTurn = msg.activePlayerId == _myPlayerId;
        }

        public void MarkConnected()
        {
            IsInSyncWindow = true;
        }

        public void MarkDisconnected()
        {
        }

        public bool TryGetListByName(string name, out RelayList list)
        {
            return _listsByName.TryGetValue(name, out list);
        }

        public bool TryGetListsByTag(string tag, out List<RelayList> list)
        {
            return _listsByTag.TryGetValue(tag, out list);
        }

        public bool TryGetTrackedStatMetadata(string statName, out TrackedStatMetadata metadata)
        {
            return _trackedStatsByName.TryGetValue(statName, out metadata);
        }

        public bool TryGetTrackedStatValue<T>(string statName, string playerId, out T value)
        {
            if (_trackedStatValues.TryGetValue(new TrackedStatKey(statName, playerId), out var storedValue) &&
                storedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public PlayerInfo GetPlayerBySlot(TurnKitConfig.PlayerSlot slot)
        {
            foreach (var player in _players)
            {
                if (player.slot == slot)
                {
                    return player;
                }
            }

            return null;
        }

        public string ResolvePlayerId(TurnKitConfig.PlayerSlot slot)
        {
            return GetPlayerBySlot(slot)?.playerId;
        }

        public void ApplyStatChanges(IEnumerable<StatChange> changes)
        {
            if (changes == null)
            {
                return;
            }

            foreach (var change in changes)
            {
                if (change == null || string.IsNullOrWhiteSpace(change.StatName))
                {
                    continue;
                }

                _trackedStatValues[new TrackedStatKey(change.StatName, change.PlayerId)] = change.ValueObject;
            }
        }

        public void ApplyVisibleChanges(VisibleChange[] changes, Action<RelayList, ListChangeType> notifyListChanged)
        {
            foreach (var change in changes)
            {
                switch (change.type)
                {
                    case ChangeType.SPAWN:
                        ApplySpawn(change, notifyListChanged);
                        break;
                    case ChangeType.MOVE:
                        ApplyMove(change, notifyListChanged);
                        break;
                    case ChangeType.REMOVE:
                        ApplyRemove(change, notifyListChanged);
                        break;
                    case ChangeType.SHUFFLE:
                        ApplyShuffle(change, notifyListChanged);
                        break;
                }
            }
        }

        private void ApplySpawn(VisibleChange change, Action<RelayList, ListChangeType> notifyListChanged)
        {
            var list = change.toList;
            if (list == null)
            {
                Debug.LogWarning("[TurnKit] Target list not found for SPAWN");
                return;
            }

            foreach (var itemData in change.items)
            {
                list.AddItem(new RelayItem(itemData.Id, itemData.Slug, itemData.CreatorSlot));
            }

            notifyListChanged?.Invoke(list, ListChangeType.ItemsAdded);
        }

        private void ApplyMove(VisibleChange change, Action<RelayList, ListChangeType> notifyListChanged)
        {
            var fromList = change.fromList;
            var toList = change.toList;
            if (fromList == null || toList == null)
            {
                Debug.LogWarning("[TurnKit] Lists not found for MOVE");
                return;
            }

            foreach (var itemData in change.items)
            {
                var existing = fromList.FindById(itemData.Id);
                if (existing != null)
                {
                    fromList.RemoveItem(existing);
                }

                toList.AddItem(new RelayItem(itemData.Id, itemData.Slug, itemData.CreatorSlot));
            }

            notifyListChanged?.Invoke(fromList, ListChangeType.ItemsRemoved);
            notifyListChanged?.Invoke(toList, ListChangeType.ItemsAdded);
        }

        private void ApplyRemove(VisibleChange change, Action<RelayList, ListChangeType> notifyListChanged)
        {
            var list = change.fromList;
            if (list == null)
            {
                Debug.LogWarning("[TurnKit] Source list not found for REMOVE");
                return;
            }

            foreach (var itemData in change.items)
            {
                var existing = list.FindById(itemData.Id);
                if (existing != null)
                {
                    list.RemoveItem(existing);
                }
            }

            notifyListChanged?.Invoke(list, ListChangeType.ItemsRemoved);
        }

        private void ApplyShuffle(VisibleChange change, Action<RelayList, ListChangeType> notifyListChanged)
        {
            if (TurnKitConfig.Instance.enableLogging)
            {
                Debug.Log($"[TurnKit] List {change.fromList?.Name} was shuffled");
            }

            if (change.fromList != null)
            {
                notifyListChanged?.Invoke(change.fromList, ListChangeType.Shuffled);
            }
        }

        private RelayItem CreateRelayItem(JSONNode itemNode)
        {
            return new RelayItem(
                itemNode["id"],
                itemNode["slug"],
                (TurnKitConfig.PlayerSlot)itemNode["creatorSlot"].AsInt
            );
        }

        private void RegisterList(RelayList relayList)
        {
            _allLists.Add(relayList);
            _listsByName[relayList.Name] = relayList;

            if (!_listsByTag.ContainsKey(relayList.Tag))
            {
                _listsByTag.Add(relayList.Tag, new List<RelayList>());
            }

            _listsByTag[relayList.Tag].Add(relayList);
        }

        private void ClearMetadata()
        {
            _allLists.Clear();
            _listsByName.Clear();
            _listsByTag.Clear();
            _trackedStatsByName.Clear();
            _trackedStatValues.Clear();
        }
    }
}
