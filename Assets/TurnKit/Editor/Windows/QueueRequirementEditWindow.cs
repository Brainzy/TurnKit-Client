using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal sealed class QueueRequirementEditWindow : EditorWindow
    {
        private TurnKitConfig.RelayConfig relay;
        private TurnKitConfig.QueueRequirementConfig requirement;
        private Vector2 scrollPosition;

        internal static void ShowWindow(TurnKitConfig.RelayConfig relay, TurnKitConfig.QueueRequirementConfig requirement)
        {
            var window = GetWindow<QueueRequirementEditWindow>($"Queue Requirement: {requirement?.name ?? "Unnamed"}");
            window.minSize = new Vector2(520, 420);
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

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            requirement.name = EditorGUILayout.TextField("Name (optional)", requirement.name ?? string.Empty);
            requirement.combinator = (TurnKitConfig.RuleCombinator)EditorGUILayout.EnumPopup("Combinator", requirement.combinator);

            GUILayout.Space(8);
            DrawConditions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawConditions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Conditions ({requirement.conditions.Count}/20)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(requirement.conditions.Count >= 20);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                requirement.conditions.Add(new TurnKitConfig.RelayConditionConfig());
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (requirement.conditions.Count == 0)
            {
                EditorGUILayout.HelpBox("At least one condition required.", MessageType.Warning);
            }

            for (int i = 0; i < requirement.conditions.Count; i++)
            {
                var condition = requirement.conditions[i];
                if (condition == null)
                {
                    condition = new TurnKitConfig.RelayConditionConfig();
                    requirement.conditions[i] = condition;
                }

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Condition {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    requirement.conditions.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                condition.source = (TurnKitConfig.ConditionSource)EditorGUILayout.EnumPopup("Source", condition.source);
                condition.key = EditorGUILayout.TextField("Key", condition.key ?? string.Empty);
                condition.@operator = (TurnKitConfig.ConditionOperator)EditorGUILayout.EnumPopup("Operator", condition.@operator);
                condition.value = EditorGUILayout.TextField("Value", condition.value ?? string.Empty);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
