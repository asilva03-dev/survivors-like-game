/*
 * TreeSpawner
 * Grows the forest around the base (a new layout every run) and replaces dead trees with stumps.
 *
 * - SpawnAll() (2026-11-10, user's sketch) clears the trees and grows a new forest around the base:
 *   - Max reach R = the farthest corner of the largest base (BaseLayout.maxSlotReach chunk rings) + the BaseLight's
 *     margin and extraRadius (= the full-base light) + ringStartOffset.
 *   - Thick ring: full density from R to R + thickWidth, then thinning out to nothing over fadeWidth.
 *   - Wedges: wedgeCountRange thick patches that join the ring and reach in toward the base, at random angles, widths
 *     and depths, with ragged edges; never overlapping, and never closer to the base than leewayRings chunk rings.
 *   - Sparse trees: sparsePer100 trees per 100 m2 scattered over the rest of the ground inside R, at least minSpacing
 *     from any tree.
 *   - Thick areas are filled at thickSpacing (density 1); where the ring thins, the spacing grows (thickSpacing /
 *     sqrt(density)) until density drops below minFadeDensity. seed 0 = a new layout every run; any other number
 *     repeats the same layout (for testing).
 * - Every spot is open ground at least layoutClearance from any base layout cell. With a MapGround, trees stand on the
 *   ground's height, never on ramps or within a unit of a cliff edge.
 * - SpawnRound(count) adds trees for a tree round (roundLightShare in the light) without clearing the others (unused;
 *   tree respawning is pinned).
 * - CountTreesIn(min, max): live trees a chunk on that rectangle would cover (ChunkPlacer refuses a spot with any,
 *   user 2026-11-10: trees must be chopped first).
 * - The light growing over trees does nothing to them. When the layout grows (BuildGrid Changed), trees and stumps
 *   that are no longer on open ground (same layoutClearance) are removed without a stump or wood.
 * - spawnOnStart spawns once the base chunk is placed (waitForBaseChunk; the run starts with the base draft).
 *   ClearAll() returns every tree and stump to its pool. Context menu "Respawn trees" rolls a new layout in Play.
 * - Performance (2026-11-10): every listInterval only trees within listRadius of the player stay in Damageable.All
 *   (Damageable.Listed); the rest keep their health and collider but the weapons' per-frame loops skip them.
 * - Trees and stumps come from ComponentPools built from the prefabs (prewarmed), never
 *   Instantiate/Destroy at runtime.
 * - When a tree dies (Damageable.Died) it is released and a stump takes its spot. Stumps persist until
 *   the next SpawnAll() / ClearAll(); trees never regrow on their own.
 * - A dying tree also plays a pooled DebrisBurst (falling green/brown chips) at its base, if
 *   debrisPrefab is assigned.
 * - Each tree gets a random yaw and a scale within scaleRange of its prefab scale; its stump copies it.
 */

using System.Collections.Generic;
using UnityEngine;

public class TreeSpawner : MonoBehaviour
{
    private const int PlacementAttempts = 30;
    private const int WedgeAttempts = 20;

    private struct Wedge
    {
        public float angle;
        public float baseHalfWidth;
        public float tipHalfWidth;
        public float tipRadius;
        public float noiseOffset;
    }

