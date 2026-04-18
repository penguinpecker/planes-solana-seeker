// Upserts a player row keyed by pl_device_id. The client sends its
// locally-generated UUID (pl_device_id) plus whatever fields it wants to
// persist. On first call the row is created; subsequent calls patch it.
// The client also uses this function to LOAD a player's saved state by
// POSTing {pl_device_id} with no other fields.

import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

function json(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), {
        status,
        headers: {
            "Content-Type": "application/json",
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Headers":
                "authorization, x-client-info, apikey, content-type",
        },
    });
}

type Patch = {
    pl_wallet?: string;
    pl_total_coins?: number;
    pl_high_score?: number;
    pl_plane_id?: number;
    pl_sound_on?: boolean;
};

Deno.serve(async (req: Request) => {
    if (req.method === "OPTIONS") return json({}, 204);
    if (req.method !== "POST") return json({ error: "method not allowed" }, 405);

    let body: any;
    try {
        body = await req.json();
    } catch {
        return json({ error: "invalid json" }, 400);
    }

    const deviceId = typeof body.pl_device_id === "string" ? body.pl_device_id.trim() : "";
    if (!deviceId || deviceId.length < 8 || deviceId.length > 128) {
        return json({ error: "missing or invalid pl_device_id" }, 400);
    }

    const supabase = createClient(SUPABASE_URL, SERVICE_KEY);

    // Build patch from whitelisted fields only. Everything else is ignored.
    const patch: Patch = {};
    if (typeof body.pl_wallet === "string" && body.pl_wallet.length > 0) patch.pl_wallet = body.pl_wallet;
    if (Number.isFinite(body.pl_total_coins) && body.pl_total_coins >= 0) patch.pl_total_coins = Math.floor(body.pl_total_coins);
    if (Number.isFinite(body.pl_high_score) && body.pl_high_score >= 0) patch.pl_high_score = Math.floor(body.pl_high_score);
    if (Number.isFinite(body.pl_plane_id) && body.pl_plane_id >= 0) patch.pl_plane_id = Math.floor(body.pl_plane_id);
    if (typeof body.pl_sound_on === "boolean") patch.pl_sound_on = body.pl_sound_on;

    // Upsert: insert on first hit, merge on subsequent.
    // Use the unique device_id as conflict target so we don't duplicate rows.
    const row = { pl_device_id: deviceId, ...patch };
    const { data, error } = await supabase
        .from("pl_players")
        .upsert(row, { onConflict: "pl_device_id" })
        .select("pl_device_id, pl_wallet, pl_total_coins, pl_high_score, pl_plane_id, pl_sound_on, pl_updated_at")
        .single();

    if (error) {
        return json({ error: `db error: ${error.message}` }, 500);
    }

    return json({ ok: true, player: data });
});
