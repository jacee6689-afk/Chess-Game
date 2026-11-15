using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using ChessGame;

public enum AppScreen
{
    MainMenu,
    Gameplay,
    Paused,
    Settings,
    Leaderboard,
    MatchSetup,
    OnlineMenu,
    QuitConfirm
}

/// <summary>
/// Master controller for the realistic 3D chess game.
/// Features Main Menu, Pause Menu, Settings Menu, Persistent Leaderboard, Quit confirmation,
/// dynamically framed 3D camera, piece animations, AI opponent, procedural audio, and chess clocks.
/// </summary>
public class ChessBoard : MonoBehaviour
{
    private const int Size = 8;
    public bool IsOnlineMatch => gameMode == GameMode.OnlineTwoPlayer;

#pragma warning disable CS0649
    [Header("Chess Set Prefabs")]
    [SerializeField] private GameObject boardPrefab;
    [SerializeField] private GameObject whitePawnPrefab, whiteKnightPrefab, whiteBishopPrefab, whiteRookPrefab, whiteQueenPrefab, whiteKingPrefab;
    [SerializeField] private GameObject blackPawnPrefab, blackKnightPrefab, blackBishopPrefab, blackRookPrefab, blackQueenPrefab, blackKingPrefab;
#pragma warning restore CS0649

    [Header("Tabletop Environment")]
    [SerializeField] private ChessEnvironment.Settings environmentSettings = new ChessEnvironment.Settings();

    // Screen state machine
    private AppScreen currentScreen = AppScreen.MainMenu;
    private AppScreen previousScreen = AppScreen.MainMenu;

    // Board state
    private Piece[,] board = new Piece[Size, Size];
    private ChessPieceVisual[,] pieceVisuals = new ChessPieceVisual[Size, Size];
    private GameObject[,] tileObjects = new GameObject[Size, Size];
    private MeshRenderer[,] tileQuadRenderers = new MeshRenderer[Size, Size];
    private MeshRenderer[,] tileDotRenderers = new MeshRenderer[Size, Size];
    private MeshRenderer[,] tileRingRenderers = new MeshRenderer[Size, Size];

    // Graveyard trays for captured pieces
    private readonly List<GameObject> whiteCapturedPieces = new List<GameObject>();
    private readonly List<GameObject> blackCapturedPieces = new List<GameObject>();
    private GameObject capturedTraysRoot;
    private GameObject coordsParent;
    private GameObject environmentRoot;

    // History & snapshots
    private readonly List<Move> moveHistory = new List<Move>();
    private readonly List<BoardSnapshot> snapshots = new List<BoardSnapshot>();
    private readonly List<string> positionHashes = new List<string>();

    // Game mode & settings
    private GameMode gameMode = GameMode.PlayerVsAI;
    private AIDifficulty aiDifficulty = AIDifficulty.Medium;
    private PlayerColor humanColor = PlayerColor.White;
    private TimeControlType timeControl = TimeControlType.FiveMinutes;

    // Active state
    private bool whiteToMove = true;
    private bool isAiThinking = false;
    private bool isPieceAnimating = false;
    private bool promotionPending = false;
    private Move pendingPromotionMove;
    private GameResult gameResult = GameResult.InProgress;
    private string statusMessage = "White to move";
    private bool matchStatsRecorded = false;
    private bool onlineMovePending;
    private ChessOnlineManager onlineManager;

    // Clocks
    private float whiteClockSeconds = 300f;
    private float blackClockSeconds = 300f;
    private bool clockActive = true;
    private float lastTickSecond = -1f;

    // Selection & dragging
    private Vector2Int selectedSquare = new Vector2Int(-1, -1);
    private Vector2Int hoveredSquare = new Vector2Int(-1, -1);
    private List<Move> currentLegalMoves = new List<Move>();
    private Vector2Int enPassantTarget = new Vector2Int(-1, -1);
    private int halfmoveClock = 0;
    private Move lastMoveMade;
    private Vector2Int hintFrom = new Vector2Int(-1, -1);
    private Vector2Int hintTo = new Vector2Int(-1, -1);

    private bool isDraggingPiece = false;
    private ChessPieceVisual draggedVisual = null;
    private Plane boardDragPlane;

    // Board geometry & scale
    private GameObject boardInstance;
    private Bounds playSurfaceBounds;
    private float squareWidth;
    private float squareDepth;
    private float boardSpan;

    // Camera system
    private Camera gameCamera;
    private Vector3 targetCameraPos;
    private Quaternion targetCameraRot;
    private Vector3 cameraVelocity;
    private bool isTopDownView = false;
    private bool isFlippedView = false;
    private bool isOrbitingCamera = false;
    private Vector3 orbitCenter;
    private float orbitDistance;
    private float defaultOrbitDistance;
    private float minOrbitDistance;
    private float maxOrbitDistance;
    private float orbitYaw = 0f;
    private float orbitPitch = 44f;

    // Materials
    private Material matTileSelect;
    private Material matTileMoveDot;
    private Material matTileCaptureRing;
    private Material matTileLastMove;
    private Material matTileCheck;
    private Material matTileHint;
    private Material fallbackLightMat;
    private Material fallbackDarkMat;
    private Material fallbackWhitePieceMat;
    private Material fallbackBlackPieceMat;

    // UI Styles & state
    private bool sidebarCollapsed = false;
    private Vector2 moveHistoryScroll = Vector2.zero;
    private Vector2 leaderboardScroll = Vector2.zero;
    private int leaderboardView;
    private Texture2D texDarkGlass;
    private Texture2D texCardBg;
    private Texture2D texButtonNormal;
    private Texture2D texButtonHover;
    private Texture2D texButtonActive;
    private Texture2D texAccent;
    private Texture2D texGoldBorder;
    private Texture2D texSliderBar;
    private GUIStyle styleTitle;
    private GUIStyle styleSubtitle;
    private GUIStyle styleCardHeader;
    private GUIStyle styleClock;
    private GUIStyle styleClockWarning;
    private GUIStyle styleButton;
    private GUIStyle styleAccentButton;
    private GUIStyle styleSmallButton;
    private GUIStyle styleMoveLog;
    private GUIStyle styleModalTitle;
    private GUIStyle styleModalText;
    private GUIStyle styleMainMenuTitle;
    private GUIStyle styleMainMenuSubtitle;
    private GUIStyle styleMainMenuButton;
    private GUIStyle styleMainMenuAccentButton;
    private GUIStyle styleMainMenuSmallButton;

    private void Awake()
    {
        gameCamera = Camera.main;
        if (gameCamera == null) gameCamera = FindAnyObjectByType<Camera>();

        // Ensure audio manager exists
        if (ChessAudio.Instance == null)
        {
            GameObject audioObj = new GameObject("ChessAudio");
            audioObj.AddComponent<ChessAudio>();
        }

        ChessSettings.Load();
        ChessStats.Load();
        ChessSettings.OnSettingsChanged += ApplySettingsChanges;

        onlineManager = GetComponent<ChessOnlineManager>();
        if (onlineManager == null) onlineManager = gameObject.AddComponent<ChessOnlineManager>();
        onlineManager.Initialize(this);

        CreateHighlightMaterials();
        BuildBoard();
        SetupCameraPositions();
        InitializeBoardPieces();

        // Start in Main Menu
        currentScreen = AppScreen.MainMenu;
    }

    private void Start()
    {
        InitializeUIStyles();
        ApplySettingsChanges();
    }

    private void OnDestroy()
    {
        ChessSettings.OnSettingsChanged -= ApplySettingsChanges;
    }

    private void ApplySettingsChanges()
    {
        if (coordsParent != null)
        {
            coordsParent.SetActive(ChessSettings.ShowCoordinates);
        }
        RefreshHighlights();
    }

    #region Board Initialization & Geometry

