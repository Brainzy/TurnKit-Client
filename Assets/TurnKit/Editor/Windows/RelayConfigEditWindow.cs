using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace TurnKit.Editor
{
    public class RelayConfigEditWindow : EditorWindow
    {
        private TurnKitConfig config;
        private TurnKitConfig.RelayConfig relay;
        private Vector2 scrollPosition;
        private readonly Dictionary<int, bool> listFoldouts = new();

        public static void ShowWindow(TurnKitConfig config, TurnKitConfig.RelayConfig relay)
        {
            var window = GetWindow<RelayConfigEditWindow>($"Edit: {relay.slug}");
            window.config = config;
            window.relay = relay;
            window.minSize = new Vector2(550, 600);
            window.Show();
        }

        private void OnGUI()
        {
            if (relay == null)
            {
                EditorGUILayout.HelpBox("Relay config is null", MessageType.Error);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(5);

            // Basic Settings Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var headerStyle = new GUIStyle(EditorStyles.boldLabel) {fontSize = 12};
            EditorGUILayout.LabelField("Basic Settings", headerStyle);

            GUILayout.Space(5);

            relay.slug = EditorGUILayout.TextField("Slug", relay.slug);

            int oldMaxPlayers = relay.maxPlayers;
            relay.maxPlayers = EditorGUILayout.IntSlider("Max Players", relay.maxPlayers, 2, 8);

            if (oldMaxPlayers != relay.maxPlayers)
            {
                CleanupSlotsAfterMaxPlayersChange();
            }

            relay.turnEnforcement =
                (TurnKitConfig.TurnEnforcement) EditorGUILayout.EnumPopup("Turn Enforcement", relay.turnEnforcement);
            relay.ignoreAllOwnership = EditorGUILayout.Toggle("Ignore All Ownership", relay.ignoreAllOwnership);
            relay.matchTimeoutMinutes = EditorGUILayout.IntField("Match Timeout (minutes)", relay.matchTimeoutMinutes);
            relay.turnTimeoutSeconds = EditorGUILayout.IntField("Turn Timeout (seconds)", relay.turnTimeoutSeconds);
            relay.waitReconnectSeconds = EditorGUILayout.IntField("Wait Reconnect (seconds)", relay.waitReconnectSeconds);

            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // Voting Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Voting", headerStyle);
            GUILayout.Space(5);
            relay.votingEnabled = EditorGUILayout.Toggle("Voting Enabled", relay.votingEnabled);

            if (relay.votingEnabled)
            {
                GUILayout.Space(3);
                EditorGUI.indentLevel++;
                relay.votingMode = (TurnKitConfig.VotingMode) EditorGUILayout.EnumPopup("Mode", relay.votingMode);
                int maxVotes = Mathf.Min(3, relay.maxPlayers);
                relay.votesRequired = EditorGUILayout.IntSlider("Votes Required", relay.votesRequired, 1, maxVotes);
                relay.votesToFail = EditorGUILayout.IntSlider("Votes to Fail", relay.votesToFail, 1, maxVotes);
                relay.failAction = (TurnKitConfig.FailAction) EditorGUILayout.EnumPopup("Fail Action", relay.failAction);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(8);
            DrawLists();
            GUILayout.Space(15);

            // Save/Cancel Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(32))) Close();
            GUILayout.Space(5);
            if (GUILayout.Button("Save Changes", GUILayout.Width(120), GUILayout.Height(32)))
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TurnKit] Saved changes to {relay.slug}");
                Close();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        private void CleanupSlotsAfterMaxPlayersChange()
        {
            foreach (var list in relay.lists)
            {
                list.ownerSlots.RemoveAll(slot => (int) slot > relay.maxPlayers);
                list.visibleToSlots.RemoveAll(slot => (int) slot > relay.maxPlayers);
            }
        }

        private void DrawLists()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) {fontSize = 12};

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Lists", headerStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Mirror", GUILayout.Width(60))) MirrorLists();
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                relay.lists.Add(new TurnKitConfig.RelayListConfig
                {
                    name = "p1_list",
                    tag = "tag",
                    ownerSlots = new List<TurnKitConfig.PlayerSlot> {TurnKitConfig.PlayerSlot.Player1},
                    visibleToSlots = new List<TurnKitConfig.PlayerSlot>
                        {TurnKitConfig.PlayerSlot.Player1, TurnKitConfig.PlayerSlot.Player2}
                });
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            if (relay.lists.Count == 0) EditorGUILayout.HelpBox("No lists. Click '+ Add' to create one.", MessageType.Info);
            else
            {
                for (int i = 0; i < relay.lists.Count; i++)
                {
                    DrawList(relay.lists[i], i);
                    GUILayout.Space(5);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawList(TurnKitConfig.RelayListConfig list, int index)
        {
            if (!listFoldouts.ContainsKey(index)) listFoldouts[index] = true;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Header Row
            EditorGUILayout.BeginHorizontal();
            string foldoutLabel = string.IsNullOrEmpty(list.name) ? $"List {index}" : list.name;
            listFoldouts[index] = EditorGUILayout.Foldout(listFoldouts[index], foldoutLabel, true);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("×", GUILayout.Width(25)))
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

                // --- Name and Tag on one line ---
                EditorGUILayout.BeginHorizontal();

                // Adjust label width so they don't take up half the screen
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 50;

                list.name = EditorGUILayout.TextField("Name", list.name);
                GUILayout.Space(10);
                list.tag = EditorGUILayout.TextField("Tag", list.tag);

                EditorGUIUtility.labelWidth = originalLabelWidth; // Reset to original
                EditorGUILayout.EndHorizontal();
                // --------------------------------

                GUILayout.Space(8);

                if (!relay.ignoreAllOwnership)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Owner Slots", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("All", GUILayout.Width(35)))
                    {
                        list.ownerSlots.Clear();
                        for (int i = 1; i <= relay.maxPlayers; i++) list.ownerSlots.Add((TurnKitConfig.PlayerSlot) i);
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
                    for (int i = 1; i <= relay.maxPlayers; i++) list.visibleToSlots.Add((TurnKitConfig.PlayerSlot) i);
                }

                if (GUILayout.Button("Clear", GUILayout.Width(45))) list.visibleToSlots.Clear();
                EditorGUILayout.EndHorizontal();

                DrawSlotToggles(list.visibleToSlots);

                EditorGUI.indentLevel--;
                GUILayout.Space(3);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSlotToggles(List<TurnKitConfig.PlayerSlot> slots)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(15);
            for (int i = 1; i <= relay.maxPlayers; i++)
            {
                var slot = (TurnKitConfig.PlayerSlot) i;
                bool isSelected = slots.Contains(slot);
                bool newSelected = GUILayout.Toggle(isSelected, $"P{i}", GUILayout.Width(45));

                if (newSelected && !isSelected) slots.Add(slot);
                else if (!newSelected && isSelected) slots.Remove(slot);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void MirrorLists()
        {
            var newLists = new List<TurnKitConfig.RelayListConfig>();
            foreach (var list in relay.lists)
            {
                var match = Regex.Match(list.name, @"(\d+)");
                if (!match.Success) continue;

                int playerNum = int.Parse(match.Value);
                if (playerNum != 1) continue;

                for (int targetPlayer = 2; targetPlayer <= relay.maxPlayers; targetPlayer++)
                {
                    string newName = Regex.Replace(list.name, @"\d+", targetPlayer.ToString());
                    if (relay.lists.Any(l => l.name == newName)) continue;

                    var newOwnerSlots = MirrorSlotList(list.ownerSlots, targetPlayer);
                    var newVisibleSlots = MirrorSlotList(list.visibleToSlots, targetPlayer);

                    newLists.Add(new TurnKitConfig.RelayListConfig
                    {
                        name = newName,
                        tag = list.tag, // --- Added: Copy the tag to mirrored lists ---
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
                newList.Add((TurnKitConfig.PlayerSlot) targetPlayer);
            }
            else if (original.Count >= relay.maxPlayers || original.Count == 0)
            {
                newList.AddRange(original);
            }
            else
            {
                // Fallback: full copy
                newList.AddRange(original);
            }

            return newList;
        }
    }
}