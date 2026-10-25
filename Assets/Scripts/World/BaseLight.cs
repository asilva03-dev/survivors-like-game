/*
 * BaseLight
 * The base's lit circle (TD slice 4): the safe/build zone. In the light enemies go for the base, in the dark they hunt
 * the player (the targeting and spawn rules read this component).
 *
 * - Sits on BasePoint next to BaseLayout; the circle is centred on this object.
 * - TargetRadius = distance from the centre to the farthest corner of any placed chunk + margin (no chunk yet: the
 *   base's TargetRadius + margin), plus a flat extraRadius (12.5: the base-chunk light is twice as large, while each
 *   placed chunk still grows it by the same amount). Recomputed on BaseLayout.LayoutChanged and never shrinks.
 * - Radius eases out from its current value to TargetRadius over growDuration whenever the target grows (the first
 *   value at Start is applied instantly).
 * - The player counts as inside at Radius and as outside only past Radius + hysteresis, so standing on the edge does
 *   not flicker. PlayerInsideChanged(inside) fires on every change.
 * - Contains(point, extra) is a plain flat-distance test against the current Radius.
 * - Shown by a BaseLightIndicator ground ring (debug look until the lighting pass).
 */

using System;
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

    private float growFrom;
    private float growTime;
    private bool started;

    public static BaseLight Instance { get; private set; }

    public event Action<bool> PlayerInsideChanged;

    public Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);
    public float Radius { get; private set; }
    public float TargetRadius { get; private set; }
    public float Hysteresis => hysteresis;
    public bool IsPlayerInside { get; private set; } = true;

    private void Awake()
    {
        Instance = this;
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
        TargetRadius = ComputeRadius();
        Radius = TargetRadius;
        growTime = growDuration;
        started = true;
        UpdatePlayer();
    }

    private void Update()
    {
        if (growTime < growDuration)
        {
            growTime += Time.deltaTime;
            float t = Mathf.Clamp01(growTime / Mathf.Max(growDuration, 0.0001f));
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            Radius = Mathf.Lerp(growFrom, TargetRadius, eased);
        }
        UpdatePlayer();
    }

    /// <summary>Whether a world point is within the current radius (+ extra).</summary>
    public bool Contains(Vector3 point, float extra = 0f)
    {
        return FlatDistance(point) <= Radius + extra;
    }

    public float FlatDistance(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    [ContextMenu("Refresh")]
    public void Refresh()
    {
        float target = ComputeRadius();
        if (!started || target <= TargetRadius)
        {
            return;
        }
        growFrom = Radius;
        TargetRadius = target;
        growTime = growDuration > 0f ? 0f : growDuration;
        if (growDuration <= 0f)
        {
            Radius = TargetRadius;
        }
    }

    private float ComputeRadius()
    {
        float farthest = BaseStructure.Active.Count > 0 ? BaseStructure.Active[0].TargetRadius : fallbackBaseRadius;
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
                Vector3 min = layout.CellCorner(origin);
                Vector3 max = layout.CellCorner(origin + size);
                farthest = Mathf.Max(farthest, FlatDistance(new Vector3(min.x, 0f, min.z)));
                farthest = Mathf.Max(farthest, FlatDistance(new Vector3(min.x, 0f, max.z)));
                farthest = Mathf.Max(farthest, FlatDistance(new Vector3(max.x, 0f, min.z)));
                farthest = Mathf.Max(farthest, FlatDistance(new Vector3(max.x, 0f, max.z)));
            }
        }
        return farthest + margin + extraRadius;
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
        Vector3 c = Center + Vector3.up * 0.05f;
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
