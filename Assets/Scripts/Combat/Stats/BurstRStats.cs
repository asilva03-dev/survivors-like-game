/*
 * BurstRStats
 * Stats asset for a burst weapon: every attackInterval it fires projectileCount spinning projectiles outward
 * from around the owner, all at once, in a fixed pattern (no targeting).
 * Create via Assets > Create > Weapons > BurstR Stats.
 *
 * - projectileCount is how many leave per volley. They fan out like the WheelO slots: the first four fly
 *   north, east, south, west, the next four in between, then the next eight in between those, and so on.
 *   North is world +Z. Each projectile starts spawnRadius from the owner, spawnHeight above its position.
 * - speed (units per second) and lifetime (seconds) set how far a projectile flies (Range = speed x lifetime).
 *   A projectile is never stopped by what it hits: it hits every target it touches (each target once) and
 *   flies on until its lifetime ends.
 * - hitRadius is how close (units, flat) a projectile's path must pass to a target's centre to hit it, so
 *   it includes the target's own size.
 * - spinSpeed is degrees per second the model spins on its own axis; projectileModel is any model/prefab,
 *   scaled so its largest dimension is modelSize. Each projectile has a trail.
 * - explosionColor tints the small particle burst played where a projectile hits. It does no damage.
 * - damage, resourceDamageMultiplier and targets come from WeaponStats; attackInterval is the time between
 *   volleys (the weapon fires whether or not anything is in range).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/BurstR Stats", fileName = "BurstR")]
public class BurstRStats : WeaponStats
{
    [SerializeField, Min(1)] private int projectileCount = 8;
    [SerializeField, Min(0.1f)] private float speed = 12f;
    [SerializeField, Min(0.1f)] private float lifetime = 1.2f;
    [SerializeField, Min(0f)] private float spawnRadius = 0.6f;
    [SerializeField] private float spawnHeight = 0f;
    [SerializeField, Min(0.05f)] private float hitRadius = 0.8f;
    [SerializeField] private float spinSpeed = 720f;
    [SerializeField] private GameObject projectileModel;
    [SerializeField, Min(0.05f)] private float modelSize = 0.6f;
    [SerializeField] private Color explosionColor = new Color(0.65f, 0.55f, 0.45f, 1f);
    [SerializeField, Min(0)] private int prewarmCount = 16;

    public override float Range => speed * lifetime;
    public int ProjectileCount => projectileCount;
    public float Speed => speed;
    public float Lifetime => lifetime;
    public float SpawnRadius => spawnRadius;
    public float SpawnHeight => spawnHeight;
    public float HitRadius => hitRadius;
    public float SpinSpeed => spinSpeed;
    public GameObject ProjectileModel => projectileModel;
    public float ModelSize => modelSize;
    public Color ExplosionColor => explosionColor;
    public int PrewarmCount => prewarmCount;
}
