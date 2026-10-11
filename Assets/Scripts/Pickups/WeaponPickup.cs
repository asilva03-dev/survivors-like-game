/*
 * WeaponPickup
 * A rotating, hovering object that gives the player a weapon when they walk over it.
 *
 * - weaponPrefab is the weapon object to add (e.g. the Gun prefab). Any weapon built on WeaponBase
 *   works, so this is also how new weapon types will be handed out later.
 * - The weapon is added through WeaponLoadout.TryAddWeapon, which respects the loadout's weapon cap.
 *   If the loadout is full the pickup stays where it is and can be collected later.
 * - Pickup uses flat (XZ) distance to the player, so no collider is needed.
 * - The pickup is disabled, not destroyed, once collected.
 * - The player is found by the "Player" tag on Start.
 */

using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private GameObject weaponPrefab;
    [SerializeField, Min(0.1f)] private float pickupRadius = 1.5f;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField, Min(0f)] private float bobHeight = 0.2f;
    [SerializeField, Min(0f)] private float bobSpeed = 2f;

    private Transform player;
    private WeaponLoadout loadout;
    private Vector3 basePosition;

    private void Start()
    {
        basePosition = transform.position;

        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            loadout = found.GetComponentInChildren<WeaponLoadout>();
        }
    }

    private void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

        if (player == null || loadout == null)
        {
            return;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;

        if (offset.sqrMagnitude <= pickupRadius * pickupRadius && loadout.TryAddWeapon(weaponPrefab))
        {
            gameObject.SetActive(false);
        }
    }
}
