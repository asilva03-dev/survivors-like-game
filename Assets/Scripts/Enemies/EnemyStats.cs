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
 *   Against the base (2026-11-20, user): when a BaseDive exists in the scene, an enemy reaching the base's edge dives
 *   into the box instead, dealing baseHitDamage once, and is gone (see BaseDive); contact damage then only hurts the
 *   player.
 * - Enemy types that attack add their own fields in a subclass (see RangedEnemyStats).
 * - focus (TD slice 4): BaseFocused enemies follow the light rule (see Enemy.RefreshTarget) and only turn on the
 *   player within playerAggroRadius; Hunter enemies only ever target the player (never the base) and keep out of
 *   the base light (ranged rats). While the player is in the light a hunter waits at the edge for hunterLingerTime,
 *   then walks back into the dark and despawns off-screen (see Enemy).
 */

using UnityEngine;

public enum EnemyFocus
{
    BaseFocused,
    Hunter
}

[CreateAssetMenu(menuName = "Enemies/Enemy Stats", fileName = "NewEnemyStats")]
public class EnemyStats : ScriptableObject
{
    [SerializeField, Min(1f)] private float maxHealth = 30f;
    [SerializeField, Min(0f)] private float moveSpeed = 2f;

    [Tooltip("BaseFocused: follows the light rule. Hunter: only targets the player, never enters the base light.")]
    [SerializeField] private EnemyFocus focus = EnemyFocus.BaseFocused;
    [Tooltip("A base-bound enemy turns on the player when the player is this close (flat distance between centres).")]
    [SerializeField, Min(0f)] private float playerAggroRadius = 2f;
    [Tooltip("Hunters only: seconds they wait at the light's edge after the player steps into the light, before walking back into the dark and despawning off-screen.")]
    [SerializeField, Min(0f)] private float hunterLingerTime = 2f;

    [SerializeField, Min(0f)] private float contactDamage = 5f;
    [SerializeField, Min(0.1f)] private float contactRange = 1.4f;
    [SerializeField, Min(0.05f)] private float contactInterval = 1f;
    [Tooltip("Damage dealt to the base when this enemy dives into the box (once; the enemy is gone after). Placeholder.")]
    [SerializeField, Min(0f)] private float baseHitDamage = 5f;

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public EnemyFocus Focus => focus;
    public bool IsHunter => focus == EnemyFocus.Hunter;
    public float PlayerAggroRadius => playerAggroRadius;
    public float HunterLingerTime => hunterLingerTime;
    public float ContactDamage => contactDamage;
    public float ContactRange => contactRange;
    public float ContactInterval => contactInterval;
    public float BaseHitDamage => baseHitDamage;
}
