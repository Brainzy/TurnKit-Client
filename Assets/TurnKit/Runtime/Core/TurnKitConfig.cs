using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurnKit
{
    [CreateAssetMenu(fileName = "TurnKitConfig", menuName = "TurnKit/Config")]
    public class TurnKitConfig : ScriptableObject
    {
        [Header("Server")]
        public string serverUrl = "https://api.turnkit.dev";
        
        [Header("Project Binding")]
        public string projectName;
        public string gameKeyId;

        [Header("Authentication")]
        public string clientKey;
        public PlayerAuthPolicy playerAuthPolicy = PlayerAuthPolicy.NO_AUTH;
        public List<PlayerAuthMethod> playerAuthMethods = new();

        [Header("Leaderboard")]
        [Tooltip("Default leaderboard slug. A 'global' leaderboard is created automatically for your game key, add more if you need via swagger api.")]
        public string defaultLeaderboard = "global";
        public List<LeaderboardConfig> leaderboards = new();

        [Header("Log")] [Tooltip("OnMatchStarted and similar events will be logged in console.")]
        public bool enableLogging = true;
        
        [Header("Relay Configs")]
        public List<RelayConfig> relayConfigs = new List<RelayConfig>();

        private static TurnKitConfig _instance;

        public static TurnKitConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<TurnKitConfig>("TurnKitConfig");
                    if (_instance == null)
                        Debug.LogError("<b>[TurnKit]</b> Config asset missing! " +
                                       "Go to 'Create > TurnKit > Config' and save it in a Resources folder.");
                }
                return _instance;
            }
        }
        
        [Serializable]
        public class LeaderboardConfig
        {
            public string slug;
            public string displayName;
            public string sortOrder;
            public string scoreStrategy;
            public double minScore;
            public double maxScore;
            public string resetFrequency;
            public bool archiveOnReset;
            public string nextResetAt;
        }

        [Serializable]
        public class RelayConfig
        {
            public string id;
            public string slug;
            public int maxPlayers = 2;
            public TurnEnforcement turnEnforcement = TurnEnforcement.ROUND_ROBIN;
            public bool ignoreAllOwnership = false;
            public bool votingEnabled = false;
            public VotingMode votingMode = VotingMode.SYNC;
            public int votesRequired = 2;
            public int votesToFail = 1;
            public FailAction failAction = FailAction.SKIP_TURN;
            public int matchTimeoutMinutes = 10;
            public int turnTimeoutSeconds = 60;
            public int waitReconnectSeconds = 45;
            public List<RelayListConfig> lists = new();
            public List<TrackedStatConfig> trackedStats = new();
        }
    
        [Serializable]
        public class RelayListConfig
        {
            public string id;
            public string name;
            public string tag;
            public List<PlayerSlot> ownerSlots = new();
            public List<PlayerSlot> visibleToSlots = new();
        }

        [Serializable]
        public class TrackedStatConfig
        {
            public string id;
            public string name;
            public TrackedStatDataType dataType = TrackedStatDataType.DOUBLE;
            public TrackedStatScope scope = TrackedStatScope.MATCH;
            public double initialDouble;
            public string initialString;
            public List<string> initialList = new();
            public List<TrackedStatSyncTargetConfig> syncTo = new();
        }

        [Serializable]
        public class TrackedStatSyncTargetConfig
        {
            public string id;
            public TrackedStatSyncDestinationType destinationType = TrackedStatSyncDestinationType.LEADERBOARD;
            public string destinationId;
        }

        [Serializable]
        public class WebhookConfig
        {
            public string entityId;
            public string id;
            public string url;
            public List<WebhookHeader> headers = new();
            public string createdAt;
            public string updatedAt;
        }

        [Serializable]
        public class WebhookHeader
        {
            public string key;
            public string value;
        }
    
        public enum PlayerSlot
        {
            Player1 = 1,
            Player2 = 2,
            Player3 = 3,
            Player4 = 4,
            Player5 = 5,
            Player6 = 6,
            Player7 = 7,
            Player8 = 8
        }
    
        public enum TurnEnforcement
        {
            ROUND_ROBIN,
            FREE
        }
    
        public enum VotingMode
        {
            SYNC,
            ASYNC
        }
    
        public enum FailAction
        {
            SKIP_TURN,
            END_GAME
        }

        public enum TrackedStatDataType
        {
            DOUBLE,
            STRING,
            LIST_STRING
        }

        public enum TrackedStatScope
        {
            PER_PLAYER,
            MATCH
        }

        public enum TrackedStatSyncDestinationType
        {
            LEADERBOARD,
            WEBHOOK
        }

        public enum PlayerAuthPolicy
        {
            NO_AUTH,
            AUTH_REQUIRED
        }

        public enum PlayerAuthMethod
        {
            YOUR_BACKEND,
            EMAIL_OTP
        }
    }
}
