// Broadcasts a push notification to every subscribed Planes install via the
// Firebase Cloud Messaging HTTP v1 API (topic fan-out — no per-device tokens).
//
// The client (PushManager.cs) subscribes each install to the "news" and
// "competitions" topics. This function publishes one message to a topic and
// FCM delivers it to all subscribers, including when the game is closed.
//
// Auth: this function is deployed with verify_jwt = false (so no Supabase JWT
// is needed), but it is gated by a shared admin secret. Callers MUST send
//   x-admin-secret: <PLANES_BROADCAST_SECRET>
// The public Supabase anon key shipped in the APK CANNOT trigger a broadcast.
//
// Request body (application/json):
//   {
//     "title": "Planes Update",                 // required
//     "body":  "New season drop — read the thread", // required
//     "url":   "https://x.com/yourhandle/status/123", // optional; tap opens it
//     "topic": "news",                          // optional, default "news"
//     "type":  "news"                           // optional; default "news", or
//                                                // "competition" for the competitions topic
//   }
// The url rides in the message `data` (not the notification block) so it
// survives the background/killed tap path and PushManager can Application.OpenURL it.
//
// Required environment secrets (set with: supabase secrets set ...):
//   FIREBASE_PROJECT_ID     - e.g. planes-xxxxx
//   FIREBASE_CLIENT_EMAIL   - service-account client_email
//   FIREBASE_PRIVATE_KEY    - service-account private_key (PEM; \n-escaped is fine)
//   PLANES_BROADCAST_SECRET - any long random string; must match x-admin-secret

import "jsr:@supabase/functions-js/edge-runtime.d.ts";

const PROJECT_ID = Deno.env.get("FIREBASE_PROJECT_ID")!;
const CLIENT_EMAIL = Deno.env.get("FIREBASE_CLIENT_EMAIL")!;
const PRIVATE_KEY = normalizePem(Deno.env.get("FIREBASE_PRIVATE_KEY") ?? "");
const ADMIN_SECRET = Deno.env.get("PLANES_BROADCAST_SECRET") ?? "";

const TOKEN_URL = "https://oauth2.googleapis.com/token";
const FCM_SCOPE = "https://www.googleapis.com/auth/firebase.messaging";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Headers":
        "authorization, x-client-info, apikey, content-type, x-admin-secret",
    },
  });
}

