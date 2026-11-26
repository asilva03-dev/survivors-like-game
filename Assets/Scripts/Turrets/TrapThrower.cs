/*
 * TrapThrower
 * The Trapper turret's weapon (2026-11-23; user: the cat grabs a trap, arms it and throws it onto the nearest path every
 * so often; traps last a while and snap on their own when they run out). On the Trapper prefab's root, next to Turret.
 *
 * - One throw per stats.throwInterval, always (no trap cap since 2026-11-23, user: "it should always be throwing"). The
 *   spot is picked just before each throw:
 *   the most valuable route cell centre within stats.range of the Trapper (flat): value = the cell's traffic x
 *   stats.stackValue per set or flying trap already on it (closer than stats.trapSpacing; any Trapper's), ties the nearest
 *   (2026-11-23, user: prefer spots without a trap but don't exclude them, so traps still stack). Only Path cells (the dirt
 *   path, PathFloor) are used, never open chunk grass, and each spot sits in the middle of its dirt corridor (PathCentre:
 *   centred across the way rats walk there, by the flow field, when that's at most stats.pathCentreMaxWidth cells wide;
 *   one spot per row along the corridor) (2026-11-23, user). Route cells (shared, rebuilt
 *   when the flow field is) = the cells rats really walk: traced along the FlowField from every entrance of the base layout
 *   (a walkable layout cell next to walkable ground outside it) to the base; a cell's traffic = how many entrance routes
 *   pass through it, so traps go where the routes converge (a first try with the nearest route cell put them on a side
 *   route few rats used). With no route cell in range it falls back to the nearest Path
 *   cell (a side path rats may never use: first test showed traps there expiring unused). The spot sits on the map
 *   ground (MapGround height) when there is one.
 * - The throw animation (2026-11-23, user): `animator` (TrapperAnimator) loops one cycle per stats.throwInterval (turn to
 *   the bag, rummage, pull out a trap, arm it, turn to the spot, throw, back to rest); it asks this for the spot at its
 *   pick time and calls back at its release time, when a pooled RatTrap is launched from the right paw (RatTrap.Launch).
 *   The animator turns the cat (Body); the crates and sack never turn. (This replaced change 6's aim-then-throw turn.)
 *   Without an animator it throws every interval from `hand` without turning.
 * - Traps come from one shared ComponentPool<RatTrap> (trapPrefab, under "Rat Traps"), never instantiated per throw.
 * - Range is the stats' range (Turret.Range uses it for the range ring and the build ghost). Upgrades (2026-11-25):
 *   Turret hands over its own upgraded copy of the stats with UseStats; no Trapper tiers exist yet.
 */

using System.Collections.Generic;
using UnityEngine;

public class TrapThrower : MonoBehaviour
{
    private static ComponentPool<RatTrap> sharedPool;
    private static Transform poolRoot;
    private static RatTrap pooledPrefab;
    private static readonly Dictionary<Vector2Int, int> route = new Dictionary<Vector2Int, int>();
    private static readonly HashSet<Vector2Int> traced = new HashSet<Vector2Int>();
    private static bool routeDirty = true;
    private static FlowField routeField;

    [SerializeField] private TrapperStats stats;
    [SerializeField] private RatTrap trapPrefab;
    [Tooltip("Plays the throw cycle and turns the cat; it launches the trap at its release time.")]
    [SerializeField] private TrapperAnimator animator;
    [Tooltip("Where the trap leaves the cat when there is no animator.")]
    [SerializeField] private Transform hand;
    [SerializeField, Min(0)] private int prewarm = 8;

    private Turret turret;
    private float cooldown;

    public float Range => stats != null ? stats.Range : 0f;
    public TrapperStats Stats => stats;

