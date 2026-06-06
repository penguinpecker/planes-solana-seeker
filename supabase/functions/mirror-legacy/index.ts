// Pulls the legacy "Gridzero" project's rows (read-only, via its PUBLIC anon
// key) into this project's private legacy_mirror schema, so the merged public
// views can present legacy + native data together. Idempotent + self-healing:
// safe to run on any cadence. Triggered by pg_cron (pg_net) and manually.
//
// Reads legacy via PostgREST keyset pagination (NEVER offset — offset windows
// reshuffle under concurrent legacy writes and silently skip rows). Writes the
// private mirror schema only through SECURITY DEFINER RPCs (legacy_mirror is
// not API-exposed). Liveness is recorded in legacy_mirror.sync_state, not
// cron.job_run_details (which only proves the POST was enqueued).
//
// Secrets: LEGACY_URL, LEGACY_ANON_KEY, MIRROR_SECRET. SUPABASE_URL +
// SUPABASE_SERVICE_ROLE_KEY are auto-injected.

import { createClient } from "npm:@supabase/supabase-js@2";

const LEGACY_URL = Deno.env.get("LEGACY_URL")!;
const LEGACY_ANON = Deno.env.get("LEGACY_ANON_KEY")!;
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const MIRROR_SECRET = Deno.env.get("MIRROR_SECRET") ?? "";

const PAGE = 1000; // PostgREST hard-caps responses at 1000 rows

function legacyHeaders(extra: Record<string, string> = {}) {
  return { apikey: LEGACY_ANON, Authorization: `Bearer ${LEGACY_ANON}`, ...extra };
}

async function legacyCount(table: string): Promise<number> {
  const r = await fetch(`${LEGACY_URL}/rest/v1/${table}?select=*`, {
    headers: legacyHeaders({ Prefer: "count=exact", Range: "0-0" }),
  });
  const cr = r.headers.get("content-range") || ""; // "0-0/529"
  const total = parseInt(cr.split("/")[1] || "0", 10);
  return Number.isFinite(total) ? total : 0;
}

// Keyset pagination on the immutable PK — concurrent legacy inserts can only
// land past the cursor, so no row is ever skipped or duplicated mid-pull.
async function pullAll(table: string, cols: string, pk: string): Promise<any[]> {
  const rows: any[] = [];
  let cursor: string | null = null;
  for (;;) {
    let url = `${LEGACY_URL}/rest/v1/${table}?select=${cols}&order=${pk}.asc&limit=${PAGE}`;
    if (cursor !== null) url += `&${pk}=gt.${encodeURIComponent(cursor)}`;
    const r = await fetch(url, { headers: legacyHeaders() });
    if (!r.ok) throw new Error(`legacy ${table} read ${r.status}: ${await r.text()}`);
    const batch = await r.json();
    if (!Array.isArray(batch) || batch.length === 0) break;
    rows.push(...batch);
    if (batch.length < PAGE) break;
    cursor = batch[batch.length - 1][pk];
  }
  return rows;
}

Deno.serve(async (req) => {
  if (MIRROR_SECRET && req.headers.get("x-mirror-secret") !== MIRROR_SECRET) {
    return new Response(JSON.stringify({ ok: false, error: "unauthorized" }), {
      status: 401, headers: { "Content-Type": "application/json" },
    });
  }

  const sb = createClient(SUPABASE_URL, SERVICE_KEY);
  try {
    const [cP, cL, cU] = await Promise.all([
      legacyCount("pl_players"),
      legacyCount("pl_leaderboard"),
      legacyCount("pl_purchases"),
    ]);

    const players = await pullAll(
      "pl_players",
      "pl_device_id,pl_wallet,pl_total_coins,pl_high_score,pl_plane_id,pl_sound_on,pl_planes_owned,pl_created_at,pl_updated_at",
      "pl_device_id",
    );
    const board = await pullAll(
      "pl_leaderboard",
      "pl_tx_signature,pl_wallet,pl_score,pl_cluster,pl_created_at",
      "pl_tx_signature",
    );
    const purch = await pullAll(
      "pl_purchases",
      "pl_tx_signature,pl_wallet,pl_package,pl_coin_amount,pl_sol_paid,pl_cluster,pl_created_at",
      "pl_tx_signature",
    );

    let upserted = 0;
    const batchUpsert = async (rpc: string, arr: any[]) => {
      for (let i = 0; i < arr.length; i += 500) {
        const slice = arr.slice(i, i + 500);
        const { data, error } = await sb.rpc(rpc, { p: slice });
        if (error) throw new Error(`${rpc}: ${error.message}`);
        upserted += typeof data === "number" ? data : slice.length;
      }
    };
    await batchUpsert("mirror_upsert_players", players);
    await batchUpsert("mirror_upsert_leaderboard", board);
    await batchUpsert("mirror_upsert_purchases", purch);

    // Complete only if we pulled exactly what legacy reported (guards against a
    // partial/RLS-tightened read being mistaken for "done"). The final
    // retirement fold is gated on this being true.
    const complete = players.length === cP && board.length === cL && purch.length === cU;
    const note = `p=${players.length}/${cP} l=${board.length}/${cL} u=${purch.length}/${cU}`;
    const { error: stErr } = await sb.rpc("mirror_record_state", {
      p_players: cP, p_leaderboard: cL, p_purchases: cU,
      p_upserted: upserted, p_complete: complete, p_note: note,
    });
    if (stErr) throw new Error(`mirror_record_state: ${stErr.message}`);

    return new Response(
      JSON.stringify({ ok: true, complete, note, upserted }),
      { headers: { "Content-Type": "application/json" } },
    );
  } catch (e) {
    // record the failed run so the staleness alarm can see last_ok_at lagging
    await sb.rpc("mirror_record_state", {
      p_players: 0, p_leaderboard: 0, p_purchases: 0,
      p_upserted: 0, p_complete: false, p_note: `ERROR: ${(e as Error).message}`,
    }).catch(() => {});
    return new Response(
      JSON.stringify({ ok: false, error: (e as Error).message }),
      { status: 500, headers: { "Content-Type": "application/json" } },
    );
  }
});
