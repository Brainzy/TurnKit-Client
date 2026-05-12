using System;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitRelaySyncCoordinator
    {
        internal static void PullFromServer(TurnKitConfig config, Func<string> getSessionToken, Action onPullSuccess)
        {
            Debug.Log("[TurnKit] Pulling relay configs from server...");
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchRelayConfigs(
                    config.gameKeyId,
                    getSessionToken(),
                    configs =>
                    {
                        config.relayConfigs = configs;
                        onPullSuccess?.Invoke();
                        Debug.Log($"[TurnKit] Pulled {configs.Count} relay config(s)");
                        EditorUtility.DisplayDialog("Success", $"Pulled {configs.Count} relay config(s) from server", "OK");
                    },
                    error =>
                    {
                        Debug.LogError($"[TurnKit] Failed to pull configs: {error}");
                        EditorUtility.DisplayDialog("Pull Failed", $"Failed to pull configs: {error}", "OK");
                    }));
        }

        internal static void PushToServer(
            TurnKitConfig config,
            Func<string> getSessionToken,
            Action<TurnKitConfig.RelayConfig, TurnKitConfig.RelayConfig> mergeRelayFromServer,
            Action markDirty,
            Action onPushComplete)
        {
            Debug.Log("[TurnKit] Pushing relay configs to server...");
            int successCount = 0;
            int totalCount = config.relayConfigs.Count;

            foreach (var relay in config.relayConfigs)
            {
                EditorCoroutineRunner.StartCoroutine(
                    TurnKitAPI.PushRelayConfig(
                        config.gameKeyId,
                        relay,
                        getSessionToken(),
                        updatedRelay =>
                        {
                            mergeRelayFromServer?.Invoke(relay, updatedRelay);
                            markDirty?.Invoke();
                            successCount++;

                            if (successCount == totalCount)
                            {
                                onPushComplete?.Invoke();
                                EditorUtility.DisplayDialog("Push Complete", $"{successCount} config(s) pushed successfully", "OK");
                            }
                        },
                        error =>
                        {
                            Debug.LogError($"[TurnKit] Failed to push {relay.slug}: {error}");
                            EditorUtility.DisplayDialog("Push Failed", $"Failed to push {relay.slug}: {error}", "OK");
                        }));
            }
        }
    }
}
