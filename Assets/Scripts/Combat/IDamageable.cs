/*
 * IDamageable
 * Anything that can take damage from weapons and projectiles (enemies, destructibles, ...).
 * Projectiles look this up with GetComponentInParent on whatever collider they hit.
 */

public interface IDamageable
{
    void TakeDamage(float amount);
}
