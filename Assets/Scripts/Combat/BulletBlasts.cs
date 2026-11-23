/*
 * BulletBlasts
 * Explosive bullets (2026-11-22, gun turret Firepower T3-T5): the blast and its shrapnel. Scene object "BulletBlasts";
 * Projectile calls the static Explode when a gun's explosion roll succeeds (GunStats.ExplosionChance).
 *
 * - Explode(point, gun, hit): the gun's ExplosionTargets enemies nearest `point` within ExplosionRadius (flat distance;
 *   the hit enemy included, shadow rats too) each take ExplosionDamage with the Lead property, so a blast breaks pots.
 * - Shrapnel (GunStats.ShrapnelCount > 0): that many bullets from shrapnelPrefab (pooled, ComponentPool) fly out flat
 *   from the blast, one at each of the nearest other enemies within shrapnelSeekRadius (the rest in random directions),
 *   with shrapnelStats' damage (10, Enemy only, no explosions of their own), speed and lifetime (short range).
 * - Look: one world-space particle system of low-poly mesh puffs (BlastPuff.mat, unlit): a big core fireball (coreSize x
 *   the puff size) in the middle, flashCount bright puffs that pop out at flashSpeed, then smokeCount grey ones that
 *   drift up (2026-11-22, user: bigger blasts: core added, puffScale / counts / speed raised). Every puff darkens over
 *   its life (colour over lifetime: full colour -> fireColor -> ashColor), so the flash burns from yellow to orange to
 *   smoky grey. Smoke (2026-11-22, user: "more smoke, make it pronounced, linger a bit"): its own particle system
 *   (no fire tint): smokeCount puffs of smokeSize x the puff size, spread out (smokeSpread) and rising slowly (smokeRise),
 *   braking hard so they hang where they are, swelling and then shrinking away over smokeLifetime seconds
 *   (2026-11-23, user: keep the amount but don't linger: smokeLifetime 1.4-2.2 -> 0.45-0.7).
 * - Sparks (2026-11-23, user: the smoke covered the fire, "shoot out fire coloured sparks"): a third particle system of
 *   stretched streaks (billboards stretched along their velocity): sparkCount sparks fly out fast (sparkSpeed), mostly
 *   up and outward, past the smoke, fall a little (sparkGravity), burn from sparkColorHot to sparkColor and shrink away
 *   over sparkLifetime. Emitted by hand; nothing is instantiated per blast.
 * - Without this object in the scene bullets never explode. Blasts made this frame are counted (BlastCount) for tests.
 * - All numbers are placeholders.
 */

using System.Collections.Generic;
using UnityEngine;

public class BulletBlasts : MonoBehaviour
{
    private static BulletBlasts instance;
    private static readonly List<Enemy> nearby = new List<Enemy>();
    private static readonly List<float> nearbyDistance = new List<float>();

    [Header("Shrapnel")]
    [SerializeField] private Projectile shrapnelPrefab;
    [Tooltip("Damage, targets, speed and lifetime of a shrapnel bullet.")]
    [SerializeField] private GunStats shrapnelStats;
    [SerializeField, Min(0)] private int shrapnelPrewarm = 24;
    [Tooltip("Shrapnel aims at the nearest other enemies within this radius of the blast.")]
    [SerializeField, Min(0.1f)] private float shrapnelSeekRadius = 4f;

    [Header("Look")]
    [SerializeField] private Material puffMaterial;
    [SerializeField, Min(0)] private int flashCount = 8;
    [SerializeField, Min(0)] private int smokeCount = 5;
    [SerializeField] private Color flashColor = new Color(1f, 0.72f, 0.25f, 1f);
    [SerializeField] private Color flashColorHot = new Color(1f, 0.93f, 0.55f, 1f);
    [SerializeField] private Color smokeColor = new Color(0.42f, 0.4f, 0.38f, 1f);
    [Tooltip("Puff size relative to the blast radius.")]
    [SerializeField, Min(0.05f)] private float puffScale = 0.35f;
    [Tooltip("The core fireball's size, x the puff size (0 = no core).")]
    [SerializeField, Min(0f)] private float coreSize = 2.2f;
    [Tooltip("How fast the flash puffs fly out (min, max units/s).")]
    [SerializeField] private Vector2 flashSpeed = new Vector2(2.5f, 5f);
    [Tooltip("Puffs are tinted toward this halfway through their life...")]
    [SerializeField] private Color fireColor = new Color(1f, 0.55f, 0.25f, 1f);
    [Tooltip("...and toward this at the end.")]
    [SerializeField] private Color ashColor = new Color(0.45f, 0.42f, 0.4f, 1f);

