using System;
using System.Collections.Generic;
using TurnKit.Internal.SimpleJSON;

namespace TurnKit
{
    internal static class RelayQueueRequestBuilder
    {
        public static string BuildQueueRequestJson(
            string slug,
            Dictionary<string, List<RelayItem>> items,
            FillPolicy fillPolicy,
            int? delegatedFillAfterSeconds)
        {
            var request = new JSONObject();
            request["slug"] = slug;
            request["items"] = BuildItemsNode(items);
            request["fillPolicy"] = fillPolicy.ToString();

            if (fillPolicy == FillPolicy.ALLOW_DELEGATED_SLOTS && delegatedFillAfterSeconds.HasValue)
            {
                request["delegatedFillAfterSeconds"] = Math.Max(0, delegatedFillAfterSeconds.Value);
            }

            return request.ToString();
        }

        public static bool HasQueueItems(Dictionary<string, List<RelayItem>> items)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            foreach (var kvp in items)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null || kvp.Value.Count == 0)
                {
                    continue;
                }

                foreach (var item in kvp.Value)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Slug))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static JSONNode BuildItemsNode(Dictionary<string, List<RelayItem>> items)
        {
            var obj = new JSONObject();
            if (items == null)
            {
                return obj;
            }

            foreach (var kvp in items)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null)
                {
                    continue;
                }

                var arr = new JSONArray();
                foreach (var item in kvp.Value)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    var itemNode = new JSONObject();
                    itemNode["id"] = item.Id;
                    itemNode["slug"] = item.Slug;
                    itemNode["creatorSlot"] = (int)item.CreatorSlot;
                    arr.Add(itemNode);
                }

                obj.Add(kvp.Key, arr);
            }

            return obj;
        }
    }
}
