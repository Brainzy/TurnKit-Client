using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal sealed class PlayerStoreMutationEditWindow : EditorWindow
    {
        private TurnKitConfig.RelayConfig relay;
        private TurnKitConfig.PlayerStoreMutationConfig mutation;
        private Vector2 scrollPosition;

        internal static void ShowWindow(TurnKitConfig.RelayConfig relay, TurnKitConfig.PlayerStoreMutationConfig mutation)
        {
            var window = GetWindow<PlayerStoreMutationEditWindow>($"Store Mutation: {mutation?.mutationId ?? "Unnamed"}");
            window.minSize = new Vector2(560, 500);
            window.relay = relay;
            window.mutation = mutation;
            window.Show();
        }

        private void OnGUI()
        {
            if (relay == null || mutation == null)
            {
                EditorGUILayout.HelpBox("Missing mutation context.", MessageType.Error);
                return;
            }

            mutation.conditions ??= new List<TurnKitConfig.RelayConditionConfig>();
            mutation.stringListValue ??= new List<string>();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            mutation.mutationId = EditorGUILayout.TextField("Mutation Id (optional)", mutation.mutationId ?? string.Empty);
            mutation.phase = (TurnKitConfig.RulePhase)EditorGUILayout.EnumPopup("Phase", mutation.phase);
            mutation.target = (TurnKitConfig.MutationTarget)EditorGUILayout.EnumPopup("Target", mutation.target);
            mutation.storeKey = EditorGUILayout.TextField("Store Key", mutation.storeKey ?? string.Empty);
            mutation.operation = (TurnKitConfig.MutationOperation)EditorGUILayout.EnumPopup("Operation", mutation.operation);
            mutation.combinator = (TurnKitConfig.RuleCombinator)EditorGUILayout.EnumPopup("Combinator", mutation.combinator);

            GUILayout.Space(6);
            DrawValueEditor();
            GUILayout.Space(6);
            DrawConditions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawValueEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Value", EditorStyles.boldLabel);

            switch (mutation.operation)
            {
                case TurnKitConfig.MutationOperation.ADD:
                case TurnKitConfig.MutationOperation.SUB:
                    mutation.valueType = TurnKitConfig.PlayerStoreValueType.NUMBER;
                    mutation.stringValue = string.Empty;
                    mutation.stringListValue.Clear();
                    mutation.numberValue = EditorGUILayout.DoubleField("Number", mutation.numberValue);
                    break;
                case TurnKitConfig.MutationOperation.LIST_SET:
                case TurnKitConfig.MutationOperation.LIST_ADD:
                case TurnKitConfig.MutationOperation.LIST_REMOVE:
                    mutation.valueType = TurnKitConfig.PlayerStoreValueType.STRING_LIST;
                    mutation.stringValue = string.Empty;
                    DrawStringListEditor();
                    break;
                case TurnKitConfig.MutationOperation.LIST_CLEAR:
                    mutation.valueType = TurnKitConfig.PlayerStoreValueType.STRING_LIST;
                    mutation.stringValue = string.Empty;
                    EditorGUILayout.HelpBox("Value forced to null for LIST_CLEAR.", MessageType.Info);
                    break;
                case TurnKitConfig.MutationOperation.SET:
                    mutation.valueType = (TurnKitConfig.PlayerStoreValueType)EditorGUILayout.EnumPopup("Value Type", mutation.valueType);
                    switch (mutation.valueType)
                    {
                        case TurnKitConfig.PlayerStoreValueType.NUMBER:
                            mutation.stringValue = string.Empty;
                            mutation.stringListValue.Clear();
                            mutation.numberValue = EditorGUILayout.DoubleField("Number", mutation.numberValue);
                            break;
                        case TurnKitConfig.PlayerStoreValueType.STRING_LIST:
                            mutation.stringValue = string.Empty;
                            DrawStringListEditor();
                            break;
                        default:
                            mutation.stringListValue.Clear();
                            mutation.stringValue = EditorGUILayout.TextField("String", mutation.stringValue ?? string.Empty);
                            break;
                    }
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStringListEditor()
        {
            for (int i = 0; i < mutation.stringListValue.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                mutation.stringListValue[i] = EditorGUILayout.TextField($"Item {i + 1}", mutation.stringListValue[i] ?? string.Empty);
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    mutation.stringListValue.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add String", GUILayout.Width(100)))
            {
                mutation.stringListValue.Add(string.Empty);
            }
        }

        private void DrawConditions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Conditions ({mutation.conditions.Count}/20)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(mutation.conditions.Count >= 20);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                mutation.conditions.Add(new TurnKitConfig.RelayConditionConfig());
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < mutation.conditions.Count; i++)
            {
                var condition = mutation.conditions[i];
                if (condition == null)
                {
                    condition = new TurnKitConfig.RelayConditionConfig();
                    mutation.conditions[i] = condition;
                }

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Condition {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    mutation.conditions.RemoveAt(i);
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
