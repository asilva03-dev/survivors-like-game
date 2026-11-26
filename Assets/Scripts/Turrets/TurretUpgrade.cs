/*
 * TurretUpgrade
 * One upgrade tier of a turret path (Create > TD > Turret Upgrade), Bloons-style (slice 6 step 5). The name,
 * description and wood cost show on the upgrade panel; the changes are applied on top of the turret's base gun stats
 * (on that turret's own runtime copy, never the shared asset).
 *
 * - damageAdd: + damage per bullet. fireRateMultiplier: 1.25 = 25% more shots per second (1 = no change).
 * - rangeAdd: + range. pierceAdd: + enemies a bullet passes through. projectilesAdd: + bullets per shot.
 * - enableHoming: bullets steer at the target (GunStats homing, its homingStrength).
 * - rangeMultiplier (2026-11-22, gun turret 3x5 upgrades): 1.25 = 25% more range, applied after rangeAdd.
 *   grantsShadowDetection: the turret can see and target shadow rats (AimedWeaponStats.detectsShadow).
 * - Explosive bullets (2026-11-22, Firepower T3-T5): explosionChance > 0 turns on blasts (GunStats.RaiseExplosion: chance,
 *   targets, damage, radius only ever go up); shrapnelCount > 0 adds shrapnel to every blast.
 * - Slowing bullets (2026-11-23, Optics T3-T5): slowPerStack > 0 turns on slows (+ slowMaxStacks, slowDuration);
 *   stunDuration > 0 adds the stun at a full stack (+ stunImmunity); infectChance > 0 adds the spreading (+ infectRadius).
 *   Values only ever go up (GunStats.RaiseSlow / RaiseStun / RaiseInfect).
 * - Homing (2026-11-23): homingStrength > 0 raises the gun's turn strength (up to 2 = 2160 deg/s), homingRadius > 0 makes
 *   bullets home only on enemies that close to them (see GunStats.homingRadius).
 * - bodyMesh (2026-11-23, Fire Rate T3 Double Barreled): when set, buying this tier swaps the turret's body model to this
 *   mesh (Turret.bodyModel); the highest bought tier with one wins. Empty = no change.
 * - effectBuilt (2026-11-22): false = the tier's special effect isn't in the game yet; it shows "Not built yet" on the
 *   panel and can't be bought (Turret.CanUpgrade). Descriptions must not show numbers (user's rule).
 * - Names, descriptions and numbers are placeholders for the user to rename and retune in the inspector.
 * - All towers (2026-11-25): besides ApplyTo(GunStats), Turret also applies each tier to its own copies of the tower's
 *   TrapperStats (Trapper), SwordSwingStats (SwordsCat swing) and ArrowVolleyStats (Angel Cat volley). Those ApplyTo
 *   overloads are empty until each tower's spec adds its fields.
 * - projectileSpeedMultiplier (2026-11-26, Fishercat Spear Gun): bullets fly faster with the same reach (GunStats
 *   ScaleProjectileSpeed: speed x, lifetime /). 1 = no change on every other tier.
 * - Fishercat nets (2026-11-26, Path 2): netUnlock, netRadius, netStun, netSpikeDamage, netFatStun, netPersist, netCount
 *   (ApplyTo(FishercatNetStats); values only go up; only towers with a FishercatNetThrower use them).
 * - Fishercat fishing (2026-11-26, Path 3): fishingUnlock, fishingPoolRadius, fishingSlow, fishingReveal,
 *   fishingSharkDamage, fishingSharkCooldown, fishingBigShark (ApplyTo(FishercatFishingStats); only improve values; only
 *   towers with a FishercatFishing use them).
 * - Fishercat reel (2026-11-26, Path 1 T4/T5): reelUnlock turns the reel on, reelHpCap raises its HP cap, reelLightRange
 *   makes it reach the whole BaseLight (ApplyTo(FishercatReelStats); only towers with a FishercatReel use it).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Turret Upgrade", fileName = "TurretUpgrade")]
public class TurretUpgrade : ScriptableObject
{
    [SerializeField] private string upgradeName = "Upgrade";
    [SerializeField, TextArea(2, 4)] private string description = "";
    [SerializeField, Min(0)] private int woodCost = 20;
    [Tooltip("Off = this tier's effect isn't built yet: the panel shows it but it can't be bought.")]
    [SerializeField] private bool effectBuilt = true;

    [Header("Changes")]
    [SerializeField] private float damageAdd;
    [SerializeField, Min(0.1f)] private float fireRateMultiplier = 1f;
    [SerializeField] private float rangeAdd;
    [SerializeField] private int pierceAdd;
    [SerializeField] private int projectilesAdd;
    [SerializeField] private bool enableHoming;
    [Tooltip("Raises the homing turn strength (1 = 1080 deg/s, max 2). 0 = no change.")]
    [SerializeField, Range(0f, 2f)] private float homingStrength;
    [Tooltip("Bullets home only on enemies within this distance of them. 0 = no change.")]
    [SerializeField, Min(0f)] private float homingRadius;
    [Tooltip("1.25 = 25% more range (after Range Add).")]
    [SerializeField, Min(0.1f)] private float rangeMultiplier = 1f;
    [Tooltip("The turret can see and target shadow rats.")]
    [SerializeField] private bool grantsShadowDetection;
    [Tooltip("1.5 = bullets fly 50% faster, same reach (1 = no change).")]
    [SerializeField, Min(0.1f)] private float projectileSpeedMultiplier = 1f;

    [Tooltip("Swap the turret's body model to this mesh when bought (empty = no change).")]
    [SerializeField] private Mesh bodyMesh;

    [Header("Slowing bullets (0 = no change)")]
    [SerializeField, Range(0f, 0.5f)] private float slowPerStack;
    [SerializeField, Min(1)] private int slowMaxStacks = 5;
    [SerializeField, Min(0.1f)] private float slowDuration = 3f;
    [SerializeField, Min(0f)] private float stunDuration;
    [SerializeField, Min(0f)] private float stunImmunity = 2f;
    [SerializeField, Range(0f, 1f)] private float infectChance;
    [SerializeField, Min(0f)] private float infectRadius = 2.5f;

    [Header("Explosive bullets (0 chance = no change)")]
    [SerializeField, Range(0f, 1f)] private float explosionChance;
    [SerializeField, Min(1)] private int explosionTargets = 3;
    [SerializeField, Min(0f)] private float explosionDamage = 10f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 2f;
    [Tooltip("Shrapnel bullets per blast (0 = no change).")]
    [SerializeField, Min(0)] private int shrapnelCount;

    [Header("Fishercat reel (off / 0 = no change)")]
    [Tooltip("Turns on the Fishercat's reel-in (Dinner Time).")]
    [SerializeField] private bool reelUnlock;
    [Tooltip("Raises the reel's HP cap to this (0 = no change).")]
    [SerializeField, Min(0f)] private float reelHpCap;
    [Tooltip("The reel reaches any rat inside the base's light.")]
    [SerializeField] private bool reelLightRange;

    [Header("Fishercat nets (off / 0 = no change)")]
    [Tooltip("Turns on the Fishercat's net throw.")]
    [SerializeField] private bool netUnlock;
    [Tooltip("Raises the net's radius to this (0 = no change).")]
    [SerializeField, Min(0f)] private float netRadius;
    [Tooltip("Raises the stun on caught rats to this many seconds (0 = no change).")]
    [SerializeField, Min(0f)] private float netStun;
    [Tooltip("Raises the damage the net deals to every rat it catches (0 = no change).")]
    [SerializeField, Min(0f)] private float netSpikeDamage;
    [Tooltip("The net can catch fat (stun-immune) rats, for this many seconds (0 = no change).")]
    [SerializeField, Min(0f)] private float netFatStun;
    [Tooltip("The net stays on the floor this many seconds and pulls rats in (0 = no change).")]
    [SerializeField, Min(0f)] private float netPersist;
    [Tooltip("Raises the nets per throw to this, each at its own spot (0 = no change).")]
    [SerializeField, Min(0)] private int netCount;

    [Header("Fishercat fishing (off / 0 = no change)")]
    [Tooltip("Turns on the Fishercat's fishing pool.")]
    [SerializeField] private bool fishingUnlock;
    [Tooltip("Raises the pool's radius to this (0 = no change).")]
    [SerializeField, Min(0f)] private float fishingPoolRadius;
    [Tooltip("Lowers the share of speed enemies keep in the pool to this (0 = no change).")]
    [SerializeField, Range(0f, 1f)] private float fishingSlow;
    [Tooltip("Shadow rats in the pool can be targeted by every tower.")]
    [SerializeField] private bool fishingReveal;
    [Tooltip("Raises the shark's bite damage to this (0 = no change; > 0 turns the shark on).")]
    [SerializeField, Min(0f)] private float fishingSharkDamage;
    [Tooltip("Lowers the seconds between sharks to this (0 = no change).")]
    [SerializeField, Min(0f)] private float fishingSharkCooldown;
    [Tooltip("The great white comes out instead of the small shark.")]
    [SerializeField] private bool fishingBigShark;

    public string UpgradeName => string.IsNullOrEmpty(upgradeName) ? name : upgradeName;
    public string Description => description;
    public int WoodCost => woodCost;
    public bool EffectBuilt => effectBuilt;
    public Mesh BodyMesh => bodyMesh;

    public void ApplyTo(GunStats stats)
    {
        stats.AddDamage(damageAdd);
        stats.ScaleFireRate(fireRateMultiplier);
        stats.AddRange(rangeAdd);
        stats.ScaleRange(rangeMultiplier);
        if (grantsShadowDetection)
        {
            stats.SetDetectsShadow(true);
        }
        if (slowPerStack > 0f)
        {
            stats.RaiseSlow(slowPerStack, slowMaxStacks, slowDuration);
        }
        if (stunDuration > 0f)
        {
            stats.RaiseStun(stunDuration, stunImmunity);
        }
        if (infectChance > 0f)
        {
            stats.RaiseInfect(infectChance, infectRadius);
        }
        if (explosionChance > 0f)
        {
            stats.RaiseExplosion(explosionChance, explosionTargets, explosionDamage, explosionRadius);
        }
        if (shrapnelCount > 0)
        {
            stats.RaiseShrapnel(shrapnelCount);
        }
        stats.AddPierce(pierceAdd);
        stats.AddProjectiles(projectilesAdd);
        if (!Mathf.Approximately(projectileSpeedMultiplier, 1f))
        {
            stats.ScaleProjectileSpeed(projectileSpeedMultiplier);
        }
        if (enableHoming)
        {
            stats.SetHoming(true);
        }
        if (homingStrength > 0f)
        {
            stats.RaiseHomingStrength(homingStrength);
        }
        if (homingRadius > 0f)
        {
            stats.RaiseHomingRadius(homingRadius);
        }
    }

    // Other towers' weapons (2026-11-25 upgrade pass): Turret calls these on its own copies of each stats type the tower
    // has. They change nothing yet; each tower's fields are added here when its upgrade spec is written.

    public void ApplyTo(TrapperStats stats)
    {
    }

    public void ApplyTo(SwordSwingStats stats)
    {
    }

    public void ApplyTo(ArrowVolleyStats stats)
    {
    }

    // Fishercat fishing (2026-11-26, Path 3): only towers with a FishercatFishing get these (Turret).
    public void ApplyTo(FishercatFishingStats stats)
    {
        stats.Apply(fishingUnlock, fishingPoolRadius, fishingSlow, fishingReveal, fishingSharkDamage, fishingSharkCooldown, fishingBigShark);
    }

    // Fishercat nets (2026-11-26, Path 2): only towers with a FishercatNetThrower get these (Turret).
    public void ApplyTo(FishercatNetStats stats)
    {
        if (netUnlock)
        {
            stats.Unlock();
        }
        stats.Raise(netRadius, netStun, netSpikeDamage, netFatStun, netPersist);
        if (netCount > 0)
        {
            stats.RaiseNetCount(netCount);
        }
    }

    // Fishercat reel (2026-11-26, Path 1 T4/T5): only towers with a FishercatReel get these (Turret).
    public void ApplyTo(FishercatReelStats stats)
    {
        if (reelUnlock)
        {
            stats.Unlock();
        }
        if (reelHpCap > 0f)
        {
            stats.RaiseHpCap(reelHpCap);
        }
        if (reelLightRange)
        {
            stats.SetUseLightRange();
        }
    }
}
