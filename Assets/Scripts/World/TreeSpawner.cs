/*
 * TreeSpawner
 * Scatters pooled trees around the spawner, replaces dead trees with stumps and regrows them later.
 *
 * - SpawnAll() picks up to treeCount random spots inside areaRadius (at least minSpacing apart and
 *   outside clearRadius of the spawner) and places a tree on each. ClearAll() returns every tree and
 *   stump to its pool. Resource rounds should call these; spawnOnStart covers testing until the round
 *   state machine exists.
 * - Trees and stumps come from ComponentPools built from the prefabs (prewarmed), never
 *   Instantiate/Destroy at runtime.
 * - When a tree dies (Damageable.Died) it is released and a stump takes its spot. Stumps persist until
 *   the next SpawnAll() / ClearAll(), i.e. the next resource round; trees never regrow on their own.
 * - Each tree gets a random yaw and a scale within scaleRange of its prefab scale; its stump copies it.
 */

using System.Collections.Generic;
using UnityEngine;

public class TreeSpawner : MonoBehaviour
{
    private const int PlacementAttempts = 30;

    [SerializeField] private ResourceTree treePrefab;
    [SerializeField] private Transform stumpPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField, Min(1)] private int treeCount = 25;
    [SerializeField, Min(1f)] private float areaRadius = 40f;
    [SerializeField, Min(0f)] private float clearRadius = 8f;
    [SerializeField, Min(0f)] private float minSpacing = 5f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.15f);

    private readonly List<ResourceTree> liveTrees = new List<ResourceTree>();
    private readonly List<Transform> stumps = new List<Transform>();
    private readonly List<Vector3> spots = new List<Vector3>();

    private ComponentPool<ResourceTree> treePool;
    private ComponentPool<Transform> stumpPool;

    private void Awake()
    {
        Transform root = new GameObject("Trees").transform;
        treePool = new ComponentPool<ResourceTree>(treePrefab, root, treeCount);
        stumpPool = new ComponentPool<Transform>(stumpPrefab, root, treeCount);
    }

    private void OnEnable() => Damageable.Died += HandleDied;

    private void OnDisable() => Damageable.Died -= HandleDied;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnAll();
        }
    }

    public void SpawnAll()
    {
        ClearAll();
        PickSpots();

        for (int i = 0; i < spots.Count; i++)
        {
            PlaceTree(spots[i]);
        }
    }

    public void ClearAll()
    {
        for (int i = 0; i < liveTrees.Count; i++)
        {
            treePool.Release(liveTrees[i]);
        }

        for (int i = 0; i < stumps.Count; i++)
        {
            stumpPool.Release(stumps[i]);
        }

        liveTrees.Clear();
        stumps.Clear();
    }

    private void PickSpots()
    {
        spots.Clear();
        Vector3 center = transform.position;
        float sqrSpacing = minSpacing * minSpacing;

        for (int i = 0; i < treeCount; i++)
        {
            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * areaRadius;
                if (offset.magnitude < clearRadius)
                {
                    continue;
                }

                Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
                if (IsFarFromSpots(point, sqrSpacing))
                {
                    spots.Add(point);
                    break;
                }
            }
        }
    }

    private bool IsFarFromSpots(Vector3 point, float sqrSpacing)
    {
        for (int i = 0; i < spots.Count; i++)
        {
            if ((spots[i] - point).sqrMagnitude < sqrSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private void PlaceTree(Vector3 position)
    {
        Quaternion yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        ResourceTree tree = treePool.Get(position, yaw);
        tree.transform.localScale = treePrefab.transform.localScale * Random.Range(scaleRange.x, scaleRange.y);
        liveTrees.Add(tree);
    }

    private void HandleDied(Damageable target)
    {
        if (!(target is ResourceTree tree) || !liveTrees.Remove(tree))
        {
            return;
        }

        Vector3 position = tree.transform.position;
        Quaternion rotation = tree.transform.rotation;
        Vector3 scale = tree.transform.localScale;
        treePool.Release(tree);

        Transform body = stumpPool.Get(position, rotation);
        body.localScale = stumpPrefab.localScale * (scale.x / treePrefab.transform.localScale.x);

        stumps.Add(body);
    }
}
