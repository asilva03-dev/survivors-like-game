/*
 * ThrowLStats
 * Stats asset for a lobbed throwable that leaves a lingering damage puddle (molotov-style).
 * Create via Assets > Create > Weapons > ThrowL Stats.
 *
 * - The throw itself (arc, flight time, prediction, projectile count, spread, landing bloom, model,
 *   explosion color, pool size) comes from ThrowStats; see there. Aiming comes from AimedWeaponStats;
 *   attackInterval, damage ("Enemy Damage"), resourceDamageMultiplier and targets from WeaponStats.
 * - radius is the size of the explosion circle / puddle on the ground; lifetime is how long (seconds)
 *   the puddle stays; lingerDamageInterval is the seconds between damage ticks while it is there
 *   (the first tick lands with the explosion). Each tick deals the weapon's damage to every target
 *   inside (enemies take damage, resources take damage x resourceDamageMultiplier rounded up).
 * - lingerColor tints the puddle (alpha = opacity).
 * - Edits apply live to puddles in Play mode; lifetime applies to puddles created afterwards.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/ThrowL Stats", fileName = "ThrowL")]
public class ThrowLStats : ThrowStats
{
    [SerializeField, Min(0.1f)] private float radius = 2.5f;
    [SerializeField, Min(0.1f)] private float lifetime = 4f;
    [SerializeField, Min(0.05f)] private float lingerDamageInterval = 0.5f;
    [SerializeField] private Color lingerColor = new Color(1f, 0.4f, 0.05f, 0.6f);

    public float Radius => radius;
    public float Lifetime => lifetime;
    public float LingerDamageInterval => lingerDamageInterval;
    public Color LingerColor => lingerColor;
}
