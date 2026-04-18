using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Unique-per-install player identity + cross-device progress sync.
// On first launch we generate a GUID and persist it in PlayerPrefs under
// "DeviceId". That id keys a row in pl_players on Supabase so the player's
// total coins, high score, plane selection, and sound preference survive
// reinstalls (and can later be reconciled against their wallet once they
// connect one).
//
// GameManager auto-spawns this alongside the other singletons. On Start it
// fetches the remote row (via pl-sync-player) and overlays anything the
// server has that's MORE RECENT than what's in PlayerPrefs. On any local
// change, call MarkDirty(). A low-frequency coroutine flushes dirty state
// back to Supabase.
public class PlayerIdentity : MonoBehaviour
{
    public static PlayerIdentity Instance { get; private set; }

    private const string PrefDeviceId = "DeviceId";
    private const string SyncEndpoint =
        SupabaseLeaderboardClient.SupabaseUrl + "/functions/v1/pl-sync-player";
    private const float SyncIntervalSec = 4f;

    public string DeviceId { get; private set; }

    private bool _dirty;
    private bool _busy;

    [Serializable]
    private class SyncPayload
    {
        public string pl_device_id;
        public string pl_wallet;
        public int pl_total_coins;
        public int pl_high_score;
        public int pl_plane_id;
        public bool pl_sound_on;
    }

    [Serializable]
    private class SyncResponse
    {
        public bool ok;
        public string error;
        public PlayerRow player;
    }

    [Serializable]
    private class PlayerRow
    {
        public string pl_device_id;
        public string pl_wallet;
        public int pl_total_coins;
        public int pl_high_score;
        public int pl_plane_id;
        public bool pl_sound_on;
        public string pl_updated_at;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        DeviceId = PlayerPrefs.GetString(PrefDeviceId, "");
        if (string.IsNullOrEmpty(DeviceId))
        {
            DeviceId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PrefDeviceId, DeviceId);
            PlayerPrefs.Save();
            Debug.Log($"[PlayerIdentity] New device id: {DeviceId}");
        }
        else
        {
            Debug.Log($"[PlayerIdentity] Existing device id: {DeviceId}");
        }
    }

    private IEnumerator Start()
    {
        // Initial load: tell server who we are, receive whatever it already has.
        yield return Sync(forceLoad: true);
        StartCoroutine(FlushLoop());
    }

    // Call after mutating coins/high-score/plane/sound so we eventually flush.
    public void MarkDirty() => _dirty = true;

    private IEnumerator FlushLoop()
    {
        var wait = new WaitForSeconds(SyncIntervalSec);
        while (true)
        {
            yield return wait;
            if (_dirty && !_busy) yield return Sync(forceLoad: false);
        }
    }

    private IEnumerator Sync(bool forceLoad)
    {
        if (_busy) yield break;
        _busy = true;

        var payload = new SyncPayload
        {
            pl_device_id = DeviceId,
            pl_wallet = SolanaManager.Instance != null && SolanaManager.Instance.IsWalletConnected
                ? SolanaManager.Instance.WalletAddress : null,
            pl_total_coins = PlayerPrefs.GetInt("TotalCoins", 0),
            pl_high_score = PlayerPrefs.GetInt("HighScore", 0),
            pl_plane_id = PlayerPrefs.GetInt("PlaneID", 0),
            pl_sound_on = PlayerPrefs.GetInt("SoundOn", 1) == 1,
        };
        string json = JsonUtility.ToJson(payload);

        using (var req = new UnityWebRequest(SyncEndpoint, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", SupabaseLeaderboardClient.SupabaseAnonKey);
            req.SetRequestHeader("Authorization", "Bearer " + SupabaseLeaderboardClient.SupabaseAnonKey);
            yield return req.SendWebRequest();

            string text = req.downloadHandler?.text ?? "";
            if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 300)
            {
                Debug.LogWarning($"[PlayerIdentity] sync failed ({req.responseCode}): {text}");
                _busy = false;
                yield break;
            }

            SyncResponse resp;
            try { resp = JsonUtility.FromJson<SyncResponse>(text); }
            catch { resp = null; }

            if (resp != null && resp.ok && resp.player != null && forceLoad)
            {
                ApplyRemote(resp.player);
            }
            _dirty = false;
        }
        _busy = false;
    }

    // Remote row overrides local PlayerPrefs when the server has progress we
    // don't (fresh install on a new device using the same device id, or a
    // reinstall). We prefer server state for coin/high-score totals because
    // the server is the cross-device source of truth; for plane and sound we
    // also prefer server since the user just expressed them there last.
    private void ApplyRemote(PlayerRow row)
    {
        PlayerPrefs.SetInt("TotalCoins", row.pl_total_coins);
        PlayerPrefs.SetInt("HighScore", row.pl_high_score);
        PlayerPrefs.SetInt("PlaneID", row.pl_plane_id);
        PlayerPrefs.SetInt("SoundOn", row.pl_sound_on ? 1 : 0);
        PlayerPrefs.Save();

        if (GameManager.Instance != null && GameManager.Instance.isActiveAndEnabled)
        {
            // Refresh the HUD — OnEnable already reads from PlayerPrefs but
            // it ran BEFORE this remote pull, so re-run the coin/plane setup.
            GameManager.Instance.RefreshFromPlayerPrefs();
        }

        Debug.Log($"[PlayerIdentity] Loaded remote state: coins={row.pl_total_coins} high={row.pl_high_score} plane={row.pl_plane_id}");
    }
}
