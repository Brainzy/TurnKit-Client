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
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            InvalidateSyncStateCache();
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
                            EditorUtility.SetDirty(config);
                            AssetDatabase.SaveAssets();
                            InvalidateSyncStateCache();
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
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                InvalidateSyncStateCache();
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

            EditorUtility.SetDirty(config);
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
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                    EnumGenerator.Generate(config);
                    InvalidateSyncStateCache();
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
                () => EditorUtility.SetDirty(config),
                () =>
                {
                    AssetDatabase.SaveAssets();
                    EnumGenerator.Generate(config);
                    InvalidateSyncStateCache();
                    Repaint();
                });
        }

        private void LoadWebhooks()
        {
            if (config == null || string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchWebhooks(
                    config.gameKeyId,
                    EditorPrefs.GetString("TurnKit_SessionToken"),
                    loaded =>
                    {
                        webhooks = loaded ?? new List<TurnKitConfig.WebhookConfig>();
                        Repaint();
                    },
                    error => Debug.LogWarning($"[TurnKit] Failed to load webhooks: {error}")));
        }

        private void LoadPlayerStoreDefs()
        {
            if (config == null || string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchPlayerStoreDefs(
                    config.gameKeyId,
                    EditorPrefs.GetString("TurnKit_SessionToken"),
                    loaded =>
                    {
                        config.playerStoreDefs = loaded ?? new List<TurnKitConfig.PlayerStoreDefConfig>();
                        EditorUtility.SetDirty(config);
                        AssetDatabase.SaveAssets();
                        EnumGenerator.Generate(config);
                        InvalidateSyncStateCache();
                        Repaint();
                    },
                    error => Debug.LogWarning($"[TurnKit] Failed to load player store defs: {error}")));
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

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.CreatePlayerStoreDef(
                    config.gameKeyId,
                    def,
                    EditorPrefs.GetString("TurnKit_SessionToken"),
                    _ =>
                    {
                        newPlayerStoreKey = string.Empty;
                        newPlayerStoreNumberMin = string.Empty;
                        newPlayerStoreNumberMax = string.Empty;
                        InvalidateSyncStateCache();
                        LoadPlayerStoreDefs();
                    },
                    err =>
                    {
                        Debug.LogError($"[TurnKit] Failed to create player store def: {err}");
                        EditorUtility.DisplayDialog("Create Failed", err, "OK");
                    }));
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

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.DeletePlayerStoreDef(
                    config.gameKeyId,
                    def.storeKey,
                    EditorPrefs.GetString("TurnKit_SessionToken"),
                    LoadPlayerStoreDefs,
                    err =>
                    {
                        Debug.LogError($"[TurnKit] Failed to delete player store def: {err}");
                        EditorUtility.DisplayDialog("Delete Failed", err, "OK");
                    }));
        }

        private void SaveWebhook(TurnKitConfig.WebhookConfig webhook)
        {
            if (string.IsNullOrWhiteSpace(webhook.id) || string.IsNullOrWhiteSpace(webhook.url))
            {
                EditorUtility.DisplayDialog("Webhook Invalid", "Webhook id and url are required.", "OK");
                return;
            }

            var coroutine = string.IsNullOrEmpty(webhook.entityId)
                ? TurnKitAPI.CreateWebhook(config.gameKeyId, webhook, EditorPrefs.GetString("TurnKit_SessionToken"), saved => ReplaceWebhook(webhook, saved), ShowWebhookError)
                : TurnKitAPI.UpdateWebhook(config.gameKeyId, webhook, EditorPrefs.GetString("TurnKit_SessionToken"), saved => ReplaceWebhook(webhook, saved), ShowWebhookError);
            EditorCoroutineRunner.StartCoroutine(coroutine);
        }

        private void ReplaceWebhook(TurnKitConfig.WebhookConfig original, TurnKitConfig.WebhookConfig saved)
        {
            int index = webhooks.IndexOf(original);
            if (index >= 0)
            {
                webhooks[index] = saved;
            }
            else
            {
                webhooks.Add(saved);
            }

            LoadWebhooks();
            RelayConfigEditWindow.ReloadAllOpenWebhookLists();
            Repaint();
        }

        private void DeleteWebhook(TurnKitConfig.WebhookConfig webhook)
        {
            if (string.IsNullOrEmpty(webhook.entityId))
            {
                webhooks.Remove(webhook);
                Repaint();
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Webhook", $"Delete webhook '{webhook.id}'?", "Delete", "Cancel"))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.DeleteWebhook(
                    config.gameKeyId,
                    webhook.id,
                    EditorPrefs.GetString("TurnKit_SessionToken"),
                    () =>
                    {
                        webhooks.Remove(webhook);
                        LoadWebhooks();
                        RelayConfigEditWindow.ReloadAllOpenWebhookLists();
                        Repaint();
                    },
                    ShowWebhookError));
        }

        private void ShowWebhookError(string error)
        {
            Debug.LogError($"[TurnKit] Webhook operation failed: {error}");
            EditorUtility.DisplayDialog("Webhook Error", error, "OK");
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

