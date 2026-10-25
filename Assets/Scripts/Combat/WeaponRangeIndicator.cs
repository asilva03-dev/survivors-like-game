/*
 * WeaponRangeIndicator
 * Ground circle showing a weapon's range, centered on the weapon's owner.
 * Drawing and setup live in GroundRangeIndicator; this only supplies the weapon's center and range.
 *
 * - With no weapon assigned (the player starts without a gun) it follows the first GunWeapon under the
 *   WeaponLoadout once one is added (number-key hotkeys), and stays hidden until then.
 */

using UnityEngine;

public class WeaponRangeIndicator : GroundRangeIndicator
{
    [SerializeField] private WeaponBase weapon;

    private WeaponLoadout loadout;

    protected override bool Visible
    {
        get
        {
            if (weapon == null)
            {
                if (loadout == null)
                {
                    loadout = FindAnyObjectByType<WeaponLoadout>();
                }

                weapon = loadout != null ? loadout.GetComponentInChildren<GunWeapon>() : null;
            }

            return weapon != null && weapon.isActiveAndEnabled;
        }
    }

    protected override Vector3 Center => weapon.Owner.position;
    protected override float Radius => weapon.Range;
}
