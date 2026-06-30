using System;
using System.Collections.Generic;

namespace TurnKit.Editor
{
    [Serializable]
    internal sealed class TurnKitGooglePlayProductConfigEntry
    {
        public string productId;
        public TurnKitStorePurchaseType purchaseType = TurnKitStorePurchaseType.PRODUCT;
        public string grantTransactionId;
        public string revokeTransactionId;
        public bool active = true;
    }

    [Serializable]
    internal sealed class TurnKitGooglePlayAppConfigDraft
    {
        public string id;
        public string gameKeyId;
        public string appId;
        public string androidPackageName;
        public string googleServiceAccountJson;
        public bool googleServiceAccountJsonConfigured;
        public string createdAt;
        public string updatedAt;
        public List<TurnKitGooglePlayProductConfigEntry> products = new();
    }

    internal static class TurnKitGooglePlayAppConfigDrafts
    {
        internal static TurnKitGooglePlayAppConfigDraft CreateEmpty()
        {
            return new TurnKitGooglePlayAppConfigDraft
            {
                products = new List<TurnKitGooglePlayProductConfigEntry>()
            };
        }

        internal static TurnKitGooglePlayAppConfigDraft Clone(TurnKitGooglePlayAppConfigDraft source)
        {
            if (source == null)
            {
                return CreateEmpty();
            }

            var clone = new TurnKitGooglePlayAppConfigDraft
            {
                id = source.id,
                gameKeyId = source.gameKeyId,
                appId = source.appId,
                androidPackageName = source.androidPackageName,
                googleServiceAccountJson = source.googleServiceAccountJson,
                googleServiceAccountJsonConfigured = source.googleServiceAccountJsonConfigured,
                createdAt = source.createdAt,
                updatedAt = source.updatedAt,
                products = new List<TurnKitGooglePlayProductConfigEntry>()
            };

            foreach (var product in source.products ?? new List<TurnKitGooglePlayProductConfigEntry>())
            {
                clone.products.Add(Clone(product));
            }

            return clone;
        }

        internal static TurnKitGooglePlayProductConfigEntry CreateEmptyProduct()
        {
            return new TurnKitGooglePlayProductConfigEntry
            {
                purchaseType = TurnKitStorePurchaseType.PRODUCT,
                active = true
            };
        }

        internal static TurnKitGooglePlayProductConfigEntry Clone(TurnKitGooglePlayProductConfigEntry source)
        {
            if (source == null)
            {
                return CreateEmptyProduct();
            }

            return new TurnKitGooglePlayProductConfigEntry
            {
                productId = source.productId,
                purchaseType = source.purchaseType,
                grantTransactionId = source.grantTransactionId,
                revokeTransactionId = source.revokeTransactionId,
                active = source.active
            };
        }
    }
}
