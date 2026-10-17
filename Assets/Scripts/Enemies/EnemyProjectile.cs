/*
 * EnemyProjectile
 * Pooled projectile fired by ranged enemies. Flies dead straight at a fixed speed, damages the first target it
 * touches (a Damageable whose category is in its target mask: the player and structures for ranged enemies)
 * and returns to the pool, or returns when its lifetime ends. It ignores every other collider and enemies.
 *
 * - Never created or destroyed at runtime: pools are created lazily per prefab by PoolFor() under one scene
 *   object and only grow. Launch() fully resets state.
 * - The model and its size are set on the prefab. The projectile turns to face its direction of travel (the
 *   model's +Z leads).
 * - Hits are a flat (XZ) distance check from each target to the segment travelled this frame, so fast
 *   projectiles never tunnel and height does not matter; the same pattern as BurstProjectile.
 * - Damage goes straight to Damageable.TakeDamage, so the player and structures only need a Damageable in the
 *   Player / Structure category to be hurt.
 */

using System.Collections.Generic;
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private static readonly Dictionary<EnemyProjectile, ComponentPool<EnemyProjectile>> pools =
        new Dictionary<EnemyProjectile, ComponentPool<EnemyProjectile>>();
    private static Transform poolRoot;

    private ComponentPool<EnemyProjectile> pool;
    private TargetMask targets;
    private Vector3 direction;
    private float speed;
    private float lifetime;
    private float hitRadius;
    private float damage;
    private float age;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pools.Clear();
        poolRoot = null;
    }

    // The shared pool for this prefab, created on first use (prewarmed with `prewarm`).
    public static ComponentPool<EnemyProjectile> PoolFor(EnemyProjectile prefab, int prewarm)
    {
        ComponentPool<EnemyProjectile> pool;
        if (pools.TryGetValue(prefab, out pool))
        {
            return pool;
        }

        if (poolRoot == null)
        {
            poolRoot = new GameObject("Enemy Projectiles").transform;
        }

        pool = new ComponentPool<EnemyProjectile>(prefab, poolRoot, prewarm);
        pools[prefab] = pool;
        return pool;
    }

    public void Launch(ComponentPool<EnemyProjectile> owningPool, Vector3 travelDirection, float travelSpeed,
        float maxLifetime, float touchRadius, float hitDamage, TargetMask hurts)
    {
        pool = owningPool;
        direction = travelDirection.normalized;
        speed = travelSpeed;
        lifetime = maxLifetime;
        hitRadius = touchRadius;
        damage = hitDamage;
        targets = hurts;
        age = 0f;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void Update()
    {
        Vector3 from = transform.position;
        Vector3 to = from + direction * (speed * Time.deltaTime);
        transform.position = to;

        if (HitAlong(from, to))
        {
            pool.Release(this);
            return;
        }

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            pool.Release(this);
        }
    }

    // Damages the closest target along the segment (if any) and reports whether the projectile hit.
    private bool HitAlong(Vector3 from, Vector3 to)
    {
        Vector2 a = new Vector2(from.x, from.z);
        Vector2 segment = new Vector2(to.x, to.z) - a;
        float sqrLength = segment.sqrMagnitude;
        float sqrRadius = hitRadius * hitRadius;

        Damageable best = null;
        float bestT = 2f;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!targets.Contains(candidate.Category))
            {
                continue;
            }

            Vector2 point = new Vector2(candidate.transform.position.x, candidate.transform.position.z);
            float t = sqrLength > 0.000001f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / sqrLength) : 0f;
            if ((point - (a + segment * t)).sqrMagnitude <= sqrRadius && t < bestT)
            {
                bestT = t;
                best = candidate;
            }
        }

        if (best == null)
        {
            return false;
        }

        best.TakeDamage(damage);
        return true;
    }
}
