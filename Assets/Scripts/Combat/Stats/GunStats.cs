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
 * - fanAngle (2026-11-26, Fishercat Spear Gun, opt-in): 0 on every asset = the layout above. > 0 = all projectileCount
 *   bullets leave from the muzzle spread evenly in a fan, fanAngle degrees apart, and fly straight (no curving extras).
 * - pierce (BTD meaning since 2026-11-20, user): how many enemies a bullet hits before it is used up. 1 = stops at
 *   the first hit (default), 2 = hits two, and so on; each hit deals full damage and each target is hit at most once
 *   per bullet. (Before 2026-11-20 it counted extra hits: old 0 = new 1; the assets were moved up by 1.)
 * - homing: when on, every bullet turns toward the target's current position from launch (curving
 *   bullets skip their settle arc and just turn in). homingStrength 0..1 scales the turn rate up to
 *   1080 degrees per second. If the target dies the bullet switches to the weapon's current target.
 *   A bullet that lined up on its target and then flew past it gives up, so misses fly off instead
 *   of circling.
 * - homingRadius (2026-11-23, user: "focus on nearby enemies; if an enemy is too far it won't home to it"): 0 = the old
 *   homing above (on the gun's target from launch). > 0 = each homing bullet steers only at the nearest enemy within this
 *   distance of the bullet (one it can see: shadow rats need DetectsShadow or a RevealLight), flying straight while none is
 *   that close. Upgrade copies may raise homingStrength past 1 (RaiseHomingStrength, up to 2 = 2160 deg/s) for tight turns.
 * - Slowing bullets (2026-11-23, gun turret Optics T3-T5): slowPerStack > 0 = every enemy hit gets a slow stack
 *   (Enemy.AddSlowStack: up to slowMaxStacks, all dropping slowDuration after the last; stunDuration > 0 = a full stack
 *   stuns, then stunImmunity). infectChance: each stack applied also gives every other enemy within infectRadius of the
 *   hit one a stack with that chance (infected stacks don't spread again). 0 = off on every asset; turret upgrade
 *   copies raise them (values only go up).
 * - Explosive bullets (2026-11-22, gun turret Firepower T3-T5): explosionChance of an enemy hit sets off a blast
 *   (BulletBlasts) that deals explosionDamage, with the Lead property (it breaks pots), to the explosionTargets enemies
 *   nearest the hit within explosionRadius (the hit enemy included). shrapnelCount > 0: each blast also fires that many
 *   shrapnel bullets at nearby enemies (BulletBlasts' shrapnel stats). 0 chance = no blasts (every asset by default;
 *   only turret upgrade copies change them).
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
    [Tooltip("0 = the usual side-by-side shot. > 0 = every bullet of a shot leaves from the muzzle in a fan, this many degrees apart (Fishercat spear gun).")]
    [SerializeField, Range(0f, 45f)] private float fanAngle;
    [Tooltip("Enemies one bullet hits before it is used up (1 = the first enemy only).")]
    [SerializeField, Min(1)] private int pierce = 1;
    [SerializeField] private bool homing;
    [SerializeField, Range(0f, 1f)] private float homingStrength = 0.5f;
    [Tooltip("0 = home on the gun's target from launch. > 0 = home only on the nearest enemy within this distance of the bullet.")]
    [SerializeField, Min(0f)] private float homingRadius;

    [Header("Slowing bullets (turret upgrades)")]
    [Tooltip("Speed taken off per slow stack (0.06 = 6%). 0 = bullets don't slow.")]
    [SerializeField, Range(0f, 0.5f)] private float slowPerStack;
    [SerializeField, Min(1)] private int slowMaxStacks = 5;
    [Tooltip("Seconds the stacks last after the last one was applied.")]
    [SerializeField, Min(0.1f)] private float slowDuration = 3f;
    [Tooltip("A full stack stuns for this long (0 = no stun).")]
    [SerializeField, Min(0f)] private float stunDuration;
    [Tooltip("Seconds after a stun before the same enemy can be stunned again.")]
    [SerializeField, Min(0f)] private float stunImmunity = 2f;
    [Tooltip("Chance that each stack applied also lands on each enemy near the hit one.")]
    [SerializeField, Range(0f, 1f)] private float infectChance;
    [SerializeField, Min(0f)] private float infectRadius = 2.5f;

    [Header("Explosive bullets (turret upgrades)")]
    [Tooltip("Chance that an enemy hit sets off a blast (0 = never).")]
    [SerializeField, Range(0f, 1f)] private float explosionChance;
    [Tooltip("Most enemies a blast hits (the nearest to the hit, the hit enemy included).")]
    [SerializeField, Min(1)] private int explosionTargets = 3;
    [SerializeField, Min(0f)] private float explosionDamage = 10f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 2f;
    [Tooltip("Shrapnel bullets each blast fires at nearby enemies (0 = none).")]
    [SerializeField, Min(0)] private int shrapnelCount;

    private const float MaxHomingTurnSpeed = 1080f;

    public int Pierce => Mathf.Max(1, pierce);

    // Runtime changes for upgrade copies only (see WeaponStats.AddDamage).
    public void AddPierce(int amount)
    {
        pierce = Mathf.Max(1, pierce + amount);
    }

    public void AddProjectiles(int amount)
    {
        projectileCount = Mathf.Max(1, projectileCount + amount);
    }

    // Faster bullets with the same reach: speed x multiplier, lifetime / multiplier (2026-11-26, Fishercat Spear Gun).
    public void ScaleProjectileSpeed(float multiplier)
    {
        if (multiplier <= 0f)
        {
            return;
        }
        projectileSpeed *= multiplier;
        lifetime /= multiplier;
    }

    public void SetHoming(bool on)
    {
        homing = on;
    }

    // Runtime change for upgrade copies only: each value only ever goes up (a later tier can't weaken an earlier one).
    public void RaiseHomingStrength(float strength)
    {
        homingStrength = Mathf.Clamp(Mathf.Max(homingStrength, strength), 0f, 2f);
    }

    public void RaiseHomingRadius(float radius)
    {
        homingRadius = Mathf.Max(homingRadius, radius);
    }

    public float HomingRadius => homingRadius;

    public void RaiseExplosion(float chance, int targets, float damage, float radius)
    {
        explosionChance = Mathf.Max(explosionChance, chance);
        explosionTargets = Mathf.Max(explosionTargets, targets);
        explosionDamage = Mathf.Max(explosionDamage, damage);
        explosionRadius = Mathf.Max(explosionRadius, radius);
    }

    public void RaiseSlow(float perStack, int maxStacks, float duration)
    {
        slowPerStack = Mathf.Max(slowPerStack, perStack);
        slowMaxStacks = Mathf.Max(slowMaxStacks, maxStacks);
        slowDuration = Mathf.Max(slowDuration, duration);
    }

    public void RaiseStun(float duration, float immunity)
    {
        stunDuration = Mathf.Max(stunDuration, duration);
        stunImmunity = Mathf.Max(stunImmunity, immunity);
    }

    public void RaiseInfect(float chance, float radius)
    {
        infectChance = Mathf.Max(infectChance, chance);
        infectRadius = Mathf.Max(infectRadius, radius);
    }

    public float SlowPerStack => slowPerStack;
    public int SlowMaxStacks => slowMaxStacks;
    public float SlowDuration => slowDuration;
    public float StunDuration => stunDuration;
    public float StunImmunity => stunImmunity;
    public float InfectChance => infectChance;
    public float InfectRadius => infectRadius;

    public void RaiseShrapnel(int count)
    {
        shrapnelCount = Mathf.Max(shrapnelCount, count);
    }

    public float ExplosionChance => explosionChance;
    public int ExplosionTargets => explosionTargets;
    public float ExplosionDamage => explosionDamage;
    public float ExplosionRadius => explosionRadius;
    public int ShrapnelCount => shrapnelCount;
    public bool Homing => homing;
    public float HomingStrength => homingStrength;
    // Degrees per second a homing bullet can turn toward its target (strength 1 = MaxHomingTurnSpeed).
    public float HomingTurnSpeed => homingStrength * MaxHomingTurnSpeed;

    public int ProjectileCount => projectileCount;
    public float ProjectileSpacing => projectileSpacing;
    public float CurveStartAngle => curveStartAngle;
    public float CurveAngleStep => curveAngleStep;
    public float CurveTurnSpeed => curveTurnSpeed;
    public float FanAngle => fanAngle;
    public Projectile ProjectilePrefab => projectilePrefab;
    public int PrewarmCount => prewarmCount;
    public float ProjectileSpeed => projectileSpeed;
    public float Lifetime => lifetime;
    public float MuzzleOffset => muzzleOffset;
}
