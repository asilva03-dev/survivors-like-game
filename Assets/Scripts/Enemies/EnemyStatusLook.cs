/*
 * EnemyStatusLook
 * Shows the Optics slows and stun on rats (2026-11-23, gun turret Optics T3-T5). Scene object "StatusLook"; one component
 * draws every enemy's status, nothing is added to the enemy prefabs.
 *
 * - Slowed (Enemy.SlowStacks > 0; 2026-11-23, user: "a misty pink gas on the slowed enemy that gains intensity"): soft
 *   see-through pink mist puffs (mistMaterial = SlowMist.mat, transparent) swirl around the rat's body. Each stack adds
 *   mistPerStack puffs per second, makes them bigger (mistSize + mistSizePerStack x stacks) and less see-through
 *   (mistAlpha + mistAlphaPerStack x stacks), so a 5-stack rat sits in a thick pink cloud. Only rats on screen.
 * - Spread (Projectile.StackSpread, Infectious Fatigue): spreadPuffs brighter puffs fly from the hit rat to the rat that
 *   caught the stack (arriving in about spreadTravelTime), then a small pink burst pops on it, so a proc is easy to see.
 * - Proc burst (Projectile.InfectProc, 2026-11-23, user: "an explosion of the pink mist that matches the actual aoe"): a
 *   flat ring of burstPuffs pink puffs shoots out from the hit rat, reaching the spread radius (InfectRadius) in burstTime,
 *   a few rise from the middle, and a ring of edgePuffs puffs appears right on the radius and hangs for edgeLifetime, so
 *   the edge of the cloud is the edge of the AOE. Own particle system with no drag (the speeds are exact).
 * - Stunned (Enemy.IsStunned): a ring of starCount yellow stars spins above the rat's head (starHeight above the top of
 *   its model) until the stun ends; each star turns to face the camera so it reads from the top-down view. Star rings are pooled (ComponentPool of a template built in Awake), never instantiated
 *   per stun.
 * - All numbers are placeholders.
 */

using System.Collections.Generic;
using UnityEngine;

public class EnemyStatusLook : MonoBehaviour
{
    [Header("Slow mist")]
    [SerializeField] private Material mistMaterial;
    [SerializeField] private Color mistColor = new Color(1f, 0.55f, 0.82f, 1f);
    [Tooltip("Mist puffs per second for each slow stack.")]
    [SerializeField, Min(0f)] private float mistPerStack = 6f;
    [Tooltip("Puff size at one stack...")]
    [SerializeField, Min(0.01f)] private float mistSize = 0.4f;
    [Tooltip("...plus this much per extra stack.")]
    [SerializeField, Min(0f)] private float mistSizePerStack = 0.1f;
    [Tooltip("Puff opacity at one stack...")]
    [SerializeField, Range(0f, 1f)] private float mistAlpha = 0.2f;
    [Tooltip("...plus this much per extra stack.")]
    [SerializeField, Range(0f, 0.3f)] private float mistAlphaPerStack = 0.06f;
    [SerializeField] private float mistLifetime = 0.9f;
    [Tooltip("How far around the rat's body the puffs start.")]
    [SerializeField, Min(0f)] private float mistSpread = 0.35f;

    [Header("Spread (Infectious Fatigue)")]
    [SerializeField, Min(0)] private int spreadPuffs = 4;
    [SerializeField] private Color spreadColor = new Color(1f, 0.4f, 0.75f, 1f);
    [SerializeField, Min(0.05f)] private float spreadTravelTime = 0.3f;
    [SerializeField, Min(0)] private int spreadBurst = 6;
    [Tooltip("Puffs in the ring that shows the spread radius when Infectious Fatigue procs.")]
    [SerializeField, Min(0)] private int burstPuffs = 32;
    [Tooltip("Seconds the ring takes to reach the spread radius.")]
    [SerializeField, Min(0.05f)] private float burstTime = 0.3f;
    [SerializeField] private Color burstColor = new Color(1f, 0.5f, 0.8f, 1f);
    [SerializeField, Range(0f, 1f)] private float burstAlpha = 0.45f;
    [Tooltip("Puffs placed right on the spread radius (the AOE outline).")]
    [SerializeField, Min(0)] private int edgePuffs = 34;
    [Tooltip("Burst and edge puff size, x the mist puff size.")]
    [SerializeField, Min(0.1f)] private float burstSize = 1.6f;
    [SerializeField, Min(0.05f)] private float edgeLifetime = 0.6f;

