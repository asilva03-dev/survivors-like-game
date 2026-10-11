/*
 * Projectile
 * Pooled straight-line projectile. Flies toward where the target was when fired (no homing).
 *
 * - Never created or destroyed at runtime: the weapon's ComponentPool owns it. Launch()
 *   fully resets state and the pool returns it with Release() on hit or when lifetime ends.
 * - Hits are detected with a SphereCast along each frame's movement, so fast projectiles
 *   do not tunnel through enemies.
 * - SphereCast ignores colliders it starts inside, so Launch also checks for a target overlapping
 *   the spawn point and hits it immediately (point-blank shots).
 * - Only IDamageable colliders take damage; the owner's own colliders are ignored.
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
    private Transform ignoreRoot;
    private Vector3 direction;
    private float speed;
    private float damage;
    private float lifetime;
    private float age;

    public void Launch(ComponentPool<Projectile> owningPool, Vector3 travelDirection, float travelSpeed,
        float hitDamage, float maxLifetime, Transform ignore)
    {
        pool = owningPool;
        direction = travelDirection;
        speed = travelSpeed;
        damage = hitDamage;
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

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage);
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;

        if (Physics.SphereCast(transform.position, hitRadius, direction, out RaycastHit hit, step,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            bool isOwner = ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot);
            if (!isOwner)
            {
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(damage);
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
