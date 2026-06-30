using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitPlayerStoreKeyPopup
    {
        internal static string Draw(
            string label,
            string currentValue,
            IReadOnlyList<TurnKitConfig.PlayerStoreDefConfig> defs,
            string missingLabel = "Missing")
        {
            var keys = defs?
                .Where(def => def != null && !string.IsNullOrWhiteSpace(def.storeKey))
                .Select(def => def.storeKey)
                .Distinct()
                .OrderBy(key => key)
                .ToList() ?? new List<string>();

            if (keys.Count == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Popup(label, 0, new[] { "(no player store defs loaded)" });
                EditorGUI.EndDisabledGroup();
                return currentValue ?? string.Empty;
            }

            var values = new List<string>(keys);
            var labels = new List<string>(keys);

            if (!string.IsNullOrWhiteSpace(currentValue) && !values.Contains(currentValue))
            {
                values.Insert(0, currentValue);
                labels.Insert(0, $"{currentValue} ({missingLabel})");
            }

            int selectedIndex = Mathf.Max(0, values.IndexOf(currentValue ?? string.Empty));
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, labels.ToArray());
            return values[newIndex];
        }
    }
}
