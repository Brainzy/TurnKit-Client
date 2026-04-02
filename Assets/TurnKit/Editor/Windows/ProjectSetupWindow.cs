using System;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    public class ProjectSetupWindow : EditorWindow
    {
        private string projectName;
        private Action<string> onConfirm;
        private Vector2 scrollPosition;
        
        public void Initialize(string defaultName, Action<string> confirmCallback)
        {
            projectName = defaultName;
            onConfirm = confirmCallback;
            
            minSize = new Vector2(400, 320);
            maxSize = new Vector2(560, 420);
        }
        
        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(20);
            
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("Welcome to TurnKit", titleStyle);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "Choose a name for your game. This will create a dedicated game key for this Unity project.",
                MessageType.Info
            );
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Project Name:", EditorStyles.boldLabel);
            projectName = EditorGUILayout.TextField(projectName);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "This binding is permanent. All relay configs, leaderboards, and stats will be associated with this game key.",
                MessageType.Warning
            );
            
            EditorGUILayout.Space(20);
            
            GUI.enabled = !string.IsNullOrWhiteSpace(projectName);
            
            if (GUILayout.Button("Continue to Login", GUILayout.Height(40)))
            {
                onConfirm?.Invoke(projectName.Trim());
                Close();
            }
            
            GUI.enabled = true;
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
