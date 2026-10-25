/*
 * BuildBarUI
 * The defense bar of build mode (user's sketch, slice 6 step 3), drawn on BuildModeUI's canvas.
 *
 * - A rounded dark bar (barSize) at the bottom centre with slotCount slots, and category tabs on its top-left (one per
 *   BuildMode category; "Turrets" holds the gun turret, the others are empty for now). Clicking a tab shows that
 *   category's defenses in the slots; unused slots are faint empty boxes.
 * - A filled slot shows the defense's picture (DefensePreview.Cached, rendered once per prefab), its name and wood
 *   cost. When the player can't afford it the slot greys out (cost in red) and clicking does nothing; it updates when
 *   the wood total changes.
 * - Clicking a slot selects that defense (BuildMode.Select). The bar is up while build mode is open with nothing
 *   selected and slides down out of view when a defense is selected or build mode closes (eased, slideSharpness);
 *   right click (BuildMode) brings it back.
 * - Test hooks: ClickSlot(i), SelectTab(i), SlotDefense(i), SlotInteractable(i), ShownAmount (0 hidden .. 1 up).
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[RequireComponent(typeof(BuildModeUI))]
public class BuildBarUI : MonoBehaviour
{
    private class Slot
    {
        public GameObject root;
        public Button button;
        public CanvasGroup group;
        public RawImage picture;
        public Text name;
        public Text cost;
        public TurretDefinition defense;
    }

    [SerializeField] private Vector2 barSize = new Vector2(1320f, 170f);
    [SerializeField] private float bottomMargin = 18f;
    [SerializeField, Min(1)] private int slotCount = 7;
    [SerializeField] private Vector2 slotSize = new Vector2(168f, 140f);
    [SerializeField] private Vector2 tabSize = new Vector2(170f, 42f);
    [SerializeField, Min(1f)] private float slideSharpness = 14f;
    [SerializeField, Range(64, 512)] private int pictureSize = 256;

    [Header("Colours")]
    [SerializeField] private Color barColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color slotColor = new Color(0.16f, 0.17f, 0.2f, 0.95f);
    [SerializeField] private Color emptySlotColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] private Color tabColor = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Color activeTabColor = new Color(0.22f, 0.24f, 0.3f, 0.95f);
    [SerializeField] private Color costColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private Color unaffordableCostColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField, Range(0f, 1f)] private float unaffordableAlpha = 0.45f;

    private readonly List<Slot> slots = new List<Slot>();
    private readonly List<Image> tabs = new List<Image>();
    private BuildModeUI ui;
    private BuildMode buildMode;
    private ResourceInventory inventory;
    private RectTransform bar;
    private float shown;
    private int category;

    public float ShownAmount => shown;
    public int CurrentCategory => category;

    private void Start()
    {
        ui = GetComponent<BuildModeUI>();
        buildMode = GetComponent<BuildMode>();
        if (buildMode == null)
        {
            buildMode = BuildMode.Instance;
        }

        inventory = buildMode != null ? buildMode.Inventory : null;
        if (inventory != null)
        {
            inventory.WoodChanged += OnWoodChanged;
        }

        EnsureEventSystem();
        Build();
        SelectTab(0);
        Place(0f);
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.WoodChanged -= OnWoodChanged;
        }
    }

    private void Update()
    {
        bool wanted = buildMode != null && buildMode.IsOpen && buildMode.Selected == null;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float target = wanted ? 1f : 0f;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-slideSharpness * dt));
        if (Mathf.Abs(shown - target) < 0.001f)
        {
            shown = target;
        }
        Place(shown);
    }

    public void SelectTab(int index)
    {
        if (buildMode == null || buildMode.Categories.Count == 0)
        {
            return;
        }

        category = Mathf.Clamp(index, 0, buildMode.Categories.Count - 1);
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i].color = i == category ? activeTabColor : tabColor;
        }

        List<TurretDefinition> defenses = buildMode.Categories[category].defenses;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            slot.defense = i < defenses.Count ? defenses[i] : null;
            bool filled = slot.defense != null;
            slot.picture.gameObject.SetActive(filled);
            slot.name.gameObject.SetActive(filled);
            slot.cost.gameObject.SetActive(filled);
            slot.root.GetComponent<Image>().color = filled ? slotColor : emptySlotColor;
            if (filled)
            {
                slot.picture.texture = Picture(slot.defense);
                slot.name.text = slot.defense.DisplayName;
                slot.cost.text = slot.defense.WoodCost + " wood";
            }
        }
        RefreshAffordability();
    }

    public void ClickSlot(int index)
    {
        if (index < 0 || index >= slots.Count || slots[index].defense == null || buildMode == null)
        {
            return;
        }
        buildMode.Select(slots[index].defense);
    }

    public TurretDefinition SlotDefense(int index)
    {
        return index >= 0 && index < slots.Count ? slots[index].defense : null;
    }

    public bool SlotInteractable(int index)
    {
        return index >= 0 && index < slots.Count && slots[index].button.interactable;
    }

    private void OnWoodChanged(int wood)
    {
        RefreshAffordability();
    }

    private void RefreshAffordability()
    {
        foreach (Slot slot in slots)
        {
            bool filled = slot.defense != null;
            bool affordable = filled && buildMode.CanAfford(slot.defense);
            slot.button.interactable = affordable;
            slot.group.alpha = !filled || affordable ? 1f : unaffordableAlpha;
            slot.cost.color = affordable ? costColor : unaffordableCostColor;
        }
    }

    private Texture2D Picture(TurretDefinition defense)
    {
        return defense.Prefab != null ? DefensePreview.Cached(defense.Prefab.gameObject, pictureSize) : null;
    }

    // 0 = hidden below the screen, 1 = up at bottomMargin.
    private void Place(float amount)
    {
        float hiddenY = -(barSize.y + tabSize.y + 20f);
        bar.anchoredPosition = new Vector2(0f, Mathf.Lerp(hiddenY, bottomMargin, amount));
        bar.gameObject.SetActive(amount > 0.001f);
    }

    private void Build()
    {
        GameObject barObject = new GameObject("DefenseBar", typeof(RectTransform), typeof(Image));
        bar = (RectTransform)barObject.transform;
        bar.SetParent(ui.Root, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = barSize;
        Image barImage = barObject.GetComponent<Image>();
        barImage.sprite = BuildModeUI.RoundedBox(18);
        barImage.type = Image.Type.Sliced;
        barImage.color = barColor;

        // Tabs on the bar's top-left edge.
        if (buildMode != null)
        {
            for (int i = 0; i < buildMode.Categories.Count; i++)
            {
                int index = i;
                RectTransform tab = CreateBox(bar, "Tab" + i, tabSize, tabColor, 12);
                tab.anchorMin = tab.anchorMax = new Vector2(0f, 1f);
                tab.pivot = new Vector2(0f, 0f);
                tab.anchoredPosition = new Vector2(18f + i * (tabSize.x + 8f), 4f);
                tab.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectTab(index));
                tabs.Add(tab.GetComponent<Image>());
                Text label = CreateText(tab, "Label", 22, buildMode.Categories[i].name);
                Stretch(label.rectTransform);
            }
        }

        // Slots, centred in the bar.
        float gap = (barSize.x - slotCount * slotSize.x) / (slotCount + 1);
        for (int i = 0; i < slotCount; i++)
        {
            int index = i;
            RectTransform rect = CreateBox(bar, "Slot" + i, slotSize, emptySlotColor, 14);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(gap + i * (slotSize.x + gap), 0f);

            Slot slot = new Slot { root = rect.gameObject };
            slot.group = rect.gameObject.AddComponent<CanvasGroup>();
            slot.button = rect.gameObject.AddComponent<Button>();
            slot.button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = slot.button.colors;
            colors.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.disabledColor = Color.white;
            slot.button.colors = colors;
            slot.button.onClick.AddListener(() => ClickSlot(index));

            GameObject pictureObject = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            RectTransform pictureRect = (RectTransform)pictureObject.transform;
            pictureRect.SetParent(rect, false);
            pictureRect.anchoredPosition = new Vector2(0f, 6f);
            pictureRect.sizeDelta = new Vector2(slotSize.y - 40f, slotSize.y - 40f);
            slot.picture = pictureObject.GetComponent<RawImage>();
            slot.picture.raycastTarget = false;

            slot.name = CreateText(rect, "Name", 16, "");
            slot.name.rectTransform.anchorMin = slot.name.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            slot.name.rectTransform.anchoredPosition = new Vector2(0f, -13f);
            slot.name.rectTransform.sizeDelta = new Vector2(slotSize.x - 10f, 22f);

            slot.cost = CreateText(rect, "Cost", 18, "");
            slot.cost.rectTransform.anchorMin = slot.cost.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            slot.cost.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            slot.cost.rectTransform.sizeDelta = new Vector2(slotSize.x - 10f, 24f);
            slots.Add(slot);
        }
    }

    private RectTransform CreateBox(RectTransform parent, string name, Vector2 size, Color color, int radius)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = BuildModeUI.RoundedBox(radius);
        image.type = Image.Type.Sliced;
        image.color = color;
        return rect;
    }

    private Text CreateText(RectTransform parent, string name, int size, string value)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = ui.Font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;
        go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }
    }
}
