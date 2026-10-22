/*
 * BaseLayout
 * Builds the base's walls from WallChunk presets: a hand-set list of (chunk, slot) placements (the slice-3 draft will
 * add to it later). Lives on the BasePoint object and writes into the BuildGrid.
 *
 * - Slots are ModuleSize-cell squares on a chunk grid centred on the base: slot (0,0) is the core slot around the base,
 *   +x east, +y north. A chunk bigger than one module also covers the slots east/north of its own slot.
 * - Applying writes every chunk cell into the grid (Path, Wall and BuildStrip as painted; Empty and painted Base become
 *   Blocked), never over the grid's own Base cells, then raises one grid Changed (one flow-field rebuild).
 * - Checks, logged as warnings (the layout still builds): chunk painting problems, chunks overlapping each other or
 *   sticking out of the grid, touching doors that don't match (open against closed), no entrance (no open door facing
 *   empty space), and entrances whose path doesn't lead to the base through the layout (a dead end).
 * - showBlocks covers the Wall cells with as few boxes as possible (greedy rectangles; wallHeight, material) under a
 *   runtime child. Each box keeps its BoxCollider and sits on wallLayer ("Wall"): it blocks the player, player bullets
 *   ignore that layer, enemies are kept out by the grid instead.
 * - In Play, editing the list, painting a placed chunk or rebuilding the grid re-applies the layout on the next frame
 *   (live preview). Selecting the object shows the slot guide (every slot that fits in the grid, labelled with its
 *   coordinates) and the placed chunks as coloured gizmos in the Scene view, also outside Play.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

// Runs before default scripts so the walls are in the grid before spawners place trees/enemies on Start.
[DefaultExecutionOrder(-50)]
public class BaseLayout : MonoBehaviour
{
    [Serializable]
    public class ChunkPlacement
    {
        public WallChunk chunk;
        [Tooltip("Slot on the chunk grid: (0,0) = core slot around the base, +x east, +y north.")]
        public Vector2Int slot;
    }

    private static readonly ChunkSide[] Sides = { ChunkSide.North, ChunkSide.East, ChunkSide.South, ChunkSide.West };

    [SerializeField] private BuildGrid grid;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private List<ChunkPlacement> placements = new List<ChunkPlacement>();

    [Header("Wall blocks")]
    [SerializeField] private bool showBlocks = true;
    [SerializeField] private Material material;
    [SerializeField, Min(0.1f)] private float wallHeight = 1.5f;
    [Tooltip("Layer of the wall blocks: collides with the player, skipped by player bullets.")]
    [SerializeField] private string wallLayer = "Wall";

    private readonly List<Vector2Int> written = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> writtenSet = new HashSet<Vector2Int>();
    private readonly List<string> warnings = new List<string>();
    private readonly Dictionary<Vector2Int, int> occupied = new Dictionary<Vector2Int, int>();
    private Transform blocks;
    private bool applied;
    private bool reapply;
    private int lastVersion;

    public IReadOnlyList<string> Warnings => warnings;
    public List<ChunkPlacement> Placements => placements;

    private void Start()
    {
        if (grid == null)
        {
            grid = BuildGrid.Instance;
        }
        if (grid != null)
        {
            grid.Rebuilt += OnGridRebuilt;
        }
        if (applyOnStart)
        {
            Apply();
        }
    }

    private void OnDestroy()
    {
        if (grid != null)
        {
            grid.Rebuilt -= OnGridRebuilt;
        }
    }

    // The grid was rebuilt empty (a size change in Play): its cells are gone, so write the layout again.
    private void OnGridRebuilt()
    {
        written.Clear();
        writtenSet.Clear();
        if (applied)
        {
            reapply = true;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && applied)
        {
            reapply = true;
        }
    }

    private void Update()
    {
        if (applied && VersionSum() != lastVersion)
        {
            reapply = true;
        }
        if (reapply)
        {
            reapply = false;
            Apply();
        }
    }

    /// <summary>Grid cell of a slot's south-west corner.</summary>
    public Vector2Int SlotOrigin(Vector2Int slot)
    {
        int size = WallChunk.ModuleSize;
        return new Vector2Int(grid.Width / 2 - size / 2 + slot.x * size, grid.Height / 2 - size / 2 + slot.y * size);
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        if (grid == null)
        {
            return;
        }

        ClearCells();
        warnings.Clear();
        occupied.Clear();

        for (int i = 0; i < placements.Count; i++)
        {
            ChunkPlacement placement = placements[i];
            if (placement == null || placement.chunk == null)
            {
                continue;
            }
            WallChunk chunk = placement.chunk;

            foreach (string problem in chunk.GetProblems())
            {
                warnings.Add(chunk.name + ": " + problem);
            }

            if (!Occupy(i, placement))
            {
                continue;
            }

            int outside = 0;
            Vector2Int origin = SlotOrigin(placement.slot);
            for (int y = 0; y < chunk.Height; y++)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    Vector2Int cell = origin + new Vector2Int(x, y);
                    if (!grid.InBounds(cell))
                    {
                        outside++;
                        continue;
                    }
                    if (grid.GetState(cell) == CellState.Base)
                    {
                        continue;
                    }
                    grid.SetState(cell, ToGridState(chunk.GetCell(x, y)), false);
                    written.Add(cell);
                    writtenSet.Add(cell);
                }
            }
            if (outside > 0)
            {
                warnings.Add(chunk.name + " at slot " + placement.slot + ": " + outside + " cells are outside the grid.");
            }
        }

        applied = true;
        lastVersion = VersionSum();
        grid.NotifyChanged();
        CheckDoors();
        RebuildBlocks();

        foreach (string warning in warnings)
        {
            Debug.LogWarning("BaseLayout: " + warning, this);
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        ClearCells();
        occupied.Clear();
        applied = false;
        if (grid != null)
        {
            grid.NotifyChanged();
        }
        RebuildBlocks();
    }

    private static CellState ToGridState(ChunkCell cell)
    {
        switch (cell)
        {
            case ChunkCell.Path: return CellState.Path;
            case ChunkCell.Wall: return CellState.Wall;
            case ChunkCell.BuildStrip: return CellState.BuildStrip;
            default: return CellState.Blocked;
        }
    }

    // Marks the placement's module slots as taken; refuses (with a warning) if any is already taken.
    private bool Occupy(int index, ChunkPlacement placement)
    {
        WallChunk chunk = placement.chunk;
        for (int my = 0; my < chunk.ModulesY; my++)
        {
            for (int mx = 0; mx < chunk.ModulesX; mx++)
            {
                Vector2Int slot = placement.slot + new Vector2Int(mx, my);
                int other;
                if (occupied.TryGetValue(slot, out other))
                {
                    warnings.Add(chunk.name + " at slot " + placement.slot + " overlaps " + placements[other].chunk.name +
                                 " (slot " + slot + "); it was skipped.");
                    return false;
                }
            }
        }

        for (int my = 0; my < chunk.ModulesY; my++)
        {
            for (int mx = 0; mx < chunk.ModulesX; mx++)
            {
                occupied[placement.slot + new Vector2Int(mx, my)] = index;
            }
        }
        return true;
    }

    // Matches every door against the chunk it touches; open doors facing empty slots are entrances and must reach the base.
    private void CheckDoors()
    {
        int entrances = 0;

        foreach (KeyValuePair<Vector2Int, int> pair in occupied)
        {
            int index = pair.Value;
            ChunkPlacement placement = placements[index];
            WallChunk chunk = placement.chunk;
            Vector2Int local = pair.Key - placement.slot;

            foreach (ChunkSide side in Sides)
            {
                int door;
                if (!DoorOnModule(chunk, side, local, out door))
                {
                    continue;
                }

                bool open = chunk.IsDoorOpen(side, door);
                Vector2Int neighbourSlot = pair.Key + Step(side);
                int other;
                if (occupied.TryGetValue(neighbourSlot, out other))
                {
                    if (other == index || other < index)
                    {
                        continue;
                    }
                    ChunkPlacement otherPlacement = placements[other];
                    ChunkSide facing = Opposite(side);
                    int otherDoor;
                    DoorOnModule(otherPlacement.chunk, facing, neighbourSlot - otherPlacement.slot, out otherDoor);
                    bool otherOpen = otherPlacement.chunk.IsDoorOpen(facing, otherDoor);
                    if (open != otherOpen)
                    {
                        warnings.Add("Door mismatch: " + chunk.name + " " + side + " door is " + (open ? "open" : "closed") +
                                     " but " + otherPlacement.chunk.name + " " + facing + " door is " +
                                     (otherOpen ? "open" : "closed") + " (slots " + pair.Key + " / " + neighbourSlot + ").");
                    }
                }
                else if (open)
                {
                    entrances++;
                    if (!DoorReachesBase(placement, side, door))
                    {
                        warnings.Add("Entrance " + chunk.name + " " + side + " door (slot " + pair.Key +
                                     ") leads to a dead end (its path never reaches the base).");
                    }
                }
            }
        }

        if (occupied.Count > 0 && entrances == 0)
        {
            warnings.Add("No entrance: no open door faces empty space, so enemies can't get in.");
        }
    }

    // The door index of `side` on the chunk's module at `local`, if that module lies on that side.
    private static bool DoorOnModule(WallChunk chunk, ChunkSide side, Vector2Int local, out int door)
    {
        switch (side)
        {
            case ChunkSide.North: door = local.x; return local.y == chunk.ModulesY - 1;
            case ChunkSide.South: door = local.x; return local.y == 0;
            case ChunkSide.East: door = local.y; return local.x == chunk.ModulesX - 1;
            default: door = local.y; return local.x == 0;
        }
    }

    // Walks from the door's cells through the layout only (never back outside), so a door that leads into a dead end
    // counts as unreachable even though enemies could walk around the outside to another door.
    private bool DoorReachesBase(ChunkPlacement placement, ChunkSide side, int door)
    {
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        Vector2Int origin = SlotOrigin(placement.slot);
        for (int i = 0; i < WallChunk.DoorWidth; i++)
        {
            Vector2Int cell = origin + placement.chunk.EdgeCell(side, door * WallChunk.ModuleSize + WallChunk.DoorStart + i);
            if (seen.Add(cell))
            {
                open.Enqueue(cell);
            }
        }

        while (open.Count > 0)
        {
            Vector2Int cell = open.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                Vector2Int next = cell + Neighbour4[k];
                if (!seen.Add(next))
                {
                    continue;
                }
                CellState state = grid.GetState(next);
                if (state == CellState.Base)
                {
                    return true;
                }
                if (state == CellState.Path && writtenSet.Contains(next))
                {
                    open.Enqueue(next);
                }
            }
        }
        return false;
    }

    private static readonly Vector2Int[] Neighbour4 =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private static Vector2Int Step(ChunkSide side)
    {
        switch (side)
        {
            case ChunkSide.North: return Vector2Int.up;
            case ChunkSide.South: return Vector2Int.down;
            case ChunkSide.East: return Vector2Int.right;
            default: return Vector2Int.left;
        }
    }

    private static ChunkSide Opposite(ChunkSide side)
    {
        switch (side)
        {
            case ChunkSide.North: return ChunkSide.South;
            case ChunkSide.South: return ChunkSide.North;
            case ChunkSide.East: return ChunkSide.West;
            default: return ChunkSide.East;
        }
    }

    private int VersionSum()
    {
        int sum = 0;
        foreach (ChunkPlacement placement in placements)
        {
            if (placement != null && placement.chunk != null)
            {
                sum = sum * 31 + placement.chunk.Version;
            }
        }
        return sum;
    }

    private void ClearCells()
    {
        if (grid != null)
        {
            foreach (Vector2Int cell in written)
            {
                if (grid.GetState(cell) != CellState.Base)
                {
                    grid.SetState(cell, CellState.Free, false);
                }
            }
        }
        written.Clear();
        writtenSet.Clear();
    }

    // Covers the Wall cells with as few boxes as possible (greedy rectangles), each a cube with a BoxCollider on the
    // wall layer: blocks the player, ignored by player bullets (Projectile skips the layer).
    private void RebuildBlocks()
    {
        if (blocks != null)
        {
            Destroy(blocks.gameObject);
            blocks = null;
        }
        if (!showBlocks || !Application.isPlaying || written.Count == 0)
        {
            return;
        }

        blocks = new GameObject("WallBlocks").transform;
        blocks.SetParent(transform, false);
        int layer = LayerMask.NameToLayer(wallLayer);

        HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
        Vector2Int min = written[0];
        Vector2Int max = written[0];
        foreach (Vector2Int cell in written)
        {
            if (grid.GetState(cell) == CellState.Wall)
            {
                walls.Add(cell);
                min = Vector2Int.Min(min, cell);
                max = Vector2Int.Max(max, cell);
            }
        }

        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2Int start = new Vector2Int(x, y);
                if (!walls.Contains(start) || used.Contains(start))
                {
                    continue;
                }

                int width = 1;
                while (IsFreeWall(walls, used, new Vector2Int(x + width, y)))
                {
                    width++;
                }
                int height = 1;
                while (RowIsFreeWall(walls, used, x, y + height, width))
                {
                    height++;
                }
                for (int dy = 0; dy < height; dy++)
                {
                    for (int dx = 0; dx < width; dx++)
                    {
                        used.Add(new Vector2Int(x + dx, y + dy));
                    }
                }
                CreateBlock(start, width, height, layer);
            }
        }
    }

    private static bool IsFreeWall(HashSet<Vector2Int> walls, HashSet<Vector2Int> used, Vector2Int cell)
    {
        return walls.Contains(cell) && !used.Contains(cell);
    }

    private static bool RowIsFreeWall(HashSet<Vector2Int> walls, HashSet<Vector2Int> used, int x, int y, int width)
    {
        for (int dx = 0; dx < width; dx++)
        {
            if (!IsFreeWall(walls, used, new Vector2Int(x + dx, y)))
            {
                return false;
            }
        }
        return true;
    }

    private void CreateBlock(Vector2Int start, int width, int height, int layer)
    {
        float size = grid.CellSize;
        Vector3 corner = CellCorner(start);
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Wall " + start.x + "," + start.y + " (" + width + "x" + height + ")";
        if (layer >= 0)
        {
            block.layer = layer;
        }
        block.transform.SetParent(blocks, false);
        block.transform.position = corner + new Vector3(width * size * 0.5f, wallHeight * 0.5f, height * size * 0.5f);
        block.transform.localScale = new Vector3(width * size, wallHeight, height * size);
        if (material != null)
        {
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    private void OnDrawGizmos()
    {
        if (grid == null)
        {
            return;
        }
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        foreach (ChunkPlacement placement in placements)
        {
            if (placement == null || placement.chunk == null)
            {
                continue;
            }
            Vector3 min = CellCorner(SlotOrigin(placement.slot));
            Vector3 size = new Vector3(placement.chunk.Width, 0f, placement.chunk.Height) * grid.CellSize;
            Gizmos.DrawWireCube(min + size * 0.5f + Vector3.up * 0.05f, size);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (grid == null)
        {
            return;
        }
        DrawSlotGuide();
        float cell = grid.CellSize;
        foreach (ChunkPlacement placement in placements)
        {
            if (placement == null || placement.chunk == null)
            {
                continue;
            }
            Vector2Int origin = SlotOrigin(placement.slot);
            for (int y = 0; y < placement.chunk.Height; y++)
            {
                for (int x = 0; x < placement.chunk.Width; x++)
                {
                    ChunkCell value = placement.chunk.GetCell(x, y);
                    if (value == ChunkCell.Empty)
                    {
                        continue;
                    }
                    Gizmos.color = GizmoColor(value);
                    Vector3 center = CellCorner(origin + new Vector2Int(x, y)) + new Vector3(cell, 0f, cell) * 0.5f;
                    float height = value == ChunkCell.Wall ? wallHeight : 0.05f;
                    Gizmos.DrawCube(center + Vector3.up * height * 0.5f, new Vector3(cell * 0.9f, height, cell * 0.9f));
                }
            }
        }
    }

    // Every slot that fits fully inside the grid, outlined and labelled with its coordinates (Scene view, selected).
    private void DrawSlotGuide()
    {
        int size = WallChunk.ModuleSize;
        int reachX = (grid.Width / 2 - size / 2) / size;
        int reachY = (grid.Height / 2 - size / 2) / size;
        Gizmos.color = new Color(1f, 1f, 0.4f, 0.35f);
        for (int sy = -reachY; sy <= reachY; sy++)
        {
            for (int sx = -reachX; sx <= reachX; sx++)
            {
                Vector2Int slot = new Vector2Int(sx, sy);
                Vector3 min = CellCorner(SlotOrigin(slot));
                Vector3 extent = new Vector3(size, 0f, size) * grid.CellSize;
                Gizmos.DrawWireCube(min + extent * 0.5f, extent * 0.98f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(min + extent * 0.5f, "(" + sx + ", " + sy + ")");
#endif
            }
        }
    }

    private Vector3 CellCorner(Vector2Int cell)
    {
        Vector3 origin = grid.Origin;
        return new Vector3(origin.x + cell.x * grid.CellSize, 0f, origin.z + cell.y * grid.CellSize);
    }

    private static Color GizmoColor(ChunkCell value)
    {
        switch (value)
        {
            case ChunkCell.Path: return new Color(0.3f, 0.85f, 1f, 0.5f);
            case ChunkCell.Wall: return new Color(0.6f, 0.6f, 0.6f, 0.8f);
            case ChunkCell.BuildStrip: return new Color(0.3f, 0.45f, 1f, 0.6f);
            default: return new Color(1f, 0.6f, 0.1f, 0.6f);
        }
    }
}
