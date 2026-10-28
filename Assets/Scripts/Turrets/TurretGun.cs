/*
 * TurretGun
 * The gun on a turret: a GunWeapon with no WeaponLoadout (so it never counts toward the player's maxGuns) that
 * targets the enemy closest to the base along its path (tower-defense "first" targeting).
 *
 * - Each frame it picks, among targets in range of its owner (flat distance), the one with the lowest flow-field
 *   distance to the base (FlowField.GetDistance of the enemy's cell). Enemies off the grid or on a cell with no
 *   route are ranked by their straight distance to the base instead (same units: 10 per cell).
 * - Range, damage, fire rate, turn speed and the bullet come from its own GunStats asset (targets Enemy only, ring
 *   radius 0, ring/claims off). The owner (WeaponBase.owner) is the turret's aim point; the gun object sits on it
 *   and yaws toward the target, turning the turret body with it.
 * - With no target it scans left and right around the turret's rest direction (toward the nearest path cell); the
 *   sweep size and speed are on the TurretDefinition. It turns to a new target at the stats' turnSpeed.
 */

using UnityEngine;

public class TurretGun : GunWeapon
{
    private const float CostPerUnit = 10f;

    private Turret turret;
    private float idleTime;
    private int lastIdleFrame = -10;

    private Turret Turret => turret != null ? turret : (turret = GetComponentInParent<Turret>());

    // The stats in use: the shared asset until an upgrade is bought, then this turret's own copy (Turret builds it).
    public GunStats CurrentStats => Stats;

    public void UseStats(GunStats stats)
    {
        ReplaceStats(stats);
    }

    // Points the gun at the turret's rest direction at once (called when the turret is placed).
    public void FaceRest()
    {
        SnapFacing(Turret != null ? Turret.RestDirection : transform.forward);
        idleTime = 0f;
    }

    // Idle scan: a sine sweep of IdleScanAngle to each side of the rest direction. It restarts from the rest
    // direction whenever the gun comes back from having a target, so it never jumps mid-sweep.
    protected override Vector3 IdleFacing()
    {
        Turret owner = Turret;
        if (owner == null || owner.Definition == null)
        {
            return transform.forward;
        }

        if (lastIdleFrame < Time.frameCount - 1)
        {
            idleTime = 0f;
        }

        lastIdleFrame = Time.frameCount;
        idleTime += Time.deltaTime;

        float angle = owner.Definition.IdleScanAngle * Mathf.Sin(idleTime * 2f * Mathf.PI / owner.Definition.IdleScanPeriod);
        return Quaternion.AngleAxis(angle, Vector3.up) * owner.RestDirection;
    }

    protected override Damageable FindOwnTarget()
    {
        FlowField field = FlowField.Instance;
        BuildGrid grid = field != null ? field.Grid : null;
        Vector3 basePoint = BaseLight.Instance != null ? BaseLight.Instance.BasePosition : Owner.position;
        Vector3 from = Owner.position;
        float sqrRange = Range * Range;

        Damageable best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!CanTarget(candidate.Category))
            {
                continue;
            }

            Vector3 position = candidate.transform.position;
            Vector3 offset = position - from;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrRange)
            {
                continue;
            }

            float score = PathDistance(position, field, grid, basePoint);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static float PathDistance(Vector3 position, FlowField field, BuildGrid grid, Vector3 basePoint)
    {
        if (field != null && grid != null)
        {
            int distance = field.GetDistance(grid.WorldToCell(position));
            if (distance != FlowField.Unreachable)
            {
                return distance;
            }
        }

        Vector3 offset = position - basePoint;
        offset.y = 0f;
        return offset.magnitude * CostPerUnit;
    }
}
