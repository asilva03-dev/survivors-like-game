/*
 * RatSwarms
 * The rat swarm (2026-11-21, new rats step 5; user: a swarm of tiny baby mice that kick up dust). Scene object
 * "RatSwarms"; one component runs every swarm.
 *
 * - Spawn(point, assignment, shadow) makes one swarm: the swarm stats' SwarmSize mice (EnemySpawner.SpawnSwarmMouse, from
 *   its pool) packed around `point`, plus a reserve of SwarmReserve. Returns the first mouse (null if none spawned).
 * - Each frame a swarm's Centre (average member position), Heading (average velocity) and Slack (the pack's rough radius,
 *   from the members' spacing and count) are refreshed; EnemyMovementSystem pulls each member back toward Centre once it
 *   strays past Slack (EnemyStats.SwarmCohesion), so the mice walk the normal route as one tight pack.
 * - Refill (user: kills only): a member that is killed (Damageable.Died) takes one mouse from the reserve and is replaced
 *   SwarmRefillDelay seconds later at the back of the pack (behind the rearmost member, with a dust burst), only while at
 *   least one member is still alive. Members that dive into the base (Enemy.EnteredBase) or despawn are not replaced.
 *   A swarm with no members left is over (its pending refills are dropped).
 * - Dust (user: "kick up dust"): every moving, on-screen member drops a small low-poly puff each dustEvery units it moves,
 *   from one shared world-space particle system (mesh particles, puffMaterial = Materials/SlideDustPuff.mat; emitted by
 *   hand, never pooled objects). Refills pop in with refillBurst puffs.
 * - All numbers are placeholders.
 */

using System.Collections.Generic;
using UnityEngine;

public class RatSwarms : MonoBehaviour
{
    public class Group
    {
        public readonly List<Enemy> Members = new List<Enemy>();
        public readonly List<float> Refills = new List<float>();
        public EnemyStats Stats;
        public EnemyAssignment Assignment;
        public bool Shadow;
        public int Reserve;
        public Vector2 Centre;
        public Vector2 Heading;
        public float Slack;
        public float Cohesion;
    }

    public static RatSwarms Instance { get; private set; }

    [SerializeField] private EnemySpawner spawner;
    [Tooltip("The shared crowd settings (min spacing; the swarm packs at its stats' spacing scale of it).")]
    [SerializeField] private EnemySettings settings;
    [Tooltip("The swarm member's stats (SwarmMouse): size, reserve, refill delay, cohesion, spacing.")]
    [SerializeField] private EnemyStats swarmStats;

    [Header("Dust")]
    [SerializeField] private Material puffMaterial;
    [Tooltip("A moving member drops one puff each this many units it moves.")]
    [SerializeField, Min(0.05f)] private float dustEvery = 0.4f;
    [Tooltip("Puffs fired when a refill mouse pops in.")]
    [SerializeField, Min(0)] private int refillBurst = 5;
    [SerializeField] private Vector2 lifetime = new Vector2(0.35f, 0.6f);
    [Tooltip("Puff diameter when it has popped in.")]
    [SerializeField] private Vector2 startSize = new Vector2(0.12f, 0.22f);
    [SerializeField] private Vector2 backSpeed = new Vector2(0.4f, 1.0f);
    [SerializeField] private Vector2 upSpeed = new Vector2(0.3f, 0.8f);
    [SerializeField, Min(0f)] private float sideSpread = 0.4f;
    [SerializeField] private Color color = new Color(0.86f, 0.82f, 0.74f, 1f);

    private readonly List<Group> groups = new List<Group>();
    private readonly List<Group> spare = new List<Group>();
    private readonly Dictionary<Enemy, Vector3> lastPositions = new Dictionary<Enemy, Vector3>();
    private readonly Dictionary<Enemy, float> walked = new Dictionary<Enemy, float>();
    private ParticleSystem dust;
    private Camera cam;

    public int SwarmCount => groups.Count;
    public IList<Group> Swarms => groups;

    private void Awake()
    {
        Instance = this;
        dust = BuildDust();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        Damageable.Died += HandleDied;
        Enemy.EnteredBase += HandleGone;
        Enemy.DespawnRequested += HandleGone;
    }

    private void OnDisable()
    {
        Damageable.Died -= HandleDied;
        Enemy.EnteredBase -= HandleGone;
        Enemy.DespawnRequested -= HandleGone;
    }

