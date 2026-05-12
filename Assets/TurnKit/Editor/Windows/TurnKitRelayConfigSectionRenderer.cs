using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitRelayConfigSectionRenderer
    {
        internal static void Draw(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action createNewRelayConfig,
            Action<int> deleteRelayConfig,
            Action<TurnKitConfig.RelayConfig> normalizeRelayConfig)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Relay Configs ({config.relayConfigs.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ New", GUILayout.Width(60)))
            {
                createNewRelayConfig?.Invoke();
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
                    DrawRelayConfig(config, state, config.relayConfigs[i], i, deleteRelayConfig, normalizeRelayConfig);
                    GUILayout.Space(5);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawRelayConfig(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            TurnKitConfig.RelayConfig relay,
            int index,
            Action<int> deleteRelayConfig,
            Action<TurnKitConfig.RelayConfig> normalizeRelayConfig)
        {
            if (relay == null)
            {
                return;
            }

            normalizeRelayConfig?.Invoke(relay);

            string foldoutKey = relay.id ?? index.ToString();
            TurnKitEditorWindowStateController.EnsureConfigFoldout(state, foldoutKey);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            state.ConfigFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                state.ConfigFoldouts[foldoutKey],
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
                    deleteRelayConfig?.Invoke(index);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (state.ConfigFoldouts[foldoutKey])
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
                EditorGUILayout.IntField("AFK turn timer (seconds)", relay.afkTurnTimerSeconds);
                EditorGUILayout.IntField("Disconnected turn timer (seconds)", relay.disconnectedTurnTimerSeconds);
                EditorGUILayout.IntField("Wait reconnect (seconds)", relay.waitReconnectSeconds);
                EditorGUILayout.IntField("Reconnect move history size", relay.reconnectMoveHistorySize);
                EditorGUILayout.EnumPopup("On turn timeout", relay.onTurnTimeout);
                EditorGUILayout.Toggle("Reveal private lists on timeout", relay.revealPrivateListsOnTimeout);
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
                EditorGUILayout.LabelField($"Queue Requirements ({relay.queueRequirements.Count})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Player Store Mutations ({relay.playerStoreMutations.Count})", EditorStyles.boldLabel);
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
    }
}
