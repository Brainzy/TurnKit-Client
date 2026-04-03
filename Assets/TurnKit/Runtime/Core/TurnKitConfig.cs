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

        [Header("Leaderboard")]
        [Tooltip("Default leaderboard slug. A 'global' leaderboard is created automatically for your game key, add more if you need via swagger api.")]
        public string defaultLeaderboard = "global";

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
        }
    
        [Serializable]
        public class RelayListConfig
        {
            public string name;
            public string tag;
            public List<PlayerSlot> ownerSlots = new();
            public List<PlayerSlot> visibleToSlots = new();
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
    }
}