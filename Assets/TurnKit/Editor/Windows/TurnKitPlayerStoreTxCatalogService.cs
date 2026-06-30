using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitPlayerStoreTxCatalogService
    {
        internal static void Load(
            TurnKitConfig config,
            Func<string> getSessionToken,
            Action<List<TurnKitPlayerStoreTxCatalogEntry>> onLoaded,
            Action<string> onError)
        {
            if (config == null || string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchPlayerStoreTxCatalogEntries(
                    config.gameKeyId,
                    getSessionToken(),
                    loaded => onLoaded?.Invoke(loaded ?? new List<TurnKitPlayerStoreTxCatalogEntry>()),
                    error => onError?.Invoke(error)));
        }

        internal static void LoadOne(
            TurnKitConfig config,
            string transactionId,
            Func<string> getSessionToken,
            Action<TurnKitPlayerStoreTxCatalogEntry> onLoaded,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchPlayerStoreTxCatalogEntry(
                    config.gameKeyId,
                    transactionId,
                    getSessionToken(),
                    loaded => onLoaded?.Invoke(loaded),
                    error => onError?.Invoke(error)));
        }

        internal static void Save(
            TurnKitConfig config,
            TurnKitPlayerStoreTxCatalogEntry entry,
            Func<string> getSessionToken,
            Action<TurnKitPlayerStoreTxCatalogEntry> onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.UpsertPlayerStoreTxCatalogEntry(
                    config.gameKeyId,
                    entry,
                    getSessionToken(),
                    saved => onSuccess?.Invoke(saved),
                    err => onError?.Invoke(err)));
        }

        internal static void Delete(
            TurnKitConfig config,
            string transactionId,
            Func<string> getSessionToken,
            Action onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.DeletePlayerStoreTxCatalogEntry(
                    config.gameKeyId,
                    transactionId,
                    getSessionToken(),
                    () => onSuccess?.Invoke(),
                    err => onError?.Invoke(err)));
        }

        internal static void ShowSaveError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to save tx catalog entry: {error}");
            EditorUtility.DisplayDialog("Save Failed", error, "OK");
        }

        internal static void ShowDeleteError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to delete tx catalog entry: {error}");
            EditorUtility.DisplayDialog("Delete Failed", error, "OK");
        }

        internal static void ShowLoadError(string error)
        {
            Debug.LogWarning($"[TurnKit] Failed to load tx catalog entries: {error}");
        }
    }
}
