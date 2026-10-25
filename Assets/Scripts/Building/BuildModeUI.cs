/*
 * BuildModeUI
 * The screen side of build mode, built at runtime on its own overlay canvas (1920x1080 reference, scales with the
 * screen). Holds the short message near the bottom centre; BuildBarUI (the defense bar) and later the upgrade panel
 * draw on the same canvas (Root, Font, RoundedBox).
 *
 * - The message is text on a rounded box (backgroundColor, black 40%; corners cornerRadius; padding around the
 *   text), sized to the text. It shows for messageTime seconds, then box and text fade out together over fadeTime.
 * - ShowError() is the refused-action style (e.g. "Build only in the light"): errorColor (red) and a shake of the
 *   whole box that dies out over shakeDuration: shakeAmplitude pixels sideways at shakeFrequency plus
 *   verticalShakeAmplitude up/down at verticalFrequencyRatio x that speed.
 * - ShowHint(controls, reason, valid) / HideHint(): a rounded box at hintHeight above the bottom while the build
 *   ghost is out: the controls line and the tile's reason (red) or "Ready to build" (green).
 * - The rounded box sprite is generated in code (9-sliced), so no image asset is needed.
 * - `font` = Minecraftia (falls back to the built-in font).
 */

using UnityEngine;
using UnityEngine.UI;

public class BuildModeUI : MonoBehaviour
{
    private const float MaxStep = 0.05f;

    [SerializeField] private Font font;
    [SerializeField] private int sortingOrder = 40;
    [SerializeField, Min(0f)] private float messageTime = 1.2f;
    [SerializeField, Min(0.01f)] private float fadeTime = 0.4f;
    [SerializeField] private Color messageColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private float messageHeight = 285f; // above the defense bar and its tabs

    [Header("Message box")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField, Range(2, 40)] private int cornerRadius = 14;
    [SerializeField] private Vector2 padding = new Vector2(28f, 12f);

    [Header("Hint box (while placing)")]
    [SerializeField] private float hintHeight = 40f;
    [SerializeField] private Color hintTextColor = Color.white;
    [SerializeField] private Color hintValidColor = new Color(0.55f, 1f, 0.6f);
    [SerializeField] private Color hintInvalidColor = new Color(1f, 0.45f, 0.4f);

