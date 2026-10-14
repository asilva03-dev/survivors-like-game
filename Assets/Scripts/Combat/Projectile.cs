/*
 * Projectile
 * Pooled projectile. Normally flies straight toward where the target was when fired; homing is opt-in
 * per gun (GunStats.homing / homingStrength).
 * Extra bullets of a multi-shot (GunStats.projectileCount > 3) are launched angled away from the aim
 * and turn back to the aim direction at a fixed rate (an arc at the start), then fly straight.
 * Homing bullets (every bullet of a homing gun, including the curving ones) skip the fixed settle
 * heading and steer at the target from launch. If the target dies mid-flight they ask the firing
 * weapon (retarget callback = the loadout's current target for that weapon) for a new one. They give
 * up for good only after they have lined up on the target and then flown past it.
 * Pierce (GunStats.pierce): a bullet survives that many damageable hits and is released on the next;
 * each target is hit at most once per bullet. Non-damageable colliders still stop it.
 *
 * - Never created or destroyed at runtime: the weapon's ComponentPool owns it. Launch()
 *   fully resets state and the pool returns it with Release() on hit or when lifetime ends.
 * - Damage is looked up per target category from the firing weapon's stats (WeaponStats.DamageFor),
 *   so the same projectile hurts enemies and resources by different amounts.
 * - Hits are detected with a SphereCast along each frame's movement, so fast projectiles
 *   do not tunnel through targets.
 * - SphereCast ignores colliders it starts inside, so Launch checks for a target overlapping the
 *   spawn point (point-blank shots) and every Update checks for one overlapping the current position
 *   before casting. The second matters because colliders only move to their new positions at the
 *   physics step, so an enemy collider can land on a flying bullet between two casts; without the
 *   overlap check that bullet would pass straight through the enemy.
 * - Only Damageable colliders take damage; the owner's own colliders are ignored.
 *   Hitting any other collider also returns the projectile to the pool.
 * - The trail is cleared on launch so a reused projectile does not streak from its old position.
 * - Pooled prefab setup: shared unlit material, shadows off, TrailRenderer on a child at the back.
 */

