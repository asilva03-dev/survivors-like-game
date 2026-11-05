/*
 * EnemyMovementSystem
 * Moves every active enemy toward its target (Enemy.Target: the player or the base, by the light rule) and
 * keeps them from overlapping, using separation steering instead of physics. One component handles all enemies.
 *
 * - Each frame: enemies are bucketed into a spatial grid so neighbor checks stay cheap, each enemy
 *   gets a seek velocity toward its target plus a push away from nearby enemies, and all moves are
 *   computed before any are applied so processing order does not bias the crowd.
 * - A short hard pass then pushes apart any pair closer than minSpacing and keeps enemies outside
 *   stopDistance of their target's edge (stopDistance + Damageable.TargetRadius, so they stop around a big
 *   base instead of walking into it), so enemies do not visibly overlap even in dense crowds.
 * - Spacing comes from the shared EnemySettings asset; speed from the enemy's own EnemyStats when it has one
 *   (EnemySettings.MoveSpeed otherwise). An enemy whose WantsToMove is false (a ranged enemy shooting) stands
 *   still but is still pushed apart from neighbours.
 * - Each enemy's Visual (the model child) turns toward its target at turnSpeed; the root never rotates.
 * - Knocked-back enemies (Enemy.Knockback) stop seeking and add their push step to the move.
 * - Enemies keep their own Y. They need a kinematic Rigidbody (cheap collider moves) and live on the
 *   "Enemy" layer, whose collisions with the default layer are disabled here so a crowd cannot trap
 *   the player. Projectile hit checks still see them.
 * - An enemy without a target yet falls back to the player (found by the "Player" tag on Start).
 * - Pathing (TD layer): an enemy whose target is a base follows the FlowField (blended cell direction) instead of
 *   seeking straight, and faces where it walks; where the field has no direction (inside the base cells, outside the
 *   grid, no route) it seeks straight as before. Enemies chasing the player still seek straight.
 * - Walls: after all moves, every enemy is pushed out of blocked grid cells (Wall / BuildStrip / Blocked) as a circle of
 *   wallRadius, so enemies slide along walls and never cross them (knockback and crowd pushes included).
 * - An enemy buried deep inside a blocked area with no open side next to it (e.g. it spawned inside a chunk's blocked
 *   ground) is moved to the nearest walkable cell (within 12 cells) instead of staying stuck.
 * - Hunters (Enemy.IsHunter) never enter the BaseLight: after all moves they are kept hunterLightGap outside its
 *   current radius (the edge works like a wall; a growing light pushes them out). A retreating hunter
 *   (Enemy.HasMoveGoal) walks straight to its MoveGoal instead of its target.
 * - Map ground (map slice step 4, 2026-10-29; only when a MapGround exists): after the moves above, an enemy is pushed
 *   out of every map pixel around it that it can't walk into from the pixel it stands on (higher ground across a cliff,
 *   the map edge) as a circle of wallRadius, so it slides along cliffs. Lower ground (MapGround.CanDrop) is open: it
 *   walks off the ledge and falls (fallGravity) until it lands; walking up or down ramps follows the ground at once.
 *   Diagonal neighbours count as open when either straight route is. The enemy's feet (root minus
 *   Enemy.RootAboveFeet) are put on the ground.
 * - Map routes (step 5, only with a MapRoutes): an enemy going for the player, or for the base where the base grid's
 *   flow field gives no direction, walks straight while the straight line to its target crosses no cliff; otherwise it
 *   follows MapRoutes toward the next map pixel (around cliffs, up ramps). Retreating hunters still walk straight.
 */

using System.Collections.Generic;
using UnityEngine;

public class EnemyMovementSystem : MonoBehaviour
{
    private const int HardPasses = 2;
    private const float GoldenAngle = 2.399963f;

    [SerializeField] private EnemySettings settings;
    [Tooltip("Enemy body radius used to keep enemies out of wall cells.")]
    [SerializeField, Min(0f)] private float wallRadius = 0.4f;
    [Tooltip("Hunters stay this far outside the base light's edge (their body radius).")]
    [SerializeField, Min(0f)] private float hunterLightGap = 0.5f;
    [Tooltip("Downward acceleration of an enemy that walked off a ledge (units/s²).")]
    [SerializeField, Min(0f)] private float fallGravity = 30f;

