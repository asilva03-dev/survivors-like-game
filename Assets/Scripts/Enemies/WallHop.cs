/*
 * WallHop
 * The athletic rat's vault over the base walls (2026-11-21, new rats step 4). On the prefab root of a runner
 * (EnemyStats.jumpsWalls), next to RatRunCycle, which blends into its single-arm vault pose by Vault.
 *
 * - User (2026-11-21): a vault goes straight across a wall from one side to the other, then the rat carries on to the
 *   base; it never rides along a wall. So a vault is a committed move, not a passive lift:
 *   - Trigger: while running, a Wall cell within triggerDistance ahead on its way to its target (straight line).
 *   - Crossing: of the grid's 4 straight directions that head toward the target, the one that clears the wall in the
 *     fewest cells (scanning up to maxCells over Wall and BuildStrip cells; it lands right after the last Wall cell, so a
 *     wall + strip + wall is one vault). Lands landingMargin past the wall, keeping its sideways position (straight
 *     across). If no straight direction works (a wall corner ahead), it crosses along its own line of travel instead.
 *   - Leap: from where it stands to the landing point at its move speed x speedFactor, an arc of `height` (world units,
 *     the fence is 1.5) that plateaus on long crossings; the model is lifted (model = Visual > RatModel), the Visual faces
 *     across the wall, and the root is placed on the line each LateUpdate (after EnemyMovementSystem, so its seek,
 *     separation and knockback don't steer a rat mid-vault). Afterwards it resumes its beeline.
 * - Grind (user, 2026-11-21: "like they are grinding the fence"): when its line to the target meets the fence at less than
 *   grindAngle and the fence runs on along its way for minGrindCells, it hops up onto the fence instead (Mount), slides
 *   along its line at move speed x grindSpeedFactor with its feet at grindHeight (Grind), and hops off (Dismount) when the
 *   fence ends (forward past the end) or the target is more than grindExitAngle off the fence's direction (sideways, onto
 *   the target's side if that's open ground). It never steers toward a fence: only a fence already on its line is ridden.
 * - Vault / Grind (0-1 pose blends for RatRunCycle) and Lift for tests. Everything resets when disabled (pooling).
 */

using UnityEngine;

public class WallHop : MonoBehaviour
{
    private static readonly Vector2Int[] Straight =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    [Tooltip("The model child to lift (Visual > RatModel).")]
    [SerializeField] private Transform model;
    [Tooltip("Top of the vault arc (world units; the fence is 1.5 tall).")]
    [SerializeField, Min(0f)] private float height = 1.35f;
    [Tooltip("A wall this close ahead (world units) starts a vault.")]
    [SerializeField, Min(0.1f)] private float triggerDistance = 0.6f;
    [Tooltip("How far past the last wall cell it lands (world units).")]
    [SerializeField, Min(0f)] private float landingMargin = 0.45f;
    [Tooltip("Longest crossing it will vault in one go (cells of wall + strip).")]
    [SerializeField, Range(1, 10)] private int maxCells = 6;
    [Tooltip("Vault speed as a share of its move speed.")]
    [SerializeField, Min(0.1f)] private float speedFactor = 1f;

    [Header("Grind (riding along a fence on its way)")]
    [Tooltip("Grinds instead of vaulting when its line to the target meets the fence at less than this angle (degrees).")]
    [SerializeField, Range(0f, 60f)] private float grindAngle = 30f;
    [Tooltip("...and the fence runs on along its way for at least this many cells.")]
    [SerializeField, Range(1, 6)] private int minGrindCells = 2;
    [Tooltip("Hops off sideways once the target is more than this angle off the fence's direction (degrees).")]
    [SerializeField, Range(10f, 90f)] private float grindExitAngle = 50f;
    [Tooltip("Height of its feet on the fence (world units; the fence is 1.5 tall).")]
    [SerializeField, Min(0f)] private float grindHeight = 1.55f;
    [Tooltip("Grind speed as a share of its move speed.")]
    [SerializeField, Min(0.1f)] private float grindSpeedFactor = 1.25f;
    [Tooltip("Extra arc of the hop up onto and down off the fence (world units).")]
    [SerializeField, Min(0f)] private float grindHopArc = 0.35f;

