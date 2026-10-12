/*
 * Damageable / TargetCategory / TargetMask / TargetRank
 * Shared health and targeting component for anything weapons can hit: enemies today, trees, stone,
 * the base, walls and the player later. Enemy derives from this.
 *
 * - category says what kind of thing this is. Weapon stats pick which categories they target and how
 *   much damage they deal to each (see WeaponStats.DamageFor).
 * - rank (Normal / Elite / Boss) feeds target priority in WeaponLoadout.
 * - damageNumberColor is the text color of this entity's floating damage numbers (set per prefab).
 * - AimPoint is where projectiles are aimed: the transform position plus a world-space aimOffset.
 *   Things whose pivot is at their base (trees, walls) set aimOffset up to gun height so bullets fly
 *   level instead of diving into the ground before reaching them.
 * - All holds every enabled Damageable so systems never need scene searches. Health resets to full
 *   in OnEnable, so pooled objects come back healthy.
 * - Damaged (target, amount) fires on every hit, before death is handled, so killing blows are
 *   reported. Died fires once when health reaches 0; after that the object disables itself (pooled
 *   owners may release it back to their pool from the Died handler).
 * - Subclasses can override OnEnable / OnDisable (call base) and OnDied for their own bookkeeping.
 * - Damage numbers and health bars hook into this, so every damageable thing gets them for free.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public enum TargetCategory { Enemy = 0, Resource = 1, Structure = 2, Player = 3 }

[Flags]
public enum TargetMask
{
    None = 0,
    Enemy = 1 << 0,
    Resource = 1 << 1,
    Structure = 1 << 2,
    Player = 1 << 3
}

public enum TargetRank { Normal, Elite, Boss }

public static class TargetMaskExtensions
{
    public static bool Contains(this TargetMask mask, TargetCategory category)
    {
        return (mask & (TargetMask)(1 << (int)category)) != 0;
    }
}

public class Damageable : MonoBehaviour, IDamageable
{
    public static readonly List<Damageable> All = new List<Damageable>();
    public static event Action<Damageable, float> Damaged;
    public static event Action<Damageable> Died;

    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private TargetCategory category = TargetCategory.Enemy;
    [SerializeField] private TargetRank rank = TargetRank.Normal;
    [SerializeField] private Vector3 aimOffset;
    [SerializeField] private Color damageNumberColor = Color.white;

    private float health;

    public Vector3 AimPoint => transform.position + aimOffset;
    public Color DamageNumberColor => damageNumberColor;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
    public TargetCategory Category => category;
    public TargetRank Rank => rank;

    protected virtual void OnEnable()
    {
        health = maxHealth;
        All.Add(this);
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        Damaged?.Invoke(this, amount);

        if (health > 0f)
        {
            return;
        }

        Died?.Invoke(this);
        OnDied();
        gameObject.SetActive(false);
    }

    protected virtual void OnDied()
    {
    }

    public static Damageable FindNearest(Vector3 from, float range, TargetMask mask)
    {
        Damageable nearest = null;
        float best = range * range;

        for (int i = 0; i < All.Count; i++)
        {
            Damageable candidate = All[i];
            if (!mask.Contains(candidate.category))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - from;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
