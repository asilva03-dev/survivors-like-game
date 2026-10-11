/*
 * DamageNumber / DamageNumberStyle
 * One pooled floating damage number, plus the shared style values that drive every number.
 *
 * - DamageNumberStyle is serialized on DamageNumberSpawner and read by every number each frame, so
 *   timing, scale and fade values can be tweaked live in Play mode. Text color, font size and outline
 *   size are applied when a number is shown.
 * - Motion: rises riseDistance over lifetime with an ease-out. Scale starts at startScale, grows to
 *   popScale over popDuration, then settles to 1 over settleDuration. Alpha holds at 1, then fades to
 *   0 over the last fadeDuration seconds.
 * - The number is a UI Text (Minecraftia font) with a UI Outline for the black border, living under
 *   the spawner's world-space canvas. It copies the camera rotation so it always faces the player's
 *   view. Pooled: Show() fully resets it and it returns itself to the pool when its lifetime ends.
 */

using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class DamageNumberStyle
{
    [Header("Look")]
    public Color textColor = Color.white;
    public Color outlineColor = Color.black;
    [Min(1)] public int fontSize = 40;
    [Min(0f)] public float outlineSize = 2f;

    [Header("Timing")]
    [Min(0.05f)] public float lifetime = 0.8f;
    [Min(0f)] public float fadeDuration = 0.4f;

    [Header("Motion")]
    public float riseDistance = 1.5f;
    public float heightOffset = 0.8f;
    [Min(0f)] public float spawnRadius = 0.4f;

    [Header("Pop")]
    [Min(0f)] public float startScale = 0.5f;
    [Min(0f)] public float popScale = 1.4f;
    [Min(0.001f)] public float popDuration = 0.08f;
    [Min(0.001f)] public float settleDuration = 0.12f;
}

public class DamageNumber : MonoBehaviour
{
    private const float WorldScale = 0.01f;

    [SerializeField] private Text label;
    [SerializeField] private Outline outline;

    private ComponentPool<DamageNumber> pool;
    private DamageNumberStyle style;
    private Camera cam;
    private Vector3 origin;
    private float age;

    public void Show(ComponentPool<DamageNumber> owningPool, DamageNumberStyle numberStyle, Vector3 position, string value)
    {
        pool = owningPool;
        style = numberStyle;
        origin = position;
        age = 0f;

        label.text = value;
        label.fontSize = style.fontSize;
        label.color = style.textColor;
        outline.effectColor = style.outlineColor;
        outline.effectDistance = new Vector2(style.outlineSize, -style.outlineSize);

        Apply();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= style.lifetime)
        {
            pool.Release(this);
            return;
        }

        Apply();
    }

    private void Apply()
    {
        float t = Mathf.Clamp01(age / style.lifetime);
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = origin + Vector3.up * (style.riseDistance * eased);

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;
        }

        transform.localScale = Vector3.one * (WorldScale * CurrentScale());

        float fadeStart = Mathf.Max(0f, style.lifetime - style.fadeDuration);
        float alpha = style.fadeDuration > 0f
            ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, style.lifetime, age))
            : 1f;

        Color color = style.textColor;
        color.a *= alpha;
        label.color = color;

        Color border = style.outlineColor;
        border.a *= alpha;
        outline.effectColor = border;
    }

    private float CurrentScale()
    {
        if (age < style.popDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, age / style.popDuration);
            return Mathf.Lerp(style.startScale, style.popScale, k);
        }

        float settle = (age - style.popDuration) / style.settleDuration;
        if (settle < 1f)
        {
            return Mathf.Lerp(style.popScale, 1f, Mathf.SmoothStep(0f, 1f, settle));
        }

        return 1f;
    }
}
