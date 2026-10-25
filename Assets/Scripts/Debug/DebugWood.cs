/*
 * DebugWood
 * Testing shortcut: press the key (G by default) to add `amount` wood (50) to the ResourceInventory, so build mode
 * can be tested without chopping trees. Same build switch as DebugKillAll (enableInBuilds).
 */

using UnityEngine;
using UnityEngine.InputSystem;

public class DebugWood : MonoBehaviour
{
    [SerializeField] private Key key = Key.G;
    [SerializeField, Min(1)] private int amount = 50;
    [SerializeField] private bool enableInBuilds = true;
    [SerializeField] private ResourceInventory inventory;

    private void Update()
    {
        if (!Application.isEditor && !enableInBuilds)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[key].wasPressedThisFrame)
        {
            return;
        }

        if (inventory == null)
        {
            inventory = FindAnyObjectByType<ResourceInventory>();
        }
        if (inventory != null)
        {
            inventory.AddWood(amount);
        }
    }
}
