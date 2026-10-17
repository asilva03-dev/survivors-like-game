/*
 * ThrowEWeapon
 * Throw weapon (bomb): where a throwable lands it explodes in a particle burst and deals damage once to
 * everything inside explosionRadius. Nothing lingers.
 *
 * - Targeting, throwing, prediction and the multi-projectile pattern are all ThrowWeapon; this class only
 *   adds the landing damage (an instant radius hit instead of a LingerPuddle).
 */

using UnityEngine;

public class ThrowEWeapon : ThrowWeapon<ThrowEStats>
{
    protected override void Land(Vector3 point)
    {
        PlayBurst(point);

        float sqrRadius = Stats.ExplosionRadius * Stats.ExplosionRadius;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!Stats.CanTarget(candidate.Category))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - point;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrRadius)
            {
                continue;
            }

            float amount = Stats.DamageFor(candidate.Category);
            if (amount > 0f)
            {
                candidate.TakeDamage(amount);
            }
        }
    }
}
