/*
 * AimedWeaponStats
 * Stats for weapons that ride a ring around the owner and swing toward the nearest enemy
 * (guns now, melee later).
 *
 * - range is how far from the owner an enemy can be and still be targeted.
 * - ringRadius is how far from the owner's center the weapon sits.
 * - turnSpeed is degrees per second the weapon swings around the ring.
 * - fireAngleTolerance is how close (degrees) the weapon must be to the target direction before it attacks.
 * - slotClearance is how close (units) an enemy can be to the weapon's ring spot before the weapon
 *   slides to a free spot on the ring. Roughly enemy radius + weapon size + a margin.
 * - separationDistance is how much room (arc length along the ring, in units) this weapon needs from
 *   other weapons wanting the same spot. When two weapons meet, the larger of the two values is used,
 *   so give bigger guns bigger values to avoid clipping.
 * - displacedTurnSpeed is the slower degrees per second used while sliding to or from a free spot
 *   (lower = smoother, less twitchy).
 */

using UnityEngine;

public abstract class AimedWeaponStats : WeaponStats
{
    [SerializeField, Min(0.1f)] private float range = 8f;
    [SerializeField, Min(0f)] private float ringRadius = 1.5f;
    [SerializeField, Min(1f)] private float turnSpeed = 540f;
    [SerializeField, Min(0f)] private float fireAngleTolerance = 10f;
    [SerializeField, Min(0f)] private float slotClearance = 1f;
    [SerializeField, Min(1f)] private float displacedTurnSpeed = 150f;
    [SerializeField, Min(0f)] private float separationDistance = 0.8f;

    public float SeparationDistance => separationDistance;

    public float DisplacedTurnSpeed => displacedTurnSpeed;
    public override float Range => range;
    public float SlotClearance => slotClearance;
    public float RingRadius => ringRadius;
    public float TurnSpeed => turnSpeed;
    public float FireAngleTolerance => fireAngleTolerance;
}
