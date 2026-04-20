using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit.Editor
{
    [InitializeOnLoad]
    public class TurnKitAuthHandler
    {
        private static string activePollId;
        private static bool isPolling = false;
        private static int pollAttempts = 0;
        private static double nextPollTime = 0;
        
        static TurnKitAuthHandler()
        {
            EditorApplication.update += CheckAuth;
        }
        
        static void CheckAuth()
        {
            EditorApplication.update -= CheckAuth;
            
            var config = LoadOrCreateConfig();
            
            if (config == null)
            {
                Debug.LogError("[TurnKit] Failed to load or create config");
                return;
            }
            
            if (!string.IsNullOrEmpty(config.gameKeyId))
            {
                string jwt = EditorPrefs.GetString("TurnKit_SessionToken", "");
                
                if (string.IsNullOrEmpty(jwt) || IsJwtExpired(jwt))
                {
                    ShowReauthDialog();
                }
                
                return;
            }
            
            ShowFirstTimeSetup();
        }
        
        static void ShowFirstTimeSetup()
        {
            string defaultName = Application.productName;
            if (string.IsNullOrEmpty(defaultName) || defaultName == "Unity Project")
            {
                defaultName = Path.GetFileName(Application.dataPath.Replace("/Assets", ""));
            }
            
            var window = EditorWindow.GetWindow<ProjectSetupWindow>(true, "TurnKit Setup", true);
            window.Initialize(defaultName, StartAuthFlow);
            window.Show();
        }
        
        static void ShowReauthDialog()
        {
            bool reauth = EditorUtility.DisplayDialog(
                "TurnKit Session Expired",
                "Your TurnKit session has expired. Reconnect to continue syncing.",
                "Reconnect",
                "Later"
            );
            
            if (reauth)
            {
                var config = LoadOrCreateConfig();
                StartAuthFlow(config.projectName);
            }
        }
        
        public static void StartAuthFlow(string projectName)
        {
            Debug.Log($"[TurnKit] Starting auth flow for project: {projectName}");
            
            if (isPolling)
            {
                Debug.LogWarning("[TurnKit] Auth flow already in progress");
                return;
            }
            
            activePollId = Guid.NewGuid().ToString();
            pollAttempts = 0;
            nextPollTime = 0;
            
            string encodedName = UnityWebRequest.EscapeURL(projectName);
            string url = $"{TurnKitAPI.BaseUrl}{TurnKitAPI.AUTH_URL_SUFFIX}?projectName={encodedName}&pollId={activePollId}";
            
            Application.OpenURL(url);
            
            isPolling = true;
            EditorApplication.update += PollUpdate;
        }
        
        static void PollUpdate()
        {
            if (!isPolling)
            {
                EditorApplication.update -= PollUpdate;
                return;
            }
            
            if (EditorApplication.timeSinceStartup < nextPollTime)
            {
                return;
            }
            
            nextPollTime = EditorApplication.timeSinceStartup + 2.0;
            pollAttempts++;
            
            if (pollAttempts > 10) // 2 minutes
            {
                EditorApplication.update -= PollUpdate;
                isPolling = false;
                OnAuthError("Authentication timed out, ");
                return;
            }
            
            Debug.Log($"[TurnKit] Polling attempt {pollAttempts}/60...");
            
            string pollUrl = $"{TurnKitAPI.BaseUrl}/v1/dev/auth-status?pollId={activePollId}";
            var request = UnityWebRequest.Get(pollUrl);
            
            var op = request.SendWebRequest();
            
            EditorApplication.CallbackFunction checkRequest = null;
            checkRequest = () =>
            {
                if (!op.isDone) return;
                
                EditorApplication.update -= checkRequest;
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    try
                    {
                        var response = TurnKitAPI.ParseAuthResponse(responseText);
                        
                        EditorApplication.update -= PollUpdate;
                        isPolling = false;
                        
                        OnAuthSuccess(response);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[TurnKit] JSON parsing failed: {e.Message}");
                        Debug.LogError($"[TurnKit] Stack: {e.StackTrace}");
                        EditorApplication.update -= PollUpdate;
                        isPolling = false;
                        
                        OnAuthError($"Failed to parse response: {e.Message}");
                    }
                }
                else if (request.responseCode == 404)
                {
                    Debug.Log($"[TurnKit] Not ready yet (404)"); // Not ready yet, continue polling
                }
                else
                {
                    Debug.LogError($"[TurnKit] Poll failed: {request.error} (code: {request.responseCode})");
                }
            };
            
            EditorApplication.update += checkRequest;
        }
        
        static void OnAuthSuccess(TurnKitAPI.AuthResponse response)
        {
            EditorPrefs.SetString("TurnKit_SessionToken", response.jwt);
            
            var config = LoadOrCreateConfig();
            
            if (config == null)
            {
                Debug.LogError("[TurnKit] Failed to load config after auth");
                return;
            }
            
            config.projectName = response.gameKey.name;
            config.gameKeyId = response.gameKey.id;
            
            if (!string.IsNullOrEmpty(response.selectedClientKey))
            {
                config.clientKey = response.selectedClientKey;
            }

            config.playerAuthPolicy = response.playerAuthPolicy;
            config.playerAuthMethods = response.playerAuthMethods ?? new System.Collections.Generic.List<TurnKitConfig.PlayerAuthMethod>();
            config.leaderboards = response.leaderboards ?? new System.Collections.Generic.List<TurnKitConfig.LeaderboardConfig>();
            config.relayConfigs = response.relayConfigs ?? new System.Collections.Generic.List<TurnKitConfig.RelayConfig>();
            config.defaultLeaderboard = ResolveDefaultLeaderboard(config.defaultLeaderboard, config.leaderboards);
            
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            
            if (string.IsNullOrEmpty(response.selectedClientKey))
            {
                EditorUtility.DisplayDialog(
                    "Welcome Back!",
                    $"Connected to project: {response.gameKey.name}\n\n" +
                    $"Synced {config.leaderboards.Count} leaderboard(s).\n" +
                    $"Synced {response.relayConfigs.Count} relay config(s).\n\n" +
                    "Your existing client key has been preserved.",
                    "OK"
                );
                TurnKitEditorWindow.ShowWindow();
                return;
            }

            TurnKitGetStartedWindow.ShowWindow();
        }
        
        static void OnAuthError(string error)
        {
            Debug.LogError($"[TurnKit] Auth failed: {error}");
            EditorUtility.DisplayDialog("Authentication Failed", error, "OK");
        }

        private static string ResolveDefaultLeaderboard(string currentDefault, System.Collections.Generic.List<TurnKitConfig.LeaderboardConfig> leaderboards)
        {
            if (leaderboards == null || leaderboards.Count == 0)
            {
                return currentDefault;
            }

            if (!string.IsNullOrWhiteSpace(currentDefault) &&
                leaderboards.Any(lb => string.Equals(lb.slug, currentDefault, StringComparison.Ordinal)))
            {
                return currentDefault;
            }

            var global = leaderboards.FirstOrDefault(lb => string.Equals(lb.slug, "global", StringComparison.Ordinal));
            if (global != null && !string.IsNullOrWhiteSpace(global.slug))
            {
                return global.slug;
            }

            var first = leaderboards.FirstOrDefault(lb => !string.IsNullOrWhiteSpace(lb.slug));
            return first != null ? first.slug : currentDefault;
        }

        internal static TurnKitConfig LoadOrCreateConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:TurnKitConfig");
            
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<TurnKitConfig>(path);
            }

            string resourcesPath = GetResourcesPath();
            EnsureFolderExists(resourcesPath);
            
            var config = ScriptableObject.CreateInstance<TurnKitConfig>();
            config.projectName = Application.productName;
            AssetDatabase.CreateAsset(config, $"{resourcesPath}/TurnKitConfig.asset");
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[TurnKit] Created config at: {resourcesPath}/TurnKitConfig.asset");
            
            return config;
        }

        static string GetResourcesPath([CallerFilePath] string scriptFilePath = "")
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptDirectory = Path.GetDirectoryName(scriptFilePath);
            string turnKitPath = Path.GetFullPath(Path.Combine(scriptDirectory, "..", ".."));
            string resourcesFullPath = Path.Combine(turnKitPath, "Runtime", "Resources");
            string relativePath = resourcesFullPath.Replace(projectPath, "").Replace('\\', '/').TrimStart('/');
            return relativePath;
        }

        static void EnsureFolderExists(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string nextPath = $"{currentPath}/{segments[i]}";

                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[i]);
                }

                currentPath = nextPath;
            }
        }
        
        static bool IsJwtExpired(string jwt)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length != 3) return true;
                
                var payload = parts[1];
                payload = payload.Replace('-', '+').Replace('_', '/');
                
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                
                var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                
                if (payloadJson.Contains("\"exp\""))
                {
                    var expStart = payloadJson.IndexOf("\"exp\":") + 6;
                    var expEnd = payloadJson.IndexOf(',', expStart);
                    if (expEnd == -1) expEnd = payloadJson.IndexOf('}', expStart);
                    
                    var expStr = payloadJson.Substring(expStart, expEnd - expStart).Trim();
                    if (long.TryParse(expStr, out long exp))
                    {
                        var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                        return expTime < DateTimeOffset.UtcNow;
                    }
                }
                
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
