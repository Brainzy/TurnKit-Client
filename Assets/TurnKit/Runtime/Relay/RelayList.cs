using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TurnKit
{
    public class RelayList
    {
        private readonly List<RelayItem> _items = new List<RelayItem>();

        internal List<TurnKitConfig.PlayerSlot> _ownerSlots = new List<TurnKitConfig.PlayerSlot>();
        internal List<TurnKitConfig.PlayerSlot> _visibleToSlots = new List<TurnKitConfig.PlayerSlot>();

        public string Name { get; internal set; }
        public string Tag { get; internal set; }

        public IReadOnlyList<RelayItem> Items => _items.AsReadOnly();
        public int Count => _items.Count;
        public RelayItem this[int index] => _items[index];
        public RelayItem Top => _items.Count > 0 ? _items[0] : null;
        public RelayItem Bottom => _items.Count > 0 ? _items[_items.Count - 1] : null;

        public bool IsOwnedByMe => _ownerSlots.Contains(Relay.MySlot);
        public bool IsVisibleToMe => _visibleToSlots.Contains(Relay.MySlot);

        public IReadOnlyList<TurnKitConfig.PlayerSlot> OwnerSlots => _ownerSlots.AsReadOnly();
        public IReadOnlyList<TurnKitConfig.PlayerSlot> VisibleToSlots => _visibleToSlots.AsReadOnly();

        public void Spawn(string slug)
        {
            Relay.Instance.EnqueueSpawn(this, new ItemSpec(slug));
        }

        public void Spawn(string itemId, string slug)
        {
            Relay.Instance.EnqueueSpawn(this, new ItemSpec(itemId, slug));
        }

        public MoveBuilder Move(SelectorType selector)
        {
            return Relay.Instance.CreateMoveBuilder(this, selector, null);
        }

        public MoveBuilder Move(SelectorType selector, string[] data)
        {
            if ((selector == SelectorType.BY_ITEM_IDS || selector == SelectorType.BY_SLUGS)
                && (data == null || data.Length == 0))
            {
                Debug.LogError($"[TurnKit] Selector {selector} requires data array.");
                return null;
            }

            return Relay.Instance.CreateMoveBuilder(this, selector, data);
        }

        public RemoveBuilder Remove(SelectorType selector)
        {
            return Relay.Instance.CreateRemoveBuilder(this, selector, null);
        }

        public RemoveBuilder Remove(SelectorType selector, string[] data)
        {
            if ((selector == SelectorType.BY_ITEM_IDS || selector == SelectorType.BY_SLUGS)
                && (data == null || data.Length == 0))
            {
                Debug.LogError($"[TurnKit] Selector {selector} requires data array.");
                return null;
            }

            return Relay.Instance.CreateRemoveBuilder(this, selector, data);
        }

        public void Shuffle()
        {
            Relay.Instance.EnqueueShuffle(this);
        }

        public RelayItem FindById(string itemId)
        {
            return _items.FirstOrDefault(i => i.Id == itemId);
        }

        public IReadOnlyList<RelayItem> FindBySlug(string slug)
        {
            return _items.Where(i => i.Slug == slug).ToList().AsReadOnly();
        }

        public IReadOnlyList<RelayItem> FindBySlugs(params string[] slugs)
        {
            var slugSet = new HashSet<string>(slugs);
            return _items.Where(i => slugSet.Contains(i.Slug)).ToList().AsReadOnly();
        }

        internal void AddItem(RelayItem item) => _items.Add(item);
        internal void RemoveItem(RelayItem item) => _items.Remove(item);
        internal void ClearItems() => _items.Clear();
    }

    public class MoveBuilder
    {
        private readonly RelayList _fromList;
        private readonly SelectorType _selector;
        private readonly string[] _data;

        private int _repeat = 1;
        private bool _ignoreOwnership;
        private bool _completed;

        internal MoveBuilder(RelayList fromList, SelectorType selector, string[] data)
        {
            _fromList = fromList;
            _selector = selector;
            _data = data;
        }

        public MoveBuilder To(RelayList targetList)
        {
            if (_completed)
            {
                Debug.LogWarning("[TurnKit] MoveBuilder already completed. This call will be ignored.");
                return this;
            }

            _completed = true;
            Relay.Instance.CompleteMoveBuilder(this, _fromList, targetList, _selector, _data, _repeat, _ignoreOwnership);
            return this;
        }

        public MoveBuilder Repeat(int count)
        {
            if (_completed)
            {
                Debug.LogWarning("[TurnKit] Cannot modify MoveBuilder after .To() is called.");
                return this;
            }

            _repeat = count;
            return this;
        }

        public MoveBuilder IgnoreOwnership()
        {
            if (_completed)
            {
                Debug.LogWarning("[TurnKit] Cannot modify MoveBuilder after .To() is called.");
                return this;
            }

            _ignoreOwnership = true;
            return this;
        }

        internal void CheckComplete()
        {
            if (!_completed)
            {
                Debug.LogError($"[TurnKit] MoveBuilder from '{_fromList.Name}' never completed (missing .To() call). Skipping action.");
            }
        }
    }

    public class RemoveBuilder
    {
        private readonly RelayList _fromList;
        private readonly SelectorType _selector;
        private readonly string[] _data;

        private int _repeat = 1;
        private bool _ignoreOwnership;
        private bool _executed;

        internal RemoveBuilder(RelayList fromList, SelectorType selector, string[] data)
        {
            _fromList = fromList;
            _selector = selector;
            _data = data;
        }

        public RemoveBuilder Repeat(int count)
        {
            if (_executed)
            {
                Debug.LogWarning("[TurnKit] Cannot modify RemoveBuilder after execution.");
                return this;
            }

            _repeat = count;
            return this;
        }

        public RemoveBuilder IgnoreOwnership()
        {
            if (_executed)
            {
                Debug.LogWarning("[TurnKit] Cannot modify RemoveBuilder after execution.");
                return this;
            }

            _ignoreOwnership = true;
            return this;
        }

        internal void Execute()
        {
            if (_executed)
            {
                return;
            }

            _executed = true;
            Relay.Instance.ExecuteRemoveBuilder(this, _fromList, _selector, _data, _repeat, _ignoreOwnership);
        }
    }
}
