using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public partial class TurnKitEditorWindow : EditorWindow
    {
        private TurnKitConfig config;
        private Vector2 scrollPosition;
        private readonly Dictionary<string, bool> configFoldouts = new();
        private readonly Dictionary<string, bool> webhookFoldouts = new();
        private List<TurnKitConfig.WebhookConfig> webhooks = new();
        private string newPlayerStoreKey = string.Empty;
        private TurnKitConfig.PlayerStoreValueType newPlayerStoreValueType = TurnKitConfig.PlayerStoreValueType.STRING;
        private bool newPlayerStoreClientWritable = true;
        private bool newPlayerStoreClientReadable = true;
        private string newPlayerStoreNumberMin = string.Empty;
        private string newPlayerStoreNumberMax = string.Empty;
        private bool cachedHasEnumChanges = true;
        private bool cachedHasUnsyncedChanges = true;
        private double nextSyncStateRefreshAt;

        [MenuItem("Tools/TurnKit/Configuration", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<TurnKitEditorWindow>("TurnKit Configuration");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
            LoadWebhooks();
            LoadPlayerStoreDefs();
            InvalidateSyncStateCache();
        }

        private void LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:TurnKitConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<TurnKitConfig>(path);
            }
            else
            {
                Debug.LogError("[TurnKit] No TurnKitConfig found. Create one first.");
            }
        }

        private void CreateNewRelayConfig()
        {
            var newConfig = new TurnKitConfig.RelayConfig
            {
                slug = "new_config",
                maxPlayers = 2,
                turnEnforcement = TurnKitConfig.TurnEnforcement.ROUND_ROBIN,
                votingEnabled = false,
                votingMode = TurnKitConfig.VotingMode.SYNC,
                votesRequired = 1,
                votesToFail = 1,
                failAction = TurnKitConfig.FailAction.SKIP_TURN,
                matchTimeoutMinutes = 10,
                turnTimeoutSeconds = 60,
                afkTurnTimerSeconds = 0,
                disconnectedTurnTimerSeconds = 0,
                waitReconnectSeconds = 45,
                reconnectMoveHistorySize = 0,
                onTurnTimeout = TurnKitConfig.OnTurnTimeout.CHANGE_TO_NEXT_PLAYER,
                revealPrivateListsOnTimeout = false,
                ignoreAllOwnership = false,
                lists = new List<TurnKitConfig.RelayListConfig>(),
                trackedStats = new List<TurnKitConfig.TrackedStatConfig>(),
                queueRequirements = new List<TurnKitConfig.QueueRequirementConfig>(),
                playerStoreMutations = new List<TurnKitConfig.PlayerStoreMutationConfig>()
            };

            config.relayConfigs.Add(newConfig);
            TurnKitConfigPersistence.SaveConfigOnly(config, InvalidateSyncStateCache);
            Debug.Log("[TurnKit] Created new relay config (local only - push to save to server)");
            Repaint();
        }

        private void DeleteRelayConfig(int index)
        {
            var relay = config.relayConfigs[index];
            if (!string.IsNullOrEmpty(relay.id))
            {
                EditorCoroutineRunner.StartCoroutine(
                    TurnKitAPI.DeleteRelayConfig(
                        config.gameKeyId,
                        relay.slug,
                        EditorPrefs.GetString("TurnKit_SessionToken"),
                        () =>
                        {
                            config.relayConfigs.RemoveAt(index);
                            TurnKitConfigPersistence.SaveConfigOnly(config, InvalidateSyncStateCache);
                            Debug.Log($"[TurnKit] Deleted relay config: {relay.slug}");
                            Repaint();
                        },
                        error =>
                        {
                            Debug.LogError($"[TurnKit] Failed to delete relay config: {error}");
                            EditorUtility.DisplayDialog("Delete Failed", $"Failed to delete: {error}", "OK");
                        }));
            }
            else
            {
                config.relayConfigs.RemoveAt(index);
                TurnKitConfigPersistence.SaveConfigOnly(config, InvalidateSyncStateCache);
                Debug.Log($"[TurnKit] Removed local relay config: {relay.slug}");
            }
        }

        private bool ValidateConfigs()
        {
            if (!TurnKitConfigValidator.TryValidateConfig(config, out var errors))
            {
                string message = "Validation failed:\n\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("Validation Errors", message, "OK");
                return false;
            }

            TurnKitConfigPersistence.MarkConfigDirty(config);
            InvalidateSyncStateCache();
            return true;
        }

        private void PullFromServer()
        {
            TurnKitRelaySyncCoordinator.PullFromServer(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                () =>
                {
                    TurnKitConfigPersistence.SaveAndRegenerate(config, InvalidateSyncStateCache);
                    LoadPlayerStoreDefs();
                    LoadWebhooks();
                    Repaint();
                });
        }

        private void PushToServer()
        {
            TurnKitRelaySyncCoordinator.PushToServer(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                TurnKitRelayMergePolicy.MergeRelayFromServer,
                () => TurnKitConfigPersistence.MarkConfigDirty(config),
                () =>
                {
                    TurnKitConfigPersistence.SaveAndRegenerate(config, InvalidateSyncStateCache);
                    Repaint();
                });
        }

        private void LoadWebhooks()
        {
            TurnKitWebhookService.Load(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                loaded => webhooks = loaded,
                Repaint);
        }

        private void LoadPlayerStoreDefs()
        {
            TurnKitPlayerStoreDefService.Load(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                loaded =>
                {
                    config.playerStoreDefs = loaded;
                    TurnKitConfigPersistence.SaveAndRegenerate(config, InvalidateSyncStateCache);
                    Repaint();
                },
                TurnKitPlayerStoreDefService.ShowLoadError);
        }

        private void CreatePlayerStoreDef()
        {
            if (!TryParseOptionalDouble(newPlayerStoreNumberMin, out var numberMin, out var minError))
            {
                EditorUtility.DisplayDialog("Player Store Def Invalid", minError, "OK");
                return;
            }

            if (!TryParseOptionalDouble(newPlayerStoreNumberMax, out var numberMax, out var maxError))
            {
                EditorUtility.DisplayDialog("Player Store Def Invalid", maxError, "OK");
                return;
            }

            var def = new TurnKitConfig.PlayerStoreDefConfig
            {
                storeKey = newPlayerStoreKey?.Trim(),
                valueType = newPlayerStoreValueType,
                clientWritable = newPlayerStoreClientWritable,
                clientReadable = newPlayerStoreClientReadable,
                numberMin = newPlayerStoreValueType == TurnKitConfig.PlayerStoreValueType.NUMBER ? numberMin : null,
                numberMax = newPlayerStoreValueType == TurnKitConfig.PlayerStoreValueType.NUMBER ? numberMax : null
            };

            if (!TurnKitConfigValidator.TryValidatePlayerStoreDef(config, def, out var error))
            {
                EditorUtility.DisplayDialog("Player Store Def Invalid", error, "OK");
                return;
            }

            TurnKitPlayerStoreDefService.Create(
                config,
                def,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                () =>
                {
                    newPlayerStoreKey = string.Empty;
                    newPlayerStoreNumberMin = string.Empty;
                    newPlayerStoreNumberMax = string.Empty;
                    InvalidateSyncStateCache();
                    LoadPlayerStoreDefs();
                },
                TurnKitPlayerStoreDefService.ShowCreateError);
        }

        private static bool TryParseOptionalDouble(string raw, out double? value, out string error)
        {
            value = null;
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            if (!double.TryParse(raw, out var parsed))
            {
                error = $"Invalid number bounds value '{raw}'.";
                return false;
            }

            value = parsed;
            return true;
        }

        private void DeletePlayerStoreDef(TurnKitConfig.PlayerStoreDefConfig def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.storeKey))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Player Store Def", $"Delete '{def.storeKey}'?", "Delete", "Cancel"))
            {
                return;
            }

            TurnKitPlayerStoreDefService.Delete(
                config,
                def.storeKey,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                LoadPlayerStoreDefs,
                TurnKitPlayerStoreDefService.ShowDeleteError);
        }

        private void SaveWebhook(TurnKitConfig.WebhookConfig webhook)
        {
            TurnKitWebhookService.Save(
                config,
                webhook,
                webhooks,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                LoadWebhooks,
                Repaint);
        }

        private void DeleteWebhook(TurnKitConfig.WebhookConfig webhook)
        {
            TurnKitWebhookService.Delete(
                config,
                webhook,
                webhooks,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                LoadWebhooks,
                Repaint);
        }

        private static void NormalizeRelayConfig(TurnKitConfig.RelayConfig relay)
        {
            if (relay.lists == null)
            {
                relay.lists = new List<TurnKitConfig.RelayListConfig>();
            }

            if (relay.trackedStats == null)
            {
                relay.trackedStats = new List<TurnKitConfig.TrackedStatConfig>();
            }

            if (relay.queueRequirements == null)
            {
                relay.queueRequirements = new List<TurnKitConfig.QueueRequirementConfig>();
            }

            if (relay.playerStoreMutations == null)
            {
                relay.playerStoreMutations = new List<TurnKitConfig.PlayerStoreMutationConfig>();
            }

            relay.afkTurnTimerSeconds = Mathf.Max(0, relay.afkTurnTimerSeconds);
            relay.disconnectedTurnTimerSeconds = Mathf.Max(0, relay.disconnectedTurnTimerSeconds);
        }

        private void InvalidateSyncStateCache()
        {
            nextSyncStateRefreshAt = 0d;
        }

        private void RefreshSyncStateIfNeeded()
        {
            if (config == null)
            {
                cachedHasEnumChanges = false;
                cachedHasUnsyncedChanges = false;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextSyncStateRefreshAt)
            {
                return;
            }

            cachedHasEnumChanges = EnumGenerator.HasChanges(config);
            cachedHasUnsyncedChanges = TurnKitSyncStateEvaluator.ComputeUnsyncedChanges(config, cachedHasEnumChanges, NormalizeRelayConfig);
            nextSyncStateRefreshAt = now + 0.75d;
        }
    }
}

