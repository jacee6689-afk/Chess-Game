using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChessGame
{
    /// <summary>
    /// MonoBehaviour that implements a full English Draughts (8×8 Checkers) game
    /// on top of the existing Chess project scene. It lives on its own GameObject
    /// and is activated / deactivated by GameManager.
    /// </summary>
    public class DraughtsBoard : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────────────

        private DraughtsPiece[,] board = new DraughtsPiece[8, 8];
        private bool redToMove = false;          // false = black (human) starts
        private DraughtsGameResult gameResult = DraughtsGameResult.InProgress;
        private bool isAiThinking = false;
        private bool aiEnabled = true;           // false in 2-player mode
        private int aiDifficulty = 1;            // 0=Easy, 1=Med, 2=Hard

        // Board geometry (set by ActivateDraughts)
        private Bounds playSurfaceBounds;
        private float squareWidth;

        // 3-D piece objects
        private GameObject[,] pieceObjects = new GameObject[8, 8];

        // Board square overlays (dark-square quads)
        private List<GameObject> squareOverlays = new List<GameObject>();

        // Selection & legal-move highlights
        private Vector2Int selectedSquare = new Vector2Int(-1, -1);
        private List<DraughtsMove> selectedPieceMoves = new List<DraughtsMove>();
        private List<DraughtsMove> allLegalMoves = new List<DraughtsMove>();
        private List<GameObject> highlightOverlays = new List<GameObject>();

        // Mid-jump (multi-capture) state
        private bool midJump = false;
        private Vector2Int midJumpPiece;
        private List<DraughtsMove> midJumpMoves;

        // Move history & 40-move rule
        private int movesSinceCapture = 0;
        private List<string> moveHistory = new List<string>();

        // Hint square (shown as a yellow overlay)
        private Vector2Int hintFrom = new Vector2Int(-1, -1);
        private Vector2Int hintTo = new Vector2Int(-1, -1);

        // Camera
        private Vector3 savedCameraPos;
        private Quaternion savedCameraRot;
        private bool cameraPositionSaved = false;

        // Right-drag orbit
        private bool isOrbiting = false;
        private Vector3 lastMousePos;

        // Board-plane for raycasting
        private Plane boardPlane;

        // Active flag (guards Update / OnGUI when not in draughts mode)
        private bool isActive = false;

        // Board-flipped view toggle
        private bool viewFlipped = false;

        // ─────────────────────────────────────────────────────────────────────
        //  UI styles (IMGUI)
        // ─────────────────────────────────────────────────────────────────────

        private GUIStyle styleTitle;
        private GUIStyle styleSubtitle;
        private GUIStyle styleLabel;
        private GUIStyle styleMoveLog;
        private GUIStyle styleButton;
        private GUIStyle styleAccentButton;
        private GUIStyle stylePanelBox;
        private GUIStyle styleStatus;

        private Texture2D texPanelBg;
        private Texture2D texButtonNormal;
        private Texture2D texButtonHover;
        private Texture2D texButtonActive;
        private Texture2D texAccent;
        private Texture2D texGold;
        private bool stylesInitialized = false;

        // ─────────────────────────────────────────────────────────────────────
        //  Constants
        // ─────────────────────────────────────────────────────────────────────

        private const int DrawMoveLimit = 40;
        private const float PieceHeightFraction = 0.18f;   // fraction of squareWidth
        private const float PieceRadiusFraction = 0.38f;   // fraction of squareWidth
        private const int SidebarWidth = 210;

        private static readonly Color ColorRed = new Color(0.78f, 0.14f, 0.14f);
        private static readonly Color ColorBlack = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color ColorDarkSquare = new Color(0.22f, 0.14f, 0.09f, 0.85f);
        private static readonly Color ColorHighlight = new Color(0.20f, 0.85f, 0.25f, 0.75f);
        private static readonly Color ColorSelected = new Color(0.90f, 0.80f, 0.10f, 0.80f);
        private static readonly Color ColorHint = new Color(0.90f, 0.70f, 0.10f, 0.80f);

        // ─────────────────────────────────────────────────────────────────────
        //  Public API (called by GameManager)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Activate draughts mode. Called by GameManager after hiding chess objects.</summary>
        public void ActivateDraughts(Bounds playSurfaceBounds, float squareWidth)
        {
            this.playSurfaceBounds = playSurfaceBounds;
            this.squareWidth = squareWidth > 0f ? squareWidth : playSurfaceBounds.size.x / 8f;

            // Raycasting board plane (Y = top of play surface)
            float boardY = playSurfaceBounds.center.y + playSurfaceBounds.extents.y;
            boardPlane = new Plane(Vector3.up, new Vector3(0f, boardY, 0f));

            // Position camera
            SaveAndSetCamera();

            // Build dark-square overlays
            BuildBoardOverlays();

            isActive = true;
        }

        /// <summary>Deactivate draughts mode and restore the scene.</summary>
        public void DeactivateDraughts()
        {
            isActive = false;
            ClearPieces();
            ClearHighlights();
            ClearBoardOverlays();

            if (cameraPositionSaved && Camera.main != null)
            {
                Camera.main.transform.position = savedCameraPos;
                Camera.main.transform.rotation = savedCameraRot;
                cameraPositionSaved = false;
            }
        }

        /// <summary>Start a new draughts game. vsAI=false for pass-and-play.</summary>
        public void StartNewDraughtsGame(bool vsAI, int difficulty)
        {
            aiEnabled = vsAI;
            aiDifficulty = Mathf.Clamp(difficulty, 0, 2);
            redToMove = false;
            gameResult = DraughtsGameResult.InProgress;
            isAiThinking = false;
            midJump = false;
            movesSinceCapture = 0;
            moveHistory.Clear();
            selectedSquare = new Vector2Int(-1, -1);
            selectedPieceMoves.Clear();
            hintFrom = new Vector2Int(-1, -1);
            hintTo = new Vector2Int(-1, -1);

            InitialiseBoard();
            SpawnAllPieces();
            ClearHighlights();
            RefreshLegalMoves();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!isActive) return;

            HandleOrbitCamera();

            if (gameResult != DraughtsGameResult.InProgress) return;
            if (isAiThinking) return;

            // AI turn
            if (aiEnabled && redToMove)
            {
                StartCoroutine(RunAI());
                return;
            }

            HandleBoardClick();
        }

        private void OnGUI()
        {
            if (!isActive) return;
            if (!stylesInitialized) InitStyles();

            DrawSidebar();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Camera
        // ─────────────────────────────────────────────────────────────────────

        private void SaveAndSetCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            if (!cameraPositionSaved)
            {
                savedCameraPos = cam.transform.position;
                savedCameraRot = cam.transform.rotation;
                cameraPositionSaved = true;
            }

            Vector3 boardCenter = playSurfaceBounds.center;
            boardCenter.y += playSurfaceBounds.extents.y;
            float boardSpan = playSurfaceBounds.size.x;

            Vector3 targetPos = boardCenter + new Vector3(0f, boardSpan * 1.4f, -boardSpan * 1.2f);
            cam.transform.position = targetPos;
            cam.transform.LookAt(boardCenter);
        }

        private void HandleOrbitCamera()
        {
            if (Camera.main == null) return;

            // Right mouse button drag → orbit
            if (Input.GetMouseButtonDown(1))
            {
                isOrbiting = true;
                lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1))
                isOrbiting = false;

            if (isOrbiting && Input.GetMouseButton(1))
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                lastMousePos = Input.mousePosition;

                Vector3 boardCenter = playSurfaceBounds.center;
                boardCenter.y += playSurfaceBounds.extents.y;

                Camera cam = Camera.main;
                Vector3 offset = cam.transform.position - boardCenter;

                // Rotate offset around world-Y (yaw) and camera-right (pitch)
                float yawDelta = -delta.x * 0.4f;
                float pitchDelta = delta.y * 0.4f;

                offset = Quaternion.AngleAxis(yawDelta, Vector3.up) * offset;
                Vector3 right = Vector3.Cross(Vector3.up, offset).normalized;
                offset = Quaternion.AngleAxis(pitchDelta, right) * offset;

                // Clamp pitch: keep camera above the board plane
                float minHeight = playSurfaceBounds.extents.y + squareWidth * 0.5f;
                if (boardCenter.y + offset.y < boardCenter.y + minHeight)
                    offset.y = minHeight;

                cam.transform.position = boardCenter + offset;
                cam.transform.LookAt(boardCenter);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Board Geometry Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns world position of the centre of a board square (col, row).</summary>
        private Vector3 SquareToWorld(int col, int row)
        {
            int displayCol = viewFlipped ? (7 - col) : col;
            int displayRow = viewFlipped ? (7 - row) : row;

            float boardY = playSurfaceBounds.center.y + playSurfaceBounds.extents.y;

            float startX = playSurfaceBounds.min.x + squareWidth * 0.5f;
            float startZ = playSurfaceBounds.min.z + squareWidth * 0.5f;

            float x = startX + displayCol * squareWidth;
            float z = startZ + displayRow * squareWidth;

            return new Vector3(x, boardY, z);
        }

        /// <summary>Convert world position on the board plane to board (col, row).</summary>
        private bool WorldToSquare(Vector3 worldPos, out int col, out int row)
        {
            col = Mathf.FloorToInt((worldPos.x - playSurfaceBounds.min.x) / squareWidth);
            row = Mathf.FloorToInt((worldPos.z - playSurfaceBounds.min.z) / squareWidth);

            if (viewFlipped)
            {
                col = 7 - col;
                row = 7 - row;
            }

            return col >= 0 && col < 8 && row >= 0 && row < 8;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Board initialisation
        // ─────────────────────────────────────────────────────────────────────

        private void InitialiseBoard()
        {
            board = new DraughtsPiece[8, 8];

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    // Draughts pieces only on dark squares ((col + row) % 2 == 1)
                    bool isDark = (col + row) % 2 == 1;

                    if (!isDark)
                    {
                        board[row, col] = new DraughtsPiece { isEmpty = true };
                        continue;
                    }

                    if (row >= 5) // Red rows 5-7 (top)
                    {
                        board[row, col] = new DraughtsPiece { isEmpty = false, isRed = true, isKing = false };
                    }
                    else if (row <= 2) // Black rows 0-2 (bottom)
                    {
                        board[row, col] = new DraughtsPiece { isEmpty = false, isRed = false, isKing = false };
                    }
                    else
                    {
                        board[row, col] = new DraughtsPiece { isEmpty = true };
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Piece Spawning
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnAllPieces()
        {
            ClearPieces();
            pieceObjects = new GameObject[8, 8];

            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                    if (!board[row, col].isEmpty)
                        SpawnPiece(col, row);
        }

        private void SpawnPiece(int col, int row)
        {
            DraughtsPiece p = board[row, col];
            if (p.isEmpty) return;

            Vector3 center = SquareToWorld(col, row);
            float boardY = playSurfaceBounds.center.y + playSurfaceBounds.extents.y;

            float radius = squareWidth * PieceRadiusFraction;
            float height = squareWidth * PieceHeightFraction;

            // Root holder
            GameObject root = new GameObject($"Draughts_{(p.isRed ? "Red" : "Black")}_{col}_{row}");
            root.transform.SetParent(transform);

            // Man (Cylinder)
            GameObject man = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            man.name = "Man";
            man.transform.SetParent(root.transform);
            man.transform.position = new Vector3(center.x, boardY + height * 0.5f, center.z);
            man.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            DisableCollider(man);
            SetMaterialColor(man, p.isRed ? ColorRed : ColorBlack);

            if (p.isKing)
                AddKingCrown(root, center, boardY, height, radius, p.isRed);

            pieceObjects[row, col] = root;
        }

        private void AddKingCrown(GameObject root, Vector3 center, float boardY, float height, float radius, bool isRed)
        {
            // Gold cylinder on top (narrower, shorter)
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "Crown";
            crown.transform.SetParent(root.transform);
            float crownH = height * 0.5f;
            float crownR = radius * 0.55f;
            crown.transform.position = new Vector3(center.x, boardY + height + crownH * 0.5f, center.z);
            crown.transform.localScale = new Vector3(crownR * 2f, crownH * 0.5f, crownR * 2f);
            DisableCollider(crown);
            SetMaterialColor(crown, new Color(0.95f, 0.78f, 0.10f)); // gold

            // Small Sphere on top
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Orb";
            orb.transform.SetParent(root.transform);
            float orbR = radius * 0.3f;
            orb.transform.position = new Vector3(center.x, boardY + height + crownH + orbR, center.z);
            orb.transform.localScale = Vector3.one * orbR * 2f;
            DisableCollider(orb);
            SetMaterialColor(orb, new Color(0.95f, 0.78f, 0.10f));
        }

        private void ClearPieces()
        {
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                    if (pieceObjects != null && pieceObjects[row, col] != null)
                    {
                        Destroy(pieceObjects[row, col]);
                        pieceObjects[row, col] = null;
                    }
        }

        private void RefreshPieceVisual(int col, int row)
        {
            if (pieceObjects[row, col] != null)
            {
                Destroy(pieceObjects[row, col]);
                pieceObjects[row, col] = null;
            }

            if (!board[row, col].isEmpty)
                SpawnPiece(col, row);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Board Overlays (dark squares)
        // ─────────────────────────────────────────────────────────────────────

        private void BuildBoardOverlays()
        {
            ClearBoardOverlays();
            float boardY = playSurfaceBounds.center.y + playSurfaceBounds.extents.y + 0.001f;
            float sz = squareWidth * 0.99f;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    // Only dark squares
                    if ((col + row) % 2 != 1) continue;

                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = $"DarkSq_{col}_{row}";
                    quad.transform.SetParent(transform);

                    Vector3 worldCenter = SquareToWorld(col, row);
                    quad.transform.position = new Vector3(worldCenter.x, boardY, worldCenter.z);
                    quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    quad.transform.localScale = new Vector3(sz, sz, 1f);

                    DisableCollider(quad);
                    SetMaterialColor(quad, ColorDarkSquare);

                    squareOverlays.Add(quad);
                }
            }
        }

        private void ClearBoardOverlays()
        {
            foreach (var go in squareOverlays)
                if (go != null) Destroy(go);
            squareOverlays.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Highlight Overlays
        // ─────────────────────────────────────────────────────────────────────

        private void ClearHighlights()
        {
            foreach (var go in highlightOverlays)
                if (go != null) Destroy(go);
            highlightOverlays.Clear();
        }

        private void DrawHighlightSquare(int col, int row, Color color, bool dot = true)
        {
            float boardY = playSurfaceBounds.center.y + playSurfaceBounds.extents.y + 0.003f;
            Vector3 worldCenter = SquareToWorld(col, row);

            if (dot)
            {
                // Small dot (Cylinder, flat)
                GameObject dotGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dotGo.name = "Highlight";
                dotGo.transform.SetParent(transform);
                float r = squareWidth * 0.18f;
                dotGo.transform.position = new Vector3(worldCenter.x, boardY + 0.001f, worldCenter.z);
                dotGo.transform.localScale = new Vector3(r * 2f, 0.001f, r * 2f);
                DisableCollider(dotGo);
                SetMaterialColor(dotGo, color);
                highlightOverlays.Add(dotGo);
            }
            else
            {
                // Full-square quad overlay
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Highlight";
                quad.transform.SetParent(transform);
                quad.transform.position = new Vector3(worldCenter.x, boardY, worldCenter.z);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                float sz = squareWidth * 0.9f;
                quad.transform.localScale = new Vector3(sz, sz, 1f);
                DisableCollider(quad);
                SetMaterialColor(quad, color);
                highlightOverlays.Add(quad);
            }
        }

        private void RefreshHighlights()
        {
            ClearHighlights();

            if (selectedSquare.x >= 0)
                DrawHighlightSquare(selectedSquare.x, selectedSquare.y, ColorSelected, false);

            List<DraughtsMove> movesToShow = midJump ? midJumpMoves : selectedPieceMoves;

            foreach (var m in movesToShow)
                DrawHighlightSquare(m.to.x, m.to.y, ColorHighlight, true);

            // Hint
            if (hintFrom.x >= 0)
            {
                DrawHighlightSquare(hintFrom.x, hintFrom.y, ColorHint, false);
                DrawHighlightSquare(hintTo.x, hintTo.y, ColorHint, true);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Input
        // ─────────────────────────────────────────────────────────────────────

        private void HandleBoardClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // Don't process clicks on sidebar
            if (Input.mousePosition.x < SidebarWidth) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!boardPlane.Raycast(ray, out float enter)) return;

            Vector3 hitPoint = ray.GetPoint(enter);

            if (!WorldToSquare(hitPoint, out int col, out int row)) return;

            Vector2Int clicked = new Vector2Int(col, row);

            if (midJump)
            {
                // Only the mid-jump piece can be "re-selected"; just try destinations
                HandleDestinationClick(clicked);
                return;
            }

            // Check if clicked on a destination of already selected piece
            if (selectedSquare.x >= 0 && IsLegalDestination(clicked, selectedPieceMoves))
            {
                HandleDestinationClick(clicked);
                return;
            }

            // Select a piece
            if (!board[row, col].isEmpty && board[row, col].isRed == redToMove)
            {
                SelectSquare(clicked);
                return;
            }

            // Deselect
            Deselect();
        }

        private void SelectSquare(Vector2Int sq)
        {
            selectedSquare = sq;
            selectedPieceMoves = MovesForPiece(sq, allLegalMoves);
            hintFrom = new Vector2Int(-1, -1);
            hintTo = new Vector2Int(-1, -1);
            RefreshHighlights();
        }

        private void Deselect()
        {
            selectedSquare = new Vector2Int(-1, -1);
            selectedPieceMoves.Clear();
            RefreshHighlights();
        }

        private bool IsLegalDestination(Vector2Int to, List<DraughtsMove> moves)
        {
            foreach (var m in moves)
                if (m.to == to) return true;
            return false;
        }

        private List<DraughtsMove> MovesForPiece(Vector2Int from, List<DraughtsMove> movePool)
        {
            List<DraughtsMove> result = new List<DraughtsMove>();
            foreach (var m in movePool)
                if (m.from == from) result.Add(m);
            return result;
        }

        private void HandleDestinationClick(Vector2Int to)
        {
            Vector2Int from = midJump ? midJumpPiece : selectedSquare;
            List<DraughtsMove> pool = midJump ? midJumpMoves : selectedPieceMoves;

            DraughtsMove? chosen = null;
            foreach (var m in pool)
            {
                if (m.to == to)
                {
                    chosen = m;
                    break;
                }
            }

            if (chosen == null) return;

            ExecuteMove(chosen.Value);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Move Execution
        // ─────────────────────────────────────────────────────────────────────

        private void ExecuteMove(DraughtsMove move)
        {
            bool isCapture = move.captured != null && move.captured.Count > 0;

            // Update 40-move counter
            if (isCapture)
                movesSinceCapture = 0;
            else
                movesSinceCapture++;

            // Apply to board data
            board = ApplyMoveToBoard(board, move);

            // Record notation
            moveHistory.Add(move.notation ?? GenerateNotation(move));

            // Audio
            if (isCapture)
                ChessAudio.Instance?.PlayCapture();
            else
                ChessAudio.Instance?.PlayMove();

            // Refresh piece visuals (move from, captured, to)
            int fromRow = move.from.y, fromCol = move.from.x;
            int toRow = move.to.y, toCol = move.to.x;

            RefreshPieceVisual(fromCol, fromRow);
            if (move.captured != null)
                foreach (var cap in move.captured)
                    RefreshPieceVisual(cap.x, cap.y);
            RefreshPieceVisual(toCol, toRow);

            // Check kinging sound / visual (the board data already reflects it)
            // (no special sound specified, PlayMove covers it)

            // Multi-jump: check if same piece can jump again
            if (isCapture)
            {
                List<DraughtsMove> furtherJumps = GetFurtherJumps(move.to);
                if (furtherJumps != null && furtherJumps.Count > 0)
                {
                    midJump = true;
                    midJumpPiece = move.to;
                    midJumpMoves = furtherJumps;
                    selectedSquare = move.to;
                    selectedPieceMoves = furtherJumps;
                    hintFrom = new Vector2Int(-1, -1);
                    hintTo = new Vector2Int(-1, -1);
                    RefreshHighlights();
                    return;
                }
            }

            // End of turn
            midJump = false;
            midJumpMoves = null;
            selectedSquare = new Vector2Int(-1, -1);
            selectedPieceMoves.Clear();
            hintFrom = new Vector2Int(-1, -1);
            hintTo = new Vector2Int(-1, -1);

            redToMove = !redToMove;
            RefreshLegalMoves();
            CheckEndConditions();
            RefreshHighlights();
        }

        /// <summary>Generate a further-jump list for a piece that just captured.</summary>
        private List<DraughtsMove> GetFurtherJumps(Vector2Int sq)
        {
            // Temporarily generate all captures available to the piece at sq for the CURRENT side
            // (after the move, the side hasn't changed yet, so redToMove still matches the mover)
            List<DraughtsMove> jumps = new List<DraughtsMove>();
            GetCapturesForPiece(board, sq.x, sq.y, board[sq.y, sq.x].isRed, jumps);
            return jumps;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Rules engine
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshLegalMoves()
        {
            allLegalMoves = GetAllLegalMoves(board, redToMove);
        }

        /// <summary>Public delegate-compatible method for the AI.</summary>
        public List<DraughtsMove> GetAllLegalMoves(DraughtsPiece[,] b, bool forRed)
        {
            // Mandatory capture: if any capture exists, return only captures
            List<DraughtsMove> captures = new List<DraughtsMove>();
            List<DraughtsMove> normals = new List<DraughtsMove>();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var p = b[row, col];
                    if (p.isEmpty || p.isRed != forRed) continue;

                    GetCapturesForPiece(b, col, row, forRed, captures);
                }
            }

            if (captures.Count > 0) return captures;

            // No captures — return normal diagonal moves
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var p = b[row, col];
                    if (p.isEmpty || p.isRed != forRed) continue;

                    GetNormalMovesForPiece(b, col, row, p, normals);
                }
            }

            return normals;
        }

        private void GetNormalMovesForPiece(DraughtsPiece[,] b, int col, int row,
                                             DraughtsPiece p, List<DraughtsMove> result)
        {
            // Forward direction: black moves up (+1), red moves down (-1)
            int fwd = p.isRed ? -1 : +1;

            int[][] dirs = p.isKing
                ? new[] { new[] { -1, +1 }, new[] { +1, +1 }, new[] { -1, -1 }, new[] { +1, -1 } }
                : new[] { new[] { -1, fwd }, new[] { +1, fwd } };

            foreach (var d in dirs)
            {
                int nc = col + d[0];
                int nr = row + d[1];
                if (!InBounds(nc, nr)) continue;
                if (!b[nr, nc].isEmpty) continue;

                bool becomesKing = !p.isKing && BecomeKing(nr, p.isRed);
                string from = DraughtsNotation.SquareName(new Vector2Int(col, row));
                string to = DraughtsNotation.SquareName(new Vector2Int(nc, nr));

                result.Add(new DraughtsMove
                {
                    from = new Vector2Int(col, row),
                    to = new Vector2Int(nc, nr),
                    captured = new List<Vector2Int>(),
                    becomesKing = becomesKing,
                    notation = $"{from}-{to}"
                });
            }
        }

        private void GetCapturesForPiece(DraughtsPiece[,] b, int col, int row, bool forRed,
                                          List<DraughtsMove> result)
        {
            DraughtsPiece p = b[row, col];
            if (p.isEmpty || p.isRed != forRed) return;

            int fwd = p.isRed ? -1 : +1;
            int[][] dirs = p.isKing
                ? new[] { new[] { -1, +1 }, new[] { +1, +1 }, new[] { -1, -1 }, new[] { +1, -1 } }
                : new[] { new[] { -1, fwd }, new[] { +1, fwd } };

            foreach (var d in dirs)
            {
                int midC = col + d[0];
                int midR = row + d[1];
                int landC = col + d[0] * 2;
                int landR = row + d[1] * 2;

                if (!InBounds(midC, midR) || !InBounds(landC, landR)) continue;
                if (b[midR, midC].isEmpty || b[midR, midC].isRed == forRed) continue;
                if (!b[landR, landC].isEmpty) continue;

                bool becomesKing = !p.isKing && BecomeKing(landR, forRed);
                string from = DraughtsNotation.SquareName(new Vector2Int(col, row));
                string land = DraughtsNotation.SquareName(new Vector2Int(landC, landR));

                result.Add(new DraughtsMove
                {
                    from = new Vector2Int(col, row),
                    to = new Vector2Int(landC, landR),
                    captured = new List<Vector2Int> { new Vector2Int(midC, midR) },
                    becomesKing = becomesKing,
                    notation = $"{from}x{land}"
                });
            }
        }

        /// <summary>Returns a new board state with the move applied (pure, used by AI).</summary>
        public DraughtsPiece[,] ApplyMoveToBoard(DraughtsPiece[,] b, DraughtsMove move)
        {
            // Clone
            DraughtsPiece[,] next = new DraughtsPiece[8, 8];
            Array.Copy(b, next, b.Length);

            int fromRow = move.from.y, fromCol = move.from.x;
            int toRow = move.to.y, toCol = move.to.x;

            DraughtsPiece piece = next[fromRow, fromCol];
            next[fromRow, fromCol] = new DraughtsPiece { isEmpty = true };

            // Remove captured pieces
            if (move.captured != null)
                foreach (var cap in move.captured)
                    next[cap.y, cap.x] = new DraughtsPiece { isEmpty = true };

            // Place piece (kinging)
            piece.isKing = piece.isKing || move.becomesKing;
            next[toRow, toCol] = piece;

            return next;
        }

        private bool BecomeKing(int row, bool isRed)
        {
            return (isRed && row == 0) || (!isRed && row == 7);
        }

        private static bool InBounds(int col, int row)
        {
            return col >= 0 && col < 8 && row >= 0 && row < 8;
        }

        private string GenerateNotation(DraughtsMove move)
        {
            string from = DraughtsNotation.SquareName(move.from);
            string to = DraughtsNotation.SquareName(move.to);
            bool cap = move.captured != null && move.captured.Count > 0;
            return cap ? $"{from}x{to}" : $"{from}-{to}";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  End Condition
        // ─────────────────────────────────────────────────────────────────────

        private void CheckEndConditions()
        {
            // Draw by 40-move rule
            if (movesSinceCapture >= DrawMoveLimit)
            {
                gameResult = DraughtsGameResult.Draw;
                ChessAudio.Instance?.PlayDraw();
                return;
            }

            // Win: opponent has no legal moves or no pieces
            if (allLegalMoves.Count == 0)
            {
                gameResult = redToMove ? DraughtsGameResult.BlackWins : DraughtsGameResult.RedWins;
                if (gameResult == DraughtsGameResult.RedWins)
                    ChessAudio.Instance?.PlayVictory();
                else
                    ChessAudio.Instance?.PlayDefeat();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AI Coroutine
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator RunAI()
        {
            isAiThinking = true;
            yield return new WaitForSeconds(0.4f);

            if (gameResult != DraughtsGameResult.InProgress || !isActive)
            {
                isAiThinking = false;
                yield break;
            }

            DraughtsMove best = DraughtsAI.GetBestMove(
                board, redToMove, aiDifficulty,
                GetAllLegalMoves,
                ApplyMoveToBoard);

            isAiThinking = false;

            if (best.from == default && best.to == default)
            {
                // No move found — game over
                RefreshLegalMoves();
                CheckEndConditions();
                yield break;
            }

            ExecuteMove(best);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Hint
        // ─────────────────────────────────────────────────────────────────────

        private void ShowHint()
        {
            if (gameResult != DraughtsGameResult.InProgress || isAiThinking) return;
            if (aiEnabled && redToMove) return; // hint is for the human

            DraughtsMove hint = DraughtsAI.GetHint(
                board, redToMove,
                GetAllLegalMoves,
                ApplyMoveToBoard);

            if (hint.from == default && hint.to == default) return;

            hintFrom = hint.from;
            hintTo = hint.to;
            RefreshHighlights();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Piece count helpers
        // ─────────────────────────────────────────────────────────────────────

        private int CountPieces(bool red)
        {
            int n = 0;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (!board[r, c].isEmpty && board[r, c].isRed == red) n++;
            return n;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IMGUI HUD
        // ─────────────────────────────────────────────────────────────────────

        private void InitStyles()
        {
            texPanelBg = MakeTex(new Color(0.10f, 0.12f, 0.18f, 0.94f));
            texButtonNormal = MakeTex(new Color(0.18f, 0.22f, 0.28f, 0.95f));
            texButtonHover = MakeTex(new Color(0.28f, 0.35f, 0.44f, 0.95f));
            texButtonActive = MakeTex(new Color(0.38f, 0.48f, 0.60f, 1f));
            texAccent = MakeTex(new Color(0.88f, 0.72f, 0.28f, 1f));
            texGold = MakeTex(new Color(0.92f, 0.78f, 0.32f, 0.9f));

            styleTitle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.88f, 0.72f) }
            };

            styleSubtitle = new GUIStyle
            {
                fontSize = 12,
                normal = { textColor = new Color(0.68f, 0.74f, 0.82f) }
            };

            styleLabel = new GUIStyle
            {
                fontSize = 13,
                normal = { textColor = new Color(0.90f, 0.92f, 0.95f) }
            };

            styleMoveLog = new GUIStyle
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.78f, 0.82f, 0.88f) }
            };

            styleButton = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = texButtonNormal, textColor = new Color(0.92f, 0.94f, 0.96f) },
                hover = { background = texButtonHover, textColor = Color.white },
                active = { background = texButtonActive, textColor = Color.white }
            };

            styleAccentButton = new GUIStyle(styleButton)
            {
                normal = { background = texAccent, textColor = new Color(0.08f, 0.08f, 0.08f) },
                hover = { background = texGold, textColor = Color.black },
                active = { background = texButtonActive, textColor = Color.white }
            };

            stylePanelBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = texPanelBg }
            };

            styleStatus = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.95f, 0.88f, 0.65f) }
            };

            stylesInitialized = true;
        }

        private Texture2D MakeTex(Color color)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }

        private void DrawSidebar()
        {
            int sw = SidebarWidth;
            int sh = Screen.height;

            // Panel background
            GUI.Box(new Rect(0, 0, sw, sh), GUIContent.none, stylePanelBox);

            int y = 10;
            int margin = 10;
            int innerW = sw - margin * 2;

            // ── Title ──────────────────────────────────────────────────────
            GUI.Label(new Rect(margin, y, innerW, 30), "DRAUGHTS", styleTitle);
            y += 34;

            // ── Piece Counts ───────────────────────────────────────────────
            int blackCount = CountPieces(false);
            int redCount = CountPieces(true);

            // Black indicator
            Color prevColor = GUI.color;
            GUI.color = !redToMove && gameResult == DraughtsGameResult.InProgress
                ? new Color(0.95f, 0.88f, 0.72f)
                : new Color(0.68f, 0.74f, 0.82f);
            GUI.Label(new Rect(margin, y, innerW, 22), $"⚫ Black: {blackCount} pieces", styleLabel);
            GUI.color = prevColor;
            y += 22;

            GUI.color = redToMove && gameResult == DraughtsGameResult.InProgress
                ? new Color(0.95f, 0.88f, 0.72f)
                : new Color(0.68f, 0.74f, 0.82f);
            GUI.Label(new Rect(margin, y, innerW, 22), $"🔴 Red:   {redCount} pieces", styleLabel);
            GUI.color = prevColor;
            y += 26;

            // ── Turn / Status ──────────────────────────────────────────────
            string statusText = GetStatusText();
            GUI.Label(new Rect(margin, y, innerW, 40), statusText, styleStatus);
            y += 46;

            // ── Move History ───────────────────────────────────────────────
            GUI.Label(new Rect(margin, y, innerW, 18), "Move History", styleSubtitle);
            y += 20;

            int logAreaH = 160;
            GUI.Box(new Rect(margin, y, innerW, logAreaH), GUIContent.none, stylePanelBox);

            // Show last 8 moves
            int startIndex = Mathf.Max(0, moveHistory.Count - 8);
            int logY = y + 4;
            for (int i = startIndex; i < moveHistory.Count; i++)
            {
                int moveNum = i + 1;
                GUI.Label(new Rect(margin + 4, logY, innerW - 8, 18),
                    $"{moveNum,2}. {moveHistory[i]}", styleMoveLog);
                logY += 18;
            }
            y += logAreaH + 8;

            // ── Buttons ────────────────────────────────────────────────────
            int btnH = 32;
            int gap = 6;

            if (GUI.Button(new Rect(margin, y, innerW, btnH), "New Game", styleAccentButton))
            {
                StartNewDraughtsGame(aiEnabled, aiDifficulty);
            }
            y += btnH + gap;

            bool hintDisabled = isAiThinking || gameResult != DraughtsGameResult.InProgress
                                || (aiEnabled && redToMove);
            GUI.enabled = !hintDisabled;
            if (GUI.Button(new Rect(margin, y, innerW, btnH), "Hint", styleButton))
            {
                ShowHint();
            }
            GUI.enabled = true;
            y += btnH + gap;

            if (GUI.Button(new Rect(margin, y, innerW, btnH), "Flip View", styleButton))
            {
                viewFlipped = !viewFlipped;
                // Rebuild overlays for flipped view
                ClearBoardOverlays();
                BuildBoardOverlays();
                ClearPieces();
                pieceObjects = new GameObject[8, 8];
                for (int row2 = 0; row2 < 8; row2++)
                    for (int col2 = 0; col2 < 8; col2++)
                        if (!board[row2, col2].isEmpty)
                            SpawnPiece(col2, row2);
                RefreshHighlights();
            }
            y += btnH + gap;

            if (GUI.Button(new Rect(margin, y, innerW, btnH), "Menu", styleButton))
            {
                DeactivateDraughts();
                ChessBoard chessBoard = FindAnyObjectByType<ChessBoard>();
                if (chessBoard != null)
                    chessBoard.OpenMainMenu();
            }
            y += btnH + gap;

            // ── AI difficulty display ──────────────────────────────────────
            if (aiEnabled)
            {
                string[] diffNames = { "Easy", "Medium", "Hard" };
                string diffStr = aiDifficulty >= 0 && aiDifficulty < 3 ? diffNames[aiDifficulty] : "?";
                GUI.Label(new Rect(margin, y, innerW, 18),
                    $"AI: {diffStr}{(isAiThinking ? " (thinking…)" : "")}", styleSubtitle);
            }
            else
            {
                GUI.Label(new Rect(margin, y, innerW, 18), "Mode: 2-Player", styleSubtitle);
            }
        }

        private string GetStatusText()
        {
            switch (gameResult)
            {
                case DraughtsGameResult.RedWins:  return "Red Wins! 🔴";
                case DraughtsGameResult.BlackWins: return "Black Wins! ⚫";
                case DraughtsGameResult.Draw:      return "Draw (40-move rule)";
                default:
                    if (isAiThinking) return "AI thinking…";
                    if (midJump) return "Must continue jump!";
                    return redToMove ? "Red to move" : "Black to move";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Utility
        // ─────────────────────────────────────────────────────────────────────

        private static void DisableCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        private static void SetMaterialColor(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            // Create a unique material instance so pieces don't share one material
            rend.material = new Material(rend.sharedMaterial != null
                ? rend.sharedMaterial
                : new Material(Shader.Find("Universal Render Pipeline/Lit")));
            rend.material.color = color;

            // URP also uses _BaseColor
            if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", color);
        }
    }
}
