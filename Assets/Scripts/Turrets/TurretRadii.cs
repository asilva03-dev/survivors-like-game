/*
 * TurretRadii
 * On a turret: draws its range on the ground as a circle outline in rangeColor (white), not filled.
 *
 * - Only visible while build mode is open and RangeVisible is set (the ghost / the selected turret, later slice 6
 *   steps). The no-build area is shown as red strip tiles by BuildStripHighlight (the red spacing disc was removed in
 *   slice 6 step 2).
 * - The ring is a looped LineRenderer child built in Awake and resized in LateUpdate when the range changes, so
 *   tuning the stats in Play updates it live. The turret root never rotates, so it stays flat. lineMaterial =
 *   WeaponRange.mat (a Sprites/Default material is made if unset).
 */

using UnityEngine;

[RequireComponent(typeof(Turret))]
public class TurretRadii : MonoBehaviour
{
    [SerializeField] private bool show = true;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color rangeColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField, Min(0.005f)] private float lineWidth = 0.06f;
    [SerializeField, Min(0f)] private float lineHeight = 0.05f;
    [SerializeField, Range(16, 256)] private int segments = 96;

    private Turret turret;
    private LineRenderer line;
    private float drawnRange = -1f;

    // The white range ring: only for the ghost and the selected turret (set by later build-mode steps).
    public bool RangeVisible { get; set; }
    public bool RingShown => line != null && line.enabled;

    private void Awake()
    {
        turret = GetComponent<Turret>();

        Transform ring = new GameObject("RangeRing").transform;
        ring.SetParent(transform, false);
        ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
        line = ring.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        line.useWorldSpace = false;
        line.loop = true;
        line.alignment = LineAlignment.TransformZ;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void LateUpdate()
    {
        line.enabled = show && BuildMode.IsActive && RangeVisible;
        if (!line.enabled)
        {
            return;
        }

        line.transform.position = new Vector3(transform.position.x, lineHeight, transform.position.z);
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = rangeColor;
        line.endColor = rangeColor;

        float range = turret.Range;
        if (!Mathf.Approximately(range, drawnRange))
        {
            drawnRange = range;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * range);
            }
        }
    }
}
