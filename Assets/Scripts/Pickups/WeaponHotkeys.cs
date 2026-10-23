/*
 * WeaponHotkeys
 * Gives the player weapons with the number keys instead of world pickups: key 1 adds weapons[0], key 2 weapons[1], ...
 * (up to 9). Scene object `WeaponHotkeys`.
 *
 * - Each press adds one more weapon through WeaponLoadout.TryAddWeapon, so the gun cap (maxGuns) still applies; a
 *   refused press logs why. Other weapon types stack, as picking up several of them did.
 * - The player is found by the "Player" tag on Start.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponHotkeys : MonoBehaviour
{
    [Tooltip("Key 1 gives the first weapon, key 2 the second, and so on (up to 9).")]
    [SerializeField] private List<GameObject> weapons = new List<GameObject>();

    private WeaponLoadout loadout;

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            loadout = player.GetComponentInChildren<WeaponLoadout>();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || loadout == null)
        {
            return;
        }
        for (int i = 0; i < weapons.Count && i < 9; i++)
        {
            if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
            {
                Give(i);
            }
        }
    }

    /// <summary>Adds weapons[index] to the player; false when the loadout refuses it (gun cap).</summary>
    public bool Give(int index)
    {
        if (loadout == null || index < 0 || index >= weapons.Count || weapons[index] == null)
        {
            return false;
        }
        if (loadout.TryAddWeapon(weapons[index]))
        {
            return true;
        }
        Debug.Log("WeaponHotkeys: " + weapons[index].name + " refused (gun limit reached).", this);
        return false;
    }
}
