/*
 * RatModelBuilder
 * Builds the low-poly basic rat mesh (2026-11-20, user: the old rat read as a flat slab) from tapered boxes, so it can
 * be tweaked and rebuilt here: Tools > Models > Build Low-Poly Rat writes Assets/Models/RatLowPoly.asset.
 *
 * - Units are world units with the feet on y 0 and the nose toward +Z (the Enemy Visual's forward). ~1.5 long with the
 *   tail, ~0.55 wide, ~0.65 tall at the ears.
 * - Parts: hunched body (rump higher than the chest), wedge head with a narrow snout and a nose, big ears with a pink
 *   lining, beady eyes, haunches and four paws, a long tail tapering and curving to the ground, two whiskers a side.
 * - Every part is a "hex": 8 corners (back rectangle, front rectangle) with flat-shaded faces, 12 triangles.
 * - Submeshes (match the rat's existing materials): 0 body (grey fur), 1 pink (nose, ear lining, paws, tail),
 *   2 dark (eyes, whiskers).
 * - The ranged rat (same day): Tools > Models > Build Low-Poly Ranged Rat writes Assets/Models/RangedRatLowPoly.asset:
 *   the same style standing up (haunches, upright pear body, forward-looking head), a pistol held in both paws whose
 *   barrel ends at the RangedEnemy prefab's Muzzle (0.44, 1.30, 0.95 from the feet), ~2 tall at the ears. Dark = the
 *   pistol too.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RatModelBuilder
{
    public const string MeshPath = "Assets/Models/RatLowPoly.asset";
    public const string RangedMeshPath = "Assets/Models/RangedRatLowPoly.asset";

    private const int Body = 0;
    private const int Pink = 1;
    private const int Dark = 2;

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles = { new List<int>(), new List<int>(), new List<int>() };

    [MenuItem("Tools/Models/Build Low-Poly Rat")]
    public static void Build()
    {
        Save(BuildMesh(), MeshPath);
    }

    [MenuItem("Tools/Models/Build Low-Poly Ranged Rat")]
    public static void BuildRanged()
    {
        Save(BuildRangedMesh(), RangedMeshPath);
    }

    // Writes the mesh to `path`, rebuilding an existing asset in place (prefabs keep their reference).
    private static void Save(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                existing.SetTriangles(mesh.GetTriangles(i), i);
            }
            existing.RecalculateBounds();
            existing.UploadMeshData(false);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("RatModelBuilder: wrote " + path);
    }

    private static void Begin()
    {
        Vertices.Clear();
        Normals.Clear();
        foreach (List<int> list in Triangles)
        {
            list.Clear();
        }
    }

    private static Mesh Finish(string name)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.subMeshCount = 3;
        for (int i = 0; i < 3; i++)
        {
            mesh.SetTriangles(Triangles[i], i);
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    // The ranged rat: stands up on its haunches, hunched, holding a pistol in both paws. Its barrel ends at the
    // RangedEnemy prefab's Muzzle (0.44, 1.30, 0.95 from the feet), where its bullets start.
    public static Mesh BuildRangedMesh()
    {
        Begin();

        // Haunches with long pink hind feet.
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Body, new Vector3(side * 0.27f, 0.22f, 0.0f), new Vector3(0.24f, 0.44f, 0.46f), Quaternion.Euler(-8f, 0f, 0f));
            Box(Pink, new Vector3(side * 0.27f, 0.03f, 0.24f), new Vector3(0.17f, 0.06f, 0.36f), Quaternion.identity);
        }

        // Upright body: wide belly, narrowing chest, leaning forward a little. Rectangles: (half width, back z, front z).
        VHex(Body, 0.12f, new Vector3(0.36f, -0.34f, 0.24f), 0.75f, new Vector3(0.38f, -0.30f, 0.30f));
        VHex(Body, 0.75f, new Vector3(0.38f, -0.30f, 0.30f), 1.22f, new Vector3(0.28f, -0.20f, 0.26f));
        VHex(Body, 1.22f, new Vector3(0.28f, -0.20f, 0.26f), 1.34f, new Vector3(0.24f, -0.17f, 0.22f));

        // Head looking forward: wedge + snout + nose (Hex rectangles: half width, bottom, top).
        Hex(Body, -0.17f, new Vector3(0.24f, 1.28f, 1.66f), 0.20f, new Vector3(0.18f, 1.32f, 1.60f));
        Hex(Body, 0.20f, new Vector3(0.18f, 1.32f, 1.60f), 0.44f, new Vector3(0.07f, 1.40f, 1.51f));
        Box(Pink, new Vector3(0f, 1.455f, 0.46f), new Vector3(0.10f, 0.08f, 0.06f), Quaternion.identity);

        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion tilt = Quaternion.Euler(-10f, side * 25f, side * -20f);
            Vector3 earPos = new Vector3(side * 0.17f, 1.76f, -0.04f);
            Box(Body, earPos, new Vector3(0.28f, 0.28f, 0.05f), tilt);
            Box(Pink, earPos + tilt * new Vector3(0f, 0f, 0.03f), new Vector3(0.20f, 0.20f, 0.02f), tilt);

            Box(Dark, new Vector3(side * 0.165f, 1.52f, 0.13f), new Vector3(0.06f, 0.07f, 0.07f), Quaternion.identity);

            for (int w = 0; w < 2; w++)
            {
                Quaternion fan = Quaternion.Euler(w == 0 ? -6f : 8f, side * (65f + w * 15f), 0f);
                Vector3 root = new Vector3(side * 0.06f, 1.44f + w * 0.03f, 0.40f);
                Box(Dark, root + fan * new Vector3(0f, 0f, 0.15f), new Vector3(0.014f, 0.014f, 0.30f), fan);
            }
        }

        // Arms: both paws meet on the pistol's grip in front of the right shoulder.
        Vector3 grip = new Vector3(0.44f, 1.17f, 0.60f);
        Segment(Body, new Vector3(0.32f, 1.16f, 0.02f), new Vector3(0.42f, 1.02f, 0.30f), 0.15f, 0.13f);
        Segment(Body, new Vector3(0.42f, 1.02f, 0.30f), grip + new Vector3(0f, -0.02f, -0.06f), 0.13f, 0.11f);
        Segment(Body, new Vector3(-0.32f, 1.16f, 0.02f), new Vector3(-0.12f, 1.00f, 0.34f), 0.15f, 0.13f);
        Segment(Body, new Vector3(-0.12f, 1.00f, 0.34f), grip + new Vector3(-0.08f, -0.04f, -0.02f), 0.13f, 0.11f);
        Box(Pink, grip + new Vector3(0f, -0.02f, -0.04f), new Vector3(0.13f, 0.11f, 0.11f), Quaternion.identity);
        Box(Pink, grip + new Vector3(-0.08f, -0.05f, 0f), new Vector3(0.11f, 0.10f, 0.11f), Quaternion.identity);

        // Pistol: grip, frame and barrel ending at the muzzle.
        Box(Dark, grip + new Vector3(0f, 0.02f, 0.03f), new Vector3(0.08f, 0.18f, 0.09f), Quaternion.Euler(-15f, 0f, 0f));
        Box(Dark, new Vector3(0.44f, 1.29f, 0.70f), new Vector3(0.11f, 0.13f, 0.30f), Quaternion.identity);
        Box(Dark, new Vector3(0.44f, 1.30f, 0.89f), new Vector3(0.07f, 0.07f, 0.12f), Quaternion.identity);

        // Tail from the base of the back, curling along the ground.
        Vector3 start = new Vector3(0f, 0.20f, -0.32f);
        Vector3 dir = Quaternion.Euler(-20f, 0f, 0f) * Vector3.back;
        float thick = 0.10f;
        float[] lengths = { 0.28f, 0.26f, 0.24f, 0.22f, 0.20f };
        for (int i = 0; i < lengths.Length; i++)
        {
            Vector3 next = start + dir * lengths[i];
            if (next.y < 0.04f)
            {
                next.y = 0.04f;
            }
            float end = thick * 0.72f;
            Segment(Pink, start, next, thick, end);
            start = next;
            thick = end;
            dir = Quaternion.Euler(-4f, -18f, 0f) * dir;
            dir.y = Mathf.Min(dir.y, 0f);
            dir.Normalize();
        }

        return Finish("RangedRatLowPoly");
    }

    public static Mesh BuildMesh()
    {
        Begin();

        // Body: rump (high, wide) -> back -> chest (lower, narrower). Rectangles: (half width, bottom, top).
        Hex(Body, -0.48f, new Vector3(0.20f, 0.10f, 0.36f), -0.30f, new Vector3(0.27f, 0.06f, 0.48f));
        Hex(Body, -0.30f, new Vector3(0.27f, 0.06f, 0.48f), -0.05f, new Vector3(0.27f, 0.06f, 0.47f));
        Hex(Body, -0.05f, new Vector3(0.27f, 0.06f, 0.47f), 0.22f, new Vector3(0.21f, 0.08f, 0.38f));
        // Neck + head wedge + snout.
        Hex(Body, 0.22f, new Vector3(0.21f, 0.08f, 0.38f), 0.32f, new Vector3(0.19f, 0.10f, 0.40f));
        Hex(Body, 0.32f, new Vector3(0.19f, 0.10f, 0.40f), 0.56f, new Vector3(0.11f, 0.13f, 0.29f));
        Hex(Body, 0.56f, new Vector3(0.11f, 0.13f, 0.29f), 0.70f, new Vector3(0.05f, 0.15f, 0.23f));
        // Nose.
        Box(Pink, new Vector3(0f, 0.20f, 0.72f), new Vector3(0.09f, 0.07f, 0.06f), Quaternion.identity);

        // Ears: a fur back and a pink lining facing forward, tipped outward.
        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion tilt = Quaternion.Euler(-10f, side * 25f, side * -20f);
            Vector3 earPos = new Vector3(side * 0.13f, 0.48f, 0.34f);
            Box(Body, earPos, new Vector3(0.20f, 0.20f, 0.04f), tilt);
            Box(Pink, earPos + tilt * new Vector3(0f, 0f, 0.025f), new Vector3(0.14f, 0.14f, 0.02f), tilt);

            // Eyes on the sides of the head.
            Box(Dark, new Vector3(side * 0.135f, 0.32f, 0.47f), new Vector3(0.05f, 0.06f, 0.06f), Quaternion.identity);

            // Whiskers: thin dark sticks fanning out from the snout.
            for (int w = 0; w < 2; w++)
            {
                Quaternion fan = Quaternion.Euler(w == 0 ? -6f : 8f, side * (65f + w * 15f), 0f);
                Vector3 root = new Vector3(side * 0.06f, 0.19f + w * 0.03f, 0.64f);
                Box(Dark, root + fan * new Vector3(0f, 0f, 0.13f), new Vector3(0.012f, 0.012f, 0.26f), fan);
            }

            // Haunches (fur) with long pink hind feet, small pink front paws.
            Box(Body, new Vector3(side * 0.25f, 0.15f, -0.28f), new Vector3(0.12f, 0.24f, 0.26f), Quaternion.identity);
            Box(Pink, new Vector3(side * 0.25f, 0.025f, -0.17f), new Vector3(0.11f, 0.05f, 0.22f), Quaternion.identity);
            Box(Pink, new Vector3(side * 0.15f, 0.03f, 0.26f), new Vector3(0.09f, 0.06f, 0.11f), Quaternion.identity);
        }

        // Tail: tapering segments from the rump, curving to one side and down to the ground.
        Vector3 start = new Vector3(0f, 0.22f, -0.46f);
        Vector3 dir = Quaternion.Euler(-15f, 0f, 0f) * Vector3.back;
        float thick = 0.075f;
        float[] lengths = { 0.22f, 0.22f, 0.20f, 0.18f, 0.16f };
        for (int i = 0; i < lengths.Length; i++)
        {
            Vector3 next = start + dir * lengths[i];
            if (next.y < 0.03f)
            {
                next.y = 0.03f;
            }
            float end = thick * 0.72f;
            Segment(Pink, start, next, thick, end);
            start = next;
            thick = end;
            dir = Quaternion.Euler(-4f, 16f, 0f) * dir;
            dir.y = Mathf.Min(dir.y, 0f);
            dir.Normalize();
        }

        return Finish("RatLowPoly");
    }

    // An upright section from y0 to y1: rectangles (half width, back z, front z) at the bottom and the top.
    private static void VHex(int material, float y0, Vector3 bottom, float y1, Vector3 top)
    {
        Vector3[] c =
        {
            new Vector3(-bottom.x, y0, bottom.y), new Vector3(bottom.x, y0, bottom.y),
            new Vector3(bottom.x, y0, bottom.z), new Vector3(-bottom.x, y0, bottom.z),
            new Vector3(-top.x, y1, top.y), new Vector3(top.x, y1, top.y),
            new Vector3(top.x, y1, top.z), new Vector3(-top.x, y1, top.z)
        };
        AddHex(material, c);
    }

    // A body section from z0 to z1: rectangles (half width, bottom y, top y) at each end.
    private static void Hex(int material, float z0, Vector3 back, float z1, Vector3 front)
    {
        Vector3[] c =
        {
            new Vector3(-back.x, back.y, z0), new Vector3(back.x, back.y, z0),
            new Vector3(back.x, back.z, z0), new Vector3(-back.x, back.z, z0),
            new Vector3(-front.x, front.y, z1), new Vector3(front.x, front.y, z1),
            new Vector3(front.x, front.z, z1), new Vector3(-front.x, front.z, z1)
        };
        AddHex(material, c);
    }

    private static void Box(int material, Vector3 centre, Vector3 size, Quaternion rotation)
    {
        Vector3 h = size * 0.5f;
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 local = new Vector3(
                (i == 0 || i == 3 || i == 4 || i == 7) ? -h.x : h.x,
                (i == 0 || i == 1 || i == 4 || i == 5) ? -h.y : h.y,
                i < 4 ? -h.z : h.z);
            c[i] = centre + rotation * local;
        }
        AddHex(material, c);
    }

    // A square-section tube from a to b, thickness tapering from startSize to endSize.
    private static void Segment(int material, Vector3 a, Vector3 b, float startSize, float endSize)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, axis).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.right;
        }
        Vector3 up = Vector3.Cross(axis, right).normalized;
        float s0 = startSize * 0.5f;
        float s1 = endSize * 0.5f;
        Vector3[] c =
        {
            a - right * s0 - up * s0, a + right * s0 - up * s0, a + right * s0 + up * s0, a - right * s0 + up * s0,
            b - right * s1 - up * s1, b + right * s1 - up * s1, b + right * s1 + up * s1, b - right * s1 + up * s1
        };
        AddHex(material, c);
    }

    // Corners 0-3 = back face (bottom-left, bottom-right, top-right, top-left), 4-7 = the same at the front.
    private static readonly int[][] Faces =
    {
        new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 3, 2, 6, 7 },
        new[] { 4, 5, 1, 0 }, new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 }
    };

    private static void AddHex(int material, Vector3[] c)
    {
        Vector3 centre = Vector3.zero;
        foreach (Vector3 p in c)
        {
            centre += p;
        }
        centre /= 8f;

        List<int> tris = Triangles[material];
        foreach (int[] f in Faces)
        {
            Vector3 a = c[f[0]], b = c[f[1]], d = c[f[2]], e = c[f[3]];
            Vector3 normal = Vector3.Cross(b - a, d - a);
            if (normal.sqrMagnitude < 1e-10f)
            {
                normal = Vector3.Cross(d - a, e - a);
            }
            Vector3 faceCentre = (a + b + d + e) * 0.25f;
            bool flip = Vector3.Dot(normal, faceCentre - centre) < 0f;
            normal = (flip ? -normal : normal).normalized;

            int start = Vertices.Count;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(d);
            Vertices.Add(e);
            for (int k = 0; k < 4; k++)
            {
                Normals.Add(normal);
            }
            if (flip)
            {
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
            }
            else
            {
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
            }
        }
    }
}
