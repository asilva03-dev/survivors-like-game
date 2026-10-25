/*
 * TreeSpawner
 * Scatters pooled trees over the map (a share inside the base light) and replaces dead trees with stumps.
 *
 * - SpawnAll() places treeCount trees over the square map area (areaHalfSize around the spawner): lightShare of them
 *   inside the BaseLight circle (its TargetRadius), the rest outside it in the dark. Every spot is at least minSpacing
 *   from other trees/stumps and on open ground at least layoutClearance from any base layout cell. Light spots that
 *   don't fit (the early light is small) go to the dark instead, so the total stays treeCount.
 * - SpawnRound(count) adds trees for a tree round (roundLightShare in the light) without clearing the others.
 * - The light growing over trees does nothing to them. When the layout grows (BuildGrid Changed), trees and stumps
 *   that are no longer on open ground (same layoutClearance) are removed without a stump or wood.
 * - spawnOnStart spawns once the base chunk is placed (waitForBaseChunk; the run starts with the base draft), so the
 *   light's size is known. ClearAll() returns every tree and stump to its pool. The round state machine should call
 *   SpawnAll/SpawnRound later.
 * - Trees and stumps come from ComponentPools built from the prefabs (prewarmed), never
 *   Instantiate/Destroy at runtime.
 * - When a tree dies (Damageable.Died) it is released and a stump takes its spot. Stumps persist until
 *   the next SpawnAll() / ClearAll(), i.e. the next resource round; trees never regrow on their own.
 * - A dying tree also plays a pooled DebrisBurst (falling green/brown chips) at its base, if
 *   debrisPrefab is assigned.
 * - Each tree gets a random yaw and a scale within scaleRange of its prefab scale; its stump copies it.
 */

using System.Collections.Generic;
using UnityEngine;

public class TreeSpawner : MonoBehaviour
{
    private const int PlacementAttempts = 30;

