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
 * - Target: every frame the enemy picks the closest of the player and the bases (BaseStructure.Active), measured
 *   to their edge (Damageable.TargetRadius), ties going to the player. Movement, contact damage and ranged fire
 *   all use it. ForceBaseTarget (debug, temporary) skips the player so every enemy goes for the base.
 * - Contact damage: an enemy within its stats' contactRange of its target's edge hurts it, at most once per
 *   contactInterval (see EnemyStats). Subclasses that override Update must call base.Update().
 * - Knockback(direction, distance, duration) pushes the enemy away with an ease-out; while it lasts
 *   EnemyMovementSystem pauses seeking and applies TakeKnockbackStep each frame.
 */

using System.Collections.Generic;
using UnityEngine;

public class Enemy : Damageable
{
    public static readonly List<Enemy> Active = new List<Enemy>();

    // Debug (temporary, set by EnemyMovementSystem.forceBaseTarget): every enemy targets the base.
    public static bool ForceBaseTarget { get; set; }

    [SerializeField] private Transform visual;
    [SerializeField] private EnemyStats stats;

    private Vector2 knockDirection;
    private float knockDistance;
    private float knockDuration;
    private float knockRemaining;
    private float contactCooldown;

    // What this enemy is going for: the player or a base, whichever edge is closer (refreshed every frame).
    public Damageable Target { get; private set; }
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
        contactCooldown = 0f;
        RefreshTarget();
    }

    protected virtual void Update()
    {
        RefreshTarget();
        TryContactDamage();
    }

    // Closest of the player and every base, measured to their edge (TargetRadius); a tie goes to the player.
    // With ForceBaseTarget on (debug) the player is skipped whenever a base exists.
    public void RefreshTarget()
    {
        Vector3 position = transform.position;
        List<BaseStructure> bases = BaseStructure.Active;
        bool skipPlayer = ForceBaseTarget && bases.Count > 0;
        Damageable best = skipPlayer ? null : PlayerHealth.Instance;
        float bestDistance = best != null ? EdgeDistance(position, best) : float.MaxValue;

        for (int i = 0; i < bases.Count; i++)
        {
            float distance = EdgeDistance(position, bases[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = bases[i];
            }
        }

        Target = best;
    }

    public static float EdgeDistance(Vector3 from, Damageable target)
    {
        Vector3 offset = target.transform.position - from;
        offset.y = 0f;
        return offset.magnitude - target.TargetRadius;
    }

    // Hurts the current target while within the stats' contactRange of its edge, at most once per contactInterval.
    private void TryContactDamage()
    {
        contactCooldown -= Time.deltaTime;
        if (stats == null || Target == null || stats.ContactDamage <= 0f || contactCooldown > 0f
            || Target.IsInvulnerable)
        {
            return;
        }

        if (EdgeDistance(transform.position, Target) <= stats.ContactRange)
        {
            Target.TakeDamage(stats.ContactDamage);
            contactCooldown = stats.ContactInterval;
        }
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
