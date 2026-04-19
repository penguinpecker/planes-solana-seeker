using UnityEngine;

// Per-plane mechanical perks keyed by PlaneID. Higher-priced planes
// steer faster, pull more stars, and pay out more coins so the 10k
// B52 actually feels like an upgrade instead of a re-skin.
//
// Numbers come from the design doc the player signed off on:
//   0: free   -- 1.00x  / 0.0  / 1.00x / no shield
//   1: 1 000  -- 1.05x  / 0.8  / 1.05x
//   2: 2 000  -- 1.10x  / 1.0  / 1.10x
//   3: 3 000  -- 1.15x  / 1.2  / 1.15x
//   4: 4 000  -- 1.20x  / 1.5  / 1.20x
//   5: 5 000  -- 1.25x  / 1.8  / 1.25x
//   6: 6 000  -- 1.30x  / 2.0  / 1.30x
//   7: 10 000 -- 1.40x  / 2.5  / 1.40x + 1 free hit (shield)
public struct PlanePerks
{
    public float TurnMult;       // scales Player.RotationSpeed
    public float MagnetRadius;   // world units; 0 = no magnet
    public float CoinMult;       // applied to YourScore before AddCoins
    public bool  HasShield;      // true = one free missile hit per run
}

public static class PlaneStats
{
    private static readonly PlanePerks[] _perks = new PlanePerks[]
    {
        new PlanePerks { TurnMult = 1.00f, MagnetRadius = 0.0f, CoinMult = 1.00f, HasShield = false }, // 0
        new PlanePerks { TurnMult = 1.05f, MagnetRadius = 0.8f, CoinMult = 1.05f, HasShield = false }, // 1
        new PlanePerks { TurnMult = 1.10f, MagnetRadius = 1.0f, CoinMult = 1.10f, HasShield = false }, // 2
        new PlanePerks { TurnMult = 1.15f, MagnetRadius = 1.2f, CoinMult = 1.15f, HasShield = false }, // 3
        new PlanePerks { TurnMult = 1.20f, MagnetRadius = 1.5f, CoinMult = 1.20f, HasShield = false }, // 4
        new PlanePerks { TurnMult = 1.25f, MagnetRadius = 1.8f, CoinMult = 1.25f, HasShield = false }, // 5
        new PlanePerks { TurnMult = 1.30f, MagnetRadius = 2.0f, CoinMult = 1.30f, HasShield = false }, // 6
        new PlanePerks { TurnMult = 1.40f, MagnetRadius = 2.5f, CoinMult = 1.40f, HasShield = true  }, // 7 (B52)
    };

    public static PlanePerks ForId(int planeId)
    {
        if (planeId < 0 || planeId >= _perks.Length) return _perks[0];
        return _perks[planeId];
    }

    // Handy accessor used by GameOver after it's already computed the
    // flat score -- we apply the multiplier before it lands in the wallet.
    public static int ApplyCoinMultiplier(int baseCoins)
    {
        int planeId = PlayerPrefs.GetInt("PlaneID", 0);
        var perks = ForId(planeId);
        return Mathf.RoundToInt(baseCoins * perks.CoinMult);
    }
}
