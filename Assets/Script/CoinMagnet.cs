using UnityEngine;

// Attached to the Player by PlayerPerkApplier when the active plane has
// a non-zero magnet radius. Every physics tick it sweeps child objects
// of the ExtraObj spawner (the stars) and, for each one inside the
// radius, eases it toward the player's position so it "flies" into the
// plane instead of requiring a direct collision path.
//
// We deliberately don't hijack the stars' collision -- they still only
// score when Player.OnCollisionEnter2D fires with tag ExtraObj. The
// magnet just closes the distance so touching them becomes trivial.
public class CoinMagnet : MonoBehaviour
{
    public float Radius = 1.0f;

    // How fast a caught star eases toward the plane (world units / sec).
    // Tuned so even the smallest radius gives a satisfying "snap".
    public float PullSpeed = 12f;

    private void FixedUpdate()
    {
        if (Radius <= 0f) return;
        var spawner = ExtraObj.Instance;
        if (spawner == null) return;

        Vector2 myPos = transform.position;
        float r2 = Radius * Radius;

        // Iterate the spawner's live children (the stars). No allocation
        // -- Transform's enumerator is a struct.
        foreach (Transform star in spawner.transform)
        {
            if (star == null || !star.gameObject.activeSelf) continue;
            Vector2 starPos = star.position;
            Vector2 delta = myPos - starPos;
            if (delta.sqrMagnitude > r2) continue;

            // Ease toward player. MoveTowards caps the per-frame step
            // so we don't teleport through the plane and miss the
            // collider.
            Vector2 next = Vector2.MoveTowards(starPos, myPos, PullSpeed * Time.fixedDeltaTime);
            star.position = new Vector3(next.x, next.y, star.position.z);
        }
    }
}
