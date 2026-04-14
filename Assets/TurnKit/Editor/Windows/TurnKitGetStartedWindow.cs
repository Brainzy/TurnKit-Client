using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TurnKit.Internal.ParrelSync;

namespace TurnKit.Editor
{
    public class TurnKitGetStartedWindow : EditorWindow
    {
        private const string AccountUrl = "https://turnkit.dev/dashboard";
        private const string DocsUrl = "https://turnkit.dev/docs";
        private const string DiscordUrl = "https://discord.gg/BUhb9a9xXd";
        private const string TicTacToeScenePath = "Assets/TurnKit/Samples/Scenes/Tic-Tac-Toe Example.unity";
        private const string RockPaperScissorsScenePath = "Assets/TurnKit/Samples/Scenes/Rock Paper Scissors Example.unity";

        private readonly List<SampleEntry> samples = new();
        private TurnKitConfig config;
        private Vector2 scrollPosition;
        private bool showCloneSection;
        private bool isConfigured;

        [MenuItem("TurnKit/GetStarted", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<TurnKitGetStartedWindow>("TurnKit Get Started");
            window.minSize = new Vector2(720f, 640f);
            window.RefreshContent();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            RefreshContent();
        }

        private void RefreshContent()
        {
            LoadConfig();
            isConfigured = config != null && !string.IsNullOrEmpty(config.clientKey);
            showCloneSection = !ClonesManager.IsClone() && ClonesManager.GetCloneProjectsPath().Count == 0;
            samples.Clear();
            samples.Add(new SampleEntry(
                "Tic-Tac-Toe",
                "Short and fun. It's a complete game in 70 lines of code. Uses turns, votes, and sending JSON functionality.",
                TicTacToeScenePath,
                true));
            samples.Add(new SampleEntry(
                "Rock Paper Scissors",
                "Uses server lists that hide opponents sign.",
                RockPaperScissorsScenePath,
                false));

            foreach (var scenePath in GetAdditionalSampleScenePaths())
            {
                samples.Add(new SampleEntry(
                    Path.GetFileNameWithoutExtension(scenePath),
                    "New sample added to TurnKit.",
                    scenePath,
                    false));
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(16f);
            DrawHeader();

            if (isConfigured)
            {
                GUILayout.Space(16f);
                DrawNextSteps();
                GUILayout.Space(16f);
                DrawFooter();
                GUILayout.Space(16f);
            }

            EditorGUILayout.EndScrollView();
        }

        private void LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:TurnKitConfig");
            config = guids.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<TurnKitConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 20,
                    wordWrap = true
                };
                var bodyStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true
                };

                EditorGUILayout.LabelField("Welcome to TurnKit!", titleStyle);
                GUILayout.Space(8f);