    /// <summary>One swarm of mice packed around `point` (see the header). Returns the first mouse, or null.</summary>
    public Enemy Spawn(Vector3 point, EnemyAssignment assignment, bool shadow)
    {
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<EnemySpawner>();
        }
        if (spawner == null || swarmStats == null || swarmStats.SwarmSize <= 0)
        {
            return null;
        }
        Group group = spare.Count > 0 ? spare[spare.Count - 1] : new Group();
        if (spare.Count > 0)
        {
            spare.RemoveAt(spare.Count - 1);
        }
        group.Members.Clear();
        group.Refills.Clear();
        group.Stats = swarmStats;
        group.Assignment = assignment;
        group.Shadow = shadow;
        group.Reserve = swarmStats.SwarmReserve;
        group.Cohesion = swarmStats.SwarmCohesion;
        group.Heading = Vector2.zero;

        // Sunflower packing around the point, one spacing apart.
        float spacing = Spacing(swarmStats);
        Enemy first = null;
        for (int i = 0; i < swarmStats.SwarmSize; i++)
        {
            float radius = spacing * 0.6f * Mathf.Sqrt(i);
            float angle = i * 2.399963f;
            Vector3 spot = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Enemy mouse = AddMember(group, spot);
            if (first == null)
            {
                first = mouse;
            }
        }
        if (group.Members.Count == 0)
        {
            spare.Add(group);
            return null;
        }
        Refresh(group);
        groups.Add(group);
        return first;
    }

    private float Spacing(EnemyStats stats)
    {
        float baseSpacing = settings != null ? settings.MinSpacing : 1f;
        return baseSpacing * stats.SpacingScale;
    }

    private Enemy AddMember(Group group, Vector3 spot)
    {
        Enemy mouse = spawner.SpawnSwarmMouse(spot, group.Assignment, group.Shadow);
        if (mouse == null)
        {
            return null;
        }
        mouse.Swarm = group;
        group.Members.Add(mouse);
        lastPositions[mouse] = mouse.transform.position;
        walked[mouse] = Random.value * dustEvery;
        return mouse;
    }

    private void HandleDied(Damageable target)
    {
        Enemy enemy = target as Enemy;
        Group group = Leave(enemy);
        if (group != null && group.Reserve > 0)
        {
            group.Reserve--;
            Vector2 delay = group.Stats.SwarmRefillDelay;
            group.Refills.Add(Random.Range(Mathf.Min(delay.x, delay.y), Mathf.Max(delay.x, delay.y)));
        }
    }

    private void HandleGone(Enemy enemy)
    {
        Leave(enemy);
    }

    // Takes the enemy out of its swarm; returns the swarm (null if it wasn't in one).
    private Group Leave(Enemy enemy)
    {
        if (enemy == null || enemy.Swarm == null)
        {
            return null;
        }
        Group group = enemy.Swarm;
        enemy.Swarm = null;
        group.Members.Remove(enemy);
        lastPositions.Remove(enemy);
        walked.Remove(enemy);
        return group;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (cam == null)
        {
            cam = Camera.main;
        }
        for (int g = groups.Count - 1; g >= 0; g--)
        {
            Group group = groups[g];
            if (group.Members.Count == 0)
            {
                groups.RemoveAt(g);
                group.Refills.Clear();
                spare.Add(group);
                continue;
            }

            for (int r = group.Refills.Count - 1; r >= 0; r--)
            {
                group.Refills[r] -= dt;
                if (group.Refills[r] <= 0f)
                {
                    group.Refills.RemoveAt(r);
                    Refill(group);
                }
            }

            Refresh(group);
            DropDust(group);
        }
    }

    // A replacement pops in just behind the rearmost member.
    private void Refill(Group group)
    {
        if (group.Members.Count == 0)
        {
            return;
        }
        Vector2 heading = group.Heading.sqrMagnitude > 0.01f ? group.Heading.normalized : Vector2.zero;
        Enemy rear = group.Members[0];
        float rearmost = float.MaxValue;
        for (int i = 0; i < group.Members.Count; i++)
        {
            Vector3 p = group.Members[i].transform.position;
            float along = Vector2.Dot(new Vector2(p.x, p.z) - group.Centre, heading);
            if (along < rearmost)
            {
                rearmost = along;
                rear = group.Members[i];
            }
        }
        Vector3 spot = rear.transform.position - new Vector3(heading.x, 0f, heading.y) * Spacing(group.Stats);
        Enemy mouse = AddMember(group, spot);
        if (mouse != null)
        {
            for (int i = 0; i < refillBurst; i++)
            {
                EmitPuff(mouse.transform.position - Vector3.up * mouse.RootAboveFeet, Random.insideUnitCircle.normalized, 1.4f);
            }
        }
    }

    private void Refresh(Group group)
    {
        Vector2 sum = Vector2.zero;
        Vector2 velocity = Vector2.zero;
        int count = group.Members.Count;
        for (int i = 0; i < count; i++)
        {
            Enemy member = group.Members[i];
            Vector3 p = member.transform.position;
            sum += new Vector2(p.x, p.z);
            Vector3 v = member.Velocity;
            velocity += new Vector2(v.x, v.z);
        }
        group.Centre = sum / Mathf.Max(count, 1);
        group.Heading = velocity / Mathf.Max(count, 1);
        group.Slack = Spacing(group.Stats) * (0.5f + 0.5f * Mathf.Sqrt(count));
    }

    private void DropDust(Group group)
    {
        for (int i = 0; i < group.Members.Count; i++)
        {
            Enemy member = group.Members[i];
            Vector3 now = member.transform.position;
            Vector3 before;
            if (!lastPositions.TryGetValue(member, out before))
            {
                before = now;
            }
            lastPositions[member] = now;
            Vector3 step = now - before;
            step.y = 0f;
            float distance = step.magnitude;
            if (distance <= 0.0001f || distance > 2f)
            {
                continue;
            }
            float total = walked[member] + distance;
            if (total < dustEvery)
            {
                walked[member] = total;
                continue;
            }
            walked[member] = total % dustEvery;
            if (IsOnScreen(now))
            {
                Vector3 forward = step / distance;
                EmitPuff(now - Vector3.up * member.RootAboveFeet - forward * 0.15f, new Vector2(-forward.x, -forward.z), 1f);
            }
        }
    }

    private bool IsOnScreen(Vector3 point)
    {
        if (cam == null)
        {
            return true;
        }
        Vector3 view = cam.WorldToViewportPoint(point);
        return view.z > 0f && view.x > -0.05f && view.x < 1.05f && view.y > -0.05f && view.y < 1.05f;
    }

    private void EmitPuff(Vector3 feet, Vector2 back, float speedScale)
    {
        if (dust == null)
        {
            return;
        }
        Vector3 backward = new Vector3(back.x, 0f, back.y);
        if (backward.sqrMagnitude < 0.0001f)
        {
            backward = Vector3.back;
        }
        backward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, backward);
        Vector3 velocity = (backward * Random.Range(backSpeed.x, backSpeed.y) + Vector3.up * Random.Range(upSpeed.x, upSpeed.y)
            + side * Random.Range(-sideSpread, sideSpread)) * speedScale;
        ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
        {
            position = feet + Vector3.up * 0.05f + side * Random.Range(-0.05f, 0.05f),
            velocity = velocity,
            startLifetime = Random.Range(lifetime.x, lifetime.y),
            startSize = Random.Range(startSize.x, startSize.y),
            startColor = color,
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            angularVelocity3D = new Vector3(Random.Range(-180f, 180f), Random.Range(-180f, 180f), Random.Range(-180f, 180f))
        };
        dust.Emit(puff, 1);
    }

    private ParticleSystem BuildDust()
    {
        GameObject go = new GameObject("SwarmDust");
        go.transform.SetParent(transform, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;
        main.gravityModifier = 0f;
        main.startRotation3D = true;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // Pop in, swell, shrink away to nothing.
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(0.6f, 1.4f), new Keyframe(1f, 0f));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        ParticleSystem.LimitVelocityOverLifetimeModule slow = system.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = 0f;
        slow.dampen = 0.2f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = BuildIcosahedron();
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = puffMaterial != null
            ? puffMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")) { name = "SwarmDustFallback" };

        system.Play();
        return system;
    }

    // Flat-shaded icosahedron (20 faces), diameter 1: cheaper than the slide dust's icosphere, fine at this size.
    private static Mesh BuildIcosahedron()
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] v =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };
        Vector3[] vertices = new Vector3[f.Length];
        int[] triangles = new int[f.Length];
        for (int i = 0; i < f.Length; i += 3)
        {
            Vector3 p0 = v[f[i]].normalized * 0.5f, p1 = v[f[i + 1]].normalized * 0.5f, p2 = v[f[i + 2]].normalized * 0.5f;
            bool outward = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), p0 + p1 + p2) > 0f;
            vertices[i] = p0;
            vertices[i + 1] = outward ? p1 : p2;
            vertices[i + 2] = outward ? p2 : p1;
            triangles[i] = i;
            triangles[i + 1] = i + 1;
            triangles[i + 2] = i + 2;
        }
        Mesh mesh = new Mesh { name = "SwarmDustPuffMesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
