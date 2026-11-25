/*
 * AngelArrowStick
 * On the Angel Cat's arrow projectile (tower 6, 2026-11-25, user's design). It hears which rat the arrow hit
 * (Projectile's IProjectileHit hook) and, when the arrow ends (IProjectileSpent), starts an ArrowVolley:
 *
 * - The rat survived (user, same day): the arrow sticks in the rat (pooled EnemyStuckArrow, kept at the pose it hit
 *   with, moving with the rat) and the volley is centred on that rat, following it until the arrows land.
 * - The arrow killed the rat: the arrow sticks in the ground where the rat stood (pooled StuckSpear with the arrow
 *   model) and the volley goes off there.
 * - The arrow hit nothing (its rat died first, or it ran out of range): no volley; it just disappears.
 * - The tower's arrow stays for the whole volley (ArrowVolleyStats.TotalTime) and shrinks with it.
 * - Ground height from MapGround when there is one, else y 0 (GroundPoint).
 * - Shared pools "Stuck Arrows", "Enemy Arrows" and "Arrow Volleys". Counters Volleys / StuckInRat / StuckInGround for tests.
 */

using UnityEngine;

public class AngelArrowStick : MonoBehaviour, IProjectileHit, IProjectileSpent
{
    private static ComponentPool<StuckSpear> groundPool;
    private static ComponentPool<EnemyStuckArrow> ratPool;
    private static ComponentPool<ArrowVolley> volleyPool;
    private static Transform groundRoot;
    private static Transform ratRoot;
    private static Transform volleyRoot;

    [Tooltip("The arrow left in the ground where it killed a rat.")]
    [SerializeField] private StuckSpear stuckPrefab;
    [Tooltip("The arrow left stuck in a rat that survived.")]
    [SerializeField] private EnemyStuckArrow ratArrowPrefab;
    [SerializeField] private ArrowVolley volleyPrefab;
    [SerializeField] private ArrowVolleyStats volleyStats;
    [Tooltip("How far past the middle of the rat's body the arrow's tip goes, along the arrow's flight.")]
    [SerializeField, Min(0f)] private float ratDepth = 0.05f;
    [Tooltip("How deep the arrow's tip sinks into the ground.")]
    [SerializeField, Min(0f)] private float embedDepth = 0.2f;
    [SerializeField, Range(0f, 89f)] private float minPitch = 40f;
    [SerializeField, Min(0)] private int prewarm = 4;

    private Damageable hitRat;
    private Vector3 ratSpot;
    private bool killed;

    public static int Volleys { get; private set; }
    public static int StuckInRat { get; private set; }
    public static int StuckInGround { get; private set; }

    /// <summary>The ground point under a position (MapGround height, else y 0).</summary>
    public static Vector3 GroundPoint(Vector3 position)
    {
        MapGround map = MapGround.Instance;
        position.y = map != null && map.HasData ? map.HeightAt(position) : 0f;
        return position;
    }

    // The middle of the rat's body: its position, at the height of its model's centre (the rat's root and aim point sit
    // above the model, so an arrow placed there would float).
    private static Vector3 BodyPoint(Damageable rat)
    {
        Vector3 point = rat.transform.position;
        Transform visual = rat.transform.Find("Visual");
        Renderer[] renderers = (visual != null ? visual : rat.transform).GetComponentsInChildren<Renderer>();
        float low = float.MaxValue, high = float.MinValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is MeshRenderer || renderers[i] is SkinnedMeshRenderer)
            {
                low = Mathf.Min(low, renderers[i].bounds.min.y);
                high = Mathf.Max(high, renderers[i].bounds.max.y);
            }
        }
        if (high > low)
        {
            point.y = (low + high) * 0.5f;
        }
        return point;
    }

    public void OnProjectileHit(Damageable target, Vector3 where, bool died)
    {
        hitRat = target;
        ratSpot = target.transform.position;
        killed = died;
    }

    public void OnProjectileSpent(Vector3 where, Vector3 direction)
    {
        Damageable rat = hitRat;
        hitRat = null;
        if (rat == null || volleyStats == null)
        {
            return;
        }

        float stay = volleyStats.TotalTime;
        float shrink = volleyStats.ShrinkTime;
        Vector3 centre;
        Damageable follow = null;

        if (!killed && rat.isActiveAndEnabled)
        {
            if (ratArrowPrefab != null)
            {
                Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.down;
                ComponentPool<EnemyStuckArrow> pool = RatPool();
                pool.Get(BodyPoint(rat) + dir * ratDepth, Quaternion.LookRotation(dir)).Attach(pool, rat, stay, shrink);
                StuckInRat++;
            }
            centre = GroundPoint(rat.transform.position);
            follow = rat;
        }
        else
        {
            centre = GroundPoint(ratSpot);
            if (stuckPrefab != null)
            {
                Vector3 flat = new Vector3(direction.x, 0f, direction.z);
                flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
                float pitch = Mathf.Max(minPitch, Mathf.Asin(Mathf.Clamp(-direction.y, -1f, 1f)) * Mathf.Rad2Deg);
                Vector3 lean = (flat * Mathf.Cos(pitch * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(pitch * Mathf.Deg2Rad)).normalized;
                Vector3 tip = centre + lean * (embedDepth / Mathf.Max(0.1f, Mathf.Sin(pitch * Mathf.Deg2Rad)));
                ComponentPool<StuckSpear> pool = GroundPool();
                pool.Get(tip, Quaternion.LookRotation(lean)).Plant(pool, stay, shrink);
                StuckInGround++;
            }
        }

        if (volleyPrefab != null)
        {
            ComponentPool<ArrowVolley> volleys = VolleyPool();
            volleys.Get(centre, Quaternion.identity).Launch(volleys, volleyStats, centre, follow);
            Volleys++;
        }
    }

    private ComponentPool<StuckSpear> GroundPool()
    {
        if (groundPool == null || groundRoot == null)
        {
            groundRoot = new GameObject("Stuck Arrows").transform;
            groundPool = new ComponentPool<StuckSpear>(stuckPrefab, groundRoot, prewarm);
        }
        return groundPool;
    }

    private ComponentPool<EnemyStuckArrow> RatPool()
    {
        if (ratPool == null || ratRoot == null)
        {
            ratRoot = new GameObject("Enemy Arrows").transform;
            ratPool = new ComponentPool<EnemyStuckArrow>(ratArrowPrefab, ratRoot, prewarm);
        }
        return ratPool;
    }

    private ComponentPool<ArrowVolley> VolleyPool()
    {
        if (volleyPool == null || volleyRoot == null)
        {
            volleyRoot = new GameObject("Arrow Volleys").transform;
            volleyPool = new ComponentPool<ArrowVolley>(volleyPrefab, volleyRoot, prewarm);
        }
        return volleyPool;
    }
}
