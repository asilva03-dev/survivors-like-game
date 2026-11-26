/*
 * FishercatReelStats
 * Gameplay numbers of the Fishercat's reel-in (Path 1 tier 4 "Dinner Time", 2026-11-26; Create > TD > Fishercat Reel
 * Stats). FishercatReel uses them; Turret gives the tower its own copy with the bought tiers applied (TurretUpgrade
 * reel fields), so the asset is never changed in play.
 *
 * - unlocked: off on the asset (the base tower doesn't reel); tier 4 turns it on.
 * - hpCap: only rats whose health (all layers) is at most this can be reeled in; the reel picks the highest-health one
 *   in range (user: "instant kill under HP cap"; fat rats can be caught, shadow rats only if the gun sees shadows).
 * - cooldown: seconds from the end of one reel to the next.
 * - range: 0 = the gun's range. useLightRange (tier 5): any rat inside the BaseLight instead.
 * - Numbers are placeholders (user's finalizing pass).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Fishercat Reel Stats", fileName = "FishercatReelStats")]
public class FishercatReelStats : ScriptableObject
{
    [SerializeField] private bool unlocked;
    [Tooltip("Only rats with at most this much health (all layers) are reeled in.")]
    [SerializeField, Min(0f)] private float hpCap = 100f;
    [Tooltip("Seconds between the end of one reel and the next.")]
    [SerializeField, Min(0.1f)] private float cooldown = 8f;
    [Tooltip("0 = the gun's range.")]
    [SerializeField, Min(0f)] private float range;
    [Tooltip("Reel any rat inside the base's light instead of only those in range.")]
    [SerializeField] private bool useLightRange;

    public bool Unlocked => unlocked;
    public float HpCap => hpCap;
    public float Cooldown => cooldown;
    public float Range => range;
    public bool UseLightRange => useLightRange;

    // Upgrade copies only (TurretUpgrade.ApplyTo): values only go up / on.
    public void Unlock()
    {
        unlocked = true;
    }

    public void RaiseHpCap(float cap)
    {
        hpCap = Mathf.Max(hpCap, cap);
    }

    public void SetUseLightRange()
    {
        useLightRange = true;
    }
}
