/*
 * EnemySpawner
 * Pooled enemy spawner and (until the round state machine exists) the spawn director of TD slice 4: one continuous
 * spawn stream (spawnsPerSecond, cap maxEnemies) split between the base and the player.
 *
 * - Enemies come from ComponentPools built from enemyPrefab (normal) and rangedEnemyPrefab (prewarmed), never
 *   Instantiate/Destroy at runtime. Dead enemies return to the pool they came from via Damageable.Died, and hunters
 *   that retreated off-screen via Enemy.DespawnRequested (no death).
 * - Split: each spawn goes to the player with probability PlayerShare, else to the base. PlayerShare is 0 while the
 *   player is in the BaseLight; in the dark it rises from playerShareMin to playerShareMax over playerShareRampTime
 *   seconds and resets when the player returns to the light. No BaseLight = always the dark.
 * - Player share: spawned at an off-screen spot around the player (minSpawnDistance, stepping outward) that is also
 *   at least lightClearance outside the light; rangedChance of them are ranged rats (hunters), the rest basic rats
 *   assigned to the player (Enemy.Assignment). Ranged rats only ever come from this share.
 * - Base share: a basic rat assigned to the base, spawned off-screen in a ring just outside the light
 *   (light TargetRadius + baseSpawnGap + up to baseSpawnBand, stepping outward when a try is visible).
 * - Every spot must also be open ground outside the base layout (BuildGrid.IsOpenGroundAt within groundClearance)
 *   and on the map (mapHalfSize around the base, BaseLight.BasePosition; the light's centre can sit off the base). If no try passes, the last spot is used (the movement
 *   system frees an enemy that lands in a wall).
 * - Recycling: player-assigned enemies farther than recycleDistance from the player are teleported to a fresh
 *   player-share spot (biased within aheadAngle of the player's movement). Base-bound enemies are never recycled
 *   (they are walking to the base) and retreating hunters despawn on their own.
 * - SpawnsEnabled pauses/resumes spawning (the timer resets, so no burst on resume) and raises
 *   SpawnsEnabledChanged. Enemies already alive keep moving and recycling. DebugKillAll toggles it.
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
    [Tooltip("Share of the player's spawns that are ranged rats (hunters). Ranged rats never come from the base share.")]
    [SerializeField, Range(0f, 1f)] private float rangedChance = 0.2f;
    [SerializeField, Min(0)] private int rangedPrewarmCount = 10;
    [SerializeField, Min(0.1f)] private float spawnsPerSecond = 2f;
    [SerializeField, Min(1)] private int maxEnemies = 30;
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
    [Tooltip("Spots must stay within this half size around the light's centre (the ground is 200x200).")]
    [SerializeField, Min(1f)] private float mapHalfSize = 98f;

    private readonly Dictionary<Enemy, ComponentPool<Enemy>> alive = new Dictionary<Enemy, ComponentPool<Enemy>>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    private ComponentPool<Enemy> pool;
    private ComponentPool<Enemy> rangedPool;
    private Transform player;
    private Rigidbody playerBody;
    private Camera cam;
    private float spawnTimer;
    private float darkTime;
    private bool spawnsEnabled = true;

    public event System.Action<bool> SpawnsEnabledChanged;

    /// <summary>Chance that the next spawn comes for the player (0 while the player is in the light).</summary>
    public float PlayerShare { get; private set; }
    public float DarkTime => darkTime;
    public int AliveCount => alive.Count;

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
            spawnTimer = 0f;
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
        float interval = 1f / spawnsPerSecond;

        if (!spawnsEnabled)
        {
            RecycleFarEnemies();
            return;
        }

        spawnTimer += Time.deltaTime;

        if (alive.Count >= maxEnemies)
        {
            spawnTimer = Mathf.Min(spawnTimer, interval);
        }
        else
        {
            while (spawnTimer >= interval && alive.Count < maxEnemies)
            {
                spawnTimer -= interval;
                Spawn();
            }
        }

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

    private void Spawn()
    {
        if (Random.value < PlayerShare)
        {
            bool ranged = rangedPool != null && Random.value < rangedChance;
            ComponentPool<Enemy> source = ranged ? rangedPool : pool;
            Enemy hunter = source.Get(FindPlayerSpawnPoint(false), Quaternion.identity);
            hunter.Assignment = EnemyAssignment.Player;
            alive.Add(hunter, source);
            return;
        }

        Enemy enemy = pool.Get(FindBaseSpawnPoint(), Quaternion.identity);
        enemy.Assignment = EnemyAssignment.Base;
        alive.Add(enemy, pool);
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
            scratch[i].transform.position = FindPlayerSpawnPoint(true);
        }
    }

    // Off-screen ring around the player, outside the light.
    private Vector3 FindPlayerSpawnPoint(bool preferAhead)
    {
        Vector3 center = player.position;
        Vector3 velocity = playerBody != null ? playerBody.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        bool useAhead = preferAhead && velocity.sqrMagnitude > 0.01f;
        float aheadYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;

        Vector3 point = center;
        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            float yaw = useAhead
                ? aheadYaw + Random.Range(-aheadAngle, aheadAngle)
                : Random.Range(0f, 360f);
            point = OnRing(center, yaw, minSpawnDistance + attempt * AttemptRadiusStep);
            if (IsGoodSpot(point, true))
            {
                return point;
            }
        }

        return point;
    }

    // Off-screen ring just outside the light, around its centre.
    private Vector3 FindBaseSpawnPoint()
    {
        BaseLight baseLight = BaseLight.Instance;
        if (baseLight == null)
        {
            return FindPlayerSpawnPoint(false);
        }

        Vector3 point = baseLight.Center;
        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            float radius = baseLight.TargetRadius + baseSpawnGap + Random.Range(0f, baseSpawnBand)
                + attempt * AttemptRadiusStep;
            point = OnRing(baseLight.Center, Random.Range(0f, 360f), radius);
            if (IsGoodSpot(point, false))
            {
                return point;
            }
        }

        return point;
    }

    private Vector3 OnRing(Vector3 center, float yawDegrees, float radius)
    {
        float radians = yawDegrees * Mathf.Deg2Rad;
        Vector3 point = center + new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * radius;
        point.y = enemyPrefab.transform.position.y;
        return point;
    }

    private bool IsGoodSpot(Vector3 point, bool outsideLight)
    {
        BaseLight baseLight = BaseLight.Instance;
        if (baseLight != null)
        {
            Vector3 offset = point - baseLight.BasePosition;
            if (Mathf.Abs(offset.x) > mapHalfSize || Mathf.Abs(offset.z) > mapHalfSize)
            {
                return false;
            }
            if (outsideLight && baseLight.Contains(point, lightClearance))
            {
                return false;
            }
        }
        return IsOffscreen(point) && BuildGrid.IsOpenGroundAt(point, groundClearance);
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
