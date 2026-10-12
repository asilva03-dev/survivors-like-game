/*
 * Projectile
 * Pooled straight-line projectile. Flies toward where the target was when fired (no homing).
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

    public void Launch(ComponentPool<Projectile> owningPool, Vector3 travelDirection, float travelSpeed,
        WeaponStats weaponStats, float maxLifetime, Transform ignore)
    {
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
            if (target != null)
            {
                Hit(target);
                return true;
            }
        }

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

        float step = speed * Time.deltaTime;

        if (Physics.SphereCast(transform.position, hitRadius, direction, out RaycastHit hit, step,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            bool isOwner = ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot);
            if (!isOwner)
            {
                Damageable target = hit.collider.GetComponentInParent<Damageable>();
                if (target != null)
                {
                    Hit(target);
                }

                pool.Release(this);
                return;
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
