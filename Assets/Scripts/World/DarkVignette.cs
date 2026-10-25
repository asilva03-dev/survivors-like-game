/*
 * DarkVignette
 * Tightens the vignette while the player is in the dark (slice 7 step 4). Sits on the `DarkVolume` scene object: a
 * global Volume (priority above `Global Volume`) whose profile, Settings/DarkVignetteProfile, holds only the dark
 * Vignette values. This script eases that volume's weight from 0 (light: the normal vignette from SampleSceneProfile)
 * to 1 (dark: the profile's values) over fadeTime seconds, and back on returning to the light; URP blends the two.
 *
 * - Tune the dark look on DarkVignetteProfile (Vignette intensity / smoothness); the light look stays on
 *   SampleSceneProfile. Both are assets, so changes made in Play mode stay.
 * - Reads BaseLight.IsPlayerInside (with its hysteresis), the same state as targeting and build mode.
 */

using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Volume))]
public class DarkVignette : MonoBehaviour
{
    [Tooltip("Seconds to blend between the light and dark vignette.")]
    [SerializeField, Min(0.01f)] private float fadeTime = 0.5f;

    private Volume volume;
    private float blend;

    public float Blend => blend;

    private void Awake()
    {
        volume = GetComponent<Volume>();
        volume.weight = 0f;
    }

    private void Update()
    {
        BaseLight baseLight = BaseLight.Instance;
        float target = baseLight != null && !baseLight.IsPlayerInside ? 1f : 0f;
        blend = Mathf.MoveTowards(blend, target, Time.deltaTime / fadeTime);
        volume.weight = Mathf.SmoothStep(0f, 1f, blend);
    }
}