    [Header("Stun stars")]
    [SerializeField, Min(1)] private int starCount = 3;
    [SerializeField] private Color starColor = new Color(1f, 0.88f, 0.2f, 1f);
    [SerializeField, Min(0.01f)] private float starSize = 0.32f;
    [SerializeField, Min(0f)] private float starRingRadius = 0.32f;
    [Tooltip("How far above the top of the rat's model the stars spin.")]
    [SerializeField] private float starHeight = 0.25f;
    [Tooltip("Ring spin, degrees per second.")]
    [SerializeField] private float starSpin = 300f;

    private readonly Dictionary<Enemy, Transform> rings = new Dictionary<Enemy, Transform>();
    private readonly Dictionary<Enemy, Renderer> bodies = new Dictionary<Enemy, Renderer>();
    private readonly List<Enemy> finished = new List<Enemy>();
    private ComponentPool<Transform> ringPool;
    private ParticleSystem mist;
    private ParticleSystem burst;
    private Camera cam;

    private void Awake()
    {
        mist = BuildMist("SlowMist", 0.05f);
        burst = BuildMist("SpreadBurst", 0f);
        Transform template = BuildRingTemplate();
        Transform root = new GameObject("Stun Stars").transform;
        ringPool = new ComponentPool<Transform>(template, root, 8);
    }

    private void OnEnable()
    {
        Projectile.StackSpread += HandleSpread;
        Projectile.InfectProc += HandleProc;
    }

    private void OnDisable()
    {
        Projectile.StackSpread -= HandleSpread;
        Projectile.InfectProc -= HandleProc;
    }

