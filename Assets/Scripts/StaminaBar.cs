/*
 * StaminaBar
 * World-space stamina bar that floats above the player and faces the camera.
 *
 * - Lives on a world-space Canvas that is a child of the player.
 * - The fill is a plain Image whose right anchor is scaled by the stamina fraction,
 *   so no sprite is required.
 * - Rotation is copied from the main camera each frame so the bar always reads flat on screen.
 * - PlayDeniedFeedback() is the "not enough stamina" cue: the bar shakes in screen space and the
 *   fill flashes red, both fading out over feedbackDuration seconds. Calling it again restarts it.
 *   shakeAmplitude is in world units; the shake offset is applied to the canvas position on top of
 *   its resting local position.
 */

using UnityEngine;
using UnityEngine.UI;

public class StaminaBar : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField] private Color deniedColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField, Min(0.05f)] private float feedbackDuration = 0.3f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.1f;

    private PlayerStamina stamina;
    private Camera cam;
    private Image fillImage;
    private Color fillColor;
    private Vector3 restLocalPosition;
    private float feedbackTimer;

    private void Awake()
    {
        stamina = GetComponentInParent<PlayerStamina>();
        cam = Camera.main;
        restLocalPosition = transform.localPosition;

        if (fill != null)
        {
            fillImage = fill.GetComponent<Image>();
            if (fillImage != null)
            {
                fillColor = fillImage.color;
            }
        }
    }

    public void PlayDeniedFeedback()
    {
        feedbackTimer = feedbackDuration;
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;
        }

        if (stamina != null && fill != null)
        {
            fill.anchorMax = new Vector2(stamina.Normalized, 1f);
        }

        UpdateFeedback();
    }

    private void UpdateFeedback()
    {
        if (feedbackTimer <= 0f)
        {
            return;
        }

        feedbackTimer = Mathf.Max(0f, feedbackTimer - Time.deltaTime);
        float strength = feedbackTimer / feedbackDuration;

        Vector3 offset = Vector3.zero;
        if (cam != null && strength > 0f)
        {
            Vector2 jitter = Random.insideUnitCircle * (shakeAmplitude * strength);
            Vector3 worldOffset = cam.transform.right * jitter.x + cam.transform.up * jitter.y;
            offset = transform.parent != null ? transform.parent.InverseTransformVector(worldOffset) : worldOffset;
        }

        transform.localPosition = restLocalPosition + offset;

        if (fillImage != null)
        {
            fillImage.color = Color.Lerp(fillColor, deniedColor, strength);
        }
    }
}
