using System.Collections.Generic;
using UnityEngine;

namespace ChessGame
{
    public enum DraughtsGameResult
    {
        InProgress,
        RedWins,
        BlackWins,
        Draw
    }

    public enum DraughtsMoveType
    {
        Normal,
        Capture,
        MultiCapture
    }

    public struct DraughtsPiece
    {
        public bool isEmpty;
        public bool isRed;    // Red = top player (rows 5-7), Black = bottom (rows 0-2)
        public bool isKing;
    }

    public struct DraughtsMove
    {
        public Vector2Int from;
        public Vector2Int to;
        public List<Vector2Int> captured;   // squares of captured pieces (can be multiple)
        public bool becomesKing;
        public string notation;             // e.g. "e3-f4" or "e3xf4xh6"
    }

    public static class DraughtsNotation
    {
        /// <summary>
        /// Returns the algebraic square name for a board position.
        /// File: column 0='a' ... column 7='h'
        /// Rank: row 0=1 ... row 7=8
        /// Example: (0,0) => "a1", (7,7) => "h8"
        /// </summary>
        public static string SquareName(Vector2Int sq)
        {
            char file = (char)('a' + sq.x);
            int rank = sq.y + 1;
            return $"{file}{rank}";
        }
    }
}
