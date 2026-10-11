/*
 * WeaponStats
 * Base ScriptableObject holding the stats every weapon type shares. Each weapon type adds its own
 * subclass (AimedWeaponStats -> GunStats, AreaStats, later ThrowStats, SpawnStats, ...).
 *
 * - One asset per weapon (e.g. "Basic Gun", "Heavy Gun"); weapon components reference an asset
 *   instead of holding their own numbers, so adding a weapon variant is just a new asset.
 * - Range is the weapon's reach from its owner. Each subclass defines the serialized value (a gun's
 *   range, an AOE's radius) so inspector labels match what the number means.
 * - attacksPerSecond is the generic attack rate; for interval-based weapons use 1 / interval.
 * - Edits to an asset apply live to every weapon using it, including during Play mode.
 */

using UnityEngine;

public abstract class WeaponStats : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float attacksPerSecond = 2.5f;
    [SerializeField] private float damage = 10f;

    public abstract float Range { get; }
    public float AttacksPerSecond => attacksPerSecond;
    public float Damage => damage;
}
