using System;

namespace TurnKit.Editor
{
    internal static class TurnKitSyncStateEvaluator
    {
        internal static bool HasUnsyncedChanges(TurnKitConfig config, Action<TurnKitConfig.RelayConfig> normalizeRelayConfig)
        {
            bool hasEnumChanges = EnumGenerator.HasChanges(config);
            return ComputeUnsyncedChanges(config, hasEnumChanges, normalizeRelayConfig);
        }

        internal static bool ComputeUnsyncedChanges(TurnKitConfig config, bool hasEnumChanges, Action<TurnKitConfig.RelayConfig> normalizeRelayConfig)
        {
            if (hasEnumChanges)
            {
                return true;
            }

            if (config?.relayConfigs == null)
            {
                return false;
            }

            foreach (var relay in config.relayConfigs)
            {
                if (relay == null)
                {
                    continue;
                }

                normalizeRelayConfig?.Invoke(relay);
                if (string.IsNullOrEmpty(relay.id) && ((relay.lists?.Count ?? 0) > 0 || (relay.trackedStats?.Count ?? 0) > 0))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
