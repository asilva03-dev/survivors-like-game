/*
 * FishercatNetThrower
 * The Fishercat's net throw (Path 2, 2026-11-26, user's spec): an extra attack beside the spears. On Fishercat.prefab's
 * root; does nothing until its stats are unlocked (tier 1).
 *
 * - Every cooldown (FishercatNetStats; Turret hands over this tower's upgraded copy with UseStats) it looks at the rats it
 *   could catch (in range: stats range, 0 = the gun's range, flat from the tower's centre; visible to the gun via
 *   ShadowSight; fat = EnemyStats.StunImmune rats only when the stats catch fat) and aims at the rat whose spot, led by
 *   its velocity over the flight, has the most such rats within the net's radius. With NetCount > 1 (tier 3) each extra
 *   net goes to the next best spot at least two radii from the others (no overlap), counting only rats not yet covered;
 *   if no such spot has a rat, fewer nets are thrown.
 * - No cat animation (user): a pooled FishercatNetShot flies from `launchPoint` (the net chest's mouth) in an arc and lands
 *   there (see FishercatNetShot for the catch, spikes and the tier 5 whirlpool).
 * - The net's look follows the stats: spiked mesh once it deals spike damage (tier 3), metal rope once it catches fat rats
 *   (tier 4). Placeholders.
 */

using System.Collections.Generic;
using UnityEngine;

public class FishercatNetThrower : MonoBehaviour
{
    [SerializeField] private FishercatNetStats stats;
    [SerializeField] private Turret turret;
    [SerializeField] private TurretGun gun;
    [Tooltip("Where the nets leave from (the net chest's mouth).")]
    [SerializeField] private Transform launchPoint;
    [SerializeField] private FishercatNetShot shotPrefab;
    [SerializeField, Min(0)] private int prewarm = 2;

    [Header("Look")]
    [SerializeField] private Mesh netMesh;
    [SerializeField] private Mesh spikedNetMesh;
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private Material metalRopeMaterial;

    private float cooldown;
    private ComponentPool<FishercatNetShot> pool;
    private Transform poolRoot;
    private readonly List<Enemy> candidates = new List<Enemy>();
    private readonly List<Vector3> spots = new List<Vector3>();

    public FishercatNetStats Stats => stats;
    /// <summary>Nets thrown since Start (testing).</summary>
    public int Throws { get; private set; }

    public void UseStats(FishercatNetStats upgraded)
    {
        stats = upgraded;
    }

    private void Awake()
    {
        poolRoot = new GameObject(name + " Nets").transform;
        if (shotPrefab != null)
        {
            pool = new ComponentPool<FishercatNetShot>(shotPrefab, poolRoot, prewarm);
        }
    }

    private void OnDestroy()
    {
        if (poolRoot != null)
        {
            Destroy(poolRoot.gameObject);
        }
    }

    private void Update()
    {
        if (stats == null || !stats.Unlocked || pool == null)
        {
            return;
        }
        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
        {
            return;
        }
        if (PickSpots())
        {
            foreach (Vector3 aim in spots)
            {
                Throw(aim);
            }
            cooldown = stats.Cooldown;
        }
    }

    /// <summary>Whether this rat can be caught by this tower's nets now (visible, alive, fat only when allowed).</summary>
    public bool CanCatch(Enemy enemy)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || !enemy.Listed || enemy.Health <= 0f)
        {
            return false;
        }
        bool fat = enemy.Stats != null && enemy.Stats.StunImmune;
        if (fat && !stats.CatchesFat)
        {
            return false;
        }
        bool sees = gun != null && gun.CurrentStats != null && gun.CurrentStats.DetectsShadow;
        return ShadowSight.CanTarget(enemy, sees);
    }

    // Up to NetCount spots: each the candidate spot with the most not-yet-covered rats within the radius, at least two
    // radii from every spot already picked (so the nets never overlap). False when no rat can be caught.
    private bool PickSpots()
    {
        spots.Clear();
        float range = stats.Range > 0f ? stats.Range : gun != null ? gun.Range : 0f;
        Vector3 centre = turret != null ? turret.Center : new Vector3(transform.position.x, 0f, transform.position.z);
        candidates.Clear();
        List<Enemy> enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (!CanCatch(enemy))
            {
                continue;
            }
            Vector3 p = enemy.transform.position;
            if (new Vector2(p.x - centre.x, p.z - centre.z).sqrMagnitude <= range * range)
            {
                candidates.Add(enemy);
            }
        }
        if (candidates.Count == 0)
        {
            return false;
        }
        float r2 = stats.Radius * stats.Radius;
        float apart2 = 4f * r2;
        for (int n = 0; n < stats.NetCount; n++)
        {
            int best = 0;
            Vector3 aim = Vector3.zero;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 spot = Lead(candidates[i]);
                if (TooClose(spot, apart2))
                {
                    continue;
                }
                int count = 0;
                for (int j = 0; j < candidates.Count; j++)
                {
                    Vector3 other = Lead(candidates[j]);
                    if (new Vector2(other.x - spot.x, other.z - spot.z).sqrMagnitude <= r2 && !Covered(other, r2))
                    {
                        count++;
                    }
                }
                if (count > best)
                {
                    best = count;
                    aim = spot;
                }
            }
            if (best == 0)
            {
                break;
            }
            spots.Add(aim);
        }
        return spots.Count > 0;
    }

    private bool TooClose(Vector3 spot, float apart2)
    {
        foreach (Vector3 s in spots)
        {
            if (new Vector2(s.x - spot.x, s.z - spot.z).sqrMagnitude < apart2)
            {
                return true;
            }
        }
        return false;
    }

    private bool Covered(Vector3 point, float r2)
    {
        foreach (Vector3 s in spots)
        {
            if (new Vector2(s.x - point.x, s.z - point.z).sqrMagnitude <= r2)
            {
                return true;
            }
        }
        return false;
    }

    // Where the rat will be when the net lands (feet height).
    private Vector3 Lead(Enemy enemy)
    {
        Vector3 v = enemy.Velocity;
        v.y = 0f;
        Vector3 p = enemy.transform.position + v * stats.FlightTime;
        p.y = enemy.FeetY;
        return p;
    }

    private void Throw(Vector3 aim)
    {
        Vector3 from = launchPoint != null && launchPoint.gameObject.activeInHierarchy ? launchPoint.position : transform.position + Vector3.up * 2f;
        FishercatNetShot shot = pool.Get(from, Quaternion.identity);
        bool spiked = stats.SpikeDamage > 0f;
        bool metal = stats.CatchesFat;
        shot.Launch(this, pool, from, aim, spiked ? spikedNetMesh : netMesh, metal ? metalRopeMaterial : ropeMaterial);
        Throws++;
    }
}
