/*
 * GunWeapon
 * Aimed weapon that shoots pooled Projectiles at the target's position.
 *
 * - All numbers and the bullet prefab come from a GunStats asset, so different guns are just
 *   different assets on this same component.
 * - Builds its own ComponentPool<Projectile> in Awake from the stats' prefab, prewarmed with
 *   prewarmCount. The pool grows if it runs dry, so shots are never skipped.
 * - The pool root is a separate scene object so projectiles do not move with the player.
 * - Projectiles fly in a straight line toward where the target was at fire time.
 * - If the target is closer than the muzzle (enemy right up against the player), the projectile
 *   spawns at the weapon itself so it never starts past the target or fires backwards from the muzzle.
 */

using UnityEngine;

public class GunWeapon : AimedWeapon<GunStats>
{
    private ComponentPool<Projectile> pool;

    protected override void Awake()
    {
        base.Awake();

        Transform poolRoot = new GameObject(name + " Projectiles").transform;
        pool = new ComponentPool<Projectile>(Stats.ProjectilePrefab, poolRoot, Stats.PrewarmCount);
    }

    protected override void Fire(Damageable target)
    {
        Vector3 muzzle = transform.position + transform.forward * Stats.MuzzleOffset;
        bool targetBeyondMuzzle = Vector3.Dot(target.AimPoint - muzzle, transform.forward) > 0f;
        Vector3 origin = targetBeyondMuzzle ? muzzle : transform.position;

        Vector3 direction = target.AimPoint - origin;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        Projectile projectile = pool.Get(origin, Quaternion.LookRotation(direction));
        projectile.Launch(pool, direction, Stats.ProjectileSpeed, Stats, Stats.Lifetime, Owner);
    }
}
