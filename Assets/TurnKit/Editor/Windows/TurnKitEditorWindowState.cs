using System.Collections.Generic;

namespace TurnKit.Editor
{
    internal sealed class TurnKitEditorWindowState
    {
        internal readonly Dictionary<string, bool> ConfigFoldouts = new();
        internal readonly Dictionary<string, bool> WebhookFoldouts = new();
        internal readonly Dictionary<string, bool> LeaderboardFoldouts = new();
        internal List<TurnKitConfig.WebhookConfig> Webhooks = new();

        internal string NewLeaderboardSlug = string.Empty;
        internal string NewLeaderboardDisplayName = string.Empty;
        internal string NewLeaderboardMinScore = "0";
        internal string NewLeaderboardMaxScore = "1000000";
        internal TurnKitLeaderboardSortOrder NewLeaderboardSortOrder = TurnKitLeaderboardSortOrder.DESC;
        internal TurnKitLeaderboardScoreStrategy NewLeaderboardScoreStrategy = TurnKitLeaderboardScoreStrategy.BEST_ONLY;
        internal TurnKitLeaderboardResetFrequency NewLeaderboardResetFrequency = TurnKitLeaderboardResetFrequency.NONE;
        internal bool NewLeaderboardArchiveOnReset;
        internal bool NewLeaderboardClientSubmitEnabled;
        internal TurnKitLeaderboardDraft SelectedLeaderboardDraft = TurnKitLeaderboardDrafts.CreateEmpty();
        internal string SelectedLeaderboardSlug = string.Empty;
        internal string NewPlayerStoreKey = string.Empty;
        internal TurnKitConfig.PlayerStoreValueType NewPlayerStoreValueType = TurnKitConfig.PlayerStoreValueType.STRING;
        internal bool NewPlayerStoreClientWritable = true;
        internal bool NewPlayerStoreClientReadable = true;
        internal string NewPlayerStoreCooldownDuration = string.Empty;
        internal string NewPlayerStoreNumberMin = string.Empty;
        internal string NewPlayerStoreNumberMax = string.Empty;
        internal List<TurnKitPlayerStoreTxCatalogEntry> TxCatalogEntries = new();
        internal TurnKitPlayerStoreTxCatalogEntry TxCatalogDraft = TurnKitPlayerStoreTxCatalogDrafts.CreateEmptyEntry();
        internal string SelectedTxCatalogTransactionId = string.Empty;
        internal List<TurnKitPlayerStorePurchaseMappingEntry> PurchaseMappings = new();
        internal TurnKitPlayerStorePurchaseMappingEntry PurchaseMappingDraft = TurnKitPlayerStorePurchaseMappingDrafts.CreateEmptyEntry();
        internal string SelectedPurchaseMappingKey = string.Empty;
        internal TurnKitGooglePlayAppConfigDraft GooglePlayAppConfigDraft = TurnKitGooglePlayAppConfigDrafts.CreateEmpty();
        internal bool GooglePlayAppConfigLoaded;

        internal bool CachedHasEnumChanges = true;
        internal bool CachedHasUnsyncedChanges = true;
        internal double NextSyncStateRefreshAt;
    }

    internal sealed class TurnKitLeaderboardDraft
    {
        internal string id = string.Empty;
        internal string slug = string.Empty;
        internal string displayName = string.Empty;
        internal TurnKitLeaderboardSortOrder sortOrder = TurnKitLeaderboardSortOrder.DESC;
        internal TurnKitLeaderboardScoreStrategy scoreStrategy = TurnKitLeaderboardScoreStrategy.BEST_ONLY;
        internal string minScore = "0";
        internal string maxScore = "1000000";
        internal TurnKitLeaderboardResetFrequency resetFrequency = TurnKitLeaderboardResetFrequency.NONE;
        internal bool archiveOnReset;
        internal bool clientSubmitEnabled;
        internal string nextResetAt = string.Empty;
    }

    internal static class TurnKitLeaderboardDrafts
    {
        internal static TurnKitLeaderboardDraft CreateEmpty()
        {
            return new TurnKitLeaderboardDraft();
        }

        internal static TurnKitLeaderboardDraft FromConfig(TurnKitConfig.LeaderboardConfig config)
        {
            if (config == null)
            {
                return CreateEmpty();
            }

            return new TurnKitLeaderboardDraft
            {
                id = config.id ?? string.Empty,
                slug = config.slug ?? string.Empty,
                displayName = config.displayName ?? string.Empty,
                sortOrder = ParseEnum(config.sortOrder, TurnKitLeaderboardSortOrder.DESC),
                scoreStrategy = ParseEnum(config.scoreStrategy, TurnKitLeaderboardScoreStrategy.BEST_ONLY),
                minScore = config.minScore.ToString("G"),
                maxScore = config.maxScore.ToString("G"),
                resetFrequency = ParseEnum(config.resetFrequency, TurnKitLeaderboardResetFrequency.NONE),
                archiveOnReset = config.archiveOnReset,
                clientSubmitEnabled = config.clientSubmitEnabled,
                nextResetAt = config.nextResetAt ?? string.Empty
            };
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return System.Enum.TryParse(value, true, out TEnum parsed) ? parsed : fallback;
        }
    }

    internal enum TurnKitLeaderboardSortOrder
    {
        ASC,
        DESC
    }

    internal enum TurnKitLeaderboardScoreStrategy
    {
        BEST_ONLY,
        MULTIPLE_ENTRIES,
        CUMULATIVE
    }

    internal enum TurnKitLeaderboardResetFrequency
    {
        NONE,
        DAILY,
        WEEKLY,
        MONTHLY
    }
}
