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
 * - Runners (athletic rats, 2026-11-21) beeline to the base, so they are ranked by their straight distance to it.
 * - Shadow rats (2026-11-21) are skipped unless its stats detect shadows or the rat stands in a RevealLight.
 * - With no target it scans left and right around the turret's rest direction (toward the nearest path cell); the
 *   sweep size and speed are on the TurretDefinition. It turns to a new target at the stats' turnSpeed.
 * - For animation (2026-11-24, Fishercat): Recharge (0 right after a shot, 1 when ready), HasTarget (a target in range
 *   this frame) and the Fired event (after each shot). Read-only; nothing about firing changes.
 * - MeleeTarget (2026-11-25, SwordsCat): while a TurretSwing sets it, the gun faces that rat and skips its shots (the
 *   cooldown still runs). Null on every other tower, so they are unchanged. HoldFire (same day, melee-mode linger): no
 *   shots while set, aiming unchanged; false on every other tower.
 */

using UnityEngine;

public class TurretGun : GunWeapon
{
    private const float CostPerUnit = 10f;

    private Turret turret;
    private float idleTime;
    private int lastIdleFrame = -10;

    private Turret Turret => turret != null ? turret : (turret = GetComponentInParent<Turret>());

    /// <summary>0 right after a shot, rising to 1 when the next one may fire.</summary>
    public float Recharge => 1f - Mathf.Clamp01(CooldownLeft / Mathf.Max(0.01f, Stats.AttackInterval));
    /// <summary>A target was in range this frame.</summary>
    public bool HasTarget { get; private set; }
    /// <summary>Raised after every shot.</summary>
    public event System.Action Fired;

    /// <summary>Set by a TurretSwing while a rat is within its reach: the gun faces that rat and holds its shot.
    /// Null (the default, every other tower) changes nothing.</summary>
    public Damageable MeleeTarget { get; set; }
    /// <summary>Set by a TurretSwing while its tower is in melee mode: no shots. False on every other tower.</summary>
    public bool HoldFire { get; set; }

    protected override void Fire(Damageable target)
    {
        if (MeleeTarget != null || HoldFire)
        {
            return;
        }
        base.Fire(target);
        Fired?.Invoke();
    }

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

    /// <summary>Turns the gun (and the body on it) to face a flat direction at once (SwordsCat swings, 2026-11-25).</summary>
    public void SnapTo(Vector3 direction)
    {
        SnapFacing(direction);
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
        if (MeleeTarget != null && MeleeTarget.isActiveAndEnabled)
        {
            HasTarget = true;
            return MeleeTarget;
        }

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
            if (!CanTarget(candidate.Category) || !ShadowSight.CanTarget(candidate, Stats.DetectsShadow))
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

            Enemy enemy = candidate as Enemy;
            bool jumper = enemy != null && enemy.Stats != null && enemy.Stats.JumpsWalls;
            float score = PathDistance(position, field, grid, basePoint, jumper);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        HasTarget = best != null;
        return best;
    }

    private static float PathDistance(Vector3 position, FlowField field, BuildGrid grid, Vector3 basePoint, bool jumper)
    {
        if (field != null && grid != null)
        {
            int distance = jumper ? FlowField.Unreachable : field.GetDistance(grid.WorldToCell(position));
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
