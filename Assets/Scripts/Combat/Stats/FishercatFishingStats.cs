/*
 * FishercatFishingStats
 * Gameplay numbers of the Fishercat's fishing (Path 3 "water", 2026-11-26; Create > TD > Fishercat Fishing Stats).
 * FishercatFishing uses them; Turret gives the tower its own copy with the bought tiers applied (TurretUpgrade fishing
 * fields), so the asset is never changed in play.
 *
 * - unlocked: off on the asset; tier 1 turns it on (a pool of water where the line lands on the path).
 * - poolRadius: the pool's size. slow: enemies in the pool move at this share of their speed (Enemy.SlowInZone).
 * - reveals (tier 2): the pool is a RevealLight: shadow rats in it can be targeted by every tower.
 * - sharkDamage > 0 (tier 3): when at least sharkMinEnemies enemies are in the pool and the cooldown is up, a shark comes
 *   out and bites every enemy in the pool for sharkDamage. bigShark (tier 5): the great white instead. sharkCooldown:
 *   seconds between sharks.
 * - range: 0 = the gun's range (where the line can land).
 * - Numbers are placeholders (user's finalizing pass).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Fishercat Fishing Stats", fileName = "FishercatFishingStats")]
public class FishercatFishingStats : ScriptableObject
{
    [SerializeField] private bool unlocked;
    [SerializeField, Min(0.2f)] private float poolRadius = 1.2f;
    [Tooltip("Enemies in the pool move at this share of their speed (0.6 = 40% slower).")]
    [SerializeField, Range(0.05f, 1f)] private float slow = 0.6f;
    [SerializeField] private bool reveals;
    [Tooltip("0 = no shark.")]
    [SerializeField, Min(0f)] private float sharkDamage;
    [SerializeField, Min(1)] private int sharkMinEnemies = 3;
    [SerializeField, Min(0.5f)] private float sharkCooldown = 6f;
    [SerializeField] private bool bigShark;
    [Tooltip("0 = the gun's range.")]
    [SerializeField, Min(0f)] private float range;

    public bool Unlocked => unlocked;
    public float PoolRadius => poolRadius;
    public float Slow => slow;
    public bool Reveals => reveals;
    public float SharkDamage => sharkDamage;
    public int SharkMinEnemies => sharkMinEnemies;
    public float SharkCooldown => sharkCooldown;
    public bool BigShark => bigShark;
    public float Range => range;

    // Upgrade copies only (TurretUpgrade.ApplyTo): each value only gets better.
    public void Apply(bool unlock, float radius, float newSlow, bool reveal, float damage, float cooldown, bool big)
    {
        unlocked |= unlock;
        poolRadius = Mathf.Max(poolRadius, radius);
        if (newSlow > 0f)
        {
            slow = Mathf.Min(slow, newSlow);
        }
        reveals |= reveal;
        sharkDamage = Mathf.Max(sharkDamage, damage);
        if (cooldown > 0f)
        {
            sharkCooldown = Mathf.Min(sharkCooldown, cooldown);
        }
        bigShark |= big;
    }
}
