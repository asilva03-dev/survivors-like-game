/*
 * WallChunk
 * One wall preset: a painted grid of cells that BaseLayout writes into the BuildGrid (Create > TD > Wall Chunk).
 *
 * - Size is in modules of ModuleSize x ModuleSize cells (1x1, 2x1, 1x2, 2x2...). modulesX runs east, modulesY north;
 *   cell (0,0) is the chunk's south-west corner. Cells are stored unrotated; the overloads taking `rotation` (quarter
 *   turns clockwise seen from above, 0-3) read the chunk as if it were turned, in the turned chunk's own coordinates.
 * - Draft settings: isBaseChunk (offered in the base draft at the start of a run, else in the normal drafts) and
 *   copiesInPool (how many copies of this chunk the run's draft pool holds; 0 = never offered).
 * - Cell kinds: Path (enemies walk), Wall (blocks enemies and the player), BuildStrip (turrets later; the player can
 *   walk on it, enemies can't), Base (only in the core chunk), Empty (unpainted: the player can walk, enemies can't,
 *   nothing can be built).
 * - Doors: every module-length edge has one door spot, DoorWidth cells wide, centred on that module's edge. A door is
 *   open when all its edge cells are Path, closed otherwise. Doors are read from the painting, never set by hand.
 *   GetProblems lists painting that breaks the rules (path on an edge outside a door spot, half-painted doors).
 * - Changing the module counts keeps the overlapping painted cells.
 */

using System.Collections.Generic;
using UnityEngine;

public enum ChunkCell : byte
{
    Empty,
    Path,
    Wall,
    BuildStrip,
    Base
}

public enum ChunkSide
{
    North,
    East,
    South,
    West
}

[CreateAssetMenu(fileName = "WallChunk", menuName = "TD/Wall Chunk")]
public class WallChunk : ScriptableObject
{
    public const int ModuleSize = 12;
    public const int DoorWidth = 4;
    public const int DoorStart = (ModuleSize - DoorWidth) / 2;

    [Tooltip("Offered in the base draft at the start of a run (it holds the base); otherwise in the normal drafts.")]
    [SerializeField] private bool isBaseChunk;
    [Tooltip("Copies of this chunk in a run's draft pool. A picked copy is used up; 0 = never offered.")]
    [SerializeField, Min(0)] private int copiesInPool = 1;
    [SerializeField, Range(1, 4)] private int modulesX = 1;
    [SerializeField, Range(1, 4)] private int modulesY = 1;
    [SerializeField, HideInInspector] private int storedWidth;
    [SerializeField, HideInInspector] private ChunkCell[] cells;

    /// <summary>Bumped on every edit (not saved), so BaseLayout can rebuild live while a chunk is painted in Play.</summary>
    public int Version { get; private set; }

    public int ModulesX => modulesX;
    public int ModulesY => modulesY;
    public int Width => modulesX * ModuleSize;
    public int Height => modulesY * ModuleSize;
    public bool IsBaseChunk => isBaseChunk;
    public int CopiesInPool => copiesInPool;

    public int RotatedWidth(int rotation) => (rotation & 1) == 0 ? Width : Height;
    public int RotatedHeight(int rotation) => (rotation & 1) == 0 ? Height : Width;
    public int RotatedModulesX(int rotation) => (rotation & 1) == 0 ? modulesX : modulesY;
    public int RotatedModulesY(int rotation) => (rotation & 1) == 0 ? modulesY : modulesX;

    /// <summary>The cell at (x, y) of the chunk turned `rotation` quarter turns clockwise.</summary>
    public ChunkCell GetCell(int x, int y, int rotation)
    {
        int w = Width;
        int h = Height;
        switch (rotation & 3)
        {
            case 1: return GetCell(w - 1 - y, x);
            case 2: return GetCell(w - 1 - x, h - 1 - y);
            case 3: return GetCell(y, h - 1 - x);
            default: return GetCell(x, y);
        }
    }

    private void OnEnable()
    {
        EnsureSize();
    }

    private void OnValidate()
    {
        EnsureSize();
        Version++;
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public ChunkCell GetCell(int x, int y)
    {
        EnsureSize();
        return InBounds(x, y) ? cells[y * Width + x] : ChunkCell.Empty;
    }

    public void SetCell(int x, int y, ChunkCell value)
    {
        EnsureSize();
        if (InBounds(x, y))
        {
            cells[y * Width + x] = value;
            Version++;
        }
    }

    public void Fill(ChunkCell value)
    {
        EnsureSize();
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = value;
        }
        Version++;
    }

