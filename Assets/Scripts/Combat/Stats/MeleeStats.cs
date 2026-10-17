/*
 * MeleeStats
 * Stats asset for the manual melee weapon (axe now; sword and pickaxe later are just more assets).
 * Create via Assets > Create > Weapons > Melee Stats.
 *
 * - Every number that shapes the attack lives here and applies live, including during Play mode.
 * - damage / resourceDamageMultiplier / targets come from WeaponStats: damage is what a swing deals to
 *   enemies, damage x resourceDamageMultiplier (rounded up) to resources (trees, ...). attackInterval
 *   (also from WeaponStats) is the normal swing's lockout.
 * - attackInterval / chargedAttackInterval: seconds after a swing is released during which Mouse 1
 *   is ignored (no new charge can start; a button held through it does nothing until pressed again).
 *   The timer starts at release, so it is the full time between one attack and the next; a swing still
 *   in progress also blocks new input. Normal and charged swings have their own interval.
 * - Arc: reach is how far from the owner the swing hits (also the weapon's Range), arcAngle the total
 *   sweep in degrees centered on the cursor direction, targetRadius extra reach so big targets (trees)
 *   count when their edge is touched.
 * - Normal attack: a quick press and release. swingDuration is the sweep time in seconds, staminaCost is
 *   paid on release. knockbackDistanceNormal is how far (units) it pushes enemies away, over the same
 *   knockbackDuration as the charged attack (0 = no knockback on normal hits).
 * - Charged attack: hold until chargeTime seconds have passed, then release. It uses its own
 *   chargedSwingDuration and chargedStaminaCost, multiplies damage by chargedDamageMultiplier and pushes
 *   enemies away knockbackDistance units (more than a normal hit) over knockbackDuration seconds. If the stamina for it is
 *   missing nothing happens (no fallback to a normal attack).
 * - Lunge: each swing also pushes the player in the swing direction: lungeDistance (normal) or
 *   chargedLungeDistance (charged) units, eased out over lungeDuration seconds, on top of normal movement
 *   (0 = no lunge).
 * - Hit stop: when a swing hits a resource (tree) the swing animation freezes for hitStopDuration
 *   (normal) or chargedHitStopDuration (charged) seconds. The attack lockout is pushed back by the
 *   same time so attack spacing is unchanged.
 * - chargeMoveSpeedMultiplier scales player move speed while charging (1 = no slowdown).
 * - swingCurve maps swing time (0..1) to arc progress (0..1) for easing. The default starts at a normal
 *   pace, speeds up through the strike, then eases out slowly as the swing finishes.
 * - extensionCurve maps swing time (0..1) to an extension amount (0..1) that goes out and comes back:
 *   the axe is pushed extendDistance units farther from the owner (hits reach that far too) and grows
 *   by growAmount (0.3 = 30% bigger) at the curve's peak, then returns to normal by the end.
 * - tiltCurve maps swing time (0..1) to a multiplier on tiltAngle (degrees): the model rotates about its
 *   own center around tiltAxis (in pivot space: X = pitch the handle up/down, Z = roll the blade,
 *   Y = yaw). Default starts cocked back (1), snaps past neutral (-0.3) at the strike and settles to
 *   0. Flip tiltAngle's sign to tilt the other way.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/Melee Stats", fileName = "NewMeleeStats")]
public class MeleeStats : WeaponStats
{
    [Header("Arc")]
    [SerializeField, Min(0.1f)] private float reach = 3f;
    [SerializeField, Range(10f, 360f)] private float arcAngle = 120f;
    [SerializeField, Min(0f)] private float targetRadius = 0.4f;

    [Header("Normal attack")]
    [SerializeField, Min(0.02f)] private float swingDuration = 0.25f;
    [SerializeField, Min(0f)] private float staminaCost = 15f;
    [SerializeField, Min(0f)] private float knockbackDistanceNormal = 0.6f;

    [Header("Charged attack")]
    [SerializeField, Min(0.05f)] private float chargeTime = 0.6f;
    [SerializeField, Min(0.02f)] private float chargedSwingDuration = 0.4f;
    [SerializeField, Min(0f)] private float chargedStaminaCost = 30f;
    [SerializeField, Min(0f)] private float chargedAttackInterval = 0.7f;
    [SerializeField, Min(0f)] private float chargedDamageMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float knockbackDistance = 2f;
    [SerializeField, Min(0.02f)] private float knockbackDuration = 0.25f;

    [Header("Lunge (push the player forward)")]
    [SerializeField, Min(0f)] private float lungeDistance = 0.4f;
    [SerializeField, Min(0f)] private float chargedLungeDistance = 1.2f;
    [SerializeField, Min(0.02f)] private float lungeDuration = 0.15f;

    [Header("Hit stop (resources)")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.06f;
    [SerializeField, Min(0f)] private float chargedHitStopDuration = 0.12f;

    [Header("Charging")]
    [SerializeField, Range(0.05f, 1f)] private float chargeMoveSpeedMultiplier = 0.6f;

    [Header("Animation")]
    [SerializeField] private AnimationCurve swingCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.8f),
        new Keyframe(0.4f, 0.55f, 2.2f, 2.2f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve extensionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.45f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f));
    [SerializeField] private float tiltAngle = 40f;
    [SerializeField] private Vector3 tiltAxis = Vector3.right;
    [SerializeField] private AnimationCurve tiltCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(0.45f, -0.3f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f));
    [SerializeField, Min(0f)] private float extendDistance = 1.2f;
    [SerializeField, Min(0f)] private float growAmount = 0.3f;

    public override float Range => reach;
    public float Reach => reach;
    public float ArcAngle => arcAngle;
    public float TargetRadius => targetRadius;
    public float SwingDuration => swingDuration;
    public float StaminaCost => staminaCost;
    public float ChargedAttackInterval => chargedAttackInterval;
    public float ChargeTime => chargeTime;
    public float ChargedSwingDuration => chargedSwingDuration;
    public float ChargedStaminaCost => chargedStaminaCost;
    public float ChargedDamageMultiplier => chargedDamageMultiplier;
    public float KnockbackDistance => knockbackDistance;
    public float KnockbackDistanceNormal => knockbackDistanceNormal;
    public float KnockbackDuration => knockbackDuration;
    public float LungeDistance => lungeDistance;
    public float ChargedLungeDistance => chargedLungeDistance;
    public float LungeDuration => lungeDuration;
    public float HitStopDuration => hitStopDuration;
    public float ChargedHitStopDuration => chargedHitStopDuration;
    public float ChargeMoveSpeedMultiplier => chargeMoveSpeedMultiplier;
    public AnimationCurve SwingCurve => swingCurve;
    public AnimationCurve ExtensionCurve => extensionCurve;
    public float TiltAngle => tiltAngle;
    public Vector3 TiltAxis => tiltAxis;
    public AnimationCurve TiltCurve => tiltCurve;
    public float ExtendDistance => extendDistance;
    public float GrowAmount => growAmount;
}
