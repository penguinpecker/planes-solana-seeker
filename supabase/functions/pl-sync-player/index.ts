// Upserts a player row keyed by pl_device_id. The client sends its
// locally-generated UUID (pl_device_id) plus whatever fields it wants to
// persist. On first call the row is created; subsequent calls patch it.
// The client also uses this function to LOAD a player's saved state by
// POSTing {pl_device_id} with no other fields.
//
// Wallet-based reconciliation: if the incoming pl_device_id has NO existing
// row AND a pl_wallet is present, we hunt for a prior row for that wallet
// (most-recent first) and seed the new device's row with its coins, high
// score, and plane-ownership bitmask. This lets a reinstall that reconnects
// the same Solana wallet resume its previous progress.

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
    if (Number.isFinite(body.pl_planes_owned) && body.pl_planes_owned >= 0) {
        // Bit 0 (default plane) is always owned. Cap at 32 bits to keep the
        // value inside the column's signed-integer range.
        patch.pl_planes_owned = (Math.floor(body.pl_planes_owned) | 1) & 0x7fffffff;
    }

    // First, see whether this device already has a row.
    const { data: existing, error: lookupErr } = await supabase
        .from("pl_players")
        .select(PLAYER_COLUMNS)
        .eq("pl_device_id", deviceId)
        .maybeSingle();

    if (lookupErr) {
        return json({ error: `db lookup error: ${lookupErr.message}` }, 500);
    }

    // Decide whether this request should try to INHERIT from a prior device
    // row for the same wallet.
    //
    // Two eligibility gates:
    //   1) Brand-new device and we have a wallet in the payload (classic
    //      "reinstall with wallet connected on first sync" case).
    //   2) Existing row for THIS device is still at defaults (no progress,
    //      default bitmask, null wallet) AND this request is ATTACHING a
    //      wallet for the first time. Covers the more common reinstall
    //      flow where the client syncs once without a wallet (creating an
    //      empty row) and then later connects the wallet.
    //
    // Once a device's row has meaningful progress or an existing wallet,
    // reconciliation stops firing — the row is authoritative from then on.
    const isBlankExisting =
        existing &&
        (existing.pl_total_coins ?? 0) === 0 &&
        (existing.pl_high_score ?? 0) === 0 &&
        ((existing.pl_planes_owned ?? 1) & ~1) === 0 &&
        !existing.pl_wallet;

    const shouldReconcile = !!patch.pl_wallet && (!existing || isBlankExisting);

    if (shouldReconcile) {
        const { data: walletRows, error: walletErr } = await supabase
            .from("pl_players")
            .select(PLAYER_COLUMNS)
            .eq("pl_wallet", patch.pl_wallet)
            .neq("pl_device_id", deviceId)
            .order("pl_updated_at", { ascending: false })
            .limit(1);

        if (walletErr) {
            return json({ error: `db wallet lookup error: ${walletErr.message}` }, 500);
        }

        const prior = walletRows && walletRows[0];
        if (prior) {
            patch.pl_total_coins = Math.max(prior.pl_total_coins ?? 0, patch.pl_total_coins ?? 0);
            patch.pl_high_score = Math.max(prior.pl_high_score ?? 0, patch.pl_high_score ?? 0);
            patch.pl_planes_owned = ((prior.pl_planes_owned ?? 1) | (patch.pl_planes_owned ?? 1)) & 0x7fffffff;
            // Carry over the prior's plane selection if the new device didn't
            // include one (fresh install won't have a meaningful value yet).
            if (patch.pl_plane_id === undefined && typeof prior.pl_plane_id === "number") {
                patch.pl_plane_id = prior.pl_plane_id;
            }
            if (patch.pl_sound_on === undefined && typeof prior.pl_sound_on === "boolean") {
                patch.pl_sound_on = prior.pl_sound_on;
            }
        }
    }

    // Upsert: insert on first hit, merge on subsequent.
    // Use the unique device_id as conflict target so we don't duplicate rows.
    const row = { pl_device_id: deviceId, ...patch };
    const { data, error } = await supabase
        .from("pl_players")
        .upsert(row, { onConflict: "pl_device_id" })
        .select(PLAYER_COLUMNS)
        .single();

    if (error) {
        return json({ error: `db error: ${error.message}` }, 500);
    }

    return json({ ok: true, player: data });
});