    // Infectious Fatigue procced: a pink ring expanding to exactly the spread radius.
    private void HandleProc(Enemy source, float radius)
    {
        if (burst == null || source == null || !IsOnScreen(source))
        {
            return;
        }
        Vector3 centre = Middle(source);
        centre.y = Mathf.Lerp(source.FeetY, centre.y, 0.5f);
        float speed = radius / burstTime;
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < burstPuffs; i++)
        {
            float angle = (start + i * 360f / Mathf.Max(1, burstPuffs) + Random.Range(-5f, 5f)) * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            EmitTo(burst, centre, outward * speed, burstTime, mistSize * burstSize * Random.Range(0.8f, 1.2f), burstColor, burstAlpha);
        }
        float edgeStart = Random.Range(0f, 360f);
        for (int i = 0; i < edgePuffs; i++)
        {
            float angle = (edgeStart + i * 360f / Mathf.Max(1, edgePuffs)) * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            EmitTo(burst, centre + outward * radius, outward * 0.2f + Vector3.up * 0.15f, edgeLifetime,
                mistSize * burstSize * Random.Range(0.85f, 1.15f), burstColor, burstAlpha);
        }
        for (int i = 0; i < 5; i++)
        {
            Vector3 up = Random.insideUnitSphere * 0.4f;
            up.y = Mathf.Abs(up.y) + 0.6f;
            EmitTo(burst, centre, up, burstTime * 1.6f, mistSize * burstSize * Random.Range(1.2f, 1.6f), burstColor, burstAlpha);
        }
    }

    // A stack spread from `from` to `to`: puffs fly across, then a burst on the new rat.
    private void HandleSpread(Enemy from, Enemy to)
    {
        if (mist == null || from == null || to == null || !IsOnScreen(to))
        {
            return;
        }
        Vector3 start = Middle(from);
        Vector3 end = Middle(to);
        Vector3 velocity = (end - start) / spreadTravelTime;
        for (int i = 0; i < spreadPuffs; i++)
        {
            Emit(start + Random.insideUnitSphere * 0.15f, velocity * Random.Range(0.85f, 1.1f),
                spreadTravelTime * 1.4f, mistSize * Random.Range(0.6f, 0.9f), spreadColor, 0.75f);
        }
        for (int i = 0; i < spreadBurst; i++)
        {
            Vector3 outward = Random.onUnitSphere;
            outward.y = Mathf.Abs(outward.y);
            Emit(end + outward * 0.1f, outward * Random.Range(0.8f, 1.6f), mistLifetime * 0.7f, mistSize * Random.Range(0.8f, 1.2f),
                spreadColor, 0.6f);
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }
        float dt = Time.deltaTime;
        List<Enemy> enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy.IsStunned)
            {
                ShowStars(enemy);
            }
            if (enemy.SlowStacks > 0 && dt > 0f && IsOnScreen(enemy))
            {
                // Emit the expected count for this frame (whole puffs plus a chance for the remainder).
                float expected = enemy.SlowStacks * mistPerStack * dt;
                int count = Mathf.FloorToInt(expected) + (Random.value < expected - Mathf.Floor(expected) ? 1 : 0);
                for (int k = 0; k < count; k++)
                {
                    Mist(enemy);
                }
            }
        }

        // Rings whose rat is no longer stunned (or gone) go back to the pool.
        finished.Clear();
        foreach (KeyValuePair<Enemy, Transform> pair in rings)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || !pair.Key.IsStunned)
            {
                finished.Add(pair.Key);
            }
            else
            {
                pair.Value.position = HeadTop(pair.Key) + Vector3.up * starHeight;
                pair.Value.Rotate(0f, starSpin * dt, 0f, Space.World);
                if (cam != null)
                {
                    foreach (Transform star in pair.Value)
                    {
                        star.rotation = cam.transform.rotation;
                    }
                }
            }
        }
        for (int i = 0; i < finished.Count; i++)
        {
            ringPool.Release(rings[finished[i]]);
            rings.Remove(finished[i]);
            bodies.Remove(finished[i]);
        }
    }

    private void ShowStars(Enemy enemy)
    {
        if (rings.ContainsKey(enemy))
        {
            return;
        }
        rings[enemy] = ringPool.Get(HeadTop(enemy) + Vector3.up * starHeight, Quaternion.identity);
    }

    // Top of the rat's model (its renderers' bounds), else a little above its root.
    private Vector3 HeadTop(Enemy enemy)
    {
        Renderer body;
        if (!bodies.TryGetValue(enemy, out body))
        {
            body = enemy.Visual != null ? enemy.Visual.GetComponentInChildren<Renderer>() : null;
            bodies[enemy] = body;
        }
        Vector3 p = enemy.transform.position;
        float top = body != null ? body.bounds.max.y : enemy.FeetY + 0.8f;
        return new Vector3(p.x, top, p.z);
    }

    private bool IsOnScreen(Enemy enemy)
    {
        if (cam == null)
        {
            return true;
        }
        Vector3 view = cam.WorldToViewportPoint(enemy.transform.position);
        return view.z > 0f && view.x > -0.05f && view.x < 1.05f && view.y > -0.05f && view.y < 1.05f;
    }

    // Middle of the rat's body (halfway between its feet and the top of its model).
    private Vector3 Middle(Enemy enemy)
    {
        Vector3 top = HeadTop(enemy);
        return new Vector3(top.x, Mathf.Lerp(enemy.FeetY, top.y, 0.5f), top.z);
    }

    // One mist puff swirling around a slowed rat; bigger and thicker with more stacks.
    private void Mist(Enemy enemy)
    {
        int extra = Mathf.Max(0, enemy.SlowStacks - 1);
        Vector3 around = Random.insideUnitSphere * mistSpread;
        around.y = Mathf.Abs(around.y) * 0.6f;
        Vector3 drift = new Vector3(around.x, 0f, around.z) * 0.6f + Vector3.up * Random.Range(0.15f, 0.4f);
        Emit(Middle(enemy) + around, drift, mistLifetime * Random.Range(0.8f, 1.2f),
            (mistSize + mistSizePerStack * extra) * Random.Range(0.8f, 1.2f), mistColor,
            Mathf.Clamp01(mistAlpha + mistAlphaPerStack * extra));
    }

    private void Emit(Vector3 position, Vector3 velocity, float life, float size, Color color, float alpha)
    {
        EmitTo(mist, position, velocity, life, size, color, alpha);
    }

    private void EmitTo(ParticleSystem system, Vector3 position, Vector3 velocity, float life, float size, Color color, float alpha)
    {
        color.a = alpha;
        ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startLifetime = life,
            startSize = size,
            startColor = color,
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            angularVelocity3D = new Vector3(Random.Range(-60f, 60f), Random.Range(-60f, 60f), Random.Range(-60f, 60f))
        };
        system.Emit(puff, 1);
    }

    // Mist puffs (see-through, swell, fade in and out). dampen 0 = no drag (the proc ring must stop at its radius).
    private ParticleSystem BuildMist(string objectName, float dampen)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;
        main.gravityModifier = 0f;
        main.startRotation3D = true;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.2f)));

        // Fade in, hold, fade out (multiplies each puff's alpha).
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.LimitVelocityOverLifetimeModule slow = system.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = 0f;
        slow.enabled = dampen > 0f;
        slow.dampen = dampen;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = BuildIcosahedron();
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = mistMaterial != null
            ? mistMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "SlowMistFallback" };

        system.Play();
        return system;
    }

    // An inactive ring of starCount flat stars (both sides), the pool's template.
    private Transform BuildRingTemplate()
    {
        GameObject ring = new GameObject("StunStarsTemplate");
        ring.transform.SetParent(transform, false);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader != null ? shader : Shader.Find("Unlit/Color")) { name = "StunStar (runtime)" };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", starColor);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", starColor);
        }
        Mesh star = BuildStar();
        for (int i = 0; i < starCount; i++)
        {
            GameObject child = new GameObject("Star" + i, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(ring.transform, false);
            float angle = i * Mathf.PI * 2f / starCount;
            child.transform.localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * starRingRadius;
            child.transform.localScale = Vector3.one * starSize;
            child.GetComponent<MeshFilter>().sharedMesh = star;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        ring.SetActive(false);
        return ring.transform;
    }

    // A flat five-pointed star, diameter ~1, standing upright (in the XY plane), visible from both sides.
    private static Mesh BuildStar()
    {
        List<Vector3> v = new List<Vector3> { Vector3.zero };
        for (int i = 0; i < 10; i++)
        {
            float r = i % 2 == 0 ? 0.5f : 0.22f;
            float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
        List<int> t = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % 10;
            t.Add(0); t.Add(b); t.Add(a);
            t.Add(0); t.Add(a); t.Add(b);
        }
        Mesh mesh = new Mesh { name = "StunStar" };
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Flat-shaded icosahedron (20 faces), diameter 1.
    private static Mesh BuildIcosahedron()
    {
        float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] p =
        {
            new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
            new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
            new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
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
            Vector3 a = p[f[i]].normalized * 0.5f, b = p[f[i + 1]].normalized * 0.5f, c = p[f[i + 2]].normalized * 0.5f;
            bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) > 0f;
            vertices[i] = a;
            vertices[i + 1] = outward ? b : c;
            vertices[i + 2] = outward ? c : b;
            triangles[i] = i;
            triangles[i + 1] = i + 1;
            triangles[i + 2] = i + 2;
        }
        Mesh mesh = new Mesh { name = "SlowMistMesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
