/*
 * DefensePreview
 * Static helper that renders a defense prefab into a small transparent picture (for the build bar slots and, later,
 * the upgrade panel).
 *
 * - Cached(prefab, size): rendered once per prefab and shared (bar slots, upgrade panel).
 * - ModelCopy(prefab, parent): the prefab instantiated under an inactive holder, so none of its scripts wake up (no
 *   gun pools, no Turret.Active entry), with every non-render component stripped. Used for the picture (placed far
 *   away at StageOrigin) and for the build ghost.
 * - A temporary orthographic camera looks at its front three-quarter side (defenses face +Z when placed: from the
 *   north-east, ViewYaw, ViewAngle degrees down), framed tightly on its
 *   renderer bounds as seen from that angle, renders once into a RenderTexture with a transparent background, and the
 *   result is copied into a Texture2D. Everything temporary is destroyed afterwards. Lit by the scene lights.
 */

using System.Collections.Generic;
using UnityEngine;

public static class DefensePreview
{
    private static readonly Vector3 StageOrigin = new Vector3(6000f, 0f, 6000f);
    private const float ViewAngle = 35f;
    private const float ViewYaw = 215f;
    private const float Margin = 1.05f;

    // A copy of the prefab with only Transforms, MeshFilters and MeshRenderers (no scripts ever run on it), active,
    // under parent. Instantiated under an inactive holder so nothing wakes up before the strip.
    public static GameObject ModelCopy(GameObject prefab, Transform parent)
    {
        GameObject holder = new GameObject("ModelCopyHolder");
        holder.SetActive(false);
        GameObject model = Object.Instantiate(prefab, holder.transform);

        List<Component> strip = new List<Component>();
        foreach (Component component in model.GetComponentsInChildren<Component>(true))
        {
            if (!(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer))
            {
                strip.Add(component);
            }
        }
        for (int i = strip.Count - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(strip[i]);
        }

        model.transform.SetParent(parent, false);
        Object.DestroyImmediate(holder);
        return model;
    }

    private static readonly Dictionary<GameObject, Texture2D> cache = new Dictionary<GameObject, Texture2D>();

    // Render once per prefab and reuse (the bar slots and the upgrade panel share it). Size of the first call wins.
    public static Texture2D Cached(GameObject prefab, int size)
    {
        Texture2D picture;
        if (prefab == null)
        {
            return null;
        }
        if (!cache.TryGetValue(prefab, out picture) || picture == null)
        {
            picture = Render(prefab, size);
            cache[prefab] = picture;
        }
        return picture;
    }

    public static Texture2D Render(GameObject prefab, int size)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject holder = new GameObject("DefensePreviewStage");
        holder.transform.position = StageOrigin;
        GameObject model = ModelCopy(prefab, holder.transform);
        model.transform.position = StageOrigin;

        Bounds bounds = new Bounds(StageOrigin, Vector3.zero);
        bool first = true;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (first)
            {
                bounds = renderer.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        GameObject cameraObject = new GameObject("DefensePreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        Quaternion rotation = Quaternion.Euler(ViewAngle, ViewYaw, 0f);

        // Frame on the bounds as seen from the camera: project the 8 corners onto the view plane.
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            float x = Vector3.Dot(corner - bounds.center, right);
            float y = Vector3.Dot(corner - bounds.center, up);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        Vector3 viewCenter = bounds.center + right * (minX + maxX) * 0.5f + up * (minY + maxY) * 0.5f;
        camera.orthographicSize = Mathf.Max(maxX - minX, maxY - minY) * 0.5f * Margin;
        cameraObject.transform.SetPositionAndRotation(viewCenter - rotation * Vector3.forward * 40f, rotation);

        RenderTexture target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D picture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = prefab.name + "Preview" };
        picture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
        picture.Apply();
        RenderTexture.active = previous;

        camera.targetTexture = null;
        RenderTexture.ReleaseTemporary(target);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(holder);
        return picture;
    }
}
