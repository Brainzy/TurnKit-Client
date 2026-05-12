using System.Collections.Generic;
using System.Linq;

namespace TurnKit.Editor
{
    internal static class TurnKitRelayMergePolicy
    {
        internal static void MergeRelayFromServer(TurnKitConfig.RelayConfig localRelay, TurnKitConfig.RelayConfig serverRelay)
        {
            if (localRelay == null || serverRelay == null)
            {
                return;
            }

            localRelay.id = serverRelay.id;
            MergeListsByName(localRelay, serverRelay);
            MergeTrackedStatsByName(localRelay, serverRelay);
        }

        internal static void MergeListsByName(TurnKitConfig.RelayConfig localRelay, TurnKitConfig.RelayConfig serverRelay)
        {
            if (localRelay?.lists == null || serverRelay?.lists == null)
            {
                return;
            }

            var serverByName = serverRelay.lists
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.name))
                .ToDictionary(item => item.name, item => item);

            foreach (var local in localRelay.lists)
            {
                if (local == null || string.IsNullOrWhiteSpace(local.name))
                {
                    continue;
                }

                if (serverByName.TryGetValue(local.name, out var server))
                {
                    local.id = server.id;
                }
            }
        }

        internal static void MergeTrackedStatsByName(TurnKitConfig.RelayConfig localRelay, TurnKitConfig.RelayConfig serverRelay)
        {
            if (localRelay?.trackedStats == null || serverRelay?.trackedStats == null)
            {
                return;
            }

            var serverByName = serverRelay.trackedStats
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.name))
                .ToDictionary(item => item.name, item => item);

            foreach (var local in localRelay.trackedStats)
            {
                if (local == null || string.IsNullOrWhiteSpace(local.name))
                {
                    continue;
                }

                if (serverByName.TryGetValue(local.name, out var server))
                {
                    local.id = server.id;
                }
            }
        }
    }
}
