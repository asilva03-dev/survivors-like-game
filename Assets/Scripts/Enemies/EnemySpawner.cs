/*
 * EnemySpawner
 * Pooled enemy spawner that places enemies just off-screen and lets them walk in, Vampire
 * Survivors style.
 *
 * - Enemies come from ComponentPools built from enemyPrefab (normal) and rangedEnemyPrefab (prewarmed), never
 *   Instantiate/Destroy at runtime. Each spawn picks the ranged type with probability rangedChance (0.2 = an
 *   80/20 mix on average), else the normal one. Dead enemies are returned to the pool they came from via
 *   Damageable.Died.
 * - Spawn rate (spawnsPerSecond) and the cap (maxEnemies) throttle spawning. If the cap is reached
 *   the timer holds, so there is no burst once enemies die.
 * - SpawnsEnabled pauses/resumes spawning (the timer resets, so no burst on resume) and raises
 *   SpawnsEnabledChanged. Enemies already alive keep moving and recycling. DebugKillAll toggles it.
 * - A spawn point is a random spot at least minSpawnDistance from the player that is also outside the
 *   camera view (plus offscreenMargin in viewport units). If the first try is visible, the radius
 *   steps outward until it is not. Raise minSpawnDistance if enemies ever appear on screen. The spot must also be
 *   open ground outside the base layout (BuildGrid.IsOpenGroundAt within groundClearance), so enemies never appear
 *   inside the walls. If no try passes, the last spot is used (the movement system frees an enemy that lands in a wall).
 * - Enemies farther than recycleDistance are teleported to a fresh off-screen spot, biased within
 *   aheadAngle degrees of the player's movement direction, instead of being destroyed.
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
    [SerializeField, Range(0f, 1f)] private float rangedChance = 0.2f;
    [SerializeField, Min(0)] private int rangedPrewarmCount = 10;
    [SerializeField, Min(0.1f)] private float spawnsPerSecond = 2f;
    [SerializeField, Min(1)] private int maxEnemies = 30;
    [SerializeField, Min(1)] private int prewarmCount = 30;
    [SerializeField, Min(1f)] private float minSpawnDistance = 14f;
    [SerializeField, Min(1f)] private float recycleDistance = 30f;
    [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;
    [SerializeField, Range(0f, 180f)] private float aheadAngle = 60f;
    [Tooltip("A spawn spot must be open ground (no base layout cells) within this radius.")]
    [SerializeField, Min(0f)] private float groundClearance = 0.6f;

    private readonly Dictionary<Enemy, ComponentPool<Enemy>> alive = new Dictionary<Enemy, ComponentPool<Enemy>>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    private ComponentPool<Enemy> pool;
    private ComponentPool<Enemy> rangedPool;
    private Transform player;
    private Rigidbody playerBody;
    private Camera cam;
    private float spawnTimer;
    private bool spawnsEnabled = true;

    public event System.Action<bool> SpawnsEnabledChanged;

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

    private void OnEnable() => Damageable.Died += HandleDied;

    private void OnDisable() => Damageable.Died -= HandleDied;

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

    private void Spawn()
    {
        ComponentPool<Enemy> source = rangedPool != null && Random.value < rangedChance ? rangedPool : pool;
        Enemy enemy = source.Get(FindSpawnPoint(false), Quaternion.identity);
        alive.Add(enemy, source);
    }

    private void HandleDied(Damageable target)
    {
        Enemy enemy = target as Enemy;
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
            Vector3 offset = enemy.transform.position - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrRecycle)
            {
                scratch.Add(enemy);
            }
        }

        for (int i = 0; i < scratch.Count; i++)
        {
            scratch[i].transform.position = FindSpawnPoint(true);
        }
    }

    private Vector3 FindSpawnPoint(bool preferAhead)
    {
        Vector3 center = player.position;
        float height = enemyPrefab.transform.position.y;

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
            float radius = minSpawnDistance + attempt * AttemptRadiusStep;
            float radians = yaw * Mathf.Deg2Rad;

            point = center + new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * radius;
            point.y = height;

            if (IsOffscreen(point) && BuildGrid.IsOpenGroundAt(point, groundClearance))
            {
                return point;
            }
        }

        return point;
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
