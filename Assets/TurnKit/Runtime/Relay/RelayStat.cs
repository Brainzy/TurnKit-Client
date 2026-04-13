using System;
using System.Collections.Generic;
using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    public interface IStatToken
    {
        string Name { get; }
        Type ValueType { get; }
    }

    public interface IStatToken<TValue> : IStatToken
    {
    }

    internal interface IRelayStatToken : IStatToken
    {
        TrackedStatMetadata Metadata { get; }
    }

    public readonly struct MatchStatToken<TValue, TBuilder>
        : IRelayStatToken, IStatToken<TValue>
    {
        private readonly TrackedStatMetadata _metadata;

        internal MatchStatToken(TrackedStatMetadata metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        TrackedStatMetadata IRelayStatToken.Metadata => _metadata;
        internal TrackedStatMetadata GetMetadata() => _metadata;
        public string Name => _metadata.Name;
        public Type ValueType => StatTokenTypeMap.GetValueType(_metadata.DataType);
    }

    public readonly struct PlayerStatToken<TValue, TBuilder>
        : IRelayStatToken, IStatToken<TValue>
    {
        private readonly TrackedStatMetadata _metadata;

        internal PlayerStatToken(TrackedStatMetadata metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        TrackedStatMetadata IRelayStatToken.Metadata => _metadata;
        internal TrackedStatMetadata GetMetadata() => _metadata;
        public string Name => _metadata.Name;
        public Type ValueType => StatTokenTypeMap.GetValueType(_metadata.DataType);
    }

    public sealed class PlayerStatTargetBuilder<TValue, TBuilder>
    {
        private readonly TrackedStatMetadata _metadata;

        internal PlayerStatTargetBuilder(TrackedStatMetadata metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public TBuilder ForPlayer(TurnKitConfig.PlayerSlot slot)
        {
            return Relay.Instance.CreateStatBuilder<TBuilder>(_metadata, slot);
        }
    }

    public sealed class DoubleStatBuilder
    {
        private readonly TrackedStatMetadata _metadata;
        private readonly TurnKitConfig.PlayerSlot? _slot;

        internal DoubleStatBuilder(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot)
        {
            _metadata = metadata;
            _slot = slot;
        }

        public void Set(double value)
        {
            Relay.Instance.QueueSetStat(_metadata, _slot, new JSONNumber(value));
        }

        public void Add(double delta)
        {
            Relay.Instance.QueueAddStat(_metadata, _slot, delta, null);
        }
    }

    public sealed class StringStatBuilder
    {
        private readonly TrackedStatMetadata _metadata;
        private readonly TurnKitConfig.PlayerSlot? _slot;

        internal StringStatBuilder(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot)
        {
            _metadata = metadata;
            _slot = slot;
        }

        public void Set(string value)
        {
            Relay.Instance.QueueSetStat(_metadata, _slot, new JSONString(value ?? string.Empty));
        }
    }

    public sealed class ListStringStatBuilder
    {
        private readonly TrackedStatMetadata _metadata;
        private readonly TurnKitConfig.PlayerSlot? _slot;

        internal ListStringStatBuilder(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot)
        {
            _metadata = metadata;
            _slot = slot;
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

            Relay.Instance.QueueSetStat(_metadata, _slot, array);
        }

        public void Add(params string[] values)
        {
            Relay.Instance.QueueAddStat(_metadata, _slot, null, values);
        }
    }

    internal static class StatTokenTypeMap
    {
        public static Type GetValueType(TurnKitConfig.TrackedStatDataType dataType)
        {
            return dataType switch
            {
                TurnKitConfig.TrackedStatDataType.DOUBLE => typeof(double),
                TurnKitConfig.TrackedStatDataType.STRING => typeof(string),
                TurnKitConfig.TrackedStatDataType.LIST_STRING => typeof(IReadOnlyList<string>),
                _ => typeof(object)
            };
        }
    }
}
