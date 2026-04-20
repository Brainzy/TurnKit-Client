using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TurnKit
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
        public string n = "";
        public double s;
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
        public TopScores topScores = new();
        public PlayerScore playerScore = new();
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

        /// <summary>
        /// Submits a score for the current player.
        /// Player identity comes from the provided player id.
        /// </summary>
        public static Task<ScoreSubmitResponse> SubmitScore(
            string playerId,
            double score,
            string metadata = null,
            string leaderboard = null)
        {
            return SubmitScore(TurnKitClientIdentity.NoAuth(playerId), score, metadata, leaderboard);
        }

        public static Task<ScoreSubmitResponse> SubmitScore(
            TurnKitPlayerSession session,
            double score,
            string metadata = null,
            string leaderboard = null)
        {
            return SubmitScore(TurnKitClientIdentity.Authenticated(session), score, metadata, leaderboard);
        }

        public static Task<ScoreSubmitResponse> SubmitScore(
            TurnKitYourBackendProof proof,
            double score,
            string metadata = null,
            string leaderboard = null)
        {
            return SubmitScore(TurnKitClientIdentity.YourBackend(proof), score, metadata, leaderboard);
        }

        private static Task<ScoreSubmitResponse> SubmitScore(
            TurnKitClientIdentity identity,
            double score,
            string metadata,
            string leaderboard)
        {
            var url = $"{Base}{Slug(leaderboard)}/scores";
            var body = new SubmitScoreRequest { scoreValue = score, metadata = metadata };
            return Post<ScoreSubmitResponse>(identity, url, JsonUtility.ToJson(body));
        }

        /// <summary>
        /// Fetches the top N scores from a leaderboard.
        /// </summary>
        public static Task<TopScores> GetTopScores(
            string playerId,
            int limit = 10,
            string leaderboard = null)
        {
            return GetTopScores(TurnKitClientIdentity.NoAuth(playerId), limit, leaderboard);
        }

        public static Task<TopScores> GetTopScores(
            TurnKitPlayerSession session,
            int limit = 10,
            string leaderboard = null)
        {
            return GetTopScores(TurnKitClientIdentity.Authenticated(session), limit, leaderboard);
        }

        public static Task<TopScores> GetTopScores(
            TurnKitYourBackendProof proof,
            int limit = 10,
            string leaderboard = null)
        {
            return GetTopScores(TurnKitClientIdentity.YourBackend(proof), limit, leaderboard);
        }

        private static Task<TopScores> GetTopScores(
            TurnKitClientIdentity identity,
            int limit,
            string leaderboard)
        {
            var url = $"{Base}{Slug(leaderboard)}/top?limit={limit}";
            return Get<TopScores>(identity, url);
        }

        /// <summary>
        /// Fetches the current player's rank and surrounding entries.
        /// Uses the provided player id as the player identity.
        /// </summary>
        public static Task<PlayerScore> GetMyRank(
            string playerId,
            int surrounding = 5,
            string leaderboard = null)
        {
            return GetMyRank(TurnKitClientIdentity.NoAuth(playerId), surrounding, leaderboard);
        }

        public static Task<PlayerScore> GetMyRank(
            TurnKitPlayerSession session,
            int surrounding = 5,
            string leaderboard = null)
        {
            return GetMyRank(TurnKitClientIdentity.Authenticated(session), surrounding, leaderboard);
        }

        public static Task<PlayerScore> GetMyRank(
            TurnKitYourBackendProof proof,
            int surrounding = 5,
            string leaderboard = null)
        {
            return GetMyRank(TurnKitClientIdentity.YourBackend(proof), surrounding, leaderboard);
        }

        private static Task<PlayerScore> GetMyRank(
            TurnKitClientIdentity identity,
            int surrounding,
            string leaderboard)
        {
            var url = $"{Base}{Slug(leaderboard)}/me?surrounding={surrounding}";
            return Get<PlayerScore>(identity, url);
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
            return GetPlayerRank(TurnKitClientIdentity.NoAuth(playerId), playerId, surrounding, leaderboard);
        }

        public static Task<PlayerScore> GetPlayerRank(
            TurnKitPlayerSession session,
            string playerId,
            int surrounding = 5,
            string leaderboard = null)
        {
            return GetPlayerRank(TurnKitClientIdentity.Authenticated(session), playerId, surrounding, leaderboard);
        }

        public static Task<PlayerScore> GetPlayerRank(
            TurnKitYourBackendProof proof,
            string playerId,
            int surrounding = 5,
            string leaderboard = null)
        {
            return GetPlayerRank(TurnKitClientIdentity.YourBackend(proof), playerId, surrounding, leaderboard);
        }

        private static Task<PlayerScore> GetPlayerRank(
            TurnKitClientIdentity identity,
            string playerId,
            int surrounding,
            string leaderboard)
        {
            var url = $"{Base}{Slug(leaderboard)}/me?surrounding={surrounding}";
            return Get<PlayerScore>(identity, url);
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
            return GetCombined(TurnKitClientIdentity.NoAuth(playerId), topLimit, surrounding, leaderboard);
        }

        public static Task<CombinedScores> GetCombined(
            TurnKitPlayerSession session,
            int topLimit = 10,
            int surrounding = 5,
            string leaderboard = null)
        {
            return GetCombined(TurnKitClientIdentity.Authenticated(session), topLimit, surrounding, leaderboard);
        }

        public static Task<CombinedScores> GetCombined(
            TurnKitYourBackendProof proof,
            int topLimit = 10,
            int surrounding = 5,
            string leaderboard = null)
        {
            return GetCombined(TurnKitClientIdentity.YourBackend(proof), topLimit, surrounding, leaderboard);
        }

        private static Task<CombinedScores> GetCombined(
            TurnKitClientIdentity identity,
            int topLimit,
            int surrounding,
            string leaderboard)
        {
            var url = $"{Base}{Slug(leaderboard)}/combined" +
                      $"?topLimit={topLimit}&surrounding={surrounding}";
            return Get<CombinedScores>(identity, url);
        }

        private static async Task<T> Post<T>(TurnKitClientIdentity identity, string url, string json) where T : new()
        {
            using var req = TurnKitClientRequest.CreateJson(url, "POST", json);
            await TurnKitClientRequest.PrepareIdentity(req, identity);
            return await TurnKitClientRequest.SendJson<T>(req);
        }

        private static async Task<T> Get<T>(TurnKitClientIdentity identity, string url) where T : new()
        {
            using var req = TurnKitClientRequest.CreateGet(url);
            await TurnKitClientRequest.PrepareIdentity(req, identity);
            return await TurnKitClientRequest.SendJson<T>(req);
        }

        private static string Base =>
            TurnKitConfig.Instance.serverUrl.TrimEnd('/') + UrlPrefix;

        private static string Slug(string leaderboard) =>
            !string.IsNullOrWhiteSpace(leaderboard) ? leaderboard : TurnKitConfig.Instance.defaultLeaderboard;
    }
}
