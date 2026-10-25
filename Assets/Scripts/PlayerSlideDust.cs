/*
 * PlayerSlideDust
 * On the Player: low-poly 3D dust clouds that shoot out behind the feet while sliding (PlayerController.IsSliding on
 * the ground). Visual only.
 *
 * - Builds one world-space ParticleSystem (`SlideDust`, its own emission off) on Awake that renders a generated
 *   flat-shaded icosphere (mesh particles, lit by the scene, no shadows) with puffMaterial
 *   (`Materials/SlideDustPuff.mat`, URP Particles/Lit, opaque). Puffs are emitted by hand so each gets its own velocity.
 * - While sliding: rate goes from minRate to maxRate with the slide speed (walking -> fullRateSpeed); each puff starts
 *   just behind the feet and flies backward (backSpeed) and up (upSpeed) with some sideways spread, slows (drag) and
 *   tumbles (spin). Being solid, a puff doesn't fade: it pops in, swells to `growth` x its start size, then shrinks
 *   away to nothing at the end of its life. A slide start also fires startBurst puffs at once.
 * - A ground dash (PlayerController.IsDashing) fires dashBurst puffs once at its start (x dashBurstSpeed).
 * - Puffs already in the air finish their life after the slide ends.
 */

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerSlideDust : MonoBehaviour
{
    [SerializeField] private Material puffMaterial;
    [SerializeField, Min(0f)] private float minRate = 14f;
    [SerializeField, Min(0f)] private float maxRate = 36f;
    [Tooltip("Slide speed at which maxRate is reached (the dash is 18).")]
    [SerializeField, Min(0.1f)] private float fullRateSpeed = 16f;
    [SerializeField, Min(0)] private int startBurst = 8;
    [Tooltip("Puffs fired once when a ground dash starts.")]
    [SerializeField, Min(0)] private int dashBurst = 6;
    [Tooltip("Speed multiplier of the dash burst's puffs.")]
    [SerializeField, Min(0f)] private float dashBurstSpeed = 1.2f;
    [SerializeField] private Vector2 lifetime = new Vector2(0.45f, 0.75f);
    [Tooltip("Puff diameter when it has popped in.")]
    [SerializeField] private Vector2 startSize = new Vector2(0.55f, 1.0f);
    [Tooltip("Peak size multiplier before the puff shrinks away.")]
    [SerializeField, Min(1f)] private float growth = 1.6f;
    [SerializeField] private Vector2 backSpeed = new Vector2(1.5f, 3.5f);
    [SerializeField] private Vector2 upSpeed = new Vector2(0.4f, 1.3f);
    [Tooltip("Sideways speed range (either side).")]
    [SerializeField, Min(0f)] private float sideSpread = 1f;
    [Tooltip("How fast the puffs slow down (limit-velocity dampen).")]
    [SerializeField, Range(0f, 1f)] private float drag = 0.15f;
    [Tooltip("Max tumble speed, degrees per second on each axis.")]
    [SerializeField, Min(0f)] private float spin = 180f;
    [Tooltip("Puffs start this far behind the feet and this high above them.")]
    [SerializeField] private float behindOffset = 0.45f;
    [SerializeField] private float heightOffset = 0.25f;
    [SerializeField] private Color color = new Color(0.88f, 0.87f, 0.84f, 1f);

    private PlayerController controller;
    private ParticleSystem dust;
    private float pending;
    private bool wasSliding;
    private bool wasDashing;

    public int ParticleCount => dust != null ? dust.particleCount : 0;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        dust = BuildSystem();
    }

    private void OnDestroy()
    {
        if (dust != null)
        {
            Destroy(dust.gameObject);
        }
    }

    private void LateUpdate()
    {
        bool dashing = controller.IsDashing && controller.IsGrounded;
        if (dashing && !wasDashing)
        {
            for (int i = 0; i < dashBurst; i++)
            {
                EmitPuff(dashBurstSpeed);
            }
        }
        wasDashing = dashing;

        bool sliding = controller.IsSliding && controller.IsGrounded;
        if (!sliding)
        {
            wasSliding = false;
            pending = 0f;
            return;
        }

        if (!wasSliding)
        {
            wasSliding = true;
            for (int i = 0; i < startBurst; i++)
            {
                EmitPuff(1.3f);
            }
        }

        float t = Mathf.InverseLerp(5f, fullRateSpeed, controller.SlideSpeed);
        pending += Mathf.Lerp(minRate, maxRate, t) * Time.deltaTime;
        while (pending >= 1f)
        {
            pending -= 1f;
            EmitPuff(1f);
        }
    }

    private void EmitPuff(float speedScale)
    {
        Vector3 forward = controller.SlideDirection;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);

        Vector3 feet = new Vector3(transform.position.x, controller.FeetHeight, transform.position.z);
        Vector3 position = feet - forward * behindOffset + Vector3.up * heightOffset
            + side * Random.Range(-0.3f, 0.3f);
        Vector3 velocity = (-forward * Random.Range(backSpeed.x, backSpeed.y)
            + Vector3.up * Random.Range(upSpeed.x, upSpeed.y)
            + side * Random.Range(-sideSpread, sideSpread)) * speedScale;

        ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startLifetime = Random.Range(lifetime.x, lifetime.y),
            startSize = Random.Range(startSize.x, startSize.y),
            startColor = color,
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            angularVelocity3D = new Vector3(Random.Range(-spin, spin), Random.Range(-spin, spin), Random.Range(-spin, spin))
        };
        dust.Emit(puff, 1);
    }

    private ParticleSystem BuildSystem()
    {
        GameObject go = new GameObject("SlideDust");
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.gravityModifier = 0f;
        main.startRotation3D = true;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // Pop in, swell to `growth`, shrink away to nothing.
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(0.6f, growth), new Keyframe(1f, 0f));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        ParticleSystem.LimitVelocityOverLifetimeModule slow = system.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = 0f;
        slow.dampen = drag;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = BuildIcosphere();
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = puffMaterial != null
            ? puffMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")) { name = "SlideDustFallback" };

        system.Play();
        return system;
    }

    // Flat-shaded icosphere (one subdivision, 80 faces), diameter 1, so it reads as a low-poly puff.
    private static Mesh BuildIcosphere()
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        List<Vector3> v = new List<Vector3>
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
        for (int i = 0; i < v.Count; i++)
        {
            v[i] = v[i].normalized * 0.5f;
        }

        // One subdivision: split every face into four.
        List<int> faces = new List<int>();
        Dictionary<long, int> mids = new Dictionary<long, int>();
        for (int i = 0; i < f.Length; i += 3)
        {
            int a = f[i], b = f[i + 1], c = f[i + 2];
            int ab = Mid(v, mids, a, b), bc = Mid(v, mids, b, c), ca = Mid(v, mids, c, a);
            faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }

        // Unshared vertices per face give the faceted look.
        Vector3[] vertices = new Vector3[faces.Count];
        int[] triangles = new int[faces.Count];
        for (int i = 0; i < faces.Count; i += 3)
        {
            Vector3 p0 = v[faces[i]], p1 = v[faces[i + 1]], p2 = v[faces[i + 2]];
            // Unity's front faces wind clockwise seen from outside; flip any face that would point inward.
            bool outward = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), p0 + p1 + p2) > 0f;
            vertices[i] = p0;
            vertices[i + 1] = outward ? p1 : p2;
            vertices[i + 2] = outward ? p2 : p1;
            triangles[i] = i;
            triangles[i + 1] = i + 1;
            triangles[i + 2] = i + 2;
        }
        Mesh mesh = new Mesh { name = "SlideDustPuffMesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int Mid(List<Vector3> v, Dictionary<long, int> mids, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        int index;
        if (mids.TryGetValue(key, out index))
        {
            return index;
        }
        v.Add(((v[a] + v[b]) * 0.5f).normalized * 0.5f);
        index = v.Count - 1;
        mids[key] = index;
        return index;
    }
}
