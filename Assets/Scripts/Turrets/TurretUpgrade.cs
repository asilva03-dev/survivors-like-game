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
}
