# Planes — Solana Seeker dApp

A top-down arcade dodger built in Unity 6 and shipped through the Solana
dApp Store. The player flies a plane, collects star coins, dodges
homing missiles, and spends coins (or SOL) to unlock bigger planes.
On-chain leaderboard + wallet-backed progress persistence are wired
through Supabase edge functions.

---

## Architecture at a glance

```
Unity client (C#, IL2CPP Android)        Supabase backend (Deno edge fns)
┌────────────────────────────┐          ┌──────────────────────────────┐
│ SampleScene.unity          │          │ pl_leaderboard (table + view)│
│ ├─ GameManager (singletons)│ HTTPS   │ pl_purchases                  │
│ ├─ Player, Missiles, Stars │ ───────▶ │ pl_players                    │
│ ├─ GameOver panel          │          │                              │
│ │                          │          │ Edge functions:              │
│ ├─ PlayerIdentity ───────▶ │          │ ├─ pl-sync-player            │
│ ├─ LeaderboardManager ───▶ │          │ ├─ pl-submit-score           │
│ └─ SolanaManager (MWA) ──▶ │          │ └─ pl-submit-purchase        │
└────────────────────────────┘          └──────────────────────────────┘
         ▲
         │ Mobile Wallet Adapter
         ▼
┌────────────────────────────┐
│ Seed Vault / Phantom / ... │  mainnet-beta, merchant = 6zuPtQg1...
└────────────────────────────┘
```

### Client singletons (all spawned from `GameManager.EnsureLeaderboardSingletons`)

| Script                          | Responsibility                                                        |
|---------------------------------|-----------------------------------------------------------------------|
| `GameManager.cs`                | Scene wiring, UI panels, coin/wallet HUD, plane shop, SOL purchases.  |
| `Player.cs`                     | Input + collision + plane-perk activation. Hosts the `CoinMagnet`.    |
| `GameScreen.cs`                 | Run-time elapsed counter and in-HUD timer string.                     |
| `DifficultyDirector.cs`         | Tier clock and multipliers for missiles + stars.                      |
| `Missiles.cs`, `MissileObj.cs`  | Spawn rhythm and per-missile homing behaviour (prop-pursuit).         |
| `ExtraObj.cs`                   | Star spawn coroutine — reads interval from DifficultyDirector.        |
| `PlaneStats.cs` (static)        | Lookup table of per-plane turn / magnet / coin / shield perks.        |
| `CoinMagnet.cs`                 | Pulls nearby stars toward the player.                                 |
| `GameOver.cs`                   | Score math, plane-multiplier coin payout, high-score persistence.     |
| `LeaderboardManager.cs`         | 0.01 SOL gated leaderboard submission via `pl-submit-score`.          |
| `LeaderboardPanelBuilder.cs`    | Runtime UI for the leaderboard overlay.                               |
| `SolanaManager.cs`              | Wraps Solana.Unity-SDK Web3 for MWA connect + SystemProgram.Transfer. |
| `PlayerIdentity.cs`             | Device UUID + wallet sync. Restores progress across reinstalls.       |
| `BackgroundMusicManager.cs`     | Airplane-engine loop, muted via `AudioListener.pause`.                |

### Server

- **`pl_players`** — one row per device UUID. Columns: `pl_wallet`,
  `pl_total_coins`, `pl_high_score`, `pl_plane_id`, `pl_sound_on`,
  `pl_planes_owned` (bitmask). On reinstall, the same wallet restores
  coins + owned-plane bits so premium planes aren't lost.
- **`pl_leaderboard`** — one row per submitted score. Only accepts a
  submission if the attached transaction signature actually transferred
  0.01 SOL to the merchant wallet (verified server-side via RPC
  `getTransaction`).
- **`pl_purchases`** — coin-pack SOL purchases (1000 / 2000 / 3000
  coins @ 0.199 / 0.299 / 0.399 SOL), verified the same way.

---

## Game math

### Difficulty tier clock (`DifficultyDirector.cs`)

Every 10 seconds of run time the tier advances by 1. Tiers are capped
at 9 so survival past 90 s is skill-bound, not numerically impossible.

| Tier | Elapsed (s) | Missile speed × | Missile turn × | Missile spawn gap × | Star spawn interval |
|------|-------------|-----------------|-----------------|---------------------|---------------------|
| 0    | 0 – 10      | 1.00            | 1.00            | 1.00                | 2.40 s              |
| 1    | 10 – 20     | 1.08            | 1.05            | 0.96                | 2.20 s              |
| 2    | 20 – 30     | 1.16            | 1.10            | 0.92                | 2.00 s              |
| 3    | 30 – 40     | 1.24            | 1.15            | 0.88                | 1.80 s              |
| 4    | 40 – 50     | 1.32            | 1.20            | 0.84                | 1.60 s              |
| 5    | 50 – 60     | 1.40            | 1.25            | 0.80                | 1.40 s              |
| 6    | 60 – 70     | 1.48            | 1.30            | 0.76                | 1.20 s              |
| 7    | 70 – 80     | 1.56            | 1.35            | 0.72                | 1.00 s              |
| 8    | 80 – 90     | 1.64            | 1.40            | 0.68                | 0.80 s (floor)      |
| 9+   | 90+         | 1.72            | 1.45            | 0.64 (floor)        | 0.80 s              |

The director resets on `GameManager.StartGame` and freezes on
`OnplayerDie`, so the game-over panel doesn't keep ticking.

### Missile homing (`MissileObj.CreatObj`)

