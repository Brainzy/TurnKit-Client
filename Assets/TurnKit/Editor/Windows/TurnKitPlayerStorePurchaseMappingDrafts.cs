using System;

namespace TurnKit.Editor
{
    internal enum TurnKitStorePurchaseProvider
    {
        GOOGLE_PLAY
    }

    internal enum TurnKitStorePurchaseType
    {
        PRODUCT,
        SUBSCRIPTION
    }

    [Serializable]
    internal sealed class TurnKitPlayerStorePurchaseMappingEntry
    {
        public string id;
        public TurnKitStorePurchaseProvider provider = TurnKitStorePurchaseProvider.GOOGLE_PLAY;
        public TurnKitStorePurchaseType purchaseType = TurnKitStorePurchaseType.PRODUCT;
        public string productId;
        public string grantTransactionId;
        public string revokeTransactionId;
        public bool active = true;

        public string EditorKey => $"{provider}:{productId}";
    }

    internal static class TurnKitPlayerStorePurchaseMappingDrafts
    {
        internal static TurnKitPlayerStorePurchaseMappingEntry CreateEmptyEntry()
        {
            return new TurnKitPlayerStorePurchaseMappingEntry
            {
                provider = TurnKitStorePurchaseProvider.GOOGLE_PLAY,
                purchaseType = TurnKitStorePurchaseType.PRODUCT,
                active = true
            };
        }

        internal static TurnKitPlayerStorePurchaseMappingEntry Clone(TurnKitPlayerStorePurchaseMappingEntry source)
        {
            if (source == null)
            {
                return CreateEmptyEntry();
            }

            return new TurnKitPlayerStorePurchaseMappingEntry
            {
                id = source.id,
                provider = source.provider,
                purchaseType = source.purchaseType,
                productId = source.productId,
                grantTransactionId = source.grantTransactionId,
                revokeTransactionId = source.revokeTransactionId,
                active = source.active
            };
        }
    }
}
