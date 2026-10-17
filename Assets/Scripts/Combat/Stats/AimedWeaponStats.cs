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
 * - focusWeakestResource: when on, the weapon picks the resource with the lowest health percentage in
 *   reach, may share one another weapon already took, and moves to a clearly weaker one while on
 *   a resource. Off for guns (they spread over targets), on for the throwable.
 * - avoidCloseRange: when on (the throwables), the weapon ignores the loadout's close-range priority and
 *   the shared close target. It prefers enemies outside closeRange and only targets ones inside it when
 *   nothing else is in reach (the blast would otherwise land on the player).
 * - preferFarTargets: among targets of the same tier the weapon picks the farthest one instead of the lowest
 *   HP / closest (the throwables). Targets stay sticky, so it does not flip as the player moves.
 * - stickyTargets: on (default), the weapon keeps its current target until a higher tier one appears. Off (the
 *   throwables), it re-picks the best target every frame, so it always follows the current ranking.
 * - useRing: on (default, guns), the weapon takes part in ring spacing and side pairing with other weapons. Off
 *   (the throwables, which have no visible model on the ring), it is ignored by both, so it never pushes a
 *   gun's ring spot. It still picks targets normally.
 * - preferClusters: the weapon ranks enemies by how many other enemies are within clusterRadius of them (most
 *   first) within the same boss/elite rank (rank still comes first), above distance. Ties (e.g. all enemies are alone) fall back to the normal
 *   order. Resources are unaffected. Set clusterRadius near the blast radius.
 * - claimsTargets: on (default, guns), the weapon takes part in target claiming: it avoids enemies other claiming
 *   weapons already took and others avoid its enemy. Off (the throwables), it ignores claims entirely and
 *   neither makes nor respects them, so it always goes for its best target.
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
    [SerializeField] private bool focusWeakestResource;
    [SerializeField] private bool avoidCloseRange;
    [SerializeField] private bool preferFarTargets;
    [SerializeField] private bool stickyTargets = true;
    [SerializeField] private bool useRing = true;
    [SerializeField] private bool claimsTargets = true;
    [SerializeField] private bool preferClusters;
    [SerializeField, Min(0.1f)] private float clusterRadius = 2.5f;

    public bool AvoidCloseRange => avoidCloseRange;
    public bool PreferFarTargets => preferFarTargets;
    public bool StickyTargets => stickyTargets;
    public bool UseRing => useRing;
    public bool ClaimsTargets => claimsTargets;
    public bool PreferClusters => preferClusters;
    public float ClusterRadius => clusterRadius;
    public float SeparationDistance => separationDistance;
    public bool FocusWeakestResource => focusWeakestResource;

    public float DisplacedTurnSpeed => displacedTurnSpeed;
    public override float Range => range;
    public float SlotClearance => slotClearance;
    public float RingRadius => ringRadius;
    public float TurnSpeed => turnSpeed;
    public float FireAngleTolerance => fireAngleTolerance;
}
