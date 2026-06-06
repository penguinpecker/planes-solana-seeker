# Push notifications (Firebase Cloud Messaging)

Cloud push for Planes: broadcast **news** (with a tappable X/Twitter link) and
**in-game competition** announcements to every install, including when the game
is closed.

## Why FCM (and not a dApp Store feature)

The Solana dApp Store has **no** native notification channel — verified against
`docs.solanamobile.com`, the publishing CLI, and the manifest spec. But the
Seeker is a Google-certified Android phone that ships with Google Play Services,
so standard Android push (FCM) works for a dApp-Store-installed build. Install
source and the presence of Play Services are independent.

Delivery uses **topics**: each install subscribes to `news` and `competitions`,
and the server publishes one message to a topic — FCM fans it out to all
subscribers with no per-device token bookkeeping.

## What's already wired in this repo

| Piece | File | Status |
|---|---|---|
| Receive + subscribe + tap-to-open-URL | `Assets/Script/PushManager.cs` | Done (Firebase calls behind `FIREBASE_MESSAGING_INSTALLED`) |
| Auto-spawned as a singleton | `Assets/Script/GameManager.cs` (`EnsureLeaderboardSingletons`) | Done |
| Notification permission + Firebase activity + forwarding service + icon meta-data | `Assets/Plugins/Android/AndroidManifest.xml` | Done |
| Status-bar icon (white plane, 5 densities) + accent color | `Assets/Plugins/Android/res/` | Done |
| Server send (topic broadcast) | `supabase/functions/pl-broadcast-news/index.ts` | Done (needs deploy + secrets) |

The URL rides in the message **`data`** payload under the `url` key (not the
notification block), and `PushManager` only opens it on an actual **tap**
(`NotificationOpened == true`), so a news message never yanks a player to a
browser mid-run.

> ⚠️ The manifest now references `com.google.firebase.MessagingUnityPlayerActivity`.
> The Android build **will fail until you import the Firebase Unity SDK** (step 2).
> This is expected — do steps 1–3 before the next build.

---

## Step 1 — Firebase project (your Google account; ~5 min)

1. <https://console.firebase.google.com> → **Add project** (e.g. `planes`).
2. **Add app → Android.** Package name: **`com.techavtranew.planes`** (must match exactly).
3. Download **`google-services.json`** → place it in **`Assets/`** (repo root `Assets/`, not a subfolder).
4. Project settings → **Service accounts → Generate new private key.** Save the
   JSON — it's used by the server send function (step 4). Keep it out of git.

## Step 2 — Import the Firebase Unity SDK

1. Download the Firebase Unity SDK (<https://firebase.google.com/download/unity>),
   import **`FirebaseMessaging.unitypackage`** into the project.
   - If Unity asks to **overwrite `Assets/Plugins/Android/AndroidManifest.xml`,
     choose _No / skip_** — our manifest already has the activity + wallet
     `<queries>`. We want exactly one manifest and one activity declaration.
2. **Assets → External Dependency Manager → Android Resolver → Force Resolve.**
   (Mandatory — pulls the native FCM `.aar`s.)
3. Player Settings → **Scripting Define Symbols**: add **`FIREBASE_MESSAGING_INSTALLED`**
   to Android (and Standalone/iOS if you like) — exactly how `SOLANA_SDK_INSTALLED`
   is already set. This lights up the Firebase code in `PushManager.cs`.

## Step 3 — Build & device-test on a real Seeker

Build as usual (`BuildScript.BuildAndroid`). On the device, verify **all** of:

- [ ] First launch shows the Android 13 **notification permission** prompt; grant it.
- [ ] **Wallet still works** — connect + sign via Seed Vault *and* Phantom/Solflare
      (the launcher activity changed; MWA uses a localhost WebSocket so it should
      be unaffected, but confirm the app re-foregrounds after signing).
- [ ] Send a test push (step 5) and confirm it arrives with the app **foregrounded**,
      **backgrounded**, and **fully killed**.
- [ ] **Tapping** a news notification opens the X/Twitter link.

> The cold-start (app-killed) tap path is the one most worth testing — it depends
> on the Firebase activity forwarding the launch intent. There are known
> Unity-6/FCM background-delivery reports, so confirm on-device before relying on it.

## Step 4 — Deploy the server send function

Set the secrets (the private key from step 1; `\n`-escaped is fine), then deploy:

```bash
supabase secrets set \
  FIREBASE_PROJECT_ID="planes-xxxxx" \
  FIREBASE_CLIENT_EMAIL="firebase-adminsdk-xxxx@planes-xxxxx.iam.gserviceaccount.com" \
  FIREBASE_PRIVATE_KEY="-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n" \
  PLANES_BROADCAST_SECRET="<a-long-random-string>"

supabase functions deploy pl-broadcast-news --no-verify-jwt
```

`--no-verify-jwt` matches the other Planes functions; the broadcast is gated by
the `x-admin-secret` header instead, so the public anon key can't trigger it.

## Step 5 — Send a notification

**Option A — your edge function (scriptable / schedulable):**

```bash
curl -X POST "https://dqvwpbggjlcumcmlliuj.supabase.co/functions/v1/pl-broadcast-news" \
  -H "Content-Type: application/json" \
  -H "x-admin-secret: <PLANES_BROADCAST_SECRET>" \
  -d '{
    "title": "New season is live ✈️",
    "body": "Tap to read the thread",
    "url": "https://x.com/yourhandle/status/123",
    "topic": "news"
  }'
```

- News with a link → set `url`, `topic: "news"`.
- Competition ping → `topic: "competitions"` and either include a `url`, or omit
  it and set `"type": "competition"` so the tap just opens the game
  (`PushManager.OnCompetitionOpened` fires for future in-app routing).

**Option B — Firebase console (zero code):** Messaging → **New campaign** →
target topic `news` → under **Additional options** add a custom data key
`url` = the link. Good for one-off manual sends.

---

## Targeted sends (optional, later)

Topic broadcasts cover "news/competitions to everyone." For per-user targeting
you'd capture the FCM token (already logged in `PushManager.OnTokenReceived`),
store it on the player's `pl_players` row via the existing sync flow, and send
to a token list instead of a topic. Not required for broadcasts.

## Before shipping

- **Notification icon — done.** A white plane silhouette `ic_stat_notify` (5
  densities) lives in `Assets/Plugins/Android/res/drawable-*/`, with accent
  color `notif_accent` in `res/values/colors.xml`, both wired via `<meta-data>`
  in the manifest. This is what stops backgrounded/closed-app pings rendering as
  a white square. Regenerate from a different plane sprite anytime by re-running
  the alpha→white-silhouette step against `Assets/Sprites/Plane1.png`.
- **Notification channel (optional).** FCM currently delivers under its
  auto-created `fcm_fallback_notification_channel` (works, but unbranded). To
  name/brand it, register a `NotificationChannel` in code at startup and add a
  `default_notification_channel_id` meta-data pointing at it. Lowest priority.
- **Privacy policy:** you now collect a device push token (via Google Play
  Services). Update the dApp Store listing's privacy/data-collection disclosure.
- Expect a real opt-out rate on the Android 13 permission prompt — denied =
  notifications silently blocked. Consider a soft in-game explainer before the
  system prompt.
- Force-stopped apps / aggressive battery managers drop notifications until the
  app is reopened — an Android limit, not specific to Planes.
