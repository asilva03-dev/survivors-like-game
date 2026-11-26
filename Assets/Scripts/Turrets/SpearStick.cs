/*
 * SpearStick
 * On the Fishercat's spear projectile (2026-11-24, user: spears stick into the ground and vanish). When the spear ends
 * (Projectile's IProjectileSpent hook: its last rat, the ground, or the end of its range) a pooled StuckSpear is planted
 * in the ground where the spear's line meets it (MapGround height when there is one, else y 0), leaning along the throw
 * at least minPitch degrees down, its tip embedDepth into the ground; it stays stickTime, then shrinks away.
 * - The stuck spears come from one shared ComponentPool<StuckSpear> (stuckPrefab, under "Stuck Spears").
 * - maxReach caps how far ahead of the end point the spear may plant itself (a spear ending high up drops straight down).
 * - (2026-11-25, user: it looked like it teleported into the ground) the stuck spear now flies that last stretch from
 *   where the spear ended, at the spear's own speed (Projectile.Speed), turning into its planted angle (StuckSpear.PlantFrom).
 */

using UnityEngine;

public class SpearStick : MonoBehaviour, IProjectileSpent
{
    private static ComponentPool<StuckSpear> pool;
    private static Transform poolRoot;
    private static StuckSpear pooledPrefab;

    [SerializeField] private StuckSpear stuckPrefab;
    [SerializeField, Min(0f)] private float stickTime = 1f;
    [SerializeField, Min(0.01f)] private float shrinkTime = 0.15f;
    [SerializeField, Min(0f)] private float embedDepth = 0.25f;
    [SerializeField, Range(0f, 89f)] private float minPitch = 35f;
    [SerializeField, Min(0f)] private float maxReach = 3f;
    [SerializeField, Min(0)] private int prewarm = 6;

    private Projectile projectile;

    public static int Planted { get; private set; }

    public void OnProjectileSpent(Vector3 where, Vector3 direction)
    {
        if (stuckPrefab == null)
        {
            return;
        }

        float ground = 0f;
        MapGround map = MapGround.Instance;
        if (map != null && map.HasData)
        {
            ground = map.HeightAt(where);
        }

        // Where the spear's line meets the ground (or straight below if it isn't heading down).
        Vector3 point = where;
        if (direction.y < -0.05f && where.y > ground)
        {
            float t = Mathf.Min((where.y - ground) / -direction.y, maxReach);
            point = where + direction * t;
        }
        point.y = ground;

        // Lean along the throw, at least minPitch down.
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        float pitch = Mathf.Max(minPitch, Mathf.Asin(Mathf.Clamp(-direction.y, -1f, 1f)) * Mathf.Rad2Deg);
        Vector3 lean = (flat * Mathf.Cos(pitch * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(pitch * Mathf.Deg2Rad)).normalized;

        Vector3 tip = point + lean * (embedDepth / Mathf.Max(0.1f, Mathf.Sin(pitch * Mathf.Deg2Rad)));
        if (projectile == null)
        {
            projectile = GetComponent<Projectile>();
        }
        float speed = projectile != null ? projectile.Speed : 0f;
        Quaternion heading = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.LookRotation(lean);
        Pool().Get(tip, Quaternion.LookRotation(lean)).PlantFrom(Pool(), stickTime, shrinkTime, where, heading, speed);
        Planted++;
    }

    private ComponentPool<StuckSpear> Pool()
    {
        if (pool == null || poolRoot == null || pooledPrefab != stuckPrefab)
        {
            poolRoot = new GameObject("Stuck Spears").transform;
            pooledPrefab = stuckPrefab;
            pool = new ComponentPool<StuckSpear>(stuckPrefab, poolRoot, prewarm);
        }
        return pool;
    }
}
