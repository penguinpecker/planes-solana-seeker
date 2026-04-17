// Verifies an on-chain Solana transfer before crediting a leaderboard row.
// The client submits { pl_wallet, pl_score, pl_tx_signature, pl_cluster }.
// This function calls the matching cluster's RPC to assert that
//   - the signature exists and is at least `confirmed`,
//   - it contains a SystemProgram transfer from pl_wallet to the merchant
//     wallet for >= MIN_LAMPORTS (default 0.01 SOL = 10,000,000 lamports),
// then inserts the row with the service role (RLS blocks anon inserts).
// verify_jwt is off: auth is done on-chain, not via a JWT.

import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2";

const SOL_LAMPORTS = 1_000_000_000;
const MERCHANT_WALLET = Deno.env.get("PLANES_MERCHANT_WALLET") ?? "DfMxre4cKmvogbLrPigxmibVTTQDuzjdXojWzjCXXhzj";
const MIN_LAMPORTS = Number(Deno.env.get("PLANES_MIN_LAMPORTS") ?? `${Math.floor(0.01 * SOL_LAMPORTS)}`);
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

const CLUSTER_RPC: Record<string, string> = {
  "devnet": "https://api.devnet.solana.com",
  "mainnet-beta": "https://api.mainnet-beta.solana.com",
  "testnet": "https://api.testnet.solana.com",
};

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

async function fetchTx(rpcUrl: string, signature: string): Promise<any> {
  // getTransaction can return null if the slot hasn't been confirmed yet;
  // three short retries smooth over typical RPC lag without hanging forever.
  for (let attempt = 0; attempt < 3; attempt++) {
    const resp = await fetch(rpcUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: attempt + 1,
        method: "getTransaction",
        params: [
          signature,
          { encoding: "jsonParsed", maxSupportedTransactionVersion: 0, commitment: "confirmed" },
        ],
      }),
    });
    if (!resp.ok) {
      await new Promise((r) => setTimeout(r, 750));
      continue;
    }
    const body = await resp.json();
    if (body?.result) return body.result;
    await new Promise((r) => setTimeout(r, 750));
  }
  return null;
}

function hasMatchingTransfer(tx: any, fromWallet: string): boolean {
  const topLevel = tx?.transaction?.message?.instructions ?? [];
  const inner = (tx?.meta?.innerInstructions ?? []).flatMap((ii: any) => ii.instructions ?? []);
  const all = [...topLevel, ...inner];
  for (const ix of all) {
    if (ix?.program !== "system") continue;
    if (ix?.parsed?.type !== "transfer") continue;
    const info = ix.parsed.info ?? {};
    if (info.source !== fromWallet) continue;
    if (info.destination !== MERCHANT_WALLET) continue;
    const lamports = Number(info.lamports);
    if (!Number.isFinite(lamports) || lamports < MIN_LAMPORTS) continue;
    return true;
  }
  return false;
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

  const wallet = typeof body.pl_wallet === "string" ? body.pl_wallet.trim() : "";
  const score = Number(body.pl_score);
  const txSig = typeof body.pl_tx_signature === "string" ? body.pl_tx_signature.trim() : "";
  const cluster = typeof body.pl_cluster === "string" ? body.pl_cluster.trim() : "devnet";

  if (!wallet || !txSig) return json({ error: "missing wallet or tx signature" }, 400);
  if (!Number.isFinite(score) || score < 0) return json({ error: "invalid score" }, 400);

  const rpcUrl = CLUSTER_RPC[cluster];
  if (!rpcUrl) return json({ error: "unknown cluster" }, 400);

  const tx = await fetchTx(rpcUrl, txSig);
  if (!tx) return json({ error: `tx not found on ${cluster}` }, 404);

  if (!hasMatchingTransfer(tx, wallet)) {
    return json({
      error: `tx does not contain a ${MIN_LAMPORTS}-lamport system transfer from ${wallet} to ${MERCHANT_WALLET}`,
    }, 400);
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_KEY);
  const { error } = await supabase.from("pl_leaderboard").insert({
    pl_wallet: wallet,
    pl_score: Math.floor(score),
    pl_tx_signature: txSig,
    pl_cluster: cluster,
  });

  if (error) {
    // 23505 = unique_violation on pl_tx_signature — player already claimed this sig.
    const code = (error as { code?: string }).code;
    if (code === "23505") return json({ error: "tx signature already submitted" }, 409);
    return json({ error: `db insert failed: ${error.message}` }, 500);
  }

  return json({ ok: true });
});
