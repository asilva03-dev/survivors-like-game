/*
 * EnemyMovementSystem
 * Moves every active enemy toward the player and keeps them from overlapping, using separation
 * steering instead of physics. One component in the scene handles all enemies.
 *
 * - Each frame: enemies are bucketed into a spatial grid so neighbor checks stay cheap, each enemy
 *   gets a seek velocity toward the player plus a push away from nearby enemies, and all moves are
 *   computed before any are applied so processing order does not bias the crowd.
 * - A short hard pass then pushes apart any pair closer than minSpacing and keeps enemies outside
 *   stopDistance of the player, so enemies do not visibly overlap even in dense crowds.
 * - Speed and spacing come from the shared EnemySettings asset.
 * - Knocked-back enemies (Enemy.Knockback) stop seeking and add their push step to the move.
 * - Enemies keep their own Y. They need a kinematic Rigidbody (cheap collider moves) and live on the
 *   "Enemy" layer, whose collisions with the default layer are disabled here so a crowd cannot trap
 *   the player. Projectile hit checks still see them.
 * - The player is found by the "Player" tag on Start.
 */

using System.Collections.Generic;
using UnityEngine;

public class EnemyMovementSystem : MonoBehaviour
{
    private const int HardPasses = 2;
    private const float GoldenAngle = 2.399963f;

    [SerializeField] private EnemySettings settings;

    private readonly Dictionary<int, int> cellHead = new Dictionary<int, int>();
    private readonly List<int> neighbors = new List<int>(32);
    private Vector2[] positions = new Vector2[64];
    private Vector2[] moved = new Vector2[64];
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
        Vector2 target = new Vector2(player.position.x, player.position.z);

        BuildGrid(enemies, count, cellSize);
        ComputeMoves(count, cellSize, target);

        for (int pass = 0; pass < HardPasses; pass++)
        {
            ResolveOverlaps(count, cellSize, target);
        }

        for (int i = 0; i < count; i++)
        {
            Transform t = enemies[i].transform;
            t.position = new Vector3(moved[i].x, t.position.y, moved[i].y);
        }
    }

    private void BuildGrid(List<Enemy> enemies, int count, float cellSize)
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

    private void ComputeMoves(int count, float cellSize, Vector2 target)
    {
        float dt = Time.deltaTime;
        float radius = settings.SeparationRadius;
        float sqrRadius = radius * radius;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = Enemy.Active[i];
            Vector2 toTarget = target - positions[i];
            float distance = toTarget.magnitude;
            Vector2 seek = Vector2.zero;

            if (!enemy.IsKnockedBack && distance > settings.StopDistance && dt > 0f)
            {
                float speed = Mathf.Min(settings.MoveSpeed, (distance - settings.StopDistance) / dt);
                seek = toTarget / distance * speed;
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

    private void ResolveOverlaps(int count, float cellSize, Vector2 target)
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
            Vector2 offset = moved[i] - target;
            float length = offset.magnitude;
            if (length < settings.StopDistance)
            {
                Vector2 away = length > 0.0001f ? offset / length : Fallback(i, count);
                moved[i] = target + away * settings.StopDistance;
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
    }
}
