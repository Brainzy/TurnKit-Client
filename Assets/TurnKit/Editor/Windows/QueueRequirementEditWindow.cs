using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal sealed class QueueRequirementEditWindow : EditorWindow
    {
        private TurnKitConfig config;
        private TurnKitConfig.RelayConfig relay;
        private TurnKitConfig.QueueRequirementConfig requirement;
        private Vector2 scrollPosition;

        internal static void ShowWindow(TurnKitConfig config, TurnKitConfig.RelayConfig relay, TurnKitConfig.QueueRequirementConfig requirement)
        {
            var window = GetWindow<QueueRequirementEditWindow>($"Queue Requirement: {requirement?.name ?? "Unnamed"}");
            window.minSize = new Vector2(520, 420);
            window.config = config;
            window.relay = relay;
            window.requirement = requirement;
            window.Show();
        }

        private void OnGUI()
        {
            if (relay == null || requirement == null)
            {
                EditorGUILayout.HelpBox("Missing queue requirement context.", MessageType.Error);
                return;
            }

            requirement.conditions ??= new List<TurnKitConfig.RelayConditionConfig>();
            requirement.groups ??= new List<TurnKitConfig.QueueRequirementGroupConfig>();
            if (requirement.groups.Count == 0 && requirement.conditions.Count > 0)
            {
                requirement.groups.Add(new TurnKitConfig.QueueRequirementGroupConfig
                {
                    combinator = requirement.combinator,
                    conditions = new List<TurnKitConfig.RelayConditionConfig>(requirement.conditions)
                });
                requirement.conditions.Clear();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            requirement.name = EditorGUILayout.TextField("Name (optional)", requirement.name ?? string.Empty);

            GUILayout.Space(8);
            DrawGroups();
            EditorGUILayout.EndScrollView();
        }

        private void DrawGroups()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Groups are OR branches. The requirement passes if any group passes.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Groups ({requirement.groups.Count}/20)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(requirement.groups.Count >= 20);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                requirement.groups.Add(new TurnKitConfig.QueueRequirementGroupConfig
                {
                    combinator = TurnKitConfig.RuleCombinator.AND,
                    conditions = new List<TurnKitConfig.RelayConditionConfig>
                    {
                        new TurnKitConfig.RelayConditionConfig()
                    }
                });
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (requirement.groups.Count == 0)
            {
                EditorGUILayout.HelpBox("Add at least one group. Groups are OR-ed together.", MessageType.Info);
            }

            for (int i = 0; i < requirement.groups.Count; i++)
            {
                var group = requirement.groups[i];
                if (group == null)
                {
                    group = new TurnKitConfig.QueueRequirementGroupConfig();
                    requirement.groups[i] = group;
                }

                group.conditions ??= new List<TurnKitConfig.RelayConditionConfig>();

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Group {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    requirement.groups.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                group.combinator = (TurnKitConfig.RuleCombinator)EditorGUILayout.EnumPopup("Combinator", group.combinator);
                DrawConditionList(group.conditions, $"Group {i + 1}");
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConditionList(List<TurnKitConfig.RelayConditionConfig> conditions, string ownerLabel)
        {
            conditions ??= new List<TurnKitConfig.RelayConditionConfig>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Conditions ({conditions.Count}/20)", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(conditions.Count >= 20);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                conditions.Add(new TurnKitConfig.RelayConditionConfig());
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (conditions.Count == 0)
            {
                EditorGUILayout.HelpBox($"{ownerLabel} has no conditions.", MessageType.Warning);
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null)
                {
                    condition = new TurnKitConfig.RelayConditionConfig();
                    conditions[i] = condition;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Condition {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    conditions.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                condition.source = (TurnKitConfig.ConditionSource)EditorGUILayout.EnumPopup("Source", condition.source);
                condition.key = TurnKitPlayerStoreKeyPopup.Draw("Key", condition.key, config?.playerStoreDefs);
                condition.@operator = (TurnKitConfig.ConditionOperator)EditorGUILayout.EnumPopup("Operator", condition.@operator);
                condition.value = EditorGUILayout.TextField("Value", condition.value ?? string.Empty);
                EditorGUILayout.EndVertical();
            }
        }
    }
}
