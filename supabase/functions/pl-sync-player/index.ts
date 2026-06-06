// Upserts a player row keyed by pl_device_id, merging across this DB and the
// legacy mirror. The heavy lifting (never-shrink MAX/OR, one-time legacy coin
// credit, cross-device wallet rollup) is done atomically in the SQL function
// public.pl_sync_player so the credit-claim and upsert can't desync. This
// edge function only validates/normalizes the client patch and calls it.
//
// Response shape is unchanged from the legacy function: { ok, player }.

import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const INT32_MAX = 2147483647;

function json(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), {
        status,
        headers: {
            "Content-Type": "application/json",
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
        },
    });
}

function clampInt(v: unknown): number | undefined {
    if (typeof v !== "number" || !Number.isFinite(v) || v < 0) return undefined;
    return Math.min(Math.floor(v), INT32_MAX);
}

Deno.serve(async (req: Request) => {
    if (req.method === "OPTIONS") return json({}, 204);
    if (req.method !== "POST") return json({ error: "method not allowed" }, 405);

    let body: any;
    try { body = await req.json(); } catch { return json({ error: "invalid json" }, 400); }

    const deviceId = typeof body.pl_device_id === "string" ? body.pl_device_id.trim() : "";
    if (!deviceId || deviceId.length < 8 || deviceId.length > 128) {
        return json({ error: "missing or invalid pl_device_id" }, 400);
    }

    // Normalized patch passed to the atomic merge function.
    const p: Record<string, unknown> = { pl_device_id: deviceId };
    if (typeof body.pl_wallet === "string" && body.pl_wallet.length > 0) p.pl_wallet = body.pl_wallet;
    const coins = clampInt(body.pl_total_coins); if (coins !== undefined) p.pl_total_coins = coins;
    const high = clampInt(body.pl_high_score); if (high !== undefined) p.pl_high_score = high;
    const plane = clampInt(body.pl_plane_id); if (plane !== undefined) p.pl_plane_id = plane;
    if (typeof body.pl_sound_on === "boolean") p.pl_sound_on = body.pl_sound_on;
    const owned = clampInt(body.pl_planes_owned);
    if (owned !== undefined) p.pl_planes_owned = (owned | 1) & INT32_MAX;

    const supabase = createClient(SUPABASE_URL, SERVICE_KEY);
    const { data, error } = await supabase.rpc("pl_sync_player", { p });
    if (error) return json({ error: `sync error: ${error.message}` }, 500);

    return json({ ok: true, player: data });
});
