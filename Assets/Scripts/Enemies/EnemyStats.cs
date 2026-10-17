/*
 * EnemyStats
 * Per-type stats asset for an enemy (one asset per enemy type, referenced by that type's prefab).
 * Create via Assets > Create > Enemies > Enemy Stats.
 *
 * - maxHealth is applied to the enemy's Damageable health when it is enabled (so it overrides the prefab's
 *   own Max Health), and moveSpeed (units per second toward the player) replaces EnemySettings.MoveSpeed
 *   for that enemy. Spacing, stop distance and turn speed stay shared in EnemySettings.
 * - Enemy types that attack add their own fields in a subclass (see RangedEnemyStats).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Enemies/Enemy Stats", fileName = "NewEnemyStats")]
public class EnemyStats : ScriptableObject
{
    [SerializeField, Min(1f)] private float maxHealth = 30f;
    [SerializeField, Min(0f)] private float moveSpeed = 2f;

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
}
