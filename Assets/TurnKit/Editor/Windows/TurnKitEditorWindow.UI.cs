using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public partial class TurnKitEditorWindow : EditorWindow
    {
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
            DrawPlayerStoreDefs();
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
                    TurnKitConfigPersistence.SaveConfigOnly(config, InvalidateSyncStateCache);
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
            TurnKitRelayConfigSectionRenderer.Draw(config, state, CreateNewRelayConfig, DeleteRelayConfig, NormalizeRelayConfig);
        }

        private void DrawWebhooks()
        {
            TurnKitWebhookSectionRenderer.Draw(config, state, LoadWebhooks, SaveWebhook, DeleteWebhook, DrawWebhookHeaders);
        }

        private void DrawPlayerStoreDefs()
        {
            TurnKitPlayerStoreSectionRenderer.Draw(
                config,
                state,
                LoadPlayerStoreDefs,
                CreatePlayerStoreDef,
                DeletePlayerStoreDef,
                DrawPlayerStoreDef);
        }

        private void DrawPlayerStoreDef(TurnKitConfig.PlayerStoreDefConfig def)
        {
            TurnKitPlayerStoreSectionRenderer.DrawDefCard(def, DeletePlayerStoreDef);
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
            RefreshSyncStateIfNeeded();

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
            bool hasUnsyncedChanges = state.CachedHasUnsyncedChanges;
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
            bool hasCodegenSources = config.relayConfigs.Count > 0 || (config.playerStoreDefs?.Count ?? 0) > 0;
            EditorGUI.BeginDisabledGroup(!hasCodegenSources);
            bool hasEnumChanges = state.CachedHasEnumChanges;
            string buttonLabel = hasEnumChanges ? "Generate Enums (Out of Sync)" : "Generate Enums";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(30)))
            {
                TurnKitConfigPersistence.SaveAndRegenerate(config, InvalidateSyncStateCache);
                Repaint();
            }
            EditorGUI.EndDisabledGroup();

            if (!hasCodegenSources)
            {
                EditorGUILayout.HelpBox("Add relay configs or player store defs to generate enums", MessageType.Info);
            }
            else if (hasEnumChanges)
            {
                EditorGUILayout.HelpBox("Enum file is out of sync with your configs", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

    }
}