// Tolerate the common ways a PEM gets mangled in env config: surrounding
// quotes from a copied JSON value, literal escaped CR, and \n-escaped newlines.
// The happy path (vanilla service_account.json key, LF-only) is unaffected.
function normalizePem(raw: string): string {
  return raw.trim().replace(/^['"]|['"]$/g, "").replace(/\\r/g, "").replace(/\\n/g, "\n");
}

// Constant-time string compare for the admin-secret gate, so the single auth
// check on this send-to-all endpoint doesn't leak per-byte timing.
function safeEq(a: string, b: string): boolean {
  const x = new TextEncoder().encode(a);
  const y = new TextEncoder().encode(b);
  if (x.length !== y.length) return false;
  let r = 0;
  for (let i = 0; i < x.length; i++) r |= x[i] ^ y[i];
  return r === 0;
}

// base64url for strings and raw bytes (no padding, URL-safe alphabet).
function b64url(input: string | ArrayBuffer): string {
  const bytes = typeof input === "string"
    ? new TextEncoder().encode(input)
    : new Uint8Array(input);
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

// Strip the PEM armor and decode the base64 body to the raw PKCS#8 key bytes.
function pemToPkcs8(pem: string): ArrayBuffer {
  const body = pem
    .replace(/-----BEGIN PRIVATE KEY-----/, "")
    .replace(/-----END PRIVATE KEY-----/, "")
    .replace(/\s+/g, "");
  let bin: string;
  try {
    bin = atob(body);
  } catch {
    throw new Error("FIREBASE_PRIVATE_KEY is not valid PEM (check for stray quotes/CR or bad base64)");
  }
  const buf = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
  return buf.buffer;
}

// Mint a short-lived Google OAuth2 access token from the service account by
// signing a JWT (RS256) and exchanging it at the token endpoint.
async function getAccessToken(): Promise<string> {
  const now = Math.floor(Date.now() / 1000);
  const header = { alg: "RS256", typ: "JWT" };
  const claims = {
    iss: CLIENT_EMAIL,
    scope: FCM_SCOPE,
    aud: TOKEN_URL,
    iat: now,
    exp: now + 3600,
  };
  const unsigned = `${b64url(JSON.stringify(header))}.${b64url(JSON.stringify(claims))}`;

  const key = await crypto.subtle.importKey(
    "pkcs8",
    pemToPkcs8(PRIVATE_KEY),
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sig = await crypto.subtle.sign(
    "RSASSA-PKCS1-v1_5",
    key,
    new TextEncoder().encode(unsigned),
  );
  const jwt = `${unsigned}.${b64url(sig)}`;

  const resp = await fetch(TOKEN_URL, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
      assertion: jwt,
    }),
  });
  const data = await resp.json();
  if (!resp.ok) throw new Error(`token exchange failed: ${JSON.stringify(data)}`);
  return data.access_token as string;
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return json({}, 200);
  if (req.method !== "POST") return json({ error: "method not allowed" }, 405);

  if (!ADMIN_SECRET || !safeEq(req.headers.get("x-admin-secret") ?? "", ADMIN_SECRET)) {
    return json({ error: "unauthorized" }, 401);
  }
  if (!PROJECT_ID || !CLIENT_EMAIL || !PRIVATE_KEY) {
    return json({ error: "server missing FIREBASE_* env" }, 500);
  }

  let payload: {
    title?: string;
    body?: string;
    url?: string;
    topic?: string;
    type?: string;
    image?: string;
  };
  try {
    payload = await req.json();
  } catch {
    return json({ error: "invalid json" }, 400);
  }

  const title = typeof payload.title === "string" ? payload.title.trim() : "";
  const body = typeof payload.body === "string" ? payload.body.trim() : "";
  const url = typeof payload.url === "string" ? payload.url.trim() : "";
  const image = typeof payload.image === "string" ? payload.image.trim() : "";
  const topic = (typeof payload.topic === "string" && payload.topic.trim()) || "news";
  // The client routes a no-URL ping on data.type == "competition" (singular).
  // Map the plural "competitions" topic to that so a bare { topic:"competitions" }
  // fires the right path without the caller having to remember an override.
  const DEFAULT_TYPE_BY_TOPIC: Record<string, string> = { competitions: "competition", news: "news" };
  const type = (typeof payload.type === "string" && payload.type.trim()) ||
    DEFAULT_TYPE_BY_TOPIC[topic] || topic;

  if (!title || !body) return json({ error: "title and body are required" }, 400);
  // FCM topic names must match this pattern.
  if (!/^[a-zA-Z0-9-_.~%]{1,900}$/.test(topic)) {
    return json({ error: "invalid topic" }, 400);
  }

  // FCM v1 data values must be strings.
  const data: Record<string, string> = { type };
  if (url) data.url = url;

  // image (optional): full-colour logo/banner shown in the expanded notification
  // (BigPicture). The small status-bar icon stays the monochrome silhouette —
  // Android requires that; only the big image can carry the colour brand.
  const notif: Record<string, string> = { title, body };
  if (image) notif.image = image;
  const message = {
    message: {
      topic,
      notification: notif,
      data,
      android: { priority: "high", ...(image ? { notification: { image } } : {}) },
    },
  };

  let accessToken: string;
  try {
    accessToken = await getAccessToken();
  } catch (e) {
    return json({ error: `auth failed: ${(e as Error).message}` }, 502);
  }

  const fcmResp = await fetch(
    `https://fcm.googleapis.com/v1/projects/${PROJECT_ID}/messages:send`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(message),
    },
  );
  const fcmBody = await fcmResp.json();
  if (!fcmResp.ok) {
    return json({ error: "fcm send failed", status: fcmResp.status, detail: fcmBody }, 502);
  }

  // FCM returns { name: "projects/<id>/messages/<message_id>" } on success.
  return json({ ok: true, topic, sent: fcmBody });
});