    [Header("Sparks")]
    [SerializeField, Min(0)] private int sparkCount = 18;
    [Tooltip("Spark speed (min, max units/s).")]
    [SerializeField] private Vector2 sparkSpeed = new Vector2(7f, 13f);
    [SerializeField] private Vector2 sparkLifetime = new Vector2(0.25f, 0.45f);
    [Tooltip("Spark thickness (min, max units); its length comes from its speed.")]
    [SerializeField] private Vector2 sparkSize = new Vector2(0.07f, 0.12f);
    [Tooltip("How fast sparks fall (x gravity).")]
    [SerializeField, Min(0f)] private float sparkGravity = 1.5f;
    [SerializeField] private Color sparkColorHot = new Color(1f, 1f, 0.75f, 1f);
    [SerializeField] private Color sparkColor = new Color(1f, 0.85f, 0.15f, 1f);

    [Header("Smoke")]
    [Tooltip("Seconds a smoke puff lingers (min, max).")]
    [SerializeField] private Vector2 smokeLifetime = new Vector2(0.45f, 0.7f);
    [Tooltip("Smoke puff size, x the puff size (min, max).")]
    [SerializeField] private Vector2 smokeSize = new Vector2(0.9f, 1.5f);
    [Tooltip("How far out from the blast the smoke starts and drifts (units).")]
    [SerializeField, Min(0f)] private float smokeSpread = 1.2f;
    [Tooltip("Upward drift speed (min, max units/s).")]
    [SerializeField] private Vector2 smokeRise = new Vector2(0.3f, 0.7f);

    private ComponentPool<Projectile> shrapnelPool;
    private ParticleSystem puffs;
    private ParticleSystem smoke;
    private ParticleSystem sparks;

    public static int BlastCount { get; private set; }
    public static int ShrapnelFired { get; private set; }

