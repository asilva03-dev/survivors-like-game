/*
 * ArrowVolley
 * One Angel Cat arrow volley (tower 6, 2026-11-25): pooled by AngelArrowStick, centred on the rat the arrow hit (it
 * follows that rat until the arrows land; user, same day) or on the spot where the arrow killed its rat. After
 * the stats' delay, arrowCount arrows appear spawnHeight above the ground spread over the circle, drop to the ground,
 * and as they land every rat within radius is hit once (no pierce cap); they stay stuck a moment, then shrink away and
 * the volley returns to the pool. Numbers in ArrowVolleyStats.
 *
 * - The arrows are copies of the `template` child (an arrow model, tip at +Z), each under a holder whose origin is the
 *   tip, made once in Awake (up to 24). Purely visual; the damage is one flat-distance check at landing.
 * - Area ring (user, 2026-11-25): a white circle outline (LineRenderer child, built in Awake) at the hit radius; it
 *   appears ringLead seconds before the arrows hit the ground (user: not on the arrow's hit) and fades out with the
 *   arrows' shrink.
 * - Hits (static) counts rats hit, for tests.
 */

using System.Collections.Generic;
using UnityEngine;

public class ArrowVolley : MonoBehaviour
{
    private const int MaxArrows = 24;

    [Tooltip("An arrow model (tip at +Z, centred), copied for each falling arrow.")]
    [SerializeField] private Transform template;
    [Tooltip("How far the arrows sink into the ground.")]
    [SerializeField, Min(0f)] private float embedDepth = 0.2f;
    [Tooltip("Most the falling arrows lean off straight down, degrees.")]
    [SerializeField, Range(0f, 45f)] private float maxLean = 14f;
    [Header("Area ring (user, 2026-11-25)")]
    [Tooltip("Line material for the white circle showing the volley's area (WeaponRange; Sprites/Default if unset).")]
    [SerializeField] private Material ringMaterial;
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Min(0.005f)] private float ringWidth = 0.07f;
    [Tooltip("Height of the ring above the volley's ground point.")]
    [SerializeField, Min(0f)] private float ringHeight = 0.06f;
    [SerializeField, Range(16, 128)] private int ringSegments = 64;
    [Tooltip("Seconds before the arrows hit the ground that the ring appears (0 = exactly as they land).")]
    [SerializeField, Min(0f)] private float ringLead = 0.05f;

    private LineRenderer ring;

    private readonly List<Transform> arrows = new List<Transform>();
    private readonly Vector3[] landing = new Vector3[MaxArrows];
    private readonly Vector3[] lean = new Vector3[MaxArrows];
    private ComponentPool<ArrowVolley> pool;
    private ArrowVolleyStats stats;
    private Damageable followed;
    private float timer;
    private bool landed;
    private int count;

    public static int Hits { get; private set; }

    private void Awake()
    {
        Transform ringObject = new GameObject("AreaRing").transform;
        ringObject.SetParent(transform, false);
        ringObject.localPosition = new Vector3(0f, ringHeight, 0f);
        ringObject.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring = ringObject.gameObject.AddComponent<LineRenderer>();
        ring.sharedMaterial = ringMaterial != null ? ringMaterial : new Material(Shader.Find("Sprites/Default"));
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.alignment = LineAlignment.TransformZ;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.enabled = false;

        if (template == null)
        {
            return;
        }
        float half = template.localScale.z * 0.45f;
        template.gameObject.SetActive(false);
        for (int i = 0; i < MaxArrows; i++)
        {
            Transform holder = new GameObject("Arrow" + i).transform;
            holder.SetParent(transform, false);
            Transform model = Instantiate(template, holder);
            model.localPosition = new Vector3(0f, 0f, -half);
            model.localRotation = Quaternion.identity;
            model.gameObject.SetActive(true);
            holder.gameObject.SetActive(false);
            arrows.Add(holder);
        }
    }

    /// <summary>Starts a volley at a ground point (y = the ground's height there). With a living `follow` rat the volley
    /// stays centred under it until the arrows land.</summary>
    public void Launch(ComponentPool<ArrowVolley> owningPool, ArrowVolleyStats volleyStats, Vector3 groundPoint, Damageable follow)
    {
        pool = owningPool;
        stats = volleyStats;
        followed = follow;
        timer = 0f;
        landed = false;
        transform.position = groundPoint;
        transform.rotation = Quaternion.identity;
        count = Mathf.Min(stats.ArrowCount, arrows.Count);

        // The area ring: the hit radius, shown from just before the arrows land (Update turns it on).
        ring.positionCount = ringSegments;
        for (int i = 0; i < ringSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / ringSegments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * stats.Radius);
        }
        ring.startWidth = ringWidth;
        ring.endWidth = ringWidth;
        SetRingAlpha(1f);
        ring.enabled = false;

        // Spread: one in the middle, the rest on a jittered sunflower spiral over the circle.
        for (int i = 0; i < arrows.Count; i++)
        {
            arrows[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < count; i++)
        {
            float r = i == 0 ? 0f : stats.Radius * 0.85f * Mathf.Sqrt((i + Random.Range(-0.3f, 0.3f)) / count);
            float a = i * 2.39996f + Random.Range(-0.3f, 0.3f);
            landing[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Vector3 tilt = Random.insideUnitCircle * Mathf.Tan(maxLean * Mathf.Deg2Rad);
            lean[i] = new Vector3(tilt.x, -1f, tilt.y).normalized;
        }
    }

    private void Update()
    {
        if (stats == null || pool == null)
        {
            return;
        }

        timer += Time.deltaTime;
        if (!landed && followed != null)
        {
            if (followed.isActiveAndEnabled)
            {
                transform.position = AngelArrowStick.GroundPoint(followed.transform.position);
            }
            else
            {
                followed = null;
            }
        }
        float t = timer - stats.Delay;
        if (t < 0f)
        {
            return;
        }

        float fallStart = stats.AppearTime;
        float fallEnd = fallStart + stats.FallTime;
        float stayEnd = fallEnd + stats.StickTime;
        float end = stayEnd + stats.ShrinkTime;

        float scale = t < fallStart ? t / fallStart : t < stayEnd ? 1f : 1f - Mathf.Clamp01((t - stayEnd) / stats.ShrinkTime);
        float drop = t < fallStart ? 0f : Mathf.Clamp01((t - fallStart) / stats.FallTime);
        drop *= drop;
        ring.enabled = t >= fallEnd - ringLead;
        SetRingAlpha(t < stayEnd ? 1f : scale);

        for (int i = 0; i < count; i++)
        {
            Transform arrow = arrows[i];
            Vector3 tipAtGround = landing[i] + lean[i] * (embedDepth / Mathf.Max(0.2f, -lean[i].y));
            Vector3 start = landing[i] - lean[i] * (stats.SpawnHeight / Mathf.Max(0.2f, -lean[i].y));
            arrow.localPosition = Vector3.Lerp(start, tipAtGround, drop);
            arrow.localRotation = Quaternion.LookRotation(lean[i]);
            arrow.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            arrow.gameObject.SetActive(true);
        }

        if (!landed && t >= fallEnd)
        {
            landed = true;
            Strike();
        }

        if (t >= end)
        {
            for (int i = 0; i < count; i++)
            {
                arrows[i].gameObject.SetActive(false);
            }
            ring.enabled = false;
            ComponentPool<ArrowVolley> owner = pool;
            pool = null;
            stats = null;
            followed = null;
            owner.Release(this);
        }
    }

    private void SetRingAlpha(float alpha)
    {
        Color c = ringColor;
        c.a *= alpha;
        ring.startColor = c;
        ring.endColor = c;
    }

    // Every rat inside the circle, once.
    private void Strike()
    {
        Vector3 centre = transform.position;
        for (int i = Damageable.All.Count - 1; i >= 0; i--)
        {
            if (i >= Damageable.All.Count)
            {
                continue;
            }
            Damageable candidate = Damageable.All[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !stats.CanTarget(candidate.Category)
                || !ShadowSight.CanTarget(candidate, stats.DetectsShadow))
            {
                continue;
            }
            Vector3 offset = candidate.transform.position - centre;
            offset.y = 0f;
            if (offset.magnitude - candidate.TargetRadius > stats.Radius)
            {
                continue;
            }
            candidate.TakeHit(stats.DamageFor(candidate.Category), stats.HitFlags);
            Hits++;
        }
    }
}
