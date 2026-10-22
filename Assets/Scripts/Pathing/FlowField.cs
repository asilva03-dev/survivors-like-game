/*
 * FlowField
 * For every walkable cell of the BuildGrid, the direction to walk to reach the base by the shortest route.
 *
 * - Dijkstra from all Base cells outward over walkable cells (Free/Path), 8 neighbours (straight step cost 10,
 *   diagonal 14), never stepping diagonally past an unwalkable corner. Each reachable cell then points at its
 *   neighbour with the lowest distance. Base cells and cells with no route to the base have no direction (zero).
 * - Wall avoidance: entering a cell within wallAvoidRange cells of a Wall/BuildStrip/Blocked cell (8-way, so corners count)
 *   costs wallAvoidCost extra (less the farther it is), so routes keep off walls and corners: in an opening the
 *   edge lanes point diagonally into the middle lanes. 0 cost = pure shortest path.
 * - Rebuilt when the grid raises Changed (a layout change), never per frame; raises Rebuilt afterwards.
 * - GetDirection(world) blends the 4 nearest cells' directions (bilinear) so movement doesn't zig-zag on cell
 *   edges. It returns zero outside the grid or where there is no route (callers fall back to straight seek).
 * - Put it on the BuildGrid object (or point grid at one).
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public class FlowField : MonoBehaviour
{
    public const int Unreachable = int.MaxValue;
    private const int StraightCost = 10;
    private const int DiagonalCost = 14;

    private static readonly Vector2Int[] Neighbours =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    [SerializeField] private BuildGrid grid;
    [Tooltip("Cells this close to a wall (in cells, corners included) cost extra to walk through.")]
    [SerializeField, Range(0, 4)] private int wallAvoidRange = 1;
    [Tooltip("Extra cost of a cell right next to a wall (a straight step costs 10); falls off with distance.")]
    [SerializeField, Min(0)] private int wallAvoidCost = 12;

    public static FlowField Instance { get; private set; }

    public event Action Rebuilt;

    private int[] extraCosts;
    private int[] distances;
    private Vector2[] directions;
    private readonly List<long> heap = new List<long>();

    public BuildGrid Grid => grid;
    public int MaxDistance { get; private set; }

    private void Awake()
    {
        if (grid == null)
        {
            grid = GetComponent<BuildGrid>();
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (grid != null)
        {
            grid.Changed += Rebuild;
        }
    }

    private void OnDisable()
    {
        if (grid != null)
        {
            grid.Changed -= Rebuild;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && distances != null && grid != null)
        {
            Rebuild();
        }
    }

    public void Rebuild()
    {
        int width = grid.Width;
        int height = grid.Height;
        int count = width * height;
        if (distances == null || distances.Length != count)
        {
            distances = new int[count];
            directions = new Vector2[count];
            extraCosts = new int[count];
        }

        ComputeWallCosts(width, height);
        heap.Clear();
        for (int i = 0; i < count; i++)
        {
            directions[i] = Vector2.zero;
            if (grid.GetState(new Vector2Int(i % width, i / width)) == CellState.Base)
            {
                distances[i] = 0;
                Push(0, i);
            }
            else
            {
                distances[i] = Unreachable;
            }
        }

        while (heap.Count > 0)
        {
            long top = Pop();
            int distance = (int)(top >> 32);
            int index = (int)(top & 0xffffffff);
            if (distance > distances[index])
            {
                continue;
            }

            Vector2Int cell = new Vector2Int(index % width, index / width);
            for (int k = 0; k < Neighbours.Length; k++)
            {
                Vector2Int next = cell + Neighbours[k];
                if (!grid.IsWalkable(next) || !CanStep(cell, Neighbours[k]))
                {
                    continue;
                }

                int nextIndex = next.y * width + next.x;
                int nextDistance = distance + (k < 4 ? StraightCost : DiagonalCost) + extraCosts[nextIndex];
                if (nextDistance < distances[nextIndex])
                {
                    distances[nextIndex] = nextDistance;
                    Push(nextDistance, nextIndex);
                }
            }
        }

        MaxDistance = 0;
        for (int i = 0; i < count; i++)
        {
            int distance = distances[i];
            if (distance == 0 || distance == Unreachable)
            {
                continue;
            }

            MaxDistance = Mathf.Max(MaxDistance, distance);
            Vector2Int cell = new Vector2Int(i % width, i / width);
            int best = distance;
            Vector2Int bestStep = Vector2Int.zero;
            for (int k = 0; k < Neighbours.Length; k++)
            {
                Vector2Int next = cell + Neighbours[k];
                if (!grid.InBounds(next) || !CanStep(cell, Neighbours[k]))
                {
                    continue;
                }

                int nextDistance = distances[next.y * width + next.x];
                if (nextDistance < best)
                {
                    best = nextDistance;
                    bestStep = Neighbours[k];
                }
            }

            directions[i] = ((Vector2)bestStep).normalized;
        }

        Rebuilt?.Invoke();
    }

    // Extra cost per cell from its distance to the nearest wall (Chebyshev, so diagonal corners count):
    // wallAvoidCost next to a wall, falling off linearly to 0 beyond wallAvoidRange.
    private void ComputeWallCosts(int width, int height)
    {
        int range = wallAvoidRange;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int extra = 0;
                if (range > 0 && wallAvoidCost > 0)
                {
                    int nearest = int.MaxValue;
                    for (int dy = -range; dy <= range && nearest > 1; dy++)
                    {
                        for (int dx = -range; dx <= range; dx++)
                        {
                            if ((dx == 0 && dy == 0) || !IsWall(new Vector2Int(x + dx, y + dy)))
                            {
                                continue;
                            }
                            nearest = Mathf.Min(nearest, Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)));
                        }
                    }
                    if (nearest <= range)
                    {
                        extra = wallAvoidCost * (range - nearest + 1) / range;
                    }
                }
                extraCosts[y * width + x] = extra;
            }
        }
    }

    private bool IsWall(Vector2Int cell)
    {
        CellState state = grid.GetState(cell);
        return state == CellState.Wall || state == CellState.BuildStrip || state == CellState.Blocked;
    }

    // A diagonal step needs both side cells open, so paths never squeeze past a wall corner.
    private bool CanStep(Vector2Int from, Vector2Int step)
    {
        if (step.x == 0 || step.y == 0)
        {
            return true;
        }
        return IsOpen(new Vector2Int(from.x + step.x, from.y)) && IsOpen(new Vector2Int(from.x, from.y + step.y));
    }

    private bool IsOpen(Vector2Int cell)
    {
        return grid.IsWalkable(cell) || grid.GetState(cell) == CellState.Base;
    }

    /// <summary>Distance to the base in cost units (10 per straight step), or Unreachable.</summary>
    public int GetDistance(Vector2Int cell)
    {
        return distances != null && grid.InBounds(cell) ? distances[cell.y * grid.Width + cell.x] : Unreachable;
    }

    /// <summary>The cell's own direction (no blending), flat XZ; zero for base, blocked or unreachable cells.</summary>
    public Vector3 GetCellDirection(Vector2Int cell)
    {
        if (directions == null || !grid.InBounds(cell))
        {
            return Vector3.zero;
        }
        Vector2 direction = directions[cell.y * grid.Width + cell.x];
        return new Vector3(direction.x, 0f, direction.y);
    }

    /// <summary>Blended direction toward the base at a world position, flat and normalized; zero if none.</summary>
    public Vector3 GetDirection(Vector3 world)
    {
        if (directions == null)
        {
            return Vector3.zero;
        }

        Vector3 local = world - grid.Origin;
        float fx = local.x / grid.CellSize - 0.5f;
        float fy = local.z / grid.CellSize - 0.5f;
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0;
        float ty = fy - y0;

        Vector2 sum = Vector2.zero;
        sum += Sample(x0, y0) * ((1f - tx) * (1f - ty));
        sum += Sample(x0 + 1, y0) * (tx * (1f - ty));
        sum += Sample(x0, y0 + 1) * ((1f - tx) * ty);
        sum += Sample(x0 + 1, y0 + 1) * (tx * ty);

        if (sum.sqrMagnitude < 0.0001f)
        {
            sum = Sample(Mathf.FloorToInt(local.x / grid.CellSize), Mathf.FloorToInt(local.z / grid.CellSize));
            if (sum.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }
        }

        sum.Normalize();
        return new Vector3(sum.x, 0f, sum.y);
    }

    private Vector2 Sample(int x, int y)
    {
        Vector2Int cell = new Vector2Int(x, y);
        return grid.InBounds(cell) ? directions[y * grid.Width + x] : Vector2.zero;
    }

    private void Push(int distance, int index)
    {
        heap.Add(((long)distance << 32) | (uint)index);
        int i = heap.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent] <= heap[i])
            {
                break;
            }
            long swap = heap[parent];
            heap[parent] = heap[i];
            heap[i] = swap;
            i = parent;
        }
    }

    private long Pop()
    {
        long top = heap[0];
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);

        int i = 0;
        while (true)
        {
            int left = i * 2 + 1;
            int right = left + 1;
            int smallest = i;
            if (left < heap.Count && heap[left] < heap[smallest])
            {
                smallest = left;
            }
            if (right < heap.Count && heap[right] < heap[smallest])
            {
                smallest = right;
            }
            if (smallest == i)
            {
                break;
            }
            long swap = heap[smallest];
            heap[smallest] = heap[i];
            heap[i] = swap;
            i = smallest;
        }
        return top;
    }
}
