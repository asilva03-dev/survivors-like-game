/*
 * MapRoutes
 * Route maps over the painted map's pixels (map slice step 5, 2026-10-29): which way an enemy walks to reach the player
 * or the base when a cliff is in the way, so it finds the ramps. Read by EnemyMovementSystem.
 *
 * - Lives on the MapGreybox prefab root next to MapGround (Editor/MapGreyboxBuilder adds it). Instance = the active one.
 * - Moves between pixels (worked out once at Start from MapGround): straight to a 4-neighbour of the same height along
 *   the shared edge (CanStep) or lower (CanDrop: enemies may walk off ledges, user 2026-10-29); diagonal only when both
 *   straight sides are open both ways round, so routes never clip a cliff corner. Climbing is only possible by ramps.
 * - Two fields, each a Dijkstra (straight 10, diagonal 14) backwards from a target pixel, so every pixel knows its next
 *   step: to the player (rebuilt when the player's pixel changes, at most every playerRebuildInterval seconds) and to
 *   the base (BaseLight.BasePosition, built once at Start).
 * - TryGetDirection(from, goal, toPlayer): false when the straight line from -> goal crosses no blocked pixel edge (walk
 *   straight, as before), when the pixel has no route, or before the field exists; else the flat direction to the
 *   centre of the next pixel on the route.
 */

using System.Collections.Generic;
using UnityEngine;

public class MapRoutes : MonoBehaviour
{
    public const int Unreachable = int.MaxValue;
    private const int StraightCost = 10;
    private const int DiagonalCost = 14;

