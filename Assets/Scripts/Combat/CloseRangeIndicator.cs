/*
 * CloseRangeIndicator
 * Ground circle showing the character's too-close range (WeaponLoadout.CloseRange), centered on
 * the loadout's owner. Drawing and setup live in GroundRangeIndicator.
 */

using UnityEngine;

public class CloseRangeIndicator : GroundRangeIndicator
{
    [SerializeField] private WeaponLoadout loadout;

    protected override Vector3 Center => loadout.transform.position;
    protected override float Radius => loadout.CloseRange;
}
