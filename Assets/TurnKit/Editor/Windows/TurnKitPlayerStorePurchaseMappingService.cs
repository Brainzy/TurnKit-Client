using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitPlayerStorePurchaseMappingService
    {
        internal static void Load(
            TurnKitConfig config,
            Func<string> getSessionToken,
            Action<List<TurnKitPlayerStorePurchaseMappingEntry>> onLoaded,
            Action<string> onError)
        {
            if (config == null || string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchPlayerStorePurchaseMappings(
                    config.gameKeyId,
                    getSessionToken(),
                    loaded => onLoaded?.Invoke(loaded ?? new List<TurnKitPlayerStorePurchaseMappingEntry>()),
                    error => onError?.Invoke(error)));
        }

        internal static void Save(
            TurnKitConfig config,
            TurnKitPlayerStorePurchaseMappingEntry entry,
            Func<string> getSessionToken,
            Action<TurnKitPlayerStorePurchaseMappingEntry> onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.UpsertPlayerStorePurchaseMapping(
                    config.gameKeyId,
                    entry,
                    getSessionToken(),
                    saved => onSuccess?.Invoke(saved),
                    err => onError?.Invoke(err)));
        }

        internal static void ShowSaveError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to save purchase mapping: {error}");
            EditorUtility.DisplayDialog("Save Failed", error, "OK");
        }

        internal static void ShowLoadError(string error)
        {
            Debug.LogWarning($"[TurnKit] Failed to load purchase mappings: {error}");
        }
    }
}
