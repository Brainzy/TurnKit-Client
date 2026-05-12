using System;
using UnityEditor;

namespace TurnKit.Editor
{
    internal static class TurnKitConfigPersistence
    {
        internal static void SaveConfigOnly(TurnKitConfig config, Action invalidateSyncStateCache)
        {
            if (config == null)
            {
                return;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            invalidateSyncStateCache?.Invoke();
        }

        internal static void SaveAndRegenerate(TurnKitConfig config, Action invalidateSyncStateCache)
        {
            if (config == null)
            {
                return;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EnumGenerator.Generate(config);
            invalidateSyncStateCache?.Invoke();
        }

        internal static void MarkConfigDirty(TurnKitConfig config)
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
            }
        }
    }
}
