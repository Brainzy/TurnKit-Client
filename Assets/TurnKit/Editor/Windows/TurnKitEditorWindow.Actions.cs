using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public partial class TurnKitEditorWindow : EditorWindow
    {
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
                loaded => state.Webhooks = loaded,
                Repaint);
        }

        private void LoadLeaderboards()
        {
            TurnKitLeaderboardService.Load(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                loaded =>
                {
                    config.leaderboards = loaded ?? new List<TurnKitConfig.LeaderboardConfig>();
                    config.defaultLeaderboard = ResolveDefaultLeaderboard(config.defaultLeaderboard, config.leaderboards);
                    TurnKitConfigPersistence.SaveConfigOnly(config, InvalidateSyncStateCache);
                    Repaint();
                },
                TurnKitLeaderboardService.ShowLoadError);
        }

        private void EditLeaderboard(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return;
            }

            var leaderboard = config?.leaderboards?.Find(entry => entry != null && entry.slug == slug);
            if (leaderboard == null)
            {
                return;
            }

            state.SelectedLeaderboardSlug = slug;
            state.SelectedLeaderboardDraft = TurnKitLeaderboardDrafts.FromConfig(leaderboard);
            TurnKitEditorWindowStateController.EnsureLeaderboardFoldout(state, slug, true);
            state.LeaderboardFoldouts[slug] = true;
            Repaint();
        }

        private void CreateLeaderboard()
        {
            var draft = new TurnKitLeaderboardDraft
            {
                slug = state.NewLeaderboardSlug?.Trim(),
                displayName = state.NewLeaderboardDisplayName?.Trim(),
                sortOrder = state.NewLeaderboardSortOrder,
                scoreStrategy = state.NewLeaderboardScoreStrategy,
                minScore = state.NewLeaderboardMinScore?.Trim(),
                maxScore = state.NewLeaderboardMaxScore?.Trim(),
                resetFrequency = state.NewLeaderboardResetFrequency,
                archiveOnReset = state.NewLeaderboardArchiveOnReset,
                clientSubmitEnabled = state.NewLeaderboardClientSubmitEnabled
            };

            if (!TurnKitConfigValidator.TryValidateLeaderboardCreateDraft(config, draft, out var error))
            {
                EditorUtility.DisplayDialog("Leaderboard Invalid", error, "OK");
                return;
            }

            TurnKitLeaderboardService.Create(
                config,
                draft,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                _ =>
                {
                    TurnKitEditorWindowStateController.ResetNewLeaderboardDraft(state);
                    InvalidateSyncStateCache();
                    LoadLeaderboards();
                },
                TurnKitLeaderboardService.ShowCreateError);
        }

        private void SaveLeaderboardDisplayName()
        {
            if (string.IsNullOrWhiteSpace(state.SelectedLeaderboardSlug))
            {
                return;
            }

            string displayName = state.SelectedLeaderboardDraft.displayName?.Trim();
            if (!TurnKitConfigValidator.TryValidateLeaderboardDisplayName(displayName, out var error))
            {
                EditorUtility.DisplayDialog("Leaderboard Invalid", error, "OK");
                return;
            }

            TurnKitLeaderboardService.UpdateDisplayName(
                config,
                state.SelectedLeaderboardSlug,
                displayName,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                _ => LoadLeaderboards(),
                TurnKitLeaderboardService.ShowUpdateError);
        }

        private void DeleteLeaderboard(TurnKitConfig.LeaderboardConfig leaderboard)
        {
            if (leaderboard == null || string.IsNullOrWhiteSpace(leaderboard.slug))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Leaderboard", $"Delete '{leaderboard.slug}' and all its scores?", "Delete", "Cancel"))
            {
                return;
            }

            TurnKitLeaderboardService.Delete(
                config,
                leaderboard.slug,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                () =>
                {
                    if (state.SelectedLeaderboardSlug == leaderboard.slug)
                    {
                        TurnKitEditorWindowStateController.ResetSelectedLeaderboardDraft(state);
                    }

                    LoadLeaderboards();
                },
                TurnKitLeaderboardService.ShowDeleteError);
        }

        private static string ResolveDefaultLeaderboard(string currentDefault, List<TurnKitConfig.LeaderboardConfig> leaderboards)
        {
            if (leaderboards == null || leaderboards.Count == 0)
            {
                return string.IsNullOrWhiteSpace(currentDefault) ? "global" : currentDefault;
            }

            if (!string.IsNullOrWhiteSpace(currentDefault) &&
                leaderboards.Exists(lb => lb != null && lb.slug == currentDefault))
            {
                return currentDefault;
            }

            var global = leaderboards.Find(lb => lb != null && lb.slug == "global");
            if (global != null)
            {
                return global.slug;
            }

            var first = leaderboards.Find(lb => lb != null && !string.IsNullOrWhiteSpace(lb.slug));
            return first?.slug ?? "global";
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

        private TurnKitConfig.PlayerStoreDefConfig ResolvePlayerStoreDef(string storeKey)
        {
            if (config?.playerStoreDefs == null || string.IsNullOrWhiteSpace(storeKey))
            {
                return null;
            }

            return config.playerStoreDefs.Find(def => def != null && def.storeKey == storeKey);
        }

        private void LoadPlayerStoreTxCatalogEntries()
        {
            TurnKitPlayerStoreTxCatalogService.Load(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                loaded =>
                {
                    state.TxCatalogEntries = loaded ?? new List<TurnKitPlayerStoreTxCatalogEntry>();
                    if (!string.IsNullOrWhiteSpace(state.SelectedTxCatalogTransactionId))
                    {
                        var selected = state.TxCatalogEntries.Find(entry => entry != null && entry.transactionId == state.SelectedTxCatalogTransactionId);
                        if (selected != null)
                        {
                            state.TxCatalogDraft = TurnKitPlayerStoreTxCatalogDrafts.Clone(selected);
                        }
                    }
                    Repaint();
                },
                TurnKitPlayerStoreTxCatalogService.ShowLoadError);
        }

        private TurnKitPlayerStoreTxCatalogEntry ResolveTxCatalogEntry(string transactionId)
        {
            if (state?.TxCatalogEntries == null || string.IsNullOrWhiteSpace(transactionId))
            {
                return null;
            }

            return state.TxCatalogEntries.Find(entry => entry != null && entry.transactionId == transactionId);
        }

        private void NewPlayerStoreTxCatalogDraft()
        {
            TurnKitEditorWindowStateController.ResetTxCatalogDraft(state);
            Repaint();
        }

        private void EditPlayerStoreTxCatalogEntry(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            TurnKitPlayerStoreTxCatalogService.LoadOne(
                config,
                transactionId,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                loaded =>
                {
                    state.SelectedTxCatalogTransactionId = loaded?.transactionId ?? string.Empty;
                    state.TxCatalogDraft = TurnKitPlayerStoreTxCatalogDrafts.Clone(loaded);
                    Repaint();
                },
                TurnKitPlayerStoreTxCatalogService.ShowLoadError);
        }

        private void LoadPlayerStorePurchaseMappings()
        {
            TurnKitPlayerStorePurchaseMappingService.Load(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                loaded =>
                {
                    state.PurchaseMappings = loaded ?? new List<TurnKitPlayerStorePurchaseMappingEntry>();
                    if (!string.IsNullOrWhiteSpace(state.SelectedPurchaseMappingKey))
                    {
                        var selected = state.PurchaseMappings.Find(entry => entry != null && entry.EditorKey == state.SelectedPurchaseMappingKey);
                        if (selected != null)
                        {
                            state.PurchaseMappingDraft = TurnKitPlayerStorePurchaseMappingDrafts.Clone(selected);
                        }
                    }
                    Repaint();
                },
                TurnKitPlayerStorePurchaseMappingService.ShowLoadError);
        }

        private void LoadGooglePlayAppConfig()
        {
            TurnKitGooglePlayAppConfigService.Load(
                config,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                loaded =>
                {
                    state.GooglePlayAppConfigDraft = TurnKitGooglePlayAppConfigDrafts.Clone(loaded);
                    PopulateAutoGooglePlayFields(state.GooglePlayAppConfigDraft);
                    state.GooglePlayAppConfigLoaded = true;
                    Repaint();
                },
                () =>
                {
                    TurnKitEditorWindowStateController.ResetGooglePlayAppConfigDraft(state);
                    PopulateAutoGooglePlayFields(state.GooglePlayAppConfigDraft);
                    state.GooglePlayAppConfigLoaded = true;
                    Repaint();
                },
                TurnKitGooglePlayAppConfigService.ShowLoadError);
        }

        private void NewPlayerStorePurchaseMappingDraft()
        {
            TurnKitEditorWindowStateController.ResetPurchaseMappingDraft(state);
            Repaint();
        }

        private void EditPlayerStorePurchaseMapping(string editorKey)
        {
            if (string.IsNullOrWhiteSpace(editorKey))
            {
                return;
            }

            var selected = state.PurchaseMappings.Find(entry => entry != null && entry.EditorKey == editorKey);
            if (selected == null)
            {
                return;
            }

            state.SelectedPurchaseMappingKey = selected.EditorKey;
            state.PurchaseMappingDraft = TurnKitPlayerStorePurchaseMappingDrafts.Clone(selected);
            Repaint();
        }

        private void SavePlayerStorePurchaseMapping()
        {
            var entry = state.PurchaseMappingDraft;
            if (!TurnKitConfigValidator.TryValidatePlayerStorePurchaseMappingEntry(config, entry, state.TxCatalogEntries, out var error))
            {
                EditorUtility.DisplayDialog("Purchase Mapping Invalid", error, "OK");
                return;
            }

            TurnKitPlayerStorePurchaseMappingService.Save(
                config,
                entry,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                saved =>
                {
                    state.SelectedPurchaseMappingKey = saved.EditorKey;
                    state.PurchaseMappingDraft = TurnKitPlayerStorePurchaseMappingDrafts.Clone(saved);
                    LoadPlayerStorePurchaseMappings();
                },
                TurnKitPlayerStorePurchaseMappingService.ShowSaveError);
        }

        private void SaveGooglePlayAppConfig()
        {
            var draft = state.GooglePlayAppConfigDraft;
            PopulateAutoGooglePlayFields(draft);
            if (!TurnKitConfigValidator.TryValidateGooglePlayAppConfig(draft, out var error))
            {
                EditorUtility.DisplayDialog("Google Play App Config Invalid", error, "OK");
                return;
            }

            TurnKitGooglePlayAppConfigService.Save(
                config,
                draft,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                saved =>
                {
                    state.GooglePlayAppConfigDraft = TurnKitGooglePlayAppConfigDrafts.Clone(saved);
                    state.GooglePlayAppConfigLoaded = true;
                    LoadPlayerStorePurchaseMappings();
                    Repaint();
                },
                TurnKitGooglePlayAppConfigService.ShowSaveError);
        }

        private void PopulateAutoGooglePlayFields(TurnKitGooglePlayAppConfigDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            draft.appId = ResolveGooglePlayAppId(draft.appId);
            draft.androidPackageName = ResolveGooglePlayAndroidPackageName(draft.androidPackageName);
        }

        private string ResolveGooglePlayAppId(string existingValue)
        {
            if (!string.IsNullOrWhiteSpace(existingValue))
            {
                return existingValue.Trim();
            }

            string source = config?.projectName;
            if (string.IsNullOrWhiteSpace(source))
            {
                source = Application.productName;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            bool previousWasSeparator = false;
            foreach (char ch in source.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                    previousWasSeparator = false;
                    continue;
                }

                if (previousWasSeparator)
                {
                    continue;
                }

                sb.Append('_');
                previousWasSeparator = true;
            }

            return sb.ToString().Trim('_');
        }

        private static string ResolveGooglePlayAndroidPackageName(string existingValue)
        {
            string packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            if (!string.IsNullOrWhiteSpace(packageName))
            {
                return packageName.Trim();
            }

            return existingValue?.Trim() ?? string.Empty;
        }

        private void SavePlayerStoreTxCatalogEntry()
        {
            var entry = state.TxCatalogDraft;
            if (!TurnKitConfigValidator.TryValidatePlayerStoreTxCatalogEntry(config, entry, out var error))
            {
                EditorUtility.DisplayDialog("Tx Catalog Invalid", error, "OK");
                return;
            }

            TurnKitPlayerStoreTxCatalogService.Save(
                config,
                entry,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                saved =>
                {
                    state.SelectedTxCatalogTransactionId = saved.transactionId;
                    state.TxCatalogDraft = TurnKitPlayerStoreTxCatalogDrafts.Clone(saved);
                    LoadPlayerStoreTxCatalogEntries();
                },
                TurnKitPlayerStoreTxCatalogService.ShowSaveError);
        }

        private void DeletePlayerStoreTxCatalogEntry(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Tx Catalog Entry", $"Delete '{transactionId}'?", "Delete", "Cancel"))
            {
                return;
            }

            TurnKitPlayerStoreTxCatalogService.Delete(
                config,
                transactionId,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                () =>
                {
                    if (state.SelectedTxCatalogTransactionId == transactionId)
                    {
                        TurnKitEditorWindowStateController.ResetTxCatalogDraft(state);
                    }
                    LoadPlayerStoreTxCatalogEntries();
                },
                TurnKitPlayerStoreTxCatalogService.ShowDeleteError);
        }

        private void CreatePlayerStoreDef()
        {
            if (!TryParseOptionalDurationSeconds(state.NewPlayerStoreCooldownDuration, out var cooldownSeconds, out var cooldownError))
            {
                EditorUtility.DisplayDialog("Player Store Def Invalid", cooldownError, "OK");
                return;
            }

            if (!TryParseOptionalDouble(state.NewPlayerStoreNumberMin, out var numberMin, out var minError))
            {
                EditorUtility.DisplayDialog("Player Store Def Invalid", minError, "OK");
                return;
            }

            if (!TryParseOptionalDouble(state.NewPlayerStoreNumberMax, out var numberMax, out var maxError))
            {
                EditorUtility.DisplayDialog("Player Store Def Invalid", maxError, "OK");
                return;
            }

            var def = new TurnKitConfig.PlayerStoreDefConfig
            {
                storeKey = state.NewPlayerStoreKey?.Trim(),
                valueType = state.NewPlayerStoreValueType,
                clientWritable = state.NewPlayerStoreClientWritable,
                clientReadable = state.NewPlayerStoreClientReadable,
                cooldownSeconds = cooldownSeconds,
                numberMin = state.NewPlayerStoreValueType == TurnKitConfig.PlayerStoreValueType.NUMBER ? numberMin : null,
                numberMax = state.NewPlayerStoreValueType == TurnKitConfig.PlayerStoreValueType.NUMBER ? numberMax : null
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
                    TurnKitEditorWindowStateController.ResetNewPlayerStoreDraft(state);
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

        private static bool TryParseOptionalDurationSeconds(string raw, out int value, out string error)
        {
            value = 0;
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            try
            {
                value = Mathf.Max(0, Mathf.RoundToInt((float)XmlConvert.ToTimeSpan(raw.Trim()).TotalSeconds));
                return true;
            }
            catch
            {
                error = "Cooldown Duration must be a valid ISO-8601 duration such as PT24H.";
                return false;
            }
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
                state.Webhooks,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                LoadWebhooks,
                Repaint);
        }

        private void DeleteWebhook(TurnKitConfig.WebhookConfig webhook)
        {
            TurnKitWebhookService.Delete(
                config,
                webhook,
                state.Webhooks,
                () => EditorPrefs.GetString("TurnKit_SessionToken"),
                LoadWebhooks,
                Repaint);
        }

    }
}
