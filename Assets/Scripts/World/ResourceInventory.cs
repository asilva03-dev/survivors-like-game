/*
 * ResourceInventory
 * Run-wide resource totals. Only wood for now (stone and berries come with their own slices).
 *
 * - Wood is added when a WoodDrop is collected (WoodDrop.Collected), not when the tree dies.
 *   WoodDropSpawner creates the drops from ResourceTree.WoodYield.
 * - WoodChanged fires with the new total for UI (see WoodCounterUI).
 * - TrySpend(amount) is for the base store later; it only spends what is available.
 */

using System;
using UnityEngine;

public class ResourceInventory : MonoBehaviour
{
    public event Action<int> WoodChanged;

    private int wood;

    public int Wood => wood;

    private void OnEnable() => WoodDrop.Collected += HandleCollected;

    private void OnDisable() => WoodDrop.Collected -= HandleCollected;

    public bool TrySpend(int amount)
    {
        if (amount > wood)
        {
            return false;
        }

        wood -= amount;
        WoodChanged?.Invoke(wood);
        return true;
    }

    private void HandleCollected(int amount)
    {
        wood += amount;
        WoodChanged?.Invoke(wood);
    }
}
