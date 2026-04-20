using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public class TurnKitEditorWindow : EditorWindow
    {
        private TurnKitConfig config;
        private Vector2 scrollPosition;
        private readonly Dictionary<string, bool> configFoldouts = new();
        private readonly Dictionary<string, bool> webhookFoldouts = new();
        private List<TurnKitConfig.WebhookConfig> webhooks = new();

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

        private void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("No TurnKitConfig found. Please create one.", MessageType.Error);
                if (GUILayout.Button("Create Config"))
                {
                    config = TurnKitAuthHandler.LoadOrCreateConfig();
                }

                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawHeader();
            GUILayout.Space(10);
            DrawConnectionStatus();
            GUILayout.Space(10);
            DrawLeaderboards();
            GUILayout.Space(10);
            DrawRelayConfigs();
            GUILayout.Space(10);
            DrawWebhooks();
            GUILayout.Space(10);
            DrawButtons();
            GUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("TurnKit Configuration", titleStyle);
            GUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Project", config.projectName);
            EditorGUILayout.TextField("Game Key", config.gameKeyId);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private void DrawConnectionStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(config.clientKey))
            {
                EditorGUILayout.HelpBox("No client key configured. Connect to TurnKit to generate one. " +
                                        "Note that quick setup can generate just first time the key, create manually on turnkit.dev/games if its not your first time running quick setup", MessageType.Warning);
                if (GUILayout.Button("Connect to TurnKit", GUILayout.Height(30)))
                {
                    TurnKitAuthHandler.StartAuthFlow(config.projectName);
                }

                EditorGUILayout.HelpBox(
                    "Connecting will open your browser to authenticate with TurnKit. This will generate a client key for your project.",
                    MessageType.Info);
            }
            else
            {
                string updatedClientKey = EditorGUILayout.TextField("Client Key", config.clientKey);
                if (updatedClientKey != config.clientKey)
                {
                    config.clientKey = updatedClientKey;
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                }

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Player Auth Policy", config.playerAuthPolicy.ToString());
                EditorGUILayout.TextField(
                    "Player Auth Methods",
                    config.playerAuthMethods == null || config.playerAuthMethods.Count == 0
                        ? "(none)"
                        : string.Join(", ", config.playerAuthMethods));
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.HelpBox("Connected to TurnKit", MessageType.Info);
                if (GUILayout.Button("Reconnect", GUILayout.Height(25)))
                {
                    TurnKitAuthHandler.StartAuthFlow(config.projectName);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLeaderboards()
        {
            config.leaderboards ??= new List<TurnKitConfig.LeaderboardConfig>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Leaderboards ({config.leaderboards.Count})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Synced from bootstrap responses. Manage definitions on the TurnKit backend.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Default Slug", config.defaultLeaderboard ?? string.Empty);

            if (config.leaderboards.Count == 0)
            {
                EditorGUILayout.HelpBox("No synced leaderboards yet. Reconnect to TurnKit to refresh bootstrap metadata.", MessageType.Info);
            }
            else
            {
                foreach (var leaderboard in config.leaderboards)
                {
                    DrawLeaderboardSummary(leaderboard);
                    GUILayout.Space(4);
                }
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private static void DrawLeaderboardSummary(TurnKitConfig.LeaderboardConfig leaderboard)
        {
            if (leaderboard == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(leaderboard.displayName) ? leaderboard.slug : leaderboard.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Slug: {leaderboard.slug}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Sort: {leaderboard.sortOrder} | Strategy: {leaderboard.scoreStrategy}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Range: {leaderboard.minScore} to {leaderboard.maxScore}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Reset: {leaderboard.resetFrequency} | Archive: {leaderboard.archiveOnReset}", EditorStyles.miniLabel);
            if (!string.IsNullOrWhiteSpace(leaderboard.nextResetAt))
            {
                EditorGUILayout.LabelField($"Next Reset: {leaderboard.nextResetAt}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawRelayConfigs()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Relay Configs ({config.relayConfigs.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ New", GUILayout.Width(60)))
            {
                CreateNewRelayConfig();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (config.relayConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox("No relay configs. Click '+ New' to create one or pull from the server.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < config.relayConfigs.Count; i++)
                {
                    DrawRelayConfig(config.relayConfigs[i], i);
                    GUILayout.Space(5);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRelayConfig(TurnKitConfig.RelayConfig relay, int index)
        {
            if (relay == null)
            {
                return;
            }

            NormalizeRelayConfig(relay);

            string foldoutKey = relay.id ?? index.ToString();
            if (!configFoldouts.ContainsKey(foldoutKey))
            {
                configFoldouts[foldoutKey] = false;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            configFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                configFoldouts[foldoutKey],
                $"{relay.slug} ({relay.maxPlayers}p, {relay.lists.Count} lists, {relay.trackedStats.Count} stats)",
                true);

            if (GUILayout.Button("Edit", GUILayout.Width(50)))
            {
                RelayConfigEditWindow.ShowWindow(config, relay);
            }

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Relay Config", $"Are you sure you want to delete '{relay.slug}'?", "Delete", "Cancel"))
                {
                    DeleteRelayConfig(index);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (configFoldouts[foldoutKey])
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Slug", relay.slug);
                EditorGUILayout.IntField("Max Players", relay.maxPlayers);
                EditorGUILayout.EnumPopup("Turn Enforcement", relay.turnEnforcement);
                if (relay.votingEnabled)
                {
                    EditorGUILayout.LabelField("Voting", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.EnumPopup("Mode", relay.votingMode);
                    EditorGUILayout.IntField("Votes Required", relay.votesRequired);
                    EditorGUILayout.IntField("Votes to Fail", relay.votesToFail);
                    EditorGUILayout.EnumPopup("Fail Action", relay.failAction);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.IntField("Match timeout (minutes)", relay.matchTimeoutMinutes);
                EditorGUILayout.IntField("Turn timeout (seconds)", relay.turnTimeoutSeconds);
                EditorGUILayout.IntField("Wait reconnect (seconds)", relay.waitReconnectSeconds);
                GUILayout.Space(5);

                EditorGUILayout.LabelField($"Lists ({relay.lists.Count})", EditorStyles.boldLabel);
                if (relay.lists.Count == 0)
                {
                    EditorGUILayout.LabelField("No lists", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (var list in relay.lists)
                    {
                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        EditorGUILayout.LabelField(list.name, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(list.tag, EditorStyles.miniLabel);
                        if (list.ownerSlots.Count > 0)
                        {
                            EditorGUILayout.LabelField("Owner: " + string.Join(", ", list.ownerSlots), EditorStyles.miniLabel);
                        }
                        if (list.visibleToSlots.Count > 0)
                        {
                            EditorGUILayout.LabelField("Visible: " + string.Join(", ", list.visibleToSlots), EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                GUILayout.Space(4);
                EditorGUILayout.LabelField($"Tracked Stats ({relay.trackedStats.Count})", EditorStyles.boldLabel);
                if (relay.trackedStats.Count == 0)
                {
                    EditorGUILayout.LabelField("No tracked stats", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (var stat in relay.trackedStats)
                    {
                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        EditorGUILayout.LabelField(stat.name, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"{stat.dataType} / {stat.scope}", EditorStyles.miniLabel);
                        if (stat.syncTo != null && stat.syncTo.Count > 0)
                        {
                            EditorGUILayout.LabelField("Sync: " + string.Join(", ", stat.syncTo.Select(s => $"{s.destinationType}:{s.destinationId}")), EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWebhooks()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Webhooks ({webhooks.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                LoadWebhooks();
            }
            if (GUILayout.Button("+ New", GUILayout.Width(60)))
            {
                webhooks.Add(new TurnKitConfig.WebhookConfig { headers = new List<TurnKitConfig.WebhookHeader>() });
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (webhooks.Count == 0)
            {
                EditorGUILayout.HelpBox("No webhooks loaded.", MessageType.Info);
            }
            else
            {
                foreach (var webhook in webhooks.ToList())
                {
                    DrawWebhook(webhook);
                    GUILayout.Space(5);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawWebhook(TurnKitConfig.WebhookConfig webhook)
        {
            string key = webhook.entityId ?? webhook.id ?? "new-webhook";
            if (!webhookFoldouts.ContainsKey(key))
            {
                webhookFoldouts[key] = string.IsNullOrEmpty(webhook.entityId);
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            webhookFoldouts[key] = EditorGUILayout.Foldout(webhookFoldouts[key], string.IsNullOrWhiteSpace(webhook.id) ? "New webhook" : webhook.id, true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", GUILayout.Width(55)))
            {
                SaveWebhook(webhook);
            }
            if (GUILayout.Button("Delete", GUILayout.Width(55)))
            {
                DeleteWebhook(webhook);
            }
            EditorGUILayout.EndHorizontal();

            if (webhookFoldouts[key])
            {
                webhook.id = EditorGUILayout.TextField("Id", webhook.id ?? string.Empty);
                webhook.url = EditorGUILayout.TextField("URL", webhook.url ?? string.Empty);
                DrawWebhookHeaders(webhook);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWebhookHeaders(TurnKitConfig.WebhookConfig webhook)
        {
            if (webhook.headers == null)
            {
                webhook.headers = new List<TurnKitConfig.WebhookHeader>();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Headers", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Header", GUILayout.Width(75)))
            {
                webhook.headers.Add(new TurnKitConfig.WebhookHeader());
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < webhook.headers.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                webhook.headers[i].key = EditorGUILayout.TextField(webhook.headers[i].key ?? string.Empty);
                webhook.headers[i].value = EditorGUILayout.TextField(webhook.headers[i].value ?? string.Empty);
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    webhook.headers.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var syncHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("Sync", syncHeaderStyle);
            GUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey));
            if (GUILayout.Button("Pull from API", GUILayout.Height(30)))
            {
                PullFromServer();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(3);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || config.relayConfigs.Count == 0);
            bool hasUnsyncedChanges = HasUnsyncedChanges();
            string pushButtonLabel = hasUnsyncedChanges ? "Push to API (Out of Sync)" : "Push to API";
            if (GUILayout.Button(pushButtonLabel, GUILayout.Height(30)))
            {
                if (ValidateConfigs())
                {
                    PushToServer();
                }
            }
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(config.clientKey))
            {
                EditorGUILayout.HelpBox("Connect to TurnKit to sync with server", MessageType.Info);
            }
            else if (hasUnsyncedChanges)
            {
                EditorGUILayout.HelpBox("You have unpushed changes or out-of-sync enums", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var codegenHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("Code Generation", codegenHeaderStyle);
            GUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(config.relayConfigs.Count == 0);
            bool hasEnumChanges = EnumGenerator.HasChanges(config);
            string buttonLabel = hasEnumChanges ? "Generate Enums (Out of Sync)" : "Generate Enums";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(30)))
            {
                EnumGenerator.Generate(config);
                Repaint();
            }
            EditorGUI.EndDisabledGroup();

            if (config.relayConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox("Add relay configs to generate enums", MessageType.Info);
            }
            else if (hasEnumChanges)
            {
                EditorGUILayout.HelpBox("Enum file is out of sync with your configs", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
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
                waitReconnectSeconds = 45,
                ignoreAllOwnership = false,
                lists = new List<TurnKitConfig.RelayListConfig>(),
                trackedStats = new List<TurnKitConfig.TrackedStatConfig>()
            };

            config.relayConfigs.Add(newConfig);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
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
                Debug.Log($"[TurnKit] Removed local relay config: {relay.slug}");
            }
        }

        private bool HasUnsyncedChanges()
        {
            if (EnumGenerator.HasChanges(config))
            {
                return true;
            }

            foreach (var relay in config.relayConfigs)
            {
                NormalizeRelayConfig(relay);
                if (string.IsNullOrEmpty(relay.id) && ((relay.lists?.Count ?? 0) > 0 || (relay.trackedStats?.Count ?? 0) > 0))
                {
                    return true;
                }
            }

            return false;
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
            return true;
        }

        private void PullFromServer()
        {
            Debug.Log("[TurnKit] Pulling relay configs from server...");
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchRelayConfigs(
                    config.gameKeyId,
                    EditorPrefs.GetString("TurnKit_SessionToken"),
                    configs =>
                    {
                        config.relayConfigs = configs;
                        EditorUtility.SetDirty(config);
                        AssetDatabase.SaveAssets();
                        EnumGenerator.Generate(config);
                        LoadWebhooks();
                        Debug.Log($"[TurnKit] Pulled {configs.Count} relay config(s)");
                        EditorUtility.DisplayDialog("Success", $"Pulled {configs.Count} relay config(s) from server", "OK");
                        Repaint();
                    },
                    error =>
                    {
                        Debug.LogError($"[TurnKit] Failed to pull configs: {error}");
                        EditorUtility.DisplayDialog("Pull Failed", $"Failed to pull configs: {error}", "OK");
                    }));
        }

        private void PushToServer()
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
                        EditorPrefs.GetString("TurnKit_SessionToken"),
                        updatedRelay =>
                        {
                            relay.id = updatedRelay.id;
                            EditorUtility.SetDirty(config);
                            successCount++;
                            if (successCount == totalCount)
                            {
                                AssetDatabase.SaveAssets();
                                EnumGenerator.Generate(config);
                                EditorUtility.DisplayDialog("Push Complete", $"{successCount} config(s) pushed successfully", "OK");
                                Repaint();
                            }
                        },
                        error =>
                        {
                            Debug.LogError($"[TurnKit] Failed to push {relay.slug}: {error}");
                            EditorUtility.DisplayDialog("Push Failed", $"Failed to push {relay.slug}: {error}", "OK");
                        }));
            }
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
        }
    }
}
