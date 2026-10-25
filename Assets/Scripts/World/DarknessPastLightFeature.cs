/*
 * DarknessPastLightFeature
 * URP renderer feature (slice 7 step 2) on PC_Renderer: URP's full-screen pass running the TD/DarknessPastLight
 * material, limited to the main game camera so the draft-card and build-bar preview cameras (and test cameras) stay
 * undarkened. The Scene view gets it only with includeSceneView ticked.
 *
 * - Set up on the renderer with injection point Before Rendering Transparents, fetch colour on, requirements Depth:
 *   opaque things past the light's edge darken; the ring, health bars, damage numbers and particles draw on top.
 * - The circle, fade width and dark tint are globals set by BaseLightVisual.
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DarknessPastLightFeature : FullScreenPassRendererFeature
{
    [Tooltip("Also darken the Scene view (off keeps scene editing readable).")]
    public bool includeSceneView;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType type = renderingData.cameraData.cameraType;
        bool allowed = type == CameraType.SceneView
            ? includeSceneView
            : type == CameraType.Game && renderingData.cameraData.camera.CompareTag("MainCamera");
        if (allowed)
        {
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
