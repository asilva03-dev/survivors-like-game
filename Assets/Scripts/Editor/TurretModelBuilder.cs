/*
 * TurretModelBuilder
 * Procedural low-poly model of the gun turret (2026-11-20). User: the old catturret model was too long and goofy; keep
 * its main idea. Picked after three rounds of renders: "cat in a hatch", made to look like a real turret with one barrel,
 * the sloped tank-turret take (A). Tools > Models > Turret > Build Gun Turret writes Models/GunTurretBase.asset and
 * Models/GunTurretBody.asset in place (prefabs keep their links); change the numbers here and rebuild, never hand-edit.
 *
 * - Base (static, feet on y 0, fits inside one 1x1 tile): bolted square pad, foot plate, a thick post with a collar and a
 *   bearing ring under the body.
 * - Body (the turning part, in the Gun pivot's space: pivot at y 0, barrel toward +Z; GunTurret.prefab's Gun sits
 *   PivotHeight above the ground): a sloped hull with a steep front, a rounded mantlet, a rear storage box, rivets, side
 *   vents, one barrel (breech, band, tube, muzzle brake; tip at MuzzleZ), a round hatch with its lid flipped open and the
 *   round yellow cat looking out with its paws on the rim, the old model's Y-sight, an ammo box with a brass belt.
 * - Submeshes: 0 main (armour), 1 accent (trim, mantlet), 2 dark (barrel, rivets, sight, ammo box), 3 cat (yellow),
 *   4 brass (ammo belt, cat ears). Materials in Materials/Models/gunturret/.
 * - Double barrel (2026-11-23, Fire Rate T3 "Double Barreled", user's pick B): the same build writes
 *   Models/GunTurretBodyDouble.asset: the body with two barrels side by side at +-TwinX (matching the 0.3 bullet spacing at
 *   the prefab's 1.35 scale), each with its own muzzle brake, held together by two bands, in a wider mantlet. The upgrade
 *   tier swaps the turret's body mesh to it (TurretUpgrade.bodyMesh).
 * - Flat-shaded quads/polygons like RatModelBuilder (its helpers are private; these are the same shapes).
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TurretModelBuilder
{
    public const string BasePath = "Assets/Models/GunTurretBase.asset";
    public const string BodyPath = "Assets/Models/GunTurretBody.asset";
    public const string DoubleBodyPath = "Assets/Models/GunTurretBodyDouble.asset";
    public const float PivotHeight = 1.56f;
    /// <summary>How far in front of the pivot the barrel ends (GunStats muzzleOffset should match).</summary>
    public const float MuzzleZ = 0.99f;

    private const int Main = 0;
    private const int Accent = 1;
    private const int Dark = 2;
    private const int Cat = 3;
    private const int CatAccent = 4;
    private const int SubmeshCount = 5;

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles =
    {
        new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>()
    };

    [MenuItem("Tools/Models/Turret/Build Gun Turret")]
    public static void BuildGunTurret()
    {
        Save(BuildBase(), BasePath);
        Save(BuildBody(), BodyPath);
        Save(BuildBody(true), DoubleBodyPath);
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
        Debug.Log("TurretModelBuilder: wrote " + path);
    }

    public static Mesh BuildBase()
    {
        Begin();
        Box(Main, new Vector3(0f, 0.04f, 0f), new Vector3(0.92f, 0.08f, 0.92f), Quaternion.identity);
        Prism(Accent, new Vector3(0f, 0.08f, 0f), new Vector2(0.62f, 0.62f), new Vector2(0.44f, 0.44f), 0.10f, Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                Box(Dark, new Vector3(x * 0.38f, 0.095f, z * 0.38f), new Vector3(0.07f, 0.035f, 0.07f), Quaternion.identity);
            }
        }
        float postTop = PivotHeight - 0.34f;
        Frustum(Accent, new Vector3(0f, 0.18f, 0f), Quaternion.identity, 0.15f, 0.12f, postTop - 0.18f, 8);
        Frustum(Dark, new Vector3(0f, 0.55f, 0f), Quaternion.identity, 0.16f, 0.16f, 0.06f, 8);
        Frustum(Dark, new Vector3(0f, postTop, 0f), Quaternion.identity, 0.30f, 0.30f, 0.05f, 12);
        return Finish("GunTurretBase");
    }

    public static Mesh BuildBody()
    {
        return BuildBody(false);
    }

    // Barrels sit at +-TwinX on the double body (matches the 0.3 bullet spacing at the prefab's 1.35 scale).
    private const float TwinX = 0.111f;

    private static Mesh BuildBody(bool twin)
    {
        Begin();
        Vector3 hatch = new Vector3(0f, 0.2f, -0.12f);
        float barrelY = -0.02f;

        // Sloped hull narrowing to the top with a steep front, trim at the bottom, a rear storage box, the mantlet, rivets.
        Prism(Main, new Vector3(0f, -0.28f, -0.02f), new Vector2(0.80f, 0.92f), new Vector2(0.62f, 0.62f), 0.46f, new Vector3(0f, 0f, -0.12f));
        Prism(Accent, new Vector3(0f, -0.30f, -0.02f), new Vector2(0.84f, 0.96f), new Vector2(0.80f, 0.92f), 0.04f, Vector3.zero);
        Box(Main, new Vector3(0f, -0.08f, -0.56f), new Vector3(0.54f, 0.26f, 0.18f), Quaternion.identity);
        Box(Dark, new Vector3(0f, -0.08f, -0.66f), new Vector3(0.44f, 0.16f, 0.03f), Quaternion.identity);
        if (!twin)
        {
            Frustum(Accent, new Vector3(0f, barrelY, 0.30f), Quaternion.Euler(90f, 0f, 0f), 0.17f, 0.15f, 0.20f, 8);
        }
        Rivets(0.28f, 0.17f, -0.40f, 0.12f, 4);

        if (!twin)
        {
            // Single barrel: a thicker breech, the tube, a band and a muzzle brake with side ports (tip at MuzzleZ).
            Barrel(0f, barrelY);
        }
        else
        {
            // Twin barrels in a wider mantlet, each with its own brake, clamped together by two bands.
            Frustum(Accent, new Vector3(0f, barrelY, 0.30f), Quaternion.Euler(90f, 0f, 0f), new Vector2(0.27f, 0.17f), new Vector2(0.25f, 0.15f), 0.20f, 8);
            Barrel(-TwinX, barrelY);
            Barrel(TwinX, barrelY);
            for (int i = 0; i < 2; i++)
            {
                Box(Accent, new Vector3(0f, barrelY, 0.62f + i * 0.16f), new Vector3(2f * TwinX + 0.15f, 0.13f, 0.04f), Quaternion.identity);
            }
        }

        // Round hatch: rim, the lid flipped open behind it, the cat looking out with its paws on the rim.
        Frustum(Dark, hatch, Quaternion.identity, 0.27f, 0.26f, 0.05f, 12);
        Vector3 hinge = hatch + new Vector3(0f, 0.05f, -0.26f);
        Frustum(Main, hinge + new Vector3(0f, 0.25f, -0.08f), Quaternion.Euler(-15f, 0f, 0f) * Quaternion.Euler(90f, 0f, 0f), 0.25f, 0.25f, 0.035f, 12);
        Box(Dark, hinge, new Vector3(0.14f, 0.05f, 0.05f), Quaternion.identity);
        Gunner(hatch + new Vector3(0f, 0.17f, 0.02f), 0.24f, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Ball(Cat, hatch + new Vector3(side * 0.13f, 0.07f, 0.20f), new Vector3(0.06f, 0.045f, 0.07f), 6, true);
        }

        // The old model's Y-shaped sight on the top front-right.
        Sight(new Vector3(0.26f, 0.17f, 0.06f));

        // Ammo box on the left with a lid, and a brass belt curving up into the hull.
        Box(Dark, new Vector3(-0.50f, -0.12f, -0.02f), new Vector3(0.18f, 0.26f, 0.38f), Quaternion.identity);
        Box(Accent, new Vector3(-0.50f, 0.02f, -0.02f), new Vector3(0.20f, 0.03f, 0.40f), Quaternion.identity);
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            Vector3 p = new Vector3(Mathf.Lerp(-0.50f, -0.36f, t), 0.06f + Mathf.Sin(t * Mathf.PI) * 0.07f, Mathf.Lerp(0.10f, 0.20f, t));
            Box(CatAccent, p, new Vector3(0.05f, 0.03f, 0.08f), Quaternion.Euler(0f, 0f, Mathf.Lerp(30f, -20f, t)));
        }

        // Three vent slats on the right side.
        for (int i = 0; i < 3; i++)
        {
            Box(Dark, new Vector3(0.36f, -0.10f, -0.20f + i * 0.12f), new Vector3(0.02f, 0.16f, 0.05f), Quaternion.Euler(0f, 0f, 12f));
        }
        return Finish(twin ? "GunTurretBodyDouble" : "GunTurretBody");
    }

    // One barrel at x: breech, tube, band and a muzzle brake with side ports, tip at MuzzleZ.
    private static void Barrel(float x, float y)
    {
        Frustum(Dark, new Vector3(x, y, 0.46f), Quaternion.Euler(90f, 0f, 0f), 0.09f, 0.08f, 0.14f, 8);
        Frustum(Dark, new Vector3(x, y, 0.58f), Quaternion.Euler(90f, 0f, 0f), 0.065f, 0.06f, 0.30f, 8);
        Frustum(Accent, new Vector3(x, y, 0.68f), Quaternion.Euler(90f, 0f, 0f), 0.08f, 0.08f, 0.04f, 8);
        Box(Dark, new Vector3(x, y, MuzzleZ - 0.07f), new Vector3(0.17f, 0.12f, 0.14f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Accent, new Vector3(x + side * 0.088f, y, MuzzleZ - 0.07f), new Vector3(0.01f, 0.07f, 0.08f), Quaternion.identity);
        }
    }

    // Rows of dark rivets along both top edges (x = half width, y height, from z0 to z1).
    private static void Rivets(float x, float y, float z0, float z1, int count)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(z0, z1, count == 1 ? 0.5f : i / (float)(count - 1));
                Box(Dark, new Vector3(side * x, y, z), new Vector3(0.035f, 0.025f, 0.035f), Quaternion.identity);
            }
        }
    }

    // A box whose top rectangle is smaller (and shifted by topOffset): bottom centre c, sizes (x, z), height h.
    private static void Prism(int material, Vector3 c, Vector2 bottom, Vector2 top, float h, Vector3 topOffset)
    {
        Vector3 t = c + new Vector3(0f, h, 0f) + topOffset;
        Vector3[] p =
        {
            c + new Vector3(-bottom.x, 0f, -bottom.y) * 0.5f, c + new Vector3(bottom.x, 0f, -bottom.y) * 0.5f,
            c + new Vector3(bottom.x, 0f, bottom.y) * 0.5f, c + new Vector3(-bottom.x, 0f, bottom.y) * 0.5f,
            t + new Vector3(-top.x, 0f, -top.y) * 0.5f, t + new Vector3(top.x, 0f, -top.y) * 0.5f,
            t + new Vector3(top.x, 0f, top.y) * 0.5f, t + new Vector3(-top.x, 0f, top.y) * 0.5f
        };
        Vector3 mid = (c + t) * 0.5f;
        AddPoly(material, new[] { p[0], p[1], p[2], p[3] }, mid);
        AddPoly(material, new[] { p[4], p[5], p[6], p[7] }, mid);
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            AddPoly(material, new[] { p[i], p[j], p[j + 4], p[i + 4] }, mid);
        }
    }

    // The Y-shaped sight on top (dark fork with a small accent pin).
    private static void Sight(Vector3 bottom)
    {
        Box(Dark, bottom + new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.16f, 0.05f), Quaternion.identity);
        Segment(Dark, bottom + new Vector3(0f, 0.15f, 0f), bottom + new Vector3(-0.09f, 0.30f, 0f), 0.045f, 0.04f);
        Segment(Dark, bottom + new Vector3(0f, 0.15f, 0f), bottom + new Vector3(0.09f, 0.30f, 0f), 0.045f, 0.04f);
    }

    // The round yellow cat (faceted ball, ears, eyes) facing +Z. sunk = false hides the lower half in a hatch.
    private static void Gunner(Vector3 centre, float r, bool whole)
    {
        Ball(Cat, centre, new Vector3(r, r * 0.95f, r), 10, whole);
        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion tilt = Quaternion.Euler(0f, 0f, side * -22f);
            Vector3 ear = centre + new Vector3(side * r * 0.55f, r * 0.78f, -r * 0.05f);
            Frustum(CatAccent, ear, tilt, r * 0.28f, 0.005f, r * 0.55f, 4);
            Box(Dark, centre + new Vector3(side * r * 0.36f, r * 0.18f, r * 0.9f), new Vector3(r * 0.14f, r * 0.2f, r * 0.12f), Quaternion.identity);
        }
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
        mesh.subMeshCount = SubmeshCount;
        for (int i = 0; i < SubmeshCount; i++)
        {
            mesh.SetTriangles(Triangles[i], i);
        }
        mesh.RecalculateBounds();
        return mesh;
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

    // n-sided frustum: bottom centre c, radius r0 at the bottom and r1 at the top, height h along rot's up, capped.
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

    // Faceted ellipsoid from stacked frustums; whole = false keeps only the upper part (for a head out of a hatch).
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

    // Square-section tube from a to b, tapering from startSize to endSize.
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

    // A flat polygon (fan) facing away from `inside`.
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
