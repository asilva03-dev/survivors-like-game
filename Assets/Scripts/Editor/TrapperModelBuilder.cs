/*
 * TrapperModelBuilder
 * Procedural low-poly model of the Trapper turret (2026-11-23, user's sketch: a round grumpy cat with a sack of rat traps
 * next to it; the cat grabs a trap, arms it and throws it onto the nearest path). Same faceted style and helpers as
 * TurretModelBuilder.
 *
 * - User's pick (2026-11-23, "B without the held trap"): the round grumpy ginger cat sitting beside a squat wide sack,
 *   paws resting in front, three traps standing up out of the sack in a fan with their bars and cheese facing forward.
 *   Tools > Models > Turret > Build Trapper writes Models/TrapperBody.asset (the cat alone: the part that turns),
 *   Models/TrapperSack.asset, Models/TrapperPedestal.asset, Models/RatTrapBase.asset and Models/RatTrapBar.asset in place
 *   (prefabs keep their links); materials in Materials/Models/trapper/ (+ the gun turret's dark and the bag rat's
 *   burlap/rope; the pedestal uses the fence's FenceCrate / FenceCrateFrame).
 * - Pedestal = an L over three tiles (user's sketch, 2026-11-23; replaces change 1's one-tile layout), in the turret root's
 *   space (root = the corner tile's centre; the footprint's rotation turns the root in 90 deg steps):
 *   corner tile (0, 0): the cat's stack, a crate with a smaller crate on top (top at StackTop 1.5 = fence level);
 *   tile (SackX 1, 0) beside it: a wide low crate with a smaller crate on it (top at SackCrateTop) and the sack on top;
 *   tile (0, FrontZ -1) in front (toward the camera at rotation 0): a low wide box.
 *   Trapper.prefab: Body (the cat, the only part that turns) at (0, StackTop, 0), Sack at (SackX, SackCrateTop, 0).
 * - Hands (2026-11-23, throw animation): the cat mesh has no paws; Models/TrapperHand.asset is one round paw (world units,
 *   centred, fur accent) used twice on Trapper.prefab (Body > HandL / HandR), moved by TrapperAnimator.
 * - Units: world units, feet on y 0, the cat faces +Z.
 * - The sack (user): not too tall, wider to compensate so it still looks full, recognisably a sack (round bulging body,
 *   gathered neck with a rope tie, a folded rim).
 * - Submeshes: 0 fur, 1 fur accent (muzzle, inner ears, paws), 2 dark (eyes, brows, nose, springs), 3 burlap, 4 rope,
 *   5 trap wood, 6 trap metal, 7 bait (cheese).
 * - The thrown trap (RatTrap.prefab) is two meshes: RatTrapBase (board, spring, pedal, cheese; centred, lying flat) and
 *   RatTrapBar (the U-shaped kill bar, hinged at the origin = the spring, pulled back toward -Z while set; RatTrap swings
 *   it over to +Z when it snaps). ~0.36 long.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TrapperModelBuilder
{
    public const string BodyPath = "Assets/Models/TrapperBody.asset";
    public const string TrapBasePath = "Assets/Models/RatTrapBase.asset";
    public const string TrapBarPath = "Assets/Models/RatTrapBar.asset";
    public const string SackPath = "Assets/Models/TrapperSack.asset";
    public const string PedestalPath = "Assets/Models/TrapperPedestal.asset";
    public const string HandPath = "Assets/Models/TrapperHand.asset";

    public const float StackTop = 1.5f;
    public const float SackX = 1f;
    public const float SackCrateTop = 1.05f;
    public const float FrontZ = -1f;

    [MenuItem("Tools/Models/Turret/Build Trapper")]
    public static void BuildTrapper()
    {
        Save(BuildBody(), BodyPath);
        Save(BuildSack(), SackPath);
        Save(BuildPedestal(), PedestalPath);
        Save(BuildHand(), HandPath);
        Begin();
        TrapBase(Vector3.zero, Quaternion.identity, 1f);
        Save(Finish("RatTrapBase"), TrapBasePath);
        // The bar mesh has its hinge (the spring) at the origin, so the prefab's Bar child sits at (0, 0.05, 0) and rotates.
        Begin();
        TrapBar(new Vector3(0f, -0.05f, 0f), Quaternion.identity, 1f);
        Save(Finish("RatTrapBar"), TrapBarPath);
    }

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
        Debug.Log("TrapperModelBuilder: wrote " + path);
    }
    public const int Fur = 0;
    public const int FurAccent = 1;
    public const int Dark = 2;
    public const int Burlap = 3;
    public const int Rope = 4;
    public const int Wood = 5;
    public const int Metal = 6;
    public const int Bait = 7;
    public const int SubmeshCount = 8;

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles =
    {
        new List<int>(), new List<int>(), new List<int>(), new List<int>(),
        new List<int>(), new List<int>(), new List<int>(), new List<int>()
    };

    /// <summary>The cat alone (the part that turns), feet at the origin, facing +Z.</summary>
    public static Mesh BuildBody()
    {
        Begin();
        Cat(Vector3.zero, 0.38f);
        return Finish("TrapperBody");
    }

    /// <summary>One round paw (world units, centred; not scaled with the cat).</summary>
    public static Mesh BuildHand()
    {
        Begin();
        Ball(FurAccent, Vector3.zero, new Vector3(0.11f, 0.09f, 0.12f), 8, true);
        return Finish("TrapperHand");
    }

    /// <summary>The sack of traps alone, bottom at the origin, traps facing +Z.</summary>
    public static Mesh BuildSack()
    {
        Begin();
        Sack(Vector3.zero);
        return Finish("TrapperSack");
    }

    /// <summary>The crates of the L, in the turret root's space: the cat's stack, the sack's stack, the front box.
    /// Two submeshes: 0 crate (FenceCrate), 1 frame (FenceCrateFrame).</summary>
    public static Mesh BuildPedestal()
    {
        Begin();
        Crate(Vector3.zero, new Vector3(0.95f, 0.95f, 0.95f), 0f);
        Crate(new Vector3(0.02f, 0.95f, -0.02f), new Vector3(0.8f, StackTop - 0.95f, 0.8f), 8f);
        Crate(new Vector3(SackX, 0f, 0f), new Vector3(0.92f, 0.6f, 0.92f), -4f);
        Crate(new Vector3(SackX + 0.02f, 0.6f, 0.01f), new Vector3(0.7f, SackCrateTop - 0.6f, 0.7f), 9f);
        Crate(new Vector3(0.02f, 0f, FrontZ), new Vector3(0.9f, 0.5f, 0.8f), -7f);
        return Finish("TrapperPedestal", 2);
    }

    // A wooden crate like the fence's lone-cell crates: body, darker edge frame, a cross brace on the front and back.
    private static void Crate(Vector3 bottom, Vector3 size, float yaw)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 c = bottom + Vector3.up * size.y * 0.5f;
        Box(0, c, size, rot);
        float t = 0.08f;
        Vector3 h = size * 0.5f;
        for (int a = -1; a <= 1; a += 2)
        {
            for (int b = -1; b <= 1; b += 2)
            {
                Box(1, c + rot * new Vector3(a * h.x, 0f, b * h.z), new Vector3(t, size.y + 0.01f, t), rot);
                Box(1, c + rot * new Vector3(0f, a * h.y, b * h.z), new Vector3(size.x + 0.01f, t, t), rot);
                Box(1, c + rot * new Vector3(a * h.x, b * h.y, 0f), new Vector3(t, t, size.z + 0.01f), rot);
            }
        }
        float angle = Mathf.Atan2(size.y - t, size.x - t) * Mathf.Rad2Deg;
        float len = Mathf.Sqrt((size.x - t) * (size.x - t) + (size.y - t) * (size.y - t));
        for (int z = -1; z <= 1; z += 2)
        {
            Box(1, c + rot * new Vector3(0f, 0f, z * (h.z + 0.01f)), new Vector3(len, t * 0.9f, 0.03f), rot * Quaternion.Euler(0f, 0f, z * angle));
        }
        // Plank seams on the sides.
        for (int k = 1; k < 3; k++)
        {
            float y = -h.y + size.y * k / 3f;
            for (int x = -1; x <= 1; x += 2)
            {
                Box(1, c + rot * new Vector3(x * (h.x + 0.005f), y, 0f), new Vector3(0.02f, 0.025f, size.z), rot);
            }
        }
    }

    // The round grumpy cat (sketch): one big round head-body, ears, half-shut eyes under slanted brows, a muzzle with
    // whisker marks, front paws resting on the ground.
    private static void Cat(Vector3 feet, float r)
    {
        Vector3 c = feet + new Vector3(0f, r * 0.98f, 0f);
        Ball(Fur, c, new Vector3(r, r * 0.95f, r * 0.9f), 10, true);
        for (int side = -1; side <= 1; side += 2)
        {
            // Ears: wide-based pointed tips, a little inner colour.
            Quaternion tilt = Quaternion.Euler(0f, 0f, side * -18f);
            Vector3 ear = c + new Vector3(side * r * 0.52f, r * 0.72f, -r * 0.05f);
            Frustum(Fur, ear, tilt, r * 0.3f, 0.005f, r * 0.62f, 4);
            Frustum(FurAccent, ear + new Vector3(0f, r * 0.05f, r * 0.12f), tilt, r * 0.17f, 0.005f, r * 0.42f, 4);
            // Half-shut eyes and slanted grumpy brows.
            Vector3 eye = c + new Vector3(side * r * 0.33f, r * 0.22f, r * 0.83f);
            Box(Dark, eye, new Vector3(r * 0.22f, r * 0.07f, r * 0.08f), Quaternion.Euler(0f, 0f, side * 8f));
            Box(Dark, eye + new Vector3(0f, r * 0.15f, -r * 0.02f), new Vector3(r * 0.28f, r * 0.05f, r * 0.08f), Quaternion.Euler(0f, 0f, side * -18f));
            // Muzzle puffs and whisker marks.
            Ball(FurAccent, c + new Vector3(side * r * 0.14f, -r * 0.05f, r * 0.82f), new Vector3(r * 0.17f, r * 0.13f, r * 0.12f), 6, true);
            for (int w = 0; w < 2; w++)
            {
                Box(Dark, c + new Vector3(side * r * 0.62f, -r * (0.02f + w * 0.12f), r * 0.68f), new Vector3(r * 0.28f, r * 0.025f, r * 0.03f),
                    Quaternion.Euler(0f, side * -30f, side * (w == 0 ? 6f : -6f)));
            }
            // (Front paws: separate hand objects since 2026-11-23, see BuildHand.)
        }
        Box(Dark, c + new Vector3(0f, r * 0.04f, r * 0.93f), new Vector3(r * 0.1f, r * 0.07f, r * 0.05f), Quaternion.identity);
        // Frown under the muzzle.
        Box(Dark, c + new Vector3(0f, -r * 0.24f, r * 0.84f), new Vector3(r * 0.26f, r * 0.035f, r * 0.04f), Quaternion.identity);
        // A short tail curling round the side.
        Segment(Fur, feet + new Vector3(-r * 0.7f, r * 0.12f, -r * 0.35f), feet + new Vector3(-r * 0.95f, r * 0.1f, r * 0.2f), r * 0.2f, r * 0.15f);
    }

    // Upright sack: squat, wide, bulging body; a tightly gathered neck with a rope tie, a floppy flared opening with flaps
    // hanging over, traps sticking up out of it at angles (tilted toward the camera so the bars and cheese show).
    private static void Sack(Vector3 bottom)
    {
        Ball(Burlap, bottom + new Vector3(0f, 0.25f, 0f), new Vector3(0.42f, 0.26f, 0.36f), 9, true);
        // Shoulders narrowing into the gathered neck.
        Frustum(Burlap, bottom + new Vector3(0f, 0.4f, 0f), Quaternion.identity, new Vector2(0.28f, 0.24f), new Vector2(0.13f, 0.11f), 0.1f, 9);
        Frustum(Rope, bottom + new Vector3(0f, 0.47f, 0f), Quaternion.identity, new Vector2(0.15f, 0.13f), new Vector2(0.15f, 0.13f), 0.045f, 9);
        // Floppy opening flaring above the tie, with a few flaps folded over the rope.
        Frustum(Burlap, bottom + new Vector3(0f, 0.51f, 0f), Quaternion.Euler(6f, 0f, -5f), new Vector2(0.13f, 0.11f), new Vector2(0.27f, 0.23f), 0.12f, 7);
        for (int i = 0; i < 4; i++)
        {
            float a2 = (i * 90f + 25f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2));
            Vector3 hinge = bottom + new Vector3(0f, 0.6f, 0f) + dir * 0.22f;
            Box(Burlap, hinge + dir * 0.04f - Vector3.up * 0.05f, new Vector3(0.12f, 0.12f, 0.025f),
                Quaternion.LookRotation(dir) * Quaternion.Euler(25f, 0f, 0f));
        }
        // Rope ends hanging from the tie.
        Segment(Rope, bottom + new Vector3(0.13f, 0.48f, 0.08f), bottom + new Vector3(0.22f, 0.32f, 0.2f), 0.035f, 0.03f);
        Segment(Rope, bottom + new Vector3(0.13f, 0.48f, 0.08f), bottom + new Vector3(0.27f, 0.36f, 0.08f), 0.035f, 0.03f);
        // A sewn-on patch.
        Box(Rope, bottom + new Vector3(-0.12f, 0.24f, 0.35f), new Vector3(0.16f, 0.14f, 0.02f), Quaternion.Euler(0f, -12f, 8f));
        // Traps standing up in the opening like a fan, half out of the sack, leaning back a little with their kill bars and
        // cheese facing the way the cat faces (+Z) and up; the middle one a bit taller and further back.
        Quaternion face = Quaternion.Euler(0f, 180f, 0f);
        Trap(bottom + new Vector3(-0.11f, 0.68f, -0.03f), Quaternion.Euler(0f, -14f, -10f) * face * Quaternion.Euler(-62f, 0f, 0f), 0.85f);
        Trap(bottom + new Vector3(0.0f, 0.74f, 0.03f), face * Quaternion.Euler(-58f, 0f, 0f), 0.85f);
        Trap(bottom + new Vector3(0.11f, 0.67f, -0.02f), Quaternion.Euler(0f, 14f, 10f) * face * Quaternion.Euler(-62f, 0f, 0f), 0.85f);
    }

    // A set snap trap: the base (board, spring, pedal, cheese) and the kill bar pulled back.
    private static void Trap(Vector3 centre, Quaternion rot, float scale)
    {
        TrapBase(centre, rot, scale);
        TrapBar(centre, rot, scale);
    }

    // Wooden board, a coiled spring across the middle (the bar's hinge), the trigger pedal with a cheese wedge toward +Z.
    private static void TrapBase(Vector3 centre, Quaternion rot, float s)
    {
        Box(Wood, centre + rot * new Vector3(0f, 0.015f * s, 0f), new Vector3(0.2f, 0.03f, 0.36f) * s, rot);
        Frustum(Dark, centre + rot * new Vector3(-0.08f, 0.05f, 0f) * s, rot * Quaternion.Euler(0f, 0f, -90f), 0.03f * s, 0.03f * s, 0.16f * s, 6);
        Box(Metal, centre + rot * new Vector3(0f, 0.04f, 0.1f) * s, new Vector3(0.07f, 0.015f, 0.06f) * s, rot);
        Prism(Bait, centre + rot * new Vector3(0f, 0.05f, 0.1f) * s, rot, 0.07f * s, 0.045f * s);
    }

    // The U-shaped kill bar, hinged at the spring (centre + 0.05 up), its arms reaching back toward -Z while set.
    private static void TrapBar(Vector3 centre, Quaternion rot, float s)
    {
        Vector3 hinge = new Vector3(0f, 0.05f, 0f) * s;
        Vector3 a = hinge + new Vector3(-0.075f, -0.01f, 0f) * s;
        Vector3 b = hinge + new Vector3(0.075f, -0.01f, 0f) * s;
        Vector3 a2 = hinge + new Vector3(-0.075f, -0.01f, -0.15f) * s;
        Vector3 b2 = hinge + new Vector3(0.075f, -0.01f, -0.15f) * s;
        Segment(Metal, centre + rot * a, centre + rot * a2, 0.02f * s, 0.02f * s);
        Segment(Metal, centre + rot * b, centre + rot * b2, 0.02f * s, 0.02f * s);
        Segment(Metal, centre + rot * a2, centre + rot * b2, 0.02f * s, 0.02f * s);
    }

    // A small cheese wedge (triangular prism) sitting at c.
    private static void Prism(int material, Vector3 c, Quaternion rot, float size, float height)
    {
        Vector3 p0 = c + rot * new Vector3(-size * 0.5f, 0f, -size * 0.4f);
        Vector3 p1 = c + rot * new Vector3(size * 0.5f, 0f, -size * 0.4f);
        Vector3 p2 = c + rot * new Vector3(0f, 0f, size * 0.6f);
        Vector3 up = rot * new Vector3(0f, height, 0f);
        Vector3 mid = (p0 + p1 + p2) / 3f + up * 0.5f;
        AddPoly(material, new[] { p0, p1, p2 }, mid);
        AddPoly(material, new[] { p0 + up, p1 + up, p2 + up }, mid);
        AddPoly(material, new[] { p0, p1, p1 + up, p0 + up }, mid);
        AddPoly(material, new[] { p1, p2, p2 + up, p1 + up }, mid);
        AddPoly(material, new[] { p2, p0, p0 + up, p2 + up }, mid);
    }

    // ---- Shared helpers (same shapes as TurretModelBuilder / RatModelBuilder).

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
        return Finish(name, SubmeshCount);
    }

    private static Mesh Finish(string name, int submeshes)
    {
        Mesh mesh = new Mesh { name = name };
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
