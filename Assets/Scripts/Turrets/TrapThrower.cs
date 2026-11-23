/*
 * TrapThrower
 * The Trapper turret's weapon (2026-11-23; user: the cat grabs a trap, arms it and throws it onto the nearest path every
 * so often; traps last a while and snap on their own when they run out). On the Trapper prefab's root, next to Turret.
 *
 * - Every stats.throwInterval seconds (while fewer than stats.maxTraps of its traps are flying or set) it picks a spot:
 *   the busiest route cell centre within stats.range of the Trapper (flat) that is at least stats.trapSpacing from every
 *   set or flying trap (ties: the nearest); none free = it waits and tries again next frame. Route cells (shared, rebuilt
 *   when the flow field is) = the cells rats really walk: traced along the FlowField from every entrance of the base layout
 *   (a walkable layout cell next to walkable ground outside it) to the base; a cell's traffic = how many entrance routes
 *   pass through it, so traps go where the routes converge (a first try with the nearest route cell put them on a side
 *   route few rats used). With no route cell in range it falls back to the nearest Path
 *   cell (a side path rats may never use: first test showed traps there expiring unused). The spot sits on the map
 *   ground (MapGround height) when there is one.
 * - It turns its body (the model, `body`) toward the spot at stats.turnSpeed and throws a pooled RatTrap from `hand`
 *   (RatTrap.Launch). The first throw comes after one interval. Between throws the body turns back toward the turret's
 *   rest direction (the nearest path).
 * - Traps come from one shared ComponentPool<RatTrap> (trapPrefab, under "Rat Traps"), never instantiated per throw.
 * - Range is the stats' range (Turret.Range uses it for the range ring and the build ghost). No upgrades yet (user).
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
    [Tooltip("The model that turns toward where it throws (Body).")]
    [SerializeField] private Transform body;
    [Tooltip("Where the trap leaves the cat (a child of the body, at its paws).")]
    [SerializeField] private Transform hand;
    [SerializeField, Min(0)] private int prewarm = 8;

    private Turret turret;
    private float cooldown;
    private Vector3 facing = Vector3.forward;
    private readonly List<Vector3> candidates = new List<Vector3>();

    public float Range => stats != null ? stats.Range : 0f;
    public TrapperStats Stats => stats;
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
        if (turret != null)
        {
            facing = turret.RestDirection;
        }
        if (body != null)
        {
            body.rotation = Quaternion.LookRotation(facing);
        }
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
        if (stats == null || trapPrefab == null)
        {
            return;
        }
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
        Turn();
        cooldown -= Time.deltaTime;
        if (cooldown > 0f || LiveTraps >= stats.MaxTraps)
        {
            return;
        }
        Vector3 spot;
        if (!FindSpot(out spot))
        {
            return;
        }
        Throw(spot);
        cooldown = stats.ThrowInterval;
    }

    private void Turn()
    {
        if (body == null)
        {
            return;
        }
        Quaternion wanted = Quaternion.LookRotation(facing);
        body.rotation = Quaternion.RotateTowards(body.rotation, wanted, stats.TurnSpeed * Time.deltaTime);
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

    // The nearest route cell (else Path cell) centre in range that keeps trapSpacing from every set or flying trap.
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
        int bestTraffic = -1;
        bool found = false;
        float sqrRange = stats.Range * stats.Range;
        float sqrSpacing = stats.TrapSpacing * stats.TrapSpacing;
        for (int dx = -reach; dx <= reach; dx++)
        {
            for (int dy = -reach; dy <= reach; dy++)
            {
                Vector2Int cell = new Vector2Int(home.x + dx, home.y + dy);
                int traffic = 0;
                if (onRoute ? !route.TryGetValue(cell, out traffic) || grid.GetState(cell) == CellState.Base : grid.GetState(cell) != CellState.Path)
                {
                    continue;
                }
                Vector3 p = grid.CellToWorld(cell);
                p.y = 0f;
                Vector3 offset = p - centre;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr > sqrRange || traffic < bestTraffic || (traffic == bestTraffic && sqr >= best) || Crowded(p, sqrSpacing))
                {
                    continue;
                }
                best = sqr;
                bestTraffic = traffic;
                spot = p;
                found = true;
            }
        }
        return found;
    }

    private static bool Crowded(Vector3 p, float sqrSpacing)
    {
        List<RatTrap> active = RatTrap.Active;
        for (int i = 0; i < active.Count; i++)
        {
            Vector3 offset = active[i].Spot - p;
            offset.y = 0f;
            if (offset.sqrMagnitude < sqrSpacing)
            {
                return true;
            }
        }
        return false;
    }

    private void Throw(Vector3 spot)
    {
        Vector3 flat = spot - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
        {
            facing = flat.normalized;
        }
        if (body != null)
        {
            body.rotation = Quaternion.LookRotation(facing);
        }
        ComponentPool<RatTrap> pool = Pool();
        Vector3 start = hand != null ? hand.position : transform.position + Vector3.up * 0.6f;
        RatTrap trap = pool.Get(start, Quaternion.identity);
        trap.Launch(pool, start, spot, stats, this);
        Thrown++;
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
