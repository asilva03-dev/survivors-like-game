/*
 * EnemyStats
 * Per-type stats asset for an enemy (one asset per enemy type, referenced by that type's prefab).
 * Create via Assets > Create > Enemies > Enemy Stats.
 *
 * - maxHealth is applied to the enemy's Damageable health when it is enabled (so it overrides the prefab's
 *   own Max Health), and moveSpeed (units per second toward the player) replaces EnemySettings.MoveSpeed
 *   for that enemy. Spacing, stop distance and turn speed stay shared in EnemySettings.
 * - contactDamage is dealt to the enemy's current target (the player or the base, whichever is closer) while
 *   the enemy is within contactRange of it (flat distance between centres, plus the target's TargetRadius for
 *   big things like the base). Each enemy waits contactInterval seconds after a hit before it can hit again
 *   (the player's invulnerability frames still apply on top); 0 damage disables it. Every Enemy does this, so
 *   new enemy types get it from their stats asset.
 * - Enemy types that attack add their own fields in a subclass (see RangedEnemyStats).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Enemies/Enemy Stats", fileName = "NewEnemyStats")]
public class EnemyStats : ScriptableObject
{
    [SerializeField, Min(1f)] private float maxHealth = 30f;
    [SerializeField, Min(0f)] private float moveSpeed = 2f;

    [SerializeField, Min(0f)] private float contactDamage = 5f;
    [SerializeField, Min(0.1f)] private float contactRange = 1.4f;
    [SerializeField, Min(0.05f)] private float contactInterval = 1f;

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float ContactDamage => contactDamage;
    public float ContactRange => contactRange;
    public float ContactInterval => contactInterval;
}
