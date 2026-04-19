using UnityEngine;

// Floating power-up the player flies into. On contact, tells the
// AbilityController to activate the matching buff and then destroys
// itself. Gently bobs + spins so it reads as "collectible" instead
// of "obstacle".
//
// Pickups spawn with a CircleCollider2D (isTrigger = true) so they
// fire OnTriggerEnter2D when the plane flies through them -- no
// physics kick on the plane. A pickup is untagged; the player's
// existing OnCollisionEnter2D won't treat it as a missile because
// our handler is on the pickup, not the plane.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class AbilityPickup : MonoBehaviour
{
    public AbilityController.AbilityType Type = AbilityController.AbilityType.Magnet;

    // Bob parameters only -- we intentionally don't spin the pickup so
    // the magnet's natural tilt and the shield's upright pose both stay
    // readable while they float.
    public float BobAmplitude = 0.10f;
    public float BobFrequency = 2.0f;
    // Lifetime; if the player never grabs it, despawn so it doesn't
    // pile up when difficulty pushes more pickups.
    public float LifetimeSeconds = 15f;

    private Vector3 _anchor;
    private float   _t;
    private float   _age;

    private void Start()
    {
        _anchor = transform.position;
        _t = Random.value * 2f * Mathf.PI; // desync neighbouring pickups
        // Make sure collider behaves as a trigger and has a sensible
        // radius even if the prefab author forgot to set it.
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        if (col.radius < 0.1f) col.radius = 0.35f;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= LifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        _t += Time.deltaTime * BobFrequency;
        float bob = Mathf.Sin(_t) * BobAmplitude;
        transform.position = _anchor + new Vector3(0f, bob, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (!other.CompareTag("Player")) return;

        var ctrl = AbilityController.Instance;
        if (ctrl == null) return;

        // If another ability is already running, ignore the pickup so
        // it can be grabbed after the current buff expires. The visual
        // keeps bobbing so the player can tell it's still there.
        if (ctrl.Current != AbilityController.AbilityType.None) return;

        if (ctrl.Activate(Type))
        {
            Destroy(gameObject);
        }
    }
}
