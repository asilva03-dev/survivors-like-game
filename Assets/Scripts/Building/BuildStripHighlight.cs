/*
 * BuildStripHighlight
 * While build mode is open, tints every BuildStrip cell of the BuildGrid (like the R overlay's blue cells) so the
 * player sees where defenses can go.
 *
 * - One runtime mesh `BuildStripMesh` (world space, child of this object): an inset quad per strip cell in
 *   stripColor, vertex coloured, `material` = a vertex-colour transparent material (GridDebug.mat), no shadows.
 * - Strip tiles touched by a built turret's no-build circle (Turret.CircleTouches) are blockedColor (red): the
 *   player can't build there (the tile rule, slice 6). This replaces the old red spacing disc.
 * - Rebuilt when the grid changes (a chunk placed) or a turret is placed/removed; shown only while BuildMode is open.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BuildStripHighlight : MonoBehaviour
{
    [SerializeField] private BuildGrid grid;
    [SerializeField] private Material material;
    [SerializeField] private Color stripColor = new Color(0.3f, 0.55f, 1f, 0.45f);
    [Tooltip("Strip tiles inside a built turret's no-build area.")]
    [SerializeField] private Color blockedColor = new Color(1f, 0.2f, 0.2f, 0.55f);
    [SerializeField, Range(0f, 0.45f)] private float cellInset = 0.06f;
    [SerializeField, Min(0f)] private float height = 0.035f;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<int> triangles = new List<int>();
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private bool dirty = true;

    public int StripCellCount { get; private set; }
    public int BlockedCellCount { get; private set; }
    public bool Visible => meshRenderer != null && meshRenderer.enabled;

    private void Awake()
    {
        if (grid == null)
        {
            grid = FindAnyObjectByType<BuildGrid>();
        }

        GameObject go = new GameObject("BuildStripMesh");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        mesh = new Mesh { name = "BuildStrips", indexFormat = IndexFormat.UInt32 };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material != null ? material : new Material(Shader.Find("Sprites/Default"));
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = false;
    }

    private void OnEnable()
    {
        if (grid != null)
        {
            grid.Changed += MarkDirty;
        }
        Turret.ActiveChanged += MarkDirty;
    }

    private void OnDisable()
    {
        if (grid != null)
        {
            grid.Changed -= MarkDirty;
        }
        Turret.ActiveChanged -= MarkDirty;
    }

    private void LateUpdate()
    {
        bool show = BuildMode.IsActive && grid != null;
        if (show && dirty)
        {
            Rebuild();
        }

        // The mesh object stays at the world origin even if this object moves.
        meshRenderer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        meshRenderer.enabled = show;
    }

    public void MarkDirty()
    {
        dirty = true;
    }

    // Red when any built turret's no-build circle touches the tile, else the normal strip colour.
    public bool IsBlocked(Vector2Int cell)
    {
        for (int i = 0; i < Turret.Active.Count; i++)
        {
            if (Turret.Active[i].CircleTouches(cell))
            {
                return true;
            }
        }
        return false;
    }

    private Color CellColor(Vector2Int cell)
    {
        if (IsBlocked(cell))
        {
            BlockedCellCount++;
            return blockedColor;
        }
        return stripColor;
    }

    private void Rebuild()
    {
        dirty = false;
        vertices.Clear();
        colors.Clear();
        triangles.Clear();
        StripCellCount = 0;
        BlockedCellCount = 0;

        float size = grid.CellSize;
        float inset = cellInset * size;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (grid.GetState(cell) != CellState.BuildStrip)
                {
                    continue;
                }

                StripCellCount++;
                Vector3 center = grid.CellToWorld(cell);
                float half = size * 0.5f - inset;
                int start = vertices.Count;
                vertices.Add(new Vector3(center.x - half, height, center.z - half));
                vertices.Add(new Vector3(center.x - half, height, center.z + half));
                vertices.Add(new Vector3(center.x + half, height, center.z + half));
                vertices.Add(new Vector3(center.x + half, height, center.z - half));
                Color color = CellColor(cell);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }
}
