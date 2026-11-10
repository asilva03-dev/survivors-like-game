/*
 * ScreenOutlineFeature
 * URP renderer feature (2026-11-10, with the cel shading) on PC_Renderer: URP's full-screen pass running the
 * TD/ScreenOutline material (Materials/ScreenOutline.mat), which draws dark lines along depth jumps and sharp turns in
 * surface direction, for the cartoon look. Limited to the main game camera, like DarknessPastLightFeature, so the
 * draft-card and build-bar preview cameras stay clean; the Scene view gets it only with includeSceneView ticked.
 *
 * - Set up on the renderer with injection point Before Rendering Transparents, fetch colour on, requirements Depth +
 *   Normal, and listed just before DarknessPastLight so the lines fade into the dark like everything else.
 * - Line colour, thickness and thresholds live on the material. Untick the feature on PC_Renderer to turn it off.
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ScreenOutlineFeature : FullScreenPassRendererFeature
{
    [Tooltip("Also draw outlines in the Scene view.")]
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
