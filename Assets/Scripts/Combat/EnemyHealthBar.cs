/*
 * EnemyHealthBar
 * World-space health bar that floats above an enemy and faces the camera. Hidden until the enemy
 * has taken damage.
 *
 * - Lives on a world-space Canvas that is a child of the enemy (same setup as StaminaBar).
 * - The Canvas component is disabled while the enemy is at full health, so unhurt enemies cost
 *   no canvas rendering or rebuilds. Pooled enemies reset to full health on enable, so the bar
 *   hides again on reuse.
 * - The fill is a plain Image whose right anchor is scaled by Enemy.HealthNormalized, and it is
 *   only touched when the value changes.
 * - Rotation is copied from the main camera each frame while visible so the bar reads flat on screen.
 */

using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    private const float FullHealthThreshold = 0.999f;

    [SerializeField] private RectTransform fill;

    private Enemy enemy;
    private Canvas canvas;
    private Camera cam;
    private float shownFraction = -1f;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        canvas = GetComponent<Canvas>();
        cam = Camera.main;
        canvas.enabled = false;
    }

    private void OnEnable()
    {
        shownFraction = -1f;
        canvas.enabled = false;
    }

    private void LateUpdate()
    {
        if (enemy == null)
        {
            return;
        }

        float fraction = enemy.HealthNormalized;
        bool shouldShow = fraction < FullHealthThreshold;

        if (canvas.enabled != shouldShow)
        {
            canvas.enabled = shouldShow;
        }

        if (!shouldShow)
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;
        }

        if (fill != null && !Mathf.Approximately(fraction, shownFraction))
        {
            fill.anchorMax = new Vector2(fraction, 1f);
            shownFraction = fraction;
        }
    }
}
