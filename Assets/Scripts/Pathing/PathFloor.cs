/*
 * PathFloor
 * A dirt floor on the base's paths (2026-11-19, user): the Path cells between the walls get a dirt ground, while wall
 * cells stay grass, so each fence (WallFence, in the middle of its wall cell) stands on a grass strip at the path's edge.
 * Purely visual: no collider, gameplay reads the BuildGrid as before.
 *
 * - Lives on BasePoint next to BaseLayout. On every BaseLayout.LayoutChanged (and at Start) it reads the BuildGrid's
 *   Path cells (and the grid's Base cells around the box when includeBase is on, so the paths reach the box) and bakes
 *   one flat mesh lift units above the ground on a runtime child "PathFloor" (Default layer, receives shadows, casts
 *   none). The old mesh is destroyed on each rebuild. Nothing is drawn until the layout has a Path cell (no dirt under
 *   the box before the base chunk is placed).
 * - Ragged edge (user: the straight edge was too clean): cells away from the edge are merged into big rectangles; along
 *   the edge the floor is built from edgeSubdivisions x edgeSubdivisions small squares per cell (4 = 0.25 units = one
 *   pixel of the dirt texture). A square is dirt when its signed distance to the cell edge (+ inside the path) plus a
 *   smooth wave (edgeWaveAmplitude, edgeWaveScale) and a per-square jitter (edgeJitter) is above 0. Dirt spills at most
 *   maxSpillIntoWalls into wall cells (the fence keeps grass under it) and maxSpill into other ground. The wave is
 *   fixed in world space, so a rebuild gives the same edge.
 * - The look comes from the material: Custom/CelLit with World Space Ground Textures on (dirt detail + the ground's
 *   patch texture), like the grass ground materials.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PathFloor : MonoBehaviour
{
    [SerializeField] private BaseLayout layout;
    [Tooltip("Custom/CelLit with World Space Ground Textures on (Materials/PathDirt.mat).")]
    [SerializeField] private Material material;
    [Tooltip("Also cover the grid's Base cells around the box, so the paths reach it.")]
    [SerializeField] private bool includeBase = true;
    [Tooltip("Height above the ground (keeps it from flickering with the ground).")]
    [SerializeField, Min(0.001f)] private float lift = 0.02f;

    [Header("Ragged edge")]
    [Tooltip("Small squares per cell side along the edge (4 = 0.25 units = one dirt texture pixel).")]
    [SerializeField, Range(1, 8)] private int edgeSubdivisions = 4;
    [Tooltip("How far the edge waves in and out (units).")]
    [SerializeField, Min(0f)] private float edgeWaveAmplitude = 0.3f;
    [Tooltip("Length of one wave along the edge (units).")]
    [SerializeField, Min(0.1f)] private float edgeWaveScale = 1.4f;
    [Tooltip("Random per-square roughness on top of the wave (units).")]
    [SerializeField, Min(0f)] private float edgeJitter = 0.12f;
    [Tooltip("How far dirt may reach into a wall cell (its fence stands 0.5 in).")]
    [SerializeField, Min(0f)] private float maxSpillIntoWalls = 0.25f;
    [Tooltip("How far dirt may reach into other ground (build strips, open ground).")]
    [SerializeField, Min(0f)] private float maxSpill = 0.4f;

    private readonly HashSet<Vector2Int> interior = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> fine = new HashSet<Vector2Int>();
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();
    private bool[] floor = new bool[0];
    private Transform floorObject;
    private MeshFilter floorFilter;
    private MeshRenderer floorRenderer;
    private Mesh mesh;
    private bool started;

    /// <summary>Milliseconds the last rebuild took (testing).</summary>
    public float LastBuildMs { get; private set; }
    public int CellCount { get; private set; }
    public int QuadCount { get; private set; }

    private void OnEnable()
    {
        if (layout == null)
        {
            layout = GetComponent<BaseLayout>();
        }
        if (layout != null)
        {
            layout.LayoutChanged += Rebuild;
        }
        if (started)
        {
            Rebuild();
        }
    }

    private void Start()
    {
        started = true;
        Rebuild();
    }

    private void OnDisable()
    {
        if (layout != null)
        {
            layout.LayoutChanged -= Rebuild;
        }
        if (floorObject != null)
        {
            floorObject.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && started && isActiveAndEnabled)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (!Application.isPlaying || layout == null || layout.Grid == null)
        {
            return;
        }
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        BuildGrid grid = layout.Grid;
        int width = grid.Width;
        int height = grid.Height;
        if (floor.Length != width * height)
        {
            floor = new bool[width * height];
        }

        bool anyPath = false;
        int count = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                CellState state = grid.GetState(new Vector2Int(x, y));
                bool isFloor = state == CellState.Path || (includeBase && state == CellState.Base);
                floor[y * width + x] = isFloor;
                if (state == CellState.Path)
                {
                    anyPath = true;
                }
                if (isFloor)
                {
                    count++;
                }
            }
        }
        if (!anyPath)
        {
            System.Array.Clear(floor, 0, floor.Length);
            count = 0;
        }
        CellCount = count;

        // Cells whose 8 neighbours are all floor are whole rectangles; every cell next to the edge (floor or not) is
        // split into small squares that follow the wavy edge.
        interior.Clear();
        fine.Clear();
        int sub = Mathf.Max(1, edgeSubdivisions);
        float size = grid.CellSize;
        Vector3 origin = grid.Origin;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isFloor = floor[y * width + x];
                bool mixed = false;
                for (int dy = -1; dy <= 1 && !mixed; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (IsFloor(x + dx, y + dy, width, height) != isFloor)
                        {
                            mixed = true;
                            break;
                        }
                    }
                }
                if (!mixed)
                {
                    if (isFloor)
                    {
                        interior.Add(new Vector2Int(x, y));
                    }
                    continue;
                }
                AddEdgeSquares(grid, x, y, isFloor, sub, width, height, origin, size);
            }
        }

        EnsureFloorObject();
        Matrix4x4 toLocal = floorObject.worldToLocalMatrix;
        vertices.Clear();
        triangles.Clear();
        int quads = 0;
        foreach (RectInt r in WallBoxes.Merge(interior))
        {
            AddQuad(toLocal, origin.x + r.x * size, origin.z + r.y * size, origin.x + r.xMax * size, origin.z + r.yMax * size);
            quads++;
        }
        float step = size / sub;
        foreach (RectInt r in WallBoxes.Merge(fine))
        {
            AddQuad(toLocal, origin.x + r.x * step, origin.z + r.y * step, origin.x + r.xMax * step, origin.z + r.yMax * step);
            quads++;
        }
        QuadCount = quads;

        if (mesh != null)
        {
            Destroy(mesh);
        }
        mesh = new Mesh { name = "PathFloor", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        Vector3 up = toLocal.MultiplyVector(Vector3.up).normalized;
        Vector3[] normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = up;
        }
        mesh.normals = normals;
        mesh.SetUVs(0, new Vector2[vertices.Count]);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        floorFilter.sharedMesh = mesh;
        floorRenderer.sharedMaterial = material;
        floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
        floorRenderer.receiveShadows = true;
        floorObject.gameObject.SetActive(vertices.Count > 0);

        watch.Stop();
        LastBuildMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    private bool IsFloor(int x, int y, int width, int height)
    {
        return x >= 0 && y >= 0 && x < width && y < height && floor[y * width + x];
    }

    // The small squares of one edge cell that end up dirt.
    private void AddEdgeSquares(BuildGrid grid, int x, int y, bool isFloor, int sub, int width, int height,
        Vector3 origin, float size)
    {
        float limit = 0f;
        if (!isFloor)
        {
            limit = grid.GetState(new Vector2Int(x, y)) == CellState.Wall ? maxSpillIntoWalls : maxSpill;
            if (limit <= 0f)
            {
                return;
            }
        }
        for (int sy = 0; sy < sub; sy++)
        {
            for (int sx = 0; sx < sub; sx++)
            {
                // Square centre in cell units.
                float px = x + (sx + 0.5f) / sub;
                float py = y + (sy + 0.5f) / sub;
                float distance = EdgeDistance(px, py, x, y, isFloor, width, height) * size;
                if (!isFloor && distance > limit)
                {
                    continue;
                }
                float signedDistance = isFloor ? distance : -distance;
                float wx = origin.x + px * size;
                float wz = origin.z + py * size;
                float value = signedDistance + edgeWaveAmplitude * Wave(wx, wz) + edgeJitter * Jitter(wx, wz);
                if (value > 0f)
                {
                    fine.Add(new Vector2Int(x * sub + sx, y * sub + sy));
                }
            }
        }
    }

    // Distance (cell units) from a point in cell (x, y) to the nearest neighbouring cell of the other kind.
    private float EdgeDistance(float px, float py, int x, int y, bool isFloor, int width, int height)
    {
        float best = 2f;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if ((dx == 0 && dy == 0) || IsFloor(x + dx, y + dy, width, height) == isFloor)
                {
                    continue;
                }
                float nx = x + dx;
                float ny = y + dy;
                float ex = Mathf.Max(nx - px, 0f, px - (nx + 1f));
                float ey = Mathf.Max(ny - py, 0f, py - (ny + 1f));
                best = Mathf.Min(best, Mathf.Sqrt(ex * ex + ey * ey));
            }
        }
        return best;
    }

    // Smooth wave in -1..1, fixed in world space (two octaves of Perlin noise).
    private float Wave(float wx, float wz)
    {
        float a = Mathf.PerlinNoise(wx / edgeWaveScale + 31.7f, wz / edgeWaveScale + 12.3f);
        float b = Mathf.PerlinNoise(wx / (edgeWaveScale * 0.45f) + 71.1f, wz / (edgeWaveScale * 0.45f) + 5.9f);
        return (a * 0.7f + b * 0.3f - 0.5f) * 2f;
    }

    // Per-square roughness in -1..1 from a position hash.
    private static float Jitter(float wx, float wz)
    {
        int hx = Mathf.RoundToInt(wx * 8f);
        int hz = Mathf.RoundToInt(wz * 8f);
        uint h = (uint)(hx * 73856093) ^ (uint)(hz * 19349663);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return (h & 0xffff) / 32767.5f - 1f;
    }

    private void AddQuad(Matrix4x4 toLocal, float x0, float z0, float x1, float z1)
    {
        int start = vertices.Count;
        vertices.Add(toLocal.MultiplyPoint3x4(new Vector3(x0, lift, z0)));
        vertices.Add(toLocal.MultiplyPoint3x4(new Vector3(x0, lift, z1)));
        vertices.Add(toLocal.MultiplyPoint3x4(new Vector3(x1, lift, z1)));
        vertices.Add(toLocal.MultiplyPoint3x4(new Vector3(x1, lift, z0)));
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private void EnsureFloorObject()
    {
        if (floorObject != null)
        {
            return;
        }
        GameObject go = new GameObject("PathFloor");
        go.layer = 0;
        floorObject = go.transform;
        floorObject.SetParent(transform, false);
        floorFilter = go.AddComponent<MeshFilter>();
        floorRenderer = go.AddComponent<MeshRenderer>();
    }
}
