using System;
using System.Collections.Generic;
using System.Linq;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelaySessionState
    {
        private readonly List<RelayList> _allLists = new();
        private readonly Dictionary<string, RelayList> _listsByName = new();
        private readonly Dictionary<string, List<RelayList>> _listsByTag = new();
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

        public void InitializeFromMetadata<T>(Dictionary<T, TurnKitConfig.RelayListConfig> metadata) where T : Enum
        {
            ClearLists();

            foreach (var kvp in metadata)
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

            ClearLists();
            foreach (var def in msg.lists)
            {
                var relayList = new RelayList
                {
                    Name = def.name,
                    Tag = def.tag,
                    _ownerSlots = ResolveSlots(def.ownerSlots, def.ownerPlayerIds),
                    _visibleToSlots = ResolveSlots(def.visibleToSlots, def.visibleToPlayerIds)
                };

                RegisterList(relayList);
            }

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

        public PlayerInfo GetPlayerBySlot(TurnKitConfig.PlayerSlot slot)
        {
            return _players.FirstOrDefault(p => p.slot == slot);
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
            if (!_listsByName.TryGetValue(change.toList, out var list))
            {
                Debug.LogWarning($"[TurnKit] List {change.toList} not found for SPAWN");
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
            if (!_listsByName.TryGetValue(change.fromList, out var fromList) ||
                !_listsByName.TryGetValue(change.toList, out var toList))
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
            if (!_listsByName.TryGetValue(change.fromList, out var list))
            {
                Debug.LogWarning($"[TurnKit] List {change.fromList} not found for REMOVE");
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
                Debug.Log($"[TurnKit] List {change.fromList} was shuffled");
            }

            if (_listsByName.TryGetValue(change.fromList, out var list))
            {
                notifyListChanged?.Invoke(list, ListChangeType.Shuffled);
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

        private List<TurnKitConfig.PlayerSlot> ResolveSlots(
            List<TurnKitConfig.PlayerSlot> explicitSlots,
            List<string> playerIds)
        {
            if (explicitSlots != null && explicitSlots.Count > 0)
            {
                return new List<TurnKitConfig.PlayerSlot>(explicitSlots);
            }

            return ConvertPlayerIdsToSlots(playerIds);
        }

        private List<TurnKitConfig.PlayerSlot> ConvertPlayerIdsToSlots(List<string> ids)
        {
            var results = new List<TurnKitConfig.PlayerSlot>();
            if (ids == null)
            {
                return results;
            }

            foreach (var id in ids)
            {
                var player = _players.FirstOrDefault(p => p.playerId == id);
                if (player != null)
                {
                    results.Add(player.slot);
                    continue;
                }

                if (Enum.TryParse(id, true, out TurnKitConfig.PlayerSlot slot))
                {
                    results.Add(slot);
                }
            }

            return results;
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

        private void ClearLists()
        {
            _allLists.Clear();
            _listsByName.Clear();
            _listsByTag.Clear();
        }
    }
}
