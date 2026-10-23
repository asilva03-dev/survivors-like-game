/*
 * BaseLayout
 * Builds the base's walls from WallChunk presets placed on a chunk grid of slots, and writes them into the BuildGrid.
 * Lives on the BasePoint object. The preset draft (PresetDraft + ChunkPlacer) adds chunks at runtime with Place().
 *
 * - Slots are ModuleSize-cell squares on a chunk grid centred on the base: slot (0,0) is the core slot around the base,
 *   +x east, +y north. A placement = chunk + slot + rotation (quarter turns clockwise); a chunk bigger than one module
 *   (after turning) also covers the slots east/north of its own slot. Only slots within maxSlotReach of the core are
 *   usable, so an open margin is left around the layout for enemies to walk.
 * - The hand-set `placements` list is a test layout, used only when useTestLayout is on; otherwise a run starts empty.
 * - Applying writes every chunk cell into the grid (Path, Wall and BuildStrip as painted; Empty stays Free = normal
 *   walkable ground; painted Base becomes Blocked), never over the grid's own Base cells, marks them all as layout cells
 *   (keeps spawns off them), then raises one grid Changed (one flow-field rebuild). The placement route checks still
 *   only follow painted Path, so enemies walking through empty ground is extra, never required.
 * - Placement rules (CanPlace): the chunk's slots are free and in reach; a base chunk goes only in the core slot of an
 *   empty layout; any other chunk needs at least one open door against an open door of a placed chunk (other touching
 *   edges may be anything) and its path must reach the base through such a connection; every entrance (open door
 *   facing empty space) must lead to the base through the layout's paths (no dead ends), and at least one must remain.
 *   The same route check gives the warnings for the test layout.
 * - showBlocks covers the Wall cells with as few boxes as possible (greedy rectangles; wallHeight, material) under a
 *   runtime child. Each box keeps its BoxCollider and sits on wallLayer ("Wall"): it blocks the player, player bullets
 *   ignore that layer, enemies are kept out by the grid instead.
 * - In Play, editing the list, painting a placed chunk or rebuilding the grid re-applies the layout on the next frame
 *   (live preview). Selecting the object shows the slot guide and the placed chunks as gizmos in the Scene view.
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
        [Tooltip("Quarter turns clockwise (seen from above).")]
        [Range(0, 3)] public int rotation;
    }

    private static readonly ChunkSide[] Sides = { ChunkSide.North, ChunkSide.East, ChunkSide.South, ChunkSide.West };

    [SerializeField] private BuildGrid grid;
    [Tooltip("Build the hand-set placements below on Start (testing). Off = a run starts with an empty layout.")]
    [SerializeField] private bool useTestLayout;
    [SerializeField] private List<ChunkPlacement> placements = new List<ChunkPlacement>();
    [Tooltip("Slots up to this many steps from the core (each way) can hold chunks.")]
    [SerializeField, Min(0)] private int maxSlotReach = 2;

    [Header("Wall blocks")]
    [SerializeField] private bool showBlocks = true;
    [SerializeField] private Material material;
    [SerializeField, Min(0.1f)] private float wallHeight = 1.5f;
    [Tooltip("Layer of the wall blocks: collides with the player, skipped by player bullets.")]
    [SerializeField] private string wallLayer = "Wall";

    private readonly List<Vector2Int> written = new List<Vector2Int>();
    private readonly List<string> warnings = new List<string>();
    private readonly Dictionary<Vector2Int, int> occupied = new Dictionary<Vector2Int, int>();
    private readonly List<Vector2Int> baseCells = new List<Vector2Int>();
    private Transform blocks;
    private bool applied;
    private bool reapply;
    private int lastVersion;

    /// <summary>Raised after the layout was applied (a chunk placed, the list edited, the grid rebuilt).</summary>
    public event Action LayoutChanged;

    public IReadOnlyList<string> Warnings => warnings;
    public List<ChunkPlacement> Placements => placements;
    public int MaxSlotReach => maxSlotReach;
    public float WallHeight => wallHeight;
    public BuildGrid Grid => grid;
    public bool HasBaseChunk => placements.Exists(p => p != null && p.chunk != null && p.chunk.IsBaseChunk);

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
        if (!useTestLayout)
        {
            placements = new List<ChunkPlacement>();
        }
        Apply();
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
        baseCells.Clear();
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

    /// <summary>The slot containing a world position.</summary>
    public Vector2Int SlotAt(Vector3 world)
    {
        Vector2Int local = grid.WorldToCell(world) - SlotOrigin(Vector2Int.zero);
        int size = WallChunk.ModuleSize;
        return new Vector2Int(Mathf.FloorToInt(local.x / (float)size), Mathf.FloorToInt(local.y / (float)size));
    }

    /// <summary>World position of a grid cell's minimum corner, on the ground.</summary>
    public Vector3 CellCorner(Vector2Int cell)
    {
        Vector3 origin = grid.Origin;
        return new Vector3(origin.x + cell.x * grid.CellSize, 0f, origin.z + cell.y * grid.CellSize);
    }

    public bool InReach(Vector2Int slot)
    {
        return Mathf.Abs(slot.x) <= maxSlotReach && Mathf.Abs(slot.y) <= maxSlotReach;
    }

    /// <summary>The placed chunk covering a slot, or null.</summary>
    public ChunkPlacement PlacementAt(Vector2Int slot)
    {
        int index;
        return occupied.TryGetValue(slot, out index) ? placements[index] : null;
    }

    /// <summary>Adds a chunk and rebuilds the layout. Call CanPlace first; this does not check the rules.</summary>
    public void Place(WallChunk chunk, Vector2Int slot, int rotation)
    {
        placements.Add(new ChunkPlacement { chunk = chunk, slot = slot, rotation = rotation & 3 });
        Apply();
    }

    /// <summary>Whether the chunk may go at slot/rotation; `reason` says why not (empty when it may).</summary>
    public bool CanPlace(WallChunk chunk, Vector2Int slot, int rotation, out string reason)
    {
        reason = "";
        if (chunk == null || grid == null)
        {
            reason = "Nothing to place.";
            return false;
        }
        rotation &= 3;
        ChunkPlacement candidate = new ChunkPlacement { chunk = chunk, slot = slot, rotation = rotation };

        if (chunk.IsBaseChunk)
        {
            if (slot != Vector2Int.zero || occupied.Count > 0)
            {
                reason = "The base chunk goes in the middle of an empty layout.";
                return false;
            }
        }
        else if (!HasBaseChunk)
        {
            reason = "Place the base chunk first.";
            return false;
        }

        Dictionary<Vector2Int, ChunkPlacement> layout = CurrentSlots();
        for (int my = 0; my < chunk.RotatedModulesY(rotation); my++)
        {
            for (int mx = 0; mx < chunk.RotatedModulesX(rotation); mx++)
            {
                Vector2Int s = slot + new Vector2Int(mx, my);
                if (!InReach(s))
                {
                    reason = "Too far from the base.";
                    return false;
                }
                if (layout.ContainsKey(s))
                {
                    reason = "That spot is taken.";
                    return false;
                }
                layout[s] = candidate;
            }
        }

        RouteReport report = CheckRoutes(layout, candidate);
        if (!chunk.IsBaseChunk && report.connections == 0)
        {
            reason = "No open door meets an open door of your base.";
            return false;
        }
        if (!chunk.IsBaseChunk && !report.candidateReachesBase)
        {
            reason = "Its path doesn't lead to the base.";
            return false;
        }
        if (report.deadEnds > 0)
        {
            reason = "An entrance would lead to a dead end.";
            return false;
        }
        if (report.entrances == 0)
        {
            reason = "It would seal the base (no way in left).";
            return false;
        }
        return true;
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
            int rotation = placement.rotation & 3;

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
            for (int y = 0; y < chunk.RotatedHeight(rotation); y++)
            {
                for (int x = 0; x < chunk.RotatedWidth(rotation); x++)
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
                    grid.SetState(cell, ToGridState(chunk.GetCell(x, y, rotation)), false);
                    grid.SetLayoutCell(cell, true);
                    written.Add(cell);
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
        CheckLayout();
        RebuildBlocks();

        foreach (string warning in warnings)
        {
            Debug.LogWarning("BaseLayout: " + warning, this);
        }
        LayoutChanged?.Invoke();
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
        LayoutChanged?.Invoke();
    }

    private static CellState ToGridState(ChunkCell cell)
    {
        switch (cell)
        {
            case ChunkCell.Path: return CellState.Path;
            case ChunkCell.Wall: return CellState.Wall;
            case ChunkCell.BuildStrip: return CellState.BuildStrip;
            case ChunkCell.Base: return CellState.Blocked;
            default: return CellState.Free;
        }
    }

    // Marks the placement's module slots as taken; refuses (with a warning) if any is already taken.
    private bool Occupy(int index, ChunkPlacement placement)
    {
        WallChunk chunk = placement.chunk;
        int rotation = placement.rotation & 3;
        for (int my = 0; my < chunk.RotatedModulesY(rotation); my++)
        {
            for (int mx = 0; mx < chunk.RotatedModulesX(rotation); mx++)
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

        for (int my = 0; my < chunk.RotatedModulesY(rotation); my++)
        {
            for (int mx = 0; mx < chunk.RotatedModulesX(rotation); mx++)
            {
                occupied[placement.slot + new Vector2Int(mx, my)] = index;
            }
        }
        return true;
    }

    private Dictionary<Vector2Int, ChunkPlacement> CurrentSlots()
    {
        Dictionary<Vector2Int, ChunkPlacement> slots = new Dictionary<Vector2Int, ChunkPlacement>();
        foreach (KeyValuePair<Vector2Int, int> pair in occupied)
        {
            slots[pair.Key] = placements[pair.Value];
        }
        return slots;
    }

    // Warnings for the applied layout (test layouts are not checked when they are set up by hand).
    private void CheckLayout()
    {
        if (occupied.Count == 0)
        {
            return;
        }
        RouteReport report = CheckRoutes(CurrentSlots(), null);
        foreach (string deadEnd in report.deadEndNames)
        {
            warnings.Add("Entrance " + deadEnd + " leads to a dead end (its path never reaches the base).");
        }
        if (report.entrances == 0)
        {
            warnings.Add("No entrance: no open door faces empty space, so enemies can't get in.");
        }
    }

    private class RouteReport
    {
        public int entrances;
        public int deadEnds;
        public int connections;
        public bool candidateReachesBase;
        public readonly List<string> deadEndNames = new List<string>();
    }

    // Floods the layout's Path cells from the base cells (never through open ground outside, so a door leading into a
    // dead end counts as unreachable even though enemies could walk around to another door), then reads every door:
    // open against open = a connection, open facing empty space = an entrance that must have been reached.
    private RouteReport CheckRoutes(Dictionary<Vector2Int, ChunkPlacement> layout, ChunkPlacement candidate)
    {
        RouteReport report = new RouteReport();
        HashSet<Vector2Int> reached = FloodFromBase(layout);

        foreach (KeyValuePair<Vector2Int, ChunkPlacement> pair in layout)
        {
            ChunkPlacement placement = pair.Value;
            Vector2Int local = pair.Key - placement.slot;
            foreach (ChunkSide side in Sides)
            {
                int door;
                if (!DoorOnModule(placement, side, local, out door) ||
                    !placement.chunk.IsDoorOpen(side, door, placement.rotation & 3))
                {
                    continue;
                }

                ChunkPlacement neighbour;
                if (layout.TryGetValue(pair.Key + Step(side), out neighbour))
                {
                    ChunkSide facing = Opposite(side);
                    Vector2Int neighbourSlot = pair.Key + Step(side);
                    int otherDoor;
                    DoorOnModule(neighbour, facing, neighbourSlot - neighbour.slot, out otherDoor);
                    if (!neighbour.chunk.IsDoorOpen(facing, otherDoor, neighbour.rotation & 3))
                    {
                        continue;
                    }
                    if (placement == candidate)
                    {
                        report.connections++;
                        if (DoorReached(placement, side, door, reached))
                        {
                            report.candidateReachesBase = true;
                        }
                    }
                }
                else
                {
                    report.entrances++;
                    if (!DoorReached(placement, side, door, reached))
                    {
                        report.deadEnds++;
                        report.deadEndNames.Add(placement.chunk.name + " " + side + " door (slot " + pair.Key + ")");
                    }
                }
            }
        }
        return report;
    }

    private HashSet<Vector2Int> FloodFromBase(Dictionary<Vector2Int, ChunkPlacement> layout)
    {
        if (baseCells.Count == 0)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (grid.GetState(cell) == CellState.Base)
                    {
                        baseCells.Add(cell);
                    }
                }
            }
        }

        HashSet<Vector2Int> seen = new HashSet<Vector2Int>(baseCells);
        Queue<Vector2Int> open = new Queue<Vector2Int>(baseCells);
        while (open.Count > 0)
        {
            Vector2Int cell = open.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                Vector2Int next = cell + Neighbour4[k];
                if (!seen.Contains(next) && LayoutCell(layout, next) == ChunkCell.Path)
                {
                    seen.Add(next);
                    open.Enqueue(next);
                }
            }
        }
        return seen;
    }

    // The painted cell of whichever chunk covers a grid cell (Empty where no chunk is).
    private ChunkCell LayoutCell(Dictionary<Vector2Int, ChunkPlacement> layout, Vector2Int cell)
    {
        int size = WallChunk.ModuleSize;
        Vector2Int local = cell - SlotOrigin(Vector2Int.zero);
        Vector2Int slot = new Vector2Int(Mathf.FloorToInt(local.x / (float)size), Mathf.FloorToInt(local.y / (float)size));
        ChunkPlacement placement;
        if (!layout.TryGetValue(slot, out placement))
        {
            return ChunkCell.Empty;
        }
        Vector2Int inChunk = cell - SlotOrigin(placement.slot);
        return placement.chunk.GetCell(inChunk.x, inChunk.y, placement.rotation & 3);
    }

    private bool DoorReached(ChunkPlacement placement, ChunkSide side, int door, HashSet<Vector2Int> reached)
    {
        Vector2Int origin = SlotOrigin(placement.slot);
        for (int i = 0; i < WallChunk.DoorWidth; i++)
        {
            Vector2Int cell = origin + placement.chunk.EdgeCell(side,
                door * WallChunk.ModuleSize + WallChunk.DoorStart + i, placement.rotation & 3);
            if (reached.Contains(cell))
            {
                return true;
            }
        }
        return false;
    }

    // The door index of `side` on the (turned) chunk's module at `local`, if that module lies on that side.
    private static bool DoorOnModule(ChunkPlacement placement, ChunkSide side, Vector2Int local, out int door)
    {
        int rotation = placement.rotation & 3;
        switch (side)
        {
            case ChunkSide.North: door = local.x; return local.y == placement.chunk.RotatedModulesY(rotation) - 1;
            case ChunkSide.South: door = local.x; return local.y == 0;
            case ChunkSide.East: door = local.y; return local.x == placement.chunk.RotatedModulesX(rotation) - 1;
            default: door = local.y; return local.x == 0;
        }
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
                sum = sum * 31 + placement.chunk.Version + placement.rotation * 7;
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
                grid.SetLayoutCell(cell, false);
            }
        }
        written.Clear();
    }

    // Covers the Wall cells with as few boxes as possible (WallBoxes, greedy rectangles), each a cube with a BoxCollider on the
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
        foreach (Vector2Int cell in written)
        {
            if (grid.GetState(cell) == CellState.Wall)
            {
                walls.Add(cell);
            }
        }
        foreach (RectInt box in WallBoxes.Merge(walls))
        {
            CreateBlock(box.position, box.width, box.height, layer);
        }
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
            int rotation = placement.rotation & 3;
            Vector3 min = CellCorner(SlotOrigin(placement.slot));
            Vector3 size = new Vector3(placement.chunk.RotatedWidth(rotation), 0f, placement.chunk.RotatedHeight(rotation)) *
                           grid.CellSize;
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
            int rotation = placement.rotation & 3;
            Vector2Int origin = SlotOrigin(placement.slot);
            for (int y = 0; y < placement.chunk.RotatedHeight(rotation); y++)
            {
                for (int x = 0; x < placement.chunk.RotatedWidth(rotation); x++)
                {
                    ChunkCell value = placement.chunk.GetCell(x, y, rotation);
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

    // Every usable slot (within maxSlotReach), outlined and labelled with its coordinates (Scene view, selected).
    private void DrawSlotGuide()
    {
        int size = WallChunk.ModuleSize;
        Gizmos.color = new Color(1f, 1f, 0.4f, 0.35f);
        for (int sy = -maxSlotReach; sy <= maxSlotReach; sy++)
        {
            for (int sx = -maxSlotReach; sx <= maxSlotReach; sx++)
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