    private void BuildBoard()
    {
        playSurfaceBounds = new Bounds(new Vector3(4f, 0f, 4f), new Vector3(8f, 0.05f, 8f));
        squareWidth = 1f;
        squareDepth = 1f;

        if (boardPrefab != null)
        {
            boardInstance = Instantiate(boardPrefab, transform, false);
            boardInstance.name = "Chess Board (3D Model)";
            Transform surface = FindChildRecursive(boardInstance.transform, "Chess Board Play Surface");
            Renderer surfaceRenderer = surface != null ? surface.GetComponent<Renderer>() : null;
            if (surfaceRenderer != null)
            {
                playSurfaceBounds = surfaceRenderer.bounds;
                squareWidth = playSurfaceBounds.size.x / Size;
                squareDepth = playSurfaceBounds.size.z / Size;
            }
        }
        else
        {
            CreateFallbackBoard();
        }

        boardSpan = Mathf.Max(playSurfaceBounds.size.x, playSurfaceBounds.size.z);
        orbitCenter = playSurfaceBounds.center;

        defaultOrbitDistance = boardSpan * 1.55f;
        orbitDistance = defaultOrbitDistance;
        minOrbitDistance = boardSpan * 0.75f;
        maxOrbitDistance = boardSpan * 2.80f;

        boardDragPlane = new Plane(Vector3.up, new Vector3(0, playSurfaceBounds.max.y + squareWidth * 0.45f, 0));

        // Create 64 tile colliders and highlight meshes
        float surfaceY = playSurfaceBounds.max.y;
        float colliderHeight = Mathf.Max(0.5f, playSurfaceBounds.size.y + squareWidth * 0.2f);
        float highlightOffsetY = squareWidth * 0.012f;

        GameObject tilesParent = new GameObject("Board Tiles");
        tilesParent.transform.SetParent(transform, false);

        int tileLayer = LayerMask.NameToLayer("Tile");
        if (tileLayer < 0) tileLayer = 0;

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Vector3 center = SquareWorldPosition(x, y);

                GameObject tile = new GameObject($"Tile_{SquareName(new Vector2Int(x, y))}");
                tile.transform.SetParent(tilesParent.transform, true);
                tile.transform.position = new Vector3(center.x, surfaceY, center.z);
                tile.layer = tileLayer;

                BoxCollider box = tile.AddComponent<BoxCollider>();
                box.size = new Vector3(squareWidth, colliderHeight, squareDepth);
                box.center = new Vector3(0, -colliderHeight * 0.45f, 0);

                // Highlight Quad
                GameObject quadObj = new GameObject("HighlightQuad");
                quadObj.transform.SetParent(tile.transform, false);
                quadObj.transform.localPosition = new Vector3(0, highlightOffsetY, 0);
                quadObj.layer = LayerMask.NameToLayer("Ignore Raycast");
                MeshFilter qf = quadObj.AddComponent<MeshFilter>();
                qf.sharedMesh = CreateSquareQuadMesh(squareWidth * 0.98f, squareDepth * 0.98f);
                MeshRenderer qr = quadObj.AddComponent<MeshRenderer>();
                qr.enabled = false;
                tileQuadRenderers[x, y] = qr;

                // Move Dot
                GameObject dotObj = new GameObject("MoveDot");
                dotObj.transform.SetParent(tile.transform, false);
                dotObj.transform.localPosition = new Vector3(0, highlightOffsetY + squareWidth * 0.002f, 0);
                dotObj.layer = LayerMask.NameToLayer("Ignore Raycast");
                MeshFilter df = dotObj.AddComponent<MeshFilter>();
                df.sharedMesh = CreateCircleDiscMesh(squareWidth * 0.17f, 16);
                MeshRenderer dr = dotObj.AddComponent<MeshRenderer>();
                dr.sharedMaterial = matTileMoveDot;
                dr.enabled = false;
                tileDotRenderers[x, y] = dr;

                // Capture Ring
                GameObject ringObj = new GameObject("CaptureRing");
                ringObj.transform.SetParent(tile.transform, false);
                ringObj.transform.localPosition = new Vector3(0, highlightOffsetY + squareWidth * 0.004f, 0);
                ringObj.layer = LayerMask.NameToLayer("Ignore Raycast");
                MeshFilter rf = ringObj.AddComponent<MeshFilter>();
                rf.sharedMesh = CreateRingMesh(squareWidth * 0.36f, squareWidth * 0.47f, 24);
                MeshRenderer rr = ringObj.AddComponent<MeshRenderer>();
                rr.sharedMaterial = matTileCaptureRing;
                rr.enabled = false;
                tileRingRenderers[x, y] = rr;

                tileObjects[x, y] = tile;
            }
        }

        CreateBoardCoordinates();

        capturedTraysRoot = new GameObject("Captured Pieces Trays");
        capturedTraysRoot.transform.SetParent(transform, false);

        environmentRoot = ChessEnvironment.Build(transform, playSurfaceBounds, squareWidth, environmentSettings);
    }

    private void CreateBoardCoordinates()
    {
        coordsParent = new GameObject("Board Coordinates");
        coordsParent.transform.SetParent(transform, false);

        float yPos = playSurfaceBounds.max.y + squareWidth * 0.015f;
        Color labelColor = new Color(0.88f, 0.82f, 0.68f, 0.88f);

        for (int y = 0; y < Size; y++)
        {
            float z = playSurfaceBounds.min.z + (y + 0.5f) * squareDepth;
            string rankStr = (y + 1).ToString();

            CreateCoordinateLabel(coordsParent.transform, rankStr,
                new Vector3(playSurfaceBounds.min.x - squareWidth * 0.30f, yPos, z),
                labelColor);

            CreateCoordinateLabel(coordsParent.transform, rankStr,
                new Vector3(playSurfaceBounds.max.x + squareWidth * 0.30f, yPos, z),
                labelColor);
        }

        for (int x = 0; x < Size; x++)
        {
            float wx = playSurfaceBounds.min.x + (x + 0.5f) * squareWidth;
            string fileStr = ((char)('a' + x)).ToString();

            CreateCoordinateLabel(coordsParent.transform, fileStr,
                new Vector3(wx, yPos, playSurfaceBounds.min.z - squareDepth * 0.30f),
                labelColor);

            CreateCoordinateLabel(coordsParent.transform, fileStr,
                new Vector3(wx, yPos, playSurfaceBounds.max.z + squareDepth * 0.30f),
                labelColor);
        }

        coordsParent.SetActive(ChessSettings.ShowCoordinates);
    }

    private void CreateCoordinateLabel(Transform parent, string text, Vector3 worldPos, Color color)
    {
        GameObject obj = new GameObject($"Coord_{text}");
        obj.transform.SetParent(parent, true);
        obj.transform.position = worldPos;
        obj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        TextMesh tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 28;
        tm.characterSize = squareWidth * 0.055f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
    }

    private void CreateFallbackBoard()
    {
        fallbackLightMat = MakeLitMaterial(new Color(0.85f, 0.82f, 0.72f), 0.3f);
        fallbackDarkMat = MakeLitMaterial(new Color(0.25f, 0.38f, 0.42f), 0.3f);
        // Warm bone/antique-ivory — clearly distinct from the pure-white board squares
        fallbackWhitePieceMat = MakeLitMaterial(new Color(0.94f, 0.84f, 0.64f), 0.72f);
        fallbackBlackPieceMat = MakeLitMaterial(new Color(0.14f, 0.18f, 0.22f), 0.6f);

        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Fallback Board Frame";
        frame.transform.SetParent(transform, false);
        frame.transform.localPosition = new Vector3(3.5f, -0.15f, 3.5f);
        frame.transform.localScale = new Vector3(8.6f, 0.3f, 8.6f);
        frame.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial(new Color(0.35f, 0.22f, 0.12f), 0.4f);
        frame.layer = LayerMask.NameToLayer("Ignore Raycast");

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = $"FallbackTile_{x}_{y}";
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = new Vector3(x, 0.01f, y);
                tile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                tile.GetComponent<Renderer>().sharedMaterial = ((x + y) % 2 == 0) ? fallbackLightMat : fallbackDarkMat;
                Destroy(tile.GetComponent<Collider>());
            }
        }
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    public Vector3 SquareWorldPosition(int x, int y)
    {
        return new Vector3(
            playSurfaceBounds.min.x + (x + 0.5f) * squareWidth,
            playSurfaceBounds.max.y + 0.025f,
            playSurfaceBounds.min.z + (y + 0.5f) * squareDepth
        );
    }

    #endregion

    #region Materials & Meshes

    private void CreateHighlightMaterials()
    {
        matTileSelect = MakeUnlitTransparentMaterial(new Color(0.98f, 0.78f, 0.22f, 0.55f));
        matTileMoveDot = MakeUnlitTransparentMaterial(new Color(0.38f, 0.88f, 0.48f, 0.75f));
        matTileCaptureRing = MakeUnlitTransparentMaterial(new Color(0.96f, 0.40f, 0.32f, 0.85f));
        matTileLastMove = MakeUnlitTransparentMaterial(new Color(0.85f, 0.82f, 0.30f, 0.35f));
        matTileCheck = MakeUnlitTransparentMaterial(new Color(0.95f, 0.15f, 0.15f, 0.65f));
        matTileHint = MakeUnlitTransparentMaterial(new Color(0.20f, 0.75f, 0.98f, 0.60f));
    }

    private Material MakeLitMaterial(Color color, float smoothness = 0.5f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private Material MakeUnlitTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material mat = new Material(shader) { color = color };

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    private Mesh CreateSquareQuadMesh(float width, float depth)
    {
        Mesh mesh = new Mesh { name = "HighlightSquareMesh" };
        float hw = width * 0.5f;
        float hd = depth * 0.5f;
        mesh.vertices = new Vector3[]
        {
            new Vector3(-hw, 0f, -hd),
            new Vector3(-hw, 0f,  hd),
            new Vector3( hw, 0f, -hd),
            new Vector3( hw, 0f,  hd)
        };
        mesh.triangles = new int[] { 0, 1, 2, 1, 3, 2 };
        mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1) };
        return mesh;
    }

    private Mesh CreateCircleDiscMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh { name = "MoveDotDisc" };
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        Vector3[] normals = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float vx = Mathf.Cos(angle) * radius;
            float vz = Mathf.Sin(angle) * radius;
            vertices[i + 1] = new Vector3(vx, 0, vz);
            normals[i + 1] = Vector3.up;
            uv[i + 1] = new Vector2(vx / (radius * 2f) + 0.5f, vz / (radius * 2f) + 0.5f);

            int next = (i + 1) % segments;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.uv = uv;
        return mesh;
    }

    private Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments)
    {
        Mesh mesh = new Mesh { name = "CaptureRingMesh" };
        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];
        Vector3[] normals = new Vector3[segments * 2];

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(cos * innerRadius, 0, sin * innerRadius);
            vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0, sin * outerRadius);
            normals[i * 2] = Vector3.up;
            normals[i * 2 + 1] = Vector3.up;

            int next = (i + 1) % segments;
            triangles[i * 6] = i * 2;
            triangles[i * 6 + 1] = i * 2 + 1;
            triangles[i * 6 + 2] = next * 2;

            triangles[i * 6 + 3] = next * 2;
            triangles[i * 6 + 4] = i * 2 + 1;
            triangles[i * 6 + 5] = next * 2 + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        return mesh;
    }

    #endregion

    #region Camera Setup & Controls

    private void SetupCameraPositions()
    {
        if (gameCamera == null) return;
        gameCamera.fieldOfView = 48f;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.06f, 0.08f, 0.11f);

        isTopDownView = ChessSettings.DefaultTopDown;
        ResetCameraView(instant: true);
    }

    private void ResetCameraView(bool instant = false)
    {
        float lateralShift = (currentScreen == AppScreen.Gameplay && !sidebarCollapsed) ? (boardSpan * 0.12f) : 0f;
        Vector3 focusPoint = orbitCenter - Vector3.right * (isFlippedView ? -lateralShift : lateralShift);

        if (isTopDownView)
        {
            float topDist = boardSpan * 1.55f;
            targetCameraPos = focusPoint + new Vector3(0f, topDist, 0.01f);
            targetCameraRot = Quaternion.Euler(89.9f, isFlippedView ? 180f : 0f, 0f);
        }
        else
        {
            orbitYaw = isFlippedView ? 180f : 0f;
            orbitPitch = 44f;
            orbitDistance = defaultOrbitDistance;

            Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            targetCameraPos = focusPoint + rot * (Vector3.back * orbitDistance);
            targetCameraRot = Quaternion.LookRotation(focusPoint - targetCameraPos, Vector3.up);
        }

        if (instant && gameCamera != null)
        {
            gameCamera.transform.position = targetCameraPos;
            gameCamera.transform.rotation = targetCameraRot;
        }
    }

    private void UpdateCamera()
    {
        if (gameCamera == null) return;

        // Cinematic slow showcase orbit during Main Menu
        if (currentScreen == AppScreen.MainMenu)
        {
            orbitYaw += 5.5f * Time.deltaTime;
            orbitPitch = 38f;
            orbitDistance = defaultOrbitDistance * 1.05f;

            Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            targetCameraPos = orbitCenter + rot * (Vector3.back * orbitDistance);
            targetCameraRot = Quaternion.LookRotation(orbitCenter - targetCameraPos, Vector3.up);

            gameCamera.transform.position = Vector3.SmoothDamp(gameCamera.transform.position, targetCameraPos, ref cameraVelocity, 0.22f);
            gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation, targetCameraRot, Time.deltaTime * 10f);
            return;
        }

        // Right mouse button holds to orbit camera in 3D
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                isOrbitingCamera = true;
            }
            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                isOrbitingCamera = false;
            }

            if (isOrbitingCamera)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                orbitYaw += delta.x * 0.28f;
                orbitPitch = Mathf.Clamp(orbitPitch - delta.y * 0.22f, 18f, 85f);

                float lateralShift = (currentScreen == AppScreen.Gameplay && !sidebarCollapsed) ? (boardSpan * 0.12f) : 0f;
                Vector3 focusPoint = orbitCenter - Vector3.right * (isFlippedView ? -lateralShift : lateralShift);

                Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                targetCameraPos = focusPoint + rot * (Vector3.back * orbitDistance);
                targetCameraRot = Quaternion.LookRotation(focusPoint - targetCameraPos, Vector3.up);
            }

            // Scroll wheel zooms
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                orbitDistance = Mathf.Clamp(orbitDistance - Mathf.Sign(scroll) * (boardSpan * 0.12f), minOrbitDistance, maxOrbitDistance);
                if (!isTopDownView)
                {
                    float lateralShift = (currentScreen == AppScreen.Gameplay && !sidebarCollapsed) ? (boardSpan * 0.12f) : 0f;
                    Vector3 focusPoint = orbitCenter - Vector3.right * (isFlippedView ? -lateralShift : lateralShift);

                    Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                    targetCameraPos = focusPoint + rot * (Vector3.back * orbitDistance);
                    targetCameraRot = Quaternion.LookRotation(focusPoint - targetCameraPos, Vector3.up);
                }
            }
        }

        gameCamera.transform.position = Vector3.SmoothDamp(gameCamera.transform.position, targetCameraPos, ref cameraVelocity, 0.18f);
        gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation, targetCameraRot, Time.deltaTime * 12f);
    }

    public void ToggleBoardFlip()
    {
        isFlippedView = !isFlippedView;
        ResetCameraView();
    }

    public void ToggleTopDownView()
    {
        isTopDownView = !isTopDownView;
        ResetCameraView();
    }

    public void ToggleSidebar()
    {
        sidebarCollapsed = !sidebarCollapsed;
        ResetCameraView();
    }

    #endregion

    #region Game Lifecycle, Clocks & State

    private void InitializeBoardPieces()
    {
        PieceType[] backRank = {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (int x = 0; x < Size; x++)
        {
            board[x, 0] = new Piece(backRank[x], true);
            board[x, 1] = new Piece(PieceType.Pawn, true);
            board[x, 6] = new Piece(PieceType.Pawn, false);
            board[x, 7] = new Piece(backRank[x], false);
        }

        SpawnAllPieceVisuals();
    }

    public void StartNewGame()
    {
        StopAllCoroutines();
        isAiThinking = false;
        isPieceAnimating = false;
        isDraggingPiece = false;
        draggedVisual = null;
        promotionPending = false;
        matchStatsRecorded = false;
        hintFrom = new Vector2Int(-1, -1);
        hintTo = new Vector2Int(-1, -1);

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (pieceVisuals[x, y] != null)
                {
                    Destroy(pieceVisuals[x, y].gameObject);
                    pieceVisuals[x, y] = null;
                }
                board[x, y] = new Piece();
            }
        }

        foreach (var obj in whiteCapturedPieces) if (obj != null) Destroy(obj);
        foreach (var obj in blackCapturedPieces) if (obj != null) Destroy(obj);
        whiteCapturedPieces.Clear();
        blackCapturedPieces.Clear();

        moveHistory.Clear();
        snapshots.Clear();
        positionHashes.Clear();

        PieceType[] backRank = {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (int x = 0; x < Size; x++)
        {
            board[x, 0] = new Piece(backRank[x], true);
            board[x, 1] = new Piece(PieceType.Pawn, true);
            board[x, 6] = new Piece(PieceType.Pawn, false);
            board[x, 7] = new Piece(backRank[x], false);
        }

        whiteToMove = true;
        gameResult = GameResult.InProgress;
        selectedSquare = new Vector2Int(-1, -1);
        hoveredSquare = new Vector2Int(-1, -1);
        currentLegalMoves.Clear();
        enPassantTarget = new Vector2Int(-1, -1);
        halfmoveClock = 0;
        lastMoveMade = default;

        whiteClockSeconds = GetInitialSeconds(timeControl);
        blackClockSeconds = whiteClockSeconds;
        clockActive = (timeControl != TimeControlType.Unlimited);
        lastTickSecond = -1f;

        if (gameMode == GameMode.PlayerVsAI)
        {
            bool playAsBlack = (humanColor == PlayerColor.Black) || (humanColor == PlayerColor.Random && UnityEngine.Random.value > 0.5f);
            isFlippedView = playAsBlack;
        }
        else
        {
            isFlippedView = false;
        }

        currentScreen = AppScreen.Gameplay;
        ResetCameraView();

        SpawnAllPieceVisuals();
        StorePositionHash();
        UpdateStatusMessage();
        RefreshHighlights();

        ChessAudio.Instance?.PlayMove();
        CheckTriggerAIMove();
    }

    public void OpenOnlineMenu()
    {
        previousScreen = currentScreen;
        currentScreen = AppScreen.OnlineMenu;
        onlineManager.OpenMenu();
    }

    public void StartOnlineGame(bool localPlayerIsWhite)
    {
        gameMode = GameMode.OnlineTwoPlayer;
        timeControl = TimeControlType.Unlimited;
        isFlippedView = !localPlayerIsWhite;
        onlineMovePending = false;
        StartNewGame();
    }

    public bool TryApplyOnlineMove(Move requestedMove, bool movingWhite)
    {
        if (gameMode != GameMode.OnlineTwoPlayer || gameResult != GameResult.InProgress ||
            whiteToMove != movingWhite || !Inside(requestedMove.from.x, requestedMove.from.y) ||
            !Inside(requestedMove.to.x, requestedMove.to.y) ||
            board[requestedMove.from.x, requestedMove.from.y].IsEmpty ||
            board[requestedMove.from.x, requestedMove.from.y].white != movingWhite)
        {
            return false;
        }

        List<Move> legalMoves = LegalMovesForSquare(board, requestedMove.from, enPassantTarget);
        for (int i = 0; i < legalMoves.Count; i++)
        {
            Move legalMove = legalMoves[i];
            if (legalMove.to != requestedMove.to) continue;

            bool isPromotion = board[requestedMove.from.x, requestedMove.from.y].type == PieceType.Pawn &&
                (requestedMove.to.y == 0 || requestedMove.to.y == 7);
            if (isPromotion && legalMove.promotion != requestedMove.promotion) continue;
            if (!isPromotion && requestedMove.promotion != PieceType.None) continue;

            onlineMovePending = false;
            ClearSelection();
            ApplyMoveToGame(legalMove);
            return true;
        }

        return false;
    }

    public string ExportOnlineState()
    {
        char[] cells = new char[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Piece piece = board[x, y];
                int encoded = (int)piece.type | (piece.white ? 8 : 0) | (piece.moved ? 16 : 0);
                cells[y * Size + x] = (char)('A' + encoded);
            }
        }

        return new string(cells) + "|" +
            (whiteToMove ? "1" : "0") + "|" +
            enPassantTarget.x.ToString(CultureInfo.InvariantCulture) + "|" +
            enPassantTarget.y.ToString(CultureInfo.InvariantCulture) + "|" +
            halfmoveClock.ToString(CultureInfo.InvariantCulture) + "|" +
            whiteClockSeconds.ToString("R", CultureInfo.InvariantCulture) + "|" +
            blackClockSeconds.ToString("R", CultureInfo.InvariantCulture) + "|" +
            ((int)gameResult).ToString(CultureInfo.InvariantCulture) + "|" +
            string.Join(",", moveHistory.ConvertAll(move => move.notation));
    }

    public bool ImportOnlineState(string state)
    {
        if (string.IsNullOrEmpty(state) || state.Length < Size * Size + 2 ||
            state[Size * Size] != '|')
        {
            return false;
        }

        string[] fields = state.Substring(Size * Size + 1).Split('|');
        if (fields.Length != 8 ||
            (fields[0] != "0" && fields[0] != "1") ||
            !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int epX) ||
            !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int epY) ||
            !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int halfmoves) ||
            !float.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float whiteTime) ||
            !float.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float blackTime) ||
            !int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int resultValue) ||
            epX < -1 || epX >= Size || epY < -1 || epY >= Size || halfmoves < 0 ||
            whiteTime < 0f || blackTime < 0f || !Enum.IsDefined(typeof(GameResult), resultValue))
        {
            return false;
        }

        Piece[,] restoredBoard = new Piece[Size, Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int encoded = state[y * Size + x] - 'A';
                int pieceType = encoded & 7;
                if (encoded < 0 || encoded > 31 || pieceType > (int)PieceType.King) return false;
                restoredBoard[x, y] = new Piece((PieceType)pieceType, (encoded & 8) != 0, (encoded & 16) != 0);
            }
        }

        List<Move> restoredMoveHistory = new List<Move>();
        if (!string.IsNullOrEmpty(fields[7]))
        {
            string[] notations = fields[7].Split(',');
            if (notations.Length > 2000) return false;
            for (int i = 0; i < notations.Length; i++)
            {
                if (string.IsNullOrEmpty(notations[i]) || notations[i].Length > 16)
                    return false;
                restoredMoveHistory.Add(new Move { notation = notations[i] });
            }
        }

        StopAllCoroutines();
        isAiThinking = false;
        isPieceAnimating = false;
        isDraggingPiece = false;
        draggedVisual = null;
        promotionPending = false;
        onlineMovePending = false;
        board = restoredBoard;
        whiteToMove = fields[0] == "1";
        enPassantTarget = new Vector2Int(epX, epY);
        halfmoveClock = halfmoves;
        whiteClockSeconds = whiteTime;
        blackClockSeconds = blackTime;
        gameResult = (GameResult)resultValue;
        clockActive = timeControl != TimeControlType.Unlimited;
        selectedSquare = new Vector2Int(-1, -1);
        hoveredSquare = new Vector2Int(-1, -1);
        currentLegalMoves.Clear();
        moveHistory.Clear();
        moveHistory.AddRange(restoredMoveHistory);
        snapshots.Clear();
        positionHashes.Clear();
        lastMoveMade = default;
        currentScreen = AppScreen.Gameplay;
        ResyncAllVisuals();
        StorePositionHash();
        UpdateStatusMessage();
        RefreshHighlights();
        if (gameResult != GameResult.InProgress)
            RecordMatchEnd();
        return true;
    }

    public List<string> GetOnlineMoveNotations()
    {
        return moveHistory.ConvertAll(move => move.notation);
    }

    public string GetOnlineResultCode()
    {
        switch (gameResult)
        {
            case GameResult.WhiteCheckmate:
            case GameResult.BlackResign:
            case GameResult.BlackTimeout:
                return "white_win";
            case GameResult.BlackCheckmate:
            case GameResult.WhiteResign:
            case GameResult.WhiteTimeout:
                return "black_win";
            case GameResult.Stalemate:
            case GameResult.FiftyMoveDraw:
            case GameResult.ThreefoldDraw:
            case GameResult.InsufficientMaterialDraw:
                return "draw";
            default:
                return null;
        }
    }

    public string GetOnlineResultReason()
    {
        switch (gameResult)
        {
            case GameResult.WhiteCheckmate:
            case GameResult.BlackCheckmate:
                return "checkmate";
            case GameResult.WhiteResign:
            case GameResult.BlackResign:
                return "resignation";
            case GameResult.WhiteTimeout:
            case GameResult.BlackTimeout:
                return "timeout";
            case GameResult.Stalemate:
                return "stalemate";
            case GameResult.FiftyMoveDraw:
                return "fifty_move";
            case GameResult.ThreefoldDraw:
                return "threefold";
            case GameResult.InsufficientMaterialDraw:
                return "insufficient_material";
            default:
                return null;
        }
    }

    public void SetOnlineMovePending(bool pending)
    {
        onlineMovePending = pending;
    }

    private float GetInitialSeconds(TimeControlType tc)
    {
        switch (tc)
        {
            case TimeControlType.OneMinute: return 60f;
            case TimeControlType.ThreeMinutes: return 180f;
            case TimeControlType.FiveMinutes: return 300f;
            case TimeControlType.TenMinutes: return 600f;
            case TimeControlType.FifteenMinutes: return 900f;
            default: return 99999f;
        }
    }

    private void UpdateClock()
    {
        if (currentScreen != AppScreen.Gameplay) return;
        if (gameResult != GameResult.InProgress || !clockActive || isPieceAnimating || promotionPending) return;
        if (gameMode == GameMode.OnlineTwoPlayer && !onlineManager.IsHost) return;

        if (whiteToMove)
        {
            whiteClockSeconds -= Time.deltaTime;
            if (whiteClockSeconds <= 0f)
            {
                whiteClockSeconds = 0f;
                OnTimeOut(true);
            }
            else if (whiteClockSeconds <= 20f && Mathf.Floor(whiteClockSeconds) != lastTickSecond)
            {
                lastTickSecond = Mathf.Floor(whiteClockSeconds);
                ChessAudio.Instance?.PlayTick();
            }
        }
        else
        {
            blackClockSeconds -= Time.deltaTime;
            if (blackClockSeconds <= 0f)
            {
                blackClockSeconds = 0f;
                OnTimeOut(false);
            }
            else if (blackClockSeconds <= 20f && Mathf.Floor(blackClockSeconds) != lastTickSecond)
            {
                lastTickSecond = Mathf.Floor(blackClockSeconds);
                ChessAudio.Instance?.PlayTick();
            }
        }
    }

    private void OnTimeOut(bool whiteTimedOut)
    {
        bool opponentHasMaterial = HasMatingMaterial(!whiteTimedOut);
        if (opponentHasMaterial)
        {
            gameResult = whiteTimedOut ? GameResult.WhiteTimeout : GameResult.BlackTimeout;
            statusMessage = whiteTimedOut ? "Black wins on time!" : "White wins on time!";
            if (gameMode == GameMode.PlayerVsAI)
            {
                bool playerWon = (whiteTimedOut && isFlippedView) || (!whiteTimedOut && !isFlippedView);
                if (playerWon) ChessAudio.Instance?.PlayVictory(); else ChessAudio.Instance?.PlayDefeat();
            }
            else
            {
                ChessAudio.Instance?.PlayVictory();
            }
        }
        else
        {
            gameResult = GameResult.InsufficientMaterialDraw;
            statusMessage = "Draw — Time out vs Insufficient Material";
            ChessAudio.Instance?.PlayDraw();
        }

        RecordMatchEnd();
        if (gameMode == GameMode.OnlineTwoPlayer) onlineManager.BroadcastCurrentState();
    }

    private void RecordMatchEnd()
    {
        if (matchStatsRecorded) return;
        matchStatsRecorded = true;
        if (gameMode == GameMode.OnlineTwoPlayer)
        {
            onlineManager?.SubmitCompletedOnlineMatch(
                GetOnlineResultCode(),
                GetOnlineResultReason(),
                GetOnlineMoveNotations());
            return;
        }
        bool playerWasWhite = !isFlippedView;
        ChessStats.RecordMatch(gameResult, gameMode, aiDifficulty, playerWasWhite, moveHistory.Count);
    }

    #endregion

    #region Visual Pieces & Spawning

    private void SpawnAllPieceVisuals()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Piece p = board[x, y];
                if (!p.IsEmpty)
                {
                    pieceVisuals[x, y] = CreatePieceGameObject(p, x, y);
                }
            }
        }
    }

    private ChessPieceVisual CreatePieceGameObject(Piece p, int x, int y)
    {
        GameObject prefab = GetPiecePrefab(p);
        GameObject pieceObj;

        Quaternion rotation = p.white
            ? Quaternion.Euler(-90f, 0f, 0f)
            : Quaternion.AngleAxis(180f, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);

        if (prefab != null)
        {
            pieceObj = Instantiate(prefab, SquareWorldPosition(x, y), rotation, transform);
        }
        else
        {
            pieceObj = CreateProceduralPieceFallback(p, SquareWorldPosition(x, y), rotation);
        }

        // Runtime colour override: white pieces use warm antique-ivory so they are
        // clearly visible against both pure-white squares and dark squares.
        if (p.white)
        {
            Color whiteTint = new Color(0.94f, 0.84f, 0.64f, 1f); // warm bone / antique ivory
            foreach (Renderer rend in pieceObj.GetComponentsInChildren<Renderer>(true))
            {
                // Clone the material so we don't dirty the shared asset
                Material[] mats = rend.materials;
                for (int mi = 0; mi < mats.Length; mi++)
                {
                    mats[mi] = new Material(mats[mi]);
                    mats[mi].color = whiteTint;
                    if (mats[mi].HasProperty("_BaseColor")) mats[mi].SetColor("_BaseColor", whiteTint);
                    if (mats[mi].HasProperty("_Smoothness")) mats[mi].SetFloat("_Smoothness", 0.72f);
                }
                rend.materials = mats;
            }
        }

        pieceObj.name = $"{(p.white ? "White" : "Black")}_{p.type}_{SquareName(new Vector2Int(x, y))}";
        SetLayerRecursive(pieceObj.transform, LayerMask.NameToLayer("Ignore Raycast"));

        ChessPieceVisual visual = pieceObj.AddComponent<ChessPieceVisual>();
        visual.type = p.type;
        visual.isWhite = p.white;
        visual.gridPos = new Vector2Int(x, y);
        return visual;
    }

    private GameObject CreateProceduralPieceFallback(Piece p, Vector3 worldPos, Quaternion rot)
    {
        GameObject root = new GameObject();
        root.transform.position = worldPos;
        root.transform.rotation = rot;
        root.transform.SetParent(transform, true);

        Material mat = p.white ? fallbackWhitePieceMat : fallbackBlackPieceMat;
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.transform.SetParent(root.transform, false);
        cylinder.transform.localPosition = new Vector3(0, 0.45f, 0);
        cylinder.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
        cylinder.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(cylinder.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0, 0.95f, 0);
        head.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        head.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(head.GetComponent<Collider>());

        return root;
    }

    private GameObject GetPiecePrefab(Piece p)
    {
        if (p.white)
        {
            switch (p.type)
            {
                case PieceType.Pawn: return whitePawnPrefab;
                case PieceType.Knight: return whiteKnightPrefab;
                case PieceType.Bishop: return whiteBishopPrefab;
                case PieceType.Rook: return whiteRookPrefab;
                case PieceType.Queen: return whiteQueenPrefab;
                case PieceType.King: return whiteKingPrefab;
            }
        }
        else
        {
            switch (p.type)
            {
                case PieceType.Pawn: return blackPawnPrefab;
                case PieceType.Knight: return blackKnightPrefab;
                case PieceType.Bishop: return blackBishopPrefab;
                case PieceType.Rook: return blackRookPrefab;
                case PieceType.Queen: return blackQueenPrefab;
                case PieceType.King: return blackKingPrefab;
            }
        }
        return null;
    }

    private void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) SetLayerRecursive(root.GetChild(i), layer);
    }

    #endregion

    #region Input, Selection & Drag-and-Drop

    private void Update()
    {
        HandleKeyboardShortcuts();
        UpdateCamera();
        UpdateClock();

        if (currentScreen != AppScreen.Gameplay) return;
        if (gameResult != GameResult.InProgress || isAiThinking || isPieceAnimating || promotionPending) return;
        if (onlineMovePending) return;

        if (IsAITurn()) return;

        HandleSquareHoverAndInput();
    }

    private void HandleKeyboardShortcuts()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (currentScreen == AppScreen.Gameplay)
            {
                if (selectedSquare.x >= 0)
                {
                    ClearSelection();
                }
                else
                {
                    PauseGame();
                }
            }
            else if (currentScreen == AppScreen.Paused)
            {
                ResumeGame();
            }
            else if (currentScreen == AppScreen.Settings || currentScreen == AppScreen.Leaderboard || currentScreen == AppScreen.MatchSetup)
            {
                currentScreen = previousScreen;
            }
            else if (currentScreen == AppScreen.QuitConfirm)
            {
                currentScreen = previousScreen;
            }
            else if (currentScreen == AppScreen.MainMenu)
            {
                OpenQuitConfirm();
            }
        }
    }

    private bool IsAITurn()
    {
        if (gameMode == GameMode.LocalTwoPlayer) return false;
        if (gameMode == GameMode.AIVsAI) return true;
        if (gameMode == GameMode.OnlineTwoPlayer)
            return onlineManager == null || whiteToMove != onlineManager.LocalPlayerIsWhite;
        bool playerIsWhite = !isFlippedView;
        return whiteToMove != playerIsWhite;
    }

    private void HandleSquareHoverAndInput()
    {
        if (gameCamera == null || Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (!sidebarCollapsed && mousePos.x < 310 && (Screen.height - mousePos.y) < 560)
        {
            if (hoveredSquare.x >= 0)
            {
                hoveredSquare = new Vector2Int(-1, -1);
                RefreshHighlights();
            }
            return;
        }

        Ray ray = gameCamera.ScreenPointToRay(mousePos);

        int hitTile = -1;
        int tileLayerMask = 1 << LayerMask.NameToLayer("Tile");
        if (tileLayerMask == 0) tileLayerMask = LayerMask.GetMask("Tile", "Default");

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, tileLayerMask))
        {
            hitTile = LookupTileIndex(hit.transform.gameObject);
        }

        Vector2Int currentSquare = (hitTile >= 0) ? new Vector2Int(hitTile / Size, hitTile % Size) : new Vector2Int(-1, -1);

        if (currentSquare != hoveredSquare)
        {
            hoveredSquare = currentSquare;
            RefreshHighlights();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && !isOrbitingCamera)
        {
            if (hoveredSquare.x >= 0)
            {
                OnSquareClicked(hoveredSquare);
            }
        }

        if (Mouse.current.leftButton.isPressed && isDraggingPiece && draggedVisual != null)
        {
            if (boardDragPlane.Raycast(ray, out float enter))
            {
                Vector3 worldHover = ray.GetPoint(enter);
                draggedVisual.UpdateDragPosition(worldHover);
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDraggingPiece && draggedVisual != null)
        {
            EndPieceDrag(hoveredSquare);
        }

        if ((Mouse.current.rightButton.wasPressedThisFrame) && !isDraggingPiece)
        {
            ClearSelection();
        }
    }

    private void OnSquareClicked(Vector2Int square)
    {
        if (selectedSquare.x >= 0)
        {
            Move chosenMove = currentLegalMoves.Find(m => m.to == square);
            if (chosenMove.from == selectedSquare)
            {
                ExecutePlayerMove(chosenMove);
                return;
            }
        }

        Piece clickedPiece = board[square.x, square.y];
        if (!clickedPiece.IsEmpty && clickedPiece.white == whiteToMove)
        {
            selectedSquare = square;
            currentLegalMoves = LegalMovesForSquare(board, square, enPassantTarget);
            hintFrom = new Vector2Int(-1, -1);
            hintTo = new Vector2Int(-1, -1);

            draggedVisual = pieceVisuals[square.x, square.y];
            if (draggedVisual != null)
            {
                isDraggingPiece = true;
                Vector3 elevatedPos = draggedVisual.transform.position + Vector3.up * (squareWidth * 0.45f);
                draggedVisual.SetDrag(true, elevatedPos);
            }

            RefreshHighlights();
        }
        else
        {
            ClearSelection();
        }
    }

    private void EndPieceDrag(Vector2Int dropSquare)
    {
        isDraggingPiece = false;
        ChessPieceVisual dropped = draggedVisual;
        draggedVisual = null;

        if (dropped == null) return;

        if (dropSquare.x >= 0 && selectedSquare.x >= 0)
        {
            Move chosenMove = currentLegalMoves.Find(m => m.to == dropSquare);
            if (chosenMove.from == selectedSquare)
            {
                ExecutePlayerMove(chosenMove);
                return;
            }
        }

        dropped.SetDrag(false);
        dropped.AnimateMoveTo(SquareWorldPosition(selectedSquare.x, selectedSquare.y), 0.14f, squareWidth * 0.12f);
    }

    private void ClearSelection()
    {
        if (isDraggingPiece && draggedVisual != null)
        {
            draggedVisual.SetDrag(false);
            draggedVisual.AnimateMoveTo(SquareWorldPosition(selectedSquare.x, selectedSquare.y), 0.12f, squareWidth * 0.05f);
            draggedVisual = null;
        }
        isDraggingPiece = false;
        selectedSquare = new Vector2Int(-1, -1);
        currentLegalMoves.Clear();
        RefreshHighlights();
    }

    private int LookupTileIndex(GameObject tile)
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (tileObjects[x, y] == tile) return x * Size + y;
            }
        }
        return -1;
    }

    #endregion

    #region Move Execution, Animation & Rules

    private void ExecutePlayerMove(Move move)
    {
        ClearSelection();

        Piece moving = board[move.from.x, move.from.y];
        if (moving.type == PieceType.Pawn && (move.to.y == 0 || move.to.y == 7))
        {
            pendingPromotionMove = move;
            promotionPending = true;
            return;
        }

        if (gameMode == GameMode.OnlineTwoPlayer)
        {
            onlineMovePending = true;
            onlineManager.SubmitMove(move);
            return;
        }

        ApplyMoveToGame(move);
    }

    public void CompletePromotion(PieceType promotedTo)
    {
        if (!promotionPending) return;
        promotionPending = false;

        Move move = pendingPromotionMove;
        move.promotion = promotedTo;
        if (gameMode == GameMode.OnlineTwoPlayer)
        {
            onlineMovePending = true;
            onlineManager.SubmitMove(move);
            return;
        }
        ApplyMoveToGame(move);
    }

    private void ApplyMoveToGame(Move move)
    {
        snapshots.Add(new BoardSnapshot(board, enPassantTarget, halfmoveClock, whiteToMove, whiteClockSeconds, blackClockSeconds, lastMoveMade));

        Piece movingPiece = board[move.from.x, move.from.y];
        bool isCapture = !board[move.to.x, move.to.y].IsEmpty || move.enPassant;
        PieceType capturedType = board[move.to.x, move.to.y].type;
        if (move.enPassant) capturedType = PieceType.Pawn;

        move.capturedType = capturedType;
        move.notation = GenerateAlgebraicNotation(move, movingPiece, board, enPassantTarget);

        isPieceAnimating = true;

        if (isCapture)
        {
            Vector2Int capCoord = move.enPassant ? new Vector2Int(move.to.x, move.from.y) : move.to;
            ChessPieceVisual capVisual = pieceVisuals[capCoord.x, capCoord.y];
            if (capVisual != null)
            {
                pieceVisuals[capCoord.x, capCoord.y] = null;
                Vector3 traySlot = GetNextGraveyardSlot(capVisual.isWhite);
                if (capVisual.isWhite) whiteCapturedPieces.Add(capVisual.gameObject);
                else blackCapturedPieces.Add(capVisual.gameObject);

                capVisual.AnimateCaptureToTray(traySlot, 0.38f, squareWidth * 0.65f);
            }
        }

        if (move.castle)
        {
            int rank = move.from.y;
            bool kingSide = (move.to.x == 6);
            int rookFromX = kingSide ? 7 : 0;
            int rookToX = kingSide ? 5 : 3;

            ChessPieceVisual rookVisual = pieceVisuals[rookFromX, rank];
            if (rookVisual != null)
            {
                pieceVisuals[rookFromX, rank] = null;
                pieceVisuals[rookToX, rank] = rookVisual;
                rookVisual.gridPos = new Vector2Int(rookToX, rank);
                rookVisual.AnimateMoveTo(SquareWorldPosition(rookToX, rank), 0.26f, squareWidth * 0.20f);
            }
        }

        ChessPieceVisual moverVisual = pieceVisuals[move.from.x, move.from.y];
        pieceVisuals[move.from.x, move.from.y] = null;
        pieceVisuals[move.to.x, move.to.y] = moverVisual;

        if (moverVisual != null)
        {
            moverVisual.gridPos = move.to;
            moverVisual.AnimateMoveTo(SquareWorldPosition(move.to.x, move.to.y), 0.28f, squareWidth * 0.45f, () =>
            {
                isPieceAnimating = false;

                if (move.promotion != PieceType.None)
                {
                    Destroy(moverVisual.gameObject);
                    Piece promotedPiece = new Piece(move.promotion, movingPiece.white, true);
                    ChessPieceVisual newVisual = CreatePieceGameObject(promotedPiece, move.to.x, move.to.y);
                    pieceVisuals[move.to.x, move.to.y] = newVisual;
                    ChessAudio.Instance?.PlayPromote();
                }

                bool enemyInCheck = IsSquareAttacked(board, FindKing(board, !whiteToMove), whiteToMove);
                if (enemyInCheck)
                {
                    ChessAudio.Instance?.PlayCheck();
                }
                else if (move.castle)
                {
                    ChessAudio.Instance?.PlayCastle();
                }
                else if (isCapture)
                {
                    ChessAudio.Instance?.PlayCapture();
                }
                else if (move.promotion == PieceType.None)
                {
                    ChessAudio.Instance?.PlayMove();
                }

                FinalizeMoveNotation(move, enemyInCheck);
                moveHistory.Add(move);
                lastMoveMade = move;

                // Auto flip in 2 player if enabled
                if (gameMode == GameMode.LocalTwoPlayer && ChessSettings.AutoFlipInTwoPlayer)
                {
                    isFlippedView = !isFlippedView;
                    ResetCameraView();
                }

                CheckGameOverState();
                RefreshHighlights();
                if (gameMode == GameMode.OnlineTwoPlayer && onlineManager.IsHost && gameResult != GameResult.InProgress)
                    onlineManager.BroadcastCurrentState();

                if (gameResult == GameResult.InProgress)
                {
                    CheckTriggerAIMove();
                }
            });
        }

        ApplyMoveToBoardArray(board, move);

        if (movingPiece.type == PieceType.Pawn || isCapture) halfmoveClock = 0;
        else halfmoveClock++;

        if (movingPiece.type == PieceType.Pawn && Mathf.Abs(move.to.y - move.from.y) == 2)
            enPassantTarget = new Vector2Int(move.from.x, (move.from.y + move.to.y) / 2);
        else
            enPassantTarget = new Vector2Int(-1, -1);

        whiteToMove = !whiteToMove;
        StorePositionHash();
        UpdateStatusMessage();
    }

    private void FinalizeMoveNotation(Move move, bool check)
    {
        List<Move> remaining = AllLegalMoves(board, whiteToMove, enPassantTarget);
        if (remaining.Count == 0 && check)
        {
            move.notation += "#";
        }
        else if (check)
        {
            move.notation += "+";
        }
    }

    private Vector3 GetNextGraveyardSlot(bool pieceIsWhite)
    {
        float trayX = pieceIsWhite
            ? (playSurfaceBounds.min.x - squareWidth * 1.05f)
            : (playSurfaceBounds.max.x + squareWidth * 1.05f);

        int count = pieceIsWhite ? whiteCapturedPieces.Count : blackCapturedPieces.Count;
        int row = count % 8;
        int col = count / 8;

        float z = playSurfaceBounds.min.z + (row + 0.5f) * squareDepth;
        float x = trayX + (pieceIsWhite ? -col * squareWidth * 0.75f : col * squareWidth * 0.75f);
        float y = playSurfaceBounds.max.y + 0.02f;

        return new Vector3(x, y, z);
    }

    private void CheckGameOverState()
    {
        bool inCheck = IsSquareAttacked(board, FindKing(board, whiteToMove), !whiteToMove);
        List<Move> legal = AllLegalMoves(board, whiteToMove, enPassantTarget);

        if (legal.Count == 0)
        {
            if (inCheck)
            {
                gameResult = whiteToMove ? GameResult.BlackCheckmate : GameResult.WhiteCheckmate;
                statusMessage = whiteToMove ? "Checkmate — Black wins!" : "Checkmate — White wins!";
                if (gameMode == GameMode.PlayerVsAI)
                {
                    bool playerWon = (whiteToMove && isFlippedView) || (!whiteToMove && !isFlippedView);
                    if (playerWon) ChessAudio.Instance?.PlayVictory(); else ChessAudio.Instance?.PlayDefeat();
                }
                else
                {
                    ChessAudio.Instance?.PlayVictory();
                }
            }
            else
            {
                gameResult = GameResult.Stalemate;
                statusMessage = "Draw — Stalemate!";
                ChessAudio.Instance?.PlayDraw();
            }
            RecordMatchEnd();
            return;
        }

        if (IsInsufficientMaterial(board))
        {
            gameResult = GameResult.InsufficientMaterialDraw;
            statusMessage = "Draw — Insufficient material";
            ChessAudio.Instance?.PlayDraw();
            RecordMatchEnd();
            return;
        }

        if (halfmoveClock >= 100)
        {
            gameResult = GameResult.FiftyMoveDraw;
            statusMessage = "Draw — 50-move rule";
            ChessAudio.Instance?.PlayDraw();
            RecordMatchEnd();
            return;
        }

        if (IsThreefoldRepetition())
        {
            gameResult = GameResult.ThreefoldDraw;
            statusMessage = "Draw — Threefold repetition";
            ChessAudio.Instance?.PlayDraw();
            RecordMatchEnd();
            return;
        }
    }

    private void CheckTriggerAIMove()
    {
        if (gameMode == GameMode.OnlineTwoPlayer) return;
        if (gameResult != GameResult.InProgress) return;
        if (!IsAITurn()) return;

        isAiThinking = true;
        statusMessage = (whiteToMove ? "White (AI)" : "Black (AI)") + " is thinking...";

        StartCoroutine(ChessAI.ThinkMove(
            board,
            whiteToMove,
            aiDifficulty,
            enPassantTarget,
            (b, w, ep) => AllLegalMoves(b, w, ep),
            (b, m) => ApplyMoveToBoardArray(b, m),
            (chosenMove) =>
            {
                isAiThinking = false;
                if (chosenMove.from.x >= 0)
                {
                    ApplyMoveToGame(chosenMove);
                }
            }
        ));
    }

    public void UndoMove()
    {
        if (gameMode == GameMode.OnlineTwoPlayer) return;
        if (snapshots.Count == 0 || isPieceAnimating || isAiThinking || promotionPending) return;

        int undos = (gameMode == GameMode.PlayerVsAI && snapshots.Count >= 2) ? 2 : 1;

        for (int i = 0; i < undos; i++)
        {
            if (snapshots.Count == 0) break;
            BoardSnapshot snap = snapshots[snapshots.Count - 1];
            snapshots.RemoveAt(snapshots.Count - 1);
            if (moveHistory.Count > 0) moveHistory.RemoveAt(moveHistory.Count - 1);
            if (positionHashes.Count > 1) positionHashes.RemoveAt(positionHashes.Count - 1);

            board = snap.board;
            enPassantTarget = snap.enPassantTarget;
            halfmoveClock = snap.halfmoveClock;
            whiteToMove = snap.whiteToMove;
            whiteClockSeconds = snap.whiteTime;
            blackClockSeconds = snap.blackTime;
            lastMoveMade = snap.lastMove;
        }

        gameResult = GameResult.InProgress;
        matchStatsRecorded = false;
        ClearSelection();

        ResyncAllVisuals();
        UpdateStatusMessage();
        RefreshHighlights();
        ChessAudio.Instance?.PlayIllegal();
    }

    private void ResyncAllVisuals()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (pieceVisuals[x, y] != null) Destroy(pieceVisuals[x, y].gameObject);
                pieceVisuals[x, y] = null;
            }
        }
        foreach (var obj in whiteCapturedPieces) if (obj != null) Destroy(obj);
        foreach (var obj in blackCapturedPieces) if (obj != null) Destroy(obj);
        whiteCapturedPieces.Clear();
        blackCapturedPieces.Clear();

        SpawnAllPieceVisuals();
    }

    public void RequestHint()
    {
        if (gameMode == GameMode.OnlineTwoPlayer) return;
        if (gameResult != GameResult.InProgress || isAiThinking || isPieceAnimating) return;

        Move best = ChessAI.GetHint(
            board,
            whiteToMove,
            enPassantTarget,
            (b, w, ep) => AllLegalMoves(b, w, ep),
            (b, m) => ApplyMoveToBoardArray(b, m)
        );

        if (best.from.x >= 0)
        {
            hintFrom = best.from;
            hintTo = best.to;
            RefreshHighlights();
            ChessAudio.Instance?.PlayCheck();
        }
    }

    public void ResignGame()
    {
        if (gameResult != GameResult.InProgress) return;
        if (gameMode == GameMode.OnlineTwoPlayer)
        {
            onlineManager.SubmitResignation();
            return;
        }
        gameResult = whiteToMove ? GameResult.WhiteResign : GameResult.BlackResign;
        statusMessage = whiteToMove ? "White resigned — Black wins!" : "Black resigned — White wins!";
        RecordMatchEnd();
        ChessAudio.Instance?.PlayDefeat();
    }

    public void ApplyOnlineResignation(bool resigningWhite)
    {
        if (gameMode != GameMode.OnlineTwoPlayer || gameResult != GameResult.InProgress) return;
        gameResult = resigningWhite ? GameResult.WhiteResign : GameResult.BlackResign;
        statusMessage = resigningWhite ? "White resigned — Black wins!" : "Black resigned — White wins!";
        onlineMovePending = false;
        currentScreen = AppScreen.Gameplay;
        ChessAudio.Instance?.PlayDefeat();
        RecordMatchEnd();
    }

    #endregion

    #region Navigation, Screens & Menu Handlers

    public void PauseGame()
    {
        if (currentScreen == AppScreen.Gameplay)
        {
            currentScreen = AppScreen.Paused;
            ClearSelection();
        }
    }

    public void ResumeGame()
    {
        if (currentScreen == AppScreen.Paused)
        {
            currentScreen = AppScreen.Gameplay;
        }
    }

    public void OpenMainMenu()
    {
        if (onlineManager != null && onlineManager.HasSession)
        {
            onlineManager.LeaveMatch();
            gameMode = GameMode.PlayerVsAI;
        }
        currentScreen = AppScreen.MainMenu;
        ClearSelection();
        ResetCameraView();
    }

    public void OpenMatchSetup(GameMode mode)
    {
        gameMode = mode;
        previousScreen = currentScreen;
        currentScreen = AppScreen.MatchSetup;
    }

    public void OpenSettings()
    {
        previousScreen = currentScreen;
        currentScreen = AppScreen.Settings;
    }

    public void OpenLeaderboard()
    {
        previousScreen = currentScreen;
        currentScreen = AppScreen.Leaderboard;
    }

    public void OpenQuitConfirm()
    {
        previousScreen = currentScreen;
        currentScreen = AppScreen.QuitConfirm;
    }

    public void ExecuteQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Standard FIDE Chess Rules & Validation

    public List<Move> AllLegalMoves(Piece[,] state, bool white, Vector2Int epTarget)
    {
        List<Move> all = new List<Move>();
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (!state[x, y].IsEmpty && state[x, y].white == white)
                {
                    all.AddRange(LegalMovesForSquare(state, new Vector2Int(x, y), epTarget));
                }
            }
        }
        return all;
    }

    public List<Move> LegalMovesForSquare(Piece[,] state, Vector2Int from, Vector2Int epTarget)
    {
        List<Move> legal = new List<Move>();
        Piece moving = state[from.x, from.y];
        if (moving.IsEmpty) return legal;

        List<Move> pseudo = GeneratePseudoMoves(state, from, epTarget);
        for (int i = 0; i < pseudo.Count; i++)
        {
            Move m = pseudo[i];
            Piece[,] testBoard = (Piece[,])state.Clone();
            ApplyMoveToBoardArray(testBoard, m);

            Vector2Int kingPos = FindKing(testBoard, moving.white);
            if (kingPos.x >= 0 && !IsSquareAttacked(testBoard, kingPos, !moving.white))
            {
                legal.Add(m);
            }
        }

        return legal;
    }

    private List<Move> GeneratePseudoMoves(Piece[,] state, Vector2Int from, Vector2Int epTarget)
    {
        List<Move> moves = new List<Move>();
        Piece p = state[from.x, from.y];
        int dir = p.white ? 1 : -1;

        if (p.type == PieceType.Pawn)
        {
            int ny = from.y + dir;
            if (Inside(from.x, ny) && state[from.x, ny].IsEmpty)
            {
                AddPawnMoves(moves, from, new Vector2Int(from.x, ny));

                int startingRank = p.white ? 1 : 6;
                int twice = from.y + 2 * dir;
                if (from.y == startingRank && !p.moved && Inside(from.x, twice) && state[from.x, twice].IsEmpty)
                {
                    moves.Add(new Move(from, new Vector2Int(from.x, twice)));
                }
            }

            for (int dx = -1; dx <= 1; dx += 2)
            {
                int nx = from.x + dx;
                int cy = from.y + dir;
                if (!Inside(nx, cy)) continue;

                if (!state[nx, cy].IsEmpty && state[nx, cy].white != p.white && state[nx, cy].type != PieceType.King)
                {
                    AddPawnMoves(moves, from, new Vector2Int(nx, cy));
                }
                else if (epTarget.x == nx && epTarget.y == cy)
                {
                    moves.Add(new Move(from, epTarget, PieceType.None, true));
                }
            }
            return moves;
        }

        if (p.type == PieceType.Knight)
        {
            int[,] jumps = { { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 } };
            for (int i = 0; i < 8; i++)
            {
                AddMoveIfAvailable(moves, state, from, from.x + jumps[i, 0], from.y + jumps[i, 1], p.white);
            }
            return moves;
        }

        if (p.type == PieceType.King)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                        AddMoveIfAvailable(moves, state, from, from.x + dx, from.y + dy, p.white);
                }
            }

            if (!p.moved && !IsSquareAttacked(state, from, !p.white))
            {
                int home = p.white ? 0 : 7;
                if (from.x == 4 && from.y == home)
                {
                    if (CanCastle(state, p.white, true))
                        moves.Add(new Move(from, new Vector2Int(6, home), PieceType.None, false, true));
                    if (CanCastle(state, p.white, false))
                        moves.Add(new Move(from, new Vector2Int(2, home), PieceType.None, false, true));
                }
            }
            return moves;
        }

        bool diagonal = (p.type == PieceType.Bishop || p.type == PieceType.Queen);
        bool orthogonal = (p.type == PieceType.Rook || p.type == PieceType.Queen);

        if (diagonal)
        {
            AddSlidingRays(moves, state, from, p.white, new[] {
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            });
        }
        if (orthogonal)
        {
            AddSlidingRays(moves, state, from, p.white, new[] {
                new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
            });
        }

        return moves;
    }

    private void AddPawnMoves(List<Move> list, Vector2Int from, Vector2Int to)
    {
        if (to.y == 0 || to.y == 7)
        {
            list.Add(new Move(from, to, PieceType.Queen));
            list.Add(new Move(from, to, PieceType.Knight));
            list.Add(new Move(from, to, PieceType.Rook));
            list.Add(new Move(from, to, PieceType.Bishop));
        }
        else
        {
            list.Add(new Move(from, to));
        }
    }

    private void AddMoveIfAvailable(List<Move> list, Piece[,] state, Vector2Int from, int tx, int ty, bool isWhite)
    {
        if (Inside(tx, ty) && (state[tx, ty].IsEmpty || (state[tx, ty].white != isWhite && state[tx, ty].type != PieceType.King)))
        {
            list.Add(new Move(from, new Vector2Int(tx, ty)));
        }
    }

    private void AddSlidingRays(List<Move> list, Piece[,] state, Vector2Int from, bool isWhite, Vector2Int[] dirs)
    {
        foreach (Vector2Int d in dirs)
        {
            for (int x = from.x + d.x, y = from.y + d.y; Inside(x, y); x += d.x, y += d.y)
            {
                if (state[x, y].IsEmpty)
                {
                    list.Add(new Move(from, new Vector2Int(x, y)));
                }
                else
                {
                    if (state[x, y].white != isWhite && state[x, y].type != PieceType.King)
                        list.Add(new Move(from, new Vector2Int(x, y)));
                    break;
                }
            }
        }
    }

    private bool CanCastle(Piece[,] state, bool white, bool kingSide)
    {
        int y = white ? 0 : 7;
        int rookX = kingSide ? 7 : 0;
        Piece king = state[4, y];
        Piece rook = state[rookX, y];

        if (king.type != PieceType.King || king.moved || rook.type != PieceType.Rook || rook.white != white || rook.moved)
            return false;

        int first = kingSide ? 5 : 1;
        int last = kingSide ? 6 : 3;
        for (int x = first; x <= last; x++)
        {
            if (!state[x, y].IsEmpty) return false;
        }

        int step = kingSide ? 1 : -1;
        if (IsSquareAttacked(state, new Vector2Int(4 + step, y), !white)) return false;
        if (IsSquareAttacked(state, new Vector2Int(4 + 2 * step, y), !white)) return false;

        return true;
    }

    public static Vector2Int ApplyMoveToBoardArray(Piece[,] state, Move move)
    {
        Piece moving = state[move.from.x, move.from.y];
        state[move.from.x, move.from.y] = new Piece();

        if (move.enPassant)
        {
            state[move.to.x, move.from.y] = new Piece();
        }

        if (move.castle)
        {
            int y = move.from.y;
            if (move.to.x == 6)
            {
                state[5, y] = state[7, y];
                state[7, y] = new Piece();
                Piece r = state[5, y]; r.moved = true; state[5, y] = r;
            }
            else
            {
                state[3, y] = state[0, y];
                state[0, y] = new Piece();
                Piece r = state[3, y]; r.moved = true; state[3, y] = r;
            }
        }

        moving.moved = true;
        if (move.promotion != PieceType.None) moving.type = move.promotion;
        state[move.to.x, move.to.y] = moving;

        if (moving.type == PieceType.Pawn && Mathf.Abs(move.to.y - move.from.y) == 2)
            return new Vector2Int(move.from.x, (move.from.y + move.to.y) / 2);

        return new Vector2Int(-1, -1);
    }

    public static bool IsSquareAttacked(Piece[,] state, Vector2Int square, bool byWhite)
    {
        if (square.x < 0 || square.y < 0) return false;

        int pawnY = square.y + (byWhite ? -1 : 1);
        for (int dx = -1; dx <= 1; dx += 2)
        {
            int px = square.x + dx;
            if (Inside(px, pawnY))
            {
                Piece p = state[px, pawnY];
                if (p.type == PieceType.Pawn && p.white == byWhite) return true;
            }
        }

        int[,] jumps = { { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 } };
        for (int i = 0; i < 8; i++)
        {
            int x = square.x + jumps[i, 0];
            int y = square.y + jumps[i, 1];
            if (Inside(x, y))
            {
                Piece p = state[x, y];
                if (p.type == PieceType.Knight && p.white == byWhite) return true;
            }
        }

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                for (int n = 1; n < Size; n++)
                {
                    int x = square.x + dx * n;
                    int y = square.y + dy * n;
                    if (!Inside(x, y)) break;
                    Piece p = state[x, y];
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

    public static Vector2Int FindKing(Piece[,] state, bool white)
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (state[x, y].type == PieceType.King && state[x, y].white == white)
                    return new Vector2Int(x, y);
            }
        }
        return new Vector2Int(-1, -1);
    }

    public static bool Inside(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

    private bool IsInsufficientMaterial(Piece[,] state)
    {
        int minors = 0;
        bool onlyBishops = true;
        int bishopColor = -1;

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Piece p = state[x, y];
                if (p.IsEmpty || p.type == PieceType.King) continue;
                if (p.type == PieceType.Pawn || p.type == PieceType.Rook || p.type == PieceType.Queen) return false;
                minors++;
                if (p.type != PieceType.Bishop) onlyBishops = false;
                int color = (x + y) % 2;
                if (bishopColor < 0) bishopColor = color;
                else if (color != bishopColor) onlyBishops = false;
            }
        }

        return minors == 0 || minors == 1 || (onlyBishops && minors == 2 && bishopColor >= 0);
    }

    private bool HasMatingMaterial(bool isWhite)
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Piece p = board[x, y];
                if (p.IsEmpty || p.white != isWhite) continue;
                if (p.type == PieceType.Pawn || p.type == PieceType.Rook || p.type == PieceType.Queen) return true;
                if (p.type == PieceType.Bishop || p.type == PieceType.Knight) return true;
            }
        }
        return false;
    }

    private void StorePositionHash()
    {
        string s = "";
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Piece p = board[x, y];
                s += p.IsEmpty ? "." : (p.white ? "w" : "b") + (int)p.type;
            }
        }
        s += whiteToMove ? "w" : "b";
        s += enPassantTarget.x < 0 ? "-" : $"{enPassantTarget.x},{enPassantTarget.y}";
        s += CanCastle(board, true, true) ? "K" : "-";
        s += CanCastle(board, true, false) ? "Q" : "-";
        s += CanCastle(board, false, true) ? "k" : "-";
        s += CanCastle(board, false, false) ? "q" : "-";
        positionHashes.Add(s);
    }

    private bool IsThreefoldRepetition()
    {
        if (positionHashes.Count < 3) return false;
        string current = positionHashes[positionHashes.Count - 1];
        int count = 0;
        for (int i = 0; i < positionHashes.Count; i++)
        {
            if (positionHashes[i] == current) count++;
        }
        return count >= 3;
    }

    private string GenerateAlgebraicNotation(Move move, Piece moving, Piece[,] state, Vector2Int epTarget)
    {
        if (move.castle) return (move.to.x == 6) ? "O-O" : "O-O-O";

        string san = "";
        if (moving.type == PieceType.Pawn)
        {
            if (move.IsCapture)
            {
                san += (char)('a' + move.from.x);
                san += "x";
            }
            san += SquareName(move.to);
            if (move.promotion != PieceType.None)
            {
                san += "=" + PieceNotationChar(move.promotion);
            }
        }
        else
        {
            san += PieceNotationChar(moving.type);

            List<Move> alternates = AllLegalMoves(state, moving.white, epTarget).FindAll(
                m => m.to == move.to && m.from != move.from && state[m.from.x, m.from.y].type == moving.type
            );

            if (alternates.Count > 0)
            {
                bool diffFile = true;
                bool diffRank = true;
                for (int i = 0; i < alternates.Count; i++)
                {
                    if (alternates[i].from.x == move.from.x) diffFile = false;
                    if (alternates[i].from.y == move.from.y) diffRank = false;
                }
                if (diffFile) san += (char)('a' + move.from.x);
                else if (diffRank) san += (move.from.y + 1).ToString();
                else san += SquareName(move.from);
            }

            if (move.IsCapture) san += "x";
            san += SquareName(move.to);
        }

        return san;
    }

    private char PieceNotationChar(PieceType type)
    {
        switch (type)
        {
            case PieceType.Knight: return 'N';
            case PieceType.Bishop: return 'B';
            case PieceType.Rook: return 'R';
            case PieceType.Queen: return 'Q';
            case PieceType.King: return 'K';
            default: return ' ';
        }
    }

    private string SquareName(Vector2Int sq) => $"{(char)('a' + sq.x)}{sq.y + 1}";

    private void UpdateStatusMessage()
    {
        bool inCheck = IsSquareAttacked(board, FindKing(board, whiteToMove), !whiteToMove);
        string side = whiteToMove ? "White" : "Black";
        statusMessage = inCheck ? $"{side} is in check!" : $"{side} to move";
    }

    #endregion

    #region Visual Highlights

    private void RefreshHighlights()
    {
        Vector2Int whiteKing = FindKing(board, true);
        Vector2Int blackKing = FindKing(board, false);
        bool whiteInCheck = IsSquareAttacked(board, whiteKing, false);
        bool blackInCheck = IsSquareAttacked(board, blackKing, true);

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                bool quadActive = false;
                Material quadMat = matTileSelect;
                bool dotActive = false;
                bool ringActive = false;

                if (currentScreen == AppScreen.Gameplay)
                {
                    // 1. King in Check indicator
                    if ((whiteInCheck && whiteKing.x == x && whiteKing.y == y) ||
                        (blackInCheck && blackKing.x == x && blackKing.y == y))
                    {
                        quadActive = true;
                        quadMat = matTileCheck;
                    }
                    // 2. Selected square
                    else if (selectedSquare.x == x && selectedSquare.y == y)
                    {
                        quadActive = true;
                        quadMat = matTileSelect;
                    }
                    // 3. Last move from / to squares
                    else if (ChessSettings.ShowLastMoveHighlight && lastMoveMade.from.x >= 0 &&
                             (lastMoveMade.from.x == x && lastMoveMade.from.y == y || lastMoveMade.to.x == x && lastMoveMade.to.y == y))
                    {
                        quadActive = true;
                        quadMat = matTileLastMove;
                    }
                    // 4. Hint square
                    else if (hintFrom.x >= 0 && (hintFrom.x == x && hintFrom.y == y || hintTo.x == x && hintTo.y == y))
                    {
                        quadActive = true;
                        quadMat = matTileHint;
                    }
                    // 5. Hover square
                    else if (hoveredSquare.x == x && hoveredSquare.y == y && selectedSquare != hoveredSquare)
                    {
                        quadActive = true;
                        quadMat = matTileSelect;
                    }

                    // Legal move indicators
                    if (ChessSettings.ShowMoveHighlights && selectedSquare.x >= 0)
                    {
                        Move m = currentLegalMoves.Find(move => move.to.x == x && move.to.y == y);
                        if (m.from == selectedSquare)
                        {
                            if (m.IsCapture) ringActive = true;
                            else dotActive = true;
                        }
                    }
                }

                if (tileQuadRenderers[x, y] != null)
                {
                    tileQuadRenderers[x, y].enabled = quadActive;
                    if (quadActive) tileQuadRenderers[x, y].sharedMaterial = quadMat;
                }

                if (tileDotRenderers[x, y] != null)
                {
                    tileDotRenderers[x, y].enabled = dotActive;
                }

                if (tileRingRenderers[x, y] != null)
                {
                    tileRingRenderers[x, y].enabled = ringActive;
                }
            }
        }
    }

    #endregion

    #region Modern UI HUD & Menus (OnGUI)

    private void InitializeUIStyles()
    {
        texDarkGlass = MakeSolidTexture(new Color(0.06f, 0.08f, 0.11f, 0.92f));
        texCardBg = MakeSolidTexture(new Color(0.10f, 0.13f, 0.17f, 0.94f));
        texButtonNormal = MakeSolidTexture(new Color(0.18f, 0.22f, 0.28f, 0.95f));
        texButtonHover = MakeSolidTexture(new Color(0.28f, 0.35f, 0.44f, 0.95f));
        texButtonActive = MakeSolidTexture(new Color(0.38f, 0.48f, 0.60f, 1f));
        texAccent = MakeSolidTexture(new Color(0.88f, 0.72f, 0.28f, 1f));
        texGoldBorder = MakeSolidTexture(new Color(0.92f, 0.78f, 0.32f, 0.9f));
        texSliderBar = MakeSolidTexture(new Color(0.25f, 0.30f, 0.38f, 1f));

        styleTitle = new GUIStyle
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.95f, 0.88f, 0.72f) }
        };

        styleSubtitle = new GUIStyle
        {
            fontSize = 12,
            normal = { textColor = new Color(0.68f, 0.74f, 0.82f) }
        };

        styleCardHeader = new GUIStyle
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        styleClock = new GUIStyle
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.92f, 0.95f, 0.98f) }
        };

        styleClockWarning = new GUIStyle(styleClock)
        {
            normal = { textColor = new Color(0.95f, 0.32f, 0.32f) }
        };

        styleButton = new GUIStyle
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = texButtonNormal, textColor = new Color(0.92f, 0.94f, 0.96f) },
            hover = { background = texButtonHover, textColor = Color.white },
            active = { background = texButtonActive, textColor = Color.white }
        };

        styleAccentButton = new GUIStyle(styleButton)
        {
            normal = { background = texAccent, textColor = new Color(0.08f, 0.10f, 0.14f) },
            hover = { background = texGoldBorder, textColor = Color.black },
            active = { background = texButtonActive, textColor = Color.white }
        };

        styleSmallButton = new GUIStyle(styleButton)
        {
            fontSize = 12
        };

        styleMoveLog = new GUIStyle
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
        };

        styleModalTitle = new GUIStyle
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.95f, 0.88f, 0.65f) }
        };

        styleModalText = new GUIStyle
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        styleMainMenuTitle = new GUIStyle(styleModalTitle)
        {
            fontSize = Mathf.RoundToInt(styleModalTitle.fontSize * 1.75f)
        };
        styleMainMenuSubtitle = new GUIStyle(styleSubtitle)
        {
            fontSize = Mathf.RoundToInt(styleSubtitle.fontSize * 1.75f)
        };
        styleMainMenuButton = new GUIStyle(styleButton)
        {
            fontSize = Mathf.RoundToInt(styleButton.fontSize * 1.75f)
        };
        styleMainMenuAccentButton = new GUIStyle(styleAccentButton)
        {
            fontSize = Mathf.RoundToInt(styleAccentButton.fontSize * 1.75f)
        };
        styleMainMenuSmallButton = new GUIStyle(styleSmallButton)
        {
            fontSize = Mathf.RoundToInt(styleSmallButton.fontSize * 1.75f)
        };
    }

    private Texture2D MakeSolidTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private void OnGUI()
    {
        if (styleTitle == null) InitializeUIStyles();

        switch (currentScreen)
        {
            case AppScreen.MainMenu:
                DrawMainMenuScreen();
                break;
            case AppScreen.Gameplay:
                DrawGameplayScreen();
                break;
            case AppScreen.Paused:
                DrawGameplayScreen();
                DrawPauseMenuModal();
                break;
            case AppScreen.Settings:
                DrawSettingsMenuScreen();
                break;
            case AppScreen.Leaderboard:
                DrawLeaderboardScreen();
                break;
            case AppScreen.MatchSetup:
                DrawNewGameModal();
                break;
            case AppScreen.OnlineMenu:
                DrawOnlineMenuScreen();
                break;
            case AppScreen.QuitConfirm:
                DrawQuitConfirmModal();
                break;
        }

        if (promotionPending) DrawPromotionModal();
        if (currentScreen == AppScreen.Gameplay && gameResult != GameResult.InProgress) DrawGameOverModal();
    }

    #region Main Menu UI

    private void DrawMainMenuScreen()
    {
        float mw = 600;
        float mh = 560;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(modalRect, texCardBg);
        GUI.DrawTexture(new Rect(modalRect.x, modalRect.y, modalRect.width, 4), texAccent);

        // Title & Logo Header
        GUI.Label(new Rect(modalRect.x, modalRect.y + 18, mw, 50), "CHESS 3D", styleMainMenuTitle);
        GUI.Label(new Rect(modalRect.x, modalRect.y + 70, mw, 28), "Realistic Tabletop Edition", styleMainMenuSubtitle);

        float curY = modalRect.y + 112;
        float btnW = 500;
        float btnH = 52;
        float btnX = modalRect.x + (mw - btnW) * 0.5f;
        float spacing = 8;

        // 1. Play vs Computer
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "⚔  Play vs Computer (AI)", styleMainMenuAccentButton))
        {
            OpenMatchSetup(GameMode.PlayerVsAI);
        }

        // 2. Pass & Play
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "👥  Pass & Play (2 Players)", styleMainMenuButton))
        {
            OpenMatchSetup(GameMode.LocalTwoPlayer);
        }

        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "🌐  Play Online (2 Players)", styleMainMenuButton))
        {
            OpenOnlineMenu();
        }

        // Leaderboard & Stats
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), $"🏆  Leaderboard ({ChessStats.Elo} ELO)", styleMainMenuButton))
        {
            OpenLeaderboard();
        }

        // 4. Settings
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "⚙  Settings & Preferences", styleMainMenuButton))
        {
            OpenSettings();
        }

        // 5. Quit Game
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "🚪  Quit Game", styleMainMenuSmallButton))
        {
            OpenQuitConfirm();
        }

        // Footer version info
        GUI.Label(new Rect(modalRect.x, modalRect.y + mh - 34, mw, 28), "Unity 6 • FIDE Standard Rules • Procedural Audio", styleMainMenuSubtitle);
    }

    #endregion

    private void DrawOnlineMenuScreen()
    {
        float mw = 460;
        float mh = 690;
        Rect panel = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);
        GUI.DrawTexture(panel, texCardBg);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 4), texAccent);
        GUI.Label(new Rect(panel.x, panel.y + 22, mw, 34), "ONLINE CHESS", styleModalTitle);
        GUI.Label(new Rect(panel.x + 24, panel.y + 62, mw - 48, 36),
            "Supabase accounts save profiles and mutually confirmed online results.", styleSubtitle);

        float x = panel.x + 35;
        float width = mw - 70;
        float y = panel.y + 104;
        GUI.Label(new Rect(x, y, width, 20), "SUPABASE ACCOUNT", styleCardHeader);
        y += 24;
        bool accountWasEnabled = GUI.enabled;
        GUI.enabled = accountWasEnabled && !onlineManager.IsSupabaseBusy;
        if (onlineManager.IsSupabaseSignedIn)
        {
            if (string.IsNullOrEmpty(onlineManager.SupabaseDisplayNameInput))
                onlineManager.SupabaseDisplayNameInput = SupabaseRest.CurrentDisplayName;
            GUI.Label(new Rect(x, y, width, 22), onlineManager.SupabaseAccountLabel, styleModalText);
            y += 24;
            GUI.Label(new Rect(x, y, 95, 28), "Profile name", styleSubtitle);
            onlineManager.SupabaseDisplayNameInput = GUI.TextField(
                new Rect(x + 100, y, width - 100, 28), onlineManager.SupabaseDisplayNameInput, 40);
            y += 34;
            if (GUI.Button(new Rect(x, y, (width - 10) * 0.5f, 34), "Save Profile", styleAccentButton))
                onlineManager.SaveSupabaseProfile();
            if (GUI.Button(new Rect(x + (width + 10) * 0.5f, y, (width - 10) * 0.5f, 34), "Sign Out", styleSmallButton))
                onlineManager.SignOutSupabaseAccount();
        }
        else
        {
            onlineManager.SupabaseEmailInput = GUI.TextField(
                new Rect(x, y, width, 30), onlineManager.SupabaseEmailInput, 120);
            if (string.IsNullOrEmpty(onlineManager.SupabaseEmailInput))
                GUI.Label(new Rect(x + 8, y + 5, width - 16, 20), "Email", styleSubtitle);
            y += 34;
            onlineManager.SupabasePasswordInput = GUI.PasswordField(
                new Rect(x, y, width, 30), onlineManager.SupabasePasswordInput, '*', 128);
            if (string.IsNullOrEmpty(onlineManager.SupabasePasswordInput))
                GUI.Label(new Rect(x + 8, y + 5, width - 16, 20), "Password", styleSubtitle);
            y += 36;
            if (GUI.Button(new Rect(x, y, (width - 10) * 0.5f, 34), "Create Account", styleAccentButton))
                onlineManager.CreateSupabaseAccount();
            if (GUI.Button(new Rect(x + (width + 10) * 0.5f, y, (width - 10) * 0.5f, 34), "Sign In", styleButton))
                onlineManager.SignInSupabaseAccount();
        }
        GUI.enabled = accountWasEnabled;

        GUI.Label(new Rect(x, panel.y + 225, width, 34), onlineManager.SupabaseStatus, styleSubtitle);
        GUI.Label(new Rect(x, panel.y + 258, width, 22), "LIVE MULTIPLAYER", styleCardHeader);
        float onlineY = panel.y + 282;
        float buttonHeight = 36;
        GUI.enabled = !onlineManager.IsBusy && (!onlineManager.HasSession || onlineManager.NeedsReconnect);

        if (onlineManager.HasSession && onlineManager.NeedsReconnect)
        {
            if (GUI.Button(new Rect(x, onlineY, width, buttonHeight), "Reconnect to Match", styleAccentButton))
                onlineManager.ReconnectToPreviousMatch();
        }
        else
        {
            if (GUI.Button(new Rect(x, onlineY, width, buttonHeight), "Create Invite Game", styleAccentButton))
                onlineManager.CreateInviteGame();
            onlineY += buttonHeight + 8;

            GUI.Label(new Rect(x, onlineY, width, 18), "Join code", styleCardHeader);
            onlineY += 20;
            onlineManager.JoinCodeInput = GUI.TextField(new Rect(x, onlineY, width, 30), onlineManager.JoinCodeInput, 32);
            onlineY += 34;
            if (GUI.Button(new Rect(x, onlineY, width, buttonHeight), "Join Game", styleButton))
                onlineManager.JoinByCode();
            onlineY += buttonHeight + 8;

            if (GUI.Button(new Rect(x, onlineY, width, buttonHeight), "Quick Match", styleButton))
                onlineManager.QuickMatch();
            onlineY += buttonHeight + 8;

            if (onlineManager.CanReconnect &&
                GUI.Button(new Rect(x, onlineY, width, buttonHeight), "Rejoin Previous Match", styleButton))
            {
                onlineManager.ReconnectToPreviousMatch();
            }
        }

        if (!string.IsNullOrEmpty(onlineManager.Status))
        {
            GUI.Label(new Rect(x, panel.y + mh - 96, width, 42), onlineManager.Status, styleModalText);
        }

        if (!string.IsNullOrEmpty(onlineManager.JoinCode))
        {
            GUI.Label(new Rect(x, panel.y + mh - 140, width - 110, 30),
                "Invite code: " + onlineManager.JoinCode, styleModalText);
            if (GUI.Button(new Rect(panel.x + mw - 128, panel.y + mh - 144, 92, 34),
                "Copy code", styleSmallButton))
            {
                GUIUtility.systemCopyBuffer = onlineManager.JoinCode;
                onlineManager.SetStatus("Invite code copied.");
            }
        }

        GUI.enabled = true;
        if (GUI.Button(new Rect(panel.x + 35, panel.y + mh - 48, width, 34), "Back", styleSmallButton))
            OpenMainMenu();
    }

    #region Gameplay UI

    private void DrawGameplayScreen()
    {
        if (gameMode == GameMode.OnlineTwoPlayer)
        {
            string onlineStatus = string.IsNullOrEmpty(onlineManager.JoinCode)
                ? "Online match"
                : "Invite code: " + onlineManager.JoinCode;
            GUI.Label(new Rect(190, 12, 220, 28), onlineStatus, styleModalText);
            if (onlineManager.NeedsReconnect &&
                GUI.Button(new Rect(420, 12, 130, 28), "Reconnect", styleSmallButton))
            {
                onlineManager.ReconnectToPreviousMatch();
            }
        }

        // Top Toolbar
        string toggleLabel = sidebarCollapsed ? "▶ Menu" : "◀ Hide";
        if (GUI.Button(new Rect(18, 12, 70, 28), toggleLabel, styleSmallButton))
        {
            ToggleSidebar();
        }

        // Pause Button
        if (GUI.Button(new Rect(94, 12, 85, 28), "⏸ Pause", styleSmallButton))
        {
            PauseGame();
        }

        if (!sidebarCollapsed)
        {
            DrawTopBar();
            DrawPlayerPanels();
            DrawMoveLog();
            DrawControlToolbar();
        }
        else
        {
            DrawCompactHUD();
        }
    }

    private void DrawCompactHUD()
    {
        float bannerW = 340;
        float bannerH = 32;
        Rect r = new Rect(188, 10, bannerW, bannerH);
        GUI.DrawTexture(r, texDarkGlass);

        string turnStr = (whiteToMove ? "♔ White" : "♚ Black") + $" ({FormatClockTime(whiteToMove ? whiteClockSeconds : blackClockSeconds)})";
        GUI.Label(new Rect(r.x + 14, r.y + 6, bannerW - 28, 22), $"{turnStr} • {statusMessage}", styleSubtitle);
    }

    private void DrawTopBar()
    {
        GUI.DrawTexture(new Rect(18, 46, 290, 50), texDarkGlass);

        GUI.Label(new Rect(30, 50, 140, 24), "CHESS 3D", styleTitle);

        string modeLabel = (gameMode == GameMode.PlayerVsAI)
            ? $"vs AI ({aiDifficulty})"
            : (gameMode == GameMode.LocalTwoPlayer ? "Pass & Play" : "AI vs AI");
        GUI.Label(new Rect(32, 72, 260, 20), $"{modeLabel} • {statusMessage}", styleSubtitle);
    }

    private void DrawPlayerPanels()
    {
        int whiteMatTotal = 0;
        int blackMatTotal = 0;
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Piece p = board[x, y];
                if (p.IsEmpty || p.type == PieceType.King) continue;
                if (p.white) whiteMatTotal += p.MaterialValue;
                else blackMatTotal += p.MaterialValue;
            }
        }
        int diff = (whiteMatTotal - blackMatTotal) / 100;

        float panelW = 290;
        float panelH = 68;
        float startY = 104;

        DrawSinglePlayerCard(new Rect(18, startY, panelW, panelH),
            false,
            (gameMode == GameMode.PlayerVsAI && !isFlippedView) ? $"AI ({aiDifficulty})" : "Black",
            blackClockSeconds,
            diff < 0 ? $"+{-diff}" : "",
            !whiteToMove);

        DrawSinglePlayerCard(new Rect(18, startY + panelH + 8, panelW, panelH),
            true,
            (gameMode == GameMode.PlayerVsAI && isFlippedView) ? $"AI ({aiDifficulty})" : "White",
            whiteClockSeconds,
            diff > 0 ? $"+{diff}" : "",
            whiteToMove);
    }

    private void DrawSinglePlayerCard(Rect r, bool isWhite, string name, float seconds, string materialLead, bool isActiveTurn)
    {
        GUI.DrawTexture(r, texCardBg);

        if (isActiveTurn && gameResult == GameResult.InProgress)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), texAccent);
        }

        GUI.Label(new Rect(r.x + 12, r.y + 12, 24, 24), isWhite ? "♔" : "♚", styleTitle);
        GUI.Label(new Rect(r.x + 36, r.y + 12, 130, 20), name, styleCardHeader);
        if (!string.IsNullOrEmpty(materialLead))
        {
            GUI.Label(new Rect(r.x + 36, r.y + 34, 60, 18), materialLead, styleSubtitle);
        }

        if (clockActive)
        {
            string timeStr = FormatClockTime(seconds);
            GUIStyle clockSt = (seconds <= 20f && isActiveTurn) ? styleClockWarning : styleClock;
            GUI.Label(new Rect(r.x + r.width - 118, r.y + 14, 105, 34), timeStr, clockSt);
        }
    }

    private string FormatClockTime(float sec)
    {
        if (sec < 0f) sec = 0f;
        int m = (int)(sec / 60f);
        int s = (int)(sec % 60f);
        if (sec < 10f)
        {
            int tenth = (int)((sec - Mathf.Floor(sec)) * 10f);
            return $"{m}:{s:00}.{tenth}";
        }
        return $"{m}:{s:00}";
    }

    private void DrawMoveLog()
    {
        float logW = 290;
        float logH = 135;
        float logY = 256;

        GUI.DrawTexture(new Rect(18, logY, logW, logH), texCardBg);
        GUI.Label(new Rect(28, logY + 6, 120, 18), "Move History", styleSubtitle);

        Rect scrollArea = new Rect(24, logY + 26, logW - 12, logH - 32);
        int turnCount = (moveHistory.Count + 1) / 2;
        Rect viewArea = new Rect(0, 0, logW - 35, Mathf.Max(logH - 32, turnCount * 20));

        moveHistoryScroll = GUI.BeginScrollView(scrollArea, moveHistoryScroll, viewArea);
        for (int i = 0; i < turnCount; i++)
        {
            int whiteIdx = i * 2;
            int blackIdx = i * 2 + 1;
            string wMove = (whiteIdx < moveHistory.Count) ? moveHistory[whiteIdx].notation : "";
            string bMove = (blackIdx < moveHistory.Count) ? moveHistory[blackIdx].notation : "";

            string turnStr = $"{i + 1}.";
            GUI.Label(new Rect(4, i * 20, 32, 18), turnStr, styleSubtitle);
            GUI.Label(new Rect(40, i * 20, 80, 18), wMove, styleMoveLog);
            GUI.Label(new Rect(125, i * 20, 80, 18), bMove, styleMoveLog);
        }
        GUI.EndScrollView();
    }

    private void DrawControlToolbar()
    {
        float barY = 399;
        float btnW = 68;
        float btnH = 28;
        float spacing = 6;
        float startX = 18;

        if (GUI.Button(new Rect(startX, barY, btnW, btnH), "New Match", styleSmallButton))
        {
            OpenMatchSetup(gameMode);
        }

        GUI.enabled = (snapshots.Count > 0 && !isPieceAnimating && !isAiThinking);
        if (GUI.Button(new Rect(startX + (btnW + spacing), barY, btnW, btnH), "Undo", styleSmallButton))
        {
            UndoMove();
        }
        GUI.enabled = true;

        GUI.enabled = (gameResult == GameResult.InProgress && !isAiThinking && !isPieceAnimating);
        if (GUI.Button(new Rect(startX + (btnW + spacing) * 2, barY, btnW, btnH), "Hint", styleSmallButton))
        {
            RequestHint();
        }
        GUI.enabled = true;

        if (GUI.Button(new Rect(startX + (btnW + spacing) * 3, barY, btnW, btnH), isFlippedView ? "Flip (W)" : "Flip (B)", styleSmallButton))
        {
            ToggleBoardFlip();
        }

        float row2Y = barY + btnH + 6;

        if (GUI.Button(new Rect(startX, row2Y, btnW, btnH), isTopDownView ? "View: 3D" : "View: 2D", styleSmallButton))
        {
            ToggleTopDownView();
        }

        bool soundOn = ChessSettings.SoundEnabled;
        if (GUI.Button(new Rect(startX + (btnW + spacing), row2Y, btnW, btnH), soundOn ? "Sound: ON" : "Sound: OFF", styleSmallButton))
        {
            ChessSettings.SoundEnabled = !soundOn;
            ChessSettings.Save();
        }

        GUI.enabled = (gameResult == GameResult.InProgress && !isAiThinking);
        if (GUI.Button(new Rect(startX + (btnW + spacing) * 2, row2Y, btnW * 2 + spacing, btnH), "Resign", styleSmallButton))
        {
            ResignGame();
        }
        GUI.enabled = true;

        GUI.DrawTexture(new Rect(18, Screen.height - 40, 420, 28), texDarkGlass);
        GUI.Label(new Rect(26, Screen.height - 35, 400, 20), "Drag piece or click to move • Right-drag to orbit 3D camera", styleSubtitle);
    }

    #endregion

    #region Pause Menu UI

    private void DrawPauseMenuModal()
    {
        float mw = 380;
        float mh = 400;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texDarkGlass);
        GUI.DrawTexture(modalRect, texCardBg);
        GUI.DrawTexture(new Rect(modalRect.x, modalRect.y, modalRect.width, 4), texAccent);

        GUI.Label(new Rect(modalRect.x, modalRect.y + 20, mw, 32), "GAME PAUSED", styleModalTitle);
        GUI.Label(new Rect(modalRect.x, modalRect.y + 54, mw, 20), $"{(whiteToMove ? "White" : "Black")} to move • Clocks paused", styleSubtitle);

        float curY = modalRect.y + 90;
        float btnW = 300;
        float btnH = 40;
        float btnX = modalRect.x + (mw - btnW) * 0.5f;
        float spacing = 10;

        // 1. Resume
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "▶  Resume Game", styleAccentButton))
        {
            ResumeGame();
        }

        // 2. Restart Match
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "🔄  Restart Match", styleButton))
        {
            ResumeGame();
            StartNewGame();
        }

        // 3. Settings
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "⚙  Settings", styleButton))
        {
            OpenSettings();
        }

        // 4. Leaderboard & Stats
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "🏆  Leaderboard & Stats", styleButton))
        {
            OpenLeaderboard();
        }

        // 5. Main Menu
        curY += btnH + spacing;
        if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "🏠  Main Menu", styleSmallButton))
        {
            OpenMainMenu();
        }
    }

    #endregion

    #region Settings Menu UI

    private void DrawSettingsMenuScreen()
    {
        float mw = 520;
        float mh = 500;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texDarkGlass);
        GUI.DrawTexture(modalRect, texCardBg);
        GUI.DrawTexture(new Rect(modalRect.x, modalRect.y, modalRect.width, 4), texAccent);

        GUI.Label(new Rect(modalRect.x, modalRect.y + 18, mw, 30), "SETTINGS & PREFERENCES", styleModalTitle);

        float curY = modalRect.y + 60;
        float leftX = modalRect.x + 36;
        float controlX = modalRect.x + 220;
        float rowH = 36;

        // Section: AUDIO
        GUI.Label(new Rect(leftX, curY, 200, 24), "AUDIO SETTINGS", styleCardHeader);
        curY += 28;

        // Sound Mute Toggle
        GUI.Label(new Rect(leftX, curY, 180, 22), "Sound Effects:", styleSubtitle);
        if (GUI.Button(new Rect(controlX, curY, 240, 26), ChessSettings.SoundEnabled ? "✓ Enabled (Audio ON)" : "✗ Muted (Audio OFF)", styleButton))
        {
            ChessSettings.SoundEnabled = !ChessSettings.SoundEnabled;
            ChessSettings.Save();
        }
        curY += rowH;

        // Volume Slider
        GUI.Label(new Rect(leftX, curY, 180, 22), $"SFX Volume ({(int)(ChessSettings.SoundVolume * 100)}%):", styleSubtitle);
        float newVol = GUI.HorizontalSlider(new Rect(controlX, curY + 6, 240, 20), ChessSettings.SoundVolume, 0f, 1f);
        if (Mathf.Abs(newVol - ChessSettings.SoundVolume) > 0.01f)
        {
            ChessSettings.SoundVolume = newVol;
            ChessSettings.Save();
        }
        curY += rowH;

        GUI.Label(new Rect(leftX, curY, 180, 22), "Background Music:", styleSubtitle);
        if (GUI.Button(new Rect(controlX, curY, 240, 26), ChessSettings.MusicEnabled ? "✓ Enabled" : "✗ Muted", styleButton))
        {
            ChessSettings.MusicEnabled = !ChessSettings.MusicEnabled;
            ChessSettings.Save();
        }
        curY += rowH;

        GUI.Label(new Rect(leftX, curY, 180, 22), $"Music Volume ({(int)(ChessSettings.MusicVolume * 100)}%):", styleSubtitle);
        float newMusicVol = GUI.HorizontalSlider(new Rect(controlX, curY + 6, 240, 20), ChessSettings.MusicVolume, 0f, 1f);
        if (Mathf.Abs(newMusicVol - ChessSettings.MusicVolume) > 0.01f)
        {
            ChessSettings.MusicVolume = newMusicVol;
            ChessSettings.Save();
        }
        curY += rowH + 8;

        // Section: VISUALS & GAMEPLAY
        GUI.Label(new Rect(leftX, curY, 200, 24), "VISUALS & BOARD", styleCardHeader);
        curY += 28;

        // Board Coordinates
        GUI.Label(new Rect(leftX, curY, 180, 22), "Board Coordinates (a-h, 1-8):", styleSubtitle);
        if (GUI.Button(new Rect(controlX, curY, 240, 26), ChessSettings.ShowCoordinates ? "✓ Visible on Rim" : "✗ Hidden", styleButton))
        {
            ChessSettings.ShowCoordinates = !ChessSettings.ShowCoordinates;
            ChessSettings.Save();
        }
        curY += rowH;

        // Move Highlights
        GUI.Label(new Rect(leftX, curY, 180, 22), "Legal Move Highlights:", styleSubtitle);
        if (GUI.Button(new Rect(controlX, curY, 240, 26), ChessSettings.ShowMoveHighlights ? "✓ Show Dots & Rings" : "✗ Hidden", styleButton))
        {
            ChessSettings.ShowMoveHighlights = !ChessSettings.ShowMoveHighlights;
            ChessSettings.Save();
        }
        curY += rowH;

        // Last Move Highlight
        GUI.Label(new Rect(leftX, curY, 180, 22), "Last Move Indicator:", styleSubtitle);
        if (GUI.Button(new Rect(controlX, curY, 240, 26), ChessSettings.ShowLastMoveHighlight ? "✓ Highlight From / To" : "✗ Off", styleButton))
        {
            ChessSettings.ShowLastMoveHighlight = !ChessSettings.ShowLastMoveHighlight;
            ChessSettings.Save();
        }
        curY += rowH;

        // Auto Flip in 2-Player
        GUI.Label(new Rect(leftX, curY, 180, 22), "Pass & Play Auto-Flip:", styleSubtitle);
        if (GUI.Button(new Rect(controlX, curY, 240, 26), ChessSettings.AutoFlipInTwoPlayer ? "✓ Auto-Rotate Camera" : "✗ Fixed View", styleButton))
        {
            ChessSettings.AutoFlipInTwoPlayer = !ChessSettings.AutoFlipInTwoPlayer;
            ChessSettings.Save();
        }
        curY += rowH + 16;

        // Bottom Controls: Reset & Back
        float btnW = 160;
        if (GUI.Button(new Rect(modalRect.x + 40, curY, btnW, 40), "Reset Defaults", styleSmallButton))
        {
            ChessSettings.ResetToDefaults();
        }
        if (GUI.Button(new Rect(modalRect.x + mw - btnW - 40, curY, btnW, 40), "Back", styleAccentButton))
        {
            currentScreen = previousScreen;
        }
    }

    #endregion

    #region Leaderboard & Stats UI

    private void DrawLeaderboardScreen()
    {
        float mw = 580;
        float mh = 530;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texDarkGlass);
        GUI.DrawTexture(modalRect, texCardBg);
        GUI.DrawTexture(new Rect(modalRect.x, modalRect.y, modalRect.width, 4), texAccent);

        GUI.Label(new Rect(modalRect.x, modalRect.y + 16, mw, 30), "LEADERBOARD & PLAYER STATS", styleModalTitle);

        float curY = modalRect.y + 54;
        float panelW = mw - 48;
        float panelX = modalRect.x + 24;

        // Profile Overview Card
        Rect profileCard = new Rect(panelX, curY, panelW, 85);
        GUI.DrawTexture(profileCard, texDarkGlass);

        // Rating Badge
        GUI.Label(new Rect(profileCard.x + 20, profileCard.y + 16, 180, 32), $"🏆 {ChessStats.Elo} ELO", styleTitle);
        string rankTitle = (ChessStats.Elo >= 2000) ? "Grandmaster" : (ChessStats.Elo >= 1700 ? "Expert" : (ChessStats.Elo >= 1400 ? "Club Player" : "Novice"));
        GUI.Label(new Rect(profileCard.x + 22, profileCard.y + 48, 180, 20), $"Rank: {rankTitle}", styleSubtitle);

        // Stats Numbers
        float statX = profileCard.x + 200;
        GUI.Label(new Rect(statX, profileCard.y + 16, 100, 20), $"Games: {ChessStats.GamesPlayed}", styleCardHeader);
        GUI.Label(new Rect(statX, profileCard.y + 44, 100, 20), $"Win Rate: {ChessStats.WinRate:F1}%", styleSubtitle);

        float statX2 = statX + 115;
        GUI.Label(new Rect(statX2, profileCard.y + 16, 120, 20), $"W: {ChessStats.Wins}  L: {ChessStats.Losses}", styleCardHeader);
        GUI.Label(new Rect(statX2, profileCard.y + 44, 120, 20), $"Streak: {ChessStats.CurrentStreak} (Best {ChessStats.BestStreak})", styleSubtitle);
        GUI.Label(new Rect(profileCard.x + 20, profileCard.y + 67, panelW - 40, 16),
            "Supabase: " + onlineManager.SupabaseAccountLabel, styleSubtitle);

        curY += 96;

        if (GUI.Button(new Rect(panelX + 4, curY, 100, 24), "Local Matches",
            leaderboardView == 0 ? styleAccentButton : styleSmallButton))
            leaderboardView = 0;
        if (GUI.Button(new Rect(panelX + 108, curY, 110, 24), "Cloud Ratings",
            leaderboardView == 1 ? styleAccentButton : styleSmallButton))
        {
            leaderboardView = 1;
            onlineManager.RefreshSupabaseData();
        }
        if (GUI.Button(new Rect(panelX + 222, curY, 135, 24), "Online History",
            leaderboardView == 2 ? styleAccentButton : styleSmallButton))
        {
            leaderboardView = 2;
            onlineManager.RefreshSupabaseData();
        }
        if (GUI.Button(new Rect(panelX + panelW - 92, curY, 88, 24), "Refresh",
            styleSmallButton))
            onlineManager.RefreshSupabaseData();
        curY += 28;

        Rect tableBox = new Rect(panelX, curY, panelW, 200);
        GUI.DrawTexture(tableBox, texDarkGlass);

        if (leaderboardView == 1)
        {
            GUI.Label(new Rect(panelX + 10, curY + 6, 45, 20), "Rank", styleSubtitle);
            GUI.Label(new Rect(panelX + 62, curY + 6, 190, 20), "Player", styleSubtitle);
            GUI.Label(new Rect(panelX + 270, curY + 6, 70, 20), "ELO", styleSubtitle);
            GUI.Label(new Rect(panelX + 350, curY + 6, 75, 20), "Games", styleSubtitle);
            GUI.Label(new Rect(panelX + 430, curY + 6, 90, 20), "W / D / L", styleSubtitle);
        }
        else if (leaderboardView == 2)
        {
            GUI.Label(new Rect(panelX + 10, curY + 6, 100, 20), "Result", styleSubtitle);
            GUI.Label(new Rect(panelX + 125, curY + 6, 125, 20), "Your Color", styleSubtitle);
            GUI.Label(new Rect(panelX + 265, curY + 6, 70, 20), "Moves", styleSubtitle);
            GUI.Label(new Rect(panelX + 360, curY + 6, 140, 20), "Completed", styleSubtitle);
        }
        else
        {
            GUI.Label(new Rect(panelX + 10, curY + 6, 60, 20), "Result", styleSubtitle);
            GUI.Label(new Rect(panelX + 80, curY + 6, 110, 20), "Opponent", styleSubtitle);
            GUI.Label(new Rect(panelX + 200, curY + 6, 95, 20), "Outcome", styleSubtitle);
            GUI.Label(new Rect(panelX + 300, curY + 6, 65, 20), "Moves", styleSubtitle);
            GUI.Label(new Rect(panelX + 375, curY + 6, 65, 20), "Rating", styleSubtitle);
            GUI.Label(new Rect(panelX + 445, curY + 6, 80, 20), "Date", styleSubtitle);
        }

        Rect scrollRect = new Rect(panelX + 6, curY + 28, panelW - 12, 166);
        List<MatchRecord> list = ChessStats.RecentMatches;
        int remoteCount = leaderboardView == 1
            ? onlineManager.SupabaseLeaderboard.Count
            : onlineManager.SupabaseMatchHistory.Count;
        int rowCount = leaderboardView == 0 ? list.Count : remoteCount;
        Rect viewRect = new Rect(0, 0, panelW - 30, Mathf.Max(166, rowCount * 24));

        leaderboardScroll = GUI.BeginScrollView(scrollRect, leaderboardScroll, viewRect);
        if (rowCount == 0)
        {
            string emptyMessage = leaderboardView == 0
                ? "No local matches played yet."
                : onlineManager.IsSupabaseSignedIn
                    ? "No confirmed online data yet. Both players must confirm completed matches."
                    : "Sign in from Play Online to load Supabase data.";
            GUI.Label(new Rect(20, 60, panelW - 40, 24), emptyMessage, styleSubtitle);
        }
        else if (leaderboardView == 1)
        {
            List<SupabaseRest.Profile> cloudProfiles = onlineManager.SupabaseLeaderboard;
            for (int i = 0; i < cloudProfiles.Count; i++)
            {
                SupabaseRest.Profile profile = cloudProfiles[i];
                float rowY = i * 24;
                GUI.Label(new Rect(4, rowY, 45, 20), (i + 1).ToString(), styleCardHeader);
                GUI.Label(new Rect(56, rowY, 190, 20),
                    string.IsNullOrEmpty(profile.display_name) ? "Player" : profile.display_name, styleMoveLog);
                GUI.Label(new Rect(264, rowY, 70, 20), profile.rating.ToString(), styleSubtitle);
                GUI.Label(new Rect(344, rowY, 75, 20), profile.games_played.ToString(), styleSubtitle);
                GUI.Label(new Rect(424, rowY, 90, 20),
                    profile.wins + " / " + profile.draws + " / " + profile.losses, styleSubtitle);
            }
        }
        else if (leaderboardView == 2)
        {
            List<SupabaseRest.MatchHistoryEntry> cloudHistory = onlineManager.SupabaseMatchHistory;
            for (int i = 0; i < cloudHistory.Count; i++)
            {
                SupabaseRest.MatchHistoryEntry entry = cloudHistory[i];
                if (entry == null || entry.match == null) continue;
                float rowY = i * 24;
                string result = entry.match.result ?? "unknown";
                GUI.Label(new Rect(4, rowY, 110, 20), result.Replace('_', ' '), styleCardHeader);
                GUI.Label(new Rect(119, rowY, 125, 20), entry.color ?? "", styleMoveLog);
                GUI.Label(new Rect(259, rowY, 70, 20), entry.match.move_count.ToString(), styleSubtitle);
                string completed = entry.match.finished_at ?? string.Empty;
                if (completed.Length > 10) completed = completed.Substring(0, 10);
                GUI.Label(new Rect(354, rowY, 150, 20), completed, styleSubtitle);
            }
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
            {
                MatchRecord m = list[i];
                float rowY = i * 24;

                Color resCol = (m.result == "WIN") ? new Color(0.4f, 0.9f, 0.5f) : (m.result == "LOSS" ? new Color(0.95f, 0.4f, 0.4f) : Color.yellow);
                GUI.color = resCol;
                GUI.Label(new Rect(4, rowY, 65, 20), m.result, styleCardHeader);
                GUI.color = Color.white;

                GUI.Label(new Rect(74, rowY, 110, 20), m.opponent, styleMoveLog);
                GUI.Label(new Rect(194, rowY, 95, 20), m.detail, styleSubtitle);
                GUI.Label(new Rect(294, rowY, 65, 20), $"{m.totalMoves}", styleSubtitle);

                string deltaStr = (m.eloDelta > 0) ? $"+{m.eloDelta}" : $"{m.eloDelta}";
                GUI.Label(new Rect(369, rowY, 65, 20), deltaStr, styleSubtitle);
                GUI.Label(new Rect(439, rowY, 80, 20), m.date, styleSubtitle);
            }
        }
        GUI.EndScrollView();

        curY += 215;

        // Bottom buttons
        if (GUI.Button(new Rect(panelX, curY, 150, 38), "Clear Records", styleSmallButton))
        {
            ChessStats.ResetAllStats();
        }
        if (GUI.Button(new Rect(panelX + panelW - 160, curY, 160, 38), "Back", styleAccentButton))
        {
            currentScreen = previousScreen;
        }
    }

    #endregion

    #region Match Setup Modal

    private void DrawNewGameModal()
    {
        float mw = 440;
        float mh = 410;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texDarkGlass);
        GUI.DrawTexture(modalRect, texCardBg);
        GUI.DrawTexture(new Rect(modalRect.x, modalRect.y, modalRect.width, 4), texAccent);

        GUI.Label(new Rect(modalRect.x, modalRect.y + 16, mw, 28), "Match Setup", styleModalTitle);

        float curY = modalRect.y + 55;

        // Mode Selection
        GUI.Label(new Rect(modalRect.x + 24, curY, 100, 22), "Game Mode:", styleSubtitle);
        if (GUI.Button(new Rect(modalRect.x + 130, curY, 120, 26), "vs Computer", (gameMode == GameMode.PlayerVsAI) ? styleAccentButton : styleSmallButton))
            gameMode = GameMode.PlayerVsAI;
        if (GUI.Button(new Rect(modalRect.x + 260, curY, 130, 26), "2-Player Local", (gameMode == GameMode.LocalTwoPlayer) ? styleAccentButton : styleSmallButton))
            gameMode = GameMode.LocalTwoPlayer;

        // Difficulty (if vs AI)
        curY += 40;
        if (gameMode == GameMode.PlayerVsAI)
        {
            GUI.Label(new Rect(modalRect.x + 24, curY, 100, 22), "AI Level:", styleSubtitle);
            if (GUI.Button(new Rect(modalRect.x + 130, curY, 80, 26), "Easy", (aiDifficulty == AIDifficulty.Easy) ? styleAccentButton : styleSmallButton))
                aiDifficulty = AIDifficulty.Easy;
            if (GUI.Button(new Rect(modalRect.x + 218, curY, 80, 26), "Medium", (aiDifficulty == AIDifficulty.Medium) ? styleAccentButton : styleSmallButton))
                aiDifficulty = AIDifficulty.Medium;
            if (GUI.Button(new Rect(modalRect.x + 306, curY, 80, 26), "Hard", (aiDifficulty == AIDifficulty.Hard) ? styleAccentButton : styleSmallButton))
                aiDifficulty = AIDifficulty.Hard;
        }

        // Play As
        curY += 40;
        if (gameMode == GameMode.PlayerVsAI)
        {
            GUI.Label(new Rect(modalRect.x + 24, curY, 100, 22), "Play As:", styleSubtitle);
            if (GUI.Button(new Rect(modalRect.x + 130, curY, 80, 26), "White", (humanColor == PlayerColor.White) ? styleAccentButton : styleSmallButton))
                humanColor = PlayerColor.White;
            if (GUI.Button(new Rect(modalRect.x + 218, curY, 80, 26), "Black", (humanColor == PlayerColor.Black) ? styleAccentButton : styleSmallButton))
                humanColor = PlayerColor.Black;
            if (GUI.Button(new Rect(modalRect.x + 306, curY, 80, 26), "Random", (humanColor == PlayerColor.Random) ? styleAccentButton : styleSmallButton))
                humanColor = PlayerColor.Random;
        }

        // Time Control
        curY += 40;
        GUI.Label(new Rect(modalRect.x + 24, curY, 100, 22), "Time Clock:", styleSubtitle);
        if (GUI.Button(new Rect(modalRect.x + 130, curY, 60, 26), "Casual", (timeControl == TimeControlType.Unlimited) ? styleAccentButton : styleSmallButton))
            timeControl = TimeControlType.Unlimited;
        if (GUI.Button(new Rect(modalRect.x + 196, curY, 48, 26), "1m", (timeControl == TimeControlType.OneMinute) ? styleAccentButton : styleSmallButton))
            timeControl = TimeControlType.OneMinute;
        if (GUI.Button(new Rect(modalRect.x + 248, curY, 48, 26), "3m", (timeControl == TimeControlType.ThreeMinutes) ? styleAccentButton : styleSmallButton))
            timeControl = TimeControlType.ThreeMinutes;
        if (GUI.Button(new Rect(modalRect.x + 300, curY, 48, 26), "5m", (timeControl == TimeControlType.FiveMinutes) ? styleAccentButton : styleSmallButton))
            timeControl = TimeControlType.FiveMinutes;
        if (GUI.Button(new Rect(modalRect.x + 352, curY, 48, 26), "10m", (timeControl == TimeControlType.TenMinutes) ? styleAccentButton : styleSmallButton))
            timeControl = TimeControlType.TenMinutes;

        // Confirm buttons
        curY += 65;
        if (GUI.Button(new Rect(modalRect.x + 40, curY, 160, 44), "Start Game", styleAccentButton))
        {
            StartNewGame();
        }
        if (GUI.Button(new Rect(modalRect.x + 240, curY, 160, 44), "Cancel", styleSmallButton))
        {
            currentScreen = previousScreen;
        }
    }

    #endregion

    #region Quit Confirm Modal

    private void DrawQuitConfirmModal()
    {
        float mw = 420;
        float mh = 220;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texDarkGlass);
        GUI.DrawTexture(modalRect, texCardBg);
        GUI.DrawTexture(new Rect(modalRect.x, modalRect.y, modalRect.width, 4), texAccent);

        GUI.Label(new Rect(modalRect.x, modalRect.y + 24, mw, 32), "QUIT GAME?", styleModalTitle);
        GUI.Label(new Rect(modalRect.x + 20, modalRect.y + 70, mw - 40, 24), "Are you sure you want to exit Chess 3D?", styleModalText);

        float curY = modalRect.y + 130;
        float btnW = 160;

        if (GUI.Button(new Rect(modalRect.x + 40, curY, btnW, 44), "Quit to Desktop", styleButton))
        {
            ExecuteQuit();
        }
        if (GUI.Button(new Rect(modalRect.x + 220, curY, btnW, 44), "Cancel", styleAccentButton))
        {
            currentScreen = previousScreen;
        }
    }

    #endregion

    #region Pawn Promotion & Game Over Modals

    private void DrawPromotionModal()
    {
        float mw = 360;
        float mh = 140;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texDarkGlass);
        GUI.DrawTexture(modalRect, texCardBg);

        GUI.Label(new Rect(modalRect.x, modalRect.y + 14, mw, 28), "Promote Pawn", styleModalTitle);

        PieceType[] choices = { PieceType.Queen, PieceType.Knight, PieceType.Rook, PieceType.Bishop };
        string[] symbols = { "Queen (Q)", "Knight (N)", "Rook (R)", "Bishop (B)" };
        float bWidth = 75;

        for (int i = 0; i < choices.Length; i++)
        {
            Rect btnRect = new Rect(modalRect.x + 18 + i * (bWidth + 8), modalRect.y + 60, bWidth, 54);
            if (GUI.Button(btnRect, symbols[i], styleButton))
            {
                CompletePromotion(choices[i]);
            }
        }
    }

    private void DrawGameOverModal()
    {
        float mw = 440;
        float mh = gameMode == GameMode.OnlineTwoPlayer ? 286 : 250;
        Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texDarkGlass);
        GUI.DrawTexture(modalRect, texCardBg);
        GUI.DrawTexture(new Rect(modalRect.x, modalRect.y, modalRect.width, 4), texAccent);

        string header = "GAME OVER";
        if (gameResult == GameResult.WhiteCheckmate || gameResult == GameResult.BlackCheckmate) header = "CHECKMATE";
        else if (gameResult == GameResult.Stalemate) header = "STALEMATE";
        else if (gameResult == GameResult.WhiteTimeout || gameResult == GameResult.BlackTimeout) header = "TIME OUT";

        GUI.Label(new Rect(modalRect.x, modalRect.y + 18, mw, 32), header, styleModalTitle);
        GUI.Label(new Rect(modalRect.x + 20, modalRect.y + 60, mw - 40, 28), statusMessage, styleModalText);

        string statsSummary = gameMode == GameMode.OnlineTwoPlayer
            ? $"Moves: {moveHistory.Count}   •   You played {(onlineManager.LocalPlayerIsWhite ? "White" : "Black")}"
            : $"Moves: {moveHistory.Count}   •   Rating: {ChessStats.Elo} ELO";
        GUI.Label(new Rect(modalRect.x + 20, modalRect.y + 96, mw - 40, 24), statsSummary, styleSubtitle);

        float curY = modalRect.y + 140;
        if (gameMode == GameMode.OnlineTwoPlayer)
        {
            GUI.Label(new Rect(modalRect.x + 20, modalRect.y + 122, mw - 40, 42),
                onlineManager.Status, styleSubtitle);
            curY += 36;
        }
        float btnW = 120;
        float spacing = 10;
        float startX = modalRect.x + (mw - (btnW * 3 + spacing * 2)) * 0.5f;

        string firstAction = gameMode == GameMode.OnlineTwoPlayer ? "Leave Match" : "Play Again";
        if (GUI.Button(new Rect(startX, curY, btnW, 42), firstAction, styleAccentButton))
        {
            if (gameMode == GameMode.OnlineTwoPlayer) OpenMainMenu();
            else StartNewGame();
        }
        if (GUI.Button(new Rect(startX + btnW + spacing, curY, btnW, 42), "Leaderboard", styleButton))
        {
            OpenLeaderboard();
        }
        if (GUI.Button(new Rect(startX + (btnW + spacing) * 2, curY, btnW, 42), "Main Menu", styleSmallButton))
        {
            OpenMainMenu();
        }
    }

    #endregion

    #endregion
}
