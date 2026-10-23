/*
 * PresetDraftUI
 * The screen side of the preset draft, built at runtime on its own overlay canvas (and an EventSystem if the scene has
 * none). PresetDraft drives it.
 *
 * - ShowCards(): a dimmed screen with a title, one card per offered chunk (picture, name, "scroll to rotate") and,
 *   for the normal drafts, a "Your base" reference card on the left showing the whole current layout (not rotatable).
 *   Scrolling over a card turns its chunk; clicking picks it and reports the card's index and rotation.
 * - Pictures are 3D top-down renders from ChunkPreview (the card's model turns when the card is rotated); without a
 *   preview assigned the cards fall back to flat ChunkMinimap textures.
 * - ShowHint(): the bar at the bottom of the screen used while a picked chunk is being placed.
 * Sizes are in 1920x1080 reference pixels (the canvas scales with the screen).
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class PresetDraftUI : MonoBehaviour
{
    [SerializeField] private Font font;
    [SerializeField] private ChunkPreview preview;
    [SerializeField] private int sortingOrder = 50;
    [SerializeField] private Vector2 cardSize = new Vector2(300f, 390f);
    [SerializeField] private float mapSize = 240f;
    [SerializeField] private float referenceMapSize = 300f;
    [SerializeField] private float cardGap = 40f;
    [Tooltip("Mini-map pixels per chunk cell.")]
    [SerializeField, Min(1)] private int pixelsPerCell = 4;
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color validHintColor = new Color(0.55f, 1f, 0.6f);
    [SerializeField] private Color invalidHintColor = new Color(1f, 0.5f, 0.45f);

    private readonly List<DraftCard> cards = new List<DraftCard>();
    private readonly List<int> rotations = new List<int>();
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private List<WallChunk> offers;
    private Action<int, int> onPick;

    private RectTransform root;
    private GameObject draftScreen;
    private Text title;
    private DraftCard reference;
    private Texture2D referenceTexture;
    private GameObject hintBar;
    private Text hintText;
    private Text reasonText;

    public bool CardsShown => draftScreen != null && draftScreen.activeSelf;

    private void Awake()
    {
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        Build();
    }

    private void OnDestroy()
    {
        foreach (Texture2D tex in textures)
        {
            Destroy(tex);
        }
        if (referenceTexture != null)
        {
            Destroy(referenceTexture);
        }
    }

    /// <summary>Shows the cards; onPick gets (card index, its rotation) when one is clicked.</summary>
    public void ShowCards(List<WallChunk> chunks, string heading, BaseLayout layoutForReference, Action<int, int> pick)
    {
        offers = chunks;
        onPick = pick;
        title.text = heading;
        draftScreen.SetActive(true);

        bool showReference = layoutForReference != null;
        reference.gameObject.SetActive(showReference);
        float rowWidth = chunks.Count * cardSize.x + Mathf.Max(0, chunks.Count - 1) * cardGap;
        float referenceWidth = referenceMapSize + 40f;
        float total = rowWidth + (showReference ? referenceWidth + cardGap * 2f : 0f);
        float x = -total * 0.5f;

        if (showReference)
        {
            if (preview != null)
            {
                reference.Map.texture = preview.ShowLayout();
            }
            else
            {
                referenceTexture = ChunkMinimap.Layout(layoutForReference, 1, referenceTexture);
                reference.Map.texture = referenceTexture;
            }
            reference.Map.rectTransform.sizeDelta = new Vector2(referenceMapSize, referenceMapSize);
            ((RectTransform)reference.transform).anchoredPosition = new Vector2(x + referenceWidth * 0.5f, -20f);
            x += referenceWidth + cardGap * 2f;
        }

        while (cards.Count < chunks.Count)
        {
            cards.Add(CreateCard(cards.Count));
            textures.Add(null);
        }
        rotations.Clear();
        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < chunks.Count;
            cards[i].gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }
            rotations.Add(0);
            cards[i].Title.text = chunks[i].name;
            cards[i].Background.color = cards[i].NormalColor;
            ((RectTransform)cards[i].transform).anchoredPosition = new Vector2(x + cardSize.x * 0.5f, -20f);
            x += cardSize.x + cardGap;
            if (preview != null)
            {
                cards[i].Map.texture = preview.ShowChunk(i, chunks[i], 0);
                cards[i].Map.rectTransform.sizeDelta = new Vector2(mapSize, mapSize);
            }
            RefreshCard(i);
        }
    }

    public void HideCards()
    {
        draftScreen.SetActive(false);
        if (preview != null)
        {
            preview.Hide();
        }
        onPick = null;
    }

    public void ShowHint(string text, string reason, bool valid)
    {
        hintBar.SetActive(true);
        hintText.text = text;
        reasonText.text = reason;
        reasonText.color = valid ? validHintColor : invalidHintColor;
    }

    public void HideHint()
    {
        hintBar.SetActive(false);
    }

    /// <summary>Picks a card as if it were clicked (tests).</summary>
    public void Pick(int index)
    {
        if (onPick != null && index >= 0 && index < rotations.Count)
        {
            onPick(index, rotations[index]);
        }
    }

    /// <summary>Turns a card as if scrolled over it (tests).</summary>
    public void Rotate(int index, int direction)
    {
        if (index < 0 || index >= rotations.Count)
        {
            return;
        }
        rotations[index] = (rotations[index] + (direction > 0 ? 1 : 3)) & 3;
        if (preview != null)
        {
            preview.Turn(index, direction);
        }
        RefreshCard(index);
    }

    public int CardRotation(int index) => index >= 0 && index < rotations.Count ? rotations[index] : 0;

    private void RefreshCard(int index)
    {
        WallChunk chunk = offers[index];
        int rotation = rotations[index];
        DraftCard card = cards[index];
        if (preview == null)
        {
            textures[index] = ChunkMinimap.Chunk(chunk, rotation, pixelsPerCell, textures[index]);
            card.Map.texture = textures[index];
            float w = chunk.RotatedWidth(rotation) + 2;
            float h = chunk.RotatedHeight(rotation) + 2;
            float fit = mapSize / Mathf.Max(w, h);
            card.Map.rectTransform.sizeDelta = new Vector2(w * fit, h * fit);
        }
        card.Hint.text = "Scroll: rotate (" + rotation * 90 + "°)\nClick: pick";
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("PresetDraftCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)canvasObject.transform;

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }

        draftScreen = new GameObject("DraftScreen", typeof(RectTransform), typeof(Image));
        RectTransform screen = (RectTransform)draftScreen.transform;
        screen.SetParent(root, false);
        Stretch(screen);
        draftScreen.GetComponent<Image>().color = dimColor;

        title = CreateText(screen, "Title", 44, TextAnchor.MiddleCenter);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -150f);
        title.rectTransform.sizeDelta = new Vector2(1600f, 80f);

        reference = CreateCardObject(screen, "YourBase", new Vector2(referenceMapSize + 40f, referenceMapSize + 120f));
        reference.Interactive = false;
        reference.Title.text = "Your base";
        reference.Hint.text = "";
        reference.Background.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
        reference.NormalColor = reference.Background.color;
        draftScreen.SetActive(false);

        hintBar = new GameObject("PlacementHint", typeof(RectTransform), typeof(Image));
        RectTransform bar = (RectTransform)hintBar.transform;
        bar.SetParent(root, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 40f);
        bar.sizeDelta = new Vector2(1100f, 110f);
        Image barImage = hintBar.GetComponent<Image>();
        barImage.color = new Color(0f, 0f, 0f, 0.6f);
        barImage.raycastTarget = false;
        hintText = CreateText(bar, "Controls", 26, TextAnchor.MiddleCenter);
        hintText.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        hintText.rectTransform.sizeDelta = new Vector2(1060f, 44f);
        reasonText = CreateText(bar, "Reason", 26, TextAnchor.MiddleCenter);
        reasonText.rectTransform.anchoredPosition = new Vector2(0f, -24f);
        reasonText.rectTransform.sizeDelta = new Vector2(1060f, 44f);
        hintBar.SetActive(false);
    }

    private DraftCard CreateCard(int index)
    {
        DraftCard card = CreateCardObject((RectTransform)draftScreen.transform, "Card" + index, cardSize);
        card.Clicked += () => Pick(index);
        card.Scrolled += direction => Rotate(index, direction);
        return card;
    }

    private DraftCard CreateCardObject(RectTransform parent, string name, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        DraftCard card = go.AddComponent<DraftCard>();
        card.Background = go.GetComponent<Image>();
        card.Background.color = card.NormalColor;

        card.Title = CreateText(rect, "Name", 26, TextAnchor.MiddleCenter);
        card.Title.rectTransform.anchorMin = card.Title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        card.Title.rectTransform.anchoredPosition = new Vector2(0f, -32f);
        card.Title.rectTransform.sizeDelta = new Vector2(size.x - 20f, 40f);

        GameObject map = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
        RectTransform mapRect = (RectTransform)map.transform;
        mapRect.SetParent(rect, false);
        mapRect.anchoredPosition = new Vector2(0f, size.y * 0.5f - 60f - (size.x - 40f) * 0.5f);
        card.Map = map.GetComponent<RawImage>();
        card.Map.raycastTarget = false;

        card.Hint = CreateText(rect, "Hint", 20, TextAnchor.MiddleCenter);
        card.Hint.rectTransform.anchorMin = card.Hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        card.Hint.rectTransform.anchoredPosition = new Vector2(0f, 36f);
        card.Hint.rectTransform.sizeDelta = new Vector2(size.x - 20f, 56f);
        card.Hint.color = new Color(0.75f, 0.75f, 0.8f);
        return card;
    }

    private Text CreateText(RectTransform parent, string name, int size, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
