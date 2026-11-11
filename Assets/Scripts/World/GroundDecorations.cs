/*
 * GroundDecorations
 * Purely visual ground cover (2026-11-10, user): spots of grass here and there over the whole map (not a carpet).
 * Flower clusters (step 2) and bushes (step 3), same day.
 *
 * - At Start the whole map (MapGround's painted area, else a square of fallbackHalfSize around this object) is sampled on
 *   a jittered grid of 1 / sqrt(tuftsPerSquareUnit) units; a two-octave noise mask decides where the spots are
 *   (patchScale = how big, patchThreshold = how rare; soft edges thin out). seed 0 = new spots every run.
 * - Spots go anywhere on the ground (under the forest too, on ramps), but not over cliff edges and not on base layout
 *   cells (BuildGrid.IsOpenGroundAt, layoutClearance); when the layout grows (BuildGrid Changed) tufts under the new
 *   chunks are removed and only the touched tiles are rebuilt.
 * - Placeholder tufts are generated here: 3-4 wide cartoon leaves folded along the spine (a light and a darker half,
 *   user 2026-11-10: "more cartoony / blocky"), in two greens. Every tile of tileSize units is one mesh in
 *   world space on the Decoration layer, drawn with grassMaterial (Custom/CelGrass): no scripts per tuft, no colliders,
 *   no shadows cast. The Decoration layer is left out of PC_Renderer's opaque and prepass masks and drawn by its
 *   "Decorations" Render Objects feature after the outline pass and before the darkness pass (no outlines, still dark).
 * - Each frame the player's position goes to the shader global _TD_PlayerPos, which bends nearby blades away.
 * - Flower clusters (step 2): clustersPer1000 cluster centres per 1000 square units, spread evenly at random over the
 *   map; each holds flowersPerCluster flowers within clusterRadius, all in one colour picked from flowerColors (like
 *   the user's Minecraft reference). A placeholder flower = 4 square petals around a square centre, lying flat just
 *   above the ground (flowerLift), in the same tile meshes (no bending). Same ground rules and chunk removal as tufts.
 * - Bushes (step 3, user: decoration, rarer, flat ground only, wade through like trees): bushesPer1000 random spots,
 *   each kept only on flat ground (not a ramp, the same height within its radius) and bushClearance off the base layout.
 *   Pooled DecorBush objects (ComponentPool, built once at Start from a runtime template) on the Default layer with
 *   bushMaterial (Custom/CelLit), casting shadows, no collider. Placeholder look: bushShapes generated blocky clumps of
 *   3-5 boxes; bushSize = width. PlayerWading reads LiveBushes (WadeRadius = half the width + bushWadeExtra). Bushes on
 *   newly placed chunks are released.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GroundDecorations : MonoBehaviour
{
    private struct Tuft
    {
        public Vector3 position;
        public float yaw;
        public float height;
        public float shade;
    }

    private struct Flower
    {
        public Vector3 position;
        public float yaw;
        public float size;
        public int colour;
    }

    [SerializeField] private Material grassMaterial;
    [Tooltip("Layer the tile meshes are on (kept out of the outline pass by PC_Renderer).")]
    [SerializeField] private string layerName = "Decoration";
    [Tooltip("0 = new spots every run; any other number repeats the same ones.")]
    [SerializeField] private int seed;
    [SerializeField, Min(4f)] private float tileSize = 32f;
    [Tooltip("Without a painted map: decorate a square of this half size around this object.")]
    [SerializeField, Min(1f)] private float fallbackHalfSize = 103f;
    [Tooltip("Ground within this radius must be off the base layout.")]
    [SerializeField, Min(0f)] private float layoutClearance = 0.3f;

    [Header("Grass spots")]
    [Tooltip("Noise frequency: higher = smaller, more frequent spots.")]
    [SerializeField, Min(0.001f)] private float patchScale = 0.09f;
    [Tooltip("Higher = rarer spots (share of the map left bare).")]
    [SerializeField, Range(0f, 1f)] private float patchThreshold = 0.66f;
    [Tooltip("How far inside a spot (0-1 past the threshold) it reaches full density; lower = harder edges.")]
    [SerializeField, Range(0.01f, 1f)] private float patchEdge = 0.35f;
    [Tooltip("Tufts per square unit in the middle of a spot.")]
    [SerializeField, Min(0.01f)] private float tuftsPerSquareUnit = 0.9f;
    [SerializeField] private Vector2 tuftHeight = new Vector2(0.7f, 1.1f);
    [Tooltip("Radius the blades of one tuft spread over.")]
    [SerializeField, Min(0.01f)] private float tuftRadius = 0.22f;
    [SerializeField] private Vector2Int bladesPerTuft = new Vector2Int(3, 4);
    [Tooltip("Width of a leaf at its root.")]
    [SerializeField, Min(0.005f)] private float bladeWidth = 0.3f;
    [Tooltip("Height (0-1 of the leaf) of its widest point.")]
    [SerializeField, Range(0.1f, 0.9f)] private float shoulderHeight = 0.4f;
    [Tooltip("Leaf width at the shoulder, relative to the root width.")]
    [SerializeField, Min(0.1f)] private float shoulderWidth = 1.15f;
    [Tooltip("Brightness of each leaf's darker half (the cartoon fold).")]
    [SerializeField, Range(0f, 1f)] private float foldShade = 0.72f;
    [SerializeField] private Color grassColorA = new Color(0.20f, 0.40f, 0.15f);
    [SerializeField] private Color grassColorB = new Color(0.30f, 0.52f, 0.20f);
    [Tooltip("Blade roots are this dark compared to the tips.")]
    [SerializeField, Range(0f, 1f)] private float rootShade = 0.65f;

    [Header("Flower clusters")]
    [Tooltip("Cluster centres per 1000 square units of map.")]
    [SerializeField, Min(0f)] private float clustersPer1000 = 1.5f;
    [SerializeField] private Vector2Int flowersPerCluster = new Vector2Int(3, 8);
    [SerializeField, Min(0.05f)] private float clusterRadius = 1.2f;
    [Tooltip("Width of one flower (petal tip to petal tip).")]
    [SerializeField] private Vector2 flowerSize = new Vector2(0.35f, 0.5f);
    [Tooltip("Height of the flowers above the ground (keeps them off the ground's surface).")]
    [SerializeField, Min(0.001f)] private float flowerLift = 0.03f;
    [Tooltip("Petal colours; each cluster picks one.")]
    [SerializeField] private Color[] flowerColors =
    {
        new Color(0.96f, 0.62f, 0.78f),
        new Color(0.98f, 0.86f, 0.38f),
        new Color(0.96f, 0.95f, 0.90f),
        new Color(0.74f, 0.64f, 0.94f)
    };
    [SerializeField] private Color flowerCentreColor = new Color(0.98f, 0.80f, 0.30f);

    [Header("Bushes")]
    [SerializeField] private Material bushMaterial;
    [Tooltip("Bush spots tried per 1000 square units (only flat ground keeps them).")]
    [SerializeField, Min(0f)] private float bushesPer1000 = 0.5f;
    [Tooltip("Bush width (random between x and y).")]
    [SerializeField] private Vector2 bushSize = new Vector2(1.8f, 2.6f);
    [Tooltip("Bush height relative to its width.")]
    [SerializeField, Min(0.1f)] private float bushHeightRatio = 0.6f;
    [Tooltip("Ground within this radius past the bush's edge must be off the base layout.")]
    [SerializeField, Min(0f)] private float bushClearance = 0.5f;
    [Tooltip("The player is wading through a bush within its half width plus this.")]
    [SerializeField, Min(0f)] private float bushWadeExtra = 0.3f;
    [Tooltip("How many different placeholder bush shapes to generate.")]
    [SerializeField, Min(1)] private int bushShapes = 4;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<Vector2> uv0 = new List<Vector2>();
    private readonly List<Vector2> uv1 = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();

    private List<Tuft>[] tiles;
    private List<Flower>[] flowerTiles;
    private readonly List<DecorBush> liveBushes = new List<DecorBush>();
    private ComponentPool<DecorBush> bushPool;
    private Mesh[] bushMeshes;
    private Mesh[] meshes;
    private MeshRenderer[] renderers;
    private int tilesX;
    private int tilesZ;
    private Vector3 areaMin;
    private BuildGrid grid;
    private int playerPosId;

    public int TuftCount { get; private set; }
    public int FlowerCount { get; private set; }
    public int BushCount => liveBushes.Count;
    /// <summary>Bushes on the map (PlayerWading wades through them).</summary>
    public IReadOnlyList<DecorBush> LiveBushes => liveBushes;
    public int TileCount => tiles != null ? tiles.Length : 0;
    public float LastBuildMs { get; private set; }

    private void Start()
    {
        playerPosId = Shader.PropertyToID("_TD_PlayerPos");
        float start = Time.realtimeSinceStartup;
        Scatter();
        LastBuildMs = (Time.realtimeSinceStartup - start) * 1000f;

        grid = BuildGrid.Instance;
        if (grid != null)
        {
            grid.Changed += RemoveCovered;
        }
    }

    private void OnDestroy()
    {
        if (grid != null)
        {
            grid.Changed -= RemoveCovered;
        }
        if (bushMeshes != null)
        {
            for (int i = 0; i < bushMeshes.Length; i++)
            {
                if (bushMeshes[i] != null)
                {
                    Destroy(bushMeshes[i]);
                }
            }
        }
        if (meshes != null)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                {
                    Destroy(meshes[i]);
                }
            }
        }
    }

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player != null)
        {
            Shader.SetGlobalVector(playerPosId, player.transform.position);
        }
    }

    private void Scatter()
    {
        MapGround ground = MapGround.Instance != null && MapGround.Instance.HasData ? MapGround.Instance : null;
        Vector3 size;
        if (ground != null)
        {
            areaMin = ground.SouthWest;
            size = new Vector3(ground.Width * ground.UnitsPerPixel, 0f, ground.Height * ground.UnitsPerPixel);
        }
        else
        {
            areaMin = transform.position - new Vector3(fallbackHalfSize, 0f, fallbackHalfSize);
            size = new Vector3(fallbackHalfSize * 2f, 0f, fallbackHalfSize * 2f);
        }
        tilesX = Mathf.Max(1, Mathf.CeilToInt(size.x / tileSize));
        tilesZ = Mathf.Max(1, Mathf.CeilToInt(size.z / tileSize));
        tiles = new List<Tuft>[tilesX * tilesZ];
        flowerTiles = new List<Flower>[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = new List<Tuft>();
            flowerTiles[i] = new List<Flower>();
        }

        System.Random rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
        Vector2 offsetA = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        Vector2 offsetB = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        float step = 1f / Mathf.Sqrt(tuftsPerSquareUnit);
        int countX = Mathf.CeilToInt(size.x / step);
        int countZ = Mathf.CeilToInt(size.z / step);

        TuftCount = 0;
        for (int z = 0; z < countZ; z++)
        {
            for (int x = 0; x < countX; x++)
            {
                Vector3 point = areaMin + new Vector3((x + (float)rng.NextDouble()) * step, 0f, (z + (float)rng.NextDouble()) * step);
                float noise = 0.7f * Mathf.PerlinNoise(point.x * patchScale + offsetA.x, point.z * patchScale + offsetA.y)
                    + 0.3f * Mathf.PerlinNoise(point.x * patchScale * 2.7f + offsetB.x, point.z * patchScale * 2.7f + offsetB.y);
                if (noise < patchThreshold)
                {
                    continue;
                }
                float inside = (noise - patchThreshold) / Mathf.Max(0.0001f, 1f - patchThreshold);
                if (rng.NextDouble() > Mathf.SmoothStep(0f, 1f, inside / patchEdge))
                {
                    continue;
                }
                if (!Settle(ground, ref point))
                {
                    continue;
                }
                Tuft tuft = new Tuft
                {
                    position = point,
                    yaw = (float)rng.NextDouble() * 360f,
                    height = Mathf.Lerp(tuftHeight.x, tuftHeight.y, (float)rng.NextDouble()),
                    shade = (float)rng.NextDouble()
                };
                int tile = TileOf(point);
                if (tile >= 0)
                {
                    tiles[tile].Add(tuft);
                    TuftCount++;
                }
            }
        }

        ScatterFlowers(ground, size, rng);
        ScatterBushes(ground, size, rng);

        int layer = LayerMask.NameToLayer(layerName);
        meshes = new Mesh[tiles.Length];
        renderers = new MeshRenderer[tiles.Length];
        Transform root = new GameObject("GroundDecorationTiles").transform;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].Count == 0 && flowerTiles[i].Count == 0)
            {
                continue;
            }
            GameObject tileObject = new GameObject("GrassTile_" + (i % tilesX) + "_" + (i / tilesX));
            tileObject.transform.SetParent(root, false);
            if (layer >= 0)
            {
                tileObject.layer = layer;
            }
            meshes[i] = new Mesh { name = tileObject.name, indexFormat = IndexFormat.UInt32 };
            tileObject.AddComponent<MeshFilter>().sharedMesh = meshes[i];
            MeshRenderer meshRenderer = tileObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = grassMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = true;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderers[i] = meshRenderer;
            BuildTile(i);
        }
    }

    private void ScatterFlowers(MapGround ground, Vector3 size, System.Random rng)
    {
        FlowerCount = 0;
        if (flowerColors == null || flowerColors.Length == 0)
        {
            return;
        }
        int clusters = Mathf.RoundToInt(size.x * size.z / 1000f * clustersPer1000);
        for (int c = 0; c < clusters; c++)
        {
            Vector3 centre = areaMin + new Vector3((float)rng.NextDouble() * size.x, 0f, (float)rng.NextDouble() * size.z);
            int colour = rng.Next(flowerColors.Length);
            int count = rng.Next(flowersPerCluster.x, flowersPerCluster.y + 1);
            for (int f = 0; f < count; f++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = clusterRadius * Mathf.Sqrt((float)rng.NextDouble());
                Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!Settle(ground, ref point))
                {
                    continue;
                }
                int tile = TileOf(point);
                if (tile < 0)
                {
                    continue;
                }
                flowerTiles[tile].Add(new Flower
                {
                    position = point,
                    yaw = (float)rng.NextDouble() * 90f,
                    size = Mathf.Lerp(flowerSize.x, flowerSize.y, (float)rng.NextDouble()),
                    colour = colour
                });
                FlowerCount++;
            }
        }
    }

    private void ScatterBushes(MapGround ground, Vector3 size, System.Random rng)
    {
        liveBushes.Clear();
        if (bushMaterial == null || bushesPer1000 <= 0f)
        {
            return;
        }
        int tries = Mathf.RoundToInt(size.x * size.z / 1000f * bushesPer1000);
        bushMeshes = new Mesh[Mathf.Max(1, bushShapes)];
        for (int i = 0; i < bushMeshes.Length; i++)
        {
            bushMeshes[i] = BuildBushMesh(rng, i);
        }

        // A runtime template for the pool: root DecorBush + child Visual with the mesh.
        Transform root = new GameObject("Bushes").transform;
        GameObject template = new GameObject("Bush");
        template.SetActive(false);
        template.transform.SetParent(root, false);
        DecorBush templateBush = template.AddComponent<DecorBush>();
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(template.transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = bushMeshes[0];
        MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = bushMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        templateBush.SetVisual(visual.transform);
        bushPool = new ComponentPool<DecorBush>(templateBush, root, Mathf.Max(1, tries / 2));

        for (int i = 0; i < tries; i++)
        {
            Vector3 point = areaMin + new Vector3((float)rng.NextDouble() * size.x, 0f, (float)rng.NextDouble() * size.z);
            float width = Mathf.Lerp(bushSize.x, bushSize.y, (float)rng.NextDouble());
            float yaw = (float)rng.NextDouble() * 360f;
            int shape = rng.Next(bushMeshes.Length);
            if (!SettleFlat(ground, ref point, width * 0.5f))
            {
                continue;
            }
            DecorBush bush = bushPool.Get(point, Quaternion.Euler(0f, yaw, 0f));
            Transform bushVisual = bush.Visual;
            bushVisual.GetComponent<MeshFilter>().sharedMesh = bushMeshes[shape];
            bushVisual.localScale = new Vector3(width, width * bushHeightRatio, width);
            bushVisual.localRotation = Quaternion.identity;
            bush.CaptureRest();
            bush.WadeRadius = width * 0.5f + bushWadeExtra;
            liveBushes.Add(bush);
        }
    }

    // Flat ground only: off the layout (bush edge + bushClearance), not a ramp, the same height across its width.
    private bool SettleFlat(MapGround ground, ref Vector3 point, float radius)
    {
        if (!BuildGrid.IsOpenGroundAt(point, radius + bushClearance))
        {
            return false;
        }
        if (ground == null)
        {
            point.y = transform.position.y;
            return true;
        }
        if (!ground.Contains(point, radius + 0.5f) || ground.IsRamp(ground.WorldToPixel(point)))
        {
            return false;
        }
        float y = ground.HeightAt(point);
        float reach = radius + 0.3f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float probeY;
            Vector3 probe = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
            if (!ground.TryGetHeight(probe, out probeY) || Mathf.Abs(probeY - y) > 0.01f)
            {
                return false;
            }
        }
        point.y = y;
        return true;
    }

    // A placeholder bush shape: 3-5 boxes of a unit-wide, unit-tall clump (scaled per bush), flat-shaded faces.
    private Mesh BuildBushMesh(System.Random rng, int index)
    {
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();
        int boxes = 3 + rng.Next(3);
        for (int b = 0; b < boxes; b++)
        {
            Vector3 centre;
            Vector3 extent;
            if (b == 0)
            {
                centre = new Vector3(0f, 0.42f, 0f);
                extent = new Vector3(0.36f, 0.42f, 0.36f);
            }
            else
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = 0.18f + 0.12f * (float)rng.NextDouble();
                float height = 0.25f + 0.13f * (float)rng.NextDouble();
                float half = 0.2f + 0.08f * (float)rng.NextDouble();
                centre = new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
                extent = new Vector3(half, height, half);
            }
            Quaternion turn = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);
            AddBox(positions, normals, indices, centre, extent, turn);
        }
        Mesh mesh = new Mesh { name = "BushShape" + index };
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        Vector2[] uvs = new Vector2[positions.Count];
        mesh.uv = uvs;
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddBox(List<Vector3> positions, List<Vector3> normals, List<int> indices, Vector3 centre,
        Vector3 extent, Quaternion turn)
    {
        Vector3[] faceNormals = { Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        foreach (Vector3 n in faceNormals)
        {
            // Two axes across the face.
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(n, u);
            Vector3 faceCentre = centre + turn * Vector3.Scale(n, extent);
            Vector3 du = turn * Vector3.Scale(u, extent);
            Vector3 dv = turn * Vector3.Scale(v, extent);
            Vector3 normal = turn * n;
            int start = positions.Count;
            positions.Add(faceCentre - du - dv);
            positions.Add(faceCentre - du + dv);
            positions.Add(faceCentre + du + dv);
            positions.Add(faceCentre + du - dv);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
            }
            // Wind so the face points along its normal.
            Vector3 cross = Vector3.Cross(positions[start + 1] - positions[start], positions[start + 2] - positions[start]);
            if (Vector3.Dot(cross, normal) >= 0f)
            {
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }
            else
            {
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
                indices.Add(start); indices.Add(start + 3); indices.Add(start + 2);
            }
        }
    }

    // On the ground, off the base layout, not hanging over a cliff edge; sets the height.
    private bool Settle(MapGround ground, ref Vector3 point)
    {
        if (!BuildGrid.IsOpenGroundAt(point, layoutClearance))
        {
            return false;
        }
        if (ground == null)
        {
            point.y = transform.position.y;
            return true;
        }
        if (!ground.Contains(point, 0.5f))
        {
            return false;
        }
        float y = ground.HeightAt(point);
        const float probe = 0.35f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = new Vector3(i == 0 ? probe : i == 1 ? -probe : 0f, 0f, i == 2 ? probe : i == 3 ? -probe : 0f);
            float probeY;
            if (!ground.TryGetHeight(point + offset, out probeY) || Mathf.Abs(probeY - y) > 0.5f)
            {
                return false;
            }
        }
        point.y = y;
        return true;
    }

    private int TileOf(Vector3 point)
    {
        int x = Mathf.FloorToInt((point.x - areaMin.x) / tileSize);
        int z = Mathf.FloorToInt((point.z - areaMin.z) / tileSize);
        if (x < 0 || z < 0 || x >= tilesX || z >= tilesZ)
        {
            return -1;
        }
        return z * tilesX + x;
    }

    // The base layout grew: drop tufts on the new chunks and rebuild the tiles that changed.
    private void RemoveCovered()
    {
        if (tiles == null || grid == null)
        {
            return;
        }
        Vector3 min = grid.Origin;
        Vector3 max = min + new Vector3(grid.Width * grid.CellSize, 0f, grid.Height * grid.CellSize);
        int x0 = Mathf.Max(0, Mathf.FloorToInt((min.x - areaMin.x) / tileSize));
        int x1 = Mathf.Min(tilesX - 1, Mathf.FloorToInt((max.x - areaMin.x) / tileSize));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((min.z - areaMin.z) / tileSize));
        int z1 = Mathf.Min(tilesZ - 1, Mathf.FloorToInt((max.z - areaMin.z) / tileSize));
        for (int b = liveBushes.Count - 1; b >= 0; b--)
        {
            DecorBush bush = liveBushes[b];
            if (!grid.IsOpenGround(bush.transform.position, bush.WadeRadius - bushWadeExtra + bushClearance))
            {
                bushPool.Release(bush);
                liveBushes.RemoveAt(b);
            }
        }
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int i = z * tilesX + x;
                int before = tiles[i].Count;
                tiles[i].RemoveAll(t => !grid.IsOpenGround(t.position, layoutClearance));
                int flowersBefore = flowerTiles[i].Count;
                flowerTiles[i].RemoveAll(f => !grid.IsOpenGround(f.position, layoutClearance));
                if (tiles[i].Count != before || flowerTiles[i].Count != flowersBefore)
                {
                    TuftCount -= before - tiles[i].Count;
                    FlowerCount -= flowersBefore - flowerTiles[i].Count;
                    BuildTile(i);
                }
            }
        }
    }

    // One tile mesh: each tuft is a few thin triangles (blades) around its root; uv0.x = bend weight, uv1 = root XZ.
    // Half a leaf as two triangles: outer root, outer shoulder, tip, spine root (flat colours, own vertices).
    private void AddLeafHalf(Vector3 outerRoot, Vector3 outerShoulder, Vector3 tipPoint, Vector3 spineRoot, Color rootColour,
        Color tipColour, Vector2 root)
    {
        int start = vertices.Count;
        vertices.Add(outerRoot);
        vertices.Add(outerShoulder);
        vertices.Add(tipPoint);
        vertices.Add(spineRoot);
        colors.Add(rootColour);
        colors.Add(Color.Lerp(rootColour, tipColour, shoulderHeight));
        colors.Add(tipColour);
        colors.Add(rootColour);
        uv0.Add(Vector2.zero);
        uv0.Add(new Vector2(shoulderHeight, 0f));
        uv0.Add(new Vector2(1f, 0f));
        uv0.Add(Vector2.zero);
        for (int i = 0; i < 4; i++)
        {
            uv1.Add(root);
        }
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 3);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    // A flat square lying on the ground (no bending): centre, half size, yaw rotation, flat colour.
    private void AddFlatSquare(Vector3 centre, float half, Quaternion yaw, Color colour, Vector2 root)
    {
        int start = vertices.Count;
        vertices.Add(centre + yaw * new Vector3(-half, 0f, -half));
        vertices.Add(centre + yaw * new Vector3(-half, 0f, half));
        vertices.Add(centre + yaw * new Vector3(half, 0f, half));
        vertices.Add(centre + yaw * new Vector3(half, 0f, -half));
        for (int i = 0; i < 4; i++)
        {
            colors.Add(colour);
            uv0.Add(Vector2.zero);
            uv1.Add(root);
        }
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    // A placeholder flower: 4 square petals around a square centre, flat on the ground.
    private void AddFlower(Flower flower)
    {
        Quaternion yaw = Quaternion.Euler(0f, flower.yaw, 0f);
        Color petal = flowerColors[Mathf.Clamp(flower.colour, 0, flowerColors.Length - 1)].linear;
        Color centreColour = flowerCentreColor.linear;
        Vector2 root = new Vector2(flower.position.x, flower.position.z);
        Vector3 basePoint = flower.position + Vector3.up * flowerLift;
        float petalHalf = flower.size * 0.2f;
        float petalOffset = flower.size * 0.27f;
        AddFlatSquare(basePoint + yaw * new Vector3(petalOffset, 0f, 0f), petalHalf, yaw, petal, root);
        AddFlatSquare(basePoint + yaw * new Vector3(-petalOffset, 0f, 0f), petalHalf, yaw, petal, root);
        AddFlatSquare(basePoint + yaw * new Vector3(0f, 0f, petalOffset), petalHalf, yaw, petal, root);
        AddFlatSquare(basePoint + yaw * new Vector3(0f, 0f, -petalOffset), petalHalf, yaw, petal, root);
        AddFlatSquare(basePoint + Vector3.up * 0.005f, flower.size * 0.13f, yaw, centreColour, root);
    }

    private void BuildTile(int index)
    {
        Mesh mesh = meshes[index];
        if (mesh == null)
        {
            return;
        }
        vertices.Clear();
        colors.Clear();
        uv0.Clear();
        uv1.Clear();
        triangles.Clear();

        List<Tuft> tufts = tiles[index];
        for (int t = 0; t < tufts.Count; t++)
        {
            Tuft tuft = tufts[t];
            // A per-tuft generator so a rebuild gives the same blades.
            System.Random rng = new System.Random(tuft.position.GetHashCode());
            int blades = rng.Next(bladesPerTuft.x, bladesPerTuft.y + 1);
            // Inspector colours are sRGB; the project renders in linear space.
            Color tip = Color.Lerp(grassColorA, grassColorB, tuft.shade).linear;
            Color baseColour = (Color.Lerp(grassColorA, grassColorB, tuft.shade) * rootShade).linear;
            baseColour.a = 1f;
            Vector2 root = new Vector2(tuft.position.x, tuft.position.z);
            for (int b = 0; b < blades; b++)
            {
                float angle = (tuft.yaw + b * 360f / blades + (float)rng.NextDouble() * 40f) * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-outward.z, 0f, outward.x);
                float spread = tuftRadius * (0.2f + 0.8f * (float)rng.NextDouble());
                float height = tuft.height * (0.7f + 0.3f * (float)rng.NextDouble());
                Vector3 baseCentre = tuft.position + outward * spread;
                Vector3 tipPoint = baseCentre + outward * (height * 0.35f) + Vector3.up * height;

                // A wide cartoon leaf, folded along its spine: a light half and a darker half, each widest at
                // the shoulder (shoulderHeight of the way up) and meeting in a point at the tip.
                Vector3 halfWidth = side * bladeWidth * 0.5f;
                Vector3 shoulder = baseCentre + outward * (height * 0.35f * shoulderHeight) + Vector3.up * (height * shoulderHeight);
                Vector3 shoulderHalf = halfWidth * shoulderWidth;
                Color lightTip = tip;
                Color lightRoot = baseColour;
                Color darkTip = tip * foldShade;
                Color darkRoot = baseColour * foldShade;
                darkTip.a = 1f;
                darkRoot.a = 1f;
                AddLeafHalf(baseCentre - halfWidth, shoulder - shoulderHalf, tipPoint, baseCentre, lightRoot, lightTip, root);
                AddLeafHalf(baseCentre + halfWidth, shoulder + shoulderHalf, tipPoint, baseCentre, darkRoot, darkTip, root);
            }
        }

        List<Flower> flowers = flowerTiles[index];
        for (int f = 0; f < flowers.Count; f++)
        {
            AddFlower(flowers[f]);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetTriangles(triangles, 0);
        Vector3[] normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }
        mesh.normals = normals;
        mesh.RecalculateBounds();
        Bounds bounds = mesh.bounds;
        bounds.Expand(new Vector3(2f, 1f, 2f));
        mesh.bounds = bounds;
        if (renderers[index] != null)
        {
            renderers[index].enabled = tufts.Count > 0 || flowers.Count > 0;
        }
    }
}