    private void Awake()
    {
        instance = this;
        if (shrapnelPrefab != null)
        {
            Transform root = new GameObject("Shrapnel").transform;
            shrapnelPool = new ComponentPool<Projectile>(shrapnelPrefab, root, shrapnelPrewarm);
        }
        puffs = BuildPuffs("BlastPuffs", false);
        smoke = BuildPuffs("BlastSmoke", true);
        sparks = BuildSparks();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>A blast at `point` from `gun`'s explosion stats (see the header). `hit` = the enemy the bullet hit.</summary>
    public static void Explode(Vector3 point, GunStats gun, Damageable hit)
    {
        if (instance == null || gun == null)
        {
            return;
        }
        BlastCount++;
        instance.Blast(point, gun, hit);
    }

    private void Blast(Vector3 point, GunStats gun, Damageable hit)
    {
        Gather(point, gun.ExplosionRadius, null);
        int targets = Mathf.Min(gun.ExplosionTargets, nearby.Count);
        for (int i = 0; i < targets; i++)
        {
            Enemy enemy = nearby[i];
            if (enemy != null && enemy.isActiveAndEnabled)
            {
                enemy.TakeHit(gun.ExplosionDamage, HitFlags.Lead);
            }
        }
        if (gun.ShrapnelCount > 0)
        {
            FireShrapnel(point, gun.ShrapnelCount, hit);
        }
        PlayLook(point, gun.ExplosionRadius);
    }

    // Enemies within radius of point (flat), nearest first, into `nearby`; `skip` left out.
    private static void Gather(Vector3 point, float radius, Damageable skip)
    {
        nearby.Clear();
        nearbyDistance.Clear();
        float sqrRadius = radius * radius;
        List<Enemy> enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == skip)
            {
                continue;
            }
            Vector3 offset = enemy.transform.position - point;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr > sqrRadius)
            {
                continue;
            }
            int at = nearbyDistance.Count;
            while (at > 0 && nearbyDistance[at - 1] > sqr)
            {
                at--;
            }
            nearby.Insert(at, enemy);
            nearbyDistance.Insert(at, sqr);
        }
    }

    private void FireShrapnel(Vector3 point, int count, Damageable hit)
    {
        if (shrapnelPool == null || shrapnelStats == null)
        {
            return;
        }
        Gather(point, shrapnelSeekRadius, hit);
        float startAngle = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            Vector3 direction;
            if (i < nearby.Count)
            {
                direction = nearby[i].AimPoint - point;
                direction.y = 0f;
            }
            else
            {
                float angle = (startAngle + i * 360f / count) * Mathf.Deg2Rad;
                direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }
            direction.Normalize();
            Projectile shard = shrapnelPool.Get(point + direction * 0.3f, Quaternion.LookRotation(direction));
            shard.Launch(shrapnelPool, direction, shrapnelStats.ProjectileSpeed, shrapnelStats, shrapnelStats.Lifetime, null);
            ShrapnelFired++;
        }
    }

    private void PlayLook(Vector3 point, float radius)
    {
        if (puffs == null)
        {
            return;
        }
        float size = radius * puffScale;
        if (coreSize > 0f)
        {
            Emit(puffs, point, Vector3.up * 0.3f, 0.28f, size * coreSize, flashColorHot, 200f);
        }
        for (int i = 0; i < flashCount; i++)
        {
            Vector3 outward = Random.onUnitSphere;
            outward.y = Mathf.Abs(outward.y) * 0.6f;
            Emit(puffs, point + outward * 0.15f, outward * Random.Range(flashSpeed.x, flashSpeed.y), Random.Range(0.18f, 0.3f),
                size * Random.Range(0.7f, 1.2f), Color.Lerp(flashColor, flashColorHot, Random.value), 200f);
        }
        if (sparks != null)
        {
            for (int i = 0; i < sparkCount; i++)
            {
                Vector3 outward = Random.onUnitSphere;
                outward.y = Mathf.Lerp(0.15f, 0.9f, Mathf.Abs(outward.y));
                outward.Normalize();
                ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams
                {
                    position = point + outward * 0.2f,
                    velocity = outward * Random.Range(sparkSpeed.x, sparkSpeed.y),
                    startLifetime = Random.Range(sparkLifetime.x, sparkLifetime.y),
                    startSize = Random.Range(sparkSize.x, sparkSize.y),
                    startColor = Color.white
                };
                sparks.Emit(spark, 1);
            }
        }
        if (smoke == null)
        {
            return;
        }
        for (int i = 0; i < smokeCount; i++)
        {
            Vector3 outward = Random.insideUnitSphere;
            outward.y = Mathf.Abs(outward.y) * 0.5f;
            Emit(smoke, point + outward * smokeSpread * 0.5f, outward * smokeSpread * 1.5f + Vector3.up * Random.Range(smokeRise.x, smokeRise.y),
                Random.Range(smokeLifetime.x, smokeLifetime.y), size * Random.Range(smokeSize.x, smokeSize.y),
                Color.Lerp(smokeColor, Color.white, Random.Range(0f, 0.15f)), 40f);
        }
    }

    private void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float life, float size, Color color, float spin)
    {
        ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startLifetime = life,
            startSize = size,
            startColor = color,
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            angularVelocity3D = new Vector3(Random.Range(-spin, spin), Random.Range(-spin, spin), Random.Range(-spin, spin))
        };
        system.Emit(puff, 1);
    }

    // The flash (fire tint, quick) or the smoke (no tint, swells and lingers, brakes hard).
    private ParticleSystem BuildPuffs(string objectName, bool isSmoke)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.gravityModifier = 0f;
        main.startRotation3D = true;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // Flash: pop in big, then shrink away. Smoke: swell, hang, then shrink away at the end.
        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve curve = isSmoke
            ? new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(0.75f, 1.1f), new Keyframe(1f, 0f))
            : new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1.2f), new Keyframe(1f, 0f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Flash only: burn from the start colour to fire to ash (multiplies each puff's colour).
        ParticleSystem.ColorOverLifetimeModule tint = system.colorOverLifetime;
        tint.enabled = !isSmoke;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.2f),
                new GradientColorKey(fireColor, 0.5f), new GradientColorKey(ashColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        tint.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.LimitVelocityOverLifetimeModule slow = system.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = 0f;
        slow.dampen = isSmoke ? 0.2f : 0.25f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = BuildIcosahedron();
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = puffMaterial != null
            ? puffMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")) { name = "BlastPuffFallback" };

        system.Play();
        return system;
    }

    // Streaks: billboards stretched along their velocity, burning hot -> spark colour, shrinking away, falling a little.
    private ParticleSystem BuildSparks()
    {
        GameObject go = new GameObject("BlastSparks");
        go.transform.SetParent(transform, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;
        main.gravityModifier = sparkGravity;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));

        ParticleSystem.ColorOverLifetimeModule tint = system.colorOverLifetime;
        tint.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(sparkColorHot, 0f), new GradientColorKey(sparkColor, 0.5f),
                new GradientColorKey(sparkColor * 0.7f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        tint.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.LimitVelocityOverLifetimeModule slow = system.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = 0f;
        slow.dampen = 0.06f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.05f;
        renderer.lengthScale = 2f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = puffMaterial != null
            ? puffMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "BlastSparkFallback" };

        system.Play();
        return system;
    }

    // Flat-shaded icosahedron (20 faces), diameter 1.
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
        Mesh mesh = new Mesh { name = "BlastPuffMesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
