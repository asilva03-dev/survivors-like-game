/*
 * WeaponRangeIndicator
 * Ground circle showing a weapon's range, centered on the weapon's owner.
 * Drawing and setup live in GroundRangeIndicator; this only supplies the weapon's center and range.
 */

using UnityEngine;

public class WeaponRangeIndicator : GroundRangeIndicator
{
    [SerializeField] private WeaponBase weapon;

    protected override Vector3 Center => weapon.Owner.position;
    protected override float Radius => weapon.Range;
}
