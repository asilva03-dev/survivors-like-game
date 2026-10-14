/*
 * ThrowLWeapon
 * Aimed weapon that lobs a throwable at its target; where it lands it explodes in a particle burst and
 * leaves a LingerPuddle that keeps damaging whatever stands in it.
 *
 * - Targeting, ring slot and aiming are the shared AimedWeapon / WeaponLoadout behaviour, so it uses the
 *   same priority as the gun. (Only guns are capped by the loadout, so it never takes a gun slot.)
 * - All numbers come from a ThrowLStats asset. The throwable's model is the stats' throwableModel: the
 *   weapon builds one hidden template (Throwable prefab + that model) in Awake and pools copies of it.
 * - The weapon object itself has no model; the throwable leaves from headHeight above the owner's
 *   position (just over the player's head) and lands at the target's ground position at throw time
 *   (it does not track a moving target, but leads it by its measured velocity, see PredictLanding).
 * - Pools (throwables, puddles, bursts) are owned by this weapon and parented to a separate scene
 *   object so they do not move with the player; nothing is created or destroyed after Awake except
 *   pool growth.
 * - Prefab refs (throwable, puddle, burst) are set on the weapon prefab, not in the stats.
 */

using UnityEngine;

public class ThrowLWeapon : AimedWeapon<ThrowLStats>
{
    [SerializeField] private ThrownProjectile throwablePrefab;
    [SerializeField] private LingerPuddle puddlePrefab;
    [SerializeField] private DebrisBurst burstPrefab;
    [SerializeField, Min(0f)] private float groundHeight = 0f;
    [SerializeField] private float headHeight = 1.4f;

    private ComponentPool<ThrownProjectile> throwablePool;
    private ComponentPool<LingerPuddle> puddlePool;
    private ComponentPool<DebrisBurst> burstPool;
    private System.Action<Vector3> onLanded;

    protected override void Awake()
    {
        base.Awake();

        Transform root = new GameObject(name + " Throwables").transform;

        ThrownProjectile template = Instantiate(throwablePrefab, root);
        template.SetModel(Stats.ThrowableModel);
        template.gameObject.SetActive(false);

        throwablePool = new ComponentPool<ThrownProjectile>(template, root, Stats.PrewarmCount);
        puddlePool = new ComponentPool<LingerPuddle>(puddlePrefab, root, 2);
        burstPool = new ComponentPool<DebrisBurst>(burstPrefab, root, 2);
        onLanded = Land;
    }

    protected override void Fire(Damageable target)
    {
        Vector3 end = PredictLanding(target);

        Vector3 origin = Owner.position + Vector3.up * headHeight;

        ThrownProjectile throwable = throwablePool.Get(origin, Quaternion.identity);
        throwable.Launch(throwablePool, origin, end, Stats.ThrowArc, Stats.FlightTime, onLanded);
    }

    // Leads the target by its measured velocity over the flight time, scaled by predictionStrength and
    // limited so the point never goes past the owner (enemies chasing the player stop near it).
    private Vector3 PredictLanding(Damageable target)
    {
        Vector3 position = target.transform.position;
        Vector3 lead = target.Velocity * (Stats.FlightTime * Stats.PredictionStrength);
        lead.y = 0f;

        Vector3 toOwner = Owner.position - position;
        toOwner.y = 0f;
        lead = Vector3.ClampMagnitude(lead, toOwner.magnitude);

        Vector3 end = position + lead;
        end.y = groundHeight;
        return end;
    }

    private void Land(Vector3 point)
    {
        DebrisBurst burst = burstPool.Get(point, Quaternion.identity);
        burst.Play(burstPool, Stats.ExplosionColor);

        LingerPuddle puddle = puddlePool.Get(point, Quaternion.identity);
        puddle.Begin(puddlePool, Stats, point);
    }
}
