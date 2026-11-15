using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessGame
{
    [Serializable]
    public class MatchRecord
    {
        public string result;      // "WIN", "LOSS", "DRAW"
        public string opponent;    // "AI (Medium)", "AI (Hard)", "Player 2"
        public string detail;      // "Checkmate", "Stalemate", "Time Out"
        public int totalMoves;
        public int eloDelta;
        public string date;
    }

    [Serializable]
    public class PlayerStatsData
    {
        public int elo = 1200;
        public int gamesPlayed = 0;
        public int wins = 0;
        public int losses = 0;
        public int draws = 0;
        public int currentStreak = 0;
        public int bestStreak = 0;
        public List<MatchRecord> recentMatches = new List<MatchRecord>();
    }

    /// <summary>
    /// Tracks persistent leaderboard ratings, win/loss records, win streaks, and match history.
    /// Uses standard Elo rating calculation (K=32) against AI difficulty tiers.
    /// </summary>
    public static class ChessStats
    {
        private const string PrefsKey = "Chess_PlayerStats_V1";
        private static PlayerStatsData data;

        public static int Elo => Data.elo;
        public static int GamesPlayed => Data.gamesPlayed;
        public static int Wins => Data.wins;
        public static int Losses => Data.losses;
        public static int Draws => Data.draws;
        public static int CurrentStreak => Data.currentStreak;
        public static int BestStreak => Data.bestStreak;
        public static List<MatchRecord> RecentMatches => Data.recentMatches;

        public static float WinRate => (GamesPlayed > 0) ? ((float)Wins / GamesPlayed * 100f) : 0f;

        private static PlayerStatsData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<PlayerStatsData>(json);
                }
                catch
                {
                    data = new PlayerStatsData();
                }
            }
            if (data == null) data = new PlayerStatsData();
        }

        public static void Save()
        {
            if (data == null) return;
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }

        public static void RecordMatch(GameResult result, GameMode mode, AIDifficulty diff, bool playerWasWhite, int totalMoves)
        {
            if (result == GameResult.InProgress) return;

            bool isWin = false;
            bool isLoss = false;
            bool isDraw = false;
            string detail = "";

            switch (result)
            {
                case GameResult.WhiteCheckmate:
                    isWin = playerWasWhite;
                    isLoss = !playerWasWhite;
                    detail = "Checkmate";
                    break;
                case GameResult.BlackCheckmate:
                    isWin = !playerWasWhite;
                    isLoss = playerWasWhite;
                    detail = "Checkmate";
                    break;
                case GameResult.WhiteTimeout:
                    isWin = !playerWasWhite;
                    isLoss = playerWasWhite;
                    detail = "Time Out";
                    break;
                case GameResult.BlackTimeout:
                    isWin = playerWasWhite;
                    isLoss = !playerWasWhite;
                    detail = "Time Out";
                    break;
                case GameResult.WhiteResign:
                    isWin = !playerWasWhite;
                    isLoss = playerWasWhite;
                    detail = "Resignation";
                    break;
                case GameResult.BlackResign:
                    isWin = playerWasWhite;
                    isLoss = !playerWasWhite;
                    detail = "Resignation";
                    break;
                default:
                    isDraw = true;
                    detail = (result == GameResult.Stalemate) ? "Stalemate" : "Draw";
                    break;
            }

            int opponentElo = 1200;
            string oppName = "Local Player";

            if (mode == GameMode.PlayerVsAI)
            {
                switch (diff)
                {
                    case AIDifficulty.Easy: opponentElo = 1000; oppName = "AI (Easy)"; break;
                    case AIDifficulty.Medium: opponentElo = 1500; oppName = "AI (Medium)"; break;
                    case AIDifficulty.Hard: opponentElo = 1900; oppName = "AI (Hard)"; break;
                }
            }

            // Standard Elo update for vs AI games
            int eloDelta = 0;
            if (mode == GameMode.PlayerVsAI)
            {
                float actualScore = isWin ? 1.0f : (isDraw ? 0.5f : 0.0f);
                float expectedScore = 1.0f / (1.0f + Mathf.Pow(10f, (opponentElo - Data.elo) / 400f));
                eloDelta = Mathf.RoundToInt(32f * (actualScore - expectedScore));
                Data.elo = Mathf.Max(400, Data.elo + eloDelta);
            }

            Data.gamesPlayed++;
            if (isWin)
            {
                Data.wins++;
                Data.currentStreak++;
                if (Data.currentStreak > Data.bestStreak) Data.bestStreak = Data.currentStreak;
            }
            else if (isLoss)
            {
                Data.losses++;
                Data.currentStreak = 0;
            }
            else
            {
                Data.draws++;
            }

            MatchRecord rec = new MatchRecord
            {
                result = isWin ? "WIN" : (isLoss ? "LOSS" : "DRAW"),
                opponent = oppName,
                detail = detail,
                totalMoves = totalMoves,
                eloDelta = eloDelta,
                date = DateTime.Now.ToString("MM/dd HH:mm")
            };

            Data.recentMatches.Insert(0, rec);
            if (Data.recentMatches.Count > 12)
            {
                Data.recentMatches.RemoveAt(Data.recentMatches.Count - 1);
            }

            Save();
        }

        public static void ResetAllStats()
        {
            data = new PlayerStatsData();
            Save();
        }
    }
}