    /// <summary>Swaps in other stats (Turret hands over its own upgraded copy; 2026-11-25 upgrade pass). New throws and
    /// traps use them; traps already thrown keep theirs.</summary>
    public void UseStats(TrapperStats replacement)
    {
        if (replacement != null)
        {
            stats = replacement;
        }
    }
    /// <summary>Traps of this Trapper flying or set right now.</summary>
    public int LiveTraps
    {
        get
        {
            int count = 0;
            List<RatTrap> active = RatTrap.Active;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Owner == this)
                {
                    count++;
                }
            }
            return count;
        }
    }

    private void Awake()
    {
        turret = GetComponent<Turret>();
    }

    private void Start()
    {
        cooldown = stats != null ? stats.ThrowInterval : 1f;
        if (animator != null && stats != null && trapPrefab != null)
        {
            animator.RestDirection = turret != null ? turret.RestDirection : transform.forward;
            animator.Run(CycleSeconds, PickSpot, Launch);
        }
    }

    private float CycleSeconds()
    {
        return stats != null ? stats.ThrowInterval : 1f;
    }

    // The animator asks for the spot just before the throw (null = nowhere to throw this cycle).
    private Vector3? PickSpot()
    {
        RefreshRouteField();
        Vector3 spot;
        return FindSpot(out spot) ? spot : (Vector3?)null;
    }

    private void Launch(Vector3 from, Vector3 spot)
    {
        ComponentPool<RatTrap> pool = Pool();
        RatTrap trap = pool.Get(from, Quaternion.identity);
        trap.Launch(pool, from, spot, stats, this);
        Thrown++;
    }

    private void OnEnable()
    {
        routeDirty = true;
    }

    private static void MarkRouteDirty()
    {
        routeDirty = true;
    }

    private void Update()
    {
        if (stats == null || trapPrefab == null || animator != null)
        {
            return;
        }
        RefreshRouteField();
        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
        {
            return;
        }
        Vector3 spot;
        if (FindSpot(out spot))
        {
            Launch(hand != null ? hand.position : transform.position + Vector3.up * 0.6f, spot);
        }
        cooldown = stats.ThrowInterval;
    }

    // Follows the current flow field so the route is rebuilt when the layout changes.
    private void RefreshRouteField()
    {
        FlowField field = FlowField.Instance;
        if (field != routeField)
        {
            if (routeField != null)
            {
                routeField.Rebuilt -= MarkRouteDirty;
            }
            routeField = field;
            if (routeField != null)
            {
                routeField.Rebuilt += MarkRouteDirty;
            }
            routeDirty = true;
        }
    }

    // The cells rats walk: from every entrance of the layout, follow the flow field to the base.
    private static void RebuildRoute(BuildGrid grid, FlowField field)
    {
        route.Clear();
        routeDirty = false;
        if (field == null)
        {
            return;
        }
        Vector2Int[] sides = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!grid.IsLayoutCell(cell) || !grid.IsWalkable(cell) || field.GetDistance(cell) == FlowField.Unreachable)
                {
                    continue;
                }
                bool entrance = false;
                for (int i = 0; i < sides.Length && !entrance; i++)
                {
                    Vector2Int next = cell + sides[i];
                    entrance = grid.InBounds(next) && grid.IsWalkable(next) && !grid.IsLayoutCell(next);
                }
                if (!entrance)
                {
                    continue;
                }
                Vector2Int step = cell;
                traced.Clear();
                for (int guard = 0; guard < 400; guard++)
                {
                    if (!traced.Add(step))
                    {
                        break;
                    }
                    int count;
                    route.TryGetValue(step, out count);
                    route[step] = count + 1;
                    if (grid.GetState(step) == CellState.Base)
                    {
                        break;
                    }
                    Vector3 dir = field.GetCellDirection(step);
                    if (dir == Vector3.zero)
                    {
                        break;
                    }
                    step += new Vector2Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.z));
                }
            }
        }
    }

    // The best route cell (else Path cell) centre in range: traffic x stackValue per trap already on it, ties nearest.
    private bool FindSpot(out Vector3 spot)
    {
        spot = Vector3.zero;
        BuildGrid grid = BuildGrid.Instance;
        if (grid == null)
        {
            return false;
        }
        if (routeDirty)
        {
            RebuildRoute(grid, FlowField.Instance);
        }
        if (FindSpot(grid, true, out spot) || FindSpot(grid, false, out spot))
        {
            MapGround ground = MapGround.Instance;
            if (ground != null && ground.HasData)
            {
                spot.y = ground.HeightAt(spot);
            }
            return true;
        }
        return false;
    }

    private bool FindSpot(BuildGrid grid, bool onRoute, out Vector3 spot)
    {
        spot = Vector3.zero;
        Vector3 centre = transform.position;
        int reach = Mathf.CeilToInt(stats.Range / grid.CellSize);
        Vector2Int home = grid.WorldToCell(centre);
        float best = float.MaxValue;
        float bestValue = -1f;
        bool found = false;
        float sqrRange = stats.Range * stats.Range;
        float sqrSpacing = stats.TrapSpacing * stats.TrapSpacing;
        for (int dx = -reach; dx <= reach; dx++)
        {
            for (int dy = -reach; dy <= reach; dy++)
            {
                Vector2Int cell = new Vector2Int(home.x + dx, home.y + dy);
                int traffic = 1;
                if (grid.GetState(cell) != CellState.Path || (onRoute && !route.TryGetValue(cell, out traffic)))
                {
                    continue;
                }
                Vector3 p = PathCentre(grid, routeField, cell, stats.PathCentreMaxWidth);
                p.y = 0f;
                Vector3 offset = p - centre;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr > sqrRange)
                {
                    continue;
                }
                float value = traffic * Mathf.Pow(stats.StackValue, TrapsOn(p, sqrSpacing));
                if (value < bestValue - 0.0001f || (value < bestValue + 0.0001f && sqr >= best))
                {
                    continue;
                }
                best = sqr;
                bestValue = value;
                spot = p;
                found = true;
            }
        }
        return found;
    }

    // The cell's spot moved to the middle of its dirt corridor: centred across the way rats walk there (the flow field's
    // direction; without one, across the narrower axis), when the Path run across is at most maxWidth cells. So a 4-wide
    // corridor's cells map to its centre line, one spot per row along it.
    private static Vector3 PathCentre(BuildGrid grid, FlowField field, Vector2Int cell, int maxWidth)
    {
        int left = PathRun(grid, cell, Vector2Int.left, maxWidth);
        int right = PathRun(grid, cell, Vector2Int.right, maxWidth);
        int down = PathRun(grid, cell, Vector2Int.down, maxWidth);
        int up = PathRun(grid, cell, Vector2Int.up, maxWidth);
        int widthX = left + right + 1;
        int widthZ = down + up + 1;
        Vector3 flow = field != null ? field.GetCellDirection(cell) : Vector3.zero;
        bool acrossX = flow.sqrMagnitude > 0.0001f ? Mathf.Abs(flow.z) >= Mathf.Abs(flow.x) : widthX <= widthZ;
        Vector3 p = grid.CellToWorld(cell);
        if (acrossX && widthX <= maxWidth)
        {
            p.x += (right - left) * grid.CellSize * 0.5f;
        }
        else if (!acrossX && widthZ <= maxWidth)
        {
            p.z += (up - down) * grid.CellSize * 0.5f;
        }
        return p;
    }

    // How many Path cells follow the cell in one direction (stops past maxWidth).
    private static int PathRun(BuildGrid grid, Vector2Int cell, Vector2Int step, int maxWidth)
    {
        int count = 0;
        Vector2Int next = cell + step;
        while (count <= maxWidth && grid.GetState(next) == CellState.Path)
        {
            count++;
            next += step;
        }
        return count;
    }

    // Set or flying traps (any Trapper's) lying on the spot (closer than trapSpacing).
    private static int TrapsOn(Vector3 p, float sqrSpacing)
    {
        int count = 0;
        List<RatTrap> active = RatTrap.Active;
        for (int i = 0; i < active.Count; i++)
        {
            Vector3 offset = active[i].Spot - p;
            offset.y = 0f;
            if (offset.sqrMagnitude < sqrSpacing)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Traps thrown by every Trapper (for tests).</summary>
    public static int Thrown { get; private set; }

    private ComponentPool<RatTrap> Pool()
    {
        if (sharedPool == null || poolRoot == null || pooledPrefab != trapPrefab)
        {
            poolRoot = new GameObject("Rat Traps").transform;
            pooledPrefab = trapPrefab;
            sharedPool = new ComponentPool<RatTrap>(trapPrefab, poolRoot, prewarm);
        }
        return sharedPool;
    }
}
