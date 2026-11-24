/*
 * SpearString
 * The thin black string trailing from the end of a Fishercat spear's handle (2026-11-24, user: it should flap as it flies;
 * later upgrades will reel struck rats in on it). On SpearProjectile.prefab > String (the handle's end), with a LineRenderer.
 *
 * - Each frame it records where the handle's end has been and lays `points` points back along that path, up to `length`
 *   world units (shorter right after the throw), in world space.
 * - Flap: every point is pushed sideways (and a little up/down) by a wave travelling down the string (flapSpeed rad/s,
 *   flapWaves waves along it), zero at the spear and growing to flapAmplitude at the loose end.
 * - Resets when the pooled spear is taken out again (OnEnable). Purely visual; no collider.
 */

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SpearString : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float length = 1.6f;
    [SerializeField, Range(3, 24)] private int points = 10;
    [SerializeField, Min(0f)] private float flapAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float flapWaves = 1.3f;
    [SerializeField, Min(0f)] private float flapSpeed = 22f;
    [Tooltip("Share of the flap that goes up/down instead of sideways.")]
    [SerializeField, Range(0f, 1f)] private float verticalShare = 0.35f;

    private LineRenderer line;
    private readonly List<Vector3> path = new List<Vector3>();
    private Vector3[] positions;
    private float phase;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
    }

    private void OnEnable()
    {
        path.Clear();
        phase = Random.Range(0f, Mathf.PI * 2f);
        if (line != null)
        {
            line.positionCount = 0;
        }
    }

    private void LateUpdate()
    {
        Vector3 here = transform.position;
        if (path.Count == 0 || (path[path.Count - 1] - here).sqrMagnitude > 0.0004f)
        {
            path.Add(here);
        }
        TrimPath();
        if (path.Count < 2)
        {
            line.positionCount = 0;
            return;
        }

        phase += flapSpeed * Time.deltaTime;
        if (positions == null || positions.Length != points)
        {
            positions = new Vector3[points];
        }

        float total = PathLength();
        Vector3 back = (path[path.Count - 2] - here).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, back);
        side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
        Vector3 up = Vector3.Cross(back, side).normalized;

        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            float s = t * Mathf.Min(length, total);
            float wave = Mathf.Sin(phase - t * flapWaves * Mathf.PI * 2f);
            float wave2 = Mathf.Cos(phase * 0.8f - t * flapWaves * Mathf.PI * 2f);
            Vector3 offset = (side * wave * (1f - verticalShare) + up * wave2 * verticalShare) * (flapAmplitude * t);
            positions[i] = PointBack(s) + offset;
        }
        line.positionCount = points;
        line.SetPositions(positions);
    }

    // Drop recorded points beyond the string's length (keep one past it to interpolate).
    private void TrimPath()
    {
        float sum = 0f;
        for (int i = path.Count - 1; i > 0; i--)
        {
            sum += Vector3.Distance(path[i], path[i - 1]);
            if (sum > length && i > 1)
            {
                path.RemoveRange(0, i - 1);
                return;
            }
        }
    }

    private float PathLength()
    {
        float sum = 0f;
        for (int i = path.Count - 1; i > 0; i--)
        {
            sum += Vector3.Distance(path[i], path[i - 1]);
        }
        return sum;
    }

    // The point `distance` back along the recorded path from the handle's end.
    private Vector3 PointBack(float distance)
    {
        float left = distance;
        for (int i = path.Count - 1; i > 0; i--)
        {
            float step = Vector3.Distance(path[i], path[i - 1]);
            if (left <= step)
            {
                return Vector3.Lerp(path[i], path[i - 1], step > 0f ? left / step : 0f);
            }
            left -= step;
        }
        return path[0];
    }
}
