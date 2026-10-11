/*
 * Enemy
 * Placeholder enemy: a Damageable in the Enemy category that is also tracked in its own list.
 *
 * - Health, rank, category, damage events and death handling all come from Damageable.
 * - Active holds every enabled enemy (typed), for systems that only care about enemies: movement,
 *   spawning and the debug kill key.
 * - Enemy movement lives in EnemyMovementSystem and spawning / pooling in EnemySpawner.
 * - Serialized fields (maxHealth, rank) are inherited, so existing enemy prefabs keep their values.
 */

using System.Collections.Generic;

public class Enemy : Damageable
{
    public static readonly List<Enemy> Active = new List<Enemy>();

    protected override void OnEnable()
    {
        base.OnEnable();
        Active.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Active.Remove(this);
    }
}
