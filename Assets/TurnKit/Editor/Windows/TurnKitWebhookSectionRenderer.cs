using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitWebhookSectionRenderer
    {
        internal static void Draw(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action loadWebhooks,
            Action<TurnKitConfig.WebhookConfig> saveWebhook,
            Action<TurnKitConfig.WebhookConfig> deleteWebhook,
            Action<TurnKitConfig.WebhookConfig> drawWebhookHeaders)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Webhooks ({state.Webhooks.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                loadWebhooks?.Invoke();
            }
            if (GUILayout.Button("+ New", GUILayout.Width(60)))
            {
                state.Webhooks.Add(new TurnKitConfig.WebhookConfig { headers = new List<TurnKitConfig.WebhookHeader>() });
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (state.Webhooks.Count == 0)
            {
                EditorGUILayout.HelpBox("No webhooks loaded.", MessageType.Info);
            }
            else
            {
                foreach (var webhook in state.Webhooks.ToList())
                {
                    DrawWebhook(state, webhook, saveWebhook, deleteWebhook, drawWebhookHeaders);
                    GUILayout.Space(5);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawWebhook(
            TurnKitEditorWindowState state,
            TurnKitConfig.WebhookConfig webhook,
            Action<TurnKitConfig.WebhookConfig> saveWebhook,
            Action<TurnKitConfig.WebhookConfig> deleteWebhook,
            Action<TurnKitConfig.WebhookConfig> drawWebhookHeaders)
        {
            string key = webhook.entityId ?? webhook.id ?? "new-webhook";
            TurnKitEditorWindowStateController.EnsureWebhookFoldout(state, key, string.IsNullOrEmpty(webhook.entityId));

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            state.WebhookFoldouts[key] = EditorGUILayout.Foldout(state.WebhookFoldouts[key], string.IsNullOrWhiteSpace(webhook.id) ? "New webhook" : webhook.id, true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", GUILayout.Width(55)))
            {
                saveWebhook?.Invoke(webhook);
            }
            if (GUILayout.Button("Delete", GUILayout.Width(55)))
            {
                deleteWebhook?.Invoke(webhook);
            }
            EditorGUILayout.EndHorizontal();

            if (state.WebhookFoldouts[key])
            {
                webhook.id = EditorGUILayout.TextField("Id", webhook.id ?? string.Empty);
                webhook.url = EditorGUILayout.TextField("URL", webhook.url ?? string.Empty);
                drawWebhookHeaders?.Invoke(webhook);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
