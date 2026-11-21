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
 * - Layers (2026-11-20, user: BTD-style "1 hit = 1 layer"): when `layers` is not empty the enemy's health is a
 *   stack of layerHealth-sized layers instead of maxHealth (which is then unused). layers[0] is the bare enemy, each
 *   next entry an armor layer on top (rat: Bare, Helmet, Plate). The spawner picks how many armor layers each spawn
 *   gets (Enemy.SetExtraLayers); damage strips layers from the top and carries through them (20 damage = 2 layers),
 *   and the enemy dies when the bare layer is emptied. Empty list = plain health (ranged rats).
 * - Spill on death (2026-11-20, bag rat): spillCount basic rats with spillArmorLayers armor pop out where it died
 *   (EnemySpawner), pushed out by spillSpread, each taking the killing hit's leftover damage (spillCarriesOverkill),
 *   with a chip burst in spillBurstColor. 0 = nothing spills.
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

    [Tooltip("Health layers, bare enemy first, then each armor layer on top (e.g. Bare, Helmet, Plate). Empty = plain Max Health.")]
    [SerializeField] private string[] layers = new string[0];
    [Tooltip("Health of each layer (used only when Layers is not empty).")]
    [SerializeField, Min(1f)] private float layerHealth = 10f;

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

    [Header("Spill on death (bag rat)")]
    [Tooltip("Basic rats that spill out when this enemy dies (0 = none).")]
    [SerializeField, Min(0)] private int spillCount;
    [Tooltip("Armor layers on each spilled rat (0 = bare, 1 = helmet, 2 = plate).")]
    [SerializeField, Min(0)] private int spillArmorLayers;
    [Tooltip("How far the spilled rats are pushed out from where it died.")]
    [SerializeField, Min(0f)] private float spillSpread = 1.5f;
    [Tooltip("The killing hit's leftover damage is dealt to every spilled rat.")]
    [SerializeField] private bool spillCarriesOverkill = true;
    [Tooltip("Colour of the chip burst when it bursts open.")]
    [SerializeField] private Color spillBurstColor = new Color(0.66f, 0.6f, 0.47f);

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public bool HasLayers => layers != null && layers.Length > 0;
    public int LayerCount => layers != null ? layers.Length : 0;
    public float LayerHealth => layerHealth;
    public string LayerName(int index) => HasLayers ? layers[Mathf.Clamp(index, 0, layers.Length - 1)] : "";
    public EnemyFocus Focus => focus;
    public bool IsHunter => focus == EnemyFocus.Hunter;
    public float PlayerAggroRadius => playerAggroRadius;
    public float HunterLingerTime => hunterLingerTime;
    public float ContactDamage => contactDamage;
    public float ContactRange => contactRange;
    public float ContactInterval => contactInterval;
    public float BaseHitDamage => baseHitDamage;
    public int SpillCount => spillCount;
    public int SpillArmorLayers => spillArmorLayers;
    public float SpillSpread => spillSpread;
    public bool SpillCarriesOverkill => spillCarriesOverkill;
    public Color SpillBurstColor => spillBurstColor;
}
