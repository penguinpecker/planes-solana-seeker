// Verifies an on-chain SOL transfer for a shop purchase before recording
// the row in pl_purchases. Mirrors pl-submit-score; the only differences
// are the package→lamports table (one of three fixed tiers) and the
// richer row we insert.

import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2";

const SOL_LAMPORTS = 1_000_000_000;
const MERCHANT_WALLET = Deno.env.get("PLANES_MERCHANT_WALLET") ?? "6zuPtQg1rygxNgzZmjn13YB3bqMGiMLbPAWbrbvwJykg";
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

// Must stay in sync with GameManager._consumableNNNNPriceSOL.
const PACKAGES: Record<string, { coin_amount: number; lamports: number }> = {
  coins_1000: { coin_amount: 1000, lamports: Math.floor(0.199 * SOL_LAMPORTS) },
  coins_2000: { coin_amount: 2000, lamports: Math.floor(0.299 * SOL_LAMPORTS) },
  coins_3000: { coin_amount: 3000, lamports: Math.floor(0.399 * SOL_LAMPORTS) },
};

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
  for (let attempt = 0; attempt < 3; attempt++) {
    const resp = await fetch(rpcUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: attempt + 1,
        method: "getTransaction",
        params: [signature, { encoding: "jsonParsed", maxSupportedTransactionVersion: 0, commitment: "confirmed" }],
      }),
    });
    if (!resp.ok) { await new Promise((r) => setTimeout(r, 750)); continue; }
    const body = await resp.json();
    if (body?.result) return body.result;
    await new Promise((r) => setTimeout(r, 750));
  }
  return null;
}

function hasMatchingTransfer(tx: any, fromWallet: string, minLamports: number): number | null {
  const topLevel = tx?.transaction?.message?.instructions ?? [];
  const inner = (tx?.meta?.innerInstructions ?? []).flatMap((ii: any) => ii.instructions ?? []);
  for (const ix of [...topLevel, ...inner]) {
    if (ix?.program !== "system" || ix?.parsed?.type !== "transfer") continue;
    const info = ix.parsed.info ?? {};
    if (info.source !== fromWallet) continue;
    if (info.destination !== MERCHANT_WALLET) continue;
    const lamports = Number(info.lamports);
    if (!Number.isFinite(lamports) || lamports < minLamports) continue;
    return lamports;
  }
  return null;
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return json({}, 204);
  if (req.method !== "POST") return json({ error: "method not allowed" }, 405);

  let body: any;
  try { body = await req.json(); } catch { return json({ error: "invalid json" }, 400); }

  const wallet = typeof body.pl_wallet === "string" ? body.pl_wallet.trim() : "";
  const pkg = typeof body.pl_package === "string" ? body.pl_package.trim() : "";
  const txSig = typeof body.pl_tx_signature === "string" ? body.pl_tx_signature.trim() : "";
  const cluster = typeof body.pl_cluster === "string" ? body.pl_cluster.trim() : "mainnet-beta";

  if (!wallet || !txSig) return json({ error: "missing wallet or tx signature" }, 400);
  const tier = PACKAGES[pkg];
  if (!tier) return json({ error: `unknown package: ${pkg}` }, 400);
  const rpcUrl = CLUSTER_RPC[cluster];
  if (!rpcUrl) return json({ error: `unknown cluster: ${cluster}` }, 400);

  const tx = await fetchTx(rpcUrl, txSig);
  if (!tx) return json({ error: `tx not found on ${cluster}` }, 404);

  const paidLamports = hasMatchingTransfer(tx, wallet, tier.lamports);
  if (paidLamports == null) {
    return json({
      error: `tx does not contain a >= ${tier.lamports}-lamport system transfer from ${wallet} to ${MERCHANT_WALLET}`,
    }, 400);
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_KEY);
  const { error } = await supabase.from("pl_purchases").insert({
    pl_wallet: wallet,
    pl_package: pkg,
    pl_coin_amount: tier.coin_amount,
    pl_sol_paid: paidLamports / SOL_LAMPORTS,
    pl_tx_signature: txSig,
    pl_cluster: cluster,
  });

  if (error) {
    const code = (error as { code?: string }).code;
    if (code === "23505") return json({ error: "tx signature already submitted" }, 409);
    return json({ error: `db insert failed: ${error.message}` }, 500);
  }

  return json({ ok: true, coins: tier.coin_amount, lamports: paidLamports });
});
