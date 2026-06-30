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

        internal static void EnsureLeaderboardFoldout(TurnKitEditorWindowState state, string key, bool defaultValue)
        {
            if (!state.LeaderboardFoldouts.ContainsKey(key))
            {
                state.LeaderboardFoldouts[key] = defaultValue;
            }
        }

        internal static void ResetNewLeaderboardDraft(TurnKitEditorWindowState state)
        {
            state.NewLeaderboardSlug = string.Empty;
            state.NewLeaderboardDisplayName = string.Empty;
            state.NewLeaderboardMinScore = "0";
            state.NewLeaderboardMaxScore = "1000000";
            state.NewLeaderboardSortOrder = TurnKitLeaderboardSortOrder.DESC;
            state.NewLeaderboardScoreStrategy = TurnKitLeaderboardScoreStrategy.BEST_ONLY;
            state.NewLeaderboardResetFrequency = TurnKitLeaderboardResetFrequency.NONE;
            state.NewLeaderboardArchiveOnReset = false;
            state.NewLeaderboardClientSubmitEnabled = false;
        }

        internal static void ResetSelectedLeaderboardDraft(TurnKitEditorWindowState state)
        {
            state.SelectedLeaderboardSlug = string.Empty;
            state.SelectedLeaderboardDraft = TurnKitLeaderboardDrafts.CreateEmpty();
        }

        internal static void ResetNewPlayerStoreDraft(TurnKitEditorWindowState state)
        {
            state.NewPlayerStoreKey = string.Empty;
            state.NewPlayerStoreCooldownDuration = string.Empty;
            state.NewPlayerStoreNumberMin = string.Empty;
            state.NewPlayerStoreNumberMax = string.Empty;
        }

        internal static void ResetTxCatalogDraft(TurnKitEditorWindowState state)
        {
            state.SelectedTxCatalogTransactionId = string.Empty;
            state.TxCatalogDraft = TurnKitPlayerStoreTxCatalogDrafts.CreateEmptyEntry();
        }

        internal static void ResetPurchaseMappingDraft(TurnKitEditorWindowState state)
        {
            state.SelectedPurchaseMappingKey = string.Empty;
            state.PurchaseMappingDraft = TurnKitPlayerStorePurchaseMappingDrafts.CreateEmptyEntry();
        }

        internal static void ResetGooglePlayAppConfigDraft(TurnKitEditorWindowState state)
        {
            state.GooglePlayAppConfigDraft = TurnKitGooglePlayAppConfigDrafts.CreateEmpty();
            state.GooglePlayAppConfigLoaded = false;
        }
    }
}