    private readonly Dictionary<int, int> cellHead = new Dictionary<int, int>();
    private readonly List<int> neighbors = new List<int>(32);
    private Vector2[] positions = new Vector2[64];
    private Vector2[] moved = new Vector2[64];
    private Vector2[] goals = new Vector2[64];
    private float[] stops = new float[64];
    private bool[] followsField = new bool[64];
    // 0 = no map route, 1 = route to the player, 2 = route to the base (MapRoutes).
    private byte[] routeTo = new byte[64];
    private Vector2[] faces = new Vector2[64];
    private int[] nextInCell = new int[64];

    private Transform player;

    private void Awake()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            Physics.IgnoreLayerCollision(enemyLayer, 0, true);
        }
    }

    private void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            player = found.transform;
        }
    }

    private void Update()
    {
        List<Enemy> enemies = Enemy.Active;
        int count = enemies.Count;
        if (player == null || settings == null || count == 0)
        {
            return;
        }

        EnsureCapacity(count);

        float cellSize = Mathf.Max(settings.SeparationRadius, settings.MinSpacing);
        FlowField field = FlowField.Instance;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = enemies[i];
            Damageable target = enemy.Target;
            if (enemy.HasMoveGoal)
            {
                goals[i] = new Vector2(enemy.MoveGoal.x, enemy.MoveGoal.z);
                stops[i] = 0f;
                followsField[i] = false;
                routeTo[i] = 0;
                continue;
            }
            Vector3 goal = target != null ? target.transform.position : player.position;
            goals[i] = new Vector2(goal.x, goal.z);
            stops[i] = settings.StopDistance + (target != null ? target.TargetRadius : 0f);
            followsField[i] = field != null && target is BaseStructure;
            routeTo[i] = (byte)(target is BaseStructure ? 2 : 1);
        }

        BucketEnemies(enemies, count, cellSize);
        ComputeMoves(count, cellSize, field);

        for (int pass = 0; pass < HardPasses; pass++)
        {
            ResolveOverlaps(count, cellSize);
        }

        BuildGrid walls = field != null ? field.Grid : global::BuildGrid.Instance;
        BaseLight baseLight = BaseLight.Instance;
        MapGround ground = MapGround.Instance;
        if (ground != null && !ground.HasData)
        {
            ground = null;
        }
        for (int i = 0; i < count; i++)
        {
            if (walls != null)
            {
                moved[i] = PushOutOfWalls(walls, positions[i], moved[i]);
            }
            if (baseLight != null && enemies[i].IsHunter)
            {
                moved[i] = KeepOutOfLight(baseLight, positions[i], moved[i]);
            }

            Transform t = enemies[i].transform;
            float y = t.position.y;
            if (ground != null)
            {
                moved[i] = KeepOffCliffs(ground, positions[i], moved[i]);
                y = GroundHeight(ground, enemies[i], moved[i], y);
            }
            t.position = new Vector3(moved[i].x, y, moved[i].y);
            FaceTarget(enemies[i].Visual, moved[i], faces[i]);
        }
    }

    // Root height with the feet on the ground (Enemy.RootAboveFeet): up/down ramps at once, off a ledge as a fall
    // (Enemy.FallSpeed) until landing.
    private float GroundHeight(MapGround ground, Enemy enemy, Vector2 position, float y)
    {
        float floor = ground.HeightAt(new Vector3(position.x, 0f, position.y)) + enemy.RootAboveFeet;
        if (y <= floor + 0.01f || Time.deltaTime <= 0f)
        {
            enemy.FallSpeed = 0f;
            return floor;
        }
        // Only a real drop falls; a small step down (walking down a ramp) follows the ground.
        if (enemy.FallSpeed <= 0f && y - floor < 0.5f)
        {
            return floor;
        }
        enemy.FallSpeed += fallGravity * Time.deltaTime;
        return Mathf.Max(floor, y - enemy.FallSpeed * Time.deltaTime);
    }

    // Pushes the enemy (a circle of wallRadius) out of every neighbouring map pixel it can't walk into from the pixel
    // it stood on (`from`): higher ground across a cliff or off the map. Lower ground (a drop) and same-height edges are open.
    private Vector2 KeepOffCliffs(MapGround ground, Vector2 from, Vector2 position)
    {
        Vector2Int standing = ground.WorldToPixel(new Vector3(from.x, 0f, from.y));
        if (!ground.InBounds(standing))
        {
            return position;
        }
        float unit = ground.UnitsPerPixel;
        Vector3 southWest = ground.SouthWest;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }
                    Vector2Int pixel = new Vector2Int(standing.x + dx, standing.y + dy);
                    if (!BlocksFrom(ground, standing, pixel))
                    {
                        continue;
                    }
                    Vector2 min = new Vector2(southWest.x + pixel.x * unit, southWest.z + pixel.y * unit);
                    position = PushOutOfRect(from, position, min, min + new Vector2(unit, unit));
                }
            }
        }
        return position;
    }

    private static bool Passable(MapGround ground, Vector2Int from, Vector2Int to)
    {
        return ground.CanStep(from, to) || ground.CanDrop(from, to);
    }

    // Whether `pixel` (one of the 8 around `standing`) can't be entered from `standing`.
    private static bool BlocksFrom(MapGround ground, Vector2Int standing, Vector2Int pixel)
    {
        if (!ground.InBounds(pixel))
        {
            return true;
        }
        int dx = pixel.x - standing.x;
        int dy = pixel.y - standing.y;
        if (dx == 0 || dy == 0)
        {
            return !Passable(ground, standing, pixel);
        }
        Vector2Int sideX = new Vector2Int(standing.x + dx, standing.y);
        Vector2Int sideY = new Vector2Int(standing.x, standing.y + dy);
        bool viaX = Passable(ground, standing, sideX) && Passable(ground, sideX, pixel);
        bool viaY = Passable(ground, standing, sideY) && Passable(ground, sideY, pixel);
        return !viaX && !viaY;
    }

    // Circle of wallRadius out of a rectangle; a centre inside leaves on the side `from` is on (the shallower if two).
    private Vector2 PushOutOfRect(Vector2 from, Vector2 position, Vector2 min, Vector2 max)
    {
        Vector2 closest = new Vector2(Mathf.Clamp(position.x, min.x, max.x), Mathf.Clamp(position.y, min.y, max.y));
        Vector2 offset = position - closest;
        float sqr = offset.sqrMagnitude;
        if (sqr > 0.000001f)
        {
            if (sqr >= wallRadius * wallRadius)
            {
                return position;
            }
            return closest + offset / Mathf.Sqrt(sqr) * wallRadius;
        }

        float best = float.MaxValue;
        Vector2 result = position;
        if (from.x < min.x && position.x - min.x < best) { best = position.x - min.x; result = new Vector2(min.x - wallRadius, position.y); }
        if (from.x > max.x && max.x - position.x < best) { best = max.x - position.x; result = new Vector2(max.x + wallRadius, position.y); }
        if (from.y < min.y && position.y - min.y < best) { best = position.y - min.y; result = new Vector2(position.x, min.y - wallRadius); }
        if (from.y > max.y && max.y - position.y < best) { best = max.y - position.y; result = new Vector2(position.x, max.y + wallRadius); }
        return best < float.MaxValue ? result : from;
    }

    // Hunters never enter the base light: a centre inside Radius + hunterLightGap is put back on that circle.
    private Vector2 KeepOutOfLight(BaseLight baseLight, Vector2 from, Vector2 to)
    {
        Vector3 c = baseLight.Center;
        Vector2 center = new Vector2(c.x, c.z);
        float limit = baseLight.Radius + hunterLightGap;
        Vector2 offset = to - center;
        if (offset.sqrMagnitude >= limit * limit)
        {
            return to;
        }
        if (offset.sqrMagnitude < 0.0001f)
        {
            offset = from - center;
        }
        if (offset.sqrMagnitude < 0.0001f)
        {
            offset = Vector2.up;
        }
        return center + offset.normalized * limit;
    }

    // Treats the enemy as a circle of wallRadius and pushes it out of every blocked cell it overlaps.
    // `from` is where it was before this frame's move, so a centre shoved inside a wall leaves the way it came.
    private Vector2 PushOutOfWalls(BuildGrid grid, Vector2 from, Vector2 position)
    {
        float size = grid.CellSize;
        Vector3 origin = grid.Origin;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            Vector2Int center = grid.WorldToCell(new Vector3(position.x, 0f, position.y));
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!IsBlocked(grid, cell))
                    {
                        continue;
                    }

                    Vector2 min = new Vector2(origin.x + cell.x * size, origin.z + cell.y * size);
                    Vector2 max = min + new Vector2(size, size);
                    position = PushOutOfBox(grid, cell, from, position, min, max);
                }
            }
        }

        // Buried deep inside a blocked area (e.g. spawned there), with no open side to leave through:
        // move to the nearest walkable cell instead of staying stuck.
        Vector2Int final = grid.WorldToCell(new Vector3(position.x, 0f, position.y));
        if (IsBlocked(grid, final))
        {
            Vector2Int free;
            if (FindNearestWalkable(grid, final, BuriedSearchRange, out free))
            {
                Vector3 center = grid.CellToWorld(free);
                position = new Vector2(center.x, center.z);
            }
        }
        return position;
    }

    private const int BuriedSearchRange = 12;

    // Closest Free/Path cell by ring distance (Chebyshev rings, nearest by real distance within the first ring found).
    private static bool FindNearestWalkable(BuildGrid grid, Vector2Int start, int range, out Vector2Int result)
    {
        for (int ring = 1; ring <= range; ring++)
        {
            float best = float.MaxValue;
            result = start;
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring)
                    {
                        continue;
                    }
                    Vector2Int cell = new Vector2Int(start.x + dx, start.y + dy);
                    if (!grid.IsWalkable(cell))
                    {
                        continue;
                    }
                    float distance = dx * dx + dy * dy;
                    if (distance < best)
                    {
                        best = distance;
                        result = cell;
                    }
                }
            }
            if (best < float.MaxValue)
            {
                return true;
            }
        }
        result = start;
        return false;
    }

    private static bool IsBlocked(BuildGrid grid, Vector2Int cell)
    {
        CellState state = grid.GetState(cell);
        return state == CellState.Wall || state == CellState.BuildStrip || state == CellState.Blocked;
    }

    private Vector2 PushOutOfBox(BuildGrid grid, Vector2Int cell, Vector2 from, Vector2 position, Vector2 min,
        Vector2 max)
    {
        Vector2 closest = new Vector2(Mathf.Clamp(position.x, min.x, max.x), Mathf.Clamp(position.y, min.y, max.y));
        Vector2 offset = position - closest;
        float sqr = offset.sqrMagnitude;

        if (sqr > 0.000001f)
        {
            if (sqr >= wallRadius * wallRadius)
            {
                return position;
            }
            float length = Mathf.Sqrt(sqr);
            return closest + offset / length * wallRadius;
        }

        // Centre inside the box: leave through a side with no blocked neighbour (never into the next wall cell,
        // which would push it straight back), preferring the side it came from, then the shallowest.
        float best = float.MaxValue;
        Vector2 result = position;
        TrySide(grid, cell + Vector2Int.left, position.x - min.x, from.x < min.x,
            new Vector2(min.x - wallRadius, position.y), ref best, ref result);
        TrySide(grid, cell + Vector2Int.right, max.x - position.x, from.x > max.x,
            new Vector2(max.x + wallRadius, position.y), ref best, ref result);
        TrySide(grid, cell + Vector2Int.down, position.y - min.y, from.y < min.y,
            new Vector2(position.x, min.y - wallRadius), ref best, ref result);
        TrySide(grid, cell + Vector2Int.up, max.y - position.y, from.y > max.y,
            new Vector2(position.x, max.y + wallRadius), ref best, ref result);
        return result;
    }

    private static void TrySide(BuildGrid grid, Vector2Int neighbour, float depth, bool cameFromHere, Vector2 exit,
        ref float best, ref Vector2 result)
    {
        if (IsBlocked(grid, neighbour))
        {
            return;
        }

        float cost = cameFromHere ? depth - 1000f : depth;
        if (cost < best)
        {
            best = cost;
            result = exit;
        }
    }

    private void FaceTarget(Transform visual, Vector2 from, Vector2 target)
    {
        if (visual == null)
        {
            return;
        }

        Vector2 toTarget = target - from;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion wanted = Quaternion.LookRotation(new Vector3(toTarget.x, 0f, toTarget.y));
        visual.rotation = Quaternion.RotateTowards(visual.rotation, wanted, settings.TurnSpeed * Time.deltaTime);
    }

    private void BucketEnemies(List<Enemy> enemies, int count, float cellSize)
    {
        cellHead.Clear();

        for (int i = 0; i < count; i++)
        {
            Vector3 p = enemies[i].transform.position;
            positions[i] = new Vector2(p.x, p.z);

            int key = CellKey(positions[i], cellSize);
            nextInCell[i] = cellHead.TryGetValue(key, out int head) ? head : -1;
            cellHead[key] = i;
        }
    }

    private void ComputeMoves(int count, float cellSize, FlowField field)
    {
        float dt = Time.deltaTime;
        MapRoutes routes = MapRoutes.Instance;
        float radius = settings.SeparationRadius;
        float sqrRadius = radius * radius;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = Enemy.Active[i];
            Vector2 toTarget = goals[i] - positions[i];
            float distance = toTarget.magnitude;
            Vector2 seek = Vector2.zero;
            faces[i] = goals[i];

            if (!enemy.IsKnockedBack && enemy.WantsToMove && distance > stops[i] && dt > 0f)
            {
                float moveSpeed = enemy.Stats != null ? enemy.Stats.MoveSpeed : settings.MoveSpeed;
                float speed = Mathf.Min(moveSpeed, (distance - stops[i]) / dt);
                Vector2 direction = toTarget / distance;
                bool routed = false;

                if (followsField[i])
                {
                    Vector3 flow = field.GetDirection(new Vector3(positions[i].x, 0f, positions[i].y));
                    if (flow != Vector3.zero)
                    {
                        direction = new Vector2(flow.x, flow.z);
                        faces[i] = positions[i] + direction;
                        routed = true;
                    }
                }

                // Outside the base grid's flow field: map route around cliffs (straight when the line is clear).
                Vector3 routeDirection;
                if (!routed && routes != null && routeTo[i] != 0
                    && routes.TryGetDirection(new Vector3(positions[i].x, 0f, positions[i].y),
                        new Vector3(goals[i].x, 0f, goals[i].y), routeTo[i] == 1, out routeDirection))
                {
                    direction = new Vector2(routeDirection.x, routeDirection.z);
                    faces[i] = positions[i] + direction;
                }

                seek = direction * speed;
            }

            Vector2 push = Vector2.zero;
            GatherNeighbors(i, cellSize);

            for (int n = 0; n < neighbors.Count; n++)
            {
                int j = neighbors[n];
                Vector2 offset = positions[i] - positions[j];
                float sqr = offset.sqrMagnitude;
                if (sqr >= sqrRadius)
                {
                    continue;
                }

                float length = Mathf.Sqrt(sqr);
                Vector2 away = length > 0.0001f ? offset / length : Fallback(i, j);
                push += away * (1f - length / radius);
            }

            push = Vector2.ClampMagnitude(push, 1f) * settings.SeparationStrength;
            moved[i] = positions[i] + (seek + push) * dt + enemy.TakeKnockbackStep(dt);
        }
    }

    private void ResolveOverlaps(int count, float cellSize)
    {
        float minSpacing = settings.MinSpacing;
        float sqrMin = minSpacing * minSpacing;

        for (int i = 0; i < count; i++)
        {
            GatherNeighbors(i, cellSize);

            for (int n = 0; n < neighbors.Count; n++)
            {
                int j = neighbors[n];
                if (j <= i)
                {
                    continue;
                }

                Vector2 offset = moved[i] - moved[j];
                float sqr = offset.sqrMagnitude;
                if (sqr >= sqrMin)
                {
                    continue;
                }

                float length = Mathf.Sqrt(sqr);
                Vector2 away = length > 0.0001f ? offset / length : Fallback(i, j);
                Vector2 correction = away * ((minSpacing - length) * 0.5f);
                moved[i] += correction;
                moved[j] -= correction;
            }
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = moved[i] - goals[i];
            float length = offset.magnitude;
            if (length < stops[i])
            {
                Vector2 away = length > 0.0001f ? offset / length : Fallback(i, count);
                moved[i] = goals[i] + away * stops[i];
            }
        }
    }

    private void GatherNeighbors(int index, float cellSize)
    {
        neighbors.Clear();

        int cx = Mathf.FloorToInt(positions[index].x / cellSize);
        int cz = Mathf.FloorToInt(positions[index].y / cellSize);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!cellHead.TryGetValue(Hash(cx + dx, cz + dz), out int j))
                {
                    continue;
                }

                for (; j >= 0; j = nextInCell[j])
                {
                    if (j != index)
                    {
                        neighbors.Add(j);
                    }
                }
            }
        }
    }

    private static int CellKey(Vector2 position, float cellSize)
    {
        return Hash(Mathf.FloorToInt(position.x / cellSize), Mathf.FloorToInt(position.y / cellSize));
    }

    private static int Hash(int x, int z)
    {
        return (x * 73856093) ^ (z * 19349663);
    }

    private static Vector2 Fallback(int a, int b)
    {
        float angle = (a + b) * GoldenAngle;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return a < b ? direction : -direction;
    }

    private void EnsureCapacity(int count)
    {
        if (positions.Length >= count)
        {
            return;
        }

        int size = Mathf.NextPowerOfTwo(count);
        System.Array.Resize(ref positions, size);
        System.Array.Resize(ref moved, size);
        System.Array.Resize(ref nextInCell, size);
        System.Array.Resize(ref goals, size);
        System.Array.Resize(ref stops, size);
        System.Array.Resize(ref followsField, size);
        System.Array.Resize(ref routeTo, size);
        System.Array.Resize(ref faces, size);
    }
}
