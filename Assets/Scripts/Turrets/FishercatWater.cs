/*
 * FishercatWater
 * Looping water under the Fishercat (2026-11-25, user's pick: spreading ripples + a gentle bob). On the tower root.
 *
 * - Ripples: from each source spot (the dock posts and a couple of open spots, in the tower root's space) rings grow from
 *   startRadius to endRadius over rippleTime, thinning from startWidth to nothing as they go (the ripple material is
 *   opaque CelLit, so they thin out instead of fading); ringsPerSource rings per spot, evenly staggered, each spot on
 *   its own phase. Rings are broken (every 4th segment left out, like the old static rings) and cut at the water's edge
 *   (waterCentre / waterRadius). All rings are one run-time mesh on a `Ripples` child of the water, rebuilt each frame.
 * - Bob: the water object (and the ripples on it) rises up to bobHeight above its rest height and back, once every
 *   bobPeriod seconds. Never below rest (2026-11-25: dipping below let the dirt paths (lift 0.02) cover the water where it
 *   spills past the footprint), and small, so it stays under the build-mode tile highlight (height 0.035).
 * - Uses scaled time, so it freezes with the game. Purely visual. Numbers are placeholders.
 */

using System.Collections.Generic;
using UnityEngine;

public class FishercatWater : MonoBehaviour
{
    private const int Segments = 20;

    [SerializeField] private Transform water;
    [SerializeField] private Material rippleMaterial;
    [Tooltip("Ripple source spots in the tower root's space (y = height above the water's base).")]
    [SerializeField] private Vector3[] sources =
    {
        new Vector3(-0.08f, 0.03f, 0.2f), new Vector3(1.08f, 0.03f, -1.08f), new Vector3(1.45f, 0.03f, 0.05f),
        new Vector3(-0.35f, 0.03f, -1.2f), new Vector3(0.6f, 0.03f, 0.55f)
    };
    [SerializeField, Min(0.1f)] private float rippleTime = 2.4f;
    [SerializeField, Range(1, 4)] private int ringsPerSource = 2;
    [SerializeField, Min(0f)] private float startRadius = 0.1f;
    [SerializeField, Min(0.05f)] private float endRadius = 0.5f;
    [SerializeField, Min(0.005f)] private float startWidth = 0.045f;
    [SerializeField] private Vector3 waterCentre = new Vector3(0.5f, 0f, -0.5f);
    [SerializeField, Min(0.1f)] private float waterRadius = 1.18f;
    [Tooltip("How far the water rises above its rest height (it never goes below it). Keep it small: the water rests at 0.025, " +
        "just above the dirt paths (0.02), and must stay under the build-mode tile highlight (0.035).")]
    [SerializeField, Min(0f)] private float bobHeight = 0.008f;
    [SerializeField, Min(0.1f)] private float bobPeriod = 2.8f;

    private Mesh mesh;
    private Vector3 waterRest;
    private float time;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();

    private void Awake()
    {
        if (water == null)
        {
            return;
        }
        waterRest = water.localPosition;
        GameObject ripples = new GameObject("Ripples");
        ripples.transform.SetParent(water, false);
        mesh = new Mesh { name = "FishercatRipples" };
        mesh.MarkDynamic();
        ripples.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = ripples.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = rippleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    private void Update()
    {
        if (water == null || mesh == null)
        {
            return;
        }
        time += Time.deltaTime;
        water.localPosition = waterRest + Vector3.up * ((Mathf.Sin(time * 2f * Mathf.PI / bobPeriod) * 0.5f + 0.5f) * bobHeight);

        vertices.Clear();
        normals.Clear();
        triangles.Clear();
        for (int s = 0; s < sources.Length; s++)
        {
            float phase = s * 0.37f;
            for (int k = 0; k < ringsPerSource; k++)
            {
                float t = Mathf.Repeat(time / rippleTime + phase + (float)k / ringsPerSource, 1f);
                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Lerp(startRadius, endRadius, eased);
                float width = startWidth * (1f - t);
                if (width > 0.002f)
                {
                    AddRing(sources[s], radius, width, s + k);
                }
            }
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private void AddRing(Vector3 centre, float radius, float width, int skipShift)
    {
        for (int i = 0; i < Segments; i++)
        {
            if ((i + skipShift) % 4 == 3)
            {
                continue;
            }
            float a0 = i * Mathf.PI * 2f / Segments;
            float a1 = (i + 1) * Mathf.PI * 2f / Segments;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 mid = centre + (d0 + d1) * 0.5f * (radius + width * 0.5f);
            Vector3 fromCentre = mid - waterCentre;
            fromCentre.y = 0f;
            if (fromCentre.magnitude > waterRadius)
            {
                continue;
            }
            int start = vertices.Count;
            vertices.Add(centre + d0 * radius);
            vertices.Add(centre + d1 * radius);
            vertices.Add(centre + d1 * (radius + width));
            vertices.Add(centre + d0 * (radius + width));
            for (int v = 0; v < 4; v++)
            {
                normals.Add(Vector3.up);
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
