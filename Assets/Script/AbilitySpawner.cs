using System.Collections;
using UnityEngine;

// Auto-spawned singleton that drops ability pickups into the play
// area during gameplay. Only spawns when NO ability is currently
// active, so the player is never holding multiple buffs at once --
// the design constraint is "one ability on at a time".
//
// Pickup type alternates with a random roll; cadence is 18-30s gap
// after the previous pickup cleared. Piggy-backs on ExtraObj's
// spawnPoints list for where to place the pickup so it always
// lands within the play area that's already been authored for
// stars (same camera framing, same reachability).
public class AbilitySpawner : MonoBehaviour
{
    public static AbilitySpawner Instance { get; private set; }

    public float MinSpawnGap = 18f;
    public float MaxSpawnGap = 30f;
    // How long after a new run starts before the first pickup drops
    // (give the player time to settle).
    public float FirstPickupDelay = 12f;

    private Coroutine _loop;
    // Set of spawned pickups we track so we don't overlap them.
    private GameObject _activePickup;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Called from GameManager.StartGame.
    public void BeginRun()
    {
        StopRun();
        _loop = StartCoroutine(SpawnLoop());
    }

    public void StopRun()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        if (_activePickup != null) { Destroy(_activePickup); _activePickup = null; }
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(FirstPickupDelay);

        while (true)
        {
            // Wait until no ability is active AND there's no pickup
            // already on the field.
            while (AbilityController.Instance != null &&
                   AbilityController.Instance.Current != AbilityController.AbilityType.None)
            {
                yield return new WaitForSeconds(1f);
            }
            while (_activePickup != null) { yield return null; }

            // Pick random type.
            var type = Random.value < 0.5f
                ? AbilityController.AbilityType.Magnet
                : AbilityController.AbilityType.Shield;

            SpawnOne(type);

            // Wait a random gap before the next pickup is *eligible*.
            yield return new WaitForSeconds(Random.Range(MinSpawnGap, MaxSpawnGap));
        }
    }

    private void SpawnOne(AbilityController.AbilityType type)
    {
        var spawner = ExtraObj.Instance;
        Vector3 pos;
        if (spawner != null && spawner.spawnPoints != null && spawner.spawnPoints.Count > 0)
        {
            pos = spawner.spawnPoints[Random.Range(0, spawner.spawnPoints.Count)].position;
        }
        else
        {
            // No spawn points wired -- put it somewhere sane relative
            // to the player so we never drop it off-camera.
            var p = Player.Instance;
            pos = p != null ? p.transform.position + new Vector3(0, 4f, 0) : Vector3.zero;
        }

        var go = new GameObject("AbilityPickup_" + type);
        go.transform.position = pos;
        // Use the star generator as the parent so existing reset logic
        // (GameManager.ResetRunState destroys ExtraObj children) nukes
        // pickups on Play Again, matching the star cleanup rules.
        if (spawner != null) go.transform.SetParent(spawner.transform, true);

        var sr = go.AddComponent<SpriteRenderer>();
        var ctrl = AbilityController.Instance;
        sr.sprite = type == AbilityController.AbilityType.Magnet
            ? (ctrl != null ? ctrl.GetMagnetSprite() : null)
            : (ctrl != null ? ctrl.GetShieldSprite() : null);
        sr.sortingOrder = 4;

        // Scale so the pickup reads just slightly bigger than a star
        // (stars are ~0.5 world units; 512px@100PPU * 0.17 * ~0.8 fill
        // puts pickups at ~0.7 world units -- close to star size so
        // they don't dominate the screen).
        go.transform.localScale = new Vector3(0.17f, 0.17f, 0.17f);
        // Both sprites are authored upright (shield cresting upward,
        // magnet prongs pointing up) so we spawn at identity rotation
        // and never spin them in Update.
        go.transform.rotation = Quaternion.identity;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.6f;

        // Rigidbody2D kinematic so OnTriggerEnter2D fires with a
        // collision-tagged Player without needing physics.
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var pickup = go.AddComponent<AbilityPickup>();
        pickup.Type = type;

        _activePickup = go;
        // Clear our handle when it dies naturally so the loop can
        // schedule the next one.
        StartCoroutine(WatchPickup(go));
    }

    private IEnumerator WatchPickup(GameObject go)
    {
        while (go != null && go.activeInHierarchy) yield return null;
        if (_activePickup == go) _activePickup = null;
    }
}
