/*
 * BaseLight
 * The base's lit circle (TD slice 4): the safe/build zone. In the light enemies go for the base, in the dark they hunt
 * the player (the targeting and spawn rules read this component).
 *
 * - Sits on BasePoint next to BaseLayout. BasePosition = this object (the base itself); Center = the light circle's
 *   centre, which is not always the base (see centreOnBuild). Code that means "the base" uses BasePosition.
 * - centreOnBuild off (the old behaviour): the circle is centred on the base; TargetRadius = distance to the farthest
 *   corner of any placed chunk + margin (no chunk yet: the base's TargetRadius + margin) + extraRadius.
 * - centreOnBuild on (2026-10-28, user's option 1): the circle is centred on the middle of what is built (the box
 *   around the base and every placed chunk), so a base built out on one side doesn't light empty ground on the
 *   others; radius = farthest corner from that centre + margin + extraRadius. The light never dims: the new circle is
 *   grown until it still contains the previous one, so a shift toward new chunks never darkens lit ground.
 * - extraRadius (12.5) is a flat bonus: the base-chunk light is twice as large, while each chunk still grows it by
 *   the same amount. Recomputed on BaseLayout.LayoutChanged; never shrinks.
 * - Radius and Center ease out from their current values to the targets over growDuration whenever the target
 *   changes (the first value at Start is applied instantly). Toggling centreOnBuild in Play recomputes from scratch
 *   (for comparing the two; that one may shrink).
 * - The player counts as inside at Radius and as outside only past Radius + hysteresis, so standing on the edge does
 *   not flicker. PlayerInsideChanged(inside) fires on every change.
 * - Contains(point, extra) is a plain flat-distance test from Center against the current Radius.
 * - Shown by a BaseLightIndicator ground ring (debug look until the lighting pass).
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public class BaseLight : MonoBehaviour
{
    [SerializeField] private BaseLayout layout;
    [Tooltip("Units of light past the farthest chunk corner (or past the base box before any chunk is placed).")]
    [SerializeField, Min(0f)] private float margin = 4f;
    [Tooltip("Flat bonus added on top, so the light is bigger without growing faster (12.5 = twice the base-chunk light).")]
    [SerializeField, Min(0f)] private float extraRadius = 12.5f;
    [Tooltip("Seconds the radius takes to ease up to a new, larger target.")]
    [SerializeField, Min(0f)] private float growDuration = 1.5f;
    [Tooltip("The player counts as outside only this far past the edge.")]
    [SerializeField, Min(0f)] private float hysteresis = 1f;
    [Tooltip("Base box radius used when no BaseStructure exists yet.")]
    [SerializeField, Min(0f)] private float fallbackBaseRadius = 1.2f;
    [Tooltip("Centre the light on the middle of what is built instead of on the base, so building out on one side doesn't light empty ground on the others. The light still never dims.")]
    [SerializeField] private bool centreOnBuild = true;

    private float growFrom;
    private Vector3 growFromCenter;
    private float growTime;
    private bool started;
    private bool appliedCentreOnBuild;

    public static BaseLight Instance { get; private set; }

    public event Action<bool> PlayerInsideChanged;

    /// <summary>The base itself (this object), flat. Not always the light's centre.</summary>
    public Vector3 BasePosition => new Vector3(transform.position.x, 0f, transform.position.z);
    /// <summary>The light circle's current centre, flat (eases with Radius).</summary>
    public Vector3 Center { get; private set; }
    public Vector3 TargetCenter { get; private set; }
    public float Radius { get; private set; }
    public float TargetRadius { get; private set; }
    public float Hysteresis => hysteresis;
    public bool IsPlayerInside { get; private set; } = true;

    private void Awake()
    {
        Instance = this;
        Center = TargetCenter = BasePosition;
        if (layout == null)
        {
            layout = GetComponent<BaseLayout>();
        }
    }

    private void OnEnable()
    {
        if (layout != null)
        {
            layout.LayoutChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        if (layout != null)
        {
            layout.LayoutChanged -= Refresh;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        Vector3 center;
        float radius;
        ComputeTarget(false, out center, out radius);
        Center = TargetCenter = center;
        Radius = TargetRadius = radius;
        growTime = growDuration;
        appliedCentreOnBuild = centreOnBuild;
        started = true;
        UpdatePlayer();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && started && appliedCentreOnBuild != centreOnBuild)
        {
            appliedCentreOnBuild = centreOnBuild;
            Vector3 center;
            float radius;
            ComputeTarget(false, out center, out radius);
            EaseTo(center, radius);
        }
    }

    private void Update()
    {
        if (growTime < growDuration)
        {
            growTime += Time.deltaTime;
            float t = Mathf.Clamp01(growTime / Mathf.Max(growDuration, 0.0001f));
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            Radius = Mathf.Lerp(growFrom, TargetRadius, eased);
            Center = Vector3.Lerp(growFromCenter, TargetCenter, eased);
        }
        UpdatePlayer();
    }

    /// <summary>Whether a world point is within the current radius (+ extra).</summary>
    public bool Contains(Vector3 point, float extra = 0f)
    {
        return FlatDistance(point) <= Radius + extra;
    }

    /// <summary>Flat distance from the light's current centre.</summary>
    public float FlatDistance(Vector3 point)
    {
        Vector3 offset = point - Center;
        offset.y = 0f;
        return offset.magnitude;
    }

    [ContextMenu("Refresh")]
    public void Refresh()
    {
        if (!started)
        {
            return;
        }
        Vector3 center;
        float radius;
        ComputeTarget(true, out center, out radius);
        if (radius <= TargetRadius + 0.001f && (center - TargetCenter).sqrMagnitude < 0.0001f)
        {
            return;
        }
        EaseTo(center, Mathf.Max(radius, TargetRadius));
    }

    private void EaseTo(Vector3 center, float radius)
    {
        growFrom = Radius;
        growFromCenter = Center;
        TargetCenter = center;
        TargetRadius = radius;
        growTime = growDuration > 0f ? 0f : growDuration;
        if (growDuration <= 0f)
        {
            Radius = TargetRadius;
            Center = TargetCenter;
        }
    }

    // The circle's target centre and radius. keepOld (centreOnBuild only): grow it until it contains the current target
    // circle, so shifting toward new chunks never darkens ground that was lit.
    private void ComputeTarget(bool keepOld, out Vector3 center, out float radius)
    {
        Vector3 basePosition = BasePosition;
        float baseRadius = BaseStructure.Active.Count > 0 ? BaseStructure.Active[0].TargetRadius : fallbackBaseRadius;
        List<Vector3> corners = new List<Vector3>();
        Vector2 min = new Vector2(basePosition.x - baseRadius, basePosition.z - baseRadius);
        Vector2 max = new Vector2(basePosition.x + baseRadius, basePosition.z + baseRadius);
        if (layout != null && layout.Grid != null)
        {
            foreach (BaseLayout.ChunkPlacement placement in layout.Placements)
            {
                if (placement == null || placement.chunk == null)
                {
                    continue;
                }
                int rotation = placement.rotation & 3;
                Vector2Int origin = layout.SlotOrigin(placement.slot);
                Vector2Int size = new Vector2Int(placement.chunk.RotatedWidth(rotation), placement.chunk.RotatedHeight(rotation));
                Vector3 a = layout.CellCorner(origin);
                Vector3 b = layout.CellCorner(origin + size);
                corners.Add(new Vector3(a.x, 0f, a.z));
                corners.Add(new Vector3(a.x, 0f, b.z));
                corners.Add(new Vector3(b.x, 0f, a.z));
                corners.Add(new Vector3(b.x, 0f, b.z));
                min = Vector2.Min(min, new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z)));
                max = Vector2.Max(max, new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
            }
        }

        center = centreOnBuild ? new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f) : basePosition;
        float farthest = Flat(center, basePosition) + baseRadius;
        foreach (Vector3 corner in corners)
        {
            farthest = Mathf.Max(farthest, Flat(center, corner));
        }
        radius = farthest + margin + extraRadius;

        if (keepOld && centreOnBuild)
        {
            radius = Mathf.Max(radius, Flat(center, TargetCenter) + TargetRadius);
        }
    }

    private static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void UpdatePlayer()
    {
        PlayerHealth player = PlayerHealth.Instance;
        if (player == null)
        {
            return;
        }
        float distance = FlatDistance(player.transform.position);
        bool inside = IsPlayerInside ? distance <= Radius + hysteresis : distance <= Radius;
        if (inside != IsPlayerInside)
        {
            IsPlayerInside = inside;
            PlayerInsideChanged?.Invoke(inside);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        float radius = Application.isPlaying ? Radius : margin + fallbackBaseRadius + extraRadius;
        Vector3 c = (Application.isPlaying ? Center : BasePosition) + Vector3.up * 0.05f;
        Vector3 last = c + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64f;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}