Each physics tick, a missile computes the normalised direction from
itself to the player's current position, takes its 2-D cross with the
missile's `up` vector (gives a signed "turn-left-or-right" magnitude),
and feeds that into `angularVelocity * rotateSpeed`. Linear velocity is
`missile.up * speed`. Proportional pursuit — always aimed at the
player's *current* position, never their predicted lead. Speed and
rotate are rolled per-spawn from `Player.SpeedRange[levelIndex]` /
`RoateRange[levelIndex]` and then multiplied by the director's live
`MissileSpeedMult` / `MissileRotateMult`.

One variant (`extraMissile = true`) flies ~2× faster with 0.8× turn
rate — shows up as one of the four missile types in the spawn loop.

### Missile spawn rhythm (`Missiles.Spawnobj`)

```
missile A  ->  wait 2.0s * gap   (tier 0: 2.0s, tier 9: 1.28s)
missile B  ->  wait 2.2s * gap
missile 3  ->  wait 6.2s * gap   (the "breather")
missile 4  ->  wait spawnTime * gap
loop
```

### Star spawn (`ExtraObj.CreateObj`)

One star per iteration, position picked uniformly from
`spawnPoints`. Wait = `DifficultyDirector.StarSpawnInterval`
(2.4 s → 0.8 s as the tier climbs). No bias toward the player, no
difficulty-scaled value.

### Plane perks (`PlaneStats.cs`)

Premium planes *do* matter — each tier adds:

| PlaneID | Price (coins) | Turn × | Magnet radius | Coin × | Shield |
|---------|---------------|--------|----------------|--------|--------|
| 0       | 0 (free)      | 1.00   | 0.0            | 1.00   | —      |
| 1       | 1 000         | 1.05   | 0.8            | 1.05   | —      |
| 2       | 2 000         | 1.10   | 1.0            | 1.10   | —      |
| 3       | 3 000         | 1.15   | 1.2            | 1.15   | —      |
| 4       | 4 000         | 1.20   | 1.5            | 1.20   | —      |
| 5       | 5 000         | 1.25   | 1.8            | 1.25   | —      |
| 6       | 6 000         | 1.30   | 2.0            | 1.30   | —      |
| 7 (B52) | 10 000        | 1.40   | 2.5            | 1.40   | 1 hit  |

- **Turn ×** multiplies `Player.RotationSpeed` on `Player.OnEnable`.
- **Magnet radius** spawns a `CoinMagnet` on the Player and sets
  `Radius`. Every `FixedUpdate`, stars within range are eased toward
  the player with `Vector2.MoveTowards(..., PullSpeed *
  fixedDeltaTime)`.
- **Coin ×** is applied by `PlaneStats.ApplyCoinMultiplier` to
  `_YourScoreValue` before `AddCoins`. Leaderboard submissions use the
  un-multiplied score so ownership doesn't buy rank.
- **Shield** flips `_shieldAvailable` true on `OnEnable`. First missile
  hit consumes it and destroys the missile; next hit is lethal.

### Score + coin payout (`GameOver.OnEnable`)

```
timeSec   = GameScreen.Instance.GetScore()
stars     = GameManager.Instance.ExtraInt
score     = int(timeSec * 0.15) + stars * 1        // leaderboard value
awarded   = round(score * PlaneStats[PlaneID].CoinMult)  // wallet value
```

Typical runs (free plane, ×1.00 multiplier):

| Run profile           | Score | Coins awarded |
|-----------------------|-------|---------------|
| 30 s, 5 stars         | 9     | 9             |
| 60 s, 10 stars        | 19    | 19            |
| 90 s, 15 stars        | 28    | 28            |
| 120 s, 25 stars (B52) | 43    | 60 (×1.40)    |

The SOL shop (0.199 / 0.299 / 0.399 SOL → 1000 / 2000 / 3000 coins)
remains the fast path if players want to skip the grind.

### Economy summary

- **First 1 000-coin plane:** ~50 moderate runs (~45 min) on the free
  plane. Intentionally slow to make the shop the obvious shortcut.
- **B52 (10 000):** too grindy to earn without buying a coin pack at
  least once, which is the design intent — the B52 is the flagship.
- **Leaderboard submission:** 0.01 SOL per entry, verified on-chain.

---

## Build

### Debug APK (for emulator / sideload)

```
PLANES_APK_OUTPUT="$PWD/Builds/planes-debug.apk" \
  /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics \
  -projectPath "$PWD" \
  -buildTarget Android \
  -executeMethod BuildScript.BuildAndroidDebug \
  -logFile /tmp/planes-debug.log
```

Signs with Unity's default debug keystore; `EditorUserBuildSettings.
development = true` sets `android:debuggable` so `adb run-as` works.

### Release APK (for the dApp Store)

```
./tools/build-release.sh
```

Prompts for keystore password (`~/.keystores/dappstore.keystore`),
runs `BuildScript.BuildAndroid`, writes `Builds/planes-release.apk`.
IL2CPP, ARM64 + ARMv7, non-debuggable, ready to upload.

### Publish update to dApp Store

```
npx dapp-store create release -k ~/.keystores/planes-publisher.json \
  -u https://api.mainnet-beta.solana.com
npx dapp-store publish update -k ~/.keystores/planes-publisher.json \
  -u https://api.mainnet-beta.solana.com \
  --requestor-is-authorized \
  --complies-with-solana-dapp-store-policies
```

Ticket **307824726727** is currently the initial submission review.
Wait for it to clear before pushing a v2; concurrent submissions are
not supported by the Publisher Portal.