                if (isConfigured)
                {
                    EditorGUILayout.LabelField("TurnKit is now fully configured.", bodyStyle);
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField(
                        "We've created a Game Key, Client Key, and example relay config for you.",
                        bodyStyle);
                }
                else
                {
                    EditorGUILayout.LabelField("Please finish configuration first.", bodyStyle);
                    GUILayout.Space(10f);

                    if (GUILayout.Button("Open Configuration", GUILayout.Height(32f), GUILayout.Width(180f)))
                    {
                        TurnKitEditorWindow.ShowWindow();
                    }
                }
            }
        }

        private void DrawNextSteps()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var headingStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 15,
                    wordWrap = true
                };

                EditorGUILayout.LabelField("Next Steps", headingStyle);
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Next steps - let's get you playing fast", EditorStyles.wordWrappedLabel);
                GUILayout.Space(12f);

                if (showCloneSection)
                {
                    DrawCard(
                        "A. Test with 2 Unity Editors",
                        "We recommend cloning the project and running two Unity Editors side-by-side. Use button bellow for quick start, or for more control asset menu TurnKit/ParrelSync",
                        () =>
                        {
                            if (GUILayout.Button("Clone Project & Open New Editor", GUILayout.Height(40f)))
                            {
                                CreateCloneAndOpen();
                            }
                        });
                    GUILayout.Space(10f);
                }

                DrawSampleCard();
                GUILayout.Space(10f);
                DrawCard(
                    "C. Create your own relay config",
                    "Ready to build something custom?",
                    () =>
                    {
                        if (GUILayout.Button("Open Relay Configuration", GUILayout.Height(34f), GUILayout.Width(220f)))
                        {
                            TurnKitEditorWindow.ShowWindow();
                        }
                    });
            }
        }

        private void DrawSampleCard()
        {
            var sampleTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true
            };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("B. Try a sample scene", EditorStyles.boldLabel);
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Pick a sample to import and run instantly:", EditorStyles.wordWrappedLabel);
                GUILayout.Space(10f);

                foreach (var sample in samples)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            string title = sample.IsRecommended ? $"{sample.Name}   Recommended" : sample.Name;
                            GUILayout.Label(title, sampleTitleStyle, GUILayout.ExpandWidth(true));
                            GUILayout.Space(12f);

                            bool canOpen = AssetDatabase.LoadAssetAtPath<SceneAsset>(sample.ScenePath) != null;
                            using (new EditorGUI.DisabledScope(!canOpen))
                            {
                                if (GUILayout.Button("Open Scene", GUILayout.Height(26f), GUILayout.Width(150f)))
                                {
                                    OpenSampleScene(sample.ScenePath);
                                }
                            }
                        }

                        GUILayout.Space(2f);
                        EditorGUILayout.LabelField(sample.Description, EditorStyles.wordWrappedMiniLabel);
                    }

                    GUILayout.Space(6f);
                }
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var bodyStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true,
                    alignment = TextAnchor.MiddleLeft
                };

                EditorGUILayout.LabelField(
                    "Documentation and guides -> <color=#2F6FED>turnkit.dev/docs</color>",
                    bodyStyle);
                EditorGUILayout.LabelField(
                    "Detailed integration, API reference, advanced config, troubleshooting",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(8f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Docs", GUILayout.Height(28f), GUILayout.Width(120f)))
                    {
                        Application.OpenURL(DocsUrl);
                    }

                    GUILayout.FlexibleSpace();
                }

                GUILayout.Space(10f);
                EditorGUILayout.LabelField(
                    "Join our Discord community -> Join Discord",
                    bodyStyle);
                EditorGUILayout.LabelField(
                    "Get fast help, share your game, ask questions, and get early updates.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(8f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Join Discord", GUILayout.Height(28f), GUILayout.Width(120f)))
                    {
                        Application.OpenURL(DiscordUrl);
                    }

                    GUILayout.FlexibleSpace();
                }
                
                GUILayout.Space(4f);
                EditorGUILayout.LabelField(
                    "When you're ready for advanced settings, additional Client Keys, new projects, or usage monitoring, open the Dashboard. <color=#2F6FED>turnkit.dev/account</color>.",
                    bodyStyle);
                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Account", GUILayout.Height(32f), GUILayout.Width(160f)))
                    {
                        Application.OpenURL(AccountUrl);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCard(string title, string body, Action drawActions)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                GUILayout.Space(4f);
                EditorGUILayout.LabelField(body, EditorStyles.wordWrappedLabel);
                GUILayout.Space(10f);
                drawActions?.Invoke();
            }
        }

        private void CreateCloneAndOpen()
        {
            try
            {
                var clone = ClonesManager.CreateCloneFromCurrent();
                if (clone == null)
                {
                    EditorUtility.DisplayDialog("Clone Failed", "TurnKit could not create a ParrelSync clone.", "OK");
                    return;
                }

                ClonesManager.OpenProject(clone.projectPath);
                Close();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[TurnKit] Failed to create clone: {exception}");
                EditorUtility.DisplayDialog("Clone Failed", exception.Message, "OK");
            }
        }

        private void OpenSampleScene(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static IEnumerable<string> GetAdditionalSampleScenePaths()
        {
            string scenesDirectory = Path.Combine(Application.dataPath, "TurnKit", "Samples", "Scenes");
            if (!Directory.Exists(scenesDirectory))
            {
                yield break;
            }

            foreach (var filePath in Directory.GetFiles(scenesDirectory, "*.unity", SearchOption.TopDirectoryOnly))
            {
                string assetPath = "Assets" + filePath.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
                if (assetPath == TicTacToeScenePath || assetPath == RockPaperScissorsScenePath)
                {
                    continue;
                }

                yield return assetPath;
            }
        }

        private readonly struct SampleEntry
        {
            public SampleEntry(string name, string description, string scenePath, bool isRecommended)
            {
                Name = name;
                Description = description;
                ScenePath = scenePath;
                IsRecommended = isRecommended;
            }

            public string Name { get; }
            public string Description { get; }
            public string ScenePath { get; }
            public bool IsRecommended { get; }
        }
    }
}
