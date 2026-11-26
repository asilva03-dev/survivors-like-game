/*
 * SwordSwingStats
 * Numbers for a tower's close-range sword swing (TurretSwing; the SwordsCat, tower 5, 2026-11-25). Damage, swing
 * interval (attackInterval), targets and lead come from WeaponStats.
 *
 * - reach: flat distance from the tower's aim point within which a rat triggers a swing and can be hit (Range).
 * - arcAngle: the swing's arc, centred on the nearest rat; every rat inside it within reach is hit (no pierce cap).
 * - targetRadius: rats count as discs of this radius (plus their own Damageable.TargetRadius), so edges get caught.
 * - detectsShadow: like the guns' flag (ShadowSight).
 * - modeLinger (user 2026-11-25): melee mode lasts this long after the last rat leaves the reach (see TurretSwing).
 * - pathReach / pathConeAngle (user 2026-11-25, "long wedge toward the path"): inside a cone of pathConeAngle degrees
 *   centred on the tower's nearest path (Turret.RestDirection) the reach is pathReach instead of reach. pathReach 0 (or
 *   not above reach) = no wedge, the same reach all round.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/Sword Swing Stats", fileName = "SwordSwingStats")]
public class SwordSwingStats : WeaponStats
{
    [SerializeField, Min(0.1f)] private float reach = 2.2f;
    [SerializeField, Range(10f, 360f)] private float arcAngle = 200f;
    [SerializeField, Min(0f)] private float targetRadius = 0.4f;
    [Tooltip("Can hit shadow rats (else only those in a RevealLight).")]
    [SerializeField] private bool detectsShadow;
    [Tooltip("Seconds the tower stays in melee mode (no ranged shots) after the last rat leaves its reach.")]
    [SerializeField, Min(0f)] private float modeLinger = 1f;
    [Tooltip("Reach inside the cone facing the tower's nearest path (0 = no wedge).")]
    [SerializeField, Min(0f)] private float pathReach;
    [Tooltip("Width of that cone, degrees.")]
    [SerializeField, Range(0f, 360f)] private float pathConeAngle = 90f;

    public override float Range => reach;
    public bool DetectsShadow => detectsShadow;
    public float ModeLinger => modeLinger;
    public float Reach => reach;
    public float ArcAngle => arcAngle;
    public float TargetRadius => targetRadius;
    public bool HasPathWedge => pathReach > reach;
    public float PathReach => pathReach;
    public float PathConeAngle => pathConeAngle;
}
