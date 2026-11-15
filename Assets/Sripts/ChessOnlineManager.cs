using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ChessOnlineManager : MonoBehaviour
{
    private const string SessionType = "ChessOnline";
    private const string GameProperty = "game";
    private const string MatchmakingProperty = "matchmaking";
    private const string SessionIdKey = "Chess_OnlineSessionId";
    private const string SessionHostKey = "Chess_OnlineSessionHost";
    private const string MoveRequestMessage = "ChessMoveRequest";
    private const string MoveMessage = "ChessMove";
    private const string StateRequestMessage = "ChessStateRequest";
    private const string StateMessage = "ChessState";
    private const string ResignRequestMessage = "ChessResignRequest";

    private static readonly int PieceMovePayloadSize = sizeof(int) * 5;

    private ChessBoard board;
    private NetworkManager networkManager;
    private ISession session;
    private bool handlersRegistered;
    private bool reconnecting;

    public string JoinCodeInput { get; set; } = string.Empty;
    public string JoinCode { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Sign in to play online.";
    public bool IsBusy { get; private set; }
    public bool HasSession => session != null;
    public bool IsSupabaseBusy { get; private set; }
    public bool NeedsReconnect => session != null && !session.IsHost &&
        (networkManager == null || !networkManager.IsConnectedClient);
    public bool CanReconnect => !string.IsNullOrEmpty(PlayerPrefs.GetString(SessionIdKey, string.Empty)) &&
        PlayerPrefs.GetInt(SessionHostKey, 0) == 0;
    public bool IsHost => session != null && session.IsHost && networkManager != null && networkManager.IsServer;
    public bool LocalPlayerIsWhite => session != null && session.IsHost;
    public string SupabaseEmailInput { get; set; } = string.Empty;
    public string SupabasePasswordInput { get; set; } = string.Empty;
    public string SupabaseDisplayNameInput { get; set; } = string.Empty;
    public bool IsSupabaseSignedIn => SupabaseRest.IsSignedIn;
    public string SupabaseAccountLabel => SupabaseRest.AccountLabel;
    public string SupabaseStatus => SupabaseRest.Status;
    public List<SupabaseRest.Profile> SupabaseLeaderboard => SupabaseRest.Leaderboard;
    public List<SupabaseRest.MatchHistoryEntry> SupabaseMatchHistory => SupabaseRest.MatchHistory;

    public void Initialize(ChessBoard chessBoard)
    {
        board = chessBoard;
    }

    public void OpenMenu()
    {
        if (session != null)
            Status = session.IsHost ? "Invite a friend with your join code." : "Connected to the online match.";
        else
            Status = "Create an invite, join a code, or find an opponent.";
    }

    public void SetStatus(string status)
    {
        Status = status;
    }

    public async void CreateSupabaseAccount()
    {
        string email = SupabaseEmailInput.Trim();
        string password = SupabasePasswordInput;
        await RunSupabaseOperation(() => SupabaseRest.SignUpAsync(email, password));
        SupabasePasswordInput = string.Empty;
    }

    public async void SignInSupabaseAccount()
    {
        string email = SupabaseEmailInput.Trim();
        string password = SupabasePasswordInput;
        await RunSupabaseOperation(() => SupabaseRest.SignInAsync(email, password));
        SupabasePasswordInput = string.Empty;
    }

    public async void SignOutSupabaseAccount()
    {
        await RunSupabaseOperation(() => SupabaseRest.SignOutAsync());
    }

    public async void SaveSupabaseProfile()
    {
        string displayName = SupabaseDisplayNameInput.Trim();
        await RunSupabaseOperation(() => SupabaseRest.UpdateDisplayNameAsync(displayName));
    }

    public async void RefreshSupabaseData()
    {
        await RunSupabaseOperation(() => SupabaseRest.RefreshCloudDataAsync());
    }

    public async void SubmitCompletedOnlineMatch(string result, string reason, List<string> moves)
    {
        if (session == null || string.IsNullOrEmpty(result))
            return;
        if (!SupabaseRest.IsSignedIn)
        {
            Status = "Online result not saved. Sign in with Supabase before the match ends.";
            return;
        }

        string unitySessionId = session.Id;
        string color = session.IsHost ? "white" : "black";
        try
        {
            string message = await SupabaseRest.ConfirmMatchAsync(unitySessionId, color, result, reason, moves);
            Status = message;
        }
        catch (Exception exception)
        {
            Status = "Online match was not saved: " + exception.Message;
        }
    }

    public async void CreateInviteGame()
    {
        await RunSessionOperation(async () =>
        {
            PrepareNetworkManager();
            await EnsureSignedIn();
            session = await MultiplayerService.Instance.CreateSessionAsync(CreateSessionOptions());
            OnSessionReady("Invite game created. Waiting for an opponent.");
        });
    }

    public async void JoinByCode()
    {
        string code = JoinCodeInput.Trim();
        if (string.IsNullOrEmpty(code))
        {
            Status = "Enter an invite code first.";
            return;
        }

        await RunSessionOperation(async () =>
        {
            PrepareNetworkManager();
            await EnsureSignedIn();
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                code, new JoinSessionOptions { Type = SessionType });
            OnSessionReady("Joined the match. Connecting to the host...");
        });
    }

    public async void QuickMatch()
    {
        await RunSessionOperation(async () =>
        {
            PrepareNetworkManager();
            await EnsureSignedIn();

            var quickJoin = new QuickJoinOptions
            {
                Filters = new List<FilterOption>
                {
                    new FilterOption(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual),
                    new FilterOption(FilterField.StringIndex1, "chess", FilterOperation.Equal),
                    new FilterOption(FilterField.StringIndex2, "quick", FilterOperation.Equal)
                },
                CreateSession = true
            };

            session = await MultiplayerService.Instance.MatchmakeSessionAsync(
                quickJoin, CreateSessionOptions(true));
            OnSessionReady(session.IsHost
                ? "No opponent was available; your match is open for a quick join."
                : "Opponent found. Connecting...");
        });
    }

    public async void ReconnectToPreviousMatch()
    {
        if (!CanReconnect)
        {
            Status = "There is no previous match available to rejoin.";
            return;
        }

        await RunSessionOperation(async () =>
        {
            PrepareNetworkManager();
            await EnsureSignedIn();
            string sessionId = PlayerPrefs.GetString(SessionIdKey, string.Empty);
            await ReconnectOrJoinAsync(sessionId);
            OnSessionReady("Reconnected. Restoring the current board...");
        }, "The host is no longer online or this match has expired. Create or join another match.");
    }

    public void SubmitMove(ChessGame.Move move)
    {
        if (session == null || networkManager == null || !networkManager.IsListening)
        {
            board.SetOnlineMovePending(false);
            Status = "The match connection is unavailable. Rejoin while the host is online.";
            return;
        }

        if (networkManager.IsServer)
        {
            if (board.TryApplyOnlineMove(move, true))
                BroadcastMove(move);
            else
            {
                board.SetOnlineMovePending(false);
                Status = "That move is no longer legal. The board has been restored.";
            }
            return;
        }

        if (!networkManager.IsConnectedClient)
        {
            board.SetOnlineMovePending(false);
            Status = "The connection is recovering. Try the move again after reconnecting.";
            return;
        }

        SendMove(MoveRequestMessage, NetworkManager.ServerClientId, move);
    }

    public void SubmitResignation()
    {
        if (session == null || networkManager == null || !networkManager.IsListening)
        {
            Status = "The match connection is unavailable.";
            return;
        }

        if (networkManager.IsServer)
        {
            board.ApplyOnlineResignation(true);
            BroadcastCurrentState();
            return;
        }

        using (var writer = new FastBufferWriter(sizeof(byte), Allocator.Temp))
            networkManager.CustomMessagingManager.SendNamedMessage(
                ResignRequestMessage, NetworkManager.ServerClientId, writer);
        Status = "Resignation sent to the host.";
    }

    public void BroadcastCurrentState()
    {
        if (networkManager == null || !networkManager.IsServer || !networkManager.IsListening) return;
        string state = board.ExportOnlineState();
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (clientId != networkManager.LocalClientId)
                SendState(clientId, state);
        }
    }

    public async void LeaveMatch()
    {
        ISession sessionToLeave = session;
        session = null;
        JoinCode = string.Empty;

        try
        {
            if (sessionToLeave != null)
                await sessionToLeave.LeaveAsync();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Status = "Could not leave the online session cleanly: " + exception.Message;
        }
        finally
        {
            if (networkManager != null)
            {
                if (handlersRegistered && networkManager.IsListening)
                {
                    networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MoveRequestMessage);
                    networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MoveMessage);
                    networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StateRequestMessage);
                    networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);
                    networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ResignRequestMessage);
                }
                if (networkManager.IsListening) networkManager.Shutdown();
                handlersRegistered = false;
            }

            PlayerPrefs.DeleteKey(SessionIdKey);
            PlayerPrefs.DeleteKey(SessionHostKey);
            PlayerPrefs.Save();
        }
    }

    private async Task RunSessionOperation(Func<Task> operation, string reconnectFailure = null)
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Connecting...";
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Status = reconnectFailure != null
                ? reconnectFailure
                : GetSessionFailureMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string GetSessionFailureMessage(Exception exception)
    {
        SessionException sessionException = exception as SessionException;
        if (sessionException != null &&
            sessionException.Message.IndexOf("session not found", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Session not found. If joining by invite, check the current code and confirm the host " +
                "has not left. Both players must use the same Unity Cloud Project and environment.";
        }

        return "Online play failed: " + exception.Message;
    }

    private async Task RunSupabaseOperation(Func<Task> operation)
    {
        if (IsSupabaseBusy) return;
        IsSupabaseBusy = true;
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            SupabaseRest.SetStatus(exception.Message);
        }
        finally
        {
            IsSupabaseBusy = false;
        }
    }

    private static async Task EnsureSignedIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private static SessionOptions CreateSessionOptions(bool quickMatch = false)
    {
        var properties = new Dictionary<string, SessionProperty>
        {
            { GameProperty, new SessionProperty("chess", VisibilityPropertyOptions.Public, PropertyIndex.String1) },
            {
                MatchmakingProperty,
                new SessionProperty(quickMatch ? "quick" : "invite",
                    VisibilityPropertyOptions.Public, PropertyIndex.String2)
            }
        };
        return new SessionOptions
        {
            Name = "Chess Online",
            Type = SessionType,
            MaxPlayers = 2,
            IsPrivate = false,
            SessionProperties = properties
        }.WithRelayNetwork();
    }

    private void PrepareNetworkManager()
    {
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                var networkObject = new GameObject("Chess Online Network Manager");
                DontDestroyOnLoad(networkObject);
                var transport = networkObject.AddComponent<UnityTransport>();
                networkManager = networkObject.AddComponent<NetworkManager>();
                networkManager.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    EnableSceneManagement = false
                };
            }

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void OnSessionReady(string status)
    {
        if (session == null) return;

        JoinCode = session.Code ?? string.Empty;
        PlayerPrefs.SetString(SessionIdKey, session.Id);
        PlayerPrefs.SetInt(SessionHostKey, session.IsHost ? 1 : 0);
        PlayerPrefs.Save();
        Status = status;

        RegisterMessageHandlers();
        if (networkManager.IsConnectedClient)
            HandleClientConnected(networkManager.LocalClientId);
    }

    private void RegisterMessageHandlers()
    {
        if (handlersRegistered || networkManager == null || !networkManager.IsListening) return;
        CustomMessagingManager messages = networkManager.CustomMessagingManager;
        messages.RegisterNamedMessageHandler(MoveRequestMessage, HandleMoveRequest);
        messages.RegisterNamedMessageHandler(MoveMessage, HandleMove);
        messages.RegisterNamedMessageHandler(StateRequestMessage, HandleStateRequest);
        messages.RegisterNamedMessageHandler(StateMessage, HandleState);
        messages.RegisterNamedMessageHandler(ResignRequestMessage, HandleResignationRequest);
        handlersRegistered = true;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (session == null || networkManager == null || !networkManager.IsListening) return;
        RegisterMessageHandlers();

        if (networkManager.IsServer)
        {
            if (clientId == networkManager.LocalClientId) return;
            if (!board.IsOnlineMatch) board.StartOnlineGame(true);
            Status = "Opponent connected. You are White.";
            SendState(clientId, board.ExportOnlineState());
            return;
        }

        if (clientId == networkManager.LocalClientId)
        {
            if (!board.IsOnlineMatch) board.StartOnlineGame(false);
            Status = "Connected. You are Black.";
            using (var writer = new FastBufferWriter(sizeof(byte), Allocator.Temp))
                networkManager.CustomMessagingManager.SendNamedMessage(
                    StateRequestMessage, NetworkManager.ServerClientId, writer);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager == null) return;
        if (networkManager.IsServer && clientId != networkManager.LocalClientId)
            Status = "Opponent disconnected. They can rejoin while you stay online.";
        else if (!networkManager.IsServer && clientId == networkManager.LocalClientId)
            Status = "Connection lost. Rejoin while the host is online.";
    }

    private void HandleMoveRequest(ulong senderId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || senderId == networkManager.LocalClientId) return;
        if (!TryReadMove(reader, out ChessGame.Move move)) return;

        if (board.TryApplyOnlineMove(move, false))
        {
            BroadcastMove(move);
        }
        else
        {
            SendState(senderId, board.ExportOnlineState());
        }
    }

    private void HandleMove(ulong senderId, FastBufferReader reader)
    {
        if (networkManager.IsServer || senderId != NetworkManager.ServerClientId) return;
        if (!TryReadMove(reader, out ChessGame.Move move)) return;
        if (!board.TryApplyOnlineMove(move, true))
            RequestCurrentState();
    }

    private void HandleStateRequest(ulong senderId, FastBufferReader reader)
    {
        if (networkManager.IsServer && senderId != networkManager.LocalClientId)
            SendState(senderId, board.ExportOnlineState());
    }

    private void HandleState(ulong senderId, FastBufferReader reader)
    {
        if (networkManager.IsServer || senderId != NetworkManager.ServerClientId) return;
        reader.ReadValueSafe(out string state);
        if (!board.ImportOnlineState(state))
            Status = "The host sent an invalid board state. Leave and rejoin the match.";
        else
            Status = "Board restored from the live match.";
    }

    private void HandleResignationRequest(ulong senderId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || senderId == networkManager.LocalClientId) return;
        board.ApplyOnlineResignation(false);
        BroadcastCurrentState();
    }

    private void RequestCurrentState()
    {
        if (networkManager == null || !networkManager.IsListening || networkManager.IsServer) return;
        using (var writer = new FastBufferWriter(sizeof(byte), Allocator.Temp))
            networkManager.CustomMessagingManager.SendNamedMessage(
                StateRequestMessage, NetworkManager.ServerClientId, writer);
    }

    private void BroadcastMove(ChessGame.Move move)
    {
        if (!networkManager.IsServer || !networkManager.IsListening) return;
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (clientId != networkManager.LocalClientId)
                SendMove(MoveMessage, clientId, move);
        }
    }

    private void SendMove(string messageName, ulong clientId, ChessGame.Move move)
    {
        using (var writer = new FastBufferWriter(PieceMovePayloadSize, Allocator.Temp))
        {
            writer.WriteValueSafe(move.from.x);
            writer.WriteValueSafe(move.from.y);
            writer.WriteValueSafe(move.to.x);
            writer.WriteValueSafe(move.to.y);
            writer.WriteValueSafe((int)move.promotion);
            networkManager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer);
        }
    }

    private void SendState(ulong clientId, string state)
    {
        using (var writer = new FastBufferWriter(state.Length * sizeof(char) + 32, Allocator.Temp))
        {
            writer.WriteValueSafe(state);
            networkManager.CustomMessagingManager.SendNamedMessage(StateMessage, clientId, writer);
        }
    }

    private static bool TryReadMove(FastBufferReader reader, out ChessGame.Move move)
    {
        reader.ReadValueSafe(out int fromX);
        reader.ReadValueSafe(out int fromY);
        reader.ReadValueSafe(out int toX);
        reader.ReadValueSafe(out int toY);
        reader.ReadValueSafe(out int promotion);
        move = default;
        if (fromX < 0 || fromX > 7 || fromY < 0 || fromY > 7 ||
            toX < 0 || toX > 7 || toY < 0 || toY > 7 ||
            promotion < 0 || promotion > (int)ChessGame.PieceType.King)
        {
            return false;
        }

        move.from = new Vector2Int(fromX, fromY);
        move.to = new Vector2Int(toX, toY);
        move.promotion = (ChessGame.PieceType)promotion;
        return true;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused && !reconnecting && CanReconnect &&
            NeedsReconnect)
        {
            reconnecting = true;
            ReconnectAfterPause();
        }
    }

    private async void ReconnectAfterPause()
    {
        try
        {
            await EnsureSignedIn();
            await ReconnectOrJoinAsync(PlayerPrefs.GetString(SessionIdKey, string.Empty));
            OnSessionReady("Reconnected. Restoring the current board...");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Status = "The host is no longer online or this match has expired. Create or join another match.";
        }
        finally
        {
            reconnecting = false;
        }
    }

    private async Task ReconnectOrJoinAsync(string sessionId)
    {
        try
        {
            session = await MultiplayerService.Instance.ReconnectToSessionAsync(
                sessionId, new ReconnectSessionOptions { Type = SessionType });
        }
        catch (Exception reconnectException)
        {
            Debug.LogWarning("Reconnect failed; trying to rejoin the same live session by ID. " +
                reconnectException.Message);
            session = await MultiplayerService.Instance.JoinSessionByIdAsync(
                sessionId, new JoinSessionOptions { Type = SessionType });
        }
    }
}