    public void Resize(int newModulesX, int newModulesY)
    {
        modulesX = Mathf.Clamp(newModulesX, 1, 4);
        modulesY = Mathf.Clamp(newModulesY, 1, 4);
        EnsureSize();
        Version++;
    }

    /// <summary>Door spots along a side (one per module on that side).</summary>
    public int DoorCount(ChunkSide side)
    {
        return DoorCount(side, 0);
    }

    public int DoorCount(ChunkSide side, int rotation)
    {
        return side == ChunkSide.North || side == ChunkSide.South ? RotatedModulesX(rotation) : RotatedModulesY(rotation);
    }

    /// <summary>True when every edge cell of that door spot is Path.</summary>
    public bool IsDoorOpen(ChunkSide side, int door)
    {
        return IsDoorOpen(side, door, 0);
    }

    public bool IsDoorOpen(ChunkSide side, int door, int rotation)
    {
        if (door < 0 || door >= DoorCount(side, rotation))
        {
            return false;
        }

        for (int i = 0; i < DoorWidth; i++)
        {
            Vector2Int cell = EdgeCell(side, door * ModuleSize + DoorStart + i, rotation);
            if (GetCell(cell.x, cell.y, rotation) != ChunkCell.Path)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The edge cell at `along` cells from the side's start (west end for N/S, south end for E/W).</summary>
    public Vector2Int EdgeCell(ChunkSide side, int along)
    {
        return EdgeCell(side, along, 0);
    }

    /// <summary>EdgeCell in the turned chunk's coordinates.</summary>
    public Vector2Int EdgeCell(ChunkSide side, int along, int rotation)
    {
        switch (side)
        {
            case ChunkSide.North: return new Vector2Int(along, RotatedHeight(rotation) - 1);
            case ChunkSide.South: return new Vector2Int(along, 0);
            case ChunkSide.East: return new Vector2Int(RotatedWidth(rotation) - 1, along);
            default: return new Vector2Int(0, along);
        }
    }

    public static bool IsDoorCell(int along)
    {
        int offset = along % ModuleSize;
        return offset >= DoorStart && offset < DoorStart + DoorWidth;
    }

    /// <summary>Painting that breaks the door rules; empty when the chunk is valid.</summary>
    public List<string> GetProblems()
    {
        List<string> problems = new List<string>();
        for (int s = 0; s < 4; s++)
        {
            ChunkSide side = (ChunkSide)s;
            int length = side == ChunkSide.North || side == ChunkSide.South ? Width : Height;

            for (int along = 0; along < length; along++)
            {
                Vector2Int cell = EdgeCell(side, along);
                if (GetCell(cell.x, cell.y) == ChunkCell.Path && !IsDoorCell(along))
                {
                    problems.Add(side + " edge: path at cell " + along + " is outside a door spot.");
                }
            }

            for (int door = 0; door < DoorCount(side); door++)
            {
                int pathCells = 0;
                for (int i = 0; i < DoorWidth; i++)
                {
                    Vector2Int cell = EdgeCell(side, door * ModuleSize + DoorStart + i);
                    if (GetCell(cell.x, cell.y) == ChunkCell.Path)
                    {
                        pathCells++;
                    }
                }
                if (pathCells > 0 && pathCells < DoorWidth)
                {
                    problems.Add(side + " door " + door + ": only " + pathCells + " of " + DoorWidth +
                                 " cells are path (paint all or none).");
                }
            }
        }
        return problems;
    }

    // Keeps the cell array matching the module counts, preserving painted cells that still fit.
    private void EnsureSize()
    {
        int width = Width;
        int height = Height;
        if (cells != null && storedWidth == width && cells.Length == width * height)
        {
            return;
        }

        ChunkCell[] resized = new ChunkCell[width * height];
        if (cells != null && storedWidth > 0)
        {
            int oldHeight = cells.Length / storedWidth;
            for (int y = 0; y < Mathf.Min(height, oldHeight); y++)
            {
                for (int x = 0; x < Mathf.Min(width, storedWidth); x++)
                {
                    resized[y * width + x] = cells[y * storedWidth + x];
                }
            }
        }

        cells = resized;
        storedWidth = width;
    }
}
