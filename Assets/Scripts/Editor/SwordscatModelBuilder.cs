/*
 * SwordscatModelBuilder
 * Procedural low-poly model of the SwordsCat tower (tower 5, 2026-11-25, user's sketch): a stern cat with long spiky
 * dark hair (bangs framing the face, spikes swept up at the back) sitting on a stack of crates, holding a sheathed katana.
 * Same faceted style and helpers as FishercatModelBuilder.
 *
 * - Footprint 1x2 (tile (0, 0) + (1, 0)): the pedestal is in the tower root's space (root = tile (0, 0)'s centre): one
 *   crate per tile and a smaller crate on top over the seam, its top at DeckTop 1.5 = fence level, where the cat sits
 *   (CatSpot). Front +Z.
 * - The cat (BuildCat, hair included) is built at the Trapper's size (r 0.38) and scaled on the prefab (x1.6 like the
 *   Trapper); the paws (BuildHand), the sheath (BuildSheath) and the blade (BuildBlade: hilt, guard, steel) are separate
 *   meshes so SwordscatAnimator can draw the blade. Sword space: the guard (tsuba) at the origin, the sheath and steel
 *   along +Z, the hilt along -Z.
 * - Look (user's pick C of three, 2026-11-25): brown fur, draw stance; GetPose holds the sword and paw spots in the
 *   cat's space for the prefab. Tools > Models > Turret > Build SwordsCat writes the meshes to Assets/Models/Swordscat*.
 * - BuildWave (2026-11-25): the sword-wave projectile, a flat crescent flying along +Z; since the same day in the slash's
 *   style with vertex colours (glow, white-hot edge, trailing speed lines) for the SwordscatSlash material. Save copies
 *   vertex colours too.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SwordscatModelBuilder
{
    public const float DeckTop = 1.5f;
    public static readonly Vector3 CatSpot = new Vector3(0.5f, DeckTop, 0f);
    public const float CatScale = 1.6f;
    public const float SheathLength = 0.86f;
    public const float HiltLength = 0.3f;
    public const float BladeLength = 0.84f;

    // Cat submeshes.
    public const int Fur = 0, FurAccent = 1, Dark = 2, Hair = 3, CatSubmeshes = 4;
    // Sword submeshes (shared by the sheath and blade meshes, so both use one material list).
    public const int Wrap = 0, WrapDark = 1, Guard = 2, Sheath = 3, Ribbon = 4, Steel = 5, SwordSubmeshes = 6;
    // Pedestal submeshes: 0 crate (FenceCrate), 1 frame (FenceCrateFrame).

    /// <summary>One look's pose in the cat's space (feet at the origin, facing +Z, before CatScale): the guard's
    /// position, the sheath's direction (guard -> tip), the two paws, the cat's yaw.</summary>
    public struct Pose
    {
        public Vector3 guard;
        public Vector3 sheathDir;
        public Vector3 pawA;
        public Vector3 pawB;
        public float catYaw;
        public bool ribbon;
    }

    /// <summary>The user's pick C (2026-11-25): draw stance at the hip, sheath angled back, a trailing cord; paw A on the
    /// hilt, paw B on the sheath's mouth; the cat turned 15 deg.</summary>
    public static Pose GetPose()
    {
        Pose p = new Pose();
        // User's sketch (2026-11-25): held at the cat's side, hilt at the lower front of the body, the sheath running back
        // and down (~32 deg), the scabbard paw partway down it (was nearly horizontal, out to the side).
        p.guard = new Vector3(0.42f, 0.27f, 0.3f);
        p.sheathDir = new Vector3(0.12f, -0.53f, -0.84f).normalized;
        p.pawA = p.guard - p.sheathDir * 0.17f;
        p.pawB = p.guard + p.sheathDir * 0.22f;
        p.catYaw = -15f;
        p.ribbon = true;
        return p;
    }

    public const string BodyPath = "Assets/Models/SwordscatBody.asset";
    public const string HandPath = "Assets/Models/SwordscatHand.asset";
    public const string SheathPath = "Assets/Models/SwordscatSheath.asset";
    public const string BladePath = "Assets/Models/SwordscatBlade.asset";
    public const string PedestalPath = "Assets/Models/SwordscatPedestal.asset";

    /// <summary>Writes every SwordsCat mesh in place (look C: brown fur, sword with the yellow cord).</summary>
    [MenuItem("Tools/Models/Turret/Build SwordsCat")]
    public static void BuildSwordscat()
    {
        Save(BuildCat(), BodyPath);
        Save(BuildHand(), HandPath);
        Save(BuildSheath(GetPose().ribbon), SheathPath);
        Save(BuildBlade(), BladePath);
        Save(BuildPedestal(), PedestalPath);
        Save(BuildWave(), WavePath);
    }

    public const string WavePath = "Assets/Models/SwordscatWave.asset";

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles = new List<int>[8];

    // ---- The cat.

    /// <summary>The stern cat with its spiky hair, feet at the origin, facing +Z (no paws: BuildHand).</summary>
    public static Mesh BuildCat()
    {
        Begin();
        float r = 0.38f;
        Vector3 c = new Vector3(0f, r * 0.98f, 0f);
        Ball(Fur, c, new Vector3(r, r * 0.95f, r * 0.9f), 10, true);
        for (int side = -1; side <= 1; side += 2)
        {
            // Ears poking up through the hair.
            Quaternion tilt = Quaternion.Euler(0f, 0f, side * -22f);
            Vector3 ear = c + new Vector3(side * r * 0.55f, r * 0.7f, -r * 0.08f);
            Frustum(Fur, ear, tilt, r * 0.26f, 0.005f, r * 0.7f, 4);
            Frustum(FurAccent, ear + new Vector3(side * -0.01f, r * 0.14f, r * 0.11f), tilt, r * 0.14f, 0.005f, r * 0.46f, 4);
            // Narrow, sharp eyes under angry brows; cheek puffs; whiskers.
            Box(Dark, c + new Vector3(side * r * 0.3f, r * 0.17f, r * 0.84f), new Vector3(r * 0.17f, r * 0.07f, r * 0.08f), Quaternion.Euler(0f, 0f, side * 10f));
            Box(Dark, c + new Vector3(side * r * 0.3f, r * 0.31f, r * 0.82f), new Vector3(r * 0.26f, r * 0.05f, r * 0.06f), Quaternion.Euler(0f, 0f, side * 18f));
            Ball(FurAccent, c + new Vector3(side * r * 0.14f, -r * 0.08f, r * 0.82f), new Vector3(r * 0.17f, r * 0.13f, r * 0.12f), 6, true);
            for (int w = 0; w < 2; w++)
            {
                Box(Dark, c + new Vector3(side * r * 0.62f, -r * (0.04f + w * 0.12f), r * 0.68f), new Vector3(r * 0.28f, r * 0.025f, r * 0.03f),
                    Quaternion.Euler(0f, side * -30f, side * (w == 0 ? 6f : -6f)));
            }
        }
        Box(Dark, c + new Vector3(0f, r * 0.02f, r * 0.93f), new Vector3(r * 0.1f, r * 0.07f, r * 0.05f), Quaternion.identity);
        Box(Dark, c + new Vector3(0f, -r * 0.24f, r * 0.85f), new Vector3(r * 0.14f, r * 0.025f, r * 0.03f), Quaternion.identity);
        Segment(Fur, new Vector3(-r * 0.7f, r * 0.12f, -r * 0.35f), new Vector3(-r * 0.95f, r * 0.1f, r * 0.2f), r * 0.2f, r * 0.15f);
        Hairdo(c, r);
        return Finish("SwordscatBody", CatSubmeshes);
    }

    // Long spiky hair: a cap over the top and back, spikes swept up and back, two long bangs framing the face, three
    // shorter bangs over the forehead (kept above the eyes).
    private static void Hairdo(Vector3 c, float r)
    {
        Vector3 capC = c + new Vector3(0f, r * 0.2f, -r * 0.15f);
        Vector3 capR = new Vector3(r * 1.03f, r * 0.85f, r * 0.95f);
        Ball(Hair, capC, capR, 10, false);
        for (int row = 0; row < 2; row++)
        {
            float el = (row == 0 ? 38f : 8f) * Mathf.Deg2Rad;
            int half = row == 0 ? 2 : 3;
            for (int k = -half; k <= half; k++)
            {
                float yaw = k * (row == 0 ? 30f : 26f) * Mathf.Deg2Rad;
                Vector3 onCap = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(el) * capR.x, Mathf.Sin(el) * capR.y, -Mathf.Cos(yaw) * Mathf.Cos(el) * capR.z) * 0.85f;
                Vector3 dir = new Vector3(Mathf.Sin(yaw) * 0.7f, row == 0 ? 0.9f : 0.45f, -1f).normalized;
                float len = r * (row == 0 ? 0.62f : 0.5f) * (1f - Mathf.Abs(k) * 0.08f);
                Spike(capC + onCap, dir, len, r * 0.21f);
            }
        }
        // Top spikes standing up.
        Spike(capC + new Vector3(-r * 0.2f, capR.y * 0.85f, -r * 0.05f), new Vector3(-0.3f, 1f, -0.5f), r * 0.4f, r * 0.2f);
        Spike(capC + new Vector3(r * 0.22f, capR.y * 0.85f, -r * 0.1f), new Vector3(0.35f, 1f, -0.6f), r * 0.38f, r * 0.2f);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 a = c + new Vector3(side * r * 0.62f, r * 0.6f, r * 0.42f);
            Vector3 b = c + new Vector3(side * r * 0.68f, -r * 0.32f, r * 0.64f);
            Spike(a, b - a, (b - a).magnitude, r * 0.17f);
        }
        Vector3[] roots = { new Vector3(-r * 0.32f, r * 0.8f, r * 0.48f), new Vector3(r * 0.06f, r * 0.84f, r * 0.48f), new Vector3(r * 0.34f, r * 0.78f, r * 0.48f) };
        Vector3[] tips = { new Vector3(-r * 0.4f, r * 0.36f, r * 0.87f), new Vector3(r * 0.13f, r * 0.42f, r * 0.9f), new Vector3(r * 0.36f, r * 0.47f, r * 0.86f) };
        for (int i = 0; i < 3; i++)
        {
            Spike(c + roots[i], tips[i] - roots[i], (tips[i] - roots[i]).magnitude, r * 0.15f);
        }
    }

    private static void Spike(Vector3 root, Vector3 dir, float length, float radius)
    {
        Frustum(Hair, root, Quaternion.FromToRotation(Vector3.up, dir.normalized), radius, 0.004f, length, 4);
    }

    /// <summary>One round paw (world units, centred; not scaled with the cat).</summary>
    public static Mesh BuildHand()
    {
        Begin();
        Ball(FurAccent, Vector3.zero, new Vector3(0.11f, 0.09f, 0.12f), 8, true);
        return Finish("SwordscatHand", CatSubmeshes);
    }

    // ---- The sword: a katana in its sheath (cat space units). Since the animation (2026-11-25) the sheath and the blade
    // (hilt, guard, collar, steel) are two meshes in the same space, so the blade can be drawn out along -Z.

    /// <summary>The drawable part: hilt along -Z, guard at the origin, the steel blade along +Z (inside the sheath when
    /// both sit on the same transform).</summary>
    public static Mesh BuildBlade()
    {
        Begin();
        Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
        Hilt(alongZ);
        Vector3 b0 = new Vector3(0f, 0f, 0.05f);
        Vector3 b1 = new Vector3(0f, 0.013f, 0.45f);
        Vector3 b2 = new Vector3(0f, 0.03f, BladeLength - 0.06f);
        Vector3[] pts = { b0, b1, b2 };
        for (int i = 0; i < 2; i++)
        {
            Vector3 d = pts[i + 1] - pts[i];
            Box(Steel, (pts[i] + pts[i + 1]) * 0.5f, new Vector3(0.012f, 0.038f - i * 0.004f, d.magnitude + 0.004f), Quaternion.LookRotation(d, Vector3.up));
        }
        Vector3 tipDir = (b2 - b1).normalized;
        Frustum(Steel, b2, Quaternion.FromToRotation(Vector3.up, tipDir), new Vector2(0.006f, 0.017f), new Vector2(0.001f, 0.002f), 0.07f, 4);
        return Finish("SwordscatBlade", SwordSubmeshes);
    }

    /// <summary>The sheath alone (mouth at z 0.05, along +Z), with or without the trailing cord (sageo).</summary>
    public static Mesh BuildSheath(bool ribbon)
    {
        Begin();
        Sheathe(ribbon);
        return Finish("SwordscatSheath", SwordSubmeshes);
    }

    private static void Hilt(Quaternion alongZ)
    {
        // Hilt: light wrap with dark diamond bands, a brass pommel cap.
        Segment(Wrap, new Vector3(0f, 0f, -0.02f), new Vector3(0f, 0f, -HiltLength + 0.03f), 0.065f, 0.06f);
        for (int i = 0; i < 5; i++)
        {
            float z = -0.06f - i * 0.045f;
            Segment(WrapDark, new Vector3(0f, 0f, z), new Vector3(0f, 0f, z - 0.014f), 0.07f, 0.066f);
        }
        Segment(Guard, new Vector3(0f, 0f, -HiltLength + 0.03f), new Vector3(0f, 0f, -HiltLength), 0.072f, 0.068f);
        // Guard (tsuba) and collar.
        Frustum(Guard, new Vector3(0f, 0f, -0.012f), alongZ, new Vector2(0.08f, 0.068f), new Vector2(0.08f, 0.068f), 0.025f, 10);
        Segment(Guard, new Vector3(0f, 0f, 0.013f), new Vector3(0f, 0f, 0.05f), 0.058f, 0.056f);
    }

    private static void Sheathe(bool ribbon)
    {
        // Sheath, a little curved, with a brass end cap.
        Vector3 p0 = new Vector3(0f, 0f, 0.05f);
        Vector3 p1 = new Vector3(0f, 0.015f, 0.45f);
        Vector3 p2 = new Vector3(0f, 0.035f, SheathLength);
        Segment(Sheath, p0, p1, 0.058f, 0.054f);
        Segment(Sheath, p1, p2, 0.054f, 0.05f);
        Segment(Guard, p2, p2 + new Vector3(0f, 0.003f, 0.04f), 0.052f, 0.046f);
        // Cord band near the sheath's mouth; with the ribbon, a long flat cord trailing from it.
        Segment(ribbon ? Ribbon : WrapDark, new Vector3(0f, 0f, 0.12f), new Vector3(0f, 0.001f, 0.15f), 0.064f, 0.064f);
        if (ribbon)
        {
            Vector3[] path =
            {
                new Vector3(0f, -0.03f, 0.14f), new Vector3(0.04f, -0.1f, 0.2f), new Vector3(-0.02f, -0.12f, 0.32f),
                new Vector3(0.05f, -0.08f, 0.44f), new Vector3(0.0f, -0.13f, 0.56f), new Vector3(0.06f, -0.1f, 0.68f)
            };
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 d = path[i + 1] - path[i];
                Box(Ribbon, (path[i] + path[i + 1]) * 0.5f, new Vector3(0.035f, 0.006f, d.magnitude + 0.01f),
                    Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0f, 0f, i * 35f));
            }
        }
    }

    // ---- The sword wave (projectile).

    /// <summary>A glowing flat crescent flying along +Z (convex side forward), centred near the origin, in the slash's
    /// style (2026-11-25): a pale glow band, a white-hot front edge, speed-line streaks trailing behind. Vertex colours,
    /// one submesh, for an unlit vertex-colour material (SwordscatSlash, Sprites/Default, two-sided).</summary>
    public static Mesh BuildWave()
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        int n = 16;
        float radius = 0.62f, half = 68f * Mathf.Deg2Rad;
        Vector3 shift = new Vector3(0f, 0f, -radius * 0.8f);
        Color glow = new Color(0.82f, 0.92f, 1f, 0.8f);
        Color clear = new Color(0.82f, 0.92f, 1f, 0f);
        Color hot = Color.white;
        Color warm = new Color(1f, 1f, 1f, 0.35f);

        // Glow inside the edge, glow past it, then the white core on the edge.
        for (int layer = 0; layer < 3; layer++)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)n);
                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a / half * Mathf.PI * 0.5f)), 0.6f);
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                if (layer == 0)
                {
                    WavePoint(vertices, colors, triangles, shift, dir, radius - (0.26f * taper + 0.01f), radius, clear, glow, i > 0);
                }
                else if (layer == 1)
                {
                    WavePoint(vertices, colors, triangles, shift, dir, radius, radius + 0.08f * Mathf.Max(0.2f, taper), glow, clear, i > 0);
                }
                else
                {
                    WavePoint(vertices, colors, triangles, shift, dir, radius - (0.06f * taper + 0.005f), radius, warm, hot, i > 0);
                }
            }
        }

        // Speed lines trailing back from the crescent's inner side, fading out behind.
        float[] spots = { -0.45f, -0.15f, 0.15f, 0.45f };
        float[] lengths = { 0.45f, 0.7f, 0.65f, 0.4f };
        for (int k = 0; k < spots.Length; k++)
        {
            float a = spots[k] * half;
            Vector3 start = shift + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (radius - 0.18f);
            Vector3 end = start + Vector3.back * lengths[k];
            float w = 0.016f;
            int b = vertices.Count;
            vertices.Add(start + Vector3.left * w);
            vertices.Add(start + Vector3.right * w);
            vertices.Add(end + Vector3.left * w * 0.3f);
            vertices.Add(end + Vector3.right * w * 0.3f);
            Color c = new Color(1f, 1f, 1f, 0.6f);
            Color c0 = new Color(1f, 1f, 1f, 0f);
            colors.Add(c);
            colors.Add(c);
            colors.Add(c0);
            colors.Add(c0);
            triangles.Add(b);
            triangles.Add(b + 2);
            triangles.Add(b + 1);
            triangles.Add(b + 1);
            triangles.Add(b + 2);
            triangles.Add(b + 3);
        }

        Mesh mesh = new Mesh { name = "SwordscatWave" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        var normals = new List<Vector3>();
        for (int i = 0; i < vertices.Count; i++)
        {
            normals.Add(Vector3.up);
        }
        mesh.SetNormals(normals);
        mesh.subMeshCount = 1;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void WavePoint(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 shift, Vector3 dir,
        float innerRadius, float outerRadius, Color innerColor, Color outerColor, bool join)
    {
        vertices.Add(shift + dir * innerRadius);
        colors.Add(innerColor);
        vertices.Add(shift + dir * outerRadius);
        colors.Add(outerColor);
        if (join)
        {
            int k = vertices.Count - 4;
            triangles.Add(k);
            triangles.Add(k + 1);
            triangles.Add(k + 3);
            triangles.Add(k);
            triangles.Add(k + 3);
            triangles.Add(k + 2);
        }
    }

    // ---- The crates (tower root space).

    /// <summary>One crate per tile and a smaller one on top over the seam (top at DeckTop).</summary>
    public static Mesh BuildPedestal()
    {
        Begin();
        Crate(new Vector3(0f, 0f, 0f), new Vector3(0.95f, 0.95f, 0.95f), 0f);
        Crate(new Vector3(1f, 0f, 0.02f), new Vector3(0.92f, 0.95f, 0.9f), -5f);
        Crate(new Vector3(0.5f, 0.95f, -0.02f), new Vector3(0.78f, DeckTop - 0.95f, 0.78f), 7f);
        return Finish("SwordscatPedestal", 2);
    }

    // A wooden crate like the fence's lone-cell crates (same as TrapperModelBuilder.Crate).
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
        for (int k = 1; k < 3; k++)
        {
            float y = -h.y + size.y * k / 3f;
            for (int x = -1; x <= 1; x += 2)
            {
                Box(1, c + rot * new Vector3(x * (h.x + 0.005f), y, 0f), new Vector3(0.02f, 0.025f, size.z), rot);
            }
        }
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
            if (mesh.colors.Length > 0)
            {
                existing.SetColors(mesh.colors);
            }
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
        Debug.Log("SwordscatModelBuilder: wrote " + path);
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
