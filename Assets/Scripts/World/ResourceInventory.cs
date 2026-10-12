/*
 * ResourceInventory
 * Run-wide resource totals. Only wood for now (stone and berries come with their own slices).
 *
 * - Listens to Damageable.Died and adds ResourceTree.WoodYield when a tree dies, so it works no
 *   matter which weapon landed the killing blow.
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

    private void OnEnable() => Damageable.Died += HandleDied;

    private void OnDisable() => Damageable.Died -= HandleDied;

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

    private void HandleDied(Damageable target)
    {
        if (target is ResourceTree tree && tree.WoodYield > 0)
        {
            wood += tree.WoodYield;
            WoodChanged?.Invoke(wood);
        }
    }
}
