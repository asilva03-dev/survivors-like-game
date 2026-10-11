/*
 * GunStats
 * Stats asset for a gun-type weapon. Create via Assets > Create > Weapons > Gun Stats.
 *
 * - projectilePrefab decides the bullet's look, trail and any Projectile settings, so every gun
 *   can have its own bullet type.
 * - Each gun builds its own pool from its prefab, prewarmed with prewarmCount instances.
 * - lifetime is the max seconds a missed projectile survives (distance = projectileSpeed * lifetime).
 * - muzzleOffset is how far in front of the weapon cube projectiles spawn.
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

    public Projectile ProjectilePrefab => projectilePrefab;
    public int PrewarmCount => prewarmCount;
    public float ProjectileSpeed => projectileSpeed;
    public float Lifetime => lifetime;
    public float MuzzleOffset => muzzleOffset;
}
