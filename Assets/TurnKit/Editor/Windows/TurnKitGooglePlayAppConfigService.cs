using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitGooglePlayAppConfigService
    {
        internal static void Load(
            TurnKitConfig config,
            System.Func<string> getSessionToken,
            System.Action<TurnKitGooglePlayAppConfigDraft> onLoaded,
            System.Action onNotFound,
            System.Action<string> onError)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.gameKeyId))
            {
                onLoaded?.Invoke(TurnKitGooglePlayAppConfigDrafts.CreateEmpty());
                return;
            }

            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.FetchGooglePlayAppConfig(
                    config.gameKeyId,
                    getSessionToken(),
                    onLoaded,
                    notFound =>
                    {
                        if (notFound)
                        {
                            onNotFound?.Invoke();
                            return;
                        }

                        onError?.Invoke("Load failed.");
                    },
                    onError));
        }

        internal static void Save(
            TurnKitConfig config,
            TurnKitGooglePlayAppConfigDraft draft,
            System.Func<string> getSessionToken,
            System.Action<TurnKitGooglePlayAppConfigDraft> onSuccess,
            System.Action<string> onError)
        {
            EditorCoroutineRunner.StartCoroutine(
                TurnKitAPI.SaveGooglePlayAppConfig(
                    config.gameKeyId,
                    draft,
                    getSessionToken(),
                    onSuccess,
                    onError));
        }

        internal static void ShowLoadError(string error)
        {
            Debug.LogWarning($"[TurnKit] Failed to load Google Play app config: {error}");
            EditorUtility.DisplayDialog("Google Play App Config Load Failed", error, "OK");
        }

        internal static void ShowSaveError(string error)
        {
            Debug.LogError($"[TurnKit] Failed to save Google Play app config: {error}");
            EditorUtility.DisplayDialog("Google Play App Config Save Failed", error, "OK");
        }
    }
}
