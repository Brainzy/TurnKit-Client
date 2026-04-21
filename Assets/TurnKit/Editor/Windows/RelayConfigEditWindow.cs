using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public class RelayConfigEditWindow : EditorWindow
    {
        private TurnKitConfig config;
        private TurnKitConfig.RelayConfig relay;
        private Vector2 scrollPosition;
        private readonly Dictionary<int, bool> listFoldouts = new();
        private readonly Dictionary<int, bool> statFoldouts = new();
        private List<TurnKitConfig.WebhookConfig> webhooks = new();

        public static void ShowWindow(TurnKitConfig config, TurnKitConfig.RelayConfig relay)
        {
            var window = GetWindow<RelayConfigEditWindow>($"Edit: {relay.slug}");
            window.config = config;
            window.relay = relay;
            window.minSize = new Vector2(620, 650);
            window.LoadWebhooks();
            window.Show();
        }

        private void OnGUI()
        {
            if (relay == null)
            {
                EditorGUILayout.HelpBox("Relay config is null", MessageType.Error);
                return;
            }

            NormalizeRelayConfig();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Space(5);

            DrawBasicSettings();
            GUILayout.Space(8);
            DrawVoting();
            GUILayout.Space(8);
            DrawLists();
            GUILayout.Space(8);
            DrawTrackedStats();
            GUILayout.Space(15);
            DrawActions();
            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        private void DrawBasicSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("Basic Settings", headerStyle);
            GUILayout.Space(5);

            relay.slug = EditorGUILayout.TextField("Slug", relay.slug);
            int oldMaxPlayers = relay.maxPlayers;
            relay.maxPlayers = EditorGUILayout.IntSlider("Max Players", relay.maxPlayers, 2, 8);
            if (oldMaxPlayers != relay.maxPlayers)
            {
                CleanupSlotsAfterMaxPlayersChange();
                NormalizeVotingSettings();
            }

            relay.turnEnforcement = (TurnKitConfig.TurnEnforcement)EditorGUILayout.EnumPopup("Turn Enforcement", relay.turnEnforcement);
            relay.ignoreAllOwnership = EditorGUILayout.Toggle("Ignore All Ownership", relay.ignoreAllOwnership);
            relay.matchTimeoutMinutes = EditorGUILayout.IntField("Match Timeout (minutes)", relay.matchTimeoutMinutes);
            relay.turnTimeoutSeconds = EditorGUILayout.IntField("Turn Timeout (seconds)", relay.turnTimeoutSeconds);
            relay.waitReconnectSeconds = EditorGUILayout.IntField("Wait Reconnect (seconds)", relay.waitReconnectSeconds);
            EditorGUILayout.EndVertical();
        }

        private void DrawVoting()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Voting", new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 });
            GUILayout.Space(5);
            relay.votingEnabled = EditorGUILayout.Toggle("Voting Enabled", relay.votingEnabled);

            if (relay.votingEnabled)
            {
                GUILayout.Space(3);
                EditorGUI.indentLevel++;
                relay.votingMode = (TurnKitConfig.VotingMode)EditorGUILayout.EnumPopup("Mode", relay.votingMode);
                int maxVotes = GetMaxVotes();
                NormalizeVotingSettings();
                relay.votesRequired = EditorGUILayout.IntSlider("Votes Required", relay.votesRequired, 1, maxVotes);
                relay.votesToFail = EditorGUILayout.IntSlider("Votes to Fail", relay.votesToFail, 1, maxVotes);
                relay.failAction = (TurnKitConfig.FailAction)EditorGUILayout.EnumPopup("Fail Action", relay.failAction);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void CleanupSlotsAfterMaxPlayersChange()
        {
            foreach (var list in relay.lists)
            {
                list.ownerSlots.RemoveAll(slot => (int)slot > relay.maxPlayers);
                list.visibleToSlots.RemoveAll(slot => (int)slot > relay.maxPlayers);
            }
        }

        private int GetMaxVotes()
        {
            return Mathf.Min(3, relay.maxPlayers);
        }

        private void NormalizeVotingSettings()
        {
            if (relay == null || !relay.votingEnabled)
            {
                return;
            }

            int maxVotes = GetMaxVotes();
            relay.votesRequired = Mathf.Clamp(relay.votesRequired, 1, maxVotes);
            relay.votesToFail = Mathf.Clamp(relay.votesToFail, 1, maxVotes);
        }

        private void DrawLists()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

            EditorGUILayout.LabelField("Lists", headerStyle);
            GUILayout.Space(5);
            if (relay.lists.Count == 0)
            {
                EditorGUILayout.HelpBox("No lists. Click '+ Add' to create one.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < relay.lists.Count; i++)
                {
                    DrawList(relay.lists[i], i);
                    GUILayout.Space(5);
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Mirror", GUILayout.Width(60))) MirrorLists();
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                relay.lists.Add(new TurnKitConfig.RelayListConfig
                {
                    name = "SomeVariable",
                    tag = "tag",
                    ownerSlots = new List<TurnKitConfig.PlayerSlot> { TurnKitConfig.PlayerSlot.Player1 },
                    visibleToSlots = new List<TurnKitConfig.PlayerSlot> { TurnKitConfig.PlayerSlot.Player1, TurnKitConfig.PlayerSlot.Player2 }
                });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawList(TurnKitConfig.RelayListConfig list, int index)
        {
            if (!listFoldouts.ContainsKey(index))
            {
                listFoldouts[index] = true;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            string foldoutLabel = string.IsNullOrEmpty(list.name) ? $"List {index}" : list.name;
            listFoldouts[index] = EditorGUILayout.Foldout(listFoldouts[index], foldoutLabel, true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Delete List", $"Delete '{list.name}'?", "Delete", "Cancel"))
                {
                    relay.lists.RemoveAt(index);
                    listFoldouts.Remove(index);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (listFoldouts[index])
            {
                GUILayout.Space(5);
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 50;
                list.name = EditorGUILayout.TextField("Name", list.name);
                GUILayout.Space(10);
                list.tag = EditorGUILayout.TextField("Tag", list.tag);
                EditorGUIUtility.labelWidth = originalLabelWidth;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("C# identifier examples: SomeVariable, someVariable, example", EditorStyles.miniLabel);
                DrawIdentifierValidation("List name", list.name);
                if (!string.IsNullOrWhiteSpace(list.tag))
                {
                    DrawIdentifierValidation("Tag", list.tag);
                }
                GUILayout.Space(8);

                if (!relay.ignoreAllOwnership)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Owner Slots", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("All", GUILayout.Width(35)))
                    {
                        list.ownerSlots.Clear();
                        for (int i = 1; i <= relay.maxPlayers; i++) list.ownerSlots.Add((TurnKitConfig.PlayerSlot)i);
                    }
                    if (GUILayout.Button("Clear", GUILayout.Width(45))) list.ownerSlots.Clear();
                    EditorGUILayout.EndHorizontal();
                    DrawSlotToggles(list.ownerSlots);
                    GUILayout.Space(8);
                }
                else
                {
                    EditorGUILayout.HelpBox("Ownership is ignored for this relay config", MessageType.Info);
                    GUILayout.Space(8);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Visible Slots", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("All", GUILayout.Width(35)))
                {
                    list.visibleToSlots.Clear();
                    for (int i = 1; i <= relay.maxPlayers; i++) list.visibleToSlots.Add((TurnKitConfig.PlayerSlot)i);
                }
                if (GUILayout.Button("Clear", GUILayout.Width(45))) list.visibleToSlots.Clear();
                EditorGUILayout.EndHorizontal();
                DrawSlotToggles(list.visibleToSlots);
                EditorGUI.indentLevel--;
                GUILayout.Space(3);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTrackedStats()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

            EditorGUILayout.LabelField("Tracked Stats", headerStyle);
            GUILayout.Space(5);
            if (relay.trackedStats.Count == 0)
            {
                EditorGUILayout.HelpBox("No tracked stats. Click '+ Add' to create one.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < relay.trackedStats.Count; i++)
                {
                    DrawTrackedStat(relay.trackedStats[i], i);
                    GUILayout.Space(5);
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh Webhooks", GUILayout.Width(120)))
            {
                LoadWebhooks();
            }
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                relay.trackedStats.Add(new TurnKitConfig.TrackedStatConfig
                {
                    name = "SomeVariable",
                    dataType = TurnKitConfig.TrackedStatDataType.DOUBLE,
                    scope = TurnKitConfig.TrackedStatScope.MATCH,
                    syncTo = new List<TurnKitConfig.TrackedStatSyncTargetConfig>()
                });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTrackedStat(TurnKitConfig.TrackedStatConfig stat, int index)
        {
            if (!statFoldouts.ContainsKey(index))
            {
                statFoldouts[index] = true;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            string label = string.IsNullOrWhiteSpace(stat.name) ? $"Stat {index}" : stat.name;
            statFoldouts[index] = EditorGUILayout.Foldout(statFoldouts[index], label, true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Tracked Stat", $"Delete '{stat.name}'?", "Delete", "Cancel"))
                {
                    relay.trackedStats.RemoveAt(index);
                    statFoldouts.Remove(index);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (statFoldouts[index])
            {
                EditorGUI.indentLevel++;
                stat.name = EditorGUILayout.TextField("Name", stat.name);
                EditorGUILayout.LabelField("C# identifier example: SomeVariable", EditorStyles.miniLabel);
                DrawTrackedStatValidation(stat.name);
                stat.dataType = (TurnKitConfig.TrackedStatDataType)EditorGUILayout.EnumPopup("Data Type", stat.dataType);
                stat.scope = (TurnKitConfig.TrackedStatScope)EditorGUILayout.EnumPopup("Scope", stat.scope);
                DrawInitialValue(stat);
                GUILayout.Space(6);
                DrawSyncTargets(stat);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawInitialValue(TurnKitConfig.TrackedStatConfig stat)
        {
            switch (stat.dataType)
            {
                case TurnKitConfig.TrackedStatDataType.DOUBLE:
                    stat.initialDouble = EditorGUILayout.DoubleField("Initial Value", stat.initialDouble);
                    break;
                case TurnKitConfig.TrackedStatDataType.STRING:
                    stat.initialString = EditorGUILayout.TextField("Initial Value", stat.initialString ?? string.Empty);
                    break;
                case TurnKitConfig.TrackedStatDataType.LIST_STRING:
                    EditorGUILayout.LabelField("Initial Values", EditorStyles.miniBoldLabel);
                    if (stat.initialList == null)
                    {
                        stat.initialList = new List<string>();
                    }
                    for (int i = 0; i < stat.initialList.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        stat.initialList[i] = EditorGUILayout.TextField($"Item {i + 1}", stat.initialList[i] ?? string.Empty);
                        if (GUILayout.Button("-", GUILayout.Width(24)))
                        {
                            stat.initialList.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("+ Add String", GUILayout.Width(100)))
                    {
                        stat.initialList.Add(string.Empty);
                    }
                    break;
            }
        }

        private void DrawSyncTargets(TurnKitConfig.TrackedStatConfig stat)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sync Targets", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Target", GUILayout.Width(90)))
            {
                stat.syncTo.Add(new TurnKitConfig.TrackedStatSyncTargetConfig());
            }
            EditorGUILayout.EndHorizontal();

            if (stat.syncTo == null || stat.syncTo.Count == 0)
            {
                EditorGUILayout.HelpBox("No sync targets configured.", MessageType.None);
                return;
            }

            for (int i = 0; i < stat.syncTo.Count; i++)
            {
                var target = stat.syncTo[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                var previousDestinationType = target.destinationType;
                target.destinationType = (TurnKitConfig.TrackedStatSyncDestinationType)EditorGUILayout.EnumPopup("Destination", target.destinationType);
                if (previousDestinationType != target.destinationType)
                {
                    target.destinationId = null;
                }
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    stat.syncTo.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                if (target.destinationType == TurnKitConfig.TrackedStatSyncDestinationType.WEBHOOK)
                {
                    DrawWebhookDestinationField(target);
                }
                else
                {
                    DrawLeaderboardDestinationField(target);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawLeaderboardDestinationField(TurnKitConfig.TrackedStatSyncTargetConfig target)
        {
            var leaderboards = config?.leaderboards?
                .Where(lb => !string.IsNullOrWhiteSpace(lb?.slug))
                .OrderBy(lb => lb.slug)
                .ToList() ?? new List<TurnKitConfig.LeaderboardConfig>();

            if (leaderboards.Count == 0)
            {
                target.destinationId = EditorGUILayout.TextField("Leaderboard Slug", target.destinationId ?? string.Empty);
                EditorGUILayout.HelpBox("No leaderboards loaded in TurnKitConfig. Sync project metadata or type an existing slug manually.", MessageType.Info);
                return;
            }

            string[] options = leaderboards
                .Select(lb => string.IsNullOrWhiteSpace(lb.displayName) ? lb.slug : $"{lb.displayName} ({lb.slug})")
                .ToArray();
            var ids = leaderboards.Select(lb => lb.slug).ToList();

            if (string.IsNullOrWhiteSpace(target.destinationId) && ids.Count > 0)
            {
                target.destinationId = ids[0];
            }

            int selectedIndex = Mathf.Max(0, ids.IndexOf(target.destinationId ?? string.Empty));
            int newIndex = EditorGUILayout.Popup("Leaderboard", selectedIndex, options);
            target.destinationId = ids[newIndex];
        }

        private void DrawWebhookDestinationField(TurnKitConfig.TrackedStatSyncTargetConfig target)
        {
            var ids = webhooks.Select(w => w.id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().OrderBy(id => id).ToList();
            if (ids.Count == 0)
            {
                target.destinationId = null;
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Webhook Id", string.Empty);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox("No webhooks available. Create or refresh webhooks before adding a webhook sync target.", MessageType.Warning);
                return;
            }

            if (!ids.Contains(target.destinationId))
            {
                target.destinationId = ids[0];
            }

            int selectedIndex = Mathf.Max(0, ids.IndexOf(target.destinationId ?? string.Empty));
            int newIndex = EditorGUILayout.Popup("Webhook Id", selectedIndex, ids.ToArray());
            target.destinationId = ids[newIndex];
        }

        private void DrawSlotToggles(List<TurnKitConfig.PlayerSlot> slots)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(15);
            for (int i = 1; i <= relay.maxPlayers; i++)
            {
                var slot = (TurnKitConfig.PlayerSlot)i;
                bool isSelected = slots.Contains(slot);
                bool newSelected = GUILayout.Toggle(isSelected, $"P{i}", GUILayout.Width(45));
                if (newSelected && !isSelected) slots.Add(slot);
                else if (!newSelected && isSelected) slots.Remove(slot);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions()
        {
            var canSave = TurnKitConfigValidator.TryValidateRelay(config, relay, out var errors);
            if (!canSave)
            {
                EditorGUILayout.HelpBox(TurnKitConfigValidator.BuildErrorSummary(errors), MessageType.Error);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(32))) Close();
            GUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(!canSave);
            if (GUILayout.Button("Save Changes", GUILayout.Width(120), GUILayout.Height(32)))
            {
                if (!TurnKitConfigValidator.TryValidateRelay(config, relay, out var latestErrors))
                {
                    EditorUtility.DisplayDialog("Validation Errors", "Fix validation errors before saving:\n\n" + string.Join("\n", latestErrors), "OK");
                    return;
                }

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TurnKit] Saved changes to {relay.slug}");
                Close();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSlugValidation(string label, string value)
        {
            if (TurnKitConfigValidator.TryGetSlugNameError(value, out string error))
            {
                return;
            }

            EditorGUILayout.HelpBox($"{label}: {error}", MessageType.Warning);
        }

        private static void DrawIdentifierValidation(string label, string value)
        {
            if (TurnKitConfigValidator.TryGetCodeIdentifierError(value, out string error))
            {
                return;
            }

            EditorGUILayout.HelpBox($"{label}: {error}", MessageType.Warning);
        }

        private static void DrawTrackedStatValidation(string value)
        {
            if (TurnKitConfigValidator.TryGetCodeIdentifierError(value, out string error))
            {
                return;
            }

            EditorGUILayout.HelpBox($"Tracked stat name: {error}", MessageType.Warning);
        }

        private void MirrorLists()
        {
            var newLists = new List<TurnKitConfig.RelayListConfig>();
            foreach (var list in relay.lists)
            {
                var match = Regex.Match(list.name, @"(\d+)");
                if (!match.Success) continue;

                int playerNum = int.Parse(match.Value, CultureInfo.InvariantCulture);
                if (playerNum != 1) continue;

                for (int targetPlayer = 2; targetPlayer <= relay.maxPlayers; targetPlayer++)
                {
                    string newName = Regex.Replace(list.name, @"\d+", targetPlayer.ToString(CultureInfo.InvariantCulture));
                    if (relay.lists.Any(l => l.name == newName)) continue;

                    var newOwnerSlots = MirrorSlotList(list.ownerSlots, targetPlayer);
                    var newVisibleSlots = MirrorSlotList(list.visibleToSlots, targetPlayer);

                    newLists.Add(new TurnKitConfig.RelayListConfig
                    {
                        name = newName,
                        tag = list.tag,
                        ownerSlots = newOwnerSlots,
                        visibleToSlots = newVisibleSlots
                    });
                }
            }

            if (newLists.Count > 0)
            {
                relay.lists.AddRange(newLists);
                EditorUtility.SetDirty(config);
                Debug.Log($"[TurnKit] Mirrored {newLists.Count} list(s)");
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog("Mirror Lists", "No P1 lists found to mirror.", "OK");
            }
        }

        private List<TurnKitConfig.PlayerSlot> MirrorSlotList(List<TurnKitConfig.PlayerSlot> original, int targetPlayer)
        {
            var newList = new List<TurnKitConfig.PlayerSlot>();
            if (original.Count == 1 && original.Contains(TurnKitConfig.PlayerSlot.Player1))
            {
                newList.Add((TurnKitConfig.PlayerSlot)targetPlayer);
            }
            else if (original.Count >= relay.maxPlayers || original.Count == 0)
            {
                newList.AddRange(original);
            }
            else
            {
                newList.AddRange(original);
            }

            return newList;
        }

        private void LoadWebhooks()
        {
            if (config == null || string.IsNullOrWhiteSpace(config.gameKeyId))
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
                    error => Debug.LogWarning($"[TurnKit] Failed to load webhooks: {error}")
                )
            );
        }

        internal void ReloadWebhooks()
        {
            LoadWebhooks();
        }

        internal static void ReloadAllOpenWebhookLists()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<RelayConfigEditWindow>())
            {
                window.ReloadWebhooks();
                window.Repaint();
            }
        }

        private void NormalizeRelayConfig()
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
