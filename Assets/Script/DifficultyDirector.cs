using UnityEngine;

// Single source of truth for "how hard is the game right now" -- every
// 10 seconds we step up a tier (0..9). Missile speed, missile turn rate,
// missile spawn gaps, and star spawn interval all read their scaling
// multipliers from here so the ramp stays in lockstep across systems.
//
// Past tier 9 everything holds at the cap so survival is skill-bound,
// not numerically impossible.
public class DifficultyDirector : MonoBehaviour
{
    public static DifficultyDirector Instance { get; private set; }

    // One "tier" per 10s of elapsed run time.
    public const float SecondsPerTier = 10f;
    public const int MaxTier = 9;

    // Per-tier missile perks: values at tier 9 cap.
    private const float MissileSpeedPerTier  = 0.08f; // 1.0 -> 1.72
    private const float MissileRotatePerTier = 0.05f; // 1.0 -> 1.45
    private const float MissileGapPerTier    = 0.04f; // 1.0 -> 0.64 (shorter = faster cadence)

    // Star spawn: baseline 2.4s, shrinks toward a 0.8s floor.
    private const float StarBaseGap      = 2.4f;
    private const float StarGapPerTier   = 0.2f;
    private const float StarGapFloor     = 0.8f;

    // Snapshot of where we were when StartRun() was called, so the tier
    // clock starts from 0 on each new run instead of wherever GameScreen
    // happened to be.
    private float _runStartTime;
    private bool  _running;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Call from GameManager.StartGame right after GameScreen.Instance.time
    // is zeroed. Safe to call repeatedly -- just rearms the clock.
    public void StartRun()
    {
        _runStartTime = GameScreen.Instance != null ? GameScreen.Instance.time : 0f;
        _running = true;
    }

    // Call when the run ends (player dies / Home button). Freezes the
    // tier so the pause/game-over screen doesn't keep ramping.
    public void StopRun() { _running = false; }

    public float ElapsedSeconds
    {
        get
        {
            if (!_running || GameScreen.Instance == null) return 0f;
            return Mathf.Max(0f, GameScreen.Instance.time - _runStartTime);
        }
    }

    public int CurrentTier
    {
        get
        {
            int t = Mathf.FloorToInt(ElapsedSeconds / SecondsPerTier);
            return Mathf.Clamp(t, 0, MaxTier);
        }
    }

    public float MissileSpeedMult  => 1f + CurrentTier * MissileSpeedPerTier;
    public float MissileRotateMult => 1f + CurrentTier * MissileRotatePerTier;
    // Gaps SHRINK with tier -- clamp to a sensible floor so the loop never
    // drops below ~60% of its original cadence.
    public float MissileGapMult    => Mathf.Max(0.4f, 1f - CurrentTier * MissileGapPerTier);

    public float StarSpawnInterval
    {
        get { return Mathf.Max(StarGapFloor, StarBaseGap - CurrentTier * StarGapPerTier); }
    }
}
