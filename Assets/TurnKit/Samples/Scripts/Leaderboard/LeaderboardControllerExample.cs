using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TurnKit.Example
{
    public class LeaderboardControllerExample : MonoBehaviour
    {
        [Header("Input")] [SerializeField] private InputField playerIdText;
        [SerializeField] private InputField scoreInput;

        [Header("Leaderboard")] [SerializeField]
        private Transform content;

        [SerializeField] private LeaderboardEntryViewExample entryPrefab;

        private readonly List<LeaderboardEntryViewExample> _entries = new();
        
        public async void OnSubmitScoreButton()
        {
            var name = playerIdText.text;
            var score = double.TryParse(scoreInput.text, out var s) ? s : 0;
            await Leaderboard.SubmitScore(name, score);
            await RefreshTopScores();
        }

        public async void OnGetTopScoresButton()
        {
            await RefreshTopScores();
        }

        public async void OnGetPlayerRankAndSurroundingButton()
        {
            PlayerScore result = await Leaderboard.GetPlayerRank(playerIdText.text);
            PopulateList(result.scores, result.startRank);
        }
        
        public async void OnGetTopScoresAndPlayerRankButton()
        {
            CombinedScores result = await Leaderboard.GetCombined(playerIdText.text); // Can add more params like Leaderboard.GetCombined(playerNameInput.text, 10, 3, true, "new-admin-added-leaderboard");
            PopulateList(result.topScores.scores);
            PopulateList(result.playerScore.scores, result.playerScore.startRank, true);
        }

        private async Task RefreshTopScores()
        {
            var result = await Leaderboard.GetTopScores(playerIdText.text);
            PopulateList(result.scores);
        }

        private void PopulateList(List<LeaderboardEntry> scores, long startRank = 1, bool skipDestroy = false)
        {
            if (!skipDestroy)
            {
                foreach (var e in _entries) Destroy(e.gameObject);
                _entries.Clear();
            }

            for (int i = 0; i < scores.Count; i++)
            {
                var view = Instantiate(entryPrefab, content);
                view.rankText.text = $"#{startRank + i}";
                view.nameText.text = scores[i].n;
                view.scoreText.text = scores[i].s.ToString("N0");
                _entries.Add(view);
            }
        }
    }
}
