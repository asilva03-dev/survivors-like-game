/*
 * GroundRangeIndicator
 * Base class that draws a flat circle on the ground around a center point. Subclasses supply the
 * center and radius (weapon range, too-close range, ...).
 *
 * - Put it on its own root-level object with a LineRenderer (do not parent it to the player;
 *   it positions itself each frame and is laid flat by its own rotation).
 * - The circle is rebuilt whenever the radius changes, so tweaking a range in the inspector at
 *   runtime updates it live.
 * - groundHeight lifts the line slightly above the ground to avoid z-fighting. Use a different
 *   line color or height per indicator so overlapping circles stay readable.
 */

using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public abstract class GroundRangeIndicator : MonoBehaviour
{
    [SerializeField] private int segments = 96;
    [SerializeField] private float groundHeight = 0.05f;

    private LineRenderer line;
    private float drawnRadius = -1f;

    protected abstract Vector3 Center { get; }
    protected abstract float Radius { get; }

    protected virtual void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.alignment = LineAlignment.TransformZ;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void LateUpdate()
    {
        Vector3 center = Center;
        transform.position = new Vector3(center.x, groundHeight, center.z);

        if (!Mathf.Approximately(drawnRadius, Radius))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        drawnRadius = Radius;
        line.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * drawnRadius);
        }
    }
}