    private enum Move { None, Vault, Mount, Grind, Dismount }

    private Enemy enemy;
    private Vector3 restPosition;
    private bool cached;
    private Move move;
    private Vector3 from;
    private Vector3 to;
    private float fromLift;
    private float toLift;
    private float arc;
    private float duration;
    private float elapsed;
    private Vector3 axis;
    private float rail;

    /// <summary>Current lift of the model above the ground (world units).</summary>
    public float Lift { get; private set; }
    /// <summary>Vault pose blend: 0 on the ground, 1 at the top of a vault (RatRunCycle).</summary>
    public float Vault { get; private set; }
    /// <summary>Grind pose blend: 1 while riding a fence, eased in/out over the hops on and off (RatRunCycle).</summary>
    public float Grind { get; private set; }
    public bool IsVaulting => move != Move.None;
    public bool IsGrinding => move == Move.Mount || move == Move.Grind || move == Move.Dismount;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        if (model != null)
        {
            restPosition = model.localPosition;
            cached = true;
        }
    }

    private void OnDisable()
    {
        move = Move.None;
        Lift = 0f;
        Vault = 0f;
        Grind = 0f;
        if (cached)
        {
            model.localPosition = restPosition;
        }
    }

    private void LateUpdate()
    {
        if (!cached || enemy == null)
        {
            return;
        }
        BuildGrid grid = BuildGrid.Instance;
        if (move == Move.None && grid != null)
        {
            TryStart(grid);
        }
        switch (move)
        {
            case Move.Grind:
                StepGrind(grid);
                break;
            case Move.None:
                Lift = 0f;
                Vault = 0f;
                Grind = 0f;
                break;
            default:
                StepLeap();
                break;
        }
        float parentScale = model.parent != null ? model.parent.lossyScale.y : 1f;
        model.localPosition = restPosition + Vector3.up * (Lift / Mathf.Max(parentScale, 0.0001f));
    }

    private float Speed(float factor)
    {
        return Mathf.Max(0.1f, (enemy.Stats != null ? enemy.Stats.MoveSpeed : 4f) * factor);
    }

    private void StartLeap(Move kind, Vector3 start, Vector3 end, float startLift, float endLift, float arcHeight, float speed)
    {
        move = kind;
        from = start;
        to = end;
        fromLift = startLift;
        toLift = endLift;
        arc = arcHeight;
        Vector3 across = end - start;
        across.y = 0f;
        duration = Mathf.Max(0.15f, across.magnitude / speed);
        elapsed = 0f;
        Face(kind == Move.Vault ? across : axis);
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (enemy.Visual != null && direction.sqrMagnitude > 0.0001f)
        {
            enemy.Visual.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    // Vault, hop onto the fence, hop off: a straight move with an arc between two lifts.
    private void StepLeap()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Vector3 point = Vector3.Lerp(from, to, t);
        point.y = transform.position.y;
        transform.position = point;
        float bump = move == Move.Vault ? Mathf.Min(1f, 1.6f * Mathf.Sin(t * Mathf.PI)) : Mathf.Sin(t * Mathf.PI);
        Lift = Mathf.Lerp(fromLift, toLift, t) + arc * bump;
        Vault = move == Move.Vault && height > 0f ? Mathf.Clamp01(Lift / height) : 0f;
        Grind = move == Move.Mount ? t : move == Move.Dismount ? 1f - t : 0f;
        if (move != Move.Vault)
        {
            Face(axis);
        }
        if (t >= 1f)
        {
            move = move == Move.Mount ? Move.Grind : Move.None;
            if (move == Move.None)
            {
                Lift = 0f;
                Vault = 0f;
                Grind = 0f;
            }
        }
    }

    // Riding the fence: along its line, on the rail, until it ends or the target turns away; then hop off.
    private void StepGrind(BuildGrid grid)
    {
        Lift = grindHeight;
        Vault = 0f;
        Grind = 1f;
        Face(axis);
        Vector3 position = transform.position;
        Vector3 next = OnRail(position + axis * Speed(grindSpeedFactor) * Time.deltaTime);
        next.y = position.y;

        Damageable target = enemy.Target;
        Vector3 toTarget = target != null ? target.transform.position - next : axis;
        toTarget.y = 0f;
        bool turnedAway = toTarget.sqrMagnitude > 0.0001f
            && Vector3.Dot(toTarget.normalized, axis) < Mathf.Cos(grindExitAngle * Mathf.Deg2Rad);
        bool railEnds = grid == null
            || grid.GetState(grid.WorldToCell(next + axis * (grid.CellSize * 0.5f))) != CellState.Wall;

        if (turnedAway || railEnds)
        {
            Vector3 landing;
            if (FindDismount(grid, next, toTarget, railEnds, out landing))
            {
                transform.position = next;
                StartLeap(Move.Dismount, next, landing, grindHeight, 0f, grindHopArc, Speed(grindSpeedFactor));
                return;
            }
            if (railEnds)
            {
                // Nowhere to land: drop off where it is and let the beeline take over.
                move = Move.None;
                Lift = 0f;
                Grind = 0f;
                return;
            }
        }
        transform.position = next;
    }

    private Vector3 OnRail(Vector3 point)
    {
        if (Mathf.Abs(axis.x) > 0.5f)
        {
            point.z = rail;
        }
        else
        {
            point.x = rail;
        }
        return point;
    }

    // Off the fence: sideways onto the target's side (else the other side), or forward past the end of the rail first
    // when the rail ends.
    private bool FindDismount(BuildGrid grid, Vector3 point, Vector3 toTarget, bool railEnds, out Vector3 landing)
    {
        landing = point;
        if (grid == null)
        {
            return false;
        }
        Vector3 side = new Vector3(-axis.z, 0f, axis.x);
        float sign = Vector3.Dot(toTarget, side) >= 0f ? 1f : -1f;
        float offset = grid.CellSize * 0.5f + landingMargin;
        Vector3[] options =
        {
            point + side * (sign * offset) + axis * 0.3f,
            point + side * (-sign * offset) + axis * 0.3f,
            point + axis * offset
        };
        int first = railEnds ? 2 : 0;
        for (int n = 0; n < options.Length; n++)
        {
            Vector3 candidate = options[(first + n) % options.Length];
            CellState state = grid.GetState(grid.WorldToCell(candidate));
            if (state != CellState.Wall && state != CellState.OutOfBounds)
            {
                landing = candidate;
                landing.y = point.y;
                return true;
            }
        }
        return false;
    }

    private void TryStart(BuildGrid grid)
    {
        Damageable target = enemy.Target;
        if (target == null)
        {
            return;
        }
        Vector3 position = transform.position;
        Vector3 toTarget = target.transform.position - position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Vector3 travel = toTarget.normalized;
        Vector2Int ahead = grid.WorldToCell(position + travel * triggerDistance);
        if (grid.GetState(ahead) != CellState.Wall)
        {
            return;
        }

        // Grind: the fence runs nearly along its line and goes on for a while.
        Vector2Int step = Mathf.Abs(travel.x) >= Mathf.Abs(travel.z)
            ? new Vector2Int(travel.x >= 0f ? 1 : -1, 0)
            : new Vector2Int(0, travel.z >= 0f ? 1 : -1);
        float along = Mathf.Abs(step.x != 0 ? travel.x : travel.z);
        if (along >= Mathf.Cos(grindAngle * Mathf.Deg2Rad) && RailLength(grid, ahead, step) >= minGrindCells)
        {
            axis = new Vector3(step.x, 0f, step.y);
            Vector3 cell = grid.CellToWorld(ahead);
            rail = step.x != 0 ? cell.z : cell.x;
            Vector3 mount = OnRail(position + axis * triggerDistance);
            mount.y = position.y;
            StartLeap(Move.Mount, position, mount, 0f, grindHeight, grindHopArc, Speed(1f));
            return;
        }

        Vector2Int start = grid.WorldToCell(position);
        Vector3 landing;
        if (!FindStraightCrossing(grid, start, position, travel, out landing)
            && !FindLineCrossing(grid, position, travel, out landing))
        {
            return;
        }
        StartLeap(Move.Vault, position, landing, 0f, 0f, height, Speed(speedFactor));
    }

    private static int RailLength(BuildGrid grid, Vector2Int start, Vector2Int step)
    {
        int length = 0;
        while (length < 64 && grid.GetState(start + step * length) == CellState.Wall)
        {
            length++;
        }
        return length;
    }

    // The straight (grid-axis) crossing toward the target with the fewest cells; lands landingMargin past the last Wall
    // cell, keeping the sideways position.
    private bool FindStraightCrossing(BuildGrid grid, Vector2Int start, Vector3 position, Vector3 travel, out Vector3 landing)
    {
        landing = position;
        int bestCells = int.MaxValue;
        float bestDot = -1f;
        bool found = false;
        for (int k = 0; k < Straight.Length; k++)
        {
            Vector2Int step = Straight[k];
            float dot = step.x * travel.x + step.y * travel.z;
            if (dot <= 0.05f)
            {
                continue;
            }
            int lastWall = 0;
            for (int i = 1; i <= maxCells; i++)
            {
                CellState state = grid.GetState(start + step * i);
                if (state == CellState.Wall)
                {
                    lastWall = i;
                }
                else if (state != CellState.BuildStrip)
                {
                    break;
                }
            }
            // Needs a wall to clear and open (non-wall) ground right after the last one.
            if (lastWall == 0 || grid.GetState(start + step * (lastWall + 1)) == CellState.Wall)
            {
                continue;
            }
            if (lastWall < bestCells || (lastWall == bestCells && dot > bestDot))
            {
                Vector3 wallCenter = grid.CellToWorld(start + step * lastWall);
                float edge = grid.CellSize * 0.5f + landingMargin;
                Vector3 axis = new Vector3(step.x, 0f, step.y);
                // Along the step axis: just past the wall's far edge; sideways: where it is now.
                Vector3 point = position + axis * (Vector3.Dot(wallCenter - position, axis) + edge);
                point.y = position.y;
                if (grid.GetState(grid.WorldToCell(point)) == CellState.Wall)
                {
                    continue;
                }
                bestCells = lastWall;
                bestDot = dot;
                landing = point;
                found = true;
            }
        }
        return found;
    }

    // Fallback (a wall corner ahead): along the line of travel to the first point past the walls.
    private bool FindLineCrossing(BuildGrid grid, Vector3 position, Vector3 travel, out Vector3 landing)
    {
        float stepSize = grid.CellSize * 0.25f;
        float limit = maxCells * grid.CellSize;
        float lastWall = -1f;
        for (float d = stepSize; d <= limit; d += stepSize)
        {
            CellState state = grid.GetState(grid.WorldToCell(position + travel * d));
            if (state == CellState.Wall)
            {
                lastWall = d;
            }
            else if (state != CellState.BuildStrip && lastWall > 0f)
            {
                break;
            }
        }
        landing = position;
        if (lastWall <= 0f || lastWall >= limit)
        {
            return false;
        }
        landing = position + travel * (lastWall + stepSize + landingMargin);
        landing.y = position.y;
        return grid.GetState(grid.WorldToCell(landing)) != CellState.Wall;
    }
}