public static class SupabaseRest
{
#pragma warning disable CS0649
    [Serializable]
    private sealed class Config
    {
        public string projectUrl;
        public string publishableKey;
        public string matchFunctionName;
    }

    [Serializable]
    private sealed class AuthResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public AuthUser user;
    }

    [Serializable]
    private sealed class AuthUser
    {
        public string id;
        public string email;
    }

    [Serializable]
    private sealed class ErrorResponse
    {
        public string message;
        public string msg;
        public string error_description;
        public string error;
    }

    [Serializable]
    private sealed class FunctionResponse
    {
        public string status;
        public string message;
    }

    [Serializable]
    private sealed class ArrayWrapper<T>
    {
        public T[] items;
    }

    [Serializable]
    public sealed class Profile
    {
        public string id;
        public string display_name;
        public int rating;
        public int games_played;
        public int wins;
        public int draws;
        public int losses;
    }

    [Serializable]
    public sealed class MatchHistoryEntry
    {
        public string color;
        public RemoteMatch match;
    }

    [Serializable]
    public sealed class RemoteMatch
    {
        public string id;
        public string status;
        public string finished_at;
        public string result;
        public int move_count;
    }
#pragma warning restore CS0649

    private static Config config;
    private static string accessToken;
    private static string refreshToken;
    private static string userId;
    private static string email;
    private static DateTime accessTokenExpiresAt;
    private static Profile currentProfile;
    private static readonly List<Profile> leaderboard = new List<Profile>();
    private static readonly List<MatchHistoryEntry> matchHistory = new List<MatchHistoryEntry>();

    public static bool IsSignedIn => !string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(userId);
    public static string AccountLabel => IsSignedIn
        ? (currentProfile != null && !string.IsNullOrEmpty(currentProfile.display_name)
            ? currentProfile.display_name + " (" + email + ")"
            : email)
        : "Signed out";
    public static string CurrentDisplayName => currentProfile != null ? currentProfile.display_name : string.Empty;
    public static string Status { get; private set; } = "Supabase is not configured.";
    public static List<Profile> Leaderboard => leaderboard;
    public static List<MatchHistoryEntry> MatchHistory => matchHistory;

    public static void SetStatus(string status)
    {
        Status = status;
    }

    public static async Task SignUpAsync(string accountEmail, string password)
    {
        ValidateCredentials(accountEmail, password);
        AuthResponse response = await SendJsonAsync<AuthResponse>(
            "auth/v1/signup", "POST",
            "{\"email\":" + EscapeJson(accountEmail) + ",\"password\":" + EscapeJson(password) + "}",
            false);

        if (response == null || response.user == null)
            throw new InvalidOperationException("Supabase did not return a user. Check the project URL and public key.");

        if (string.IsNullOrEmpty(response.access_token))
        {
            Status = "Account created. Check your email to confirm the address, then sign in.";
            return;
        }

        SetSession(response);
        await RefreshCloudDataAsync();
        Status = "Account created and signed in.";
    }

    public static async Task SignInAsync(string accountEmail, string password)
    {
        ValidateCredentials(accountEmail, password);
        AuthResponse response = await SendJsonAsync<AuthResponse>(
            "auth/v1/token?grant_type=password", "POST",
            "{\"email\":" + EscapeJson(accountEmail) + ",\"password\":" + EscapeJson(password) + "}",
            false);
        SetSession(response);
        await RefreshCloudDataAsync();
        Status = "Signed in. Profile and online match history are up to date.";
    }

    public static async Task SignOutAsync()
    {
        if (!IsSignedIn) return;
        await EnsureFreshTokenAsync();
        try
        {
            await SendJsonAsync<FunctionResponse>("auth/v1/logout", "POST", null, true);
        }
        finally
        {
            ClearSession();
            Status = "Signed out.";
        }
    }

    public static async Task UpdateDisplayNameAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 40)
            throw new InvalidOperationException("Choose a profile name from 1 to 40 characters.");
        await EnsureFreshTokenAsync();
        await SendJsonAsync<Profile[]>(
            "rest/v1/profiles?id=eq." + Uri.EscapeDataString(userId) + "&select=id,display_name,rating,games_played,wins,draws,losses",
            "PATCH", "{\"display_name\":" + EscapeJson(displayName) + "}", true);
        await RefreshCloudDataAsync();
        Status = "Profile name saved.";
    }

    public static async Task RefreshCloudDataAsync()
    {
        if (!IsSignedIn)
            throw new InvalidOperationException("Sign in to load your cloud profile and online leaderboard.");
        await EnsureFreshTokenAsync();

        Profile[] profiles = await SendJsonAsync<Profile[]>(
            "rest/v1/profiles?id=eq." + Uri.EscapeDataString(userId) +
            "&select=id,display_name,rating,games_played,wins,draws,losses",
            "GET", null, true);
        currentProfile = profiles != null && profiles.Length > 0 ? profiles[0] : null;

        Profile[] topProfiles = await SendJsonAsync<Profile[]>(
            "rest/v1/profiles?select=id,display_name,rating,games_played,wins,draws,losses&order=rating.desc&limit=10",
            "GET", null, true);
        leaderboard.Clear();
        if (topProfiles != null) leaderboard.AddRange(topProfiles);

        string historyPath = "rest/v1/match_players?select=color,match:matches!match_players_match_id_fkey(id,status,finished_at,result,move_count)" +
            "&player_id=eq." + Uri.EscapeDataString(userId) +
            "&match.status=eq.completed&order=confirmed_at.desc&limit=10";
        MatchHistoryEntry[] history = await SendJsonAsync<MatchHistoryEntry[]>(
            historyPath, "GET", null, true);
        matchHistory.Clear();
        if (history != null) matchHistory.AddRange(history);
        Status = "Cloud profile, leaderboard, and online match history loaded.";
    }

    public static async Task<string> ConfirmMatchAsync(
        string unitySessionId, string playerColor, string result, string reason, List<string> moves)
    {
        if (!IsSignedIn)
            throw new InvalidOperationException("Sign in before submitting an online match.");
        if (string.IsNullOrEmpty(unitySessionId) || unitySessionId.Length > 128 ||
            (playerColor != "white" && playerColor != "black") ||
            (result != "white_win" && result != "black_win" && result != "draw") ||
            string.IsNullOrEmpty(reason) || reason.Length > 32 ||
            moves == null || moves.Count > 2000)
        {
            throw new InvalidOperationException("The completed match data is incomplete or invalid.");
        }

        StringBuilder json = new StringBuilder();
        json.Append("{\"unity_session_id\":").Append(EscapeJson(unitySessionId));
        json.Append(",\"player_color\":").Append(EscapeJson(playerColor));
        json.Append(",\"result\":").Append(EscapeJson(result));
        json.Append(",\"reason\":").Append(EscapeJson(reason));
        json.Append(",\"moves\":[");
        for (int i = 0; i < moves.Count; i++)
        {
            if (string.IsNullOrEmpty(moves[i]) || moves[i].Length > 16)
                throw new InvalidOperationException("The online move history contains an invalid move.");
            if (i > 0) json.Append(',');
            json.Append(EscapeJson(moves[i]));
        }
        json.Append("]}");

        await EnsureFreshTokenAsync();
        FunctionResponse response = await SendJsonAsync<FunctionResponse>(
            "functions/v1/" + GetConfig().matchFunctionName, "POST", json.ToString(), true);
        if (response == null)
            throw new InvalidOperationException("The match service returned an empty response.");
        if (response.status == "completed")
        {
            try
            {
                await RefreshCloudDataAsync();
                return "Both players confirmed this result. The online match and ratings were saved.";
            }
            catch (Exception exception)
            {
                return "The match and ratings were saved, but cloud data could not refresh: " + exception.Message;
            }
        }
        if (response.status == "disputed")
            return "The match confirmations did not agree. It was not rated.";
        return "Your result was recorded; it will be saved only after the other player confirms the same result and moves.";
    }

    private static void ValidateCredentials(string accountEmail, string password)
    {
        if (string.IsNullOrWhiteSpace(accountEmail) || !accountEmail.Contains("@"))
            throw new InvalidOperationException("Enter a valid email address.");
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Enter your password.");
    }

    private static void SetSession(AuthResponse response)
    {
        if (response == null || response.user == null ||
            string.IsNullOrEmpty(response.user.id) || string.IsNullOrEmpty(response.access_token))
        {
            throw new InvalidOperationException(
                "No active sign-in session was returned. If email confirmation is enabled, confirm your address and sign in.");
        }

        accessToken = response.access_token;
        refreshToken = response.refresh_token;
        userId = response.user.id;
        email = response.user.email ?? string.Empty;
        accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(response.expires_in > 0 ? response.expires_in : 3600);
    }

    private static void ClearSession()
    {
        accessToken = null;
        refreshToken = null;
        userId = null;
        email = null;
        currentProfile = null;
        leaderboard.Clear();
        matchHistory.Clear();
        accessTokenExpiresAt = DateTime.MinValue;
    }

    private static async Task EnsureFreshTokenAsync()
    {
        if (!IsSignedIn)
            throw new InvalidOperationException("Sign in to use Supabase cloud features.");
        if (accessTokenExpiresAt > DateTime.UtcNow.AddMinutes(2)) return;
        if (string.IsNullOrEmpty(refreshToken))
        {
            ClearSession();
            throw new InvalidOperationException("Your sign-in expired. Sign in again.");
        }

        try
        {
            AuthResponse response = await SendJsonAsync<AuthResponse>(
                "auth/v1/token?grant_type=refresh_token", "POST",
                "{\"refresh_token\":" + EscapeJson(refreshToken) + "}", false);
            SetSession(response);
        }
        catch
        {
            ClearSession();
            throw new InvalidOperationException("Your sign-in expired. Sign in again.");
        }
    }

    private static Config GetConfig()
    {
        if (config != null) return config;
        TextAsset asset = Resources.Load<TextAsset>("supabase-config");
        if (asset == null)
            throw new InvalidOperationException(
                "Add the Supabase project URL and publishable key to Assets/Resources/supabase-config.json.");

        Config loaded;
        try
        {
            loaded = JsonUtility.FromJson<Config>(asset.text);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("supabase-config.json is not valid JSON.");
        }
        Uri projectUri;
        if (loaded == null ||
            !Uri.TryCreate(loaded.projectUrl, UriKind.Absolute, out projectUri) ||
            projectUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(loaded.publishableKey))
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Enter the HTTPS project URL and public publishable/anon key in supabase-config.json.");
        }

        loaded.projectUrl = loaded.projectUrl.Trim().TrimEnd('/');
        loaded.publishableKey = loaded.publishableKey.Trim();
        if (string.IsNullOrWhiteSpace(loaded.matchFunctionName))
            loaded.matchFunctionName = "confirm-online-match";
        for (int i = 0; i < loaded.matchFunctionName.Length; i++)
        {
            char character = loaded.matchFunctionName[i];
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                throw new InvalidOperationException("The Supabase match function name is invalid.");
        }
        config = loaded;
        return config;
    }

    private static async Task<T> SendJsonAsync<T>(
        string path, string method, string body, bool authenticated)
    {
        Config settings = GetConfig();
        using (UnityWebRequest request = new UnityWebRequest(settings.projectUrl + "/" + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("apikey", settings.publishableKey);
            if (authenticated)
            {
                if (!IsSignedIn)
                    throw new InvalidOperationException("Sign in to use Supabase cloud features.");
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            }

            await request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(FormatError(request.responseCode, request.downloadHandler.text));
            if (string.IsNullOrEmpty(request.downloadHandler.text))
                return default(T);
            if (typeof(T).IsArray)
                return ParseJsonArray<T>(request.downloadHandler.text);
            T response = JsonUtility.FromJson<T>(request.downloadHandler.text);
            if (response == null)
                throw new InvalidOperationException("Supabase returned an unreadable response.");
            return response;
        }
    }

    private static T ParseJsonArray<T>(string json)
    {
        string wrapped = "{\"items\":" + json + "}";
        if (typeof(T) == typeof(Profile[]))
        {
            ArrayWrapper<Profile> parsed = JsonUtility.FromJson<ArrayWrapper<Profile>>(wrapped);
            return (T)(object)(parsed != null && parsed.items != null ? parsed.items : new Profile[0]);
        }
        if (typeof(T) == typeof(MatchHistoryEntry[]))
        {
            ArrayWrapper<MatchHistoryEntry> parsed = JsonUtility.FromJson<ArrayWrapper<MatchHistoryEntry>>(wrapped);
            return (T)(object)(parsed != null && parsed.items != null ? parsed.items : new MatchHistoryEntry[0]);
        }
        throw new InvalidOperationException("Supabase returned an unsupported data response.");
    }

    private static string FormatError(long statusCode, string responseBody)
    {
        if (!string.IsNullOrEmpty(responseBody))
        {
            try
            {
                ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(responseBody);
                if (error != null)
                {
                    if (!string.IsNullOrEmpty(error.message)) return error.message;
                    if (!string.IsNullOrEmpty(error.msg)) return error.msg;
                    if (!string.IsNullOrEmpty(error.error_description)) return error.error_description;
                    if (!string.IsNullOrEmpty(error.error)) return error.error;
                }
            }
            catch (Exception)
            {
                // Use the status-only message below when the server response is not a JSON error.
            }
        }
        return statusCode > 0
            ? "Supabase request failed (HTTP " + statusCode + ")."
            : "Could not reach Supabase. Check the network and project URL.";
    }

    private static string EscapeJson(string value)
    {
        StringBuilder escaped = new StringBuilder(value.Length + 2);
        escaped.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            switch (character)
            {
                case '"': escaped.Append("\\\""); break;
                case '\\': escaped.Append("\\\\"); break;
                case '\b': escaped.Append("\\b"); break;
                case '\f': escaped.Append("\\f"); break;
                case '\n': escaped.Append("\\n"); break;
                case '\r': escaped.Append("\\r"); break;
                case '\t': escaped.Append("\\t"); break;
                default:
                    if (character < 0x20)
                        escaped.Append("\\u").Append(((int)character).ToString("x4"));
                    else
                        escaped.Append(character);
                    break;
            }
        }
        escaped.Append('"');
        return escaped.ToString();
    }
}