using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float hitRadius = 0.15f;

    private static readonly Collider[] overlapBuffer = new Collider[8];

    private ComponentPool<Projectile> pool;
    private WeaponStats stats;
    private Transform ignoreRoot;
    private Vector3 direction;
    private float speed;
    private float lifetime;
    private float age;
    private Vector3 finalDirection;
    private float steerSpeed;
    private bool steering;
    private Damageable homingTarget;
    private float homingSpeed;
    private bool homingActive;
    private bool homingAcquired;
    private System.Func<Damageable> retarget;
    private int piercesLeft;
    private readonly System.Collections.Generic.List<Damageable> hitTargets = new System.Collections.Generic.List<Damageable>();

    public void Launch(ComponentPool<Projectile> owningPool, Vector3 travelDirection, float travelSpeed,
        WeaponStats weaponStats, float maxLifetime, Transform ignore,
        Vector3? settleDirection = null, float steerDegreesPerSecond = 0f,
        Damageable homeOn = null, float homingDegreesPerSecond = 0f, System.Func<Damageable> retargetCallback = null,
        int pierce = 0)
    {
        piercesLeft = Mathf.Max(0, pierce);
        hitTargets.Clear();
        homingActive = homingDegreesPerSecond > 0f && homeOn != null;
        homingTarget = homingActive ? homeOn : null;
        homingSpeed = homingDegreesPerSecond;
        homingAcquired = false;
        retarget = homingActive ? retargetCallback : null;
        steerSpeed = steerDegreesPerSecond;
        finalDirection = settleDirection.GetValueOrDefault();
        steering = !homingActive && settleDirection.HasValue && steerSpeed > 0f;
        pool = owningPool;
        direction = travelDirection;
        speed = travelSpeed;
        stats = weaponStats;
        lifetime = maxLifetime;
        ignoreRoot = ignore;
        age = 0f;

        transform.rotation = Quaternion.LookRotation(direction);

        if (trail != null)
        {
            trail.Clear();
        }

        if (HitOverlappingTarget())
        {
            pool.Release(this);
        }
    }

    private bool HitOverlappingTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, overlapBuffer,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapBuffer[i];
            if (ignoreRoot != null && other.transform.IsChildOf(ignoreRoot))
            {
                continue;
            }

            Damageable target = other.GetComponentInParent<Damageable>();
            if (target != null && !hitTargets.Contains(target))
            {
                if (HitAndCheckDone(target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Curving projectiles (extra bullets of a multi-shot) are launched angled away from the aim and
    // turn back to the aim direction at a fixed rate. It is an opening arc only: once the bullet
    // points along the aim direction it keeps that heading and flies dead straight.
    private void SteerToHeading()
    {
        if (!steering)
        {
            Home();
            return;
        }

        direction = Vector3.RotateTowards(direction, finalDirection, steerSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        transform.rotation = Quaternion.LookRotation(direction);

        if (Vector3.Angle(direction, finalDirection) < 0.1f)
        {
            direction = finalDirection;
            steering = false;
        }
    }

    // Optional homing (GunStats.homing): turn toward the target's current position at a limited rate.
    // A dead target is replaced by the firing weapon's current target. A bullet that was lined up on
    // its target (within 90 degrees) and then ends up facing away from it has flown past: it gives up
    // for good so misses fly off instead of orbiting. Bullets launched angled away start unacquired
    // and keep turning in until they line up.
    private void Home()
    {
        if (!homingActive)
        {
            return;
        }

        if (homingTarget != null && (!homingTarget.isActiveAndEnabled || hitTargets.Contains(homingTarget)))
        {
            homingTarget = null;
            homingAcquired = false;
        }

        if (homingTarget == null && retarget != null)
        {
            homingTarget = retarget();
            if (homingTarget != null && hitTargets.Contains(homingTarget))
            {
                homingTarget = null;
            }
        }

        if (homingTarget == null)
        {
            return;
        }

        Vector3 wanted = homingTarget.AimPoint - transform.position;
        if (wanted.sqrMagnitude < 0.0001f)
        {
            return;
        }

        wanted.Normalize();
        float angle = Vector3.Angle(direction, wanted);
        if (angle <= 90f)
        {
            homingAcquired = true;
        }
        else if (homingAcquired)
        {
            homingActive = false;
            homingTarget = null;
            return;
        }

        direction = Vector3.RotateTowards(direction, wanted, homingSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        transform.rotation = Quaternion.LookRotation(direction);
    }

    // Deals damage and remembers the target so a piercing bullet never hits it twice.
    // Returns true when the bullet has no pierces left and must be released.
    private bool HitAndCheckDone(Damageable target)
    {
        Hit(target);

        if (piercesLeft <= 0)
        {
            return true;
        }

        piercesLeft--;
        hitTargets.Add(target);
        return false;
    }

    private void Hit(Damageable target)
    {
        float amount = stats.DamageFor(target.Category);
        if (amount > 0f)
        {
            target.TakeDamage(amount);
        }
    }

    private void Update()
    {
        // Colliders only jump to their new positions at the physics step, so one can land on the bullet
        // between two casts; a cast ignores what it starts inside, so check the current position first.
        if (HitOverlappingTarget())
        {
            pool.Release(this);
            return;
        }

        SteerToHeading();

        float step = speed * Time.deltaTime;

        if (Physics.SphereCast(transform.position, hitRadius, direction, out RaycastHit hit, step,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            bool isOwner = ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot);
            if (!isOwner)
            {
                Damageable target = hit.collider.GetComponentInParent<Damageable>();
                if (target == null || !hitTargets.Contains(target))
                {
                    if (target == null || HitAndCheckDone(target))
                    {
                        pool.Release(this);
                        return;
                    }
                }
            }
        }

        transform.position += direction * step;

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            pool.Release(this);
        }
    }
}
