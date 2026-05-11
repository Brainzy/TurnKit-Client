using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using TurnKit.Internal.SimpleJSON;
using UnityEngine.Networking;

namespace TurnKit
{
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
    }

    public sealed class PlayerStoreValueBuilder<TValue>
    {
        private readonly PlayerStoreToken<TValue> _token;
        private readonly TurnKitClientIdentity _identity;

        internal PlayerStoreValueBuilder(PlayerStoreToken<TValue> token, TurnKitClientIdentity identity)
        {
            _token = token;
            _identity = identity;
        }

        public async Task<TValue> Get()
        {
            if (!_token.ClientReadable)
            {
                throw new InvalidOperationException($"PlayerStore key '{_token.StoreKey}' is not client readable.");
            }

            using var request = TurnKitClientRequest.CreateGet($"/v1/client/player-store/{UnityWebRequest.EscapeURL(_token.StoreKey)}");
            await TurnKitClientRequest.PrepareIdentity(request, _identity);
            await TurnKitClientRequest.Send(request);
            return ParseResponseValue(request.downloadHandler.text, _token.ValueType, _token.StoreKey);
        }

        public async Task Set(TValue value)
        {
            if (!_token.ClientWritable)
            {
                throw new InvalidOperationException($"PlayerStore key '{_token.StoreKey}' is not client writable.");
            }

            string body = BuildRequestBody(value, _token.ValueType, _token.StoreKey);
            using var request = TurnKitClientRequest.CreateJson($"/v1/client/player-store/{UnityWebRequest.EscapeURL(_token.StoreKey)}", "PUT", body);
            await TurnKitClientRequest.PrepareIdentity(request, _identity);
            await TurnKitClientRequest.Send(request);
        }

        private static TValue ParseResponseValue(string json, TurnKitConfig.PlayerStoreValueType expectedType, string storeKey)
        {
            var root = JSON.Parse(json).AsObject;
            var valueNode = root?["value"];
            if (valueNode == null)
            {
                throw new Exception($"PlayerStore response missing value for '{storeKey}'.");
            }

            object parsed = expectedType switch
            {
                TurnKitConfig.PlayerStoreValueType.STRING => valueNode.Value,
                TurnKitConfig.PlayerStoreValueType.NUMBER => ParseDecimal(valueNode.Value, storeKey),
                TurnKitConfig.PlayerStoreValueType.STRING_LIST => ParseStringList(valueNode.AsArray),
                _ => throw new Exception($"Unsupported PlayerStore value type for '{storeKey}'.")
            };

            if (parsed is TValue typed)
            {
                return typed;
            }

            throw new Exception($"PlayerStore type mismatch for '{storeKey}'. Token expects {typeof(TValue).Name}, backend type is {expectedType}.");
        }

        private static string BuildRequestBody(TValue value, TurnKitConfig.PlayerStoreValueType expectedType, string storeKey)
        {
            string valueJson = expectedType switch
            {
                TurnKitConfig.PlayerStoreValueType.STRING => SerializeString(value, storeKey),
                TurnKitConfig.PlayerStoreValueType.NUMBER => SerializeDecimal(value, storeKey),
                TurnKitConfig.PlayerStoreValueType.STRING_LIST => SerializeStringList(value, storeKey),
                _ => throw new Exception($"Unsupported PlayerStore value type for '{storeKey}'.")
            };

            return "{\"value\":" + valueJson + "}";
        }

        private static string SerializeString(TValue value, string storeKey)
        {
            if (!(value is string text))
            {
                throw new Exception($"PlayerStore key '{storeKey}' expects string value.");
            }

            return ToJsonString(text ?? string.Empty);
        }

        private static string SerializeDecimal(TValue value, string storeKey)
        {
            if (!(value is decimal number))
            {
                throw new Exception($"PlayerStore key '{storeKey}' expects decimal value.");
            }

            return number.ToString(CultureInfo.InvariantCulture);
        }

        private static string SerializeStringList(TValue value, string storeKey)
        {
            if (!(value is IEnumerable<string> values))
            {
                throw new Exception($"PlayerStore key '{storeKey}' expects a string list value.");
            }

            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var item in values)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(ToJsonString(item ?? string.Empty));
                first = false;
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static decimal ParseDecimal(string raw, string storeKey)
        {
            if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                throw new Exception($"PlayerStore key '{storeKey}' expected decimal but got '{raw}'.");
            }

            return result;
        }

        private static IReadOnlyList<string> ParseStringList(JSONArray array)
        {
            var list = new List<string>();
            if (array == null)
            {
                return list;
            }

            foreach (JSONNode item in array)
            {
                list.Add(item?.Value ?? string.Empty);
            }

            return list;
        }

        private static string ToJsonString(string value)
        {
            string escaped = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }
    }
}
