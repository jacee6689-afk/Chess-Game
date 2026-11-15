# Supabase setup

The Unity client integration is implemented, but this repository is not connected to a Supabase project. No project URL, key, database, or Edge Function has been supplied or deployed.

## Configure the project

1. Create or select your Supabase project. In **Authentication > Providers > Email**, enable email/password sign-in. Email confirmation is supported; new users may need to confirm their address and sign in again before they get an active session. Configure SMTP before production use.
2. In **Project Settings > API**, copy the project URL and the public publishable key (or legacy `anon` key). Put these in `Assets/Resources/supabase-config.json`:

   ```json
   {
     "projectUrl": "https://YOUR_PROJECT_REF.supabase.co",
     "publishableKey": "YOUR_PUBLIC_PUBLISHABLE_OR_ANON_KEY",
     "matchFunctionName": "confirm-online-match"
   }
   ```

   The config file currently contains empty sample values. The public key is intended for clients; do not put a service-role or secret key here. The email and password entered in the game are kept in memory only, and the Supabase session is not written to `PlayerPrefs` or another local credential store.

3. Install and authenticate the Supabase CLI, link this repository to the selected project, then deploy the versioned schema:

   ```text
   supabase login
   supabase link --project-ref YOUR_PROJECT_REF
   supabase db push
   ```

   `supabase/config.toml` contains only the local CLI project label; it does not select or connect to a remote project. The migration creates `profiles`, `matches`, `match_players`, `match_moves`, `player_ratings`, and `rating_history`. Completed-match history reads the move count from `matches.move_count`. A database trigger creates each profile and rating row from the corresponding Auth user.
4. Deploy the confirmation Edge Function:

   ```text
   supabase functions deploy confirm-online-match
   ```

   Configure `SUPABASE_SERVICE_ROLE_KEY` only in the selected project's server-side Edge Function secrets (Dashboard **Edge Functions > Secrets**). The function verifies the caller's Supabase access token, then calls a database function that is executable only by `service_role`. Never place this key in Unity, the local config, client logs, or source control. Supabase supplies `SUPABASE_URL` to deployed functions.
5. Run the Unity project. **Play Online** contains the account form; **Leaderboard & Player Stats** has separate local-match, cloud-rating, and online-history views. Online play itself remains available without a Supabase account. Cloud submissions require both players to be signed in when the match ends.

## Access and match integrity

All six tables have RLS enabled. The client can read leaderboard profiles, update only its own `display_name`, and read match/rating rows only where the policy allows it. It has no insert/update/delete grants for match, move, rating, or history rows. Ratings are applied only in the server-side database function after two distinct authenticated participants submit the same result, reason, colors, and move list; conflicting reports are marked disputed and are not rated.

The existing game is player-hosted Unity multiplayer, not a trusted chess server. Supabase verifies each account and requires mutual confirmation, but cannot independently prove that the Unity session ID belongs to those users or replay/validate the submitted moves. Players could collude or modify a client. Treat these ratings as casual, not anti-cheat or tournament-certified. A stronger competitive rating system needs a trusted authoritative game server.

The setup and deployment steps do not connect or deploy anything by themselves. A real sign-in, database read/write, Edge Function call, or rating update can only be verified after configuring a Supabase project.
