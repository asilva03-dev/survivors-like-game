/*
 * GunWeapon
 * Aimed weapon that shoots pooled Projectiles at the target's position.
 *
 * - All numbers and the bullet prefab come from a GunStats asset, so different guns are just
 *   different assets on this same component.
 * - Builds its own ComponentPool<Projectile> in Awake from the stats' prefab, prewarmed with
 *   prewarmCount. The pool grows if it runs dry, so shots are never skipped.
 * - The pool root is a separate scene object so projectiles do not move with the player; it is destroyed with the
 *   gun (a sold turret).
 * - Projectiles fly in a straight line toward where the target was at fire time.
 * - projectileCount: up to 3 bullets leave side by side (parallel, projectileSpacing apart: 1 = center,
 *   2 = either side of center, 3 = left, center, right). Bullets beyond 3 leave together with them
 *   from the outer spots, alternating right and left, angled outward and turning back to the aim
 *   direction (see GunStats), so they arc out of the way of the others and then fly straight and
 *   parallel to them (no homing).
 * - With GunStats.homing on, every bullet (the curving ones too, they skip their fixed settle arc)
 *   steers at the target from launch, and takes the weapon's current target from the shared
 *   targeting (WeaponLoadout) if its own target dies in flight.
 * - If the target is closer than the muzzle (enemy right up against the player), the projectile
 *   spawns at the weapon itself so it never starts past the target or fires backwards from the muzzle.
 */

using UnityEngine;

public class GunWeapon : AimedWeapon<GunStats>
{
    private const int StraightLimit = 3;

    private ComponentPool<Projectile> pool;
    private Transform poolRoot;
    private System.Func<Damageable> retarget;

    protected override void Awake()
    {
        base.Awake();
        retarget = () => CurrentTarget;

        poolRoot = new GameObject(name + " Projectiles").transform;
        pool = new ComponentPool<Projectile>(Stats.ProjectilePrefab, poolRoot, Stats.PrewarmCount);
    }

    // The pool lives at the scene root; remove it with the gun (a sold turret), bullets in flight included.
    protected virtual void OnDestroy()
    {
        if (poolRoot != null)
        {
            Destroy(poolRoot.gameObject);
        }
    }

    protected override void Fire(Damageable target)
    {
        Vector3 muzzle = transform.position + transform.forward * Stats.MuzzleOffset;
        bool targetBeyondMuzzle = Vector3.Dot(target.AimPoint - muzzle, transform.forward) > 0f;
        Vector3 origin = targetBeyondMuzzle ? muzzle : transform.position;

        Vector3 direction = target.AimPoint - origin;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        float spacing = Stats.ProjectileSpacing;
        int count = Stats.ProjectileCount;
        int straight = Mathf.Min(count, StraightLimit);

        for (int i = 0; i < straight; i++)
        {
            float lateral = (i - (straight - 1) * 0.5f) * spacing;
            Spawn(origin + right * lateral, direction, null, target);
        }

        for (int k = 0; k < count - StraightLimit; k++)
        {
            float side = k % 2 == 0 ? 1f : -1f;
            float angle = Stats.CurveStartAngle + (k / 2) * Stats.CurveAngleStep;
            Vector3 outward = Quaternion.AngleAxis(side * angle, Vector3.up) * direction;
            Spawn(origin + right * (side * spacing), outward, direction, target);
        }
    }

    private void Spawn(Vector3 position, Vector3 direction, Vector3? settleDirection, Damageable target)
    {
        Projectile projectile = pool.Get(position, Quaternion.LookRotation(direction));
        projectile.Launch(pool, direction, Stats.ProjectileSpeed, Stats, Stats.Lifetime, Owner,
            settleDirection, Stats.CurveTurnSpeed,
            Stats.Homing ? target : null, Stats.Homing ? Stats.HomingTurnSpeed : 0f, retarget,
            Stats.Pierce);
    }
}
