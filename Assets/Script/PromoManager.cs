using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Pulls the active promo from the pl_promos table and shows the themed
// PromoPopup once per promo. Edit/flip the row in Supabase to change what
// players see (review ask, follow-us / tweet CTA, announcement) with no app
// update; a new pl_id makes it show again. Spawned by GameManager alongside
// the other singletons, so it runs on the title screen at launch.
public class PromoManager : MonoBehaviour
{
    public static PromoManager Instance { get; private set; }

    private const string PrefLastPromo = "PromoLastShownId";

    [Serializable]
    private class Promo
    {
        public string pl_id;
        public string pl_title;
        public string pl_body;
        public string pl_image_url;
        public string pl_cta_label;
        public string pl_cta_url;
    }

    [Serializable]
    private class PromoList { public Promo[] items; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        // Small delay so the title screen is up before a popup can appear.
        yield return new WaitForSeconds(1.0f);
        yield return FetchActivePromo();
    }

    private IEnumerator FetchActivePromo()
    {
        string url = SupabaseLeaderboardClient.SupabaseUrl +
            "/rest/v1/pl_promos" +
            "?select=pl_id,pl_title,pl_body,pl_image_url,pl_cta_label,pl_cta_url" +
            "&active=eq.true&order=pl_updated_at.desc&limit=1";

        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("apikey", SupabaseLeaderboardClient.SupabaseAnonKey);
            req.SetRequestHeader("Authorization", "Bearer " + SupabaseLeaderboardClient.SupabaseAnonKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[PromoManager] fetch failed: {req.responseCode} {req.error}");
                yield break;
            }

            PromoList list;
            try { list = JsonUtility.FromJson<PromoList>("{\"items\":" + req.downloadHandler.text + "}"); }
            catch (Exception e) { Debug.LogWarning("[PromoManager] parse: " + e.Message); yield break; }

            if (list?.items == null || list.items.Length == 0) yield break;
            var promo = list.items[0];
            if (string.IsNullOrEmpty(promo.pl_id)) yield break;

            // Show each promo once; a new pl_id (new promo) shows again.
            if (PlayerPrefs.GetString(PrefLastPromo, "") == promo.pl_id) yield break;

            // Wait for the popup builder to be ready, then show.
            float t = 0f;
            while (PromoPopupBuilder.Instance == null && t < 5f) { t += Time.deltaTime; yield return null; }
            if (PromoPopupBuilder.Instance == null) yield break;

            PromoPopupBuilder.Instance.Show(
                promo.pl_title, promo.pl_body, promo.pl_image_url,
                promo.pl_cta_label, promo.pl_cta_url,
                onDismiss: () =>
                {
                    PlayerPrefs.SetString(PrefLastPromo, promo.pl_id);
                    PlayerPrefs.Save();
                });
        }
    }
}
