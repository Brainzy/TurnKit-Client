using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    internal static class PlayerStoreValueCodec
    {
        internal static PlayerStoreValueResult<T> ParseResponse<T>(string json, TurnKitConfig.PlayerStoreValueType expectedType, string storeKey)
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

            if (parsed is T typed)
            {
                return new PlayerStoreValueResult<T>(typed, root["updatedAt"]?.Value);
            }

            throw new Exception($"PlayerStore type mismatch for '{storeKey}'. Token expects {typeof(T).Name}, backend type is {expectedType}.");
        }

        internal static T ParseResponseValue<T>(string json, TurnKitConfig.PlayerStoreValueType expectedType, string storeKey)
        {
            return ParseResponse<T>(json, expectedType, storeKey).Value;
        }

        internal static string BuildRequestBody<T>(T value, TurnKitConfig.PlayerStoreValueType expectedType, string storeKey)
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

        private static string SerializeString<T>(T value, string storeKey)
        {
            if (value is not string text)
            {
                throw new Exception($"PlayerStore key '{storeKey}' expects string value.");
            }

            return ToJsonString(text ?? string.Empty);
        }

        private static string SerializeDecimal<T>(T value, string storeKey)
        {
            if (value is not decimal number)
            {
                throw new Exception($"PlayerStore key '{storeKey}' expects decimal value.");
            }

            return number.ToString(CultureInfo.InvariantCulture);
        }

        private static string SerializeStringList<T>(T value, string storeKey)
        {
            if (value is not IEnumerable<string> values)
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
