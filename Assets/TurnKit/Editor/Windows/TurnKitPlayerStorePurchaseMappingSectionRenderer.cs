using System;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitPlayerStorePurchaseMappingSectionRenderer
    {
        internal static void Draw(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action loadMappings,
            Action createNewDraft,
            Action<string> editMapping,
            Action saveMapping,
            Func<string, TurnKitPlayerStoreTxCatalogEntry> resolveTxCatalogEntry)
        {
            state.PurchaseMappings ??= new System.Collections.Generic.List<TurnKitPlayerStorePurchaseMappingEntry>();
            state.PurchaseMappingDraft ??= TurnKitPlayerStorePurchaseMappingDrafts.CreateEmptyEntry();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Purchase Mappings ({state.PurchaseMappings.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                loadMappings?.Invoke();
            }

            if (GUILayout.Button("New", GUILayout.Width(60)))
            {
                createNewDraft?.Invoke();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Map store products to tx-catalog grants for external purchase verification.",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6);
            DrawDraftEditor(state.PurchaseMappingDraft, saveMapping, resolveTxCatalogEntry, config);
            GUILayout.Space(6);

            if (state.PurchaseMappings.Count == 0)
            {
                EditorGUILayout.HelpBox("No purchase mappings loaded.", MessageType.Info);
            }
            else
            {
                foreach (var entry in state.PurchaseMappings)
                {
                    DrawEntryCard(entry, editMapping);
                    GUILayout.Space(4);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawDraftEditor(
            TurnKitPlayerStorePurchaseMappingEntry draft,
            Action saveMapping,
            Func<string, TurnKitPlayerStoreTxCatalogEntry> resolveTxCatalogEntry,
            TurnKitConfig config)
        {
            if (draft == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(draft.id) ? "New Mapping" : "Edit Mapping",
                EditorStyles.miniBoldLabel);
            draft.provider = (TurnKitStorePurchaseProvider) EditorGUILayout.EnumPopup("Provider", draft.provider);
            draft.purchaseType = (TurnKitStorePurchaseType) EditorGUILayout.EnumPopup("Purchase Type", draft.purchaseType);
            draft.productId = EditorGUILayout.TextField("Product Id", draft.productId ?? string.Empty);
            draft.grantTransactionId = EditorGUILayout.TextField("Grant Tx Id", draft.grantTransactionId ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(draft.grantTransactionId) &&
                resolveTxCatalogEntry?.Invoke(draft.grantTransactionId) == null)
            {
                EditorGUILayout.HelpBox(
                    "Grant transaction id not found in loaded tx-catalog entries. Save will fail until tx-catalog is refreshed.",
                    MessageType.Warning);
            }

            draft.revokeTransactionId = EditorGUILayout.TextField("Revoke Tx Id", draft.revokeTransactionId ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(draft.revokeTransactionId) &&
                resolveTxCatalogEntry?.Invoke(draft.revokeTransactionId) == null)
            {
                EditorGUILayout.HelpBox(
                    "Revoke transaction id not found in loaded tx-catalog entries. Save will fail until tx-catalog is refreshed.",
                    MessageType.Warning);
            }

            draft.active = EditorGUILayout.Toggle("Active", draft.active);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                saveMapping?.Invoke();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private static void DrawEntryCard(TurnKitPlayerStorePurchaseMappingEntry entry, Action<string> editMapping)
        {
            if (entry == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.productId, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Edit", GUILayout.Width(60)))
            {
                editMapping?.Invoke(entry.EditorKey);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Provider", entry.provider.ToString());
            EditorGUILayout.TextField("Purchase Type", entry.purchaseType.ToString());
            EditorGUILayout.TextField("Grant Tx Id", entry.grantTransactionId ?? string.Empty);
            EditorGUILayout.TextField("Revoke Tx Id",
                string.IsNullOrWhiteSpace(entry.revokeTransactionId) ? "(none)" : entry.revokeTransactionId);
            EditorGUILayout.Toggle("Active", entry.active);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }
}
