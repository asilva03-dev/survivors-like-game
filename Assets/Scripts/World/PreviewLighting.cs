/*
 * PreviewLighting
 * Daylight for the off-screen picture renders (slice 7 step 5). The draft cards (ChunkPreview) and the build-bar /
 * upgrade-panel pictures (DefensePreview) are lit by the scene's own lights, which are a dim night since the lighting
 * pass; this puts the old daylight back for exactly the length of one preview render.
 *
 * - Sits on `Directional Light` (sun = that light). PreviewLighting.Begin() before a preview Camera.Render() and End()
 *   right after: the sun's intensity / colour / colour temperature, the ambient colour and the reflection intensity
 *   switch to the preview values and back. It all happens inside one call, so the game view never sees it.
 * - Begin/End nest (a counter), and do nothing when no PreviewLighting exists.
 * - Preview values default to the scene's pre-slice-7 daylight; tune them here to change only the pictures.
 */

using UnityEngine;

public class PreviewLighting : MonoBehaviour
{
    [SerializeField] private Light sun;
    [SerializeField, Min(0f)] private float intensity = 2f;
    [SerializeField] private bool useColorTemperature = true;
    [SerializeField, Range(1500f, 20000f)] private float colorTemperature = 5000f;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Color ambient = new Color(0.212f, 0.227f, 0.259f);
    [SerializeField, Min(0f)] private float reflectionIntensity = 1f;

    private static PreviewLighting instance;
    private static int depth;

    private float savedIntensity;
    private bool savedUseTemperature;
    private float savedTemperature;
    private Color savedColor;
    private Color savedAmbient;
    private float savedReflection;

    private void Awake()
    {
        instance = this;
        if (sun == null)
        {
            sun = GetComponent<Light>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            depth = 0;
        }
    }

    public static void Begin()
    {
        if (instance == null || depth++ > 0)
        {
            return;
        }
        instance.Apply();
    }

    public static void End()
    {
        if (instance == null || depth == 0 || --depth > 0)
        {
            return;
        }
        instance.Restore();
    }

    private void Apply()
    {
        savedAmbient = RenderSettings.ambientLight;
        savedReflection = RenderSettings.reflectionIntensity;
        RenderSettings.ambientLight = ambient;
        RenderSettings.reflectionIntensity = reflectionIntensity;
        if (sun == null)
        {
            return;
        }
        savedIntensity = sun.intensity;
        savedUseTemperature = sun.useColorTemperature;
        savedTemperature = sun.colorTemperature;
        savedColor = sun.color;
        sun.intensity = intensity;
        sun.useColorTemperature = useColorTemperature;
        sun.colorTemperature = colorTemperature;
        sun.color = color;
    }

    private void Restore()
    {
        RenderSettings.ambientLight = savedAmbient;
        RenderSettings.reflectionIntensity = savedReflection;
        if (sun == null)
        {
            return;
        }
        sun.intensity = savedIntensity;
        sun.useColorTemperature = savedUseTemperature;
        sun.colorTemperature = savedTemperature;
        sun.color = savedColor;
    }
}
