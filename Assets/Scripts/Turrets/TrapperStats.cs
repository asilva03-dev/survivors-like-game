/*
 * TrapperStats
 * Numbers for the Trapper turret (2026-11-23; user: the cat throws a trap onto the nearest path every so often, traps last
 * a while, a rat stepping on one takes 10 damage). Create > TD > Trapper Stats; asset Assets/Turrets/TrapperStats.asset.
 * All values are Claude's placeholders for the first test (user: "default values that you think work for now").
 *
 * - range: how far from the Trapper a trap may land (path cells within it); also the range ring.
 * - throwInterval: seconds between throws. maxTraps: most of this Trapper's traps set at once (it waits while full).
 * - trapSpacing: a new trap never lands closer than this to another set trap (any Trapper's).
 * - flightTime / arcHeight: the throw's flight (a parabola from the cat's paw to the path).
 * - trapLifetime: seconds a set trap waits; then it snaps shut on nothing and is gone.
 * - damage: dealt once to the rat that steps on it (one rat per trap). triggerRadius: how close a rat's centre must come.
 * - turnSpeed: how fast the Trapper turns toward where it throws (degrees per second).
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Trapper Stats", fileName = "TrapperStats")]
public class TrapperStats : ScriptableObject
{
    [SerializeField, Min(0.5f)] private float range = 6f;
    [SerializeField, Min(0.1f)] private float throwInterval = 2.5f;
    [SerializeField, Min(1)] private int maxTraps = 5;
    [SerializeField, Min(0f)] private float trapSpacing = 0.9f;
    [SerializeField, Min(0.05f)] private float flightTime = 0.55f;
    [SerializeField, Min(0f)] private float arcHeight = 1.4f;
    [SerializeField, Min(0.5f)] private float trapLifetime = 15f;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0.05f)] private float triggerRadius = 0.45f;
    [SerializeField, Min(1f)] private float turnSpeed = 540f;

    public float Range => range;
    public float ThrowInterval => throwInterval;
    public int MaxTraps => maxTraps;
    public float TrapSpacing => trapSpacing;
    public float FlightTime => flightTime;
    public float ArcHeight => arcHeight;
    public float TrapLifetime => trapLifetime;
    public float Damage => damage;
    public float TriggerRadius => triggerRadius;
    public float TurnSpeed => turnSpeed;
}
