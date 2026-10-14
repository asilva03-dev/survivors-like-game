/*
 * WoodDropSpawner
 * Bursts collectable wood out of a tree when it dies.
 *
 * - Listens to Damageable.Died; for a ResourceTree it launches ResourceTree.WoodYield pooled
 *   WoodDrops (one wood each) from spawnHeight above the tree's base to random landing spots
 *   between minScatter and maxScatter units away.
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

    private void OnEnable() => Damageable.Died += HandleDied;

    private void OnDisable() => Damageable.Died -= HandleDied;

    private void HandleDied(Damageable target)
    {
        if (!(target is ResourceTree tree))
        {
            return;
        }

        Vector3 basePosition = tree.transform.position;
        Vector3 from = basePosition + Vector3.up * spawnHeight;

        for (int i = 0; i < tree.WoodYield; i++)
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
