/*
 * FishercatNetStats
 * Gameplay numbers of the Fishercat's net throw (Path 2 "nets", 2026-11-26; Create > TD > Fishercat Net Stats).
 * FishercatNetThrower uses them; Turret gives the tower its own copy with the bought tiers applied (TurretUpgrade net
 * fields), so the asset is never changed in play.
 *
 * - unlocked: off on the asset (the base tower throws no nets); tier 1 turns it on.
 * - cooldown: seconds between throws (an extra attack beside the spears; no cat animation, user).
 * - radius: the landed net's radius; every visible rat (ShadowSight) within it is caught: stunned for stunTime and hit
 *   for spikeDamage (tier 3). Fat rats (EnemyStats.StunImmune, the bag rat) are skipped unless fatStunTime > 0 (tier 4).
 * - persistTime > 0 (tier 5): the net stays down that long on a whirlpool that pulls rats within radius x pullReach
 *   toward its middle at pullSpeed; each rat that comes under it is caught once.
 * - range: 0 = the gun's range. flightTime: seconds in the air. netCount (tier 3: 2): nets per throw, each at its own
 *   spot, never overlapping.
 * - Without persistTime the net is gone the moment it lands (user, 2026-11-26); the stun stays on the rats.
 * - Numbers are placeholders (user's finalizing pass).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Fishercat Net Stats", fileName = "FishercatNetStats")]
public class FishercatNetStats : ScriptableObject
{
    [SerializeField] private bool unlocked;
    [SerializeField, Min(0.1f)] private float cooldown = 5f;
    [SerializeField, Min(0.1f)] private float radius = 0.6f;
    [SerializeField, Min(0f)] private float stunTime = 1.2f;
    [SerializeField, Min(0f)] private float spikeDamage;
    [Tooltip("0 = fat (stun-immune) rats can't be caught.")]
    [SerializeField, Min(0f)] private float fatStunTime;
    [Tooltip("0 = the net doesn't stay; > 0 = it stays this long on a whirlpool that pulls rats in.")]
    [SerializeField, Min(0f)] private float persistTime;
    [Tooltip("The whirlpool reaches this many times the net's radius.")]
    [SerializeField, Min(1f)] private float pullReach = 1.8f;
    [SerializeField, Min(0f)] private float pullSpeed = 3f;
    [Tooltip("0 = the gun's range.")]
    [SerializeField, Min(0f)] private float range;
    [SerializeField, Min(0.05f)] private float flightTime = 0.5f;
    [Tooltip("Nets per throw, each aimed at a different spot (they don't overlap).")]
    [SerializeField, Min(1)] private int netCount = 1;

    public bool Unlocked => unlocked;
    public float Cooldown => cooldown;
    public float Radius => radius;
    public float StunTime => stunTime;
    public float SpikeDamage => spikeDamage;
    public bool CatchesFat => fatStunTime > 0f;
    public float FatStunTime => fatStunTime;
    public float PersistTime => persistTime;
    public float PullReach => pullReach;
    public float PullSpeed => pullSpeed;
    public float Range => range;
    public float FlightTime => flightTime;
    public int NetCount => netCount;

    // Upgrade copies only (TurretUpgrade.ApplyTo): values only go up / on.
    public void Unlock()
    {
        unlocked = true;
    }

    public void RaiseNetCount(int count)
    {
        netCount = Mathf.Max(netCount, count);
    }

    public void Raise(float newRadius, float newStun, float newSpikeDamage, float newFatStun, float newPersist)
    {
        radius = Mathf.Max(radius, newRadius);
        stunTime = Mathf.Max(stunTime, newStun);
        spikeDamage = Mathf.Max(spikeDamage, newSpikeDamage);
        fatStunTime = Mathf.Max(fatStunTime, newFatStun);
        persistTime = Mathf.Max(persistTime, newPersist);
    }
}
