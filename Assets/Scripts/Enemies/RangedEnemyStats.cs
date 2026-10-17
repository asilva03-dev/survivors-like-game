/*
 * RangedEnemyStats
 * Stats asset for a ranged enemy type: it walks toward the player, stops once the player is within `range`,
 * and shoots straight projectiles. A different ranged enemy is a new asset (and prefab/model) with other
 * numbers. Create via Assets > Create > Enemies > Ranged Enemy Stats.
 *
 * - maxHealth and moveSpeed come from EnemyStats.
 * - range is how close (units, flat) the player must be before the enemy stops and shoots.
 * - fireInterval is the seconds between volleys; projectileCount is how many projectiles leave per volley.
 *   One flies straight at the player; several fan out evenly across spreadAngle degrees (each still flies
 *   in a straight line).
 * - projectileSpeed (units per second), projectileLifetime (seconds) and damage set the projectile; it
 *   flies straight and is released on its first hit or when its lifetime ends. hitRadius is how close (flat)
 *   its path must pass to a target's centre to hit it, so it includes the target's own size.
 * - targets is what the projectile can hurt: the player and structures by default (neither exists yet, so
 *   nothing is hit until they are Damageables in those categories).
 * - After shotsBeforeMoveMin..Max volleys (random each time) the enemy rolls moveCloserChance; on success it
 *   walks toward the player for advanceDistance units, then stands and shoots again.
 * - projectilePrefab is the pooled EnemyProjectile prefab (its model and size are set on the prefab).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Enemies/Ranged Enemy Stats", fileName = "NewRangedEnemyStats")]
public class RangedEnemyStats : EnemyStats
{
    [SerializeField, Min(1f)] private float range = 9f;
    [SerializeField, Min(0.1f)] private float fireInterval = 1.5f;
    [SerializeField, Min(1)] private int projectileCount = 1;
    [SerializeField, Range(0f, 90f)] private float spreadAngle = 15f;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 2f;
    [SerializeField, Min(0f)] private float damage = 5f;
    [SerializeField, Min(0.05f)] private float hitRadius = 0.5f;
    [SerializeField] private TargetMask targets = TargetMask.Player | TargetMask.Structure;
    [SerializeField, Min(1)] private int shotsBeforeMoveMin = 2;
    [SerializeField, Min(1)] private int shotsBeforeMoveMax = 4;
    [SerializeField, Range(0f, 1f)] private float moveCloserChance = 0.5f;
    [SerializeField, Min(0.1f)] private float advanceDistance = 3f;
    [SerializeField] private EnemyProjectile projectilePrefab;

    public float Range => range;
    public float FireInterval => fireInterval;
    public int ProjectileCount => projectileCount;
    public float SpreadAngle => spreadAngle;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public float Damage => damage;
    public float HitRadius => hitRadius;
    public TargetMask Targets => targets;
    public int ShotsBeforeMoveMin => shotsBeforeMoveMin;
    public int ShotsBeforeMoveMax => Mathf.Max(shotsBeforeMoveMin, shotsBeforeMoveMax);
    public float MoveCloserChance => moveCloserChance;
    public float AdvanceDistance => advanceDistance;
    public EnemyProjectile ProjectilePrefab => projectilePrefab;
}
