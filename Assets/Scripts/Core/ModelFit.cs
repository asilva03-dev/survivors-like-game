/*
 * ModelFit
 * Shared helper that fits an instantiated model into a holder transform: scales the holder so the model's
 * largest dimension equals `size`, and moves the model so its visual centre sits on the holder's pivot
 * (so it can spin or tumble in place).
 *
 * - Used by ThrownProjectile, BurstProjectile and WheelOWeapon. The model must already be a child of the
 *   holder, and the holder is expected to start at scale 1 (its scale is set, not multiplied).
 * - A model with no renderers is left untouched.
 */

using UnityEngine;

public static class ModelFit
{
    public static void Fit(Transform holder, Transform model, float size)
    {
        model.localPosition = Vector3.zero;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        float largest = Largest(Bounds(renderers));
        if (largest > 0.0001f)
        {
            holder.localScale = Vector3.one * (size / largest);
        }

        // Recompute after scaling so the model's visual centre sits on the holder's pivot.
        model.position += holder.position - Bounds(renderers).center;
    }

    private static Bounds Bounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static float Largest(Bounds bounds)
    {
        return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
    }
}