    [SerializeField] private ResourceTree treePrefab;
    [SerializeField] private Transform stumpPrefab;
    [SerializeField] private DebrisBurst debrisPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [Tooltip("Wait for the base chunk to be placed before the start spawn (the base draft opens on Play).")]
    [SerializeField] private bool waitForBaseChunk = true;
    [SerializeField] private BaseLayout layout;
    [SerializeField, Min(1)] private int treeCount = 25;
    [Tooltip("Trees spawn inside a square of this half size around the spawner (the map).")]
    [SerializeField, Min(1f)] private float areaHalfSize = 95f;
    [Tooltip("Share of SpawnAll's trees placed inside the base light.")]
    [SerializeField, Range(0f, 1f)] private float lightShare = 0.1f;
    [Tooltip("Share of SpawnRound's trees placed inside the base light.")]
    [SerializeField, Range(0f, 1f)] private float roundLightShare = 0.05f;
    [SerializeField, Min(0f)] private float minSpacing = 5f;
    [Tooltip("A tree spot must be open ground (no base layout cells) within this radius, so canopies stay off the walls.")]
    [SerializeField, Min(0f)] private float layoutClearance = 2f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.15f);

    private readonly List<ResourceTree> liveTrees = new List<ResourceTree>();
    private readonly List<Transform> stumps = new List<Transform>();
    private readonly List<Vector3> spots = new List<Vector3>();

    private ComponentPool<ResourceTree> treePool;
    private ComponentPool<Transform> stumpPool;
    private ComponentPool<DebrisBurst> debrisPool;
    private BuildGrid grid;
    private bool spawnPending;

    public int LiveTreeCount => liveTrees.Count;
    public IReadOnlyList<ResourceTree> LiveTrees => liveTrees;

    private void Awake()
    {
        Transform root = new GameObject("Trees").transform;
        treePool = new ComponentPool<ResourceTree>(treePrefab, root, treeCount);
        stumpPool = new ComponentPool<Transform>(stumpPrefab, root, treeCount);

        if (debrisPrefab != null)
        {
            debrisPool = new ComponentPool<DebrisBurst>(debrisPrefab, root, 4);
        }
    }

    private void OnEnable() => Damageable.Died += HandleDied;

    private void OnDisable() => Damageable.Died -= HandleDied;

    private void Start()
    {
        grid = BuildGrid.Instance;
        if (grid != null)
        {
            grid.Changed += RemoveCoveredTrees;
        }
        if (layout == null)
        {
            layout = FindAnyObjectByType<BaseLayout>();
        }
        spawnPending = spawnOnStart;
    }

    private void Update()
    {
        if (spawnPending && (!waitForBaseChunk || layout == null || layout.HasBaseChunk))
        {
            spawnPending = false;
            SpawnAll();
        }
    }

    private void OnDestroy()
    {
        if (grid != null)
        {
            grid.Changed -= RemoveCoveredTrees;
        }
    }

    // The base layout grew: trees and stumps under (or too close to) the new chunks go away.
    private void RemoveCoveredTrees()
    {
        for (int i = liveTrees.Count - 1; i >= 0; i--)
        {
            if (!grid.IsOpenGround(liveTrees[i].transform.position, layoutClearance))
            {
                treePool.Release(liveTrees[i]);
                liveTrees.RemoveAt(i);
            }
        }
        for (int i = stumps.Count - 1; i >= 0; i--)
        {
            if (!grid.IsOpenGround(stumps[i].position, layoutClearance))
            {
                stumpPool.Release(stumps[i]);
                stumps.RemoveAt(i);
            }
        }
    }

    public void SpawnAll()
    {
        ClearAll();
        Spawn(treeCount, lightShare);
    }

    /// <summary>Adds count trees (a tree round), roundLightShare of them in the light; existing trees stay.</summary>
    public void SpawnRound(int count)
    {
        Spawn(count, roundLightShare);
    }

    private void Spawn(int count, float share)
    {
        spots.Clear();
        for (int i = 0; i < liveTrees.Count; i++)
        {
            spots.Add(liveTrees[i].transform.position);
        }
        for (int i = 0; i < stumps.Count; i++)
        {
            spots.Add(stumps[i].position);
        }
        int first = spots.Count;

        BaseLight baseLight = BaseLight.Instance;
        float lightRadius = baseLight != null ? baseLight.TargetRadius : 0f;
        int lightCount = baseLight != null ? Mathf.RoundToInt(count * share) : 0;
        int placedLight = 0;
        for (int i = 0; i < lightCount; i++)
        {
            if (TryPickSpot(true, lightRadius, baseLight))
            {
                placedLight++;
            }
        }
        int darkCount = count - placedLight;
        for (int i = 0; i < darkCount; i++)
        {
            TryPickSpot(false, lightRadius, baseLight);
        }

        for (int i = first; i < spots.Count; i++)
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

    // One random spot inside (inLight) or outside the light circle, added to spots; false when none fit.
    private bool TryPickSpot(bool inLight, float lightRadius, BaseLight baseLight)
    {
        Vector3 center = transform.position;
        float sqrSpacing = minSpacing * minSpacing;

        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            Vector3 point;
            if (inLight)
            {
                Vector2 offset = Random.insideUnitCircle * lightRadius;
                point = baseLight.Center + new Vector3(offset.x, 0f, offset.y);
            }
            else
            {
                point = center + new Vector3(Random.Range(-areaHalfSize, areaHalfSize), 0f, Random.Range(-areaHalfSize, areaHalfSize));
                if (baseLight != null && baseLight.FlatDistance(point) <= lightRadius)
                {
                    continue;
                }
            }

            if (IsFarFromSpots(point, sqrSpacing) && BuildGrid.IsOpenGroundAt(point, layoutClearance))
            {
                spots.Add(point);
                return true;
            }
        }
        return false;
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

        if (debrisPool != null)
        {
            debrisPool.Get(position, Quaternion.identity).Play(debrisPool);
        }
    }
}
