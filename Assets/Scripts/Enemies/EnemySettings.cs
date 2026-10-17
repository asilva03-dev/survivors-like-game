/*
 * EnemySettings
 * Shared tuning asset for all enemies. Every enemy uses the same asset, so changing a value here
 * (including in Play mode) changes every enemy at once.
 *
 * - moveSpeed is units per second toward the player.
 * - stopDistance is how close (center to center) enemies get to the player before they stop and are
 *   held out; roughly player radius + enemy radius.
 * - separationRadius is how far apart enemies start steering away from each other. Larger values
 *   spread crowds out earlier and more smoothly.
 * - separationStrength is the speed (units per second) of that sideways push at full overlap.
 * - minSpacing is the hard minimum center-to-center distance between enemies; roughly an enemy's
 *   diameter. Enemies closer than this are pushed apart directly, so they never visibly overlap.
 * - turnSpeed is degrees per second an enemy's model turns to face the player.
 * - Create more assets via Assets > Create > Enemies > Enemy Settings if enemy groups ever need
 *   different shared values.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Enemies/Enemy Settings", fileName = "EnemySettings")]
public class EnemySettings : ScriptableObject
{
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float stopDistance = 1f;
    [SerializeField, Min(0.1f)] private float separationRadius = 1.6f;
    [SerializeField, Min(0f)] private float separationStrength = 3f;
    [SerializeField, Min(0.1f)] private float minSpacing = 1f;
    [SerializeField, Min(1f)] private float turnSpeed = 540f;

    public float MoveSpeed => moveSpeed;
    public float StopDistance => stopDistance;
    public float SeparationRadius => separationRadius;
    public float SeparationStrength => separationStrength;
    public float MinSpacing => minSpacing;
    public float TurnSpeed => turnSpeed;
}
