/*
 * GunStats
 * Stats asset for a gun-type weapon. Create via Assets > Create > Weapons > Gun Stats.
 *
 * - projectilePrefab decides the bullet's look, trail and any Projectile settings, so every gun
 *   can have its own bullet type.
 * - Each gun builds its own pool from its prefab, prewarmed with prewarmCount instances.
 * - lifetime is the max seconds a missed projectile survives (distance = projectileSpeed * lifetime).
 * - muzzleOffset is how far in front of the weapon cube projectiles spawn.
 * - projectileCount is bullets per shot. 1 = one bullet; 2 = two side by side; 3 = center plus left and
 *   right; 4+ = those three plus extra bullets that start from the outer left/right spots, swing out
 *   (curveStartAngle degrees off the aim, curveAngleStep more per extra bullet on the same side) and
 *   turn back to the aim direction at curveTurnSpeed degrees per second, so the arc is only at the
 *   start and they then fly straight, parallel to the others. A higher start angle or lower turn
 *   speed makes a wider, longer arc. projectileSpacing is the side-by-side gap in units.
 * - pierce: how many enemies a bullet passes through. 0 = stops at the first hit (default), 1 = survives
 *   one hit and dies on the second, and so on. Each target is hit at most once per bullet.
 * - homing: when on, every bullet turns toward the target's current position from launch (curving
 *   bullets skip their settle arc and just turn in). homingStrength 0..1 scales the turn rate up to
 *   1080 degrees per second. If the target dies the bullet switches to the weapon's current target.
 *   A bullet that lined up on its target and then flew past it gives up, so misses fly off instead
 *   of circling.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/Gun Stats", fileName = "NewGunStats")]
public class GunStats : AimedWeaponStats
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField, Min(0)] private int prewarmCount = 20;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 20f;
    [SerializeField, Min(0.1f)] private float lifetime = 2f;
    [SerializeField] private float muzzleOffset = 0.6f;
    [SerializeField, Min(1)] private int projectileCount = 1;
    [SerializeField, Min(0f)] private float projectileSpacing = 0.4f;
    [SerializeField, Range(0f, 120f)] private float curveStartAngle = 50f;
    [SerializeField, Range(0f, 90f)] private float curveAngleStep = 25f;
    [SerializeField, Min(1f)] private float curveTurnSpeed = 240f;
    [SerializeField, Min(0)] private int pierce;
    [SerializeField] private bool homing;
    [SerializeField, Range(0f, 1f)] private float homingStrength = 0.5f;

    private const float MaxHomingTurnSpeed = 1080f;

    public int Pierce => pierce;

    // Runtime changes for upgrade copies only (see WeaponStats.AddDamage).
    public void AddPierce(int amount)
    {
        pierce = Mathf.Max(0, pierce + amount);
    }

    public void AddProjectiles(int amount)
    {
        projectileCount = Mathf.Max(1, projectileCount + amount);
    }

    public void SetHoming(bool on)
    {
        homing = on;
    }
    public bool Homing => homing;
    public float HomingStrength => homingStrength;
    // Degrees per second a homing bullet can turn toward its target (strength 1 = MaxHomingTurnSpeed).
    public float HomingTurnSpeed => homingStrength * MaxHomingTurnSpeed;

    public int ProjectileCount => projectileCount;
    public float ProjectileSpacing => projectileSpacing;
    public float CurveStartAngle => curveStartAngle;
    public float CurveAngleStep => curveAngleStep;
    public float CurveTurnSpeed => curveTurnSpeed;
    public Projectile ProjectilePrefab => projectilePrefab;
    public int PrewarmCount => prewarmCount;
    public float ProjectileSpeed => projectileSpeed;
    public float Lifetime => lifetime;
    public float MuzzleOffset => muzzleOffset;
}
