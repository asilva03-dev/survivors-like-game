/*
 * WoodCounterUI
 * Screen-space text showing the current wood total from ResourceInventory.
 *
 * - Lives on a Text (Minecraftia font) under a screen-space overlay canvas.
 * - Redraws only when ResourceInventory.WoodChanged fires, not every frame.
 */

using UnityEngine;
using UnityEngine.UI;

public class WoodCounterUI : MonoBehaviour
{
    [SerializeField] private ResourceInventory inventory;
    [SerializeField] private Text label;
    [SerializeField] private string format = "Wood: {0}";

    private void OnEnable()
    {
        inventory.WoodChanged += Refresh;
        Refresh(inventory.Wood);
    }

    private void OnDisable()
    {
        inventory.WoodChanged -= Refresh;
    }

    private void Refresh(int wood)
    {
        label.text = string.Format(format, wood);
    }
}
