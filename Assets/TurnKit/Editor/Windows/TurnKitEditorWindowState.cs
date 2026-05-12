using System.Collections.Generic;

namespace TurnKit.Editor
{
    internal sealed class TurnKitEditorWindowState
    {
        internal readonly Dictionary<string, bool> ConfigFoldouts = new();
        internal readonly Dictionary<string, bool> WebhookFoldouts = new();
        internal List<TurnKitConfig.WebhookConfig> Webhooks = new();

        internal string NewPlayerStoreKey = string.Empty;
        internal TurnKitConfig.PlayerStoreValueType NewPlayerStoreValueType = TurnKitConfig.PlayerStoreValueType.STRING;
        internal bool NewPlayerStoreClientWritable = true;
        internal bool NewPlayerStoreClientReadable = true;
        internal string NewPlayerStoreNumberMin = string.Empty;
        internal string NewPlayerStoreNumberMax = string.Empty;

        internal bool CachedHasEnumChanges = true;
        internal bool CachedHasUnsyncedChanges = true;
        internal double NextSyncStateRefreshAt;
    }
}
