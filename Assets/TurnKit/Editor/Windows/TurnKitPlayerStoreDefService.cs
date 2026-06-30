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

    internal static class TurnKitLeaderboardService
    {
        internal static void Load(
            TurnKitConfig config,
            Func<string> getSessionToken,
            Action<List<TurnKitConfig.LeaderboardConfig>> onLoaded,
            Action<string> onError)
        {
            if (config == null || string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId))
            {
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchLeaderboards(
                    config.gameKeyId,
                    getSessionToken(),
                    loaded => onLoaded?.Invoke(loaded ?? new List<TurnKitConfig.LeaderboardConfig>()),
                    error => onError?.Invoke(error)));
        }

        internal static void Create(
            TurnKitConfig config,
            TurnKitLeaderboardDraft draft,
            Func<string> getSessionToken,
            Action<TurnKitConfig.LeaderboardConfig> onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.CreateLeaderboard(
                    config.gameKeyId,
                    draft,
                    getSessionToken(),
                    saved => onSuccess?.Invoke(saved),
                    err => onError?.Invoke(err)));
        }

        internal static void UpdateDisplayName(
            TurnKitConfig config,
            string slug,
            string displayName,
            Func<string> getSessionToken,
            Action<TurnKitConfig.LeaderboardConfig> onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.UpdateLeaderboardDisplayName(
                    config.gameKeyId,
                    slug,
                    displayName,
                    getSessionToken(),
                    saved => onSuccess?.Invoke(saved),
                    err => onError?.Invoke(err)));
        }

        internal static void Delete(
            TurnKitConfig config,
            string slug,
            Func<string> getSessionToken,
            Action onSuccess,
            Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.DeleteLeaderboard(
                    config.gameKeyId,
                    slug,
                    getSessionToken(),
                    () => onSuccess?.Invoke(),
                    err => onError?.Invoke(err)));
        }

        internal static void ShowCreateError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to create leaderboard: {error}");
            EditorUtility.DisplayDialog("Create Failed", error, "OK");
        }

        internal static void ShowUpdateError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to update leaderboard: {error}");
            EditorUtility.DisplayDialog("Update Failed", error, "OK");
        }

        internal static void ShowDeleteError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to delete leaderboard: {error}");
            EditorUtility.DisplayDialog("Delete Failed", error, "OK");
        }

        internal static void ShowLoadError(string error)
        {
            Debug.LogWarning($"[TurnKit] Failed to load leaderboards: {error}");
        }
    }
}
