using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    [CustomEditor(typeof(TurnKitConfig))]
    public class TurnKitConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (TurnKitConfig)target;
            
            // Big prominent button at top
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Open TurnKit Configuration", GUILayout.Height(40)))
            {
                TurnKitEditorWindow.ShowWindow();
            }
            
            EditorGUILayout.Space(10);
            
            // Divider
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            EditorGUILayout.Space(5);
            
            // Read-only summary view
            EditorGUILayout.LabelField("Configuration Summary", EditorStyles.boldLabel);
            
            EditorGUILayout.TextField("Project", config.projectName);
            EditorGUILayout.TextField("Game Key", string.IsNullOrEmpty(config.gameKeyId) ? "Not connected" : config.gameKeyId);
            config.clientKey = EditorGUILayout.TextField("Client Key", config.clientKey);
            EditorGUILayout.TextField("Player Auth Policy", config.playerAuthPolicy.ToString());
            EditorGUILayout.TextField("Player Auth Methods", config.playerAuthMethods == null || config.playerAuthMethods.Count == 0 ? "(none)" : string.Join(", ", config.playerAuthMethods));
            EditorGUILayout.TextField("Default Leaderboard", config.defaultLeaderboard);
            
            EditorGUILayout.Space(5);
            config.leaderboards ??= new System.Collections.Generic.List<TurnKitConfig.LeaderboardConfig>();
            EditorGUILayout.LabelField($"Leaderboards: {config.leaderboards.Count}");

            if (config.leaderboards.Count > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var leaderboard in config.leaderboards)
                {
                    string label = string.IsNullOrWhiteSpace(leaderboard.displayName)
                        ? leaderboard.slug
                        : $"{leaderboard.displayName} ({leaderboard.slug})";
                    EditorGUILayout.LabelField($"- {label}");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Relay Configs: {config.relayConfigs.Count}");
            
            if (config.relayConfigs.Count > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var relay in config.relayConfigs)
                {
                    EditorGUILayout.LabelField($"• {relay.slug} ({relay.maxPlayers}p, {relay.lists.Count} lists)");
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "To edit relay configurations, sync with server, or manage settings, " +
                "click 'Open TurnKit Configuration' above or go to Window > TurnKit > Configuration",
                MessageType.Info
            );
            
            // Connection status
            if (string.IsNullOrEmpty(config.clientKey))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("⚠ Not connected to TurnKit", MessageType.Warning);
                
                if (GUILayout.Button("Connect Now", GUILayout.Height(30)))
                {
                    string projectName = config.projectName;
                    
                    if (string.IsNullOrEmpty(projectName))
                    {
                        projectName = Application.productName;
                        if (string.IsNullOrEmpty(projectName) || projectName == "Unity Project")
                        {
                            projectName = System.IO.Path.GetFileName(Application.dataPath.Replace("/Assets", ""));
                        }
                    }
                    
                    TurnKitAuthHandler.StartAuthFlow(projectName);
                }
            }
            else
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("✓ Connected to TurnKit", MessageType.Info);
            }
            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }
    }
}
