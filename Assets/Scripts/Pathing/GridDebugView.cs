/*
 * GridDebugView
 * Debug overlay for BuildGrid (+ FlowField): press toggleKey (R) in Play mode to show/hide it on the ground
 * (Game view); press distanceKey (T) to toggle distance shading while it is shown.
 *
 * - Draws the cell lines (lineWidth thick) plus a tinted square on every cell that is not Free (Base, Path, Wall,
 *   BuildStrip, Blocked each have a colour). Free cells only show the lines.
 * - With a FlowField: an arrow on every cell that has a route, pointing to the next cell toward the base
 *   (showArrows). Distance shading tints reachable cells from nearColor (next to the base) to farColor (the
 *   farthest reachable cell). Walkable cells with no route get no arrow.
 * - Put it on the same object as the BuildGrid / FlowField (or assign them). It builds its own child mesh in world
 *   space, lifted groundHeight above the ground, and rebuilds it only when the grid or field changes, a toggle or a
 *   setting here changes, and only while visible.
 * - material must be unlit and use vertex colours (Materials/GridDebug.mat, Sprites/Default).
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridDebugView : MonoBehaviour
{
    [SerializeField] private BuildGrid grid;
    [SerializeField] private FlowField flowField;
    [SerializeField] private Material material;
    [SerializeField] private Key toggleKey = Key.R;
    [SerializeField] private Key distanceKey = Key.T;
    [SerializeField] private bool visibleOnStart;
    [SerializeField] private bool showArrows = true;
    [SerializeField] private bool showDistance;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.04f;
    [SerializeField] private float groundHeight = 0.03f;
    [Tooltip("Gap left around each tinted cell so neighbouring cells stay readable.")]
    [SerializeField, Range(0f, 0.45f)] private float cellInset = 0.06f;

    [Header("Arrows (fraction of a cell)")]
    [SerializeField, Range(0.1f, 1f)] private float arrowLength = 0.6f;
    [SerializeField, Range(0.01f, 0.3f)] private float arrowWidth = 0.06f;
    [SerializeField, Range(0.05f, 0.5f)] private float arrowHeadSize = 0.22f;

    [Header("Colours")]
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color baseColor = new Color(1f, 0.6f, 0.1f, 0.5f);
    [SerializeField] private Color pathColor = new Color(0.3f, 0.85f, 1f, 0.3f);
    [SerializeField] private Color wallColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
    [SerializeField] private Color buildStripColor = new Color(0.3f, 0.45f, 1f, 0.45f);
    [SerializeField] private Color blockedColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color arrowColor = new Color(1f, 0.95f, 0.55f, 0.85f);
    [SerializeField] private Color nearColor = new Color(1f, 0.85f, 0.3f, 0.45f);
    [SerializeField] private Color farColor = new Color(0.15f, 0.2f, 0.6f, 0.45f);

    private GameObject view;
    private Mesh mesh;
    private bool dirty = true;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<int> triangles = new List<int>();

    private void Awake()
    {
        if (grid == null)
        {
            grid = GetComponent<BuildGrid>();
        }
        if (flowField == null)
        {
            flowField = GetComponent<FlowField>();
        }

        mesh = new Mesh { name = "GridDebugView", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic();

        view = new GameObject("GridDebugMesh");
        view.transform.SetParent(transform, false);
        view.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer meshRenderer = view.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        view.SetActive(visibleOnStart);
    }

    private void OnEnable()
    {
        if (grid != null)
        {
            grid.Changed += MarkDirty;
        }
        if (flowField != null)
        {
            flowField.Rebuilt += MarkDirty;
        }
        dirty = true;
    }

    private void OnDisable()
    {
        if (grid != null)
        {
            grid.Changed -= MarkDirty;
        }
        if (flowField != null)
        {
            flowField.Rebuilt -= MarkDirty;
        }
    }

    private void OnValidate()
    {
        dirty = true;
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    private void MarkDirty()
    {
        dirty = true;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                view.SetActive(!view.activeSelf);
            }
            if (view.activeSelf && keyboard[distanceKey].wasPressedThisFrame)
            {
                showDistance = !showDistance;
                dirty = true;
            }
        }

        if (view.activeSelf && dirty && grid != null)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        dirty = false;
        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        // Vertices are in world space, so the child mesh object sits at the world origin.
        view.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Vector3 origin = grid.Origin;
        float size = grid.CellSize;
        float totalX = grid.Width * size;
        float totalZ = grid.Height * size;
        float half = lineWidth * 0.5f;
        float inset = cellInset * size;
        bool hasField = flowField != null;
        float maxDistance = hasField ? Mathf.Max(1, flowField.MaxDistance) : 1f;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                float minX = origin.x + x * size + inset;
                float minZ = origin.z + y * size + inset;
                float maxX = minX + size - inset * 2f;
                float maxZ = minZ + size - inset * 2f;

                if (showDistance && hasField)
                {
                    int distance = flowField.GetDistance(cell);
                    if (distance > 0 && distance != FlowField.Unreachable)
                    {
                        AddQuad(minX, minZ, maxX, maxZ, Color.Lerp(nearColor, farColor, distance / maxDistance));
                    }
                }

                Color color;
                if (TryGetCellColor(grid.GetState(cell), out color))
                {
                    AddQuad(minX, minZ, maxX, maxZ, color);
                }
            }
        }

        for (int x = 0; x <= grid.Width; x++)
        {
            float lineX = origin.x + x * size;
            AddQuad(lineX - half, origin.z - half, lineX + half, origin.z + totalZ + half, lineColor);
        }

        for (int y = 0; y <= grid.Height; y++)
        {
            float lineZ = origin.z + y * size;
            AddQuad(origin.x - half, lineZ - half, origin.x + totalX + half, lineZ + half, lineColor);
        }

        if (showArrows && hasField)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Vector3 direction = flowField.GetCellDirection(cell);
                    if (direction != Vector3.zero)
                    {
                        AddArrow(grid.CellToWorld(cell), direction, size);
                    }
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private bool TryGetCellColor(CellState state, out Color color)
    {
        switch (state)
        {
            case CellState.Base: color = baseColor; return true;
            case CellState.Path: color = pathColor; return true;
            case CellState.Wall: color = wallColor; return true;
            case CellState.BuildStrip: color = buildStripColor; return true;
            case CellState.Blocked: color = blockedColor; return true;
            default: color = Color.clear; return false;
        }
    }

    private void AddQuad(float minX, float minZ, float maxX, float maxZ, Color color)
    {
        AddQuad(new Vector3(minX, groundHeight, minZ), new Vector3(minX, groundHeight, maxZ),
            new Vector3(maxX, groundHeight, maxZ), new Vector3(maxX, groundHeight, minZ), color);
    }

    private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        for (int i = 0; i < 4; i++)
        {
            colors.Add(color);
        }

        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    // Shaft quad + head triangle centred on the cell, flat on the ground (the material has no culling).
    private void AddArrow(Vector3 center, Vector3 direction, float cellSize)
    {
        center.y = groundHeight;
        Vector3 side = new Vector3(-direction.z, 0f, direction.x);
        float length = arrowLength * cellSize;
        float head = Mathf.Min(arrowHeadSize * cellSize, length);
        float shaftHalf = arrowWidth * cellSize * 0.5f;

        Vector3 tail = center - direction * (length * 0.5f);
        Vector3 tip = center + direction * (length * 0.5f);
        Vector3 neck = tip - direction * head;

        AddQuad(tail - side * shaftHalf, tail + side * shaftHalf, neck + side * shaftHalf, neck - side * shaftHalf,
            arrowColor);

        int start = vertices.Count;
        vertices.Add(neck - side * head * 0.6f);
        vertices.Add(tip);
        vertices.Add(neck + side * head * 0.6f);
        colors.Add(arrowColor);
        colors.Add(arrowColor);
        colors.Add(arrowColor);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }
}