    // Order matters: 0-3 straight (E, W, N, S), 4-7 diagonal.
    private static readonly Vector2Int[] Steps =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
    };

    [Tooltip("The player route map is rebuilt when the player reaches a new map pixel, at most this often (seconds).")]
    [SerializeField, Min(0.02f)] private float playerRebuildInterval = 0.25f;

    private class Field
    {
        public int[] Distance;
        public sbyte[] Step;
        public Vector2Int Target = new Vector2Int(int.MinValue, int.MinValue);
        public bool Built;
    }

    private MapGround ground;
    private byte[] exits;
    private readonly Field toPlayer = new Field();
    private readonly Field toBase = new Field();
    private float lastPlayerBuild = float.NegativeInfinity;

    public static MapRoutes Instance { get; private set; }

    /// <summary>True once the route map to the player exists (GetDistance(…, true) means something).</summary>
    public bool PlayerRouteReady => toPlayer.Built;
    /// <summary>True once the route map to the base exists.</summary>
    public bool BaseRouteReady => toBase.Built;

    /// <summary>Milliseconds the last player route rebuild took (for profiling).</summary>
    public float LastBuildMs { get; private set; }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        ground = GetComponent<MapGround>();
        if (ground == null || !ground.HasData)
        {
            enabled = false;
            return;
        }
        BuildExits();
        if (BaseLight.Instance != null)
        {
            Build(toBase, ground.WorldToPixel(BaseLight.Instance.BasePosition));
        }
    }

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || exits == null)
        {
            return;
        }
        Vector2Int pixel = ground.WorldToPixel(player.transform.position);
        if (pixel != toPlayer.Target && ground.InBounds(pixel) && Time.time - lastPlayerBuild >= playerRebuildInterval)
        {
            lastPlayerBuild = Time.time;
            float start = Time.realtimeSinceStartup;
            Build(toPlayer, pixel);
            LastBuildMs = (Time.realtimeSinceStartup - start) * 1000f;
        }
    }

    /// <summary>Direction along the route toward the player or the base; false = walk straight (clear line, no route, no field).</summary>
    public bool TryGetDirection(Vector3 from, Vector3 goal, bool towardPlayer, out Vector3 direction)
    {
        direction = Vector3.zero;
        Field field = towardPlayer ? toPlayer : toBase;
        if (!field.Built || CanWalkStraight(from, goal))
        {
            return false;
        }
        Vector2Int cell = ground.WorldToPixel(from);
        if (!ground.InBounds(cell))
        {
            return false;
        }
        int index = cell.y * ground.Width + cell.x;
        if (field.Distance[index] == Unreachable || field.Distance[index] == 0)
        {
            return false;
        }
        Vector3 next = ground.PixelCenter(cell + Steps[field.Step[index]]);
        direction = new Vector3(next.x - from.x, 0f, next.z - from.z);
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }
        direction.Normalize();
        return true;
    }

    /// <summary>Remaining route cost (10 per straight pixel step) from a world point to the player or base; Unreachable if none.</summary>
    public int GetDistance(Vector3 from, bool towardPlayer)
    {
        Field field = towardPlayer ? toPlayer : toBase;
        Vector2Int cell = ground != null ? ground.WorldToPixel(from) : Vector2Int.zero;
        if (!field.Built || !ground.InBounds(cell))
        {
            return Unreachable;
        }
        return field.Distance[cell.y * ground.Width + cell.x];
    }

    /// <summary>True when walking the straight line between two points never crosses a pixel edge that can't be walked (ignores body width).</summary>
    public bool CanWalkStraight(Vector3 from, Vector3 to)
    {
        if (exits == null)
        {
            return true;
        }
        float unit = ground.UnitsPerPixel;
        Vector3 sw = ground.SouthWest;
        Vector2 a = new Vector2((from.x - sw.x) / unit, (from.z - sw.z) / unit);
        Vector2 b = new Vector2((to.x - sw.x) / unit, (to.z - sw.z) / unit);
        Vector2Int p = new Vector2Int(Mathf.FloorToInt(a.x), Mathf.FloorToInt(a.y));
        Vector2Int q = new Vector2Int(Mathf.FloorToInt(b.x), Mathf.FloorToInt(b.y));
        if (!ground.InBounds(p))
        {
            return true;
        }

        Vector2 d = b - a;
        int stepX = d.x > 0f ? 1 : -1;
        int stepY = d.y > 0f ? 1 : -1;
        float tDeltaX = Mathf.Abs(d.x) > 1e-6f ? Mathf.Abs(1f / d.x) : float.MaxValue;
        float tDeltaY = Mathf.Abs(d.y) > 1e-6f ? Mathf.Abs(1f / d.y) : float.MaxValue;
        float tMaxX = Mathf.Abs(d.x) > 1e-6f ? ((stepX > 0 ? p.x + 1 - a.x : a.x - p.x) * tDeltaX) : float.MaxValue;
        float tMaxY = Mathf.Abs(d.y) > 1e-6f ? ((stepY > 0 ? p.y + 1 - a.y : a.y - p.y) * tDeltaY) : float.MaxValue;

        for (int guard = 0; p != q && guard < 1024; guard++)
        {
            int k;
            if (tMaxX < tMaxY)
            {
                k = stepX > 0 ? 0 : 1;
                tMaxX += tDeltaX;
            }
            else
            {
                k = stepY > 0 ? 2 : 3;
                tMaxY += tDeltaY;
            }
            if ((exits[p.y * ground.Width + p.x] & (1 << k)) == 0)
            {
                return false;
            }
            p += Steps[k];
            if (!ground.InBounds(p))
            {
                return false;
            }
        }
        return true;
    }

    private void BuildExits()
    {
        int width = ground.Width;
        int height = ground.Height;
        exits = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int p = new Vector2Int(x, y);
                int bits = 0;
                for (int k = 0; k < 4; k++)
                {
                    if (Passable(p, p + Steps[k]))
                    {
                        bits |= 1 << k;
                    }
                }
                for (int k = 4; k < 8; k++)
                {
                    Vector2Int q = p + Steps[k];
                    Vector2Int sideX = new Vector2Int(q.x, p.y);
                    Vector2Int sideY = new Vector2Int(p.x, q.y);
                    if (Passable(p, sideX) && Passable(sideX, q) && Passable(p, sideY) && Passable(sideY, q))
                    {
                        bits |= 1 << k;
                    }
                }
                exits[y * width + x] = (byte)bits;
            }
        }
    }

    private bool Passable(Vector2Int from, Vector2Int to)
    {
        return ground.CanStep(from, to) || ground.CanDrop(from, to);
    }

    // Dijkstra backwards from the target: a pixel's Step is the move that leads one step closer along a shortest route.
    // Plain arrays and index math (no Vector2Int/List in the loop): this runs a few times a second over the whole map.
    private void Build(Field field, Vector2Int target)
    {
        int width = ground.Width;
        int height = ground.Height;
        int count = width * height;
        if (field.Distance == null || field.Distance.Length != count)
        {
            field.Distance = new int[count];
            field.Step = new sbyte[count];
        }
        int[] distances = field.Distance;
        sbyte[] steps = field.Step;
        for (int i = 0; i < count; i++)
        {
            distances[i] = Unreachable;
        }
        field.Target = target;
        field.Built = true;
        if (!ground.InBounds(target))
        {
            return;
        }

        heapCount = 0;
        int start = target.y * width + target.x;
        distances[start] = 0;
        Push(0, start);
        while (heapCount > 0)
        {
            long top = Pop();
            int distance = (int)(top >> 32);
            int index = (int)(top & 0xffffffff);
            if (distance > distances[index])
            {
                continue;
            }
            int x = index % width;
            int y = index / width;
            for (int k = 0; k < 8; k++)
            {
                // A pixel `before` that moves by Steps[k] into this cell.
                int bx = x - StepX[k];
                int by = y - StepY[k];
                if (bx < 0 || by < 0 || bx >= width || by >= height)
                {
                    continue;
                }
                int beforeIndex = by * width + bx;
                if ((exits[beforeIndex] & (1 << k)) == 0)
                {
                    continue;
                }
                int next = distance + (k < 4 ? StraightCost : DiagonalCost);
                if (next < distances[beforeIndex])
                {
                    distances[beforeIndex] = next;
                    steps[beforeIndex] = (sbyte)k;
                    Push(next, beforeIndex);
                }
            }
        }
    }

    private static readonly int[] StepX = { 1, -1, 0, 0, 1, -1, 1, -1 };
    private static readonly int[] StepY = { 0, 0, 1, -1, 1, 1, -1, -1 };

    private long[] heapItems = new long[1024];
    private int heapCount;

    private void Push(int distance, int index)
    {
        if (heapCount == heapItems.Length)
        {
            System.Array.Resize(ref heapItems, heapItems.Length * 2);
        }
        long item = ((long)distance << 32) | (uint)index;
        int i = heapCount++;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (heapItems[parent] <= item)
            {
                break;
            }
            heapItems[i] = heapItems[parent];
            i = parent;
        }
        heapItems[i] = item;
    }

    private long Pop()
    {
        long top = heapItems[0];
        long last = heapItems[--heapCount];
        int i = 0;
        while (true)
        {
            int left = i * 2 + 1;
            if (left >= heapCount)
            {
                break;
            }
            int right = left + 1;
            int child = right < heapCount && heapItems[right] < heapItems[left] ? right : left;
            if (heapItems[child] >= last)
            {
                break;
            }
            heapItems[i] = heapItems[child];
            i = child;
        }
        if (heapCount > 0)
        {
            heapItems[i] = last;
        }
        return top;
    }
}
