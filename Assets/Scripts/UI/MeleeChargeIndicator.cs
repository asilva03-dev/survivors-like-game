/*
 * MeleeChargeIndicator
 * Circle next to the stamina bar for the melee attack. It fills from bottom to top while Mouse 1 is
 * held to charge, and flashes red after a charged swing until its attack interval is over.
 *
 * - Lives on a child of the stamina canvas. background and fill are circle Images; fill is a Filled
 *   (Vertical, origin Bottom) image.
 * - Charging: fillAmount is MeleeWeapon.ChargeNormalized; the fill switches to fullColor once the
 *   charge is complete, so the player can see when a release will be the strong attack.
 * - Recovery: after a charged release the full circle blinks between recoveryColor and
 *   recoveryDimColor at flashRate blinks per second for as long as MeleeWeapon.IsChargedLockout,
 *   which is exactly the time Mouse 1 is ignored. It disappears when attacking is possible again.
 * - Hidden otherwise. The weapon is found under the same player as the stamina canvas.
 */

using UnityEngine;
using UnityEngine.UI;

public class MeleeChargeIndicator : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image fill;
    [SerializeField] private Color chargingColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color fullColor = new Color(1f, 0.4f, 0.1f, 1f);
    [SerializeField] private Color recoveryColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color recoveryDimColor = new Color(0.45f, 0f, 0f, 1f);
    [SerializeField, Min(0.5f)] private float flashRate = 4f;

    private MeleeWeapon weapon;
    private bool shown = true;

    private void Start()
    {
        PlayerStamina player = GetComponentInParent<PlayerStamina>();
        if (player != null)
        {
            weapon = player.GetComponentInChildren<MeleeWeapon>(true);
        }

        SetShown(false);
    }

    private void LateUpdate()
    {
        if (weapon == null)
        {
            return;
        }

        if (weapon.IsChargedLockout)
        {
            SetShown(true);
            fill.fillAmount = 1f;
            bool bright = Mathf.Repeat(Time.time * flashRate, 1f) < 0.5f;
            fill.color = bright ? recoveryColor : recoveryDimColor;
            return;
        }

        bool charging = weapon.IsCharging;
        SetShown(charging);

        if (charging)
        {
            float charge = weapon.ChargeNormalized;
            fill.fillAmount = charge;
            fill.color = charge >= 1f ? fullColor : chargingColor;
        }
    }

    private void SetShown(bool visible)
    {
        if (shown == visible)
        {
            return;
        }

        shown = visible;
        background.enabled = visible;
        fill.enabled = visible;
    }
}
