using System;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitGooglePlayAppConfigSectionRenderer
    {
        internal static void Draw(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action loadConfig,
            Action saveConfig)
        {
            state.GooglePlayAppConfigDraft ??= TurnKitGooglePlayAppConfigDrafts.CreateEmpty();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Google Play App Config", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                loadConfig?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Save per-game Google Play app credentials used by purchase verify. Product mappings stay in Purchase Mappings.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6);

            var draft = state.GooglePlayAppConfigDraft;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("App Id", draft.appId ?? string.Empty);
            EditorGUILayout.TextField("Android Package", draft.androidPackageName ?? string.Empty);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.LabelField("Service Account JSON", EditorStyles.miniBoldLabel);
            draft.googleServiceAccountJson = EditorGUILayout.TextArea(
                draft.googleServiceAccountJson ?? string.Empty,
                EditorStyles.textArea,
                GUILayout.MinHeight(140),
                GUILayout.MaxHeight(220));

            string jsonStatus = draft.googleServiceAccountJsonConfigured ? "Saved on backend" : "Not saved yet";
            EditorGUILayout.LabelField($"JSON Status: {jsonStatus}", EditorStyles.miniLabel);
            if (draft.googleServiceAccountJsonConfigured)
            {
                EditorGUILayout.HelpBox("Leave the JSON box empty to keep the currently saved backend secret. Paste a new JSON value only when replacing it.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Paste the full Google service account JSON, then save it once.", MessageType.Info);
            }

            EditorGUILayout.HelpBox("App Id and Android package are auto-sent from local TurnKit/Unity config.", MessageType.None);

            if (!string.IsNullOrWhiteSpace(draft.updatedAt))
            {
                EditorGUILayout.LabelField($"Updated: {draft.updatedAt}", EditorStyles.miniLabel);
            }

            GUILayout.Space(6);
            EditorGUILayout.HelpBox("Configure Google Play products in Purchase Mappings below. This section only stores app-level Google Play credentials.", MessageType.Info);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Save Google Play Config", GUILayout.Height(28)))
            {
                saveConfig?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

    }
}
