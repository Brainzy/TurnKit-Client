using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit.Leaderboard
{
    [Serializable]
    internal class SubmitScoreRequest
    {
        public double scoreValue;
        public string metadata;
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public string playerId = "";
        public double scoreValue;
        public long rank;
        public string metadata = "";
    }

    [Serializable]
    public class TopScores
    {
        public List<LeaderboardEntry> scores = new();
    }

    [Serializable]
    public class PlayerScore
    {
        public long startRank;
        public List<LeaderboardEntry> scores = new();
    }

    [Serializable]
    public class CombinedScores
    {
        public TopScores top = new();
        public PlayerScore player = new();
    }

    [Serializable]
    public class ScoreSubmitResponse
    {
        public string id = "";
        public string playerId = "";
        public double scoreValue;
        public long rank;
    }

    public static class Leaderboard
    {
        private const string UrlPrefix = "/v1/client/leaderboards/";
        private const int TimeoutSeconds = 10;

        /// <summary>
        /// Submits a score for the current player.
        /// Player identity comes from TurnKitConfig.PlayerId.
        /// </summary>
        public static Task<ScoreSubmitResponse> SubmitScore(
            string playerId,
            double score,
            string metadata = null,
            string leaderboard = null)
        {
            var url = $"{Base}{Slug(leaderboard)}/scores";
            var body = new SubmitScoreRequest { scoreValue = score, metadata = metadata };
            return Post<ScoreSubmitResponse>(playerId, url, JsonUtility.ToJson(body));
        }

        /// <summary>
        /// Fetches the top N scores from a leaderboard.
        /// </summary>
        public static Task<TopScores> GetTopScores(
            string playerId,
            int limit = 10,
            string leaderboard = null)
        {
            var url = $"{Base}{Slug(leaderboard)}/top?limit={limit}";
            return Get<TopScores>(playerId, url);
        }

        /// <summary>
        /// Fetches the current player's rank and surrounding entries.
        /// Uses TurnKitConfig.PlayerId as the player identity.
        /// </summary>
        public static Task<PlayerScore> GetMyRank(
            string playerId,
            int surrounding = 5,
            string leaderboard = null)
        {
            var url = $"{Base}{Slug(leaderboard)}/me?surrounding={surrounding}";
            return Get<PlayerScore>(playerId, url);
        }

        /// <summary>
        /// Fetches a specific player's rank and surrounding entries.
        /// Use this to look up other players.
        /// </summary>
        public static Task<PlayerScore> GetPlayerRank(
            string playerId,
            int surrounding = 5,
            string leaderboard = null)
        {
            var url = $"{Base}{Slug(leaderboard)}/players/{UnityWebRequest.EscapeURL(playerId)}" +
                      $"?surrounding={surrounding}";
            return Get<PlayerScore>(playerId, url);
        }

        /// <summary>
        /// Fetches top scores and current player's rank in one request.
        /// </summary>
        public static Task<CombinedScores> GetCombined(
            string playerId,
            int topLimit = 10,
            int surrounding = 5,
            string leaderboard = null)
        {
            var url = $"{Base}{Slug(leaderboard)}/combined" +
                      $"?topLimit={topLimit}&surrounding={surrounding}";
            return Get<CombinedScores>(playerId, url);
        }

        private static async Task<T> Post<T>(string playerId, string url, string json) where T : new()
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bytes),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {playerId}");
            req.SetRequestHeader("X-Player-Id", playerId);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"TurnKit [{req.responseCode}]: {req.downloadHandler.text}");

            return JsonUtility.FromJson<T>(req.downloadHandler.text) ?? new T();
        }

        private static async Task<T> Get<T>(string playerId, string url) where T : new()
        {
            using var req = UnityWebRequest.Get(url);
            req.timeout = TimeoutSeconds;
            req.SetRequestHeader("Authorization", $"Bearer { TurnKitConfig.Instance.clientKey}");
            req.SetRequestHeader("X-Player-Id", playerId);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"TurnKit [{req.responseCode}]: {req.downloadHandler.text}");

            return JsonUtility.FromJson<T>(req.downloadHandler.text) ?? new T();
        }

        private static string Base =>
            TurnKitConfig.Instance.serverUrl.TrimEnd('/') + UrlPrefix;

        private static string Slug(string leaderboard) =>
            !string.IsNullOrWhiteSpace(leaderboard) ? leaderboard : TurnKitConfig.Instance.clientKey;
    }
}
