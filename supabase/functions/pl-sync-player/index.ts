// Upserts a player row keyed by pl_device_id. The client sends its
// locally-generated UUID (pl_device_id) plus whatever fields it wants to
// persist. On first call the row is created; subsequent calls patch it.
//
// Merge semantics (important!): coins, high score, and planes_owned
// NEVER shrink. On every sync the server takes the MAX across the
// existing row, the client's patch, and EVERY prior row tied to the
// same wallet. That keeps fresh installs + stale devices from
// overwriting the player's best state, and guarantees that attaching
// a wallet always restores the best coins + ownership we've ever seen
// for that wallet across any device.

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
    pl_planes_owned?: number;
};

const PLAYER_COLUMNS =
    "pl_device_id, pl_wallet, pl_total_coins, pl_high_score, pl_plane_id, pl_sound_on, pl_planes_owned, pl_updated_at";

function maxDefined(...vals: Array<number | null | undefined>): number | undefined {
    let best: number | undefined = undefined;
    for (const v of vals) {
        if (typeof v === "number" && Number.isFinite(v)) {
            if (best === undefined || v > best) best = v;
        }
    }
    return best;
}

function orDefined(...vals: Array<number | null | undefined>): number | undefined {
    let any = false;
    let acc = 0;
    for (const v of vals) {
        if (typeof v === "number" && Number.isFinite(v)) {
            acc |= Math.floor(v);
            any = true;
        }
    }
    return any ? (acc | 1) & 0x7fffffff : undefined;
}

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

    const patch: Patch = {};
    if (typeof body.pl_wallet === "string" && body.pl_wallet.length > 0) patch.pl_wallet = body.pl_wallet;
    if (Number.isFinite(body.pl_total_coins) && body.pl_total_coins >= 0) patch.pl_total_coins = Math.floor(body.pl_total_coins);
    if (Number.isFinite(body.pl_high_score) && body.pl_high_score >= 0) patch.pl_high_score = Math.floor(body.pl_high_score);
    if (Number.isFinite(body.pl_plane_id) && body.pl_plane_id >= 0) patch.pl_plane_id = Math.floor(body.pl_plane_id);
    if (typeof body.pl_sound_on === "boolean") patch.pl_sound_on = body.pl_sound_on;
    if (Number.isFinite(body.pl_planes_owned) && body.pl_planes_owned >= 0) {
        patch.pl_planes_owned = (Math.floor(body.pl_planes_owned) | 1) & 0x7fffffff;
    }

    const { data: existing, error: lookupErr } = await supabase
        .from("pl_players")
        .select(PLAYER_COLUMNS)
        .eq("pl_device_id", deviceId)
        .maybeSingle();
    if (lookupErr) return json({ error: `db lookup error: ${lookupErr.message}` }, 500);

    const effectiveWallet = patch.pl_wallet ?? existing?.pl_wallet ?? null;

    // Aggregate the BEST values across EVERY prior row for this wallet.
    // Picking one row (even the most recent) loses progress when a device
    // that was 10k-coins-strong is sitting on a stale row because a newer
    // install overwrote itself with low numbers.
    let priorMaxCoins: number | undefined;
    let priorMaxHigh: number | undefined;
    let priorOwned: number | undefined;
    let priorPlaneId: number | undefined;
    let priorSoundOn: boolean | undefined;
    if (effectiveWallet) {
        const { data: walletRows, error: walletErr } = await supabase
            .from("pl_players")
            .select(PLAYER_COLUMNS)
            .eq("pl_wallet", effectiveWallet)
            .neq("pl_device_id", deviceId)
            .order("pl_total_coins", { ascending: false })
            .limit(50);
        if (walletErr) return json({ error: `db wallet lookup error: ${walletErr.message}` }, 500);
        if (walletRows && walletRows.length > 0) {
            priorMaxCoins = maxDefined(...walletRows.map((r: any) => r.pl_total_coins));
            priorMaxHigh  = maxDefined(...walletRows.map((r: any) => r.pl_high_score));
            priorOwned    = orDefined(...walletRows.map((r: any) => r.pl_planes_owned));
            // Plane / sound: carry over from the richest row (first after sort).
            priorPlaneId  = walletRows[0].pl_plane_id;
            priorSoundOn  = walletRows[0].pl_sound_on;
        }
    }

    const mergedCoins   = maxDefined(existing?.pl_total_coins, patch.pl_total_coins, priorMaxCoins);
    const mergedHigh    = maxDefined(existing?.pl_high_score,  patch.pl_high_score,  priorMaxHigh);
    const mergedOwned   = orDefined(existing?.pl_planes_owned, patch.pl_planes_owned, priorOwned);
    const mergedPlaneId = patch.pl_plane_id  ?? existing?.pl_plane_id  ?? priorPlaneId;
    const mergedSoundOn = typeof patch.pl_sound_on === "boolean"     ? patch.pl_sound_on
                        : typeof existing?.pl_sound_on === "boolean" ? existing.pl_sound_on
                        : typeof priorSoundOn === "boolean"          ? priorSoundOn
                        : true;
    const mergedWallet  = patch.pl_wallet ?? existing?.pl_wallet ?? null;

    const row: any = { pl_device_id: deviceId };
    if (mergedWallet !== null && mergedWallet !== undefined) row.pl_wallet = mergedWallet;
    if (mergedCoins   !== undefined) row.pl_total_coins  = mergedCoins;
    if (mergedHigh    !== undefined) row.pl_high_score   = mergedHigh;
    if (mergedPlaneId !== undefined) row.pl_plane_id     = mergedPlaneId;
    if (mergedOwned   !== undefined) row.pl_planes_owned = mergedOwned;
    row.pl_sound_on = mergedSoundOn;

    const { data, error } = await supabase
        .from("pl_players")
        .upsert(row, { onConflict: "pl_device_id" })
        .select(PLAYER_COLUMNS)
        .single();
    if (error) return json({ error: `db error: ${error.message}` }, 500);

    return json({ ok: true, player: data });
});
