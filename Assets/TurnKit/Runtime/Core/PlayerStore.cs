using System;

namespace TurnKit
{
    public readonly struct PlayerStoreValueResult<TValue>
    {
        public readonly TValue Value;
        public readonly string UpdatedAtRaw;

        public bool HasUpdatedAt => !string.IsNullOrWhiteSpace(UpdatedAtRaw);

        public DateTimeOffset? UpdatedAt =>
            DateTimeOffset.TryParse(UpdatedAtRaw, out var parsed) ? parsed : null;

        public PlayerStoreValueResult(TValue value, string updatedAtRaw)
        {
            Value = value;
            UpdatedAtRaw = updatedAtRaw;
        }
    }

    public readonly struct PlayerStoreToken<TValue>
    {
        internal readonly string StoreKey;
        internal readonly TurnKitConfig.PlayerStoreValueType ValueType;
        internal readonly bool ClientWritable;
        internal readonly bool ClientReadable;

        public PlayerStoreToken(string storeKey, TurnKitConfig.PlayerStoreValueType valueType, bool clientWritable, bool clientReadable)
        {
            StoreKey = storeKey ?? throw new ArgumentNullException(nameof(storeKey));
            ValueType = valueType;
            ClientWritable = clientWritable;
            ClientReadable = clientReadable;
        }

        public string Name => StoreKey;
    }

    public static class PlayerStore
    {
        public static PlayerStoreValueBuilder<TValue> Value<TValue>(PlayerStoreToken<TValue> token)
        {
            var playerId = Relay.CurrentPlayerId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new InvalidOperationException("PlayerStore.Value(token) requires a relay session with CurrentPlayerId. Use an overload with explicit identity.");
            }

            return new PlayerStoreValueBuilder<TValue>(token, TurnKitClientIdentity.NoAuth(playerId));
        }

        public static PlayerStoreValueBuilder<TValue> Value<TValue>(PlayerStoreToken<TValue> token, string playerId)
        {
            return new PlayerStoreValueBuilder<TValue>(token, TurnKitClientIdentity.NoAuth(playerId));
        }

        public static PlayerStoreValueBuilder<TValue> Value<TValue>(PlayerStoreToken<TValue> token, TurnKitPlayerSession session)
        {
            return new PlayerStoreValueBuilder<TValue>(token, TurnKitClientIdentity.Authenticated(session));
        }

        public static PlayerStoreValueBuilder<TValue> Value<TValue>(PlayerStoreToken<TValue> token, TurnKitYourBackendProof proof)
        {
            return new PlayerStoreValueBuilder<TValue>(token, TurnKitClientIdentity.YourBackend(proof));
        }

        public static PlayerStoreTransactionBuilder Transaction(string transactionId)
        {
            var playerId = Relay.CurrentPlayerId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new InvalidOperationException("PlayerStore.Transaction(transactionId) requires a relay session with CurrentPlayerId. Use an overload with explicit identity.");
            }

