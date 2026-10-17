/*
 * EnemyDeathEffect
 * Plays a pooled burst of red particles where an enemy dies.
 *
 * - Listens to Damageable.Died; for any Enemy it plays one DebrisBurst (the burst prefab, a red full-sphere
 *   burst tuned on Prefabs/EnemyDeathBurst.prefab) at the centre of the enemy's model (renderers under
 *   Enemy.Visual), falling back to the enemy's position when it has none.
 * - Bursts come from a ComponentPool<DebrisBurst> (prewarmed, grows when empty), never Instantiate/Destroy.
 *   Look, count and colour are tuned on the prefab's ParticleSystem; no tint is applied.
 * - Died fires just before the enemy is deactivated, so its renderers still have valid bounds here.
 */

using UnityEngine;

public class EnemyDeathEffect : MonoBehaviour
{
    [SerializeField] private DebrisBurst burstPrefab;
    [SerializeField, Min(0)] private int prewarmCount = 12;

    private ComponentPool<DebrisBurst> pool;

    private void Awake()
    {
        Transform root = new GameObject("Enemy Death Bursts").transform;
        pool = new ComponentPool<DebrisBurst>(burstPrefab, root, prewarmCount);
    }

    private void OnEnable() => Damageable.Died += HandleDied;

    private void OnDisable() => Damageable.Died -= HandleDied;

    private void HandleDied(Damageable target)
    {
        Enemy enemy = target as Enemy;
        if (enemy == null)
        {
            return;
        }

        Vector3 point = enemy.transform.position;
        if (enemy.Visual != null)
        {
            Renderer renderer = enemy.Visual.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                point = renderer.bounds.center;
            }
        }

        DebrisBurst burst = pool.Get(point, Quaternion.identity);
        burst.Play(pool);
    }
}
