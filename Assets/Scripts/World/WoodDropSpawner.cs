/*
 * WoodDropSpawner
 * Bursts collectable wood out of a tree when it dies.
 *
 * - Listens to Damageable.Damaged and Died; for a ResourceTree it launches pooled WoodDrops (one wood each) from
 *   spawnHeight above the tree's base to random landing spots between minScatter and maxScatter units away: on every
 *   hit ResourceTree.TakeHitWood(damage) of them, when it dies TakeRemainingWood (the rest of WoodYield) (2026-11-20).
 * - Wood is no longer awarded on tree death; ResourceInventory adds it when a drop is collected.
 * - Drops come from a ComponentPool<WoodDrop> (prewarmed, grows when empty), never Instantiate/Destroy.
 */

using UnityEngine;

public class WoodDropSpawner : MonoBehaviour
{
    [SerializeField] private WoodDrop dropPrefab;
    [SerializeField, Min(0)] private int prewarmCount = 30;
    [SerializeField, Min(0f)] private float spawnHeight = 1.5f;
    [SerializeField, Min(0f)] private float minScatter = 1.5f;
    [SerializeField, Min(0f)] private float maxScatter = 3.5f;

    private ComponentPool<WoodDrop> pool;

    private void Awake()
    {
        Transform root = new GameObject("WoodDrops").transform;
        pool = new ComponentPool<WoodDrop>(dropPrefab, root, prewarmCount);
    }

    private void OnEnable()
    {
        Damageable.Damaged += HandleDamaged;
        Damageable.Died += HandleDied;
    }

    private void OnDisable()
    {
        Damageable.Damaged -= HandleDamaged;
        Damageable.Died -= HandleDied;
    }

    // Every hit drops part of the wood (by its damage); Damaged fires before Died, so a killing hit drops its share first.
    private void HandleDamaged(Damageable target, float amount)
    {
        if (target is ResourceTree tree)
        {
            Launch(tree, tree.TakeHitWood(amount));
        }
    }

    private void HandleDied(Damageable target)
    {
        if (target is ResourceTree tree)
        {
            Launch(tree, tree.TakeRemainingWood());
        }
    }

    private void Launch(ResourceTree tree, int count)
    {
        Vector3 basePosition = tree.transform.position;
        Vector3 from = basePosition + Vector3.up * spawnHeight;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minScatter, maxScatter);
            Vector3 land = basePosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            land.y = dropPrefab.HoverHeight;

            WoodDrop drop = pool.Get(from, Quaternion.identity);
            drop.Launch(pool, from, land, 1);
        }
    }
}
