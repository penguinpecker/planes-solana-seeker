using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Thin PostgREST wrapper for the Supabase pl_leaderboard feature. The URL and
// publishable key are safe to ship in the APK — Supabase is designed that way,
// RLS (row-level security) gates what the anon role can do. Keep the heavy
// validation (e.g. on-chain tx signature verification) for a later edge
// function; this client trusts whatever it's told to write.
public class SupabaseLeaderboardClient : MonoBehaviour
{
    // Project "Gridzero" (dqvwpbggjlcumcmlliuj).
    public const string SupabaseUrl = "https://dqvwpbggjlcumcmlliuj.supabase.co";
    public const string SupabaseAnonKey = "sb_publishable_cP9JqtSBOWihN8-xTWJyUQ_yl_RdXg8";

    public const string LeaderboardTable = "pl_leaderboard";
    public const string LeaderboardTopView = "pl_leaderboard_top";

    [Serializable]
    public class LeaderboardEntry
    {
        public string pl_wallet;
        public int pl_score;
        public string pl_tx_signature;
        public string pl_cluster;
        public string pl_created_at;
    }

    [Serializable]
    private class LeaderboardEntryList
    {
        public LeaderboardEntry[] items;
    }

    [Serializable]
    private class SubmitPayload
    {
        public string pl_wallet;
        public int pl_score;
        public string pl_tx_signature;
        public string pl_cluster;
    }

    public IEnumerator FetchTop(int offset, int limit, Action<LeaderboardEntry[], string> callback)
    {
        string url = $"{SupabaseUrl}/rest/v1/{LeaderboardTopView}" +
                     $"?select=pl_wallet,pl_score,pl_tx_signature,pl_cluster,pl_created_at" +
                     $"&order=pl_score.desc,pl_created_at.asc" +
                     $"&limit={limit}&offset={offset}";

        using (var req = UnityWebRequest.Get(url))
        {
            ApplyHeaders(req);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                callback?.Invoke(null, $"{req.responseCode} {req.error}: {req.downloadHandler?.text}");
                yield break;
            }

            // PostgREST returns a JSON array; JsonUtility doesn't parse top-level arrays
            // so wrap it as {"items": [...]} before deserializing.
            string body = req.downloadHandler.text;
            string wrapped = "{\"items\":" + body + "}";
            LeaderboardEntryList list;
            try { list = JsonUtility.FromJson<LeaderboardEntryList>(wrapped); }
            catch (Exception e)
            {
                callback?.Invoke(null, "json parse: " + e.Message + " — body: " + body);
                yield break;
            }

            callback?.Invoke(list.items ?? Array.Empty<LeaderboardEntry>(), null);
        }
    }

    public IEnumerator SubmitScore(string wallet, int score, string txSignature, string cluster, Action<bool, string> callback)
    {
        if (string.IsNullOrEmpty(wallet) || string.IsNullOrEmpty(txSignature))
        {
            callback?.Invoke(false, "missing wallet or tx signature");
            yield break;
        }

        string url = $"{SupabaseUrl}/rest/v1/{LeaderboardTable}";
        var payload = new SubmitPayload
        {
            pl_wallet = wallet,
            pl_score = score,
            pl_tx_signature = txSignature,
            pl_cluster = string.IsNullOrEmpty(cluster) ? "devnet" : cluster
        };
        string json = JsonUtility.ToJson(payload);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Prefer", "return=minimal");
            ApplyHeaders(req);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                callback?.Invoke(false, $"{req.responseCode} {req.error}: {req.downloadHandler?.text}");
                yield break;
            }

            callback?.Invoke(true, null);
        }
    }

    private static void ApplyHeaders(UnityWebRequest req)
    {
        req.SetRequestHeader("apikey", SupabaseAnonKey);
        req.SetRequestHeader("Authorization", "Bearer " + SupabaseAnonKey);
    }
}
