/*
 * LitterboxModelBuilder
 * Procedural low-poly model of the litter box tower (tower 4, 2026-11-24, user: "a simple cat litterbox"; it will buff
 * nearby towers and change look per upgrade threshold later). Same faceted style and helpers as FishercatModelBuilder.
 *
 * - Footprint 1x2 (corner tile (0, 0) + (1, 0)): everything is in the tower root's space (root = the corner tile's centre),
 *   centred on (0.5, 0); it may spill a little past the two tiles (user). No stand: it sits on the ground.
 * - The look (user's pick of three, 2026-11-24: A): an open plastic tray (teal) with litter, a few clumps and a scoop
 *   leaning on the rim. Tools > Models > Turret > Build Litter Box writes Models/LitterboxTray.asset.
 * - Submeshes: 0 plastic, 1 plastic rim, 2 litter, 3 clumps, 4 scoop, 5 dark.
 * - Units: world units, bottom on y 0, the front is +Z.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LitterboxModelBuilder
{
    public const int Plastic = 0, Rim = 1, Litter = 2, Clump = 3, Scoop = 4, Dark = 5, Submeshes = 6;
    public static readonly Vector3 Centre = new Vector3(0.5f, 0f, 0f);
    public static readonly Vector3 TraySize = new Vector3(1.9f, 0.34f, 1.05f);

    public const string TrayPath = "Assets/Models/LitterboxTray.asset";

    /// <summary>Writes the litter box mesh in place (user's pick 2026-11-24: A, the open tray with a scoop).</summary>
    [MenuItem("Tools/Models/Turret/Build Litter Box")]
    public static void BuildLitterbox()
    {
        Save(BuildTray(), TrayPath);
    }

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles = new List<int>[Submeshes];

    /// <summary>The open tray with litter, clumps and a scoop on the rim, in the tower root's space.</summary>
    public static Mesh BuildTray()
    {
        Begin();
        Tray(TraySize.y);
        Clumps(7, TraySize.y);
        ScoopOnRim(TraySize.y);
        return Finish("LitterboxTray");
    }

    // An open plastic tray: four walls with a thicker rim, a flat litter fill.
    private static void Tray(float height)
    {
        Vector3 s = new Vector3(TraySize.x, height, TraySize.z);
        float wall = 0.07f;
        // Floor and walls (slightly sloped outward look via a wider rim).
        Box(Plastic, Centre + new Vector3(0f, 0.03f, 0f), new Vector3(s.x - 0.06f, 0.06f, s.z - 0.06f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Plastic, Centre + new Vector3(side * (s.x * 0.5f - wall * 0.5f), height * 0.5f, 0f), new Vector3(wall, height, s.z), Quaternion.identity);
            Box(Plastic, Centre + new Vector3(0f, height * 0.5f, side * (s.z * 0.5f - wall * 0.5f)), new Vector3(s.x, height, wall), Quaternion.identity);
            Box(Rim, Centre + new Vector3(side * (s.x * 0.5f), height - 0.02f, 0f), new Vector3(wall + 0.05f, 0.05f, s.z + 0.05f), Quaternion.identity);
            Box(Rim, Centre + new Vector3(0f, height - 0.02f, side * (s.z * 0.5f)), new Vector3(s.x + 0.05f, 0.05f, wall + 0.05f), Quaternion.identity);
        }
        // Litter fill, a little lumpy.
        Box(Litter, Centre + new Vector3(0f, height - 0.08f, 0f), new Vector3(s.x - wall * 2f, 0.06f, s.z - wall * 2f), Quaternion.identity);
        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(-s.x * 0.38f, s.x * 0.38f, Hash(i, 1));
            float z = Mathf.Lerp(-s.z * 0.32f, s.z * 0.32f, Hash(i, 2));
            Ball(Litter, Centre + new Vector3(x, height - 0.06f, z), new Vector3(0.22f, 0.04f, 0.18f), 6, false);
        }
    }

    private static void Clumps(int count, float height)
    {
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(-TraySize.x * 0.36f, TraySize.x * 0.36f, Hash(i, 7));
            float z = Mathf.Lerp(-TraySize.z * 0.28f, TraySize.z * 0.28f, Hash(i, 8));
            Ball(Clump, Centre + new Vector3(x, height - 0.03f, z), new Vector3(0.07f, 0.045f, 0.06f), 5, true);
        }
    }

    // A scoop leaning on the east rim: handle + slotted head.
    private static void ScoopOnRim(float height)
    {
        Vector3 rimPoint = Centre + new Vector3(TraySize.x * 0.5f - 0.1f, height, 0.15f);
        Vector3 handleEnd = rimPoint + new Vector3(0.38f, 0.32f, 0.12f);
        Segment(Scoop, rimPoint, handleEnd, 0.06f, 0.06f);
        Quaternion head = Quaternion.Euler(-20f, 25f, 0f);
        Vector3 headCentre = rimPoint + new Vector3(-0.17f, -0.07f, -0.06f);
        Box(Scoop, headCentre, new Vector3(0.28f, 0.03f, 0.22f), head);
        for (int i = -1; i <= 1; i++)
        {
            Box(Dark, headCentre + head * new Vector3(i * 0.07f, 0.018f, 0f), new Vector3(0.025f, 0.01f, 0.15f), head);
        }
    }

    private static float Hash(int i, int salt)
    {
        int h = i * 374761393 + salt * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xffff) / 65535f;
    }

    // ---- Helpers (same shapes as FishercatModelBuilder).

    private static void Begin()
    {
        Vertices.Clear();
        Normals.Clear();
        for (int i = 0; i < Triangles.Length; i++)
        {
            if (Triangles[i] == null)
            {
                Triangles[i] = new List<int>();
            }
            Triangles[i].Clear();
        }
    }

    private static Mesh Finish(string name)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.subMeshCount = Submeshes;
        for (int i = 0; i < Submeshes; i++)
        {
            mesh.SetTriangles(Triangles[i], i);
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    public static void Save(Mesh mesh, string path)
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
        Debug.Log("LitterboxModelBuilder: wrote " + path);
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
        int[][] faces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 3, 2, 6, 7 },
            new[] { 4, 5, 1, 0 }, new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 }
        };
        foreach (int[] f in faces)
        {
            AddPoly(material, new[] { c[f[0]], c[f[1]], c[f[2]], c[f[3]] }, centre);
        }
    }

    private static void Frustum(int material, Vector3 c, Quaternion rot, float r0, float r1, float h, int n)
    {
        Frustum(material, c, rot, new Vector2(r0, r0), new Vector2(r1, r1), h, n);
    }

    private static void Frustum(int material, Vector3 c, Quaternion rot, Vector2 r0, Vector2 r1, float h, int n)
    {
        Vector3[] bottom = new Vector3[n];
        Vector3[] top = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / n;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            bottom[i] = c + rot * new Vector3(cs * r0.x, 0f, sn * r0.y);
            top[i] = c + rot * new Vector3(cs * r1.x, h, sn * r1.y);
        }
        Vector3 centre = c + rot * new Vector3(0f, h * 0.5f, 0f);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            AddPoly(material, new[] { bottom[i], bottom[j], top[j], top[i] }, centre);
        }
        if (r1.x > 0.01f)
        {
            AddPoly(material, top, centre);
        }
        if (r0.x > 0.01f)
        {
            AddPoly(material, bottom, centre);
        }
    }

    private static void Ball(int material, Vector3 centre, Vector3 radii, int sides, bool whole)
    {
        float[] t = whole ? new[] { -1f, -0.7f, -0.25f, 0.25f, 0.7f, 1f } : new[] { -0.25f, 0.25f, 0.7f, 1f };
        for (int i = 0; i < t.Length - 1; i++)
        {
            float k0 = Mathf.Sqrt(Mathf.Max(0f, 1f - t[i] * t[i]));
            float k1 = Mathf.Sqrt(Mathf.Max(0f, 1f - t[i + 1] * t[i + 1]));
            Vector3 bottom = centre + new Vector3(0f, t[i] * radii.y, 0f);
            Frustum(material, bottom, Quaternion.identity, new Vector2(radii.x * k0, radii.z * k0),
                new Vector2(radii.x * k1, radii.z * k1), (t[i + 1] - t[i]) * radii.y, sides);
        }
    }

    private static void Segment(int material, Vector3 a, Vector3 b, float startSize, float endSize)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        right = right.sqrMagnitude < 0.001f ? Vector3.right : right.normalized;
        Vector3 up = Vector3.Cross(axis, right).normalized;
        float s0 = startSize * 0.5f, s1 = endSize * 0.5f;
        Vector3[] c =
        {
            a - right * s0 - up * s0, a + right * s0 - up * s0, a + right * s0 + up * s0, a - right * s0 + up * s0,
            b - right * s1 - up * s1, b + right * s1 - up * s1, b + right * s1 + up * s1, b - right * s1 + up * s1
        };
        Vector3 mid = (a + b) * 0.5f;
        int[][] faces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 3, 2, 6, 7 },
            new[] { 4, 5, 1, 0 }, new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 }
        };
        foreach (int[] f in faces)
        {
            AddPoly(material, new[] { c[f[0]], c[f[1]], c[f[2]], c[f[3]] }, mid);
        }
    }

    private static void AddPoly(int material, Vector3[] p, Vector3 inside)
    {
        Vector3 normal = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        if (normal.sqrMagnitude < 1e-12f && p.Length > 3)
        {
            normal = Vector3.Cross(p[2] - p[0], p[3] - p[0]);
        }
        Vector3 faceCentre = Vector3.zero;
        foreach (Vector3 v in p)
        {
            faceCentre += v;
        }
        faceCentre /= p.Length;
        bool flip = Vector3.Dot(normal, faceCentre - inside) < 0f;
        normal = (flip ? -normal : normal).normalized;
        int start = Vertices.Count;
        foreach (Vector3 v in p)
        {
            Vertices.Add(v);
            Normals.Add(normal);
        }
        List<int> tris = Triangles[material];
        for (int k = 1; k < p.Length - 1; k++)
        {
            tris.Add(start);
            tris.Add(start + (flip ? k + 1 : k));
            tris.Add(start + (flip ? k : k + 1));
        }
    }
}
