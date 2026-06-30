using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public partial class TurnKitEditorWindow : EditorWindow
    {
        [SerializeField] private TurnKitConfig config;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private TurnKitEditorWindowState state = new();

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
            LoadPlayerStoreTxCatalogEntries();
            LoadPlayerStorePurchaseMappings();
            LoadGooglePlayAppConfig();
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
            else
            {
                foreach (var requirement in relay.queueRequirements)
                {
                    if (requirement == null)
                    {
                        continue;
                    }

                    requirement.groups ??= new List<TurnKitConfig.QueueRequirementGroupConfig>();
                    if (requirement.groups.Count == 0 && requirement.conditions != null && requirement.conditions.Count > 0)
                    {
                        requirement.groups.Add(new TurnKitConfig.QueueRequirementGroupConfig
                        {
                            combinator = requirement.combinator,
                            conditions = new List<TurnKitConfig.RelayConditionConfig>(requirement.conditions)
                        });
                    }

                    requirement.conditions = new List<TurnKitConfig.RelayConditionConfig>();
                }
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


