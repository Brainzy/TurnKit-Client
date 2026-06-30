using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitPlayerStoreTxCatalogSectionRenderer
    {
        internal static void Draw(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action refreshEntries,
            Action newDraft,
            Action<string> editEntry,
            Action saveDraft,
            Action<string> deleteEntry,
            Func<string, TurnKitConfig.PlayerStoreDefConfig> resolvePlayerStoreDef)
        {
            state.TxCatalogEntries ??= new List<TurnKitPlayerStoreTxCatalogEntry>();
            state.TxCatalogDraft ??= TurnKitPlayerStoreTxCatalogDrafts.CreateEmptyEntry();
            config.playerStoreDefs ??= new List<TurnKitConfig.PlayerStoreDefConfig>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Player Store Tx Catalog ({state.TxCatalogEntries.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                refreshEntries?.Invoke();
            }
            if (GUILayout.Button("New Draft", GUILayout.Width(90)))
            {
                newDraft?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Configure reusable client transactions for PlayerStore.Transaction(transactionId).", EditorStyles.wordWrappedMiniLabel);

            GUILayout.Space(6);
            if (state.TxCatalogEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No tx catalog entries loaded.", MessageType.Info);
            }
            else
            {
                foreach (var entry in state.TxCatalogEntries.ToList())
                {
                    DrawEntryCard(entry, state.SelectedTxCatalogTransactionId, editEntry, deleteEntry);
                    GUILayout.Space(4);
                }
            }

            GUILayout.Space(8);
            DrawDraftEditor(config, state, saveDraft, resolvePlayerStoreDef);
            EditorGUILayout.EndVertical();
        }

        private static void DrawEntryCard(
            TurnKitPlayerStoreTxCatalogEntry entry,
            string selectedTransactionId,
            Action<string> editEntry,
            Action<string> deleteEntry)
        {
            if (entry == null)
            {
                return;
            }

            bool isSelected = string.Equals(selectedTransactionId, entry.transactionId, StringComparison.Ordinal);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.transactionId, isSelected ? EditorStyles.whiteBoldLabel : EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Edit", GUILayout.Width(50)))
            {
                editEntry?.Invoke(entry.transactionId);
            }
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                deleteEntry?.Invoke(entry.transactionId);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"Enabled: {entry.enabled} | Version: {Mathf.Max(1, entry.catalogVersion)} | Mutations: {Mathf.Max(0, entry.mutationCount)}",
                EditorStyles.miniLabel);

            if (!string.IsNullOrWhiteSpace(entry.updatedAt))
            {
                EditorGUILayout.LabelField($"Updated: {entry.updatedAt}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawDraftEditor(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action saveDraft,
            Func<string, TurnKitConfig.PlayerStoreDefConfig> resolvePlayerStoreDef)
        {
            var draft = state.TxCatalogDraft;
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(state.SelectedTxCatalogTransactionId) ? "Tx Catalog Draft" : $"Editing: {state.SelectedTxCatalogTransactionId}",
                EditorStyles.boldLabel);

            draft.transactionId = EditorGUILayout.TextField("Transaction Id", draft.transactionId ?? string.Empty);
            draft.enabled = EditorGUILayout.Toggle("Enabled", draft.enabled);
            draft.catalogVersion = Mathf.Max(1, EditorGUILayout.IntField("Catalog Version", Mathf.Max(1, draft.catalogVersion)));

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Created At", string.IsNullOrWhiteSpace(draft.createdAt) ? "(new)" : draft.createdAt);
            EditorGUILayout.TextField("Updated At", string.IsNullOrWhiteSpace(draft.updatedAt) ? "(new)" : draft.updatedAt);
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(4);
            DrawConditions(config, draft.conditions, resolvePlayerStoreDef);
            GUILayout.Space(4);
            DrawMutations(config, draft.mutations, resolvePlayerStoreDef);

            GUILayout.Space(6);
            EditorGUILayout.HelpBox("Server limits: max 20 conditions, 1-20 mutations, payload must use typed JSON values.", MessageType.None);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Save Tx Catalog Entry", GUILayout.Height(28)))
            {
                saveDraft?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private static void DrawConditions(
            TurnKitConfig config,
            List<TurnKitPlayerStoreTxCatalogConditionDraft> conditions,
            Func<string, TurnKitConfig.PlayerStoreDefConfig> resolvePlayerStoreDef)
        {
            conditions ??= new List<TurnKitPlayerStoreTxCatalogConditionDraft>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Conditions ({conditions.Count}/20)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(conditions.Count >= 20);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                conditions.Add(new TurnKitPlayerStoreTxCatalogConditionDraft());
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i] ?? new TurnKitPlayerStoreTxCatalogConditionDraft();
                conditions[i] = condition;

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Condition {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    conditions.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.EnumPopup("Source", TurnKitConfig.ConditionSource.STORE);
                EditorGUI.EndDisabledGroup();
                condition.source = TurnKitConfig.ConditionSource.STORE;
                condition.key = TurnKitPlayerStoreKeyPopup.Draw("Store Key", condition.key, config.playerStoreDefs);
                condition.@operator = (TurnKitConfig.ConditionOperator)EditorGUILayout.EnumPopup("Operator", condition.@operator);

                var def = resolvePlayerStoreDef?.Invoke(condition.key);
                TurnKitConfig.PlayerStoreValueType effectiveType = def?.valueType ?? condition.valueType;
                if (def == null && !string.IsNullOrWhiteSpace(condition.key))
                {
                    effectiveType = (TurnKitConfig.PlayerStoreValueType)EditorGUILayout.EnumPopup("Value Type", condition.valueType);
                    EditorGUILayout.HelpBox("Store key not found in loaded player-store defs. Save may fail until defs are refreshed.", MessageType.Warning);
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.EnumPopup("Value Type", effectiveType);
                    EditorGUI.EndDisabledGroup();
                }

                condition.valueType = effectiveType;
                DrawConditionValueEditor(condition, effectiveType);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawMutations(
            TurnKitConfig config,
            List<TurnKitPlayerStoreTxCatalogMutationDraft> mutations,
            Func<string, TurnKitConfig.PlayerStoreDefConfig> resolvePlayerStoreDef)
        {
            mutations ??= new List<TurnKitPlayerStoreTxCatalogMutationDraft>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Mutations ({mutations.Count}/20)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(mutations.Count >= 20);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                mutations.Add(new TurnKitPlayerStoreTxCatalogMutationDraft());
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < mutations.Count; i++)
            {
                var mutation = mutations[i] ?? new TurnKitPlayerStoreTxCatalogMutationDraft();
                mutations[i] = mutation;

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Mutation {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    mutations.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                mutation.storeKey = TurnKitPlayerStoreKeyPopup.Draw("Store Key", mutation.storeKey, config.playerStoreDefs);
                mutation.operation = (TurnKitConfig.MutationOperation)EditorGUILayout.EnumPopup("Operation", mutation.operation);

                var def = resolvePlayerStoreDef?.Invoke(mutation.storeKey);
                TurnKitConfig.PlayerStoreValueType effectiveType = ResolveMutationValueType(mutation, def);
                if (def == null && string.IsNullOrWhiteSpace(mutation.storeKey) == false)
                {
                    EditorGUILayout.HelpBox("Store key not found in loaded player-store defs. Save may fail until defs are refreshed.", MessageType.Warning);
                }

                mutation.valueType = effectiveType;
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.EnumPopup("Value Type", effectiveType);
                EditorGUI.EndDisabledGroup();
                DrawMutationValueEditor(mutation, effectiveType);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private static TurnKitConfig.PlayerStoreValueType ResolveMutationValueType(
            TurnKitPlayerStoreTxCatalogMutationDraft mutation,
            TurnKitConfig.PlayerStoreDefConfig def)
        {
            return mutation.operation switch
            {
                TurnKitConfig.MutationOperation.ADD => TurnKitConfig.PlayerStoreValueType.NUMBER,
                TurnKitConfig.MutationOperation.SUB => TurnKitConfig.PlayerStoreValueType.NUMBER,
                TurnKitConfig.MutationOperation.LIST_SET => TurnKitConfig.PlayerStoreValueType.STRING_LIST,
                TurnKitConfig.MutationOperation.LIST_ADD => TurnKitConfig.PlayerStoreValueType.STRING_LIST,
                TurnKitConfig.MutationOperation.LIST_REMOVE => TurnKitConfig.PlayerStoreValueType.STRING_LIST,
                TurnKitConfig.MutationOperation.LIST_CLEAR => TurnKitConfig.PlayerStoreValueType.STRING_LIST,
                _ => def?.valueType ?? mutation.valueType
            };
        }

        private static void DrawConditionValueEditor(
            TurnKitPlayerStoreTxCatalogConditionDraft condition,
            TurnKitConfig.PlayerStoreValueType valueType)
        {
            switch (valueType)
            {
                case TurnKitConfig.PlayerStoreValueType.NUMBER:
                    condition.numberValue = EditorGUILayout.DoubleField("Value", condition.numberValue);
                    break;
                case TurnKitConfig.PlayerStoreValueType.STRING_LIST:
                    DrawStringListEditor(condition.stringListValue, "Condition Value");
                    break;
                default:
                    condition.stringValue = EditorGUILayout.TextField("Value", condition.stringValue ?? string.Empty);
                    break;
            }
        }

        private static void DrawMutationValueEditor(
            TurnKitPlayerStoreTxCatalogMutationDraft mutation,
            TurnKitConfig.PlayerStoreValueType valueType)
        {
            switch (mutation.operation)
            {
                case TurnKitConfig.MutationOperation.ADD:
                case TurnKitConfig.MutationOperation.SUB:
                    mutation.numberValue = EditorGUILayout.DoubleField("Value", mutation.numberValue);
                    break;
                case TurnKitConfig.MutationOperation.LIST_SET:
                    DrawStringListEditor(mutation.stringListValue, "Values");
                    break;
                case TurnKitConfig.MutationOperation.LIST_ADD:
                case TurnKitConfig.MutationOperation.LIST_REMOVE:
                    mutation.stringValue = EditorGUILayout.TextField("Item", mutation.stringValue ?? string.Empty);
                    break;
                case TurnKitConfig.MutationOperation.LIST_CLEAR:
                    EditorGUILayout.HelpBox("Value is forced to null for LIST_CLEAR.", MessageType.Info);
                    break;
                case TurnKitConfig.MutationOperation.SET:
                default:
                    switch (valueType)
                    {
                        case TurnKitConfig.PlayerStoreValueType.NUMBER:
                            mutation.numberValue = EditorGUILayout.DoubleField("Value", mutation.numberValue);
                            break;
                        case TurnKitConfig.PlayerStoreValueType.STRING_LIST:
                            DrawStringListEditor(mutation.stringListValue, "Values");
                            break;
                        default:
                            mutation.stringValue = EditorGUILayout.TextField("Value", mutation.stringValue ?? string.Empty);
                            break;
                    }
                    break;
            }
        }

        private static void DrawStringListEditor(List<string> values, string label)
        {
            values ??= new List<string>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ String", GUILayout.Width(80)))
            {
                values.Add(string.Empty);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < values.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                values[i] = EditorGUILayout.TextField($"Item {i + 1}", values[i] ?? string.Empty);
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    values.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