    [SerializeField] private ResourceTree treePrefab;
    [SerializeField] private Transform stumpPrefab;
    [SerializeField] private DebrisBurst debrisPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [Tooltip("Wait for the base chunk to be placed before the start spawn (the base draft opens on Play).")]
    [SerializeField] private bool waitForBaseChunk = true;
    [SerializeField] private BaseLayout layout;
    [Tooltip("Trees made at load (the pool grows if more are needed, with a hitch).")]
    [SerializeField, Min(1)] private int treePrewarm = 1000;
    [Tooltip("Stumps made at load.")]
    [SerializeField, Min(1)] private int stumpPrewarm = 64;
    [Tooltip("SpawnRound's dark trees go inside a square of this half size around the spawner.")]
    [SerializeField, Min(1f)] private float areaHalfSize = 95f;
    [Tooltip("Share of SpawnRound's trees placed inside the base light.")]
    [SerializeField, Range(0f, 1f)] private float roundLightShare = 0.05f;
    [Tooltip("Sparse trees (and SpawnRound's) stay at least this far from other trees.")]
    [SerializeField, Min(0f)] private float minSpacing = 5f;
    [Tooltip("A tree spot must be open ground (no base layout cells) within this radius, so canopies stay off the walls.")]
    [SerializeField, Min(0f)] private float layoutClearance = 2f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.15f);
    [Tooltip("Only trees within this distance of the player are targetable (in Damageable.All); keep it above every weapon's reach.")]
    [SerializeField, Min(1f)] private float listRadius = 30f;
    [Tooltip("Seconds between refreshes of which trees are targetable.")]
    [SerializeField, Min(0.02f)] private float listInterval = 0.25f;

    [Header("Forest layout")]
    [Tooltip("0 = a new layout every run; any other number repeats the same layout.")]
    [SerializeField] private int seed;
    [Tooltip("Units added to the max reach (the full-base light) where the thick ring starts.")]
    [SerializeField] private float ringStartOffset;
    [Tooltip("Width of the full-density part of the thick ring.")]
    [SerializeField, Min(0f)] private float thickWidth = 15f;
    [Tooltip("Past the full-density part, the forest thins to nothing over this distance.")]
    [SerializeField, Min(0.01f)] private float fadeWidth = 25f;
    [Tooltip("Distance between trees at full density.")]
    [SerializeField, Min(1f)] private float thickSpacing = 3.5f;
    [Tooltip("Where the thinning density drops below this, no more trees.")]
    [SerializeField, Range(0.01f, 1f)] private float minFadeDensity = 0.08f;
    [Tooltip("Tries per tree slot when filling thick areas (higher = closer to full).")]
    [SerializeField, Min(1f)] private float fillAttempts = 3f;
    [Tooltip("Sparse trees per 100 square units inside the max reach.")]
    [SerializeField, Min(0f)] private float sparsePer100 = 0.4f;

    [Header("Wedges")]
    [Tooltip("How many thick wedges reach in from the ring (min, max; both included).")]
    [SerializeField] private Vector2Int wedgeCountRange = new Vector2Int(0, 3);
    [Tooltip("Wedges stay out of this many chunk rings around the base chunk.")]
    [SerializeField, Min(0)] private int leewayRings = 1;
    [Tooltip("Half width where a wedge meets the ring (random between x and y).")]
    [SerializeField] private Vector2 wedgeBaseHalfWidth = new Vector2(16f, 26f);
    [Tooltip("Half width at the wedge's inner tip (random between x and y).")]
    [SerializeField] private Vector2 wedgeTipHalfWidth = new Vector2(5f, 9f);
    [Tooltip("How far in a wedge reaches: 0 = to the leeway edge, 1 = not past the ring (random between x and y).")]
    [SerializeField] private Vector2 wedgeDepth = new Vector2(0f, 0.6f);
    [Tooltip("How much a wedge's edge wanders in and out (units).")]
    [SerializeField, Min(0f)] private float wedgeRaggedness = 3f;
    [Tooltip("Gap kept between two wedges where they meet the ring (units).")]
    [SerializeField, Min(0f)] private float wedgeGap = 8f;

    private readonly List<ResourceTree> liveTrees = new List<ResourceTree>();
    private readonly List<Transform> stumps = new List<Transform>();
    private readonly List<Vector3> spots = new List<Vector3>();
    private readonly List<Wedge> wedges = new List<Wedge>();

    private ComponentPool<ResourceTree> treePool;
    private ComponentPool<Transform> stumpPool;
    private ComponentPool<DebrisBurst> debrisPool;
    private BuildGrid grid;
    private bool spawnPending;
    private float listTimer;

    // Spatial hash for the layout scatter.
    private List<int>[] hashCells;
    private int hashSize;
    private float hashCell;
    private Vector3 hashMin;

    private System.Random rng;
    private Vector3 center;
    private float maxReach;
    private float leewayRadius;

    public int LiveTreeCount => liveTrees.Count;
    public IReadOnlyList<ResourceTree> LiveTrees => liveTrees;
    /// <summary>Last layout: thick trees (ring + wedges), sparse trees, wedge count, max reach (for tests).</summary>
    public int LastThickCount { get; private set; }
    public int LastSparseCount { get; private set; }
    public int LastWedgeCount => wedges.Count;
    public float MaxReach => maxReach;
    /// <summary>Trees currently targetable (within listRadius of the player).</summary>
    public int ListedTreeCount { get; private set; }

    private void Awake()
    {
        Transform root = new GameObject("Trees").transform;
        treePool = new ComponentPool<ResourceTree>(treePrefab, root, treePrewarm);
        stumpPool = new ComponentPool<Transform>(stumpPrefab, root, stumpPrewarm);

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

        listTimer -= Time.deltaTime;
        if (listTimer <= 0f)
        {
            listTimer = listInterval;
            RefreshListed();
        }
    }

    // Only trees near the player stay in Damageable.All, so the weapons' per-frame target loops skip the far forest.
    private void RefreshListed()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null)
        {
            return;
        }
        Vector3 origin = player.transform.position;
        float sqrRadius = listRadius * listRadius;
        int listed = 0;
        for (int i = 0; i < liveTrees.Count; i++)
        {
            Vector3 offset = liveTrees[i].transform.position - origin;
            bool near = offset.x * offset.x + offset.z * offset.z <= sqrRadius;
            liveTrees[i].Listed = near;
            if (near)
            {
                listed++;
            }
        }
        ListedTreeCount = listed;
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

    /// <summary>
    /// Live trees standing on, or within layoutClearance of, a flat world rectangle (min/max corners): the trees a chunk
    /// placed there would cover. Stumps don't count.
    /// </summary>
    public int CountTreesIn(Vector3 min, Vector3 max)
    {
        float x0 = Mathf.Min(min.x, max.x) - layoutClearance;
        float x1 = Mathf.Max(min.x, max.x) + layoutClearance;
        float z0 = Mathf.Min(min.z, max.z) - layoutClearance;
        float z1 = Mathf.Max(min.z, max.z) + layoutClearance;
        int count = 0;
        for (int i = 0; i < liveTrees.Count; i++)
        {
            Vector3 p = liveTrees[i].transform.position;
            if (p.x >= x0 && p.x <= x1 && p.z >= z0 && p.z <= z1)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Clears every tree and stump and grows a new forest layout (see the header).</summary>
    [ContextMenu("Respawn trees (new layout)")]
    public void SpawnAll()
    {
        if (!Application.isPlaying)
        {
            return;
        }
        ClearAll();
        GenerateLayout(seed, spots);
        for (int i = 0; i < spots.Count; i++)
        {
            PlaceTree(spots[i]);
        }
    }

    /// <summary>
    /// Works out a forest layout into `result` (thick trees first, then sparse) without placing anything. Also works in
    /// edit mode (no pools needed), for previews. layoutSeed 0 = random.
    /// </summary>
    public void GenerateLayout(int layoutSeed, List<Vector3> result)
    {
        result.Clear();
        rng = new System.Random(layoutSeed != 0 ? layoutSeed : System.Environment.TickCount);
        ReadBaseSize();

        float outer = maxReach + thickWidth + fadeWidth;
        float maxSpacing = Mathf.Max(minSpacing, thickSpacing / Mathf.Sqrt(minFadeDensity));
        BeginHash(outer + maxSpacing, thickSpacing);
        PickWedges();

        // Thick areas: dart throwing over the whole disc, each spot kept only where the density says so and far enough
        // from its neighbours (spacing grows where the forest thins).
        float area = Mathf.PI * outer * outer;
        int candidates = Mathf.CeilToInt(fillAttempts * area / (thickSpacing * thickSpacing));
        for (int i = 0; i < candidates; i++)
        {
            Vector3 point = RandomInDisc(outer);
            float density = Density(point);
            if (density <= 0f)
            {
                continue;
            }
            float spacing = thickSpacing / Mathf.Sqrt(density);
            if (IsFree(point, spacing, result) && TrySettle(ref point))
            {
                AddSpot(point, result);
            }
        }
        LastThickCount = result.Count;

        // Sparse trees over the open ground inside the max reach.
        int sparse = Mathf.RoundToInt(sparsePer100 * Mathf.PI * maxReach * maxReach / 100f);
        for (int i = 0; i < sparse; i++)
        {
            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                Vector3 point = RandomInDisc(maxReach);
                if (Density(point) <= 0f && IsFree(point, minSpacing, result) && TrySettle(ref point))
                {
                    AddSpot(point, result);
                    break;
                }
            }
        }
        LastSparseCount = result.Count - LastThickCount;
    }

    // Max reach (the full-base light) and the leeway around the base chunk, from the layout and the light.
    private void ReadBaseSize()
    {
        if (layout == null)
        {
            layout = FindAnyObjectByType<BaseLayout>();
        }
        BaseLight baseLight = BaseLight.Instance != null ? BaseLight.Instance : FindAnyObjectByType<BaseLight>();
        BuildGrid buildGrid = BuildGrid.Instance != null ? BuildGrid.Instance : FindAnyObjectByType<BuildGrid>();
        float cell = buildGrid != null ? buildGrid.CellSize : 1f;
        float slot = WallChunk.ModuleSize * cell;
        int reach = layout != null ? layout.MaxSlotReach : 2;

        center = baseLight != null ? baseLight.BasePosition : new Vector3(transform.position.x, 0f, transform.position.z);
        // Farthest corner of a ring of slots around the core slot (the base sits in the middle of the core slot).
        float farthestCorner = (reach + 0.5f) * slot * Mathf.Sqrt(2f);
        maxReach = farthestCorner + (baseLight != null ? baseLight.Margin + baseLight.ExtraRadius : 0f) + ringStartOffset;
        leewayRadius = (leewayRings + 0.5f) * slot * Mathf.Sqrt(2f);
    }

    private void PickWedges()
    {
        wedges.Clear();
        int count = rng.Next(Mathf.Min(wedgeCountRange.x, wedgeCountRange.y), Mathf.Max(wedgeCountRange.x, wedgeCountRange.y) + 1);
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < WedgeAttempts; attempt++)
            {
                Wedge wedge = new Wedge
                {
                    angle = Range(0f, Mathf.PI * 2f),
                    baseHalfWidth = Range(wedgeBaseHalfWidth.x, wedgeBaseHalfWidth.y),
                    tipHalfWidth = Range(wedgeTipHalfWidth.x, wedgeTipHalfWidth.y),
                    noiseOffset = Range(0f, 1000f)
                };
                float nearest = leewayRadius + wedge.tipHalfWidth;
                wedge.tipRadius = Mathf.Lerp(nearest, Mathf.Max(nearest, maxReach), Range(wedgeDepth.x, wedgeDepth.y));
                if (!OverlapsWedge(wedge))
                {
                    wedges.Add(wedge);
                    break;
                }
            }
        }
    }

    private bool OverlapsWedge(Wedge wedge)
    {
        for (int i = 0; i < wedges.Count; i++)
        {
            float apart = Mathf.Abs(Mathf.DeltaAngle(wedge.angle * Mathf.Rad2Deg, wedges[i].angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float needed = (wedge.baseHalfWidth + wedges[i].baseHalfWidth + wedgeRaggedness * 2f + wedgeGap) / maxReach;
            if (apart < needed)
            {
                return true;
            }
        }
        return false;
    }

    // 1 = thick, 0 = no thick forest (sparse ground or nothing), in between = the thinning edge.
    private float Density(Vector3 point)
    {
        Vector3 offset = point - center;
        offset.y = 0f;
        float r = offset.magnitude;
        if (r >= maxReach)
        {
            float past = r - maxReach - thickWidth;
            if (past <= 0f)
            {
                return 1f;
            }
            float density = 1f - past / fadeWidth;
            return density >= minFadeDensity ? density : 0f;
        }
        return InWedge(offset, r) ? 1f : 0f;
    }

    private bool InWedge(Vector3 offset, float r)
    {
        float angle = Mathf.Atan2(offset.z, offset.x);
        for (int i = 0; i < wedges.Count; i++)
        {
            Wedge w = wedges[i];
            float ragged = wedgeRaggedness * (Mathf.PerlinNoise(w.noiseOffset + angle * 3f, w.noiseOffset + r * 0.08f) * 2f - 1f);
            if (r < w.tipRadius)
            {
                // Rounded tip: a disc around the tip point.
                Vector3 tip = new Vector3(Mathf.Cos(w.angle), 0f, Mathf.Sin(w.angle)) * w.tipRadius;
                if ((offset - tip).magnitude <= w.tipHalfWidth + ragged)
                {
                    return true;
                }
                continue;
            }
            float t = Mathf.Clamp01((r - w.tipRadius) / Mathf.Max(0.01f, maxReach - w.tipRadius));
            float half = Mathf.Lerp(w.tipHalfWidth, w.baseHalfWidth, t) + ragged;
            float lateral = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, w.angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad * r;
            if (lateral <= half)
            {
                return true;
            }
        }
        return false;
    }

    // Open ground off the base layout; with a map: on the ground, not on a ramp, not within a unit of a cliff edge.
    private bool TrySettle(ref Vector3 point)
    {
        if (!BuildGrid.IsOpenGroundAt(point, layoutClearance))
        {
            return false;
        }
        MapGround ground = MapGround.Instance;
        if (ground == null || !ground.HasData)
        {
            point.y = transform.position.y;
            return true;
        }
        Vector2Int pixel = ground.WorldToPixel(point);
        if (!ground.InBounds(pixel) || ground.IsRamp(pixel))
        {
            return false;
        }
        float y = ground.HeightAt(point);
        for (int i = 0; i < 4; i++)
        {
            Vector3 probe = point + new Vector3(i == 0 ? 1f : i == 1 ? -1f : 0f, 0f, i == 2 ? 1f : i == 3 ? -1f : 0f);
            float probeY;
            if (!ground.TryGetHeight(probe, out probeY) || Mathf.Abs(probeY - y) > 0.01f)
            {
                return false;
            }
        }
        point.y = y;
        return true;
    }

    private Vector3 RandomInDisc(float radius)
    {
        float r = radius * Mathf.Sqrt((float)rng.NextDouble());
        float a = Range(0f, Mathf.PI * 2f);
        return center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    private float Range(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private void BeginHash(float halfSize, float cellSize)
    {
        hashCell = cellSize;
        hashSize = Mathf.CeilToInt(halfSize * 2f / cellSize) + 1;
        hashMin = center - new Vector3(halfSize, 0f, halfSize);
        hashCells = new List<int>[hashSize * hashSize];
    }

    private bool IsFree(Vector3 point, float spacing, List<Vector3> placed)
    {
        int cx = Mathf.FloorToInt((point.x - hashMin.x) / hashCell);
        int cz = Mathf.FloorToInt((point.z - hashMin.z) / hashCell);
        int reach = Mathf.CeilToInt(spacing / hashCell);
        float sqrSpacing = spacing * spacing;
        for (int z = Mathf.Max(0, cz - reach); z <= Mathf.Min(hashSize - 1, cz + reach); z++)
        {
            for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(hashSize - 1, cx + reach); x++)
            {
                List<int> list = hashCells[z * hashSize + x];
                if (list == null)
                {
                    continue;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    Vector3 other = placed[list[i]];
                    float dx = other.x - point.x;
                    float dz = other.z - point.z;
                    if (dx * dx + dz * dz < sqrSpacing)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private void AddSpot(Vector3 point, List<Vector3> placed)
    {
        int cx = Mathf.Clamp(Mathf.FloorToInt((point.x - hashMin.x) / hashCell), 0, hashSize - 1);
        int cz = Mathf.Clamp(Mathf.FloorToInt((point.z - hashMin.z) / hashCell), 0, hashSize - 1);
        int index = cz * hashSize + cx;
        if (hashCells[index] == null)
        {
            hashCells[index] = new List<int>(4);
        }
        hashCells[index].Add(placed.Count);
        placed.Add(point);
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
        Vector3 origin = transform.position;
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
                point = origin + new Vector3(Random.Range(-areaHalfSize, areaHalfSize), 0f, Random.Range(-areaHalfSize, areaHalfSize));
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
