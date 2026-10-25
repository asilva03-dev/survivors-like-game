/*
 * BaseLightVisual
 * The base's campfire light (slice 7 step 1): a warm point light that follows the BaseLight circle, so the lit area
 * grows with the base. Gameplay never reads it; BaseLight's Radius stays the only source of truth for the edge.
 *
 * - Sits on a child of BasePoint with a Light (point, no shadows). It owns the light's position, range and intensity
 *   every frame: tune centreBrightness / the shape fields here, not the Light's own Intensity or Range.
 * - The light hangs above the base at heightPerRadius x Radius (at least minHeight) and reaches rangePerRadius x Radius
 *   along the ground. Intensity = centreBrightness x height^2, so the ground at the base centre gets centreBrightness
 *   whatever the radius, and the falloff toward the edge looks the same at every size.
 * - Outside Play (ExecuteAlways) it uses previewRadius so the Scene/Game view shows the base-chunk look while tuning.
 * - Darkness past the edge (slice 7 step 2): it also feeds the TD/DarknessPastLight full-screen pass
 *   (DarknessPastLightFeature on PC_Renderer) its globals: the circle (same centre and radius), fadeWidth and darkTint.
 *   Fully lit up to the edge, then a fade over fadeWidth units into darkTint. Disabling this component turns it off.
 */

using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class BaseLightVisual : MonoBehaviour
{
    [SerializeField] private BaseLight baseLight;
    [Tooltip("Ground brightness straight under the light (roughly comparable to a directional light's intensity).")]
    [SerializeField, Min(0f)] private float centreBrightness = 2.2f;
    [Tooltip("Light height above the ground as a share of the radius. Higher = softer falloff toward the edge.")]
    [SerializeField, Min(0.05f)] private float heightPerRadius = 0.9f;
    [SerializeField, Min(0.5f)] private float minHeight = 6f;
    [Tooltip("How far along the ground the light reaches, as a share of the radius (past the edge, where the darkening takes over).")]
    [SerializeField, Min(1f)] private float rangePerRadius = 1.8f;
    [Tooltip("Radius used outside Play mode (25 = the base chunk alone).")]
    [SerializeField, Min(1f)] private float previewRadius = 25f;

    [Header("Darkness past the edge")]
    [Tooltip("Units past the light's edge over which the darkness fades in.")]
    [SerializeField, Min(0f)] private float fadeWidth = 3f;
    [Tooltip("Colour multiplier for everything in the dark (black = pitch dark, white = no darkening).")]
    [SerializeField] private Color darkTint = new Color(0.5f, 0.55f, 0.68f);

    private static readonly int LightCircleId = Shader.PropertyToID("_TD_LightCircle");
    private static readonly int DarkTintId = Shader.PropertyToID("_TD_DarkTint");
    private static readonly int DarknessOnId = Shader.PropertyToID("_TD_DarknessOn");

    private Light pointLight;

    public Light Light => pointLight != null ? pointLight : (pointLight = GetComponent<Light>());

    private void Awake()
    {
        if (baseLight == null)
        {
            baseLight = GetComponentInParent<BaseLight>();
        }
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(DarknessOnId, 0f);
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Apply()
    {
        Light target = Light;
        if (target == null)
        {
            return;
        }

        if (baseLight == null)
        {
            baseLight = GetComponentInParent<BaseLight>();
        }

        float radius = previewRadius;
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        if (Application.isPlaying && baseLight != null && baseLight.Radius > 0f)
        {
            radius = baseLight.Radius;
            center = baseLight.Center;
        }

        float height = Mathf.Max(minHeight, radius * heightPerRadius);
        float reach = radius * rangePerRadius;
        transform.position = new Vector3(center.x, height, center.z);
        target.range = Mathf.Sqrt(height * height + reach * reach);
        target.intensity = centreBrightness * height * height;

        Shader.SetGlobalVector(LightCircleId, new Vector4(center.x, center.z, radius, fadeWidth));
        Shader.SetGlobalColor(DarkTintId, darkTint);
        Shader.SetGlobalFloat(DarknessOnId, isActiveAndEnabled ? 1f : 0f);
    }
}
