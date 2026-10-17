/*
 * BurstRWeapon
 * Burst weapon: every attackInterval it fires projectileCount spinning projectiles outward from around the
 * owner, all at once. No targeting, no model of its own, and it fires whether or not anything is in range.
 *
 * - All numbers come from a BurstRStats asset. Not an AimedWeapon: it never registers with the
 *   WeaponLoadout, so it takes no part in targeting, claiming or ring spacing.
 * - Directions follow the WheelO slot pattern (WheelOWeapon.SlotAngle): the first four go N/E/S/W, then
 *   the gaps between them, doubling each level. The pattern is the same every volley.
 * - Builds one hidden BurstProjectile template (prefab + stats' projectileModel) in Awake and pools copies;
 *   the pool grows if it runs dry. Pools (projectiles, hit bursts) live under a separate scene object so
 *   they do not move with the player.
 * - Each projectile hit plays a tinted DebrisBurst at the hit point (visual only, no damage). The burst
 *   prefab is set on the weapon prefab (ThrowExplosion, like the throw weapons).
 */

using UnityEngine;

public class BurstRWeapon : WeaponBase<BurstRStats>
{
    [SerializeField] private BurstProjectile projectilePrefab;
    [SerializeField] private DebrisBurst burstPrefab;

    private ComponentPool<BurstProjectile> projectilePool;
    private ComponentPool<DebrisBurst> burstPool;
    private System.Action<Vector3> onHit;

    protected override void Awake()
    {
        base.Awake();

        Transform root = new GameObject(name + " Projectiles").transform;

        BurstProjectile template = Instantiate(projectilePrefab, root);
        template.SetModel(Stats.ProjectileModel, Stats.ModelSize);
        template.gameObject.SetActive(false);

        projectilePool = new ComponentPool<BurstProjectile>(template, root, Stats.PrewarmCount);
        burstPool = new ComponentPool<DebrisBurst>(burstPrefab, root, Mathf.Max(4, Stats.ProjectileCount));
        onHit = PlayHitBurst;
    }

    protected override void Tick()
    {
        if (!IsReady)
        {
            return;
        }

        Fire();
        StartCooldown();
    }

    private void Fire()
    {
        int count = Stats.ProjectileCount;
        Vector3 center = Owner.position + Vector3.up * Stats.SpawnHeight;

        for (int i = 0; i < count; i++)
        {
            float radians = WheelOWeapon.SlotAngle(i) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 origin = center + direction * Stats.SpawnRadius;

            BurstProjectile projectile = projectilePool.Get(origin, Quaternion.LookRotation(direction));
            projectile.Launch(projectilePool, direction, Stats.Speed, Stats.Lifetime, Stats.HitRadius,
                Stats.SpinSpeed, Stats, onHit);
        }
    }

    private void PlayHitBurst(Vector3 point)
    {
        DebrisBurst burst = burstPool.Get(point, Quaternion.identity);
        burst.Play(burstPool, Stats.ExplosionColor);
    }
}
