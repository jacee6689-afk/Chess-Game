using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChessGame
{
    /// <summary>
    /// Intelligent chess engine AI using Minimax with Alpha-Beta pruning,
    /// Piece-Square Tables (PST) positional evaluation, MVV-LVA move ordering,
    /// and Quiescence search across 3 difficulty tiers.
    /// </summary>
    public static class ChessAI
    {
        private const int Infinity = 1000000;
        private const int MateScore = 100000;

        #region Piece-Square Tables (From White's perspective; inverted for Black)

        // Pawns: encourage center control, advancing, and protecting king
        private static readonly int[] PawnTable = {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
             5,  5, 10, 25, 25, 10,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-20,-20, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0
        };

        // Knights: strong in the center, weak on edges
        private static readonly int[] KnightTable = {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50
        };

        // Bishops: active along long diagonals
        private static readonly int[] BishopTable = {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-10,-10,-10,-10,-10,-20
        };

        // Rooks: control central open files and 7th rank
        private static readonly int[] RookTable = {
              0,  0,  0,  0,  0,  0,  0,  0,
              5, 10, 10, 10, 10, 10, 10,  5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              0,  0,  0,  5,  5,  0,  0,  0
        };

        // Queen: active in center during middlegame
        private static readonly int[] QueenTable = {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
             -5,  0,  5,  5,  5,  5,  0, -5,
              0,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20
        };

        // King (Middlegame): castle early, stay behind pawns
        private static readonly int[] KingMiddlegameTable = {
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -10,-20,-20,-20,-20,-20,-20,-10,
             20, 20,  0,  0,  0,  0, 20, 20,
             20, 30, 10,  0,  0, 10, 30, 20
        };

        #endregion

        /// <summary>
        /// Coroutine that computes the best move with an organic thinking pause
        /// and invokes the completion callback with the chosen move.
        /// </summary>
        public static IEnumerator ThinkMove(
            Piece[,] board,
            bool whiteToMove,
            AIDifficulty difficulty,
            Vector2Int enPassantTarget,
            Func<Piece[,], bool, Vector2Int, List<Move>> legalMovesGen,
            Func<Piece[,], Move, Vector2Int> applyMoveFunc,
            Action<Move> onMoveComplete)
        {
            // Human-like thinking delay so the move feels organic
            float delay = (difficulty == AIDifficulty.Easy) ? 0.35f : (difficulty == AIDifficulty.Medium ? 0.50f : 0.65f);
            yield return new WaitForSeconds(delay);

            List<Move> legalMoves = legalMovesGen(board, whiteToMove, enPassantTarget);
            if (legalMoves.Count == 0)
            {
                onMoveComplete?.Invoke(default);
                yield break;
            }

            Move bestMove = legalMoves[0];

            if (difficulty == AIDifficulty.Easy)
            {
                // Easy: search depth 1-2 with small randomness
                bestMove = FindMoveAtDepth(board, whiteToMove, enPassantTarget, 2, legalMovesGen, applyMoveFunc, true);
            }
            else if (difficulty == AIDifficulty.Medium)
            {
                // Medium: search depth 3 with positional evaluation
                bestMove = FindMoveAtDepth(board, whiteToMove, enPassantTarget, 3, legalMovesGen, applyMoveFunc, false);
            }
            else
            {
                // Hard: search depth 4 with quiescence search on tactical captures
                bestMove = FindMoveAtDepth(board, whiteToMove, enPassantTarget, 4, legalMovesGen, applyMoveFunc, false, true);
            }

            onMoveComplete?.Invoke(bestMove);
        }

        /// <summary>
        /// Returns the engine-recommended best move for the Hint feature.
        /// </summary>
        public static Move GetHint(
            Piece[,] board,
            bool whiteToMove,
            Vector2Int enPassantTarget,
            Func<Piece[,], bool, Vector2Int, List<Move>> legalMovesGen,
            Func<Piece[,], Move, Vector2Int> applyMoveFunc)
        {
            return FindMoveAtDepth(board, whiteToMove, enPassantTarget, 3, legalMovesGen, applyMoveFunc, false);
        }

        private static Move FindMoveAtDepth(
            Piece[,] board,
            bool whiteToMove,
            Vector2Int enPassantTarget,
            int maxDepth,
            Func<Piece[,], bool, Vector2Int, List<Move>> legalMovesGen,
            Func<Piece[,], Move, Vector2Int> applyMoveFunc,
            bool addRandomness = false,
            bool useQuiescence = false)
        {
            List<Move> moves = legalMovesGen(board, whiteToMove, enPassantTarget);
            if (moves.Count == 0) return default;

            OrderMoves(moves, board);

            Move bestMove = moves[0];
            int bestScore = whiteToMove ? -Infinity : Infinity;

            for (int i = 0; i < moves.Count; i++)
            {
                Move move = moves[i];
                Piece[,] copy = (Piece[,])board.Clone();
                Vector2Int newEp = applyMoveFunc(copy, move);

                int score = Minimax(copy, maxDepth - 1, -Infinity, Infinity, !whiteToMove, newEp, legalMovesGen, applyMoveFunc, useQuiescence);

                if (addRandomness)
                {
                    // Small jitter of +/- 20 centipawns
                    score += UnityEngine.Random.Range(-20, 21);
                }

                if (whiteToMove)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                }
                else
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                }
            }

            return bestMove;
        }

        private static int Minimax(
            Piece[,] board,
            int depth,
            int alpha,
            int beta,
            bool isWhiteTurn,
            Vector2Int enPassantTarget,
            Func<Piece[,], bool, Vector2Int, List<Move>> legalMovesGen,
            Func<Piece[,], Move, Vector2Int> applyMoveFunc,
            bool useQuiescence)
        {
            if (depth <= 0)
            {
                if (useQuiescence)
                    return Quiescence(board, alpha, beta, isWhiteTurn, enPassantTarget, legalMovesGen, applyMoveFunc, 3);
                return EvaluateBoard(board);
            }

            List<Move> moves = legalMovesGen(board, isWhiteTurn, enPassantTarget);
            if (moves.Count == 0)
            {
                // Terminal: Checkmate or Stalemate
                return EvaluateTerminal(board, isWhiteTurn);
            }

            OrderMoves(moves, board);

            if (isWhiteTurn)
            {
                int maxEval = -Infinity;
                for (int i = 0; i < moves.Count; i++)
                {
                    Piece[,] copy = (Piece[,])board.Clone();
                    Vector2Int newEp = applyMoveFunc(copy, moves[i]);
                    int eval = Minimax(copy, depth - 1, alpha, beta, false, newEp, legalMovesGen, applyMoveFunc, useQuiescence);
                    maxEval = Mathf.Max(maxEval, eval);
                    alpha = Mathf.Max(alpha, eval);
                    if (beta <= alpha) break; // Beta cutoff
                }
                return maxEval;
            }
            else
            {
                int minEval = Infinity;
                for (int i = 0; i < moves.Count; i++)
                {
                    Piece[,] copy = (Piece[,])board.Clone();
                    Vector2Int newEp = applyMoveFunc(copy, moves[i]);
                    int eval = Minimax(copy, depth - 1, alpha, beta, true, newEp, legalMovesGen, applyMoveFunc, useQuiescence);
                    minEval = Mathf.Min(minEval, eval);
                    beta = Mathf.Min(beta, eval);
                    if (beta <= alpha) break; // Alpha cutoff
                }
                return minEval;
            }
        }

        private static int Quiescence(
            Piece[,] board,
            int alpha,
            int beta,
            bool isWhiteTurn,
            Vector2Int enPassantTarget,
            Func<Piece[,], bool, Vector2Int, List<Move>> legalMovesGen,
            Func<Piece[,], Move, Vector2Int> applyMoveFunc,
            int qDepth)
        {
            int standPat = EvaluateBoard(board);
            if (qDepth <= 0) return standPat;

            if (isWhiteTurn)
            {
                if (standPat >= beta) return beta;
                if (standPat > alpha) alpha = standPat;

                List<Move> moves = legalMovesGen(board, true, enPassantTarget);
                // Filter only capture moves for quiescence
                List<Move> captures = moves.FindAll(m => m.IsCapture || m.promotion != PieceType.None);
                OrderMoves(captures, board);

                for (int i = 0; i < captures.Count; i++)
                {
                    Piece[,] copy = (Piece[,])board.Clone();
                    Vector2Int newEp = applyMoveFunc(copy, captures[i]);
                    int score = Quiescence(copy, alpha, beta, false, newEp, legalMovesGen, applyMoveFunc, qDepth - 1);
                    if (score >= beta) return beta;
                    if (score > alpha) alpha = score;
                }
                return alpha;
            }
            else
            {
                if (standPat <= alpha) return alpha;
                if (standPat < beta) beta = standPat;

                List<Move> moves = legalMovesGen(board, false, enPassantTarget);
                List<Move> captures = moves.FindAll(m => m.IsCapture || m.promotion != PieceType.None);
                OrderMoves(captures, board);

                for (int i = 0; i < captures.Count; i++)
                {
                    Piece[,] copy = (Piece[,])board.Clone();
                    Vector2Int newEp = applyMoveFunc(copy, captures[i]);
                    int score = Quiescence(copy, alpha, beta, true, newEp, legalMovesGen, applyMoveFunc, qDepth - 1);
                    if (score <= alpha) return alpha;
                    if (score < beta) beta = score;
                }
                return beta;
            }
        }

        private static int EvaluateTerminal(Piece[,] board, bool whiteToMove)
        {
            // If the king is under attack, it's checkmate. Otherwise stalemate (0).
            bool inCheck = IsKingAttacked(board, whiteToMove);
            if (inCheck)
            {
                return whiteToMove ? -MateScore : MateScore;
            }
            return 0; // Stalemate
        }

        private static bool IsKingAttacked(Piece[,] board, bool kingIsWhite)
        {
            Vector2Int kingPos = new Vector2Int(-1, -1);
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    if (board[x, y].type == PieceType.King && board[x, y].white == kingIsWhite)
                    {
                        kingPos = new Vector2Int(x, y);
                        break;
                    }
                }
                if (kingPos.x >= 0) break;
            }

            if (kingPos.x < 0) return false;
            return IsAttackedByColor(board, kingPos, !kingIsWhite);
        }

        private static bool IsAttackedByColor(Piece[,] board, Vector2Int square, bool byWhite)
        {
            int pY = square.y + (byWhite ? -1 : 1);
            for (int dx = -1; dx <= 1; dx += 2)
            {
                int px = square.x + dx;
                if (px >= 0 && px < 8 && pY >= 0 && pY < 8)
                {
                    Piece p = board[px, pY];
                    if (p.type == PieceType.Pawn && p.white == byWhite) return true;
                }
            }

            int[,] jumps = { { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 } };
            for (int i = 0; i < 8; i++)
            {
                int x = square.x + jumps[i, 0], y = square.y + jumps[i, 1];
                if (x >= 0 && x < 8 && y >= 0 && y < 8)
                {
                    Piece p = board[x, y];
                    if (p.type == PieceType.Knight && p.white == byWhite) return true;
                }
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    for (int n = 1; n < 8; n++)
                    {
                        int x = square.x + dx * n, y = square.y + dy * n;
                        if (x < 0 || x >= 8 || y < 0 || y >= 8) break;
                        Piece p = board[x, y];
                        if (p.IsEmpty) continue;

                        if (p.white == byWhite)
                        {
                            if (n == 1 && p.type == PieceType.King) return true;
                            if ((dx == 0 || dy == 0) && (p.type == PieceType.Rook || p.type == PieceType.Queen)) return true;
                            if ((dx != 0 && dy != 0) && (p.type == PieceType.Bishop || p.type == PieceType.Queen)) return true;
                        }
                        break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Evaluates board position (positive favors White, negative favors Black).
        /// Combines material values with positional Piece-Square Tables (PST).
        /// </summary>
        public static int EvaluateBoard(Piece[,] board)
        {
            int score = 0;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Piece p = board[x, y];
                    if (p.IsEmpty) continue;

                    int val = p.MaterialValue;
                    int pst = GetPstScore(p.type, x, y, p.white);

                    int total = val + pst;
                    score += p.white ? total : -total;
                }
            }

            return score;
        }

        private static int GetPstScore(PieceType type, int x, int y, bool isWhite)
        {
            // Table index is 0..63 where row 0 is rank 8 (top) for standard tables
            // For White, y=0 is rank 1 (bottom), so rank 8 is 7-y
            int tableRow = isWhite ? (7 - y) : y;
            int tableCol = isWhite ? x : (7 - x);
            int idx = tableRow * 8 + tableCol;

            switch (type)
            {
                case PieceType.Pawn: return PawnTable[idx];
                case PieceType.Knight: return KnightTable[idx];
                case PieceType.Bishop: return BishopTable[idx];
                case PieceType.Rook: return RookTable[idx];
                case PieceType.Queen: return QueenTable[idx];
                case PieceType.King: return KingMiddlegameTable[idx];
                default: return 0;
            }
        }

        private static void OrderMoves(List<Move> moves, Piece[,] board)
        {
            // Order captures using MVV-LVA (Most Valuable Victim - Least Valuable Attacker)
            moves.Sort((a, b) =>
            {
                int scoreA = ScoreMove(a, board);
                int scoreB = ScoreMove(b, board);
                return scoreB.CompareTo(scoreA);
            });
        }

        private static int ScoreMove(Move move, Piece[,] board)
        {
            int score = 0;
            if (move.IsCapture)
            {
                Piece victim = board[move.to.x, move.to.y];
                Piece attacker = board[move.from.x, move.from.y];
                int victimVal = victim.IsEmpty ? 100 : victim.MaterialValue; // En passant victim is 100
                int attackerVal = attacker.MaterialValue;
                score += 10000 + (victimVal * 10 - attackerVal);
            }
            if (move.promotion != PieceType.None)
            {
                score += 9000;
            }
            if (move.castle)
            {
                score += 500;
            }
            return score;
        }
    }
}
