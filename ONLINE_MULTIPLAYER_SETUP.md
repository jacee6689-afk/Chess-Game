# Online chess setup

The project uses Unity Multiplayer Services sessions with Netcode for GameObjects and Relay. The host is one player's device; the host validates every move with the existing chess rules and sends the accepted move or current board state to the other player. AI and pass-and-play remain available offline.

Unity's anonymous Authentication sign-in is used for online transport and is separate from the optional Supabase email/password account. A completed online result is submitted to Supabase only when both participants are signed in and confirm the same result and moves. These cloud ratings do not change the local leaderboard. See [SUPABASE_SETUP.md](SUPABASE_SETUP.md) for project configuration, schema deployment, and the match-confirmation function.

## Unity Cloud configuration

1. This checkout already has a Cloud Project ID in its existing project settings. Verify it points to the intended Unity Cloud Project in **Edit > Project Settings > Services**; do not copy or invent a project ID.
2. In that Cloud Project, enable Authentication, Lobby (used by sessions and Quick Match), and Relay. The game signs in anonymously; no authentication provider configuration is needed for anonymous sign-in. These dashboard/service settings have not been verified from the project checkout.
3. Keep the session disconnect-removal window at least as long as the expected background/reconnect interval. Unity's default is 120 seconds. The client first calls `ReconnectToSessionAsync`; if it has already been removed from the live session, it tries to join the same session ID again. A client cannot rejoin after the host leaves or the session expires.
4. No matchmaking queue is required. Quick Match uses the session quick-join API and filters for one open slot plus indexed `game=chess` and `matchmaking=quick` properties. Invite sessions use `matchmaking=invite`, so quick match won't consume a game reserved for a join code.

The package manifest pins `com.unity.services.multiplayer` 2.4.1 and `com.unity.netcode.gameobjects` 2.14.0. Unity Package Manager must finish resolving these packages before the project can compile.

## Trying a match

- **Create Invite Game** creates a two-player session and displays its join code.
- **Join Game** connects to a session by code.
- **Quick Match** joins an open chess session or creates a two-player session if none is available.
- **Rejoin Previous Match** or returning from app background attempts to restore the previous live session and its current board.

Use two signed-in clients in the same Unity Cloud Project and Unity Services environment to test online play. An invite code points to a live host session: the host application must stay online, and the code must be current. The **Back** button from the online screen leaves the session and invalidates its invite. If a join reports that the session was not found, check the code, confirm the host has not left, and make sure both clients use the same project and environment. Host migration and dedicated servers are not configured.
