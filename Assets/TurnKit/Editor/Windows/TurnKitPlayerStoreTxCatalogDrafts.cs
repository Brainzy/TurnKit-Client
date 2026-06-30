using System;
using System.Collections.Generic;
using System.Linq;

namespace TurnKit.Editor
{
    [Serializable]
    internal sealed class TurnKitPlayerStoreTxCatalogEntry
    {
        public string transactionId;
        public List<TurnKitPlayerStoreTxCatalogConditionDraft> conditions = new();
        public List<TurnKitPlayerStoreTxCatalogMutationDraft> mutations = new();
        public bool enabled = true;
        public int mutationCount;
        public int catalogVersion = 1;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    internal sealed class TurnKitPlayerStoreTxCatalogConditionDraft
    {
        public TurnKitConfig.ConditionSource source = TurnKitConfig.ConditionSource.STORE;
        public string key;
        public TurnKitConfig.ConditionOperator @operator = TurnKitConfig.ConditionOperator.EQ;
        public TurnKitConfig.PlayerStoreValueType valueType = TurnKitConfig.PlayerStoreValueType.STRING;
        public string stringValue = string.Empty;
        public double numberValue;
        public List<string> stringListValue = new();
    }

    [Serializable]
    internal sealed class TurnKitPlayerStoreTxCatalogMutationDraft
    {
        public string storeKey;
        public TurnKitConfig.MutationOperation operation = TurnKitConfig.MutationOperation.SET;
        public TurnKitConfig.PlayerStoreValueType valueType = TurnKitConfig.PlayerStoreValueType.STRING;
        public string stringValue = string.Empty;
        public double numberValue;
        public List<string> stringListValue = new();
    }

    internal static class TurnKitPlayerStoreTxCatalogDrafts
    {
        internal static TurnKitPlayerStoreTxCatalogEntry CreateEmptyEntry()
        {
            return new TurnKitPlayerStoreTxCatalogEntry
            {
                conditions = new List<TurnKitPlayerStoreTxCatalogConditionDraft>(),
                mutations = new List<TurnKitPlayerStoreTxCatalogMutationDraft>(),
                enabled = true,
                catalogVersion = 1
            };
        }

        internal static TurnKitPlayerStoreTxCatalogEntry Clone(TurnKitPlayerStoreTxCatalogEntry source)
        {
            if (source == null)
            {
                return CreateEmptyEntry();
            }

            return new TurnKitPlayerStoreTxCatalogEntry
            {
                transactionId = source.transactionId,
                conditions = source.conditions?.Select(Clone).ToList() ?? new List<TurnKitPlayerStoreTxCatalogConditionDraft>(),
                mutations = source.mutations?.Select(Clone).ToList() ?? new List<TurnKitPlayerStoreTxCatalogMutationDraft>(),
                enabled = source.enabled,
                mutationCount = source.mutationCount,
                catalogVersion = source.catalogVersion,
                createdAt = source.createdAt,
                updatedAt = source.updatedAt
            };
        }

        internal static TurnKitPlayerStoreTxCatalogConditionDraft Clone(TurnKitPlayerStoreTxCatalogConditionDraft source)
        {
            if (source == null)
            {
                return new TurnKitPlayerStoreTxCatalogConditionDraft();
            }

            return new TurnKitPlayerStoreTxCatalogConditionDraft
            {
                source = source.source,
                key = source.key,
                @operator = source.@operator,
                valueType = source.valueType,
                stringValue = source.stringValue,
                numberValue = source.numberValue,
                stringListValue = source.stringListValue?.ToList() ?? new List<string>()
            };
        }

        internal static TurnKitPlayerStoreTxCatalogMutationDraft Clone(TurnKitPlayerStoreTxCatalogMutationDraft source)
        {
            if (source == null)
            {
                return new TurnKitPlayerStoreTxCatalogMutationDraft();
            }

            return new TurnKitPlayerStoreTxCatalogMutationDraft
            {
                storeKey = source.storeKey,
                operation = source.operation,
                valueType = source.valueType,
                stringValue = source.stringValue,
                numberValue = source.numberValue,
                stringListValue = source.stringListValue?.ToList() ?? new List<string>()
            };
        }
    }
}
