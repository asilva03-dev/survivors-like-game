/*
 * ThrowWeapon<TStats>
 * Base for aimed weapons that lob a throwable at their target (ThrowLWeapon, ThrowEWeapon, ...). Subclasses
 * implement only Land(point): what happens where a throwable comes down.
 *
 * - Targeting, ring slot and aiming are the shared AimedWeapon / WeaponLoadout behaviour. Only guns are
 *   capped by the loadout, so throw weapons never take a gun slot.
 * - All numbers come from a ThrowStats subclass asset. The throwable's model is the stats' throwableModel: the
 *   weapon builds one hidden template (Throwable prefab + that model) in Awake and pools copies of it.
 * - The weapon object itself has no model; throwables leave from headHeight above the owner's position and
 *   land at the target's ground position at throw time (they do not track a moving target, but lead it by its
 *   measured velocity, see PredictCenter).
 * - projectileCount throwables leave together from the same point; the first lands on the (predicted) target
 *   and the others around it within `spread` (see SpreadOffset), each with its own random bloom.
 * - Pools (throwables, bursts, and any a subclass adds) are parented to a separate scene object (PoolRoot) so
 *   they do not move with the player; nothing is created or destroyed after Awake except pool growth.
 * - Prefab refs (throwable, burst, and subclass extras) are set on the weapon prefab, not in the stats.
 */

using UnityEngine;

public abstract class ThrowWeapon<TStats> : AimedWeapon<TStats> where TStats : ThrowStats
{
    [SerializeField] private ThrownProjectile throwablePrefab;
    [SerializeField] private DebrisBurst burstPrefab;
    [SerializeField, Min(0f)] private float groundHeight = 0f;
    [SerializeField] private float headHeight = 1.4f;

    private ComponentPool<ThrownProjectile> throwablePool;
    private ComponentPool<DebrisBurst> burstPool;
    private System.Action<Vector3> onLanded;

    // Parent for every pool this weapon owns (subclasses use it for their own pools).
    protected Transform PoolRoot { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        PoolRoot = new GameObject(name + " Throwables").transform;

        ThrownProjectile template = Instantiate(throwablePrefab, PoolRoot);
        template.SetModel(Stats.ThrowableModel);
        template.gameObject.SetActive(false);

        throwablePool = new ComponentPool<ThrownProjectile>(template, PoolRoot, Stats.PrewarmCount);
        burstPool = new ComponentPool<DebrisBurst>(burstPrefab, PoolRoot, Mathf.Max(2, Stats.ProjectileCount));
        onLanded = Land;
    }

    // Called with the ground point where a throwable landed.
    protected abstract void Land(Vector3 point);

    // The landing particle burst, tinted with the stats' explosionColor.
    protected void PlayBurst(Vector3 point)
    {
        DebrisBurst burst = burstPool.Get(point, Quaternion.identity);
        burst.Play(burstPool, Stats.ExplosionColor);
    }

    protected override void Fire(Damageable target)
    {
        Vector3 origin = Owner.position + Vector3.up * headHeight;
        Vector3 center = PredictCenter(target);

        Vector3 forward = center - origin;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        int count = Stats.ProjectileCount;
        for (int i = 0; i < count; i++)
        {
            Vector3 end = center + SpreadOffset(i, count, Stats.Spread, forward, right) + Bloom();
            end.y = groundHeight;

            ThrownProjectile throwable = throwablePool.Get(origin, Quaternion.identity);
            throwable.Launch(throwablePool, origin, end, Stats.ThrowArc, Stats.FlightTime, onLanded);
        }
    }

    // Projectile 0 is the middle (the intended spot). The rest fill rings around it: ring r holds up to
    // 4 * r points at radius spread * r / ringCount, evenly spaced starting on the right, so 5 projectiles
    // give right, further, left, nearer around the middle.
    private static Vector3 SpreadOffset(int index, int count, float spread, Vector3 forward, Vector3 right)
    {
        if (index == 0)
        {
            return Vector3.zero;
        }

        int extras = count - 1;
        int ringCount = 0;
        for (int remaining = extras; remaining > 0; remaining -= 4 * ringCount)
        {
            ringCount++;
        }

        int slotIndex = index - 1;
        int ring = 1;
        int before = 0;
        while (slotIndex >= before + 4 * ring)
        {
            before += 4 * ring;
            ring++;
        }

        int inRing = Mathf.Min(4 * ring, extras - before);
        float angle = (slotIndex - before) * 2f * Mathf.PI / inRing;
        float radius = spread * ring / ringCount;
        return (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
    }

    private Vector3 Bloom()
    {
        Vector2 offset = Random.insideUnitCircle * Stats.LandingSpread;
        return new Vector3(offset.x, 0f, offset.y);
    }

    // Leads the target by its measured velocity over the flight time, scaled by predictionStrength and
    // limited so the point never goes past the owner (enemies chasing the player stop near it). This is
    // the middle of the spread pattern; every projectile also gets its own random landingSpread bloom.
    private Vector3 PredictCenter(Damageable target)
    {
        Vector3 position = target.transform.position;
        Vector3 lead = target.Velocity * (Stats.FlightTime * Stats.PredictionStrength);
        lead.y = 0f;

        Vector3 toOwner = Owner.position - position;
        toOwner.y = 0f;
        lead = Vector3.ClampMagnitude(lead, toOwner.magnitude);

        return position + lead;
    }
}
