/*
 * Enemy
 * Placeholder enemy: a Damageable in the Enemy category that is also tracked in its own list.
 *
 * - Health, rank, category, damage events and death handling all come from Damageable.
 * - Active holds every enabled enemy (typed), for systems that only care about enemies: movement,
 *   spawning and the debug kill key.
 * - Enemy movement lives in EnemyMovementSystem and spawning / pooling in EnemySpawner.
 * - Serialized fields (maxHealth, rank) are inherited, so existing enemy prefabs keep their values.
 * - An optional EnemyStats asset gives the enemy type its health and move speed (applied in Awake, so it
 *   overrides the prefab's Max Health); without one the prefab health and EnemySettings speed apply.
 *   WantsToMove lets a subclass (ranged enemies) stand still; EnemyMovementSystem only seeks while it is true.
 * - Visual is the child that holds the model; EnemyMovementSystem turns it to face the player (the root, with
 *   the collider and health bar, never rotates).
 * - Knockback(direction, distance, duration) pushes the enemy away with an ease-out; while it lasts
 *   EnemyMovementSystem pauses seeking and applies TakeKnockbackStep each frame.
 */

using System.Collections.Generic;
using UnityEngine;

public class Enemy : Damageable
{
    public static readonly List<Enemy> Active = new List<Enemy>();

    [SerializeField] private Transform visual;
    [SerializeField] private EnemyStats stats;

    private Vector2 knockDirection;
    private float knockDistance;
    private float knockDuration;
    private float knockRemaining;

    public bool IsKnockedBack => knockRemaining > 0f;
    public Transform Visual => visual;
    public EnemyStats Stats => stats;
    public virtual bool WantsToMove => true;

    protected virtual void Awake()
    {
        if (stats != null)
        {
            SetMaxHealth(stats.MaxHealth);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Active.Add(this);
        knockRemaining = 0f;
    }

    // Starts a push of `distance` units away along `direction` (XZ), easing out over `duration` seconds.
    public void Knockback(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        knockDirection = direction.normalized;
        knockDistance = distance;
        knockDuration = Mathf.Max(duration, 0.01f);
        knockRemaining = knockDuration;
    }

    // Called once per frame by EnemyMovementSystem; returns this frame's displacement and advances the push.
    public Vector2 TakeKnockbackStep(float deltaTime)
    {
        if (knockRemaining <= 0f)
        {
            return Vector2.zero;
        }

        float before = knockRemaining / knockDuration;
        knockRemaining = Mathf.Max(0f, knockRemaining - deltaTime);
        float after = knockRemaining / knockDuration;

        // quadratic ease-out: the integral of 2u over the remaining fraction sums to the full distance
        return knockDirection * (knockDistance * (before * before - after * after));
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Active.Remove(this);
    }
}
