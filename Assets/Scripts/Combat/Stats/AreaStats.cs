/*
 * AreaStats
 * Stats asset for an AOE weapon: a zone around the owner that damages enemies on an interval.
 * Create via Assets > Create > Weapons > Area Stats.
 *
 * - radius is the zone size in world units around the owner (also the weapon's Range).
 * - maxTargets: -1 hits every enemy in the zone; 1 or more hits only that many, nearest first.
 * - color is the floor fill of the zone, alpha included (0.6 = 60% opacity).
 * - flashDuration is how long (seconds) the zone brightens after each attack so ticks are readable;
 *   0 disables it.
 * - attackInterval and damage come from WeaponStats; each attack deals damage once per target.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/Area Stats", fileName = "NewAreaStats")]
public class AreaStats : WeaponStats
{
    [SerializeField, Min(0.1f)] private float radius = 5.5f;
    [SerializeField, Min(-1)] private int maxTargets = -1;
    [SerializeField] private Color color = new Color(1f, 0f, 0f, 0.6f);
    [SerializeField, Min(0f)] private float flashDuration = 0.12f;

    public override float Range => radius;
    public float Radius => radius;
    public int MaxTargets => maxTargets;
    public Color Color => color;
    public float FlashDuration => flashDuration;
}
