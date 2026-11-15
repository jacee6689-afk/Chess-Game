using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessGame
{
    /// <summary>
    /// Minimax with alpha-beta pruning AI for English draughts (8x8).
    /// </summary>
    public static class DraughtsAI
    {
        // Piece values
        private const int ManValue  = 100;
        private const int KingValue = 175;

        // Difficulty depth table
        private static readonly int[] DepthByDifficulty = { 2, 5, 8 };

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns the best move for the current side.
        /// difficulty: 0 = Easy (depth 2), 1 = Medium (depth 5), 2 = Hard (depth 8).
        /// </summary>
        public static DraughtsMove GetBestMove(
            DraughtsPiece[,] board,
            bool redToMove,
            int difficulty,
            Func<DraughtsPiece[,], bool, List<DraughtsMove>> getLegalMoves,
            Func<DraughtsPiece[,], DraughtsMove, DraughtsPiece[,]>  applyMove)
        {
            int depth = DepthByDifficulty[Mathf.Clamp(difficulty, 0, 2)];
            return Search(board, redToMove, depth, getLegalMoves, applyMove);
        }

        /// <summary>
        /// Returns a quick hint move (always searches to depth 3).
        /// </summary>
        public static DraughtsMove GetHint(
            DraughtsPiece[,] board,
            bool redToMove,
            Func<DraughtsPiece[,], bool, List<DraughtsMove>> getLegalMoves,
            Func<DraughtsPiece[,], DraughtsMove, DraughtsPiece[,]>  applyMove)
        {
            return Search(board, redToMove, 3, getLegalMoves, applyMove);
        }

        // -------------------------------------------------------------------------
        // Internal search
        // -------------------------------------------------------------------------

        private static DraughtsMove Search(
            DraughtsPiece[,] board,
            bool redToMove,
            int depth,
            Func<DraughtsPiece[,], bool, List<DraughtsMove>> getLegalMoves,
            Func<DraughtsPiece[,], DraughtsMove, DraughtsPiece[,]>  applyMove)
        {
            List<DraughtsMove> moves = getLegalMoves(board, redToMove);
            if (moves == null || moves.Count == 0)
                return default;

            DraughtsMove bestMove = moves[0];
            int bestScore = redToMove ? int.MinValue : int.MaxValue;

            foreach (DraughtsMove move in moves)
            {
                DraughtsPiece[,] next = applyMove(board, move);
                int score = Minimax(next, !redToMove, depth - 1,
                                    int.MinValue, int.MaxValue,
                                    getLegalMoves, applyMove);

                if (redToMove && score > bestScore)
                {
                    bestScore = score;
                    bestMove  = move;
                }
                else if (!redToMove && score < bestScore)
                {
                    bestScore = score;
                    bestMove  = move;
                }
            }

            return bestMove;
        }

        /// <summary>
        /// Alpha-beta minimax. Red is the maximising player.
        /// </summary>
        private static int Minimax(
            DraughtsPiece[,] board,
            bool redToMove,
            int depth,
            int alpha,
            int beta,
            Func<DraughtsPiece[,], bool, List<DraughtsMove>> getLegalMoves,
            Func<DraughtsPiece[,], DraughtsMove, DraughtsPiece[,]>  applyMove)
        {
            List<DraughtsMove> moves = getLegalMoves(board, redToMove);

            if (depth == 0 || moves == null || moves.Count == 0)
                return Evaluate(board, moves != null ? moves.Count : 0, redToMove);

            if (redToMove) // maximise
            {
                int value = int.MinValue;
                foreach (DraughtsMove move in moves)
                {
                    DraughtsPiece[,] next = applyMove(board, move);
                    value = Math.Max(value,
                        Minimax(next, false, depth - 1, alpha, beta, getLegalMoves, applyMove));
                    alpha = Math.Max(alpha, value);
                    if (alpha >= beta) break; // beta cut-off
                }
                return value;
            }
            else // minimise
            {
                int value = int.MaxValue;
                foreach (DraughtsMove move in moves)
                {
                    DraughtsPiece[,] next = applyMove(board, move);
                    value = Math.Min(value,
                        Minimax(next, true, depth - 1, alpha, beta, getLegalMoves, applyMove));
                    beta = Math.Min(beta, value);
                    if (alpha >= beta) break; // alpha cut-off
                }
                return value;
            }
        }

        // -------------------------------------------------------------------------
        // Static evaluation
        // -------------------------------------------------------------------------

        /// <summary>
        /// Positive score favours Red; negative score favours Black.
        /// Components:
        ///   - Material (man = 100, king = 175)
        ///   - Board-centre bonus (inner 4x4 squares)
        ///   - King-row protection bonus
        ///   - Mobility bonus (number of legal moves)
        /// </summary>
        private static int Evaluate(DraughtsPiece[,] board, int mobilityForCurrent, bool redToMove)
        {
            int material   = 0;
            int centre     = 0;
            int kingRow    = 0;
            int redMobility   = 0;
            int blackMobility = 0;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    DraughtsPiece p = board[row, col];
                    if (p.isEmpty) continue;

                    int sign = p.isRed ? +1 : -1;

                    // Material
                    material += sign * (p.isKing ? KingValue : ManValue);

                    // Centre bonus: columns 2-5, rows 2-5 get +10 each
                    if (col >= 2 && col <= 5 && row >= 2 && row <= 5)
                        centre += sign * 10;

                    // King-row protection bonus:
                    // Red pieces on their back rank (row 7) = good for red
                    // Black pieces on their back rank (row 0) = good for black
                    if (p.isRed   && row == 7) kingRow += 15;
                    if (!p.isRed  && row == 0) kingRow -= 15;
                }
            }

            // Mobility bonus (5 points per extra legal move)
            if (redToMove)
                redMobility = mobilityForCurrent * 5;
            else
                blackMobility = mobilityForCurrent * 5;

            return material + centre + kingRow + redMobility - blackMobility;
        }
    }
}
