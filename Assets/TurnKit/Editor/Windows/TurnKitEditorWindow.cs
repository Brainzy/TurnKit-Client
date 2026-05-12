using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public partial class TurnKitEditorWindow : EditorWindow
    {
        private TurnKitConfig config;
        private Vector2 scrollPosition;
        private readonly TurnKitEditorWindowState state = new();

        [MenuItem("Tools/TurnKit/Configuration", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<TurnKitEditorWindow>("TurnKit Configuration");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
            LoadWebhooks();
            LoadPlayerStoreDefs();
            InvalidateSyncStateCache();
        }

        private void LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:TurnKitConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<TurnKitConfig>(path);
            }
            else
            {
                Debug.LogError("[TurnKit] No TurnKitConfig found. Create one first.");
            }
        }

        private static void NormalizeRelayConfig(TurnKitConfig.RelayConfig relay)
        {
            if (relay.lists == null)
            {
                relay.lists = new List<TurnKitConfig.RelayListConfig>();
            }

            if (relay.trackedStats == null)
            {
                relay.trackedStats = new List<TurnKitConfig.TrackedStatConfig>();
            }

            if (relay.queueRequirements == null)
            {
                relay.queueRequirements = new List<TurnKitConfig.QueueRequirementConfig>();
            }

            if (relay.playerStoreMutations == null)
            {
                relay.playerStoreMutations = new List<TurnKitConfig.PlayerStoreMutationConfig>();
            }

            relay.afkTurnTimerSeconds = Mathf.Max(0, relay.afkTurnTimerSeconds);
            relay.disconnectedTurnTimerSeconds = Mathf.Max(0, relay.disconnectedTurnTimerSeconds);
        }

        private void InvalidateSyncStateCache()
        {
            TurnKitEditorWindowStateController.InvalidateSyncStateCache(state);
        }

        private void RefreshSyncStateIfNeeded()
        {
            TurnKitEditorWindowStateController.RefreshSyncStateIfNeeded(state, config, NormalizeRelayConfig);
        }
    }
}


