using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitPlayerStoreDefService
    {
        internal static void Load(
            TurnKitConfig config,
            Func<string> getSessionToken,
            Action<List<TurnKitConfig.PlayerStoreDefConfig>> onLoaded,
            Action<string> onError)
        {
            if (config == null || string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchPlayerStoreDefs(
                    config.gameKeyId,
                    getSessionToken(),
                    loaded => onLoaded?.Invoke(loaded ?? new List<TurnKitConfig.PlayerStoreDefConfig>()),
                    error => onError?.Invoke(error)));
        }

        internal static void Create(
            TurnKitConfig config,
            TurnKitConfig.PlayerStoreDefConfig def,
            Func<string> getSessionToken,
            Action onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.CreatePlayerStoreDef(
                    config.gameKeyId,
                    def,
                    getSessionToken(),
                    _ => onSuccess?.Invoke(),
                    err => onError?.Invoke(err)));
        }

        internal static void Delete(
            TurnKitConfig config,
            string storeKey,
            Func<string> getSessionToken,
            Action onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.DeletePlayerStoreDef(
                    config.gameKeyId,
                    storeKey,
                    getSessionToken(),
                    () => onSuccess?.Invoke(),
                    err => onError?.Invoke(err)));
        }

        internal static void ShowCreateError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to create player store def: {error}");
            EditorUtility.DisplayDialog("Create Failed", error, "OK");
        }

        internal static void ShowDeleteError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to delete player store def: {error}");
            EditorUtility.DisplayDialog("Delete Failed", error, "OK");
        }

        internal static void ShowLoadError(string error)
        {
            Debug.LogWarning($"[TurnKit] Failed to load player store defs: {error}");
        }
    }
}
