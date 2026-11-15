import { createClient } from "npm:@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Content-Type": "application/json",
};

const allowedResults = new Set(["white_win", "black_win", "draw"]);
const allowedReasons = new Set([
  "checkmate",
  "resignation",
  "timeout",
  "stalemate",
  "fifty_move",
  "threefold",
  "insufficient_material",
]);
const movePattern = /^[a-hKQRBN0-9x+#=O-]+$/;

Deno.serve(async (request: Request) => {
  if (request.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed." }, 405);
  }

  const authorization = request.headers.get("Authorization") ?? "";
  const bearer = authorization.match(/^Bearer\s+(.+)$/i);
  if (!bearer) {
    return jsonResponse({ error: "Sign in before confirming a match." }, 401);
  }

  const projectUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!projectUrl || !serviceRoleKey) {
    return jsonResponse({ error: "The match service is not configured." }, 503);
  }

  const admin = createClient(projectUrl, serviceRoleKey, {
    auth: { autoRefreshToken: false, persistSession: false },
  });
  const { data: userData, error: authError } = await admin.auth.getUser(bearer[1]);
  if (authError || !userData.user) {
    return jsonResponse({ error: "Your sign-in is invalid or has expired." }, 401);
  }

  let body: Record<string, unknown>;
  try {
    const parsedBody: unknown = await request.json();
    if (parsedBody === null || typeof parsedBody !== "object" || Array.isArray(parsedBody)) {
      return jsonResponse({ error: "Invalid JSON request." }, 400);
    }
    body = parsedBody as Record<string, unknown>;
  } catch {
    return jsonResponse({ error: "Invalid JSON request." }, 400);
  }

  const sessionId = body.unity_session_id;
  const color = body.player_color;
  const result = body.result;
  const reason = body.reason;
  const moves = body.moves;
  if (typeof sessionId !== "string" || sessionId.length < 1 || sessionId.length > 128 ||
      (color !== "white" && color !== "black") ||
      typeof result !== "string" || !allowedResults.has(result) ||
      typeof reason !== "string" || !allowedReasons.has(reason) ||
      !Array.isArray(moves) || moves.length > 2000 ||
      moves.some((move) => typeof move !== "string" || move.length < 1 ||
        move.length > 16 || !movePattern.test(move))) {
    return jsonResponse({ error: "Invalid completed match data." }, 400);
  }

  const { data, error } = await admin.rpc("confirm_completed_chess_match", {
    p_unity_session_id: sessionId,
    p_player_color: color,
    p_result: result,
    p_reason: reason,
    p_moves: moves,
    p_player_id: userData.user.id,
  });
  if (error) {
    return jsonResponse({ error: "The match confirmation could not be saved." }, 400);
  }

  const status = data?.status;
  if (status !== "pending" && status !== "completed" && status !== "disputed") {
    return jsonResponse({ error: "The match service returned an unexpected result." }, 500);
  }
  return jsonResponse({ status });
});

function jsonResponse(body: Record<string, string>, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: corsHeaders });
}
