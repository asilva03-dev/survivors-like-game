/*
 * TurretUpgrade
 * One upgrade tier of a turret path (Create > TD > Turret Upgrade), Bloons-style (slice 6 step 5). The name,
 * description and wood cost show on the upgrade panel; the changes are applied on top of the turret's base gun stats
 * (on that turret's own runtime copy, never the shared asset).
 *
 * - damageAdd: + damage per bullet. fireRateMultiplier: 1.25 = 25% more shots per second (1 = no change).
 * - rangeAdd: + range. pierceAdd: + enemies a bullet passes through. projectilesAdd: + bullets per shot.
 * - enableHoming: bullets steer at the target (GunStats homing, its homingStrength).
 * - Names, descriptions and numbers are placeholders for the user to rename and retune in the inspector.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Turret Upgrade", fileName = "TurretUpgrade")]
public class TurretUpgrade : ScriptableObject
{
    [SerializeField] private string upgradeName = "Upgrade";
    [SerializeField, TextArea(2, 4)] private string description = "";
    [SerializeField, Min(0)] private int woodCost = 20;

    [Header("Changes")]
    [SerializeField] private float damageAdd;
    [SerializeField, Min(0.1f)] private float fireRateMultiplier = 1f;
    [SerializeField] private float rangeAdd;
    [SerializeField] private int pierceAdd;
    [SerializeField] private int projectilesAdd;
    [SerializeField] private bool enableHoming;

    public string UpgradeName => string.IsNullOrEmpty(upgradeName) ? name : upgradeName;
    public string Description => description;
    public int WoodCost => woodCost;

    public void ApplyTo(GunStats stats)
    {
        stats.AddDamage(damageAdd);
        stats.ScaleFireRate(fireRateMultiplier);
        stats.AddRange(rangeAdd);
        stats.AddPierce(pierceAdd);
        stats.AddProjectiles(projectilesAdd);
        if (enableHoming)
        {
            stats.SetHoming(true);
        }
    }
}
