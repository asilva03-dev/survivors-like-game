/*
 * FishercatModelBuilder
 * Procedural low-poly model of the Fishercat tower (tower 3, 2026-11-24, user's sketch from behind): a cat in a blue
 * fisherman (bucket) hat sitting on a small broken-off piece of dock above a patch of water, a barrel of spears beside it
 * and an open cooler behind it. Same faceted style and helpers as TrapperModelBuilder.
 *
 * - Footprint 2x2 (corner tile (0, 0) + (1, 0), (0, -1), (1, -1)): every part is in the tower root's space (root = the
 *   corner tile's centre), centred on the footprint's middle (0.5, -0.5). The tower's front is +Z (at rotation 0 the game
 *   camera sees it from behind, like the sketch).
 * - Water (flat, wavy-edged patch over the 4 tiles, ripple rings), the dock (plank deck at DeckTop 1.5 = fence level on
 *   four log posts, one taller mooring post, uneven plank ends) and the cooler (red body, white lid flipped open, ice and a
 *   fish tail inside) are static (BuildProps). The barrel (staves, dark bands, three spears standing in it) is its own mesh
 *   (BuildBarrel, pivot at its bottom centre, placed at BarrelSpot) so the throw animation can rattle it when the cat
 *   rummages for a spear (2026-11-24).
 * - The cat (BuildCat, earsOut or ears under the hat) is the part that turns; paws are separate (BuildHand), like the
 *   Trapper. The spear (BuildSpear) points along +Z, its middle at the origin, tip at +SpearLength/2.
 * - Units: world units; the cat is built at the Trapper's size (r 0.38) and scaled on the prefab.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FishercatModelBuilder
{
    public const float DeckTop = 1.5f;
    public static readonly Vector3 Centre = new Vector3(0.5f, 0f, -0.5f);
    public static readonly Vector3 CatSpot = new Vector3(0.3f, DeckTop, -0.15f);
    public const float SpearLength = 1.5f;
    /// <summary>How far the water reaches from the footprint's middle (user: further than the 2x2 tiles).</summary>
    public const float WaterRadius = 1.2f;

    // Cat submeshes.
    public const int Fur = 0, FurAccent = 1, Dark = 2, Hat = 3, HatBand = 4, CatSubmeshes = 5;
    // Prop submeshes.
    public const int Plank = 0, PlankDark = 1, Post = 2, BarrelWood = 3, PropDark = 4, CoolerBody = 5, CoolerWhite = 6,
        SpearWood = 7, Metal = 8, Fish = 9, Ice = 10, PropSubmeshes = 11;
    // Water submeshes.
    public const int Water = 0, Ripple = 1, WaterSubmeshes = 2;
    // Spear submeshes.
    public const int SpearShaft = 0, SpearHead = 1, SpearCollar = 2, SpearSubmeshes = 3;

    public const string BodyPath = "Assets/Models/FishercatBody.asset";
    public const string HandPath = "Assets/Models/FishercatHand.asset";
    public const string PropsPath = "Assets/Models/FishercatProps.asset";
    public const string WaterPath = "Assets/Models/FishercatWater.asset";
    public const string SpearPath = "Assets/Models/FishercatSpear.asset";
    public const string BarrelPath = "Assets/Models/FishercatBarrel.asset";
    public static readonly Vector3 BarrelSpot = new Vector3(1.08f, DeckTop, -0.22f);
    public const float BarrelHeight = 0.65f;

    /// <summary>Writes every Fishercat mesh in place (user's pick 2026-11-24: B, cream cat, ears under the hat).</summary>
    [MenuItem("Tools/Models/Turret/Build Fishercat")]
    public static void BuildFishercat()
    {
        Save(BuildCat(false), BodyPath);
        Save(BuildHand(), HandPath);
        Save(BuildProps(), PropsPath);
        Save(BuildWater(), WaterPath);
        Save(BuildSpear(), SpearPath);
        Save(BuildBarrel(), BarrelPath);
    }

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles = new List<int>[12];

    // ---- The cat.

    /// <summary>The cat in its fisherman hat, feet at the origin, facing +Z (no paws: BuildHand).</summary>
    public static Mesh BuildCat(bool earsOut)
    {
        Begin();
        float r = 0.38f;
        Vector3 c = new Vector3(0f, r * 0.98f, 0f);
        Ball(Fur, c, new Vector3(r, r * 0.95f, r * 0.9f), 10, true);
        for (int side = -1; side <= 1; side += 2)
        {
            if (earsOut)
            {
                // Ears poking up through the hat's sides.
                Quaternion tilt = Quaternion.Euler(0f, 0f, side * -24f);
                Vector3 ear = c + new Vector3(side * r * 0.56f, r * 0.62f, -r * 0.05f);
                Frustum(Fur, ear, tilt, r * 0.26f, 0.005f, r * 0.62f, 4);
                Frustum(FurAccent, ear + new Vector3(side * -0.01f, r * 0.12f, r * 0.11f), tilt, r * 0.14f, 0.005f, r * 0.42f, 4);
            }
            // Calm eyes, cheek puffs, whiskers.
            Box(Dark, c + new Vector3(side * r * 0.3f, r * 0.18f, r * 0.84f), new Vector3(r * 0.13f, r * 0.13f, r * 0.08f), Quaternion.identity);
            Ball(FurAccent, c + new Vector3(side * r * 0.14f, -r * 0.08f, r * 0.82f), new Vector3(r * 0.17f, r * 0.13f, r * 0.12f), 6, true);
            for (int w = 0; w < 2; w++)
            {
                Box(Dark, c + new Vector3(side * r * 0.62f, -r * (0.04f + w * 0.12f), r * 0.68f), new Vector3(r * 0.28f, r * 0.025f, r * 0.03f),
                    Quaternion.Euler(0f, side * -30f, side * (w == 0 ? 6f : -6f)));
            }
        }
        Box(Dark, c + new Vector3(0f, r * 0.02f, r * 0.93f), new Vector3(r * 0.1f, r * 0.07f, r * 0.05f), Quaternion.identity);
        // Short tail curled round the side.
        Segment(Fur, new Vector3(-r * 0.7f, r * 0.12f, -r * 0.35f), new Vector3(-r * 0.95f, r * 0.1f, r * 0.2f), r * 0.2f, r * 0.15f);

        // Blue bucket hat: sloped brim, crown, darker band.
        Frustum(Hat, c + new Vector3(0f, r * 0.48f, 0f), Quaternion.Euler(-6f, 0f, 0f), new Vector2(r * 1.08f, r * 1.02f), new Vector2(r * 0.74f, r * 0.7f), r * 0.16f, 12);
        Frustum(HatBand, c + new Vector3(0f, r * 0.62f, -r * 0.02f), Quaternion.identity, new Vector2(r * 0.75f, r * 0.71f), new Vector2(r * 0.72f, r * 0.68f), r * 0.11f, 12);
        Frustum(Hat, c + new Vector3(0f, r * 0.72f, -r * 0.02f), Quaternion.identity, new Vector2(r * 0.72f, r * 0.68f), new Vector2(r * 0.56f, r * 0.52f), r * 0.32f, 12);
        return Finish("FishercatBody", CatSubmeshes);
    }

    /// <summary>One round paw (world units, centred; not scaled with the cat).</summary>
    public static Mesh BuildHand()
    {
        Begin();
        Ball(FurAccent, Vector3.zero, new Vector3(0.11f, 0.09f, 0.12f), 8, true);
        return Finish("FishercatHand", CatSubmeshes);
    }

    // ---- The spear (user's diagram: long thin shaft, a collar near the back, a pointed head with one barb sticking up).

    /// <summary>One spear along +Z, middle at the origin, the barb pointing up (+Y).</summary>
    public static Mesh BuildSpear()
    {
        Begin();
        Spear(SpearShaft, SpearHead, SpearCollar, new Vector3(0f, 0f, -SpearLength * 0.5f), Vector3.forward, Vector3.up, SpearLength);
        return Finish("FishercatSpear", SpearSubmeshes);
    }

    private static void Spear(int wood, int metal, int collar, Vector3 butt, Vector3 dir, Vector3 up, float length)
    {
        dir = dir.normalized;
        up = Vector3.ProjectOnPlane(up, dir).normalized;
        Vector3 headBase = butt + dir * (length * 0.82f);
        Segment(wood, butt, headBase, 0.04f, 0.045f);
        Segment(collar, butt + dir * (length * 0.3f), butt + dir * (length * 0.36f), 0.065f, 0.065f);
        // Head: a socket, then a four-sided point.
        Frustum(metal, headBase, Quaternion.FromToRotation(Vector3.up, dir), 0.035f, 0.05f, length * 0.04f, 4);
        Frustum(metal, headBase + dir * (length * 0.04f), Quaternion.FromToRotation(Vector3.up, dir), 0.055f, 0.004f, length * 0.14f, 4);
        // The barb: a thin swept-back fin rising from the head's base.
        Vector3 a = headBase + dir * (length * 0.02f);
        Vector3 b = headBase + dir * (length * 0.08f);
        Vector3 tip = headBase + dir * (length * 0.035f) + up * (length * 0.1f);
        Fin(metal, a, b, tip, Vector3.Cross(dir, up) * 0.012f);
    }

    // A thin triangular plate (two faces and edges) between a, b and tip, `side` = half its thickness.
    private static void Fin(int material, Vector3 a, Vector3 b, Vector3 tip, Vector3 side)
    {
        Vector3 mid = (a + b + tip) / 3f;
        AddPoly(material, new[] { a + side, b + side, tip + side }, mid);
        AddPoly(material, new[] { a - side, b - side, tip - side }, mid);
        AddPoly(material, new[] { a + side, a - side, tip - side, tip + side }, mid);
        AddPoly(material, new[] { b + side, b - side, tip - side, tip + side }, mid);
    }

    // ---- Water.

    /// <summary>A flat wavy-edged patch of water over the 2x2 footprint (root space) with a few ripple rings.</summary>
    public static Mesh BuildWater()
    {
        Begin();
        int n = 28;
        Vector3[] ring = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float wobble = 0.06f * Mathf.Sin(a * 3f + 0.7f) + 0.04f * Mathf.Sin(a * 5f + 2.1f);
            float rx = WaterRadius + wobble;
            float rz = WaterRadius + wobble;
            ring[i] = Centre + new Vector3(Mathf.Cos(a) * rx, 0.025f, Mathf.Sin(a) * rz);
        }
        Vector3 centre = Centre + new Vector3(0f, 0.025f, 0f);
        for (int i = 0; i < n; i++)
        {
            AddPolyUp(Water, new[] { centre, ring[(i + 1) % n], ring[i] });
        }
        // Ripple rings around the posts and one in the open.
        RippleRing(new Vector3(-0.08f, 0.03f, 0.2f), 0.2f, 0.035f);
        RippleRing(new Vector3(1.08f, 0.03f, -1.08f), 0.24f, 0.035f);
        RippleRing(new Vector3(1.45f, 0.03f, 0.05f), 0.17f, 0.03f);
        RippleRing(new Vector3(-0.35f, 0.03f, -1.2f), 0.15f, 0.03f);
        RippleRing(new Vector3(0.6f, 0.03f, 0.55f), 0.13f, 0.03f);
        return Finish("FishercatWater", WaterSubmeshes);
    }

    private static void RippleRing(Vector3 centre, float radius, float width)
    {
        int n = 14;
        for (int i = 0; i < n; i++)
        {
            if (i % 4 == 3)
            {
                continue; // broken ring
            }
            float a0 = i * Mathf.PI * 2f / n;
            float a1 = (i + 1) * Mathf.PI * 2f / n;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            AddPolyUp(Ripple, new[] { centre + d0 * radius, centre + d1 * radius, centre + d1 * (radius + width), centre + d0 * (radius + width) });
        }
    }

    // ---- Dock, barrel, cooler.

    /// <summary>The static props in the tower root's space: dock section and open cooler (the barrel: BuildBarrel).</summary>
    public static Mesh BuildProps()
    {
        Begin();
        Dock();
        Cooler(new Vector3(0.5f, DeckTop, -0.93f), -8f);
        return Finish("FishercatProps", PropSubmeshes);
    }

    /// <summary>The barrel of spears, bottom centre at the origin (prop submeshes); placed at BarrelSpot.</summary>
    public static Mesh BuildBarrel()
    {
        Begin();
        Barrel(Vector3.zero);
        return Finish("FishercatBarrel", PropSubmeshes);
    }

    private static void Dock()
    {
        float minX = -0.2f, maxX = 1.22f, minZ = -1.2f, maxZ = 0.3f;
        float thick = 0.07f;
        // Planks run along X; their east ends are uneven (a broken-off piece of a pier).
        int count = 7;
        float width = (maxZ - minZ) / count;
        float[] endJitter = { 0.04f, -0.12f, 0.02f, 0.1f, -0.06f, 0.05f, -0.16f };
        for (int i = 0; i < count; i++)
        {
            float z = minZ + width * (i + 0.5f);
            float x1 = maxX + endJitter[i];
            Box(i % 3 == 1 ? PlankDark : Plank, new Vector3((minX + x1) * 0.5f, DeckTop - thick * 0.5f, z),
                new Vector3(x1 - minX, thick, width - 0.025f), Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 0.6f : -0.6f)));
        }
        // Beams under the planks, along Z.
        for (int b = 0; b < 2; b++)
        {
            float x = b == 0 ? -0.05f : 1.05f;
            Box(Post, new Vector3(x, DeckTop - thick - 0.06f, (minZ + maxZ) * 0.5f), new Vector3(0.12f, 0.12f, maxZ - minZ - 0.05f), Quaternion.identity);
        }
        // Four log posts into the water; the north-east one stands up as a mooring post with a rope band.
        Vector3[] posts = { new Vector3(-0.08f, 0f, 0.2f), new Vector3(1.08f, 0f, 0.2f), new Vector3(-0.08f, 0f, -1.08f), new Vector3(1.08f, 0f, -1.08f) };
        for (int i = 0; i < posts.Length; i++)
        {
            float top = i == 1 ? DeckTop + 0.35f : DeckTop - thick;
            Frustum(Post, posts[i] + Vector3.down * 0.05f, Quaternion.identity, 0.095f, 0.085f, top + 0.05f, 7);
            if (i == 1)
            {
                Frustum(PropDark, posts[i] + Vector3.up * (DeckTop + 0.12f), Quaternion.identity, 0.105f, 0.105f, 0.08f, 7);
                Frustum(PlankDark, posts[i] + Vector3.up * (top - 0.01f), Quaternion.identity, 0.09f, 0.07f, 0.04f, 7);
            }
        }
    }

    private static void Barrel(Vector3 bottom)
    {
        // Bigger barrel (user): it may hang a little over the dock's edge.
        float h = BarrelHeight;
        Frustum(BarrelWood, bottom, Quaternion.identity, 0.25f, 0.3f, h * 0.5f, 12);
        Frustum(BarrelWood, bottom + Vector3.up * (h * 0.5f), Quaternion.identity, 0.3f, 0.26f, h * 0.5f, 12);
        Frustum(PropDark, bottom + Vector3.up * 0.08f, Quaternion.identity, 0.275f, 0.282f, 0.05f, 12);
        Frustum(PropDark, bottom + Vector3.up * (h - 0.13f), Quaternion.identity, 0.285f, 0.275f, 0.05f, 12);
        Frustum(PropDark, bottom + Vector3.up * (h - 0.02f), Quaternion.identity, 0.235f, 0.235f, 0.015f, 12);
        // Three spears standing in it, leaning a little apart.
        Vector3[] lean = { new Vector3(-0.1f, 1f, 0.06f), new Vector3(0.02f, 1f, -0.08f), new Vector3(0.12f, 1f, 0.05f) };
        Vector3[] foot = { new Vector3(-0.1f, 0.12f, 0.05f), new Vector3(0.01f, 0.12f, -0.08f), new Vector3(0.11f, 0.12f, 0.05f) };
        for (int i = 0; i < 3; i++)
        {
            Spear(SpearWood, Metal, PropDark, bottom + foot[i], lean[i], Vector3.forward, SpearLength * 0.95f);
        }
    }

    private static void Cooler(Vector3 bottom, float yaw)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 size = new Vector3(0.8f, 0.45f, 0.5f);
        Box(CoolerBody, bottom + rot * new Vector3(0f, size.y * 0.5f, 0f), size, rot);
        // White rim, ice inside, a fish tail sticking out.
        Box(CoolerWhite, bottom + rot * new Vector3(0f, size.y - 0.02f, 0f), new Vector3(size.x + 0.02f, 0.05f, size.z + 0.02f), rot);
        Box(Ice, bottom + rot * new Vector3(0f, size.y - 0.015f, 0f), new Vector3(size.x - 0.08f, 0.05f, size.z - 0.08f), rot);
        Ball(Fish, bottom + rot * new Vector3(0.1f, size.y + 0.02f, 0.03f), new Vector3(0.2f, 0.06f, 0.08f), 6, true);
        Vector3 tailBase = bottom + rot * new Vector3(0.28f, size.y + 0.03f, 0.03f);
        Fin(Fish, tailBase, tailBase + rot * new Vector3(0.15f, 0.07f, 0f), tailBase + rot * new Vector3(0.12f, 0.18f, 0f), rot * new Vector3(0f, 0f, 0.012f));
        // Handles.
        for (int s = -1; s <= 1; s += 2)
        {
            Box(PropDark, bottom + rot * new Vector3(s * (size.x * 0.5f + 0.015f), size.y * 0.7f, 0f), new Vector3(0.035f, 0.05f, 0.2f), rot);
        }
        // Lid hinged at the back edge, flipped open and leaning back.
        Vector3 hinge = bottom + rot * new Vector3(0f, size.y + 0.02f, -size.z * 0.5f);
        Quaternion lid = rot * Quaternion.Euler(-105f, 0f, 0f);
        Box(CoolerWhite, hinge + lid * new Vector3(0f, 0.025f, size.z * 0.5f), new Vector3(size.x + 0.02f, 0.05f, size.z + 0.02f), lid);
    }

    // ---- Helpers (same shapes as TrapperModelBuilder).

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
        Debug.Log("FishercatModelBuilder: wrote " + path);
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

    // A flat polygon facing up (water).
    private static void AddPolyUp(int material, Vector3[] p)
    {
        AddPoly(material, p, p[0] + Vector3.down);
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
