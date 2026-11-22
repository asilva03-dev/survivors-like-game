/*
 * ShadowSight
 * Who can target a shadow rat (2026-11-21, user's rule): towers and auto-aim weapons need the "shadow" property
 * (AimedWeaponStats.detectsShadow), unless the rat stands inside an external light source (a RevealLight). The base
 * light is too weak to count. Non-shadow targets (and anything that isn't an Enemy) are always visible.
 *
 * - Only targeting asks this (WeaponLoadout, AimedWeapon / Damageable.FindNearest, TurretGun). Hits don't: a bullet,
 *   puddle, burst or axe swing that touches a shadow rat still hurts it.
 * - How the player detects shadow rats is decided later (user); for now the player's auto weapons follow this rule.
 */

public static class ShadowSight
{
    public static bool CanTarget(Damageable target, bool detectsShadow)
    {
        if (detectsShadow)
        {
            return true;
        }
        Enemy enemy = target as Enemy;
        if (enemy == null || !enemy.IsShadow)
        {
            return true;
        }
        return RevealLight.Reveals(enemy.transform.position);
    }
}
