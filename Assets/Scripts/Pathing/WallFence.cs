/*
 * WallFence
 * The look of the base walls (2026-11-10, user's pick: a plank fence). Lives on BasePoint next to BaseLayout and only
 * changes how walls look: the WallBlocks boxes keep their colliders (player collision, standing on wall tops) and the
 * grid still drives enemies and enemy bullets; their renderers are hidden instead.
 *
 * - On every BaseLayout.LayoutChanged it reads every Wall cell of the BuildGrid (the whole layout, so fences flow across
 *   chunk borders) and bakes the fence into ONE mesh (a submesh per material) on a runtime child "WallFence" (Default
 *   layer: outlined, casts shadows, no collider). The old mesh is destroyed on each rebuild.
 * - Rules (docs/td-design.md > "Wall look: plank fence", reference docs/fence-mockup-reference.cs.txt):
 *   - Wall cells link to wall neighbours: the 4 straight ones always, a diagonal one only when neither shared straight
 *     cell is a wall (stair-steps stay connected). Each link is a fence segment: 2 rails + vertical boards.
 *   - A cell's anchor is its centre, or for cells in a 2x2 all-wall block the block's centre, where a pillar stands;
 *     links inside the block are skipped so fences run into the pillar.
 *   - A capped joint post on every non-straight cell (corner, T, end, diagonal turn); a plain post every second cell
 *     on straight runs. A wall cell with no wall neighbour is a crate (half of them with a smaller crate on top).
 * - Hides the renderers of every "WallBlocks" child (the old one may still exist for a frame: Destroy is deferred).
 *   Disabling this component (or unticking hideBlocks) shows the blocks again, for comparing.
 * - Sizes and spacings are placeholders (the mock-up's numbers).
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class WallFence : MonoBehaviour
{
    private const int MatPlank = 0;
    private const int MatPlankDark = 1;
    private const int MatRail = 2;
    private const int MatPost = 3;
    private const int MatCap = 4;
    private const int MatCrate = 5;
    private const int MatCrateFrame = 6;
    private const int MaterialCount = 7;
    // Caps sink this far into their post's top (as in the mock-up).
    private const float CapSink = 0.01f;

    [SerializeField] private BaseLayout layout;
    [Tooltip("Hide the WallBlocks renderers (their colliders stay).")]
    [SerializeField] private bool hideBlocks = true;
    [SerializeField] private bool castShadows = true;

    [Header("Materials (Custom/CelLit)")]
    [SerializeField] private Material plank;
    [SerializeField] private Material plankDark;
    [SerializeField] private Material rail;
    [SerializeField] private Material post;
    [SerializeField] private Material cap;
    [SerializeField] private Material crate;
    [SerializeField] private Material crateFrame;

    [Header("Rails")]
    [Tooltip("Rail cross-section: x = width, y = height.")]
    [SerializeField] private Vector2 railSize = new Vector2(0.1f, 0.14f);
    [SerializeField] private float lowRailHeight = 0.45f;
    [SerializeField] private float highRailHeight = 1.25f;
    [Tooltip("Rails sit this far to one side of the fence line, boards to the other.")]
    [SerializeField] private float railOffset = 0.06f;

    [Header("Boards")]
    [SerializeField, Min(0.01f)] private float boardWidth = 0.28f;
    [SerializeField, Min(0.01f)] private float boardThickness = 0.07f;
    [Tooltip("Distance between board centres (rounded so a segment holds whole boards).")]
    [SerializeField, Min(0.05f)] private float boardSpacing = 0.31f;
    [SerializeField] private float boardOffset = 0.03f;
    [SerializeField] private float tallBoardHeight = 1.55f;
    [SerializeField] private float shortBoardHeight = 1.42f;
    [Tooltip("Height step of the random jitter (-2..+2 steps).")]
    [SerializeField] private float boardHeightJitter = 0.02f;
    [Tooltip("Boards tilt by -1, 0 or +1 times this (degrees).")]
    [SerializeField] private float boardTilt = 1.5f;
    [Tooltip("Every this-many boards (by hash) use the darker plank material.")]
    [SerializeField, Min(1)] private int darkBoardEvery = 3;

    [Header("Posts")]
    [SerializeField] private Vector3 jointPostSize = new Vector3(0.34f, 1.8f, 0.34f);
    [Tooltip("Joint post cap: x = width, y = thickness.")]
    [SerializeField] private Vector2 jointCapSize = new Vector2(0.42f, 0.07f);
    [SerializeField] private Vector3 plainPostSize = new Vector3(0.22f, 1.7f, 0.22f);
    [SerializeField] private Vector3 pillarSize = new Vector3(0.66f, 1.84f, 0.66f);
    [Tooltip("Pillar cap: x = width, y = thickness.")]
    [SerializeField] private Vector2 pillarCapSize = new Vector2(0.76f, 0.08f);

    [Header("Crates (lone wall cells)")]
    [SerializeField, Min(0.05f)] private float crateSize = 0.92f;
    [SerializeField, Min(0.05f)] private float smallCrateSize = 0.62f;
    [SerializeField, Min(0.01f)] private float crateEdgeWidth = 0.13f;
    [SerializeField, Min(0.01f)] private float crateFrameThickness = 0.1f;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<int>[] indices = new List<int>[MaterialCount];
    private readonly HashSet<long> innerEdges = new HashSet<long>();
    private Vector3[] anchors = new Vector3[0];
    private bool[] inPillar = new bool[0];
    private bool[] walls = new bool[0];
    private int[] degree = new int[0];
    private Vector3[] firstDir = new Vector3[0];
    private Vector3[] secondDir = new Vector3[0];
    private int gridWidth;
    private int gridHeight;

    private Transform fenceObject;
    private MeshFilter fenceFilter;
    private MeshRenderer fenceRenderer;
    private Mesh mesh;
    private Matrix4x4 toLocal;
    private bool started;

    /// <summary>Milliseconds the last rebuild took (testing).</summary>
    public float LastBuildMs { get; private set; }
    public int VertexCount => mesh != null ? mesh.vertexCount : 0;
    public int WallCellCount { get; private set; }
    public int SegmentCount { get; private set; }
    public int PillarCount { get; private set; }
    public int CrateCount { get; private set; }
    public int JointPostCount { get; private set; }

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
        if (fenceObject != null)
        {
            fenceObject.gameObject.SetActive(false);
        }
        SetBlockRenderers(true);
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
        SetBlockRenderers(!hideBlocks);
        EnsureFenceObject();
        toLocal = fenceObject.worldToLocalMatrix;

        vertices.Clear();
        normals.Clear();
        for (int m = 0; m < MaterialCount; m++)
        {
            if (indices[m] == null)
            {
                indices[m] = new List<int>();
            }
            indices[m].Clear();
        }
        SegmentCount = 0;
        PillarCount = 0;
        CrateCount = 0;
        JointPostCount = 0;

        BuildFence(layout.Grid);

        if (mesh != null)
        {
            Destroy(mesh);
        }
        mesh = new Mesh { name = "WallFence", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, new Vector2[vertices.Count]);
        mesh.subMeshCount = MaterialCount;
        for (int m = 0; m < MaterialCount; m++)
        {
            mesh.SetTriangles(indices[m], m, false);
        }
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        fenceFilter.sharedMesh = mesh;
        fenceRenderer.sharedMaterials = new[] { plank, plankDark, rail, post, cap, crate, crateFrame };
        fenceRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        fenceObject.gameObject.SetActive(vertices.Count > 0);

        watch.Stop();
        LastBuildMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    private void EnsureFenceObject()
    {
        if (fenceObject != null)
        {
            return;
        }
        GameObject go = new GameObject("WallFence");
        go.layer = 0;
        fenceObject = go.transform;
        fenceObject.SetParent(transform, false);
        fenceFilter = go.AddComponent<MeshFilter>();
        fenceRenderer = go.AddComponent<MeshRenderer>();
    }

    // Every WallBlocks child (the old one lingers for a frame after a rebuild: Destroy is deferred).
    private void SetBlockRenderers(bool visible)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "WallBlocks")
            {
                continue;
            }
            foreach (MeshRenderer block in child.GetComponentsInChildren<MeshRenderer>(true))
            {
                block.enabled = visible;
            }
        }
    }

    private bool IsWall(int x, int y)
    {
        return x >= 0 && y >= 0 && x < gridWidth && y < gridHeight && walls[y * gridWidth + x];
    }

    private long EdgeKey(int x0, int y0, int x1, int y1)
    {
        return (long)(y0 * gridWidth + x0) * (gridWidth * gridHeight) + (y1 * gridWidth + x1);
    }

    private void BuildFence(BuildGrid grid)
    {
        gridWidth = grid.Width;
        gridHeight = grid.Height;
        int count = gridWidth * gridHeight;
        if (walls.Length != count)
        {
            walls = new bool[count];
            anchors = new Vector3[count];
            inPillar = new bool[count];
            degree = new int[count];
            firstDir = new Vector3[count];
            secondDir = new Vector3[count];
        }

        WallCellCount = 0;
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int i = y * gridWidth + x;
                walls[i] = grid.GetState(new Vector2Int(x, y)) == CellState.Wall;
                anchors[i] = grid.CellToWorld(new Vector2Int(x, y));
                inPillar[i] = false;
                degree[i] = 0;
                if (walls[i])
                {
                    WallCellCount++;
                }
            }
        }
        if (WallCellCount == 0)
        {
            return;
        }

        // 2x2 all-wall blocks: a pillar on the block's centre, the 4 links inside the block skipped.
        float half = grid.CellSize * 0.5f;
        innerEdges.Clear();
        for (int y = 0; y < gridHeight - 1; y++)
        {
            for (int x = 0; x < gridWidth - 1; x++)
            {
                if (!(IsWall(x, y) && IsWall(x + 1, y) && IsWall(x, y + 1) && IsWall(x + 1, y + 1)))
                {
                    continue;
                }
                Vector3 centre = anchors[y * gridWidth + x] + new Vector3(half, 0f, half);
                AddBox(centre + Vector3.up * (pillarSize.y * 0.5f), pillarSize, Quaternion.identity, MatPost, false);
                AddBox(centre + Vector3.up * (pillarSize.y + pillarCapSize.y * 0.5f - CapSink),
                    new Vector3(pillarCapSize.x, pillarCapSize.y, pillarCapSize.x), Quaternion.identity, MatCap, false);
                PillarCount++;
                innerEdges.Add(EdgeKey(x, y, x + 1, y));
                innerEdges.Add(EdgeKey(x, y + 1, x + 1, y + 1));
                innerEdges.Add(EdgeKey(x, y, x, y + 1));
                innerEdges.Add(EdgeKey(x + 1, y, x + 1, y + 1));
                ClaimPillar(x, y, centre);
                ClaimPillar(x + 1, y, centre);
                ClaimPillar(x, y + 1, centre);
                ClaimPillar(x + 1, y + 1, centre);
            }
        }

        // Links: east + north always; diagonals only when neither shared straight cell is a wall.
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (!IsWall(x, y))
                {
                    continue;
                }
                if (IsWall(x + 1, y))
                {
                    AddSegment(x, y, x + 1, y);
                }
                if (IsWall(x, y + 1))
                {
                    AddSegment(x, y, x, y + 1);
                }
                if (IsWall(x + 1, y + 1) && !IsWall(x + 1, y) && !IsWall(x, y + 1))
                {
                    AddSegment(x, y, x + 1, y + 1);
                }
                if (IsWall(x - 1, y + 1) && !IsWall(x - 1, y) && !IsWall(x, y + 1))
                {
                    AddSegment(x, y, x - 1, y + 1);
                }
            }
        }

        // Posts and crates per cell.
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int i = y * gridWidth + x;
                if (!walls[i] || inPillar[i])
                {
                    continue;
                }
                Vector3 c = anchors[i];
                int links = degree[i];
                if (links == 0)
                {
                    int hash = (x * 73 + y * 151) % 7;
                    AddCrate(c, crateSize, hash * 13f);
                    if (hash % 2 == 0)
                    {
                        AddCrate(c + Vector3.up * crateSize, smallCrateSize, hash * 13f + 25f);
                    }
                    CrateCount++;
                    continue;
                }
                bool straight = links == 2 && Vector3.Dot(firstDir[i], secondDir[i]) < -0.99f;
                if (!straight)
                {
                    AddBox(c + Vector3.up * (jointPostSize.y * 0.5f), jointPostSize, Quaternion.identity, MatPost, false);
                    AddBox(c + Vector3.up * (jointPostSize.y + jointCapSize.y * 0.5f - CapSink),
                        new Vector3(jointCapSize.x, jointCapSize.y, jointCapSize.x), Quaternion.identity, MatCap, false);
                    JointPostCount++;
                }
                else if ((x + y) % 2 == 0)
                {
                    AddBox(c + Vector3.up * (plainPostSize.y * 0.5f), plainPostSize,
                        Quaternion.LookRotation(firstDir[i], Vector3.up), MatRail, false);
                }
            }
        }
    }

    private void ClaimPillar(int x, int y, Vector3 centre)
    {
        int i = y * gridWidth + x;
        if (!inPillar[i])
        {
            anchors[i] = centre;
        }
        inPillar[i] = true;
    }

    // One fence segment between two cells' anchors: 2 rails on one side, boards on the other.
    private void AddSegment(int x0, int y0, int x1, int y1)
    {
        if (innerEdges.Contains(EdgeKey(x0, y0, x1, y1)))
        {
            return;
        }
        int i0 = y0 * gridWidth + x0;
        int i1 = y1 * gridWidth + x1;
        Vector3 a = anchors[i0];
        Vector3 b = anchors[i1];
        Vector3 d = b - a;
        float length = d.magnitude;
        if (length < 0.05f)
        {
            return;
        }
        Vector3 dir = d / length;
        Quaternion turn = Quaternion.LookRotation(dir, Vector3.up);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 mid = (a + b) * 0.5f;

        Vector3 railScale = new Vector3(railSize.x, railSize.y, length);
        AddBox(mid + Vector3.up * lowRailHeight + side * railOffset, railScale, turn, MatRail, false);
        AddBox(mid + Vector3.up * highRailHeight + side * railOffset, railScale, turn, MatRail, false);

        int boards = Mathf.Max(1, Mathf.RoundToInt(length / boardSpacing));
        float width = boardWidth * (length / boards) / boardSpacing;
        for (int k = 0; k < boards; k++)
        {
            Vector3 p = Vector3.Lerp(a, b, (k + 0.5f) / boards);
            int hash = Mathf.Abs(Mathf.RoundToInt(p.x * 10f) * 7 + Mathf.RoundToInt(p.z * 10f) * 13);
            float height = (hash % 2 == 0 ? tallBoardHeight : shortBoardHeight) + ((hash % 5) - 2) * boardHeightJitter;
            Quaternion tilt = turn * Quaternion.Euler(((hash % 3) - 1) * boardTilt, 0f, 0f);
            AddBox(p + Vector3.up * (height * 0.5f) - side * boardOffset, new Vector3(boardThickness, height, width), tilt,
                hash % darkBoardEvery == 0 ? MatPlankDark : MatPlank, false);
        }

        SegmentCount++;
        AddLink(i0, dir);
        AddLink(i1, -dir);
    }

    private void AddLink(int cell, Vector3 dir)
    {
        if (degree[cell] == 0)
        {
            firstDir[cell] = dir;
        }
        else if (degree[cell] == 1)
        {
            secondDir[cell] = dir;
        }
        degree[cell]++;
    }

    // A crate: body, darker corner edges, a top and bottom frame and a cross brace on two faces.
    private void AddCrate(Vector3 basePos, float size, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 c = basePos + Vector3.up * (size * 0.5f);
        AddBox(c, new Vector3(size, size, size), q, MatCrate, true);
        float e = size * 0.5f - 0.05f;
        float edge = crateEdgeWidth;
        float frame = crateFrameThickness;
        for (int k = 0; k < 4; k++)
        {
            float sx = (k & 1) == 0 ? 1f : -1f;
            float sz = (k & 2) == 0 ? 1f : -1f;
            AddBox(c + q * new Vector3(sx * e, 0f, sz * e), new Vector3(edge, size + 0.02f, edge), q, MatCrateFrame, true);
        }
        for (int k = 0; k < 2; k++)
        {
            float yy = k == 0 ? -size * 0.5f + 0.05f : size * 0.5f - 0.05f;
            AddBox(c + q * new Vector3(0f, yy, e), new Vector3(size, frame, edge), q, MatCrateFrame, true);
            AddBox(c + q * new Vector3(0f, yy, -e), new Vector3(size, frame, edge), q, MatCrateFrame, true);
            AddBox(c + q * new Vector3(e, yy, 0f), new Vector3(edge, frame, size), q, MatCrateFrame, true);
            AddBox(c + q * new Vector3(-e, yy, 0f), new Vector3(edge, frame, size), q, MatCrateFrame, true);
        }
        AddBox(c + q * new Vector3(0f, 0f, size * 0.5f + 0.005f), new Vector3(size * 1.25f, frame, 0.02f),
            q * Quaternion.Euler(0f, 0f, 45f), MatCrateFrame, true);
        AddBox(c + q * new Vector3(size * 0.5f + 0.005f, 0f, 0f), new Vector3(0.02f, frame, size * 1.25f),
            q * Quaternion.Euler(45f, 0f, 0f), MatCrateFrame, true);
    }

    private static readonly Vector3[] FaceNormals =
    {
        Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.down
    };

    // A box with flat-shaded faces; the bottom face is left out unless asked for (never seen from the game camera).
    private void AddBox(Vector3 centre, Vector3 size, Quaternion rotation, int material, bool bottom)
    {
        Vector3 h = size * 0.5f;
        int faces = bottom ? 6 : 5;
        List<int> tris = indices[material];
        for (int f = 0; f < faces; f++)
        {
            Vector3 n = FaceNormals[f];
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(n, u);
            int start = vertices.Count;
            for (int k = 0; k < 4; k++)
            {
                float su = (k == 0 || k == 3) ? -1f : 1f;
                float sv = k < 2 ? -1f : 1f;
                Vector3 local = n + u * su + v * sv;
                Vector3 world = centre + rotation * Vector3.Scale(local, h);
                vertices.Add(toLocal.MultiplyPoint3x4(world));
                normals.Add(toLocal.MultiplyVector(rotation * n).normalized);
            }
            // Clockwise seen from outside (Unity's front face).
            tris.Add(start);
            tris.Add(start + 1);
            tris.Add(start + 2);
            tris.Add(start);
            tris.Add(start + 2);
            tris.Add(start + 3);
        }
    }
}
