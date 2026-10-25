/*
 * UpgradePanelUI
 * The Bloons-style upgrade panel of build mode (user's sketch, slice 6 step 6), drawn on BuildModeUI's canvas.
 *
 * - Size: the base layout (panelSize 440x640) times scale (1.25 since the user asked for a bigger panel), fonts
 *   included; raised by verticalOffset so it clears the defense bar.
 * - Slides in from the right edge (eased, slideSharpness) while BuildMode.SelectedTurret is set, and back out when
 *   it is cleared (right click, picking a defense from the bar, closing build mode).
 * - Shows the turret's name, its picture (DefensePreview.Cached) and one row per upgrade path: an upgrade button
 *   (">", with tier pips under it: filled = bought) and a description box (path name, next upgrade's name and wood
 *   cost, its description). A maxed path reads "Maxed", a path blocked by the 3/1 rule reads "Path locked"; both
 *   grey out their button. An unaffordable upgrade dims its button and shows the cost in red.
 * - Sell button at the bottom (step 7): "Sell (+N wood)" with the turret's SellValue (50% of everything spent on it);
 *   clicking it refunds the wood, removes the turret and closes the panel (BuildMode.SellSelected).
 * - Clicking a button buys the next tier (Turret.TryUpgrade); a refusal shows the reason as a red shaking message.
 *   The panel refreshes on upgrades and wood changes.
 * - Test hooks: ClickUpgrade(path), ClickSell(), RowText(path), Title, SellText, ShownAmount.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BuildModeUI))]
public class UpgradePanelUI : MonoBehaviour
{
    private class Row
    {
        public Image button;
        public Text arrow;
        public Image[] pips;
        public Text pathName;
        public Text upgradeName;
        public Text cost;
        public Text description;
    }

    [Tooltip("Base layout size; everything (boxes, rows, text) is multiplied by scale.")]
    [SerializeField] private Vector2 panelSize = new Vector2(440f, 640f);
    [SerializeField, Range(0.5f, 2f)] private float scale = 1.25f;
    [Tooltip("Moves the panel up from the screen's vertical middle so it clears the defense bar.")]
    [SerializeField] private float verticalOffset = 110f;
    [SerializeField] private float edgeMargin = 24f;
    [SerializeField, Min(1f)] private float slideSharpness = 14f;
    [SerializeField, Range(64, 512)] private int pictureSize = 256;

    [Header("Colours")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color boxColor = new Color(0.16f, 0.17f, 0.2f, 0.95f);
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.6f, 0.3f, 1f);
    [SerializeField] private Color unaffordableButtonColor = new Color(0.25f, 0.35f, 0.27f, 1f);
    [SerializeField] private Color lockedButtonColor = new Color(0.3f, 0.3f, 0.32f, 1f);
    [SerializeField] private Color pipOnColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private Color pipOffColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private Color costColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private Color unaffordableCostColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] private Color labelColor = new Color(0.7f, 0.72f, 0.78f);
    [SerializeField] private Color sellColor = new Color(0.62f, 0.22f, 0.2f, 1f);

    private const int MaxPips = 3;

    private readonly List<Row> rows = new List<Row>();
    private BuildModeUI ui;
    private BuildMode buildMode;
    private ResourceInventory inventory;
    private RectTransform panel;
    private Text title;
    private RawImage picture;
    private Text sellText;
    private Turret shownTurret;
    private float shown;

    public float ShownAmount => shown;
    public string Title => title != null ? title.text : null;
    public string SellText => sellText != null ? sellText.text : null;

    private void Start()
    {
        ui = GetComponent<BuildModeUI>();
        buildMode = GetComponent<BuildMode>();
        inventory = buildMode != null ? buildMode.Inventory : null;
        if (buildMode != null)
        {
            buildMode.TurretSelectionChanged += OnTurretSelectionChanged;
        }
        if (inventory != null)
        {
            inventory.WoodChanged += OnWoodChanged;
        }

        Build();
        Place(0f);
    }

    private void OnDestroy()
    {
        if (buildMode != null)
        {
            buildMode.TurretSelectionChanged -= OnTurretSelectionChanged;
        }
        if (inventory != null)
        {
            inventory.WoodChanged -= OnWoodChanged;
        }
        Watch(null);
    }

    private void Update()
    {
        bool wanted = buildMode != null && buildMode.IsOpen && buildMode.SelectedTurret != null;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float target = wanted ? 1f : 0f;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-slideSharpness * dt));
        if (Mathf.Abs(shown - target) < 0.001f)
        {
            shown = target;
        }
        Place(shown);
    }

    public void ClickUpgrade(int path)
    {
        if (shownTurret == null)
        {
            return;
        }

        string reason;
        if (!shownTurret.TryUpgrade(path, inventory, out reason) && reason != null)
        {
            ui.ShowError(reason);
        }
    }

    // Sells the shown turret (50% back) and closes the panel.
    public void ClickSell()
    {
        if (buildMode != null)
        {
            buildMode.SellSelected();
        }
    }

    public string RowText(int path)
    {
        if (path < 0 || path >= rows.Count || !rows[path].pathName.gameObject.activeInHierarchy)
        {
            return null;
        }
        Row row = rows[path];
        int bought = 0;
        foreach (Image pip in row.pips)
        {
            if (pip.gameObject.activeSelf && pip.color == pipOnColor)
            {
                bought++;
            }
        }
        return row.pathName.text + " | " + row.upgradeName.text + " | " + row.cost.text + " | tier " + bought;
    }

    private void OnTurretSelectionChanged()
    {
        if (buildMode.SelectedTurret != null)
        {
            Watch(buildMode.SelectedTurret);
            Refresh();
        }
    }

    private void OnWoodChanged(int wood)
    {
        Refresh();
    }

    private void Watch(Turret turret)
    {
        if (shownTurret != null)
        {
            shownTurret.Upgraded -= Refresh;
        }
        shownTurret = turret;
        if (shownTurret != null)
        {
            shownTurret.Upgraded += Refresh;
        }
    }

    private void Refresh()
    {
        if (shownTurret == null || panel == null)
        {
            return;
        }

        TurretDefinition definition = shownTurret.Definition;
        title.text = definition != null ? definition.DisplayName : shownTurret.name;
        picture.texture = definition != null && definition.Prefab != null
            ? DefensePreview.Cached(definition.Prefab.gameObject, pictureSize)
            : null;
        sellText.text = "Sell  (+" + shownTurret.SellValue + " wood)";

        for (int path = 0; path < rows.Count; path++)
        {
            Row row = rows[path];
            bool exists = definition != null && path < definition.Paths.Count;
            row.button.transform.parent.gameObject.SetActive(exists);
            if (!exists)
            {
                continue;
            }

            int tier = shownTurret.Tier(path);
            int tierCount = definition.Paths[path].tiers.Count;
            for (int i = 0; i < row.pips.Length; i++)
            {
                row.pips[i].gameObject.SetActive(i < tierCount);
                row.pips[i].color = i < tier ? pipOnColor : pipOffColor;
            }

            row.pathName.text = definition.Paths[path].name.ToUpperInvariant();
            string reason;
            bool canUpgrade = shownTurret.CanUpgrade(path, out reason);
            TurretUpgrade next = shownTurret.NextUpgrade(path);
            if (!canUpgrade)
            {
                bool maxed = next == null;
                row.upgradeName.text = maxed ? "Maxed" : reason;
                row.cost.text = "";
                row.description.text = maxed ? "Every tier bought." : "Another path is upgraded further.";
                row.button.color = lockedButtonColor;
                row.arrow.text = "-";
                continue;
            }

            bool affordable = inventory != null && inventory.Wood >= next.WoodCost;
            row.upgradeName.text = next.UpgradeName;
            row.cost.text = next.WoodCost + " wood";
            row.cost.color = affordable ? costColor : unaffordableCostColor;
            row.description.text = next.Description;
            row.button.color = affordable ? buttonColor : unaffordableButtonColor;
            row.arrow.text = ">";
        }
    }

    // 0 = hidden past the right edge, 1 = in at edgeMargin.
    private void Place(float amount)
    {
        panel.anchoredPosition = new Vector2(Mathf.Lerp(panelSize.x * scale + 40f, -edgeMargin, amount), verticalOffset);
        panel.gameObject.SetActive(amount > 0.001f);
    }

    private void Build()
    {
        float k = scale;
        Vector2 size = panelSize * k;
        panel = CreateBox(ui.Root, "UpgradePanel", size, panelColor, Mathf.RoundToInt(22 * k));
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);

        title = CreateText(panel, "Name", F(32), TextAnchor.MiddleCenter, Color.white);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -38f * k), new Vector2(size.x - 30f * k, 44f * k));

        RectTransform pictureBox = CreateBox(panel, "PictureBox", new Vector2(size.x - 50f * k, 160f * k), boxColor, Mathf.RoundToInt(16 * k));
        Place(pictureBox, new Vector2(0.5f, 1f), new Vector2(0f, -150f * k), pictureBox.sizeDelta);
        GameObject pictureObject = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
        RectTransform pictureRect = (RectTransform)pictureObject.transform;
        pictureRect.SetParent(pictureBox, false);
        pictureRect.sizeDelta = new Vector2(150f, 150f) * k;
        picture = pictureObject.GetComponent<RawImage>();
        picture.raycastTarget = false;

        float rowHeight = 96f * k;
        float top = -246f * k;
        for (int path = 0; path < 3; path++)
        {
            int index = path;
            GameObject rowObject = new GameObject("Path" + path, typeof(RectTransform));
            RectTransform rowRect = (RectTransform)rowObject.transform;
            rowRect.SetParent(panel, false);
            Place(rowRect, new Vector2(0.5f, 1f), new Vector2(0f, top - path * (rowHeight + 10f * k) - rowHeight * 0.5f),
                new Vector2(size.x - 50f * k, rowHeight));

            Row row = new Row();
            RectTransform button = CreateBox(rowRect, "UpgradeButton", new Vector2(rowHeight, rowHeight), buttonColor, Mathf.RoundToInt(14 * k));
            Place(button, new Vector2(0f, 0.5f), new Vector2(rowHeight * 0.5f, 0f), button.sizeDelta);
            Button clickable = button.gameObject.AddComponent<Button>();
            clickable.targetGraphic = button.GetComponent<Image>();
            ColorBlock colors = clickable.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            clickable.colors = colors;
            clickable.onClick.AddListener(() => ClickUpgrade(index));
            row.button = button.GetComponent<Image>();
            row.arrow = CreateText(button, "Arrow", F(40), TextAnchor.MiddleCenter, Color.white);
            Place(row.arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f * k), new Vector2(rowHeight, 50f * k));

            row.pips = new Image[MaxPips];
            for (int i = 0; i < MaxPips; i++)
            {
                RectTransform pip = CreateBox(button, "Pip" + i, new Vector2(14f, 14f) * k, pipOffColor, Mathf.RoundToInt(3 * k));
                Place(pip, new Vector2(0.5f, 0f), new Vector2((i - (MaxPips - 1) * 0.5f) * 20f * k, 16f * k), pip.sizeDelta);
                pip.GetComponent<Image>().raycastTarget = false;
                row.pips[i] = pip.GetComponent<Image>();
            }

            float boxWidth = size.x - 50f * k - rowHeight - 10f * k;
            RectTransform box = CreateBox(rowRect, "Description", new Vector2(boxWidth, rowHeight), boxColor, Mathf.RoundToInt(14 * k));
            Place(box, new Vector2(1f, 0.5f), new Vector2(-boxWidth * 0.5f, 0f), box.sizeDelta);
            box.GetComponent<Image>().raycastTarget = false;

            float inner = boxWidth - 20f * k;
            row.pathName = CreateText(box, "Path", F(14), TextAnchor.MiddleLeft, labelColor);
            Place(row.pathName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f * k), new Vector2(inner, 20f * k));
            row.upgradeName = CreateText(box, "Upgrade", F(19), TextAnchor.MiddleLeft, Color.white);
            Place(row.upgradeName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f * k), new Vector2(inner, 24f * k));
            row.cost = CreateText(box, "Cost", F(16), TextAnchor.MiddleRight, costColor);
            Place(row.cost.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f * k), new Vector2(inner, 20f * k));
            row.description = CreateText(box, "Text", F(14), TextAnchor.UpperLeft, new Color(0.88f, 0.88f, 0.9f));
            row.description.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(row.description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f * k), new Vector2(inner, 40f * k));
            rows.Add(row);
        }

        RectTransform sell = CreateBox(panel, "SellButton", new Vector2(size.x - 50f * k, 60f * k), sellColor, Mathf.RoundToInt(14 * k));
        Place(sell, new Vector2(0.5f, 0f), new Vector2(0f, 18f * k + 30f * k), sell.sizeDelta);
        Button sellButton = sell.gameObject.AddComponent<Button>();
        sellButton.targetGraphic = sell.GetComponent<Image>();
        ColorBlock sellColors = sellButton.colors;
        sellColors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        sellColors.highlightedColor = Color.white;
        sellColors.selectedColor = sellColors.normalColor;
        sellColors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        sellButton.colors = sellColors;
        sellButton.onClick.AddListener(ClickSell);
        sellText = CreateText(sell, "Label", F(24), TextAnchor.MiddleCenter, Color.white);
        Place(sellText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, sell.sizeDelta);
    }

    // Font size at the panel's scale.
    private int F(int baseSize)
    {
        return Mathf.Max(8, Mathf.RoundToInt(baseSize * scale));
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private RectTransform CreateBox(Transform parent, string name, Vector2 size, Color color, int radius)
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

    private Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = ui.Font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return text;
    }
}
