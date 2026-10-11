/*
 * StaminaBar
 * World-space stamina bar that floats above the player and faces the camera.
 *
 * - Lives on a world-space Canvas that is a child of the player.
 * - The fill is a plain Image whose right anchor is scaled by the stamina fraction,
 *   so no sprite is required.
 * - Rotation is copied from the main camera each frame so the bar always reads flat on screen.
 */

using UnityEngine;

public class StaminaBar : MonoBehaviour
{
    [SerializeField] private RectTransform fill;

    private PlayerStamina stamina;
    private Camera cam;

    private void Awake()
    {
        stamina = GetComponentInParent<PlayerStamina>();
        cam = Camera.main;
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
    }
}
