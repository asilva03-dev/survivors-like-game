/*
 * BuildGrid
 * The square cell grid shared by enemy pathing (flow field, later) and building. Lies flat on the ground (XZ),
 * centred on this object's position (put it on the base spot), width x height cells of cellSize units.
 * The object's rotation and scale are ignored (keep them at 0 / 1; the debug view assumes it).
 *
 * - Every cell has a CellState. Enemies may walk Free and Path cells only; anything outside the grid reads as
 *   OutOfBounds. Blocked = ground enemies can't walk (the player can, nothing is built there; today only painted Base
 *   cells outside the grid's own base). Cells whose centre is within baseRadius of the grid centre start as Base.
 * - Layout cells: BaseLayout marks every cell a chunk covers (SetLayoutCell), including its unpainted ground, which
 *   stays Free (walkable). IsOpenGround treats layout cells as taken, so spawners keep trees/enemies off the chunks.
 * - Wall presets will write Path / Wall / BuildStrip cells later. SetState raises Changed (pass notify false
 *   while writing many cells, then call NotifyChanged once) so listeners rebuild only when the layout changes.
 * - Changing width/height/cellSize/baseRadius in the inspector during Play rebuilds the grid, clearing written cells, and
 *   raises Rebuilt (BaseLayout re-applies itself on it).
 * - Size: 84 x 84 = the core slot + two rings of 12-cell chunk slots (60 cells) + a 12-cell open margin on every side.
 */

using System;
using UnityEngine;

public enum CellState
{
    Free,
    Path,
    Wall,
    BuildStrip,
    Blocked,
    Base,
    OutOfBounds
}

public class BuildGrid : MonoBehaviour
{
    [SerializeField, Min(1)] private int width = 84;
    [SerializeField, Min(1)] private int height = 84;
    [SerializeField, Min(0.25f)] private float cellSize = 1f;
    [Tooltip("Cells whose centre is within this distance of the grid centre are Base cells. 2.2 with 1-unit cells = a " +
             "4x4 block that sticks out past the base model so it stays visible under it.")]
    [SerializeField, Min(0f)] private float baseRadius = 2.2f;

    public static BuildGrid Instance { get; private set; }

    public event Action Changed;

    /// <summary>Raised after the grid is rebuilt from scratch (size change in Play), which clears every written cell.</summary>
    public event Action Rebuilt;

    private CellState[] cells;
    private bool[] layoutCells;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    /// <summary>World position of the grid's minimum corner (cell 0,0's outer corner), on the ground.</summary>
    public Vector3 Origin
    {
        get
        {
            Vector3 center = transform.position;
            return new Vector3(center.x - width * cellSize * 0.5f, 0f, center.z - height * cellSize * 0.5f);
        }
    }

    private void Awake()
    {
        Instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && cells != null)
        {
            Build();
            Rebuilt?.Invoke();
            NotifyChanged();
        }
    }

    private void Build()
    {
        cells = new CellState[width * height];
        layoutCells = new bool[width * height];
        Vector3 center = transform.position;
        float sqrRadius = baseRadius * baseRadius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 offset = CellToWorld(new Vector2Int(x, y)) - center;
                offset.y = 0f;
                if (offset.sqrMagnitude <= sqrRadius)
                {
                    cells[y * width + x] = CellState.Base;
                }
            }
        }
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - Origin;
        return new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));
    }

    /// <summary>Centre of the cell on the ground (y 0).</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        Vector3 origin = Origin;
        return new Vector3(origin.x + (cell.x + 0.5f) * cellSize, 0f, origin.z + (cell.y + 0.5f) * cellSize);
    }

    public CellState GetState(Vector2Int cell)
    {
        return InBounds(cell) ? cells[cell.y * width + cell.x] : CellState.OutOfBounds;
    }

    public bool IsWalkable(Vector2Int cell)
    {
        CellState state = GetState(cell);
        return state == CellState.Free || state == CellState.Path;
    }

    public void SetState(Vector2Int cell, CellState state, bool notify = true)
    {
        if (!InBounds(cell) || state == CellState.OutOfBounds)
        {
            return;
        }

        int index = cell.y * width + cell.x;
        if (cells[index] == state)
        {
            return;
        }

        cells[index] = state;
        if (notify)
        {
            NotifyChanged();
        }
    }

    /// <summary>Marks a cell as covered (or no longer covered) by a base layout chunk. Does not raise Changed.</summary>
    public void SetLayoutCell(Vector2Int cell, bool inLayout)
    {
        if (InBounds(cell))
        {
            layoutCells[cell.y * width + cell.x] = inLayout;
        }
    }

    public bool IsLayoutCell(Vector2Int cell)
    {
        return InBounds(cell) && layoutCells[cell.y * width + cell.x];
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    /// <summary>True when every cell touched by a circle of `radius` around `world` is Free (and not part of a layout chunk)
    /// or off the grid, i.e. the spot is open ground outside the base layout. Used by the enemy and tree spawners.</summary>
    public bool IsOpenGround(Vector3 world, float radius)
    {
        Vector2Int min = WorldToCell(world - new Vector3(radius, 0f, radius));
        Vector2Int max = WorldToCell(world + new Vector3(radius, 0f, radius));
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                CellState state = GetState(cell);
                if ((state != CellState.Free && state != CellState.OutOfBounds) || IsLayoutCell(cell))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>IsOpenGround on the scene's grid; true when there is no grid.</summary>
    public static bool IsOpenGroundAt(Vector3 world, float radius)
    {
        return Instance == null || Instance.IsOpenGround(world, radius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        Vector3 size = new Vector3(width * cellSize, 0f, height * cellSize);
        Gizmos.DrawWireCube(Origin + size * 0.5f, size);
    }
}
