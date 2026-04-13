using System.Collections.Generic;
using TurnKit.Internal.ParrelSync;
using UnityEngine;
using UnityEngine.UI;

namespace TurnKit.Example
{
    public class RelaySyncedLeaderboardControllerExample : MonoBehaviour
    {
        [Header("Input")] [SerializeField] private InputField playerIdText;
        [SerializeField] private InputField scoreInput;
        [SerializeField] private Text statusText;

        [Header("Leaderboard")] [SerializeField]
        private Transform content;

        [SerializeField] private LeaderboardEntryViewExample entryPrefab;

        private readonly List<LeaderboardEntryViewExample> _entries = new();
        
        private void Awake()
        {
#if UNITY_EDITOR
            playerIdText.text = ClonesManager.IsClone() ? "player2" : "player1";
#endif
            Relay.OnMatchStarted += (_, _) => { statusText.text = "Game started"; };
            Relay.OnMoveMade += (msg, _) => { ValidateStatChange(msg); };
            Relay.OnGameEnded += (_) => { statusText.text = "Game ended"; };
        }

        private void ValidateStatChange(MoveMadeMessage msg)
        {
            bool isExampleValid = !(msg.statChanges.TryGet(ExampleConfig.Stats.Score, out var scoreChange) && scoreChange.Value < 0);
            // add your game logic here to check if score is valid
            // alternatively use msg.statChanges.allChanges or msg.statChanges.doubleChanges
            Relay.Vote(msg.moveNumber, isExampleValid);
        }

        public async void OnFindMatch()
        {
            statusText.text = "Waiting for opponent, connect with another client";
            await Relay.MatchWithAnyone(playerIdText.text, ExampleConfig.Slug);
        }
        
        public void OnAddRelayScoreButton()
        {
            Relay.Stat(ExampleConfig.Stats.Score).ForPlayer(Relay.MySlot).Add(double.Parse(scoreInput.text));
            // this stat is connected to Leaderboards via config and executes Leaderboard.SubmitScore but on backend and is verified by votes of other players in this match
            // go to asset menu TurnKit > Configuration > ExampleConfig to see connection, can use same way for your own webhooks to your backend or similar
            Relay.EndMyTurn();
        }

        public void OnEndRelayMatch()
        {
            Relay.EndGame();
            statusText.text = "Click end game on other client too";
        }

        public async void OnShowTopScores()
        {
            statusText.text = "Game ended";
            var result = await Leaderboard.GetTopScores(playerIdText.text);
            var scores = result.scores;
            
            foreach (var e in _entries) Destroy(e.gameObject);
            _entries.Clear();

            for (int i = 0; i < scores.Count; i++)
            {
                var view = Instantiate(entryPrefab, content);
                view.rankText.text = $"#{1 + i}";
                view.nameText.text = scores[i].n;
                view.scoreText.text = scores[i].s.ToString("N0");
                _entries.Add(view);
            }
        }
    }
}
