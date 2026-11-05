/*
 * EnemySpawner
 * Pooled enemy spawner: where and as what each enemy appears. It no longer spawns on its own (round system step 2,
 * 2026-11-05): RunDirector calls TrySpawn once per enemy at the current wave's rate. The old steady stream
 * (spawnsPerSecond) and the alive cap (maxEnemies) were removed (the cap was a test aid, user).
 *
 * - TrySpawn(rangedChance): one enemy, split between the base and the player as below; false (nothing spawned, counted
 *   in SkippedSpawns) when no valid spot was found, spawning is paused, or there is no player.
 * - Enemies come from ComponentPools built from enemyPrefab (normal) and rangedEnemyPrefab (prewarmed), never
 *   Instantiate/Destroy at runtime. Dead enemies return to the pool they came from via Damageable.Died, and hunters
 *   that retreated off-screen via Enemy.DespawnRequested (no death).
 * - Split: each spawn goes to the player with probability PlayerShare, else to the base. PlayerShare is 0 while the
 *   player is in the BaseLight; in the dark it rises from playerShareMin to playerShareMax over playerShareRampTime
 *   seconds and resets when the player returns to the light. No BaseLight = always the dark.
 * - Player share: spawned at an off-screen spot around the player (minSpawnDistance, stepping outward) that is also
 *   at least lightClearance outside the light; the wave's rangedChance of them are ranged rats (hunters), the rest basic rats
 *   assigned to the player (Enemy.Assignment). Ranged rats only ever come from this share. With a map (MapRoutes) the
 *   spot must also have a walking route to the player, so hills and slopes are fine but sealed-off ground is not.
 * - Base share (user's rule, 2026-10-30): a basic rat assigned to the base, at a random direction around the light,
 *   starting just outside it in the dark (light TargetRadius + baseSpawnGap + up to baseSpawnBand); if that spot is on
 *   screen it is pushed straight outward until just past the camera's view, so it spawns as close to the light as the
 *   camera allows. With a map the spot must have a route to the base.
 * - Every spot must also be off screen, open ground outside the base layout (BuildGrid.IsOpenGroundAt within
 *   groundClearance) and on the map: with a MapGround, at least mapEdgeMargin inside the painted map's edge, with the
 *   enemy's feet put on the ground there (floor, hill or ramp); without one, within mapHalfSize of the base (old
 *   behaviour). If no try passes, nothing spawns this tick (never on screen, never off the map).
 * - Recycling: player-assigned enemies farther than recycleDistance from the player are moved to a fresh
 *   player-share spot (biased within aheadAngle of the player's movement); if none is found they stay where they are.
 *   Base-bound enemies are never recycled (they are walking to the base) and retreating hunters despawn on their own.
 * - SpawnsEnabled pauses/resumes spawning (TrySpawn refuses while paused; the run clock keeps going) and raises
 *   SpawnsEnabledChanged. Enemies already alive keep moving and recycling. DebugKillAll toggles it.
 * - BaseAliveCount / PlayerAliveCount: alive enemies by assignment (debug line).
 * - The player is found by the "Player" tag; the camera is Camera.main.
 */

