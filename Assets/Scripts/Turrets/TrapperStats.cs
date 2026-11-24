/*
 * TrapperStats
 * Numbers for the Trapper turret (2026-11-23; user: the cat throws a trap onto the nearest path every so often, traps last
 * a while, a rat stepping on one takes 10 damage). Create > TD > Trapper Stats; asset Assets/Turrets/TrapperStats.asset.
 * All values are Claude's placeholders for the first test (user: "default values that you think work for now").
 *
 * - range: how far from the Trapper a trap may land (path cells within it); also the range ring.
 * - throwInterval: seconds per throw = one whole throw animation cycle (TrapperAnimator plays faster or slower with it).
 *   No trap cap (2026-11-23, user): it always throws.
 * - trapSpacing: traps (any Trapper's) closer than this to a spot count as lying on it.
 * - stackValue (2026-11-23, user: prefer free spots but still let traps stack): a spot's worth is its route traffic x
 *   stackValue per trap already on it (0.5 = each trap halves it; 0 = never stack while a free spot is in range, 1 = ignore
 *   traps there).
 * - pathCentreMaxWidth (2026-11-23, user: traps only on the dirt path, in its middle): a spot is moved to the middle of
 *   its dirt corridor when the corridor is at most this many cells wide (wider open areas keep the cell centre).
 * - flightTime / arcHeight: the throw's flight (a parabola from the cat's paw to the path).
 * - trapLifetime: seconds a set trap waits; then it snaps shut on nothing and is gone.
 * - damage: dealt once to the rat that steps on it (one rat per trap). triggerRadius: how close a rat's centre must come.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Trapper Stats", fileName = "TrapperStats")]
public class TrapperStats : ScriptableObject
{
    [SerializeField, Min(0.5f)] private float range = 6f;
    [SerializeField, Min(0.1f)] private float throwInterval = 2.5f;
    [SerializeField, Min(0f)] private float trapSpacing = 0.9f;
    [SerializeField, Range(0f, 1f)] private float stackValue = 0.5f;
    [SerializeField, Min(1)] private int pathCentreMaxWidth = 6;
    [SerializeField, Min(0.05f)] private float flightTime = 0.55f;
    [SerializeField, Min(0f)] private float arcHeight = 1.4f;
    [SerializeField, Min(0.5f)] private float trapLifetime = 15f;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0.05f)] private float triggerRadius = 0.45f;

    public float Range => range;
    public float ThrowInterval => throwInterval;
    public float TrapSpacing => trapSpacing;
    public float StackValue => stackValue;
    public int PathCentreMaxWidth => pathCentreMaxWidth;
    public float FlightTime => flightTime;
    public float ArcHeight => arcHeight;
    public float TrapLifetime => trapLifetime;
    public float Damage => damage;
    public float TriggerRadius => triggerRadius;
}
