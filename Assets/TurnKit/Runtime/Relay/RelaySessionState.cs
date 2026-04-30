using System;
using System.Collections.Generic;
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
                if (kvp.Value.Scope == TurnKitConfig.TrackedStatScope.MATCH)
                {
                    _trackedStatValues[new TrackedStatKey(kvp.Key, null)] = CreateInitialValue(kvp.Value);
                }
            }
        }

        public void ApplyMatchStarted(MatchStartedMessage msg)
        {
            ClearListItems();
            _players.Clear();
            if (msg.players != null)
            {
                _players.AddRange(msg.players);
            }

            SeedPerPlayerStatDefaults();

            CurrentTurnPlayerId = msg.activePlayerId;
            IsMyTurn = msg.yourTurn;
            LastAcknowledgedMoveNumber = msg.moveNumber;
            IsInSyncWindow = false;

            ApplyCompactContents(msg.contents, msg.lists);
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

        public void ApplyTurnStarted(TurnStartedMessage msg)
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

            if (_trackedStatsByName.TryGetValue(statName, out var metadata))
            {
                object initialValue = CreateInitialValue(metadata);
                if (initialValue is T typedInitialValue)
                {
                    value = typedInitialValue;
                    return true;
                }
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

            for (int i = 0; i < change.ids.Length; i++)
            {
                list.AddItem(new RelayItem(
                    change.ids[i],
                    GetSlug(change.slugs, i),
                    GetCreatorSlot(change.creators, i)));
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

            for (int i = 0; i < change.ids.Length; i++)
            {
                string itemId = change.ids[i];
                var existing = fromList.FindById(itemId);
                if (existing != null)
                {
                    fromList.RemoveItem(existing);
                }

                toList.AddItem(new RelayItem(
                    itemId,
                    GetSlug(change.slugs, i, existing?.Slug),
                    existing?.CreatorSlot ?? GetCreatorSlot(change.creators, i)));
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

            foreach (var itemId in change.ids)
            {
                var existing = list.FindById(itemId);
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

        private void ApplyCompactContents(ListSnapshot[] contents, ListDefinition[] lists)
        {
            if (contents == null || lists == null)
            {
                return;
            }

            int count = Math.Min(lists.Length, contents.Length);
            for (int i = 0; i < count; i++)
            {
                var listDefinition = lists[i];
                if (listDefinition == null || string.IsNullOrWhiteSpace(listDefinition.name) ||
                    !_listsByName.TryGetValue(listDefinition.name, out var relayList))
                {
                    continue;
                }

                var snapshot = contents[i];
                if (snapshot?.ids == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < snapshot.ids.Length; itemIndex++)
                {
                    relayList.AddItem(new RelayItem(
                        snapshot.ids[itemIndex],
                        snapshot.slugs != null && itemIndex < snapshot.slugs.Length ? snapshot.slugs[itemIndex] : null,
                        default));
                }
            }
        }

        private void ClearListItems()
        {
            foreach (var list in _allLists)
            {
                list?.ClearItems();
            }
        }

        private static string GetSlug(string[] slugs, int index, string fallback = null)
        {
            if (slugs != null && index >= 0 && index < slugs.Length)
            {
                return slugs[index];
            }

            return fallback;
        }

        private static TurnKitConfig.PlayerSlot GetCreatorSlot(int[] creators, int index)
        {
            if (creators != null && index >= 0 && index < creators.Length)
            {
                return (TurnKitConfig.PlayerSlot)creators[index];
            }

            return default;
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

        private void SeedPerPlayerStatDefaults()
        {
            foreach (var kvp in _trackedStatsByName)
            {
                if (kvp.Value.Scope != TurnKitConfig.TrackedStatScope.PER_PLAYER)
                {
                    continue;
                }

                object initialValue = CreateInitialValue(kvp.Value);
                foreach (var player in _players)
                {
                    if (player == null || string.IsNullOrWhiteSpace(player.playerId))
                    {
                        continue;
                    }

                    var key = new TrackedStatKey(kvp.Key, player.playerId);
                    if (!_trackedStatValues.ContainsKey(key))
                    {
                        _trackedStatValues[key] = initialValue;
                    }
                }
            }
        }

        private static object CreateInitialValue(TrackedStatMetadata metadata)
        {
            return metadata.DataType switch
            {
                TurnKitConfig.TrackedStatDataType.DOUBLE => metadata.InitialDouble,
                TurnKitConfig.TrackedStatDataType.STRING => metadata.InitialString ?? string.Empty,
                TurnKitConfig.TrackedStatDataType.LIST_STRING => metadata.InitialList != null ? new List<string>(metadata.InitialList) : new List<string>(),
                _ => null
            };
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