using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    private const int SpawnAttempts = 12;
    private const float AttemptRadiusStep = 2f;

    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private Enemy rangedEnemyPrefab;
    [Tooltip("Ranged rats made at load (the pool grows if more are needed, with a small hitch).")]
    [SerializeField, Min(0)] private int rangedPrewarmCount = 10;
    [Tooltip("Basic rats made at load (the pool grows if more are needed, with a small hitch).")]
    [SerializeField, Min(1)] private int prewarmCount = 30;

    [Header("Split")]
    [Tooltip("Share of spawns that come for the player the moment they leave the light.")]
    [SerializeField, Range(0f, 1f)] private float playerShareMin = 0.3f;
    [Tooltip("Share reached after playerShareRampTime seconds in the dark.")]
    [SerializeField, Range(0f, 1f)] private float playerShareMax = 0.7f;
    [SerializeField, Min(0.01f)] private float playerShareRampTime = 20f;

    [Header("Spots")]
    [SerializeField, Min(1f)] private float minSpawnDistance = 14f;
    [SerializeField, Min(1f)] private float recycleDistance = 30f;
    [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;
    [SerializeField, Range(0f, 180f)] private float aheadAngle = 60f;
    [Tooltip("A spawn spot must be open ground (no base layout cells) within this radius.")]
    [SerializeField, Min(0f)] private float groundClearance = 0.6f;
    [Tooltip("Player-share spots must be at least this far outside the base light's edge.")]
    [SerializeField, Min(0f)] private float lightClearance = 2f;
    [Tooltip("Base-share spots start this far outside the light's edge...")]
    [SerializeField, Min(0f)] private float baseSpawnGap = 2f;
    [Tooltip("...and spread over this much more.")]
    [SerializeField, Min(0f)] private float baseSpawnBand = 4f;
    [Tooltip("Without a painted map (MapGround): spots must stay within this half size around the base.")]
    [SerializeField, Min(1f)] private float mapHalfSize = 98f;
    [Tooltip("With a painted map: spots must be at least this far inside the map's edge.")]
    [SerializeField, Min(0f)] private float mapEdgeMargin = 2f;
    [Tooltip("A base-share spot on screen is pushed outward in steps of this size until it is off screen.")]
    [SerializeField, Min(0.25f)] private float offscreenPushStep = 1f;

    private readonly Dictionary<Enemy, ComponentPool<Enemy>> alive = new Dictionary<Enemy, ComponentPool<Enemy>>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    private ComponentPool<Enemy> pool;
    private ComponentPool<Enemy> rangedPool;
    private Transform player;
    private Rigidbody playerBody;
    private Camera cam;
    private float darkTime;
    private bool spawnsEnabled = true;

    public event System.Action<bool> SpawnsEnabledChanged;

    /// <summary>Chance that the next spawn comes for the player (0 while the player is in the light).</summary>
    public float PlayerShare { get; private set; }
    public float DarkTime => darkTime;
    public int AliveCount => alive.Count;

    /// <summary>Alive enemies walking to the base.</summary>
    public int BaseAliveCount
    {
        get
        {
            int count = 0;
            foreach (Enemy enemy in alive.Keys)
            {
                if (enemy.Assignment == EnemyAssignment.Base)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>Alive enemies coming for the player (hunters included).</summary>
    public int PlayerAliveCount => alive.Count - BaseAliveCount;

    public bool SpawnsEnabled
    {
        get { return spawnsEnabled; }
        set
        {
            if (spawnsEnabled == value)
            {
                return;
            }

            spawnsEnabled = value;
            SpawnsEnabledChanged?.Invoke(value);
        }
    }

    private void Awake()
    {
        Transform poolRoot = new GameObject("Enemies").transform;
        pool = new ComponentPool<Enemy>(enemyPrefab, poolRoot, prewarmCount);
        if (rangedEnemyPrefab != null)
        {
            rangedPool = new ComponentPool<Enemy>(rangedEnemyPrefab, poolRoot, rangedPrewarmCount);
        }
    }

    private void OnEnable()
    {
        Damageable.Died += HandleDied;
        Enemy.DespawnRequested += HandleDespawn;
    }

    private void OnDisable()
    {
        Damageable.Died -= HandleDied;
        Enemy.DespawnRequested -= HandleDespawn;
    }

    private void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            playerBody = found.GetComponent<Rigidbody>();
        }

        cam = Camera.main;
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        UpdateShare();
        RecycleFarEnemies();
    }

    private void UpdateShare()
    {
        BaseLight baseLight = BaseLight.Instance;
        bool inDark = baseLight == null || !baseLight.IsPlayerInside;
        darkTime = inDark ? darkTime + Time.deltaTime : 0f;
        PlayerShare = inDark
            ? Mathf.Lerp(playerShareMin, playerShareMax, darkTime / playerShareRampTime)
            : 0f;
    }

    /// <summary>Spawns that found no valid spot and were skipped (for testing).</summary>
    public int SkippedSpawns { get; private set; }

    /// <summary>Spawns one enemy (base or player share, see the header). False when nothing spawned.</summary>
    public bool TrySpawn(float rangedChance)
    {
        if (!spawnsEnabled || player == null)
        {
            return false;
        }
        if (cam == null)
        {
            cam = Camera.main;
        }

        Vector3 point;
        if (Random.value < PlayerShare)
        {
            bool ranged = rangedPool != null && Random.value < rangedChance;
            ComponentPool<Enemy> source = ranged ? rangedPool : pool;
            if (!TryFindPlayerSpawnPoint(false, out point))
            {
                SkippedSpawns++;
                return false;
            }
            Enemy hunter = source.Get(point, Quaternion.identity);
            hunter.Assignment = EnemyAssignment.Player;
            alive.Add(hunter, source);
            return true;
        }

        if (!TryFindBaseSpawnPoint(out point))
        {
            SkippedSpawns++;
            return false;
        }
        Enemy enemy = pool.Get(point, Quaternion.identity);
        enemy.Assignment = EnemyAssignment.Base;
        alive.Add(enemy, pool);
        return true;
    }

    private void HandleDied(Damageable target)
    {
        Release(target as Enemy);
    }

    private void HandleDespawn(Enemy enemy)
    {
        Release(enemy);
    }

    private void Release(Enemy enemy)
    {
        ComponentPool<Enemy> source;
        if (enemy != null && alive.TryGetValue(enemy, out source))
        {
            alive.Remove(enemy);
            source.Release(enemy);
        }
    }

    private void RecycleFarEnemies()
    {
        float sqrRecycle = recycleDistance * recycleDistance;
        scratch.Clear();

        foreach (Enemy enemy in alive.Keys)
        {
            if (enemy.Assignment != EnemyAssignment.Player || enemy.IsRetreating)
            {
                continue;
            }
            Vector3 offset = enemy.transform.position - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrRecycle)
            {
                scratch.Add(enemy);
            }
        }

        for (int i = 0; i < scratch.Count; i++)
        {
            Vector3 point;
            if (TryFindPlayerSpawnPoint(true, out point))
            {
                scratch[i].transform.position = point;
                scratch[i].FallSpeed = 0f;
            }
        }
    }

    // Off-screen ring around the player, outside the light (stepping outward each try). False = no valid spot.
    private bool TryFindPlayerSpawnPoint(bool preferAhead, out Vector3 point)
    {
        Vector3 center = player.position;
        Vector3 velocity = playerBody != null ? playerBody.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        bool useAhead = preferAhead && velocity.sqrMagnitude > 0.01f;
        float aheadYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;

        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            float yaw = useAhead
                ? aheadYaw + Random.Range(-aheadAngle, aheadAngle)
                : Random.Range(0f, 360f);
            point = OnRing(center, yaw, minSpawnDistance + attempt * AttemptRadiusStep);
            if (IsGoodSpot(point, true, true))
            {
                point = OnGround(point);
                return true;
            }
        }

        point = center;
        return false;
    }

    // A random direction around the light, just outside it in the dark; pushed straight outward until off screen.
    private bool TryFindBaseSpawnPoint(out Vector3 point)
    {
        BaseLight baseLight = BaseLight.Instance;
        if (baseLight == null)
        {
            return TryFindPlayerSpawnPoint(false, out point);
        }

        Vector3 center = baseLight.Center;
        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            float yaw = Random.Range(0f, 360f);
            float radius = baseLight.TargetRadius + baseSpawnGap + Random.Range(0f, baseSpawnBand);
            point = OnRing(center, yaw, radius);
            for (int push = 0; push < 200 && !IsOffscreen(OnGround(point)) && IsOnMap(point); push++)
            {
                radius += offscreenPushStep;
                point = OnRing(center, yaw, radius);
            }
            if (IsGoodSpot(point, false, false))
            {
                point = OnGround(point);
                return true;
            }
        }

        point = center;
        return false;
    }

    private Vector3 OnRing(Vector3 center, float yawDegrees, float radius)
    {
        float radians = yawDegrees * Mathf.Deg2Rad;
        Vector3 point = center + new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * radius;
        point.y = enemyPrefab.transform.position.y;
        return point;
    }

    // Feet on the painted map's ground (floor, hill or ramp); unchanged without a map.
    private Vector3 OnGround(Vector3 point)
    {
        MapGround ground = MapGround.Instance;
        if (ground != null && ground.HasData)
        {
            point.y = ground.HeightAt(point) + enemyPrefab.RootAboveFeet;
        }
        return point;
    }

    private bool IsOnMap(Vector3 point)
    {
        MapGround ground = MapGround.Instance;
        if (ground != null && ground.HasData)
        {
            return ground.Contains(point, mapEdgeMargin);
        }
        BaseLight baseLight = BaseLight.Instance;
        if (baseLight == null)
        {
            return true;
        }
        Vector3 offset = point - baseLight.BasePosition;
        return Mathf.Abs(offset.x) <= mapHalfSize && Mathf.Abs(offset.z) <= mapHalfSize;
    }

    // towardPlayer: the route that must exist (to the player for the player share, to the base for the base share).
    private bool IsGoodSpot(Vector3 point, bool outsideLight, bool towardPlayer)
    {
        if (!IsOnMap(point))
        {
            return false;
        }
        BaseLight baseLight = BaseLight.Instance;
        if (baseLight != null && outsideLight && baseLight.Contains(point, lightClearance))
        {
            return false;
        }
        if (!IsOffscreen(OnGround(point)) || !BuildGrid.IsOpenGroundAt(point, groundClearance))
        {
            return false;
        }
        MapRoutes routes = MapRoutes.Instance;
        if (routes != null && (towardPlayer ? routes.PlayerRouteReady : routes.BaseRouteReady)
            && routes.GetDistance(point, towardPlayer) == MapRoutes.Unreachable)
        {
            return false;
        }
        return true;
    }

    private bool IsOffscreen(Vector3 point)
    {
        if (cam == null)
        {
            return true;
        }

        Vector3 view = cam.WorldToViewportPoint(point);
        return view.z <= 0f
            || view.x < -offscreenMargin || view.x > 1f + offscreenMargin
            || view.y < -offscreenMargin || view.y > 1f + offscreenMargin;
    }
}
