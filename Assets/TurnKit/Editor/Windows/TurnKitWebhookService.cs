using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitWebhookService
    {
        internal static void Load(
            TurnKitConfig config,
            Func<string> getSessionToken,
            Action<List<TurnKitConfig.WebhookConfig>> onLoaded,
            Action onRepaint)
        {
            if (config == null || string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchWebhooks(
                    config.gameKeyId,
                    getSessionToken(),
                    loaded =>
                    {
                        onLoaded?.Invoke(loaded ?? new List<TurnKitConfig.WebhookConfig>());
                        onRepaint?.Invoke();
                    },
                    error => Debug.LogWarning($"[TurnKit] Failed to load webhooks: {error}")));
        }

        internal static void Save(
            TurnKitConfig config,
            TurnKitConfig.WebhookConfig webhook,
            List<TurnKitConfig.WebhookConfig> webhooks,
            Func<string> getSessionToken,
            Action loadWebhooks,
            Action onRepaint)
        {
            if (string.IsNullOrWhiteSpace(webhook.id) || string.IsNullOrWhiteSpace(webhook.url))
            {
                EditorUtility.DisplayDialog("Webhook Invalid", "Webhook id and url are required.", "OK");
                return;
            }

            var coroutine = string.IsNullOrEmpty(webhook.entityId)
                ? TurnKitAPI.CreateWebhook(config.gameKeyId, webhook, getSessionToken(), saved => ReplaceWebhook(webhook, saved, webhooks, loadWebhooks, onRepaint), ShowError)
                : TurnKitAPI.UpdateWebhook(config.gameKeyId, webhook, getSessionToken(), saved => ReplaceWebhook(webhook, saved, webhooks, loadWebhooks, onRepaint), ShowError);
            EditorCoroutineRunner.StartCoroutine(coroutine);
        }

        internal static void Delete(
            TurnKitConfig config,
            TurnKitConfig.WebhookConfig webhook,
            List<TurnKitConfig.WebhookConfig> webhooks,
            Func<string> getSessionToken,
            Action loadWebhooks,
            Action onRepaint)
        {
            if (string.IsNullOrEmpty(webhook.entityId))
            {
                webhooks.Remove(webhook);
                onRepaint?.Invoke();
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Webhook", $"Delete webhook '{webhook.id}'?", "Delete", "Cancel"))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.DeleteWebhook(
                    config.gameKeyId,
                    webhook.id,
                    getSessionToken(),
                    () =>
                    {
                        webhooks.Remove(webhook);
                        loadWebhooks?.Invoke();
                        RelayConfigEditWindow.ReloadAllOpenWebhookLists();
                        onRepaint?.Invoke();
                    },
                    ShowError));
        }

        private static void ReplaceWebhook(
            TurnKitConfig.WebhookConfig original,
            TurnKitConfig.WebhookConfig saved,
            List<TurnKitConfig.WebhookConfig> webhooks,
            Action loadWebhooks,
            Action onRepaint)
        {
            int index = webhooks.IndexOf(original);
            if (index >= 0)
            {
                webhooks[index] = saved;
            }
            else
            {
                webhooks.Add(saved);
            }

            loadWebhooks?.Invoke();
            RelayConfigEditWindow.ReloadAllOpenWebhookLists();
            onRepaint?.Invoke();
        }

        private static void ShowError(string error)
        {
            Debug.LogError($"[TurnKit] Webhook operation failed: {error}");
            EditorUtility.DisplayDialog("Webhook Error", error, "OK");
        }
    }
}
