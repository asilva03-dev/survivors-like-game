/*
 * DamageNumberSpawner
 * Spawns a pooled floating damage number whenever an enemy takes damage.
 *
 * - Put it on a world-space Canvas at the origin with scale 1; numbers are children of it and are
 *   positioned directly in world space.
 * - Listens to Damageable.Damaged, so enemies, trees and anything else damageable all show numbers.
 *   Each number appears at the target's position plus heightOffset, at a
 *   random point within spawnRadius, so rapid hits do not stack on one spot.
 * - Numbers come from a ComponentPool<DamageNumber> built from numberPrefab (prewarmed); the pool
 *   grows if many hits land at once.
 * - All look, timing, motion and pop values live in the serialized style and apply to every number.
 * - Damage is shown rounded to a whole number, at least 1.
 */

using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private DamageNumber numberPrefab;
    [SerializeField, Min(0)] private int prewarmCount = 40;
    [SerializeField] private DamageNumberStyle style = new DamageNumberStyle();

    private ComponentPool<DamageNumber> pool;

    private void Awake()
    {
        pool = new ComponentPool<DamageNumber>(numberPrefab, transform, prewarmCount);
    }

    private void OnEnable() => Damageable.Damaged += HandleDamaged;

    private void OnDisable() => Damageable.Damaged -= HandleDamaged;

    private void HandleDamaged(Damageable target, float amount)
    {
        Vector2 offset = Random.insideUnitCircle * style.spawnRadius;
        Vector3 position = target.transform.position + new Vector3(offset.x, style.heightOffset, offset.y);

        DamageNumber number = pool.Get(position, Quaternion.identity);
        number.Show(pool, style, position, Mathf.Max(1, Mathf.RoundToInt(amount)).ToString());
    }
}
