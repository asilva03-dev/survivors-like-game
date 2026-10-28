/*
 * MapMinimap
 * A simple map in the top-left corner (slice 8, so the map's size can be judged while walking it in MapTest): the
 * painted height image (MapDraftSettings.HeightImage, north up) with a dot for the player and one for the base.
 *
 * - Put it on a screen-space canvas (the HUD); it builds its own child "Minimap" at Awake: a dark frame, the map
 *   image and the two dots (plain squares). offset/size are in canvas units from the top-left corner (below the wood
 *   counter by default).
 * - Positions come from the same numbers as the greybox (mapCenter, unitsPerPixel, the image size), so the dots line
 *   up with the built map. A dot outside the map is hidden.
 * - The image should be imported with point filtering, no compression and no power-of-two resize so it stays crisp
 *   (display only; the checker and builder read the PNG file itself).
 */

using UnityEngine;
using UnityEngine.UI;

public class MapMinimap : MonoBehaviour
{
    [SerializeField] private MapDraftSettings settings;
    [Tooltip("Top-left corner of the map box, from the canvas's top-left corner (canvas units).")]
    [SerializeField] private Vector2 offset = new Vector2(30f, -100f);
    [Tooltip("Width of the map box (canvas units); the height follows the image's shape.")]
    [SerializeField, Min(32f)] private float size = 260f;
    [SerializeField, Min(0f)] private float frameWidth = 4f;
    [SerializeField] private Color frameColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color playerColor = new Color(0.2f, 1f, 1f);
    [SerializeField] private Color baseColor = new Color(1f, 0.55f, 0.1f);
    [SerializeField, Min(2f)] private float playerDotSize = 10f;
    [SerializeField, Min(2f)] private float baseDotSize = 8f;

    private RectTransform mapRect;
    private RectTransform playerDot;
    private RectTransform baseDot;
    private Vector2 mapWorldMin;
    private Vector2 mapWorldSize;

    private void Awake()
    {
        if (settings == null || settings.HeightImage == null)
        {
            enabled = false;
            return;
        }

        Texture2D image = settings.HeightImage;
        float unit = settings.UnitsPerPixel;
        mapWorldSize = new Vector2(image.width * unit, image.height * unit);
        mapWorldMin = new Vector2(settings.MapCenter.x, settings.MapCenter.z) - mapWorldSize * 0.5f;
        float height = size * image.height / image.width;

        RectTransform frame = NewRect("Minimap", transform);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0f, 1f);
        frame.anchoredPosition = offset;
        frame.sizeDelta = new Vector2(size + frameWidth * 2f, height + frameWidth * 2f);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = frameColor;
        frameImage.raycastTarget = false;

        mapRect = NewRect("Map", frame);
        mapRect.anchorMin = mapRect.anchorMax = mapRect.pivot = new Vector2(0f, 0f);
        mapRect.anchoredPosition = new Vector2(frameWidth, frameWidth);
        mapRect.sizeDelta = new Vector2(size, height);
        RawImage map = mapRect.gameObject.AddComponent<RawImage>();
        map.texture = image;
        map.raycastTarget = false;

        baseDot = Dot("Base", baseColor, baseDotSize);
        playerDot = Dot("Player", playerColor, playerDotSize);
    }

    private void LateUpdate()
    {
        Place(playerDot, PlayerController.Instance != null ? PlayerController.Instance.transform.position : (Vector3?)null);
        Place(baseDot, BaseLight.Instance != null ? BaseLight.Instance.BasePosition : (Vector3?)null);
    }

    private void Place(RectTransform dot, Vector3? world)
    {
        if (!world.HasValue)
        {
            dot.gameObject.SetActive(false);
            return;
        }
        Vector2 t = new Vector2((world.Value.x - mapWorldMin.x) / mapWorldSize.x, (world.Value.z - mapWorldMin.y) / mapWorldSize.y);
        bool inside = t.x >= 0f && t.x <= 1f && t.y >= 0f && t.y <= 1f;
        dot.gameObject.SetActive(inside);
        if (inside)
        {
            dot.anchoredPosition = Vector2.Scale(t, mapRect.sizeDelta);
        }
    }

    private RectTransform Dot(string name, Color color, float dotSize)
    {
        RectTransform dot = NewRect(name, mapRect);
        dot.anchorMin = dot.anchorMax = new Vector2(0f, 0f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(dotSize, dotSize);
        Image image = dot.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        Outline outline = dot.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return dot;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
