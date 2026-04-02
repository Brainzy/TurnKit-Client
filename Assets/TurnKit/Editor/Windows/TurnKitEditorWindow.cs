using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public class TurnKitEditorWindow : EditorWindow
    {
        private TurnKitConfig config;
        private Vector2 scrollPosition;
        private readonly Dictionary<string, bool> configFoldouts = new();

        [MenuItem("TurnKit/Configuration", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<TurnKitEditorWindow>("TurnKit Configuration");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
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

            DrawRelayConfigs();

            GUILayout.Space(10);

            DrawButtons();

            GUILayout.Space(20);

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

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
                EditorGUILayout.HelpBox("⚠ No client key configured. Connect to TurnKit to generate one.", MessageType.Warning);

                if (GUILayout.Button("Connect to TurnKit", GUILayout.Height(30)))
                {
                    TurnKitAuthHandler.StartAuthFlow(config.projectName);
                }

                EditorGUILayout.HelpBox(
                    "Connecting will open your browser to authenticate with TurnKit. " +
                    "This will generate a client key for your project.",
                    MessageType.Info
                );
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

                EditorGUILayout.HelpBox("✓ Connected to TurnKit", MessageType.Info);

                if (GUILayout.Button("Reconnect", GUILayout.Height(25)))
                {
                    TurnKitAuthHandler.StartAuthFlow(config.projectName);
                }
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
                EditorGUILayout.HelpBox("No relay configs. Click '+ New' to create one or pull from the server.",
                    MessageType.Info);
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
            if (relay == null) return;

            string foldoutKey = relay.id ?? index.ToString();
            if (!configFoldouts.ContainsKey(foldoutKey))
            {
                configFoldouts[foldoutKey] = false;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Header
            EditorGUILayout.BeginHorizontal();

            configFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                configFoldouts[foldoutKey],
                $"{relay.slug} ({relay.maxPlayers}p, {relay.lists.Count} lists)",
                true
            );

            if (GUILayout.Button("Edit", GUILayout.Width(50)))
            {
                RelayConfigEditWindow.ShowWindow(config, relay);
            }

            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog(
                        "Delete Relay Config",
                        $"Are you sure you want to delete '{relay.slug}'?",
                        "Delete",
                        "Cancel"))
                {
                    DeleteRelayConfig(index);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Details (when expanded)
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
                EditorGUILayout.IntField("Wait for player reconnecting (seconds)", relay.waitReconnectSeconds);

                GUILayout.Space(5);

                // Lists
                EditorGUILayout.LabelField($"Lists ({relay.lists.Count})", EditorStyles.boldLabel);

                if (relay.lists.Count > 0)
                {
                    foreach (var list in relay.lists)
                    {
                        EditorGUILayout.BeginVertical(GUI.skin.box);

                        EditorGUILayout.LabelField(list.name, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(list.tag, EditorStyles.boldLabel);

                        if (list.ownerSlots != null && list.ownerSlots.Count > 0)
                        {
                            string owners = "Owner: " + string.Join(", ", list.ownerSlots);
                            EditorGUILayout.LabelField(owners, EditorStyles.miniLabel);
                        }

                        if (list.visibleToSlots != null && list.visibleToSlots.Count > 0)
                        {
                            string visible = "Visible: " + string.Join(", ", list.visibleToSlots);
                            EditorGUILayout.LabelField(visible, EditorStyles.miniLabel);
                        }

                        EditorGUILayout.EndVertical();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No lists", EditorStyles.miniLabel);
                }

                EditorGUI.EndDisabledGroup();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawButtons()
        {
            // Sync Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var syncHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
            EditorGUILayout.LabelField("Sync", syncHeaderStyle);

            GUILayout.Space(5);

            // Pull button
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey));

            if (GUILayout.Button("Pull from API", GUILayout.Height(30)))
            {
                PullFromServer();
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(3);

            // Push button with sync warning
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || config.relayConfigs.Count == 0);

            bool hasUnsyncedChanges = HasUnsyncedChanges();
            string pushButtonLabel = hasUnsyncedChanges ? "⚠ Push to API (Out of Sync)" : "Push to API";

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

            // Code Generation Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var codegenHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
            EditorGUILayout.LabelField("Code Generation", codegenHeaderStyle);

            GUILayout.Space(5);

            // Generate Enums button
            EditorGUI.BeginDisabledGroup(config.relayConfigs.Count == 0);

            bool hasEnumChanges = EnumGenerator.HasChanges(config);
            string buttonLabel = hasEnumChanges ? "⚠ Generate Enums (Out of Sync)" : "Generate Enums";

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
                lists = new List<TurnKitConfig.RelayListConfig>()
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

            // If it has an ID, it exists on server - delete via API
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
                        (error) =>
                        {
                            Debug.LogError($"[TurnKit] Failed to delete relay config: {error}");
                            EditorUtility.DisplayDialog("Delete Failed", $"Failed to delete: {error}", "OK");
                        }
                    )
                );
            }
            else
            {
                // Local only - just remove
                config.relayConfigs.RemoveAt(index);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TurnKit] Removed local relay config: {relay.slug}");
            }
        }

        private bool HasUnsyncedChanges()
        {
            // Check if enum file is out of sync
            if (EnumGenerator.HasChanges(config))
                return true;

            // Check if any config doesn't have an ID (not pushed yet)
            foreach (var relay in config.relayConfigs)
            {
                if (string.IsNullOrEmpty(relay.id) && relay.lists != null && relay.lists.Count > 0)
                    return true;
            }

            return false;
        }

        private bool ValidateConfigs()
        {
            var errors = new List<string>();

            // Check for duplicate slugs
            var slugs = new HashSet<string>();
            foreach (var relay in config.relayConfigs)
            {
                if (slugs.Contains(relay.slug))
                {
                    errors.Add($"Duplicate slug: {relay.slug}");
                }

                slugs.Add(relay.slug);

                // Validate voting
                if (relay.votingEnabled)
                {
                    // Each threshold must be <= maxPlayers (they're separate, not additive)
                    if (relay.votesRequired > relay.maxPlayers)
                    {
                        errors.Add(
                            $"{relay.slug}: Votes required ({relay.votesRequired}) exceeds max players ({relay.maxPlayers})");
                    }

                    if (relay.votesToFail > relay.maxPlayers)
                    {
                        errors.Add($"{relay.slug}: Votes to fail ({relay.votesToFail}) exceeds max players ({relay.maxPlayers})");
                    }

                    if (relay.votingMode == TurnKitConfig.VotingMode.ASYNC &&
                        relay.failAction == TurnKitConfig.FailAction.END_GAME)
                    {
                        errors.Add($"{relay.slug}: ASYNC voting cannot use END_GAME fail action");
                    }
                }

                // Validate lists
                var listNames = new HashSet<string>();
                foreach (var list in relay.lists)
                {
                    if (listNames.Contains(list.name))
                    {
                        errors.Add($"{relay.slug}: Duplicate list name: {list.name}");
                    }

                    listNames.Add(list.name);

                    // Auto-clean: Remove out-of-range slots instead of erroring
                    list.ownerSlots.RemoveAll(slot => (int) slot < 1 || (int) slot > relay.maxPlayers);
                    list.visibleToSlots.RemoveAll(slot => (int) slot < 1 || (int) slot > relay.maxPlayers);
                }
            }

            if (errors.Count > 0)
            {
                string message = "Validation failed:\n\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("Validation Errors", message, "OK");
                return false;
            }

            // Mark as dirty since we cleaned up slots
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
                    (configs) =>
                    {
                        config.relayConfigs = configs;
                        EditorUtility.SetDirty(config);
                        AssetDatabase.SaveAssets();

                        // Auto-generate enums
                        EnumGenerator.Generate(config);

                        Debug.Log($"[TurnKit] Pulled {configs.Count} relay config(s)");
                        EditorUtility.DisplayDialog("Success", $"Pulled {configs.Count} relay config(s) from server", "OK");
                        Repaint();
                    },
                    (error) =>
                    {
                        Debug.LogError($"[TurnKit] Failed to pull configs: {error}");
                        EditorUtility.DisplayDialog("Pull Failed", $"Failed to pull configs: {error}", "OK");
                    }
                )
            );
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
                        (updatedRelay) =>
                        {
                            // Update the ID if it was newly created
                            relay.id = updatedRelay.id;
                            EditorUtility.SetDirty(config);

                            successCount++;

                            if (successCount == totalCount)
                            {
                                AssetDatabase.SaveAssets();
                                EnumGenerator.Generate(config); // Auto-generate enums
                                EditorUtility.DisplayDialog("Push Complete", $"{successCount} config(s) pushed successfully", "OK");
                                Repaint();
                            }
                        },
                        (error) =>
                        {
                            Debug.LogError($"[TurnKit] Failed to push {relay.slug}: {error}");
                            EditorUtility.DisplayDialog("Push Failed", $"Failed to push {relay.slug}: {error}", "OK");
                        }
                    )
                );
            }
        }
    }
}
