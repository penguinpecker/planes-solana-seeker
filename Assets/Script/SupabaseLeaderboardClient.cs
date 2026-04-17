using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Thin wrapper over the Supabase PostgREST (reads) and the pl-submit-score
// edge function (writes). The URL and publishable key are safe to ship in the
// APK — Supabase is designed that way; RLS on pl_leaderboard now blocks anon
// inserts outright, so the only way a score ever lands in the DB is via the
// edge function, which verifies the transfer on-chain before accepting.
public class SupabaseLeaderboardClient : MonoBehaviour
{
    // Project "Gridzero" (dqvwpbggjlcumcmlliuj).
    public const string SupabaseUrl = "https://dqvwpbggjlcumcmlliuj.supabase.co";
    public const string SupabaseAnonKey = "sb_publishable_cP9JqtSBOWihN8-xTWJyUQ_yl_RdXg8";

    public const string LeaderboardTable = "pl_leaderboard";
    public const string LeaderboardTopView = "pl_leaderboard_top";
    public const string SubmitFunctionSlug = "pl-submit-score";

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

    [Serializable]
    private class SubmitResponse
    {
        public bool ok;
        public string error;
    }

    public IEnumerator SubmitScore(string wallet, int score, string txSignature, string cluster, Action<bool, string> callback)
    {
        if (string.IsNullOrEmpty(wallet) || string.IsNullOrEmpty(txSignature))
        {
            callback?.Invoke(false, "missing wallet or tx signature");
            yield break;
        }

        string url = $"{SupabaseUrl}/functions/v1/{SubmitFunctionSlug}";
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
            ApplyHeaders(req);

            yield return req.SendWebRequest();

            string respText = req.downloadHandler?.text ?? "";
            long code = req.responseCode;

            // The edge function returns {"ok":true} on success and {"error":"..."}
            // with an HTTP 4xx/5xx on failure; surface the error text to the caller
            // so the UI can explain what went wrong.
            if (req.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
            {
                callback?.Invoke(true, null);
                yield break;
            }

            string reason = respText;
            if (!string.IsNullOrEmpty(respText))
            {
                try
                {
                    var parsed = JsonUtility.FromJson<SubmitResponse>(respText);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.error)) reason = parsed.error;
                }
                catch { /* keep raw body */ }
            }
            callback?.Invoke(false, $"{code}: {reason}");
        }
    }

    private static void ApplyHeaders(UnityWebRequest req)
    {
        req.SetRequestHeader("apikey", SupabaseAnonKey);
        req.SetRequestHeader("Authorization", "Bearer " + SupabaseAnonKey);
    }
}