    [Header("Error message (red + shake)")]
    [SerializeField] private Color errorColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField, Min(0f)] private float shakeAmplitude = 14f;
    [SerializeField, Min(0f)] private float verticalShakeAmplitude = 8f;
    [Tooltip("Vertical shake speed relative to the horizontal one (not 1, so the text doesn't slide on a diagonal line).")]
    [SerializeField, Min(0.1f)] private float verticalFrequencyRatio = 1.37f;
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.4f;
    [SerializeField, Min(1f)] private float shakeFrequency = 30f;

    private RectTransform root;
    private RectTransform box;
    private Image boxImage;
    private Text message;
    private float messageTimer;
    private Color currentColor;
    private float shakeTimer;
    private RectTransform hintBox;
    private Text hintControls;
    private Text hintReason;

    public RectTransform Root => root;
    public Font Font => font;

    public string CurrentMessage => box != null && box.gameObject.activeSelf ? message.text : null;
    public Color CurrentColor => currentColor;
    public Color CurrentBackgroundColor => boxImage != null ? boxImage.color : Color.clear;
    public Vector2 CurrentBoxSize => box != null ? box.sizeDelta : Vector2.zero;
    public bool HintShown => hintBox != null && hintBox.gameObject.activeSelf;
    public string HintReason => hintReason != null ? hintReason.text : null;
    public Vector2 CurrentShakeOffset => box != null ? box.anchoredPosition - new Vector2(0f, messageHeight) : Vector2.zero;

    private void Awake()
    {
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        Build();
    }

    private void Update()
    {
        if (messageTimer <= 0f)
        {
            return;
        }

        // Capped step so a hitch (or the unfocused editor) can't skip the shake or the fade in one frame.
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        messageTimer -= dt;
        float fade = Mathf.Clamp01(messageTimer / fadeTime);
        Color color = currentColor;
        color.a = fade;
        message.color = color;
        Color background = backgroundColor;
        background.a *= fade;
        boxImage.color = background;

        // Horizontal + vertical shake (different speeds) that dies out over shakeDuration.
        Vector2 offset = Vector2.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer = Mathf.Max(0f, shakeTimer - dt);
            float strength = shakeTimer / shakeDuration;
            float phase = (shakeDuration - shakeTimer) * shakeFrequency * 2f * Mathf.PI;
            offset.x = Mathf.Sin(phase) * shakeAmplitude * strength;
            offset.y = Mathf.Sin(phase * verticalFrequencyRatio + 1f) * verticalShakeAmplitude * strength;
        }
        box.anchoredPosition = new Vector2(offset.x, messageHeight + offset.y);

        if (messageTimer <= 0f)
        {
            box.gameObject.SetActive(false);
        }
    }

    public void ShowMessage(string text)
    {
        Show(text, messageColor, false);
    }

    // A refused action (e.g. opening build mode in the dark): red text that shakes.
    public void ShowError(string text)
    {
        Show(text, errorColor, true);
    }

    // The box at the bottom while a defense is being placed: the controls, and below them the reason the tile is
    // invalid (red) or that it's ready (green).
    public void ShowHint(string controls, string reason, bool valid)
    {
        hintControls.text = controls;
        hintReason.text = reason ?? "";
        hintReason.color = valid ? hintValidColor : hintInvalidColor;
        float width = Mathf.Max(hintControls.preferredWidth, hintReason.preferredWidth) + padding.x * 2f;
        hintBox.sizeDelta = new Vector2(width, hintBox.sizeDelta.y);
        if (!hintBox.gameObject.activeSelf)
        {
            hintBox.gameObject.SetActive(true);
        }
    }

    public void HideHint()
    {
        if (hintBox != null && hintBox.gameObject.activeSelf)
        {
            hintBox.gameObject.SetActive(false);
        }
    }

    private void Show(string text, Color color, bool shake)
    {
        currentColor = color;
        message.text = text;
        message.color = color;
        boxImage.color = backgroundColor;
        box.sizeDelta = new Vector2(message.preferredWidth + padding.x * 2f, message.preferredHeight + padding.y * 2f);
        box.anchoredPosition = new Vector2(0f, messageHeight);
        box.gameObject.SetActive(true);
        messageTimer = messageTime + fadeTime;
        shakeTimer = shake ? shakeDuration : 0f;
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("BuildModeCanvas");
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

        GameObject boxObject = new GameObject("Message", typeof(RectTransform), typeof(Image));
        box = (RectTransform)boxObject.transform;
        box.SetParent(root, false);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.anchoredPosition = new Vector2(0f, messageHeight);
        boxImage = boxObject.GetComponent<Image>();
        boxImage.sprite = RoundedBox(cornerRadius);
        boxImage.type = Image.Type.Sliced;
        boxImage.color = backgroundColor;
        boxImage.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(box, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        message = textObject.GetComponent<Text>();
        message.font = font;
        message.fontSize = 30;
        message.alignment = TextAnchor.MiddleCenter;
        message.raycastTarget = false;
        message.horizontalOverflow = HorizontalWrapMode.Overflow;
        message.verticalOverflow = VerticalWrapMode.Overflow;
        textObject.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        boxObject.SetActive(false);

        GameObject hintObject = new GameObject("PlacementHint", typeof(RectTransform), typeof(Image));
        hintBox = (RectTransform)hintObject.transform;
        hintBox.SetParent(root, false);
        hintBox.anchorMin = hintBox.anchorMax = new Vector2(0.5f, 0f);
        hintBox.pivot = new Vector2(0.5f, 0f);
        hintBox.anchoredPosition = new Vector2(0f, hintHeight);
        hintBox.sizeDelta = new Vector2(700f, 92f);
        Image hintImage = hintObject.GetComponent<Image>();
        hintImage.sprite = RoundedBox(cornerRadius);
        hintImage.type = Image.Type.Sliced;
        hintImage.color = backgroundColor;
        hintImage.raycastTarget = false;
        hintControls = CreateLine(hintBox, "Controls", 22f);
        hintControls.color = hintTextColor;
        hintReason = CreateLine(hintBox, "Reason", -22f);
        hintObject.SetActive(false);
    }

    private Text CreateLine(RectTransform parent, string name, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1200f, 40f);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return text;
    }

    private static readonly System.Collections.Generic.Dictionary<int, Sprite> roundedBoxes =
        new System.Collections.Generic.Dictionary<int, Sprite>();

    // White rounded square (radius in pixels, anti-aliased edge) with 9-slice borders, tinted by the Image colour.
    // Cached per radius; shared by every build-mode panel.
    public static Sprite RoundedBox(int radius)
    {
        Sprite cached;
        if (roundedBoxes.TryGetValue(radius, out cached) && cached != null)
        {
            return cached;
        }

        int size = radius * 2 + 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RoundedBox",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance from the pixel centre to the nearest corner circle centre (inside the flat middle = 0).
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        roundedBoxes[radius] = sprite;
        return sprite;
    }
}
