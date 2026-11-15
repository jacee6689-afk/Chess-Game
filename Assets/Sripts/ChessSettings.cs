using System;
using UnityEngine;

namespace ChessGame
{
    /// <summary>
    /// Manages persistent user preferences, audio levels, board aesthetics, and camera defaults.
    /// Automatically saves and loads settings via Unity's PlayerPrefs.
    /// </summary>
    public static class ChessSettings
    {
        private const string KeySoundVolume = "Chess_SoundVolume";
        private const string KeySoundEnabled = "Chess_SoundEnabled";
        private const string KeyMusicVolume = "Chess_MusicVolume";
        private const string KeyMusicEnabled = "Chess_MusicEnabled";
        private const string KeyShowCoords = "Chess_ShowCoords";
        private const string KeyShowHighlights = "Chess_ShowHighlights";
        private const string KeyShowLastMove = "Chess_ShowLastMove";
        private const string KeyDefaultTopDown = "Chess_DefaultTopDown";
        private const string KeyAutoFlip2P = "Chess_AutoFlip2P";
        private const string KeyWhitePieceColor = "Chess_WhitePieceColor";

        public static float SoundVolume { get; set; } = 0.85f;
        public static bool SoundEnabled { get; set; } = true;
        public static float MusicVolume { get; set; } = 0.3f;
        public static bool MusicEnabled { get; set; } = true;
        public static bool ShowCoordinates { get; set; } = true;
        public static bool ShowMoveHighlights { get; set; } = true;
        public static bool ShowLastMoveHighlight { get; set; } = true;
        public static bool DefaultTopDown { get; set; } = false;
        public static bool AutoFlipInTwoPlayer { get; set; } = false;
        public static int WhitePieceColorIndex { get; set; } = 0;

        public static event Action OnSettingsChanged;

        static ChessSettings()
        {
            Load();
        }

        public static void Load()
        {
            SoundVolume = PlayerPrefs.GetFloat(KeySoundVolume, 0.85f);
            SoundEnabled = PlayerPrefs.GetInt(KeySoundEnabled, 1) == 1;
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, 0.3f));
            MusicEnabled = PlayerPrefs.GetInt(KeyMusicEnabled, 1) == 1;
            ShowCoordinates = PlayerPrefs.GetInt(KeyShowCoords, 1) == 1;
            ShowMoveHighlights = PlayerPrefs.GetInt(KeyShowHighlights, 1) == 1;
            ShowLastMoveHighlight = PlayerPrefs.GetInt(KeyShowLastMove, 1) == 1;
            DefaultTopDown = PlayerPrefs.GetInt(KeyDefaultTopDown, 0) == 1;
            AutoFlipInTwoPlayer = PlayerPrefs.GetInt(KeyAutoFlip2P, 0) == 1;
            WhitePieceColorIndex = PlayerPrefs.GetInt(KeyWhitePieceColor, 0);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(KeySoundVolume, SoundVolume);
            PlayerPrefs.SetInt(KeySoundEnabled, SoundEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
            PlayerPrefs.SetInt(KeyMusicEnabled, MusicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowCoords, ShowCoordinates ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowHighlights, ShowMoveHighlights ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowLastMove, ShowLastMoveHighlight ? 1 : 0);
            PlayerPrefs.SetInt(KeyDefaultTopDown, DefaultTopDown ? 1 : 0);
            PlayerPrefs.SetInt(KeyAutoFlip2P, AutoFlipInTwoPlayer ? 1 : 0);
            PlayerPrefs.SetInt(KeyWhitePieceColor, WhitePieceColorIndex);
            PlayerPrefs.Save();

            OnSettingsChanged?.Invoke();
        }

        public static void ResetToDefaults()
        {
            SoundVolume = 0.85f;
            SoundEnabled = true;
            MusicVolume = 0.3f;
            MusicEnabled = true;
            ShowCoordinates = true;
            ShowMoveHighlights = true;
            ShowLastMoveHighlight = true;
            DefaultTopDown = false;
            AutoFlipInTwoPlayer = false;
            WhitePieceColorIndex = 0;
            Save();
        }
    }
}
