using System;
using UnityEditor;

namespace TurnKit.Editor
{
    internal static class TurnKitEditorWindowStateController
    {
        internal static void InvalidateSyncStateCache(TurnKitEditorWindowState state)
        {
            state.NextSyncStateRefreshAt = 0d;
        }

        internal static void RefreshSyncStateIfNeeded(
            TurnKitEditorWindowState state,
            TurnKitConfig config,
            Action<TurnKitConfig.RelayConfig> normalizeRelayConfig)
        {
            if (config == null)
            {
                state.CachedHasEnumChanges = false;
                state.CachedHasUnsyncedChanges = false;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < state.NextSyncStateRefreshAt)
            {
                return;
            }

            state.CachedHasEnumChanges = EnumGenerator.HasChanges(config);
            state.CachedHasUnsyncedChanges = TurnKitSyncStateEvaluator.ComputeUnsyncedChanges(config, state.CachedHasEnumChanges, normalizeRelayConfig);
            state.NextSyncStateRefreshAt = now + 0.75d;
        }

        internal static void EnsureConfigFoldout(TurnKitEditorWindowState state, string key)
        {
            if (!state.ConfigFoldouts.ContainsKey(key))
            {
                state.ConfigFoldouts[key] = false;
            }
        }

        internal static void EnsureWebhookFoldout(TurnKitEditorWindowState state, string key, bool defaultValue)
        {
            if (!state.WebhookFoldouts.ContainsKey(key))
            {
                state.WebhookFoldouts[key] = defaultValue;
            }
        }

        internal static void ResetNewPlayerStoreDraft(TurnKitEditorWindowState state)
        {
            state.NewPlayerStoreKey = string.Empty;
            state.NewPlayerStoreNumberMin = string.Empty;
            state.NewPlayerStoreNumberMax = string.Empty;
        }
    }
}
