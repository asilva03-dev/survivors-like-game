/*
 * WeaponStats
 * Base ScriptableObject holding the stats every weapon type shares. Each weapon type adds its own
 * subclass (AimedWeaponStats -> GunStats, AreaStats, later ThrowStats, SpawnStats, ...).
 *
 * - One asset per weapon (e.g. "Basic Gun", "Heavy Gun"); weapon components reference an asset
 *   instead of holding their own numbers, so adding a weapon variant is just a new asset.
 * - Range is the weapon's reach from its owner. Each subclass defines the serialized value (a gun's
 *   range, an AOE's radius) so inspector labels match what the number means.
 * - attackInterval is the seconds between attacks (lower = faster). Guns and AOE use it as their
 *   cooldown; melee uses it as the lockout after a normal swing.
 * - Damage is per target category: damage is what the weapon deals to enemies; resources (trees,
 *   stone, ...) take damage x resourceDamageMultiplier rounded up to a whole number, so later damage
 *   buffs scale resource damage consistently. Structures and the player are never damaged by weapons.
 *   DamageFor(category) returns 0 for any category not in targets.
 * - targets is which categories the weapon will aim at and hurt (enemies and resources by default).
 * - Edits to an asset apply live to every weapon using it, including during Play mode.
 */

using UnityEngine;

public abstract class WeaponStats : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float attackInterval = 0.4f;
    [SerializeField] private float damage = 10f;
    [SerializeField, Min(0f)] private float resourceDamageMultiplier = 1f;
    [SerializeField] private TargetMask targets = TargetMask.Enemy | TargetMask.Resource;

    public abstract float Range { get; }
    public float AttackInterval => attackInterval;
    public float Damage => damage;
    public float ResourceDamageMultiplier => resourceDamageMultiplier;
    public float ResourceDamage => ResourceDamageFor(damage);

    // Damage dealt to resources for a given enemy damage value, rounded up to a whole number.
    public float ResourceDamageFor(float baseDamage)
    {
        return Mathf.Ceil(baseDamage * resourceDamageMultiplier - 0.0001f);
    }
    public TargetMask Targets => targets;

    // Runtime changes, for upgrade copies only (turret upgrades call these on an Instantiate'd copy, never the asset).
    public void AddDamage(float amount)
    {
        damage += amount;
    }

    // multiplier 1.25 = 25% more attacks per second.
    public void ScaleFireRate(float multiplier)
    {
        if (multiplier > 0f)
        {
            attackInterval = Mathf.Max(0.01f, attackInterval / multiplier);
        }
    }

    public bool CanTarget(TargetCategory category)
    {
        return targets.Contains(category);
    }

    public float DamageFor(TargetCategory category)
    {
        if (!targets.Contains(category))
        {
            return 0f;
        }

        switch (category)
        {
            case TargetCategory.Enemy:
                return damage;
            case TargetCategory.Resource:
                return ResourceDamageFor(damage);
            default:
                return 0f;
        }
    }
}