            return new PlayerStoreTransactionBuilder(transactionId, TurnKitClientIdentity.NoAuth(playerId));
        }

        public static PlayerStoreTransactionBuilder Transaction(string transactionId, string playerId)
        {
            return new PlayerStoreTransactionBuilder(transactionId, TurnKitClientIdentity.NoAuth(playerId));
        }

        public static PlayerStoreTransactionBuilder Transaction(string transactionId, TurnKitPlayerSession session)
        {
            return new PlayerStoreTransactionBuilder(transactionId, TurnKitClientIdentity.Authenticated(session));
        }

        public static PlayerStoreTransactionBuilder Transaction(string transactionId, TurnKitYourBackendProof proof)
        {
            return new PlayerStoreTransactionBuilder(transactionId, TurnKitClientIdentity.YourBackend(proof));
        }

        public static GooglePlayPurchaseVerifyBuilder GooglePlayPurchase(
            string packageName,
            string productId,
            string purchaseToken,
            StorePurchaseType purchaseType)
        {
            var playerId = Relay.CurrentPlayerId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new InvalidOperationException("PlayerStore.GooglePlayPurchase(...) requires a relay session with CurrentPlayerId. Use an overload with explicit identity.");
            }

            return new GooglePlayPurchaseVerifyBuilder(
                packageName,
                productId,
                purchaseToken,
                purchaseType,
                TurnKitClientIdentity.NoAuth(playerId));
        }

        public static GooglePlayPurchaseVerifyBuilder GooglePlayPurchase(
            string packageName,
            string productId,
            string purchaseToken,
            StorePurchaseType purchaseType,
            string playerId)
        {
            return new GooglePlayPurchaseVerifyBuilder(
                packageName,
                productId,
                purchaseToken,
                purchaseType,
                TurnKitClientIdentity.NoAuth(playerId));
        }

        public static GooglePlayPurchaseVerifyBuilder GooglePlayPurchase(
            string packageName,
            string productId,
            string purchaseToken,
            StorePurchaseType purchaseType,
            TurnKitPlayerSession session)
        {
            return new GooglePlayPurchaseVerifyBuilder(
                packageName,
                productId,
                purchaseToken,
                purchaseType,
                TurnKitClientIdentity.Authenticated(session));
        }

        public static GooglePlayPurchaseVerifyBuilder GooglePlayPurchase(
            string packageName,
            string productId,
            string purchaseToken,
            StorePurchaseType purchaseType,
            TurnKitYourBackendProof proof)
        {
            return new GooglePlayPurchaseVerifyBuilder(
                packageName,
                productId,
                purchaseToken,
                purchaseType,
                TurnKitClientIdentity.YourBackend(proof));
        }
    }

    public enum StorePurchaseType
    {
        PRODUCT = 0,
        SUBSCRIPTION = 1
    }

    public readonly struct GooglePlayPurchaseVerifyResult
    {
        public readonly bool Verified;
        public readonly bool Granted;
        public readonly bool AlreadyGranted;
        public readonly string OrderId;
        public readonly string State;

        public bool Succeeded => Verified && (Granted || AlreadyGranted);

        public GooglePlayPurchaseVerifyResult(bool verified, bool granted, bool alreadyGranted, string orderId, string state)
        {
            Verified = verified;
            Granted = granted;
            AlreadyGranted = alreadyGranted;
            OrderId = orderId;
            State = state;
        }
    }

    public readonly struct PlayerStoreTransactionResult
    {
        public readonly string TransactionId;
        public readonly bool Applied;
        public readonly bool AlreadyApplied;

        public bool Succeeded => Applied || AlreadyApplied;

        public PlayerStoreTransactionResult(string transactionId, bool applied, bool alreadyApplied)
        {
            TransactionId = transactionId;
            Applied = applied;
            AlreadyApplied = alreadyApplied;
        }
    }

    public class PlayerStoreTransactionConditionFailedException : Exception
    {
        public PlayerStoreTransactionConditionFailedException(string message) : base(message) { }
    }

    public class PlayerStoreTransactionNotAllowedException : Exception
    {
        public PlayerStoreTransactionNotAllowedException(string message) : base(message) { }
    }

    public class PlayerStoreInvalidTransactionIdException : Exception
    {
        public PlayerStoreInvalidTransactionIdException(string message) : base($"PlayerStore transactionId is invalid. {message}") { }
    }

    public class PlayerStoreTransactionMismatchException : Exception
    {
        public PlayerStoreTransactionMismatchException(string message) : base($"PlayerStore transaction payload mismatch. {message}") { }
    }

    public class PlayerStoreCooldownActiveException : Exception
    {
        public PlayerStoreCooldownActiveException(string message) : base($"PlayerStore cooldown active. {message}") { }
    }

    [Serializable]
    internal sealed class PlayerStoreTransactionRequest
    {
        public string transactionId;
    }

    [Serializable]
    internal sealed class PlayerStoreTransactionResponse
    {
        public string transactionId;
        public bool applied;
        public bool alreadyApplied;
    }

    [Serializable]
    internal sealed class GooglePlayPurchaseVerifyRequest
    {
        public string packageName;
        public string productId;
        public string purchaseToken;
        public StorePurchaseType purchaseType;
    }

    [Serializable]
    internal sealed class GooglePlayPurchaseVerifyResponse
    {
        public bool verified;
        public bool granted;
        public bool alreadyGranted;
        public string orderId;
        public string state;
    }
}
