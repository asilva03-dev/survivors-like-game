/*
 * ThrowEStats
 * Stats asset for a lobbed throwable that explodes on landing (bomb-style): it deals damage once to
 * everything within explosionRadius of the landing point, and leaves nothing behind.
 * Create via Assets > Create > Weapons > ThrowE Stats.
 *
 * - The throw itself (arc, flight time, prediction, projectile count, spread, landing bloom, model,
 *   explosion color, pool size) comes from ThrowStats; see there. Aiming comes from AimedWeaponStats;
 *   attackInterval, damage ("Enemy Damage"), resourceDamageMultiplier and targets from WeaponStats.
 * - explosionRadius is the size of the blast on the ground. Each bomb deals the weapon's damage once to every
 *   target inside it (enemies take damage, resources take damage x resourceDamageMultiplier rounded up);
 *   blasts from different bombs stack.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/ThrowE Stats", fileName = "ThrowE")]
public class ThrowEStats : ThrowStats
{
    [SerializeField, Min(0.1f)] private float explosionRadius = 2.5f;

    public float ExplosionRadius => explosionRadius;
}
