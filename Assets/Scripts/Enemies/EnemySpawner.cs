/*
 * EnemySpawner
 * Pooled enemy spawner that places enemies just off-screen and lets them walk in, Vampire
 * Survivors style.
 *
 * - Enemies come from a ComponentPool<Enemy> built from enemyPrefab (prewarmed), never
 *   Instantiate/Destroy at runtime. Dead enemies are returned to the pool via Enemy.Died.
 * - Spawn rate (spawnsPerSecond) and the cap (maxEnemies) throttle spawning. If the cap is reached
 *   the timer holds, so there is no burst once enemies die.
 * - A spawn point is a random spot at least minSpawnDistance from the player that is also outside the
 *   camera view (plus offscreenMargin in viewport units). If the first try is visible, the radius
 *   steps outward until it is not. Raise minSpawnDistance if enemies ever appear on screen.
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
    [SerializeField, Min(0.1f)] private float spawnsPerSecond = 2f;
    [SerializeField, Min(1)] private int maxEnemies = 30;
    [SerializeField, Min(1)] private int prewarmCount = 30;
    [SerializeField, Min(1f)] private float minSpawnDistance = 14f;
    [SerializeField, Min(1f)] private float recycleDistance = 30f;
    [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;
    [SerializeField, Range(0f, 180f)] private float aheadAngle = 60f;

    private readonly HashSet<Enemy> alive = new HashSet<Enemy>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    private ComponentPool<Enemy> pool;
    private Transform player;
    private Rigidbody playerBody;
    private Camera cam;
    private float spawnTimer;

    private void Awake()
    {
        Transform poolRoot = new GameObject("Enemies").transform;
        pool = new ComponentPool<Enemy>(enemyPrefab, poolRoot, prewarmCount);
    }

    private void OnEnable() => Enemy.Died += HandleDied;

    private void OnDisable() => Enemy.Died -= HandleDied;

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
        Enemy enemy = pool.Get(FindSpawnPoint(false), Quaternion.identity);
        alive.Add(enemy);
    }

    private void HandleDied(Enemy enemy)
    {
        if (alive.Remove(enemy))
        {
            pool.Release(enemy);
        }
    }

    private void RecycleFarEnemies()
    {
        float sqrRecycle = recycleDistance * recycleDistance;
        scratch.Clear();

        foreach (Enemy enemy in alive)
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

            if (IsOffscreen(point))
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
