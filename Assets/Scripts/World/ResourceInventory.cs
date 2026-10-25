/*
 * ResourceInventory
 * Run-wide resource totals. Only wood for now (stone and berries come with their own slices).
 *
 * - Wood is added when a WoodDrop is collected (WoodDrop.Collected), not when the tree dies.
 *   WoodDropSpawner creates the drops from ResourceTree.WoodYield.
 * - WoodChanged fires with the new total for UI (see WoodCounterUI).
 * - TrySpend(amount) spends wood for building (only what is available); AddWood(amount) adds directly (debug key,
 *   sell refunds).
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

    // Direct add (debug wood key, later refunds from selling).
    public void AddWood(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        wood += amount;
        WoodChanged?.Invoke(wood);
    }

    private void HandleCollected(int amount)
    {
        wood += amount;
        WoodChanged?.Invoke(wood);
    }
}
