/*
 * AngelcatModelBuilder
 * Procedural low-poly model of the Angel Cat tower (tower 6, 2026-11-25, user's sketch): a round cat with four-point star
 * eyes, a halo, two feathered wings and a bow. It sits on a cloud when placed, then takes off and flies a circle around
 * the base. Same faceted style and helpers as SwordscatModelBuilder.
 *
 * - Footprint 2x2 (corner tile (0, 0) + (1, 0), (0, -1), (1, -1)) like the Fishercat: the cloud is in the tower root's
 *   space, centred on the footprint's middle (0.5, -0.5), its top at CloudTop (fence level), where the cat sits (CatSpot).
 * - The cat (BuildCat), wings (BuildWing, one per side), halo (BuildHalo) and bow (BuildBow) are separate meshes in the
 *   cat's space (feet at the origin, facing +Z, before CatScale) so they can be animated. Bow space: the grip at the
 *   origin, limbs along +-Y, the belly toward +Z, the string behind it. The arrow (BuildArrow) is in world units, along
 *   +Z, tip at +ArrowLength/2.
 * - Look (user's pick C of three, 2026-11-25; A and B removed): pale blue-grey fur, gold star eyes, a pale belly, small
 *   round cherub wings, a wide flat pale-gold halo, a white bow with gold trim held upright at the cat's left side.
 *   GetPose holds the part spots in the cat's space for the prefab. Tools > Models > Turret > Build Angel Cat writes the
 *   meshes to Assets/Models/Angelcat*.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AngelcatModelBuilder
{
    public const float CloudTop = 1.5f;
    public static readonly Vector3 Centre = new Vector3(0.5f, 0f, -0.5f);
    public static readonly Vector3 CatSpot = new Vector3(0.5f, CloudTop - 0.06f, -0.5f);
    public const float CatScale = 1.6f;
    public const float ArrowLength = 0.9f;
    public const float R = 0.38f;

    // Cat submeshes.
    public const int Fur = 0, FurAccent = 1, Dark = 2, Shine = 3, Eye = 4, CatSubmeshes = 5;
    // Wing submeshes.
    public const int Feather = 0, FeatherShade = 1, WingSubmeshes = 2;
    // Bow submeshes, shared by the arrow (shaft = Wood, head = Trim, vanes = Fletch; the arrow leaves BowString empty).
    public const int Wood = 0, Trim = 1, BowString = 2, Fletch = 3, BowSubmeshes = 4;
    // Cloud submeshes.
    public const int Puff = 0, PuffShade = 1, CloudSubmeshes = 2;

    /// <summary>Where the wing roots sit on the body (cat space), per side (x mirrored).</summary>
    public static Vector3 Shoulder(int side)
    {
        return new Vector3(side * R * 0.62f, R * 1.15f, -R * 0.55f);
    }

    /// <summary>Centre of the round body (cat space).</summary>
    public static Vector3 BodyCentre => new Vector3(0f, R * 0.95f, 0f);

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles = new List<int>[8];

    /// <summary>The picked look's pose in the cat's space (feet at the origin, facing +Z, before CatScale).</summary>
    public struct Pose
    {
        public Vector3 wingSpot;     // right wing root (left = x mirrored)
        public Vector3 wingEuler;    // right wing rotation (left = y and z negated)
        public Vector3 haloSpot;
        public Vector3 haloEuler;
        public Vector3 bowSpot;      // the bow's grip
        public Vector3 bowEuler;
        public Vector3 pawA;         // on the grip
        public Vector3 pawB;         // the free paw
    }

    /// <summary>Look C (2026-11-25): wings swept back 25 deg and tipped up 8, the halo tilted back 14 deg above the ears,
    /// the bow upright at the cat's left (+X) side, tilted 12 deg.</summary>
    public static Pose GetPose()
    {
        Pose p = new Pose();
        p.wingSpot = Shoulder(1);
        p.wingEuler = new Vector3(0f, 25f, 8f);
        p.haloSpot = new Vector3(0f, 1.0f, -0.05f);
        p.haloEuler = new Vector3(-14f, 0f, 0f);
        p.bowSpot = new Vector3(R * 1.05f, R * 0.95f, R * 0.35f);
        p.bowEuler = new Vector3(0f, 0f, 12f);
        p.pawA = p.bowSpot - new Vector3(R * 0.12f, 0f, 0f);
        p.pawB = new Vector3(-R * 0.75f, R * 0.5f, R * 0.65f);
        return p;
    }

    public const string BodyPath = "Assets/Models/AngelcatBody.asset";
    public const string HandPath = "Assets/Models/AngelcatHand.asset";
    public const string WingLPath = "Assets/Models/AngelcatWingL.asset";
    public const string WingRPath = "Assets/Models/AngelcatWingR.asset";
    public const string HaloPath = "Assets/Models/AngelcatHalo.asset";
    public const string BowPath = "Assets/Models/AngelcatBow.asset";
    public const string ArrowPath = "Assets/Models/AngelcatArrow.asset";
    public const string CloudPath = "Assets/Models/AngelcatCloud.asset";

    /// <summary>Writes every Angel Cat mesh in place (look C).</summary>
    [MenuItem("Tools/Models/Turret/Build Angel Cat")]
    public static void BuildAngelcat()
    {
        Save(BuildCat(), BodyPath);
        Save(BuildHand(), HandPath);
        Save(BuildWing(-1), WingLPath);
        Save(BuildWing(1), WingRPath);
        Save(BuildHalo(), HaloPath);
        Save(BuildBow(), BowPath);
        Save(BuildArrow(), ArrowPath);
        Save(BuildCloud(), CloudPath);
    }

    // ---- The cat: one round body (head and body in one, like the sketch), ears, gold star eyes, a "w" mouth with
    // cheek strokes, a pale belly patch, little feet and a tail stub.

    public static Mesh BuildCat()
    {
        Begin();
        float r = R;
        Vector3 c = BodyCentre;
        Vector3 radii = new Vector3(r * 1.04f, r * 0.95f, r * 0.96f);
        Ball(Fur, c, radii, 12, true);
        Ball(FurAccent, c + new Vector3(0f, -r * 0.42f, r * 0.5f), new Vector3(r * 0.55f, r * 0.42f, r * 0.45f), 10, true);
        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion tilt = Quaternion.Euler(0f, 0f, side * -20f);
            Vector3 ear = c + new Vector3(side * r * 0.56f, r * 0.62f, -r * 0.05f);
            Frustum(Fur, ear, tilt, r * 0.3f, 0.005f, r * 0.62f, 4);
            Frustum(FurAccent, ear + new Vector3(side * -0.012f, r * 0.12f, r * 0.13f), tilt, r * 0.16f, 0.005f, r * 0.4f, 4);

            // Four-point star eye, a tiny white sparkle beside it, a short brow stroke above.
            Vector3 eyeDir = new Vector3(side * 0.36f, 0.2f, 0.91f).normalized;
            Vector3 eye = Surface(c, radii, eyeDir) + eyeDir * 0.004f;
            float star = r * 0.27f;
            Star(Eye, eye, eyeDir, star, star * 0.3f, 0.02f);
            Vector3 sparkDir = new Vector3(side * 0.24f, 0.34f, 0.91f).normalized;
            Ball(Shine, Surface(c, radii, sparkDir), new Vector3(r * 0.05f, r * 0.05f, r * 0.03f), 6, true);
            Vector3 browDir = new Vector3(side * 0.38f, 0.5f, 0.78f).normalized;
            Vector3 brow = Surface(c, radii, browDir);
            Segment(Dark, brow - new Vector3(side * r * 0.12f, -r * 0.02f, 0f), brow + new Vector3(side * r * 0.1f, r * 0.03f, -r * 0.03f), r * 0.035f, r * 0.03f);

            // Cheek puff under the eye with three short strokes (the sketch's cheek lines).
            Vector3 puffDir = new Vector3(side * 0.16f, -0.12f, 0.98f).normalized;
            Ball(FurAccent, Surface(c, radii, puffDir) - puffDir * 0.03f, new Vector3(r * 0.19f, r * 0.13f, r * 0.12f), 6, true);
            for (int k = 0; k < 3; k++)
            {
                Vector3 d = new Vector3(side * (0.62f + k * 0.06f), -0.08f + k * 0.03f, 0.77f - k * 0.06f).normalized;
                Vector3 p = Surface(c, radii, d) + d * 0.004f;
                Segment(Dark, p + new Vector3(0f, r * 0.07f, 0f), p - new Vector3(0f, r * 0.07f, 0f), r * 0.03f, r * 0.03f);
            }
            // Little feet.
            Ball(FurAccent, new Vector3(side * r * 0.42f, r * 0.08f, r * 0.62f), new Vector3(r * 0.2f, r * 0.12f, r * 0.24f), 6, true);
        }
        // Nose and the "w" mouth between the cheek puffs.
        Vector3 nose = Surface(c, radii, new Vector3(0f, -0.02f, 1f)) + new Vector3(0f, 0f, r * 0.08f);
        Box(Dark, nose, new Vector3(r * 0.1f, r * 0.06f, r * 0.05f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 a = nose + new Vector3(0f, -r * 0.05f, 0f);
            Vector3 m = nose + new Vector3(side * r * 0.1f, -r * 0.14f, -r * 0.01f);
            Vector3 e = nose + new Vector3(side * r * 0.2f, -r * 0.06f, -r * 0.03f);
            Segment(Dark, a, m, r * 0.03f, r * 0.03f);
            Segment(Dark, m, e, r * 0.03f, r * 0.03f);
        }
        // Tail stub curling up at the back.
        Segment(Fur, c + new Vector3(r * 0.2f, -r * 0.7f, -r * 0.85f), c + new Vector3(r * 0.45f, -r * 0.35f, -r * 1.2f), r * 0.2f, r * 0.14f);
        return Finish("AngelcatBody", CatSubmeshes);
    }

    /// <summary>One round paw (world units, centred; not scaled with the cat).</summary>
    public static Mesh BuildHand()
    {
        Begin();
        Ball(FurAccent, Vector3.zero, new Vector3(0.1f, 0.085f, 0.11f), 8, true);
        return Finish("AngelcatHand", CatSubmeshes);
    }

    // ---- Wings: flat feathers in the XY plane (facing +Z), the shoulder at the origin, reaching out along side * X:
    // a small round cherub wing (a covert slab over the arm, three blunt feathers below it).

    public static Mesh BuildWing(int side)
    {
        Begin();
        float t = 0.035f;
        Vector3 n = Vector3.forward;
        Flat(Feather, Mirror(side, new[] { new Vector3(0f, 0.06f, 0f), new Vector3(0.26f, 0.22f, 0f), new Vector3(0.54f, 0.3f, 0f),
            new Vector3(0.58f, 0.18f, 0f), new Vector3(0.3f, -0.02f, 0f), new Vector3(0.03f, -0.07f, 0f) }), n, t);
        FeatherRow(side, new[] { new Vector3(0.44f, 0.22f, 0f), new Vector3(0.28f, 0.12f, 0f), new Vector3(0.12f, 0.03f, 0f) },
            new[] { new Vector3(0.66f, 0.02f, 0f), new Vector3(0.48f, -0.14f, 0f), new Vector3(0.26f, -0.2f, 0f) },
            new[] { 0.28f, 0.28f, 0.24f }, t, 0.6f);
        return Finish(side > 0 ? "AngelcatWingR" : "AngelcatWingL", WingSubmeshes);
    }

    // A row of feathers from roots to tips, alternating shades, each a little behind the previous so they overlap.
    private static void FeatherRow(int side, Vector3[] roots, Vector3[] tips, float[] widths, float t, float round)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            Vector3 back = new Vector3(0f, 0f, -0.012f * (i + 1));
            FeatherBlade(i % 2 == 0 ? Feather : FeatherShade, Mirror(side, roots[i]) + back, Mirror(side, tips[i]) + back, widths[i], Vector3.forward, t, round);
        }
    }

    private static Vector3 Mirror(int side, Vector3 p)
    {
        return new Vector3(p.x * side, p.y, p.z);
    }

    private static Vector3[] Mirror(int side, Vector3[] p)
    {
        Vector3[] o = new Vector3[p.Length];
        for (int i = 0; i < p.Length; i++)
        {
            o[i] = Mirror(side, p[i]);
        }
        return o;
    }

    /// <summary>A flat feather from root to tip; round 0 = pointed tip, 1 = blunt rounded tip.</summary>
    private static void FeatherBlade(int material, Vector3 root, Vector3 tip, float width, Vector3 normal, float t, float round)
    {
        Vector3 a = tip - root;
        Vector3 s = Vector3.Cross(normal, a).normalized;
        float w = width * 0.5f;
        Vector3[] outline =
        {
            root + s * w * 0.35f,
            root + a * 0.5f + s * w,
            root + a * (0.82f + round * 0.08f) + s * w * (0.55f + round * 0.4f),
            root + a * (0.97f - round * 0.02f) + s * w * round * 0.45f,
            tip,
            root + a * (0.97f - round * 0.02f) - s * w * round * 0.45f,
            root + a * (0.82f + round * 0.08f) - s * w * (0.55f + round * 0.4f),
            root + a * 0.5f - s * w,
            root - s * w * 0.35f
        };
        Flat(material, outline, normal, t);
    }

    // ---- Halo: a wide flat ring in the XZ plane, centred on the origin.

    public static Mesh BuildHalo()
    {
        Begin();
        Torus(0, Vector3.zero, 0.27f, new Vector2(0.05f, 0.022f), 20, 6);
        return Finish("AngelcatHalo", 1);
    }

    // ---- Bow: white limbs (Wood slot) with gold nocks, a small gold wing flourish near each tip and a gold star on the grip.

    public static Mesh BuildBow()
    {
        Begin();
        float h = 0.44f;
        float depth = 0.16f;
        int steps = 7;
        int limb = Wood;
        Vector3[] top = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float y = h * i / steps;
            float u = y / h;
            top[i] = new Vector3(0f, y, -depth * u * u);
        }
        for (int sign = -1; sign <= 1; sign += 2)
        {
            for (int i = 0; i < steps; i++)
            {
                Vector3 a = Vector3.Scale(top[i], new Vector3(1f, sign, 1f));
                Vector3 b = Vector3.Scale(top[i + 1], new Vector3(1f, sign, 1f));
                float s0 = Mathf.Lerp(0.05f, 0.022f, (float)i / steps);
                float s1 = Mathf.Lerp(0.05f, 0.022f, (float)(i + 1) / steps);
                Segment(limb, a, b, s0, s1);
            }
            Vector3 tipPos = Vector3.Scale(top[steps], new Vector3(1f, sign, 1f));
            Ball(Trim, tipPos, new Vector3(0.025f, 0.025f, 0.025f), 6, true);
            // Small gold wing flourish near each tip.
            Vector3 baseP = Vector3.Scale(top[steps - 2], new Vector3(1f, sign, 1f));
            FeatherBlade(Trim, baseP, baseP + new Vector3(0f, sign * 0.1f, 0.08f), 0.06f, Vector3.right, 0.02f, 0.3f);
        }
        // String between the tips.
        Vector3 stringTop = top[steps];
        Segment(BowString, stringTop, Vector3.Scale(stringTop, new Vector3(1f, -1f, 1f)), 0.01f, 0.01f);
        // Grip wrap with a gold star.
        Segment(Wood, new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.06f, 0f), 0.065f, 0.065f);
        Star(Trim, new Vector3(0f, 0f, 0.035f), Vector3.forward, 0.07f, 0.025f, 0.02f);
        return Finish("AngelcatBow", BowSubmeshes);
    }

    // ---- Arrow (world units): shaft along +Z, head at the front, three fletching vanes at the back.

    public static Mesh BuildArrow()
    {
        Begin();
        float half = ArrowLength * 0.5f;
        Segment(Wood, new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half - 0.12f), 0.035f, 0.035f);
        Frustum(Trim, new Vector3(0f, 0f, half - 0.13f), Quaternion.FromToRotation(Vector3.up, Vector3.forward), 0.06f, 0.004f, 0.13f, 4);
        for (int k = 0; k < 3; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 0f, k * 120f);
            Vector3 n = q * Vector3.right;
            Vector3 o = q * new Vector3(0f, 0.045f, 0f);
            Flat(Fletch, new[] { new Vector3(0f, 0f, -half + 0.02f) + o * 0.4f, new Vector3(0f, 0f, -half + 0.24f) + o * 0.4f,
                new Vector3(0f, 0f, -half + 0.12f) + o * 1.6f, new Vector3(0f, 0f, -half - 0.02f) + o * 1.5f }, n, 0.012f);
        }
        return Finish("AngelcatArrow", BowSubmeshes);
    }

    // ---- Cloud (tower root space): a cluster of puffs over the 2x2 tiles, top at CloudTop, shaded puffs underneath.

    public static Mesh BuildCloud()
    {
        Begin();
        Vector3 m = Centre;
        Ball(Puff, m + new Vector3(0f, CloudTop - 0.32f, 0f), new Vector3(0.62f, 0.32f, 0.6f), 12, true);
        float[] ang = { 0f, 52f, 101f, 155f, 205f, 258f, 310f };
        float[] rad = { 0.34f, 0.3f, 0.36f, 0.32f, 0.35f, 0.3f, 0.33f };
        for (int i = 0; i < ang.Length; i++)
        {
            float a = ang[i] * Mathf.Deg2Rad;
            Vector3 p = m + new Vector3(Mathf.Cos(a) * 0.62f, CloudTop - 0.42f + (i % 3) * 0.05f, Mathf.Sin(a) * 0.62f);
            Ball(Puff, p, new Vector3(rad[i], rad[i] * 0.85f, rad[i]), 10, true);
            Ball(PuffShade, p + new Vector3(0f, -0.2f, 0f) - new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f,
                new Vector3(rad[i] * 0.8f, rad[i] * 0.55f, rad[i] * 0.8f), 8, true);
        }
        Ball(Puff, m + new Vector3(-0.3f, CloudTop - 0.18f, 0.25f), new Vector3(0.3f, 0.2f, 0.3f), 10, true);
        Ball(Puff, m + new Vector3(0.35f, CloudTop - 0.2f, -0.2f), new Vector3(0.28f, 0.18f, 0.28f), 10, true);
        Ball(PuffShade, m + new Vector3(0f, CloudTop - 0.68f, 0f), new Vector3(0.5f, 0.16f, 0.5f), 10, true);
        return Finish("AngelcatCloud", CloudSubmeshes);
    }

    // ---- Helpers (same shapes as SwordscatModelBuilder, plus Flat, Star, Torus, Surface).

    private static Vector3 Surface(Vector3 c, Vector3 radii, Vector3 dir)
    {
        return c + Vector3.Scale(dir.normalized, radii);
    }

    // A convex outline extruded along its normal (thickness t).
    private static void Flat(int material, Vector3[] outline, Vector3 normal, float t)
    {
        int n = outline.Length;
        Vector3 off = normal.normalized * t * 0.5f;
        Vector3[] top = new Vector3[n];
        Vector3[] bottom = new Vector3[n];
        Vector3 centre = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            top[i] = outline[i] + off;
            bottom[i] = outline[i] - off;
            centre += outline[i];
        }
        centre /= n;
        AddPoly(material, top, centre);
        AddPoly(material, bottom, centre);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            AddPoly(material, new[] { bottom[i], bottom[j], top[j], top[i] }, centre);
        }
    }

    // A four-point star facing `normal`: two crossed flat rhombi (long arm `size`, short arm `thin`).
    private static void Star(int material, Vector3 centre, Vector3 normal, float size, float thin, float t)
    {
        Vector3 n = normal.normalized;
        Vector3 u = Vector3.Cross(Vector3.up, n);
        u = u.sqrMagnitude < 0.001f ? Vector3.right : u.normalized;
        Vector3 v = Vector3.Cross(n, u).normalized;
        Flat(material, new[] { centre + v * size, centre + u * thin, centre - v * size, centre - u * thin }, n, t);
        Flat(material, new[] { centre + u * size, centre + v * thin, centre - u * size, centre - v * thin }, n, t * 0.9f);
    }

    // A ring in the XZ plane (radius `ring`, elliptic tube: x = radial half-width, y = half-height).
    private static void Torus(int material, Vector3 c, float ring, Vector2 tube, int segments, int sides)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            float am = (a0 + a1) * 0.5f;
            Vector3 inside = c + new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * ring;
            for (int j = 0; j < sides; j++)
            {
                float b0 = j * Mathf.PI * 2f / sides, b1 = (j + 1) * Mathf.PI * 2f / sides;
                Vector3 p00 = c + d0 * (ring + Mathf.Cos(b0) * tube.x) + Vector3.up * Mathf.Sin(b0) * tube.y;
                Vector3 p01 = c + d0 * (ring + Mathf.Cos(b1) * tube.x) + Vector3.up * Mathf.Sin(b1) * tube.y;
                Vector3 p10 = c + d1 * (ring + Mathf.Cos(b0) * tube.x) + Vector3.up * Mathf.Sin(b0) * tube.y;
                Vector3 p11 = c + d1 * (ring + Mathf.Cos(b1) * tube.x) + Vector3.up * Mathf.Sin(b1) * tube.y;
                AddPoly(material, new[] { p00, p10, p11, p01 }, inside);
            }
        }
    }

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

    private static Mesh Finish(string name, int submeshes)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.indexFormat = Vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.subMeshCount = submeshes;
        for (int i = 0; i < submeshes; i++)
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
        Debug.Log("AngelcatModelBuilder: wrote " + path);
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
