using System;
using UnityEngine;

namespace ChessGame
{
    public enum PieceType
    {
        None,
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    public enum GameResult
    {
        InProgress,
        WhiteCheckmate,
        BlackCheckmate,
        Stalemate,
        FiftyMoveDraw,
        ThreefoldDraw,
        InsufficientMaterialDraw,
        WhiteTimeout,
        BlackTimeout,
        WhiteResign,
        BlackResign
    }

    public enum GameMode
    {
        PlayerVsAI,
        LocalTwoPlayer,
        AIVsAI,
        OnlineTwoPlayer
    }

    public enum AIDifficulty
    {
        Easy,      // Depth 1-2, occasional sub-optimal move (~1000 ELO)
        Medium,    // Depth 3, solid positional evaluation (~1500 ELO)
        Hard       // Depth 4 + Quiescence search, tactical (~1900 ELO)
    }

    public enum PlayerColor
    {
        White,
        Black,
        Random
    }

    public enum TimeControlType
    {
        Unlimited,
        OneMinute,
        ThreeMinutes,
        FiveMinutes,
        TenMinutes,
        FifteenMinutes
    }

    [Serializable]
    public struct Piece
    {
        public PieceType type;
        public bool white;
        public bool moved;

        public Piece(PieceType type, bool white, bool moved = false)
        {
            this.type = type;
            this.white = white;
            this.moved = moved;
        }

        public bool IsEmpty => type == PieceType.None;

        public int MaterialValue
        {
            get
            {
                switch (type)
                {
                    case PieceType.Pawn: return 100;
                    case PieceType.Knight: return 320;
                    case PieceType.Bishop: return 330;
                    case PieceType.Rook: return 500;
                    case PieceType.Queen: return 900;
                    case PieceType.King: return 20000;
                    default: return 0;
                }
            }
        }
    }

    [Serializable]
    public struct Move
    {
        public Vector2Int from;
        public Vector2Int to;
        public PieceType promotion;
        public bool enPassant;
        public bool castle;
        public PieceType capturedType;
        public string notation;

        public Move(Vector2Int from, Vector2Int to, PieceType promotion = PieceType.None, bool enPassant = false, bool castle = false, PieceType capturedType = PieceType.None, string notation = "")
        {
            this.from = from;
            this.to = to;
            this.promotion = promotion;
            this.enPassant = enPassant;
            this.castle = castle;
            this.capturedType = capturedType;
            this.notation = notation;
        }

        public bool IsCapture => capturedType != PieceType.None || enPassant;
    }

    public struct BoardSnapshot
    {
        public Piece[,] board;
        public Vector2Int enPassantTarget;
        public int halfmoveClock;
        public bool whiteToMove;
        public float whiteTime;
        public float blackTime;
        public Move lastMove;

        public BoardSnapshot(Piece[,] board, Vector2Int enPassantTarget, int halfmoveClock, bool whiteToMove, float whiteTime, float blackTime, Move lastMove)
        {
            this.board = (Piece[,])board.Clone();
            this.enPassantTarget = enPassantTarget;
            this.halfmoveClock = halfmoveClock;
            this.whiteToMove = whiteToMove;
            this.whiteTime = whiteTime;
            this.blackTime = blackTime;
            this.lastMove = lastMove;
        }
    }
}
