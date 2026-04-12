using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    public sealed class RelayStatBuilder
    {
        private readonly string _statName;
        private readonly TurnKitConfig.PlayerSlot? _slot;

        internal RelayStatBuilder(string statName, TurnKitConfig.PlayerSlot? slot)
        {
            _statName = statName;
            _slot = slot;
        }

        public void Set(double value)
        {
            Relay.Instance.QueueSetStat(_statName, _slot, new JSONNumber(value));
        }

        public void Set(string value)
        {
            Relay.Instance.QueueSetStat(_statName, _slot, new JSONString(value ?? string.Empty));
        }

        public void Set(params string[] values)
        {
            var array = new JSONArray();
            if (values != null)
            {
                foreach (var item in values)
                {
                    array.Add(item ?? string.Empty);
                }
            }

            Relay.Instance.QueueSetStat(_statName, _slot, array);
        }

        public void Add(double delta)
        {
            Relay.Instance.QueueAddStat(_statName, _slot, delta, null);
        }

        public void Add(params string[] values)
        {
            Relay.Instance.QueueAddStat(_statName, _slot, null, values);
        }
    }
}
