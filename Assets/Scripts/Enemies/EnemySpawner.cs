/*
 * EnemySpawner
 * Pooled enemy spawner: where and as what each enemy appears. It no longer spawns on its own (round system step 2,
 * 2026-11-05): the spawn director (RunDirector) releases the packs it bought through SpawnPack (see below; the old
 * one-at-a-time TrySpawn was removed in spawn director step 5, 2026-11-22). The old steady stream (spawnsPerSecond) and
 * the alive cap (maxEnemies) were removed earlier (the cap was a test aid, user). A pack that finds no valid spot is
 * counted in SkippedSpawns.
 * - Enemies come from ComponentPools built from enemyPrefab (normal) and rangedEnemyPrefab (prewarmed), never
 *   Instantiate/Destroy at runtime. Dead enemies return to the pool they came from via Damageable.Died, and hunters
 *   that retreated off-screen via Enemy.DespawnRequested (no death), and enemies that dived into the base via
 *   Enemy.EnteredBase (no death, no refund).
 * - Split: each pack goes to the player with probability PlayerShare, else to the base (side rules in SpawnPack). PlayerShare is 0 while the
 *   player is in the BaseLight; in the dark it rises from playerShareMin to playerShareMax over playerShareRampTime
 *   seconds and resets when the player returns to the light. No BaseLight = always the dark.
 * - Player share: spawned at an off-screen spot around the player (minSpawnDistance, stepping outward) that is also
 *   at least lightClearance outside the light, assigned to the player (Enemy.Assignment). Ranged rats only ever come
 *   from this share. With a map (MapRoutes) the
 *   spot must also have a walking route to the player, so hills and slopes are fine but sealed-off ground is not.
 * - Base share (user's rule, 2026-10-30): assigned to the base, at a random direction around the light (or near the pack's front),
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
 * - SpawnsEnabled pauses/resumes spawning (SpawnPack refuses while paused; the run clock keeps going) and raises
 *   SpawnsEnabledChanged. Enemies already alive keep moving and recycling. DebugKillAll toggles it.
 * - Spill (2026-11-20, bag rat): an enemy whose EnemyStats.SpillCount > 0 spills that many basic rats from the normal
 *   pool where it died (same assignment and shadow property, the stats' armor layers, pushed out evenly by Enemy.Knockback over spillSpread),
 *   each then takes the killing hit's leftover damage (Enemy.Overkill, if spillCarriesOverkill), and a chip burst plays
 *   (ArmorPopEffect, spillBurstColor). Spilled rats use no wave budget. A bag rat that dives into the base spills nothing.
 * - Pot rats (2026-11-21): potEnemyPrefab's pool; for now only SpawnForBase (debug keys) spawns them (the spawn director
 *   will put them in waves). When one dies its stats' spill releases the rats under the pot (see Spill).
 * - Athletic rats (2026-11-21): athleticEnemyPrefab's pool; base-bound only, spawned by SpawnForBase (F6) until the spawn
 *   director (which will keep them out of the player's dark share).
 * - Swarm mice (2026-11-21, rat swarm): swarmEnemyPrefab's pool. SpawnForBase(Swarm) spawns a whole swarm through
 *   RatSwarms.Spawn, which takes each mouse from here with SpawnSwarmMouse (also its refills). Base-bound only for now.
 * - SpawnPack(type, count, shadow, side, spawned) (spawn director step 2, 2026-11-21): one pack the director bought,
 *   released together at one spot. The light/dark split is rolled once for the whole pack (PlayerShare): BaseOnly packs
 *   always go for the base; DarkOnly packs (ranged) only for the player, so a roll for the base returns NeedsDark (the
 *   director retries later). The spot is a normal player- or base-share spot; members are spread around it
 *   (packSpread). A Swarm pack is one RatSwarms swarm. Spawned enemies are added to `spawned` (the director tracks them for
 *   refunds). NoSpot (nothing spawned) when no valid spot was found, spawns are paused or there is no player.
 *   Fronts (spawn director step 3): a base-bound pack given a frontYaw (degrees around the light, 0 = +Z) spawns within
 *   frontJitter of it (the same just-outside-the-light, pushed-off-screen spot as the base share); if no valid spot
 *   is found there for the first frontAttempts tries, the rest of the tries widen to any direction. The player's dark
 *   share ignores fronts (it spawns around the player as before).
 * - SpawnForBase(kind, armorLayers, shadow) (2026-11-21): one base-bound enemy at a base-share spot for debug keys and
 *   tests (DebugSpawnKeys); no budget, ignores the share and the spawn pause.
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
    [Tooltip("Bag rat (2026-11-20): base-bound only (SpawnPrices side). Empty = none.")]
    [SerializeField] private Enemy bagEnemyPrefab;
    [SerializeField, Min(0)] private int bagPrewarmCount = 2;
    [Tooltip("Pot rat (2026-11-21): a pot carried by rats; only lead hits break it. Spawned by SpawnForBase (debug) until the spawn director.")]
    [SerializeField] private Enemy potEnemyPrefab;
    [SerializeField, Min(0)] private int potPrewarmCount = 2;
    [Tooltip("Athletic rat (2026-11-21): fast, jumps walls, base-bound only. Spawned by SpawnForBase (debug) until the spawn director.")]
    [SerializeField] private Enemy athleticEnemyPrefab;
    [SerializeField, Min(0)] private int athleticPrewarmCount = 4;
    [Tooltip("Swarm mouse (2026-11-21): tiny mice spawned as packs by RatSwarms. Spawned by SpawnForBase (debug) until the spawn director.")]
    [SerializeField] private Enemy swarmEnemyPrefab;
    [SerializeField, Min(0)] private int swarmPrewarmCount = 24;

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
    [Tooltip("Director packs: members are spread around the pack's spot this far apart.")]
    [SerializeField, Min(0f)] private float packSpread = 0.8f;
    [Tooltip("Director fronts: a base-bound pack spawns within this many degrees either side of its front's direction.")]
    [SerializeField, Range(0f, 180f)] private float frontJitter = 15f;
    [Tooltip("Director fronts: tries near the front before any direction is allowed (of the 12 tries).")]
    [SerializeField, Range(1, 12)] private int frontAttempts = 8;

    private readonly Dictionary<Enemy, ComponentPool<Enemy>> alive = new Dictionary<Enemy, ComponentPool<Enemy>>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    private ComponentPool<Enemy> pool;
    private ComponentPool<Enemy> rangedPool;
    private ComponentPool<Enemy> bagPool;
    private ComponentPool<Enemy> potPool;
    private ComponentPool<Enemy> athleticPool;
    private ComponentPool<Enemy> swarmPool;
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
        if (bagEnemyPrefab != null)
        {
            bagPool = new ComponentPool<Enemy>(bagEnemyPrefab, poolRoot, bagPrewarmCount);
        }
        if (potEnemyPrefab != null)
        {
            potPool = new ComponentPool<Enemy>(potEnemyPrefab, poolRoot, potPrewarmCount);
        }
        if (athleticEnemyPrefab != null)
        {
            athleticPool = new ComponentPool<Enemy>(athleticEnemyPrefab, poolRoot, athleticPrewarmCount);
        }
        if (swarmEnemyPrefab != null)
        {
            swarmPool = new ComponentPool<Enemy>(swarmEnemyPrefab, poolRoot, swarmPrewarmCount);
        }
    }

    private void OnEnable()
    {
        Damageable.Died += HandleDied;
        Enemy.DespawnRequested += HandleDespawn;
        Enemy.EnteredBase += HandleDespawn;
    }

    private void OnDisable()
    {
        Damageable.Died -= HandleDied;
        Enemy.DespawnRequested -= HandleDespawn;
        Enemy.EnteredBase -= HandleDespawn;
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

    public enum Kind { Basic, Ranged, Bag, Pot, Athletic, Swarm }

    public enum PackResult { Spawned, NoSpot, NeedsDark }

    /// <summary>The spawn director's packs (see the header).</summary>
    public PackResult SpawnPack(RatType type, int count, bool shadow, SpawnSide side, List<Enemy> spawned,
        float frontYaw = float.NaN)
    {
        if (!spawnsEnabled || player == null)
        {
            return PackResult.NoSpot;
        }
        if (cam == null)
        {
            cam = Camera.main;
        }
        bool toPlayer = side != SpawnSide.BaseOnly && Random.value < PlayerShare;
        if (side == SpawnSide.DarkOnly && !toPlayer)
        {
            return PackResult.NeedsDark;
        }
        Vector3 point;
        if (toPlayer ? !TryFindPlayerSpawnPoint(false, out point) : !TryFindBaseSpawnPoint(frontYaw, out point))
        {
            SkippedSpawns++;
            return PackResult.NoSpot;
        }
        EnemyAssignment assignment = toPlayer ? EnemyAssignment.Player : EnemyAssignment.Base;

        EnemySpawner.Kind kind;
        int armorLayers;
        SpawnPrices.ToSpawn(type, out kind, out armorLayers);
        if (kind == Kind.Swarm)
        {
            RatSwarms swarms = RatSwarms.Instance;
            Enemy first = swarms != null && swarmPool != null ? swarms.Spawn(point, assignment, shadow) : null;
            if (first == null)
            {
                return PackResult.NoSpot;
            }
            if (spawned != null && first.Swarm != null)
            {
                spawned.AddRange(first.Swarm.Members);
            }
            return PackResult.Spawned;
        }

        ComponentPool<Enemy> source = kind == Kind.Ranged ? rangedPool : kind == Kind.Bag ? bagPool : kind == Kind.Pot ? potPool
            : kind == Kind.Athletic ? athleticPool : pool;
        if (source == null)
        {
            return PackResult.NoSpot;
        }
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            float radius = i == 0 ? 0f : packSpread * Mathf.Sqrt(i);
            float angle = i * 2.399963f;
            Vector3 spot = OnGround(point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            Enemy enemy = source.Get(spot, Quaternion.identity);
            enemy.Assignment = assignment;
            enemy.SetExtraLayers(armorLayers);
            enemy.SetShadow(shadow);
            alive.Add(enemy, source);
            if (spawned != null)
            {
                spawned.Add(enemy);
            }
        }
        return PackResult.Spawned;
    }

    /// <summary>
    /// Debug / tests (2026-11-21): spawns one enemy of `kind` heading for the base at a normal base-share spot (off screen
    /// just outside the light), with armorLayers and the shadow property. Ignores the player share and SpawnsEnabled;
    /// uses no wave budget. Null when no spot was found or that kind has no prefab.
    /// </summary>
    public Enemy SpawnForBase(Kind kind, int armorLayers, bool shadow)
    {
        if (kind == Kind.Swarm)
        {
            return SpawnSwarmForBase(shadow);
        }
        ComponentPool<Enemy> source = kind == Kind.Ranged ? rangedPool : kind == Kind.Bag ? bagPool : kind == Kind.Pot ? potPool
            : kind == Kind.Athletic ? athleticPool : pool;
        if (source == null || player == null)
        {
            return null;
        }
        if (cam == null)
        {
            cam = Camera.main;
        }
        Vector3 point;
        if (!TryFindBaseSpawnPoint(out point))
        {
            return null;
        }
        Enemy enemy = source.Get(point, Quaternion.identity);
        enemy.Assignment = EnemyAssignment.Base;
        enemy.SetExtraLayers(armorLayers);
        enemy.SetShadow(shadow);
        alive.Add(enemy, source);
        return enemy;
    }

    // A whole swarm (RatSwarms) at a base-share spot.
    private Enemy SpawnSwarmForBase(bool shadow)
    {
        RatSwarms swarms = RatSwarms.Instance;
        if (swarms == null || swarmPool == null || player == null)
        {
            return null;
        }
        if (cam == null)
        {
            cam = Camera.main;
        }
        Vector3 point;
        if (!TryFindBaseSpawnPoint(out point))
        {
            return null;
        }
        return swarms.Spawn(point, EnemyAssignment.Base, shadow);
    }

    /// <summary>RatSwarms: one swarm mouse from its pool at `point` (feet put on the ground). Null without a swarm prefab.</summary>
    public Enemy SpawnSwarmMouse(Vector3 point, EnemyAssignment assignment, bool shadow)
    {
        if (swarmPool == null)
        {
            return null;
        }
        point = OnGround(point);
        Enemy mouse = swarmPool.Get(point, Quaternion.identity);
        mouse.Assignment = assignment;
        mouse.SetShadow(shadow);
        alive.Add(mouse, swarmPool);
        return mouse;
    }

    private void HandleDied(Damageable target)
    {
        Enemy enemy = target as Enemy;
        if (enemy != null && alive.ContainsKey(enemy) && enemy.Stats != null && enemy.Stats.SpillCount > 0)
        {
            Spill(enemy);
        }
        Release(enemy);
    }

    // A bag rat burst: its rats pop out around it, each taking the killing hit's leftover damage.
    private void Spill(Enemy parent)
    {
        EnemyStats stats = parent.Stats;
        Vector3 point = parent.transform.position;
        float overkill = stats.SpillCarriesOverkill ? parent.Overkill : 0f;
        float startAngle = Random.Range(0f, 360f);
        for (int i = 0; i < stats.SpillCount; i++)
        {
            Enemy child = pool.Get(point, Quaternion.identity);
            child.Assignment = parent.Assignment;
            child.SetExtraLayers(stats.SpillArmorLayers);
            child.SetShadow(parent.IsShadow);
            alive.Add(child, pool);
            float angle = (startAngle + i * 360f / stats.SpillCount + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            child.Knockback(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), stats.SpillSpread, 0.35f);
            if (overkill > 0f)
            {
                child.TakeDamage(overkill);
            }
        }
        ArmorPopEffect.Play(point + Vector3.up * 0.6f, stats.SpillBurstColor);
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
        return TryFindBaseSpawnPoint(float.NaN, out point);
    }

    // As above, aimed at a director front (frontYaw degrees; NaN = any direction).
    private bool TryFindBaseSpawnPoint(float frontYaw, out Vector3 point)
    {
        BaseLight baseLight = BaseLight.Instance;
        if (baseLight == null)
        {
            return TryFindPlayerSpawnPoint(false, out point);
        }

        Vector3 center = baseLight.Center;
        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            float yaw = !float.IsNaN(frontYaw) && attempt < frontAttempts
                ? frontYaw + Random.Range(-frontJitter, frontJitter)
                : Random.Range(0f, 360f);
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
