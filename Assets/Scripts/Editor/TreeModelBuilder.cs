/*
 * TreeModelBuilder
 * Builds the tree's meshes (2026-11-20, user: the tree looked too simple; keep the footprint, a bit more detail but cheap,
 * simple trunk; picked the tiered pine of four options, the others were removed). World units with the ground on y 0.
 *
 * - Footprint (the old treein2.fbx): trunk 0.4 wide; canopy 2.4 wide from y 1.14 (CanopyBase) to 3.8.
 * - Trunk: a six-sided tapered post with a small root flare, 30 triangles (TreeTrunk.asset, from the ground).
 * - Leaves: three stacked tiers, each a zig-zag skirt (12 points alternating the full radius and 78% of it, the outer
 *   points drooping a little) rising to an apex, turned 15 degrees against the tier below, with a slightly domed
 *   underside; flat-shaded, 72 triangles (TreeLeaves.asset). Its pivot is the canopy base, so ResourceTree's leaf wobble
 *   sways it from the bottom.
 * - Tools > Models > Tree > Build Tree rewrites both assets in place (Tree.prefab keeps its links).
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TreeModelBuilder
{
    public const string TrunkPath = "Assets/Models/TreeTrunk.asset";
    public const string LeavesPath = "Assets/Models/TreeLeaves.asset";
    public const float CanopyBase = 1.14f;
    public const float CanopyHeight = 2.66f;

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int> Triangles = new List<int>();

    [MenuItem("Tools/Models/Tree/Build Tree")]
    public static void Save()
    {
        WriteMesh(BuildLeaves(), LeavesPath);
        WriteMesh(BuildTrunk(), TrunkPath);
        AssetDatabase.SaveAssets();
    }

    private static void WriteMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return;
        }
        existing.Clear();
        existing.SetVertices(mesh.vertices);
        existing.SetNormals(mesh.normals);
        existing.SetTriangles(mesh.triangles, 0);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
    }

    private static void Begin()
    {
        Vertices.Clear();
        Normals.Clear();
        Triangles.Clear();
    }

    private static Mesh Finish(string name)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.SetTriangles(Triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Six-sided tapered post from the ground into the canopy, with a small flare at the roots.
    public static Mesh BuildTrunk()
    {
        Begin();
        float[] heights = { 0f, 0.22f, 1.7f };
        float[] radii = { 0.25f, 0.19f, 0.13f };
        Vector3[] previous = null;
        for (int i = 0; i < heights.Length; i++)
        {
            Vector3[] ring = Ring(heights[i], radii[i], 6);
            if (previous != null)
            {
                for (int s = 0; s < 6; s++)
                {
                    int n = (s + 1) % 6;
                    Vector3 c = (previous[s] + previous[n] + ring[s] + ring[n]) * 0.25f;
                    Vector3 outward = c - new Vector3(0f, c.y, 0f);
                    Tri(previous[s], previous[n], ring[n], outward);
                    Tri(previous[s], ring[n], ring[s], outward);
                }
            }
            previous = ring;
        }
        Vector3 top = new Vector3(0f, heights[2], 0f);
        for (int s = 0; s < 6; s++)
        {
            Tri(top, previous[s], previous[(s + 1) % 6], Vector3.up);
        }
        return Finish("TreeTrunk");
    }

    // Three tiers: (bottom of the skirt, apex, outer radius), each turned 15 degrees against the one below.
    public static Mesh BuildLeaves()
    {
        Begin();
        Tier(0.0f, 1.35f, 1.2f, 0f);
        Tier(0.72f, 2.02f, 0.95f, 15f);
        Tier(1.42f, CanopyHeight, 0.66f, 30f);
        return Finish("TreeLeaves");
    }

    private static Vector3[] Ring(float y, float radius, int sides)
    {
        Vector3[] ring = new Vector3[sides];
        for (int s = 0; s < sides; s++)
        {
            float a = 2f * Mathf.PI * s / sides;
            ring[s] = new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);
        }
        return ring;
    }

    // One tier: a zig-zag skirt (points alternate between the outer radius and 78% of it, the outer ones droop a little)
    // rising to an apex, with a slightly domed underside.
    private static void Tier(float bottom, float apex, float radius, float yawOffset)
    {
        const int Points = 12;
        Vector3[] skirt = new Vector3[Points];
        for (int s = 0; s < Points; s++)
        {
            bool outer = s % 2 == 0;
            float a = (yawOffset + 360f * s / Points) * Mathf.Deg2Rad;
            float r = outer ? radius : radius * 0.78f;
            float y = outer ? bottom - 0.06f : bottom + 0.06f;
            skirt[s] = new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
        }
        Vector3 tip = new Vector3(0f, apex, 0f);
        Vector3 under = new Vector3(0f, bottom + 0.22f, 0f);
        for (int s = 0; s < Points; s++)
        {
            int n = (s + 1) % Points;
            Vector3 c = (skirt[s] + skirt[n] + tip) / 3f;
            Tri(skirt[s], skirt[n], tip, c - new Vector3(0f, c.y, 0f) + Vector3.up * 0.3f);
            Tri(under, skirt[s], skirt[n], Vector3.down);
        }
    }

    // A flat-shaded triangle facing `outward`.
    private static void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
        if (Vector3.Dot(n, outward) < 0f)
        {
            Vector3 swap = b;
            b = c;
            c = swap;
            n = -n;
        }
        int start = Vertices.Count;
        Vertices.Add(a);
        Vertices.Add(b);
        Vertices.Add(c);
        Normals.Add(n);
        Normals.Add(n);
        Normals.Add(n);
        Triangles.Add(start);
        Triangles.Add(start + 1);
        Triangles.Add(start + 2);
    }
}
