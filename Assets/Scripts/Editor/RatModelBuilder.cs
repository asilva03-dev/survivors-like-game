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
 * - Bag rat (2026-11-20, enemy layers step 3): Tools > Models > Build Bag Rat writes BagRatBody.asset (an upright,
 *   round-bellied rat, 1.3x the ranged rat) and BagRatSack.asset (a burlap sack resting over its right shoulder).
 * - Pot rat (2026-11-21, new rats): Tools > Models > Build Pot Rat writes PotCrew.asset (feet, tails and a nose of the
 *   3 rats under the pot) and PotShell.asset (the dented tin cooking pot, upside down).
 * - Athletic rat (2026-11-21, new rats step 4): Tools > Models > Build Athletic Rat writes the runner's 7 part meshes
 *   (Models/AthleticRat_<part>.asset: torso, arms, thighs, shins) for the jointed rig RatRunCycle animates.
 * - Swarm mouse (2026-11-21, new rats step 5): Tools > Models > Build Swarm Mouse writes SwarmMouse.asset, a tiny simple
 *   baby mouse (round body, snout, round pink ears, eye dots, short tail; 204 tris).
 * - Rat armor (2026-11-20, enemy layers): Tools > Models > Build Rat Armor writes RatHelmet.asset (red bottle cap),
 *   RatPlate.asset (iron lames on the back) and RatLowPolyNoEars.asset (the basic rat without ears, worn under the cap).
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
    private static readonly List<int>[] Triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };

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
        return Finish(name, 3);
    }

    // `submeshes` = how many of the material lists to use (3 for every model; 5 for the athletic rat's band colours).
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

    public static Mesh BuildMesh(bool ears = true)
    {
        Begin();
        AddRat(ears);
        return Finish(ears ? "RatLowPoly" : "RatLowPolyNoEars");
    }

    // The basic rat's parts into the current mesh lists.
    private static void AddRat(bool ears)
    {

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
            if (ears)
            {
                Quaternion tilt = Quaternion.Euler(-10f, side * 25f, side * -20f);
                Vector3 earPos = new Vector3(side * 0.13f, 0.48f, 0.34f);
                Box(Body, earPos, new Vector3(0.20f, 0.20f, 0.04f), tilt);
                Box(Pink, earPos + tilt * new Vector3(0f, 0f, 0.025f), new Vector3(0.14f, 0.14f, 0.02f), tilt);
            }

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

    }

    // ---- Bag rat (2026-11-20, enemy layers step 3; MOAB-style). User's picks: upright with a round belly, a burlap sack
    // resting over its right shoulder, nothing peeking out. Built like the ranged rat (standing), scaled up by BagScale.
    // Body mesh = rat submeshes (fur, pink, dark); sack mesh = its own (0 burlap, 1 rope, 2 dark).
    // Tools > Models > Build Bag Rat writes Models/BagRatBody.asset and Models/BagRatSack.asset.
    public const string BagBodyPath = "Assets/Models/BagRatBody.asset";
    public const string BagSackPath = "Assets/Models/BagRatSack.asset";
    private const float BagScale = 1.3f;
    private static readonly Vector3 BellyCentre = new Vector3(0f, 0.70f, 0.20f);
    private static readonly Vector3 BellyRadii = new Vector3(0.37f, 0.41f, 0.30f);

    [MenuItem("Tools/Models/Build Bag Rat")]
    public static void BuildBagRat()
    {
        Save(BuildBagRatBody(), BagBodyPath);
        Save(BuildBag(), BagSackPath);
    }
    // The sack's centre line (user: resting on the shoulder): the tied mouth held in front of the right shoulder, over the
    // top of the shoulder, then slumping down the back into a heavy bottom. Radii per point.
    private static readonly Vector3[] SackPath =
    {
        new Vector3(0.34f, 1.28f, 0.30f), new Vector3(0.38f, 1.42f, 0.14f), new Vector3(0.42f, 1.52f, -0.06f),
        new Vector3(0.42f, 1.46f, -0.28f), new Vector3(0.38f, 1.22f, -0.50f), new Vector3(0.32f, 0.94f, -0.62f),
        new Vector3(0.28f, 0.74f, -0.62f)
    };
    private static readonly float[] SackRadii = { 0.10f, 0.13f, 0.19f, 0.27f, 0.37f, 0.40f, 0.22f };

    public static Mesh BuildBagRatBody()
    {
        Begin();
        Vector3 bellyCentre = BellyCentre;
        Vector3 bellyRadii = BellyRadii;

        // Haunches with long pink hind feet.
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Body, new Vector3(side * 0.29f, 0.22f, 0.0f), new Vector3(0.26f, 0.44f, 0.48f), Quaternion.Euler(-8f, 0f, 0f));
            Box(Pink, new Vector3(side * 0.29f, 0.03f, 0.26f), new Vector3(0.18f, 0.06f, 0.38f), Quaternion.identity);
        }

        // Upright body (as the ranged rat) and a round belly pushing out in front.
        VHex(Body, 0.12f, new Vector3(0.36f, -0.34f, 0.16f), 0.75f, new Vector3(0.38f, -0.30f, 0.20f));
        VHex(Body, 0.75f, new Vector3(0.38f, -0.30f, 0.20f), 1.22f, new Vector3(0.28f, -0.20f, 0.22f));
        VHex(Body, 1.22f, new Vector3(0.28f, -0.20f, 0.22f), 1.34f, new Vector3(0.24f, -0.17f, 0.20f));
        Ball(Body, bellyCentre, bellyRadii, 10);
        Ball(Pink, bellyCentre + new Vector3(0f, -0.02f, bellyRadii.z * 0.9f), new Vector3(0.06f, 0.06f, 0.03f), 6);

        // Head looking forward: wedge + snout + nose.
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

        // Right paw holding the sack's tied mouth in front of the shoulder; left paw resting on the belly.
        Vector3 grip = SackPath[0] + new Vector3(0f, -0.03f, 0.02f);
        Segment(Body, new Vector3(0.30f, 1.16f, 0.02f), new Vector3(0.42f, 1.02f, 0.22f), 0.15f, 0.13f);
        Segment(Body, new Vector3(0.42f, 1.02f, 0.22f), grip, 0.13f, 0.11f);
        Box(Pink, grip, new Vector3(0.13f, 0.12f, 0.12f), Quaternion.identity);
        Vector3 bellyPaw = bellyCentre + new Vector3(-0.16f, 0.12f, bellyRadii.z * 0.92f);
        Segment(Body, new Vector3(-0.32f, 1.16f, 0.02f), new Vector3(-0.36f, 0.94f, bellyCentre.z + 0.05f), 0.15f, 0.13f);
        Segment(Body, new Vector3(-0.36f, 0.94f, bellyCentre.z + 0.05f), bellyPaw, 0.13f, 0.11f);
        Box(Pink, bellyPaw, new Vector3(0.12f, 0.11f, 0.10f), Quaternion.identity);

        // Tail from the base of the back, curling along the ground.
        Vector3 start = new Vector3(0f, 0.20f, -0.32f);
        Vector3 dir = Quaternion.Euler(-20f, 0f, 0f) * Vector3.back;
        float thick = 0.10f;
        float[] lengths = { 0.28f, 0.26f, 0.24f, 0.22f, 0.20f };
        for (int i = 0; i < lengths.Length; i++)
        {
            Vector3 next = start + dir * lengths[i];
            next.y = Mathf.Max(next.y, 0.04f);
            float end = thick * 0.72f;
            Segment(Pink, start, next, thick, end);
            start = next;
            thick = end;
            dir = Quaternion.Euler(-4f, -18f, 0f) * dir;
            dir.y = Mathf.Min(dir.y, 0f);
            dir.Normalize();
        }

        ScaleAll(BagScale);
        return Finish("BagRatBody");
    }

    // The burlap sack resting over the right shoulder: a bent tube along SackPath (frustum per stretch, a ball at each
    // bend so there are no gaps), the mouth in front tied with rope and flared, the heavy bottom down the back.
    public static Mesh BuildBag()
    {
        Begin();
        for (int i = 0; i < SackPath.Length - 1; i++)
        {
            Vector3 a = SackPath[i], b = SackPath[i + 1];
            Quaternion along = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            float r0 = SackRadii[i], r1 = SackRadii[i + 1];
            Frustum(Body, a, along, new Vector2(r0, r0 * 0.92f), new Vector2(r1, r1 * 0.92f), (b - a).magnitude, 9);
            if (i > 0)
            {
                Ball(Body, a, new Vector3(r0, r0 * 0.95f, r0), 9);
            }
        }
        Vector3 last = SackPath[SackPath.Length - 1];
        Frustum(Body, last, Quaternion.FromToRotation(Vector3.up, Vector3.down), new Vector2(0.22f, 0.2f), new Vector2(0.08f, 0.07f), 0.07f, 9);

        // Rope round the neck and the flared mouth pointing forward and down from the paw.
        Vector3 mouthDir = (SackPath[0] - SackPath[1]).normalized;
        Quaternion mouth = Quaternion.FromToRotation(Vector3.up, mouthDir);
        Frustum(Pink, SackPath[0] - mouthDir * 0.06f, mouth, new Vector2(0.115f, 0.105f), new Vector2(0.115f, 0.105f), 0.05f, 9);
        Frustum(Body, SackPath[0], mouth, new Vector2(0.09f, 0.08f), new Vector2(0.16f, 0.14f), 0.08f, 9);

        // A darker patch and a stitch line on the bulk.
        Box(Pink, SackPath[5] + new Vector3(0.36f, 0.06f, 0.02f), new Vector3(0.05f, 0.14f, 0.16f), Quaternion.Euler(0f, 0f, 8f));
        Box(Pink, SackPath[4] + new Vector3(0.05f, 0.08f, -0.34f), new Vector3(0.20f, 0.05f, 0.05f), Quaternion.Euler(25f, 0f, 0f));
        ScaleAll(BagScale);
        return Finish("BagRatSack");
    }

    private static void ScaleAll(float scale)
    {
        for (int i = 0; i < Vertices.Count; i++)
        {
            Vertices[i] *= scale;
        }
    }

    // A faceted ellipsoid from stacked frustums.
    private static void Ball(int material, Vector3 centre, Vector3 radii, int sides)
    {
        float[] t = { -1f, -0.7f, -0.25f, 0.25f, 0.7f, 1f };
        for (int i = 0; i < t.Length - 1; i++)
        {
            float k0 = Mathf.Sqrt(Mathf.Max(0f, 1f - t[i] * t[i]));
            float k1 = Mathf.Sqrt(Mathf.Max(0f, 1f - t[i + 1] * t[i + 1]));
            Vector3 bottom = centre + new Vector3(0f, t[i] * radii.y, 0f);
            Frustum(material, bottom, Quaternion.identity, new Vector2(radii.x * k0, radii.z * k0), new Vector2(radii.x * k1, radii.z * k1), (t[i + 1] - t[i]) * radii.y, sides);
        }
    }

    // ---- Pot rat (2026-11-21, new rats step 3; user's pick: the dented tin cooking pot). An upside-down pot carried by
    // the rats underneath (3 of them): pink feet under the rim, tails out the back, a nose peeking out the front. Crew
    // mesh = rat submeshes (fur, pink, dark); shell mesh = its own (0 tin, 1 light tin: rim and dents, 2 dark: handle).
    // Tools > Models > Build Pot Rat writes Models/PotCrew.asset and Models/PotShell.asset.
    public const string PotCrewPath = "Assets/Models/PotCrew.asset";
    public const string PotShellPath = "Assets/Models/PotShell.asset";

    [MenuItem("Tools/Models/Build Pot Rat")]
    public static void BuildPotRat()
    {
        Save(BuildPotCrew(), PotCrewPath);
        Save(BuildPotShell(), PotShellPath);
    }

    public static Mesh BuildPotCrew()
    {
        Begin();
        // Feet: three pairs under the rim.
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 30f) * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Sin(a) * 0.48f, 0.03f, Mathf.Cos(a) * 0.48f);
            Box(Pink, p, new Vector3(0.11f, 0.06f, 0.16f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f));
            Box(Body, p + new Vector3(0f, 0.08f, 0f) - p.normalized * 0.04f, new Vector3(0.10f, 0.10f, 0.12f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f));
        }
        // Nose and whiskers peeking out at the front.
        Hex(Body, 0.42f, new Vector3(0.10f, 0.03f, 0.17f), 0.62f, new Vector3(0.05f, 0.05f, 0.13f));
        Box(Pink, new Vector3(0f, 0.09f, 0.64f), new Vector3(0.07f, 0.06f, 0.05f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Dark, new Vector3(side * 0.05f, 0.14f, 0.50f), new Vector3(0.04f, 0.04f, 0.04f), Quaternion.identity);
            Quaternion fan = Quaternion.Euler(0f, side * 70f, 0f);
            Box(Dark, new Vector3(side * 0.04f, 0.09f, 0.60f) + fan * new Vector3(0f, 0f, 0.11f), new Vector3(0.01f, 0.01f, 0.22f), fan);
        }
        // Three tails out of the back, curling along the ground.
        for (int t = 0; t < 3; t++)
        {
            Vector3 start = new Vector3((t - 1) * 0.20f, 0.06f, -0.50f);
            Vector3 dir = Quaternion.Euler(0f, (t - 1) * 25f, 0f) * Vector3.back;
            float thick = 0.06f;
            for (int k = 0; k < 4; k++)
            {
                Vector3 next = start + dir * 0.17f;
                next.y = 0.03f;
                Segment(Pink, start, next, thick, thick * 0.75f);
                start = next;
                thick *= 0.75f;
                dir = Quaternion.Euler(0f, (t - 1) * 14f + 10f, 0f) * dir;
            }
        }
        return Finish("PotCrew");
    }

    public static Mesh BuildPotShell()
    {
        Begin();
        float y0 = 0.10f;
        // Dented tin cooking pot: straight sides, rolled rim, a long handle off the back, two dents.
        Frustum(Pink, new Vector3(0f, y0, 0f), Quaternion.identity, new Vector2(0.60f, 0.60f), new Vector2(0.60f, 0.60f), 0.05f, 12);
        Frustum(Body, new Vector3(0f, y0 + 0.05f, 0f), Quaternion.identity, new Vector2(0.57f, 0.57f), new Vector2(0.55f, 0.55f), 0.44f, 12);
        Frustum(Body, new Vector3(0f, y0 + 0.49f, 0f), Quaternion.identity, new Vector2(0.55f, 0.55f), new Vector2(0.48f, 0.48f), 0.05f, 12);
        Segment(Dark, new Vector3(0f, y0 + 0.30f, -0.55f), new Vector3(0f, y0 + 0.24f, -1.05f), 0.07f, 0.06f);
        Box(Pink, new Vector3(0.36f, y0 + 0.28f, 0.42f), new Vector3(0.14f, 0.12f, 0.03f), Quaternion.Euler(0f, 40f, 0f));
        Box(Pink, new Vector3(-0.48f, y0 + 0.36f, -0.10f), new Vector3(0.03f, 0.10f, 0.14f), Quaternion.Euler(0f, -10f, 0f));
        return Finish("PotShell");
    }

    // ---- Shadow veil (2026-11-21, shadow rat): a smooth unit sphere (radius 0.5, smooth normals) that the shadow rats
    // wear scaled around their body with the Custom/ShadowVeil material. Tools > Models > Build Shadow Veil.
    public const string VeilPath = "Assets/Models/ShadowVeil.asset";

    [MenuItem("Tools/Models/Build Shadow Veil")]
    public static void BuildVeil()
    {
        Save(BuildVeilMesh(), VeilPath);
    }

    public static Mesh BuildVeilMesh()
    {
        const int rings = 10;
        const int segments = 16;
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> tris = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float lat = Mathf.PI * r / rings;
            for (int s = 0; s <= segments; s++)
            {
                float lon = 2f * Mathf.PI * s / segments;
                Vector3 n = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                verts.Add(n * 0.5f);
                normals.Add(n);
            }
        }
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s;
                int b = a + segments + 1;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
            }
        }
        Mesh mesh = new Mesh { name = "ShadowVeil" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---- Athletic rat (2026-11-21, new rats step 4; user's pick: option B, the upright runner, with a real run cycle so
    // it reads as "he's sprinting for the base"). Built as separate parts so Enemies/RatRunCycle can swing them:
    // Torso (body, head, laid-back ears, headband, tail), ArmL/ArmR (bent at the elbow, paw, wristband), ThighL/ThighR and
    // ShinL/ShinR (shin + foot). Rest pose = standing straight; RatRunCycle leans, bounces and swings the parts.
    // Every part uses the same 5 materials: 0 fur, 1 pink, 2 dark (the rat's), 3 band (white), 4 band stripe (red).
    // Each part mesh is in its joint's space (vertex = rat position - joint) and scaled by RunnerScale (small rat).
    public const float RunnerScale = 0.6f;
    private const int Band = 3;
    private const int Stripe = 4;
    public static readonly string[] RunnerParts = { "Torso", "ArmL", "ArmR", "ThighL", "ThighR", "ShinL", "ShinR" };

    // Joint of each part in rat units (feet y 0, nose +Z), before RunnerScale. The hips joint = the torso's.
    public static Vector3 RunnerJoint(string part)
    {
        switch (part)
        {
            case "Torso": return new Vector3(0f, 0.58f, 0f);
            case "ArmL": return new Vector3(-0.32f, 1.16f, 0f);
            case "ArmR": return new Vector3(0.32f, 1.16f, 0f);
            case "ThighL": return new Vector3(-0.20f, 0.58f, 0f);
            case "ThighR": return new Vector3(0.20f, 0.58f, 0f);
            case "ShinL": return new Vector3(-0.20f, 0.31f, 0.03f);
            case "ShinR": return new Vector3(0.20f, 0.31f, 0.03f);
        }
        return Vector3.zero;
    }

    // Parent part in the rig ("" = the Hips transform).
    public static string RunnerParent(string part)
    {
        if (part.StartsWith("Arm"))
        {
            return "Torso";
        }
        if (part.StartsWith("Shin"))
        {
            return "Thigh" + part.Substring(4);
        }
        return "";
    }

    public static string RunnerMeshPath(string part)
    {
        return "Assets/Models/AthleticRat_" + part + ".asset";
    }

    [MenuItem("Tools/Models/Build Athletic Rat")]
    public static void BuildAthleticRat()
    {
        foreach (string part in RunnerParts)
        {
            Save(BuildRunnerPart(part), RunnerMeshPath(part));
        }
    }

    public static Mesh BuildRunnerPart(string part)
    {
        Begin();
        float side = part.EndsWith("L") ? -1f : 1f;
        if (part == "Torso")
        {
            AddRunnerTorso();
        }
        else if (part.StartsWith("Arm"))
        {
            AddRunnerArm(side);
        }
        else if (part.StartsWith("Thigh"))
        {
            Segment(Body, new Vector3(side * 0.20f, 0.64f, 0f), new Vector3(side * 0.20f, 0.31f, 0.03f), 0.21f, 0.14f);
        }
        else
        {
            Segment(Body, new Vector3(side * 0.20f, 0.33f, 0.03f), new Vector3(side * 0.20f, 0.07f, 0f), 0.13f, 0.10f);
            Box(Pink, new Vector3(side * 0.20f, 0.035f, 0.08f), new Vector3(0.15f, 0.07f, 0.28f), Quaternion.identity);
        }
        Vector3 joint = RunnerJoint(part);
        for (int i = 0; i < Vertices.Count; i++)
        {
            Vertices[i] = (Vertices[i] - joint) * RunnerScale;
        }
        return Finish("Runner" + part, 5);
    }

    // Builds the rig under `parent` (Visual > RatModel): Hips (no mesh) > Torso > ArmL/ArmR, Hips > ThighL/ThighR >
    // ShinL/ShinR. meshes in RunnerParts order (null = build them in memory). Returns the Hips transform.
    public static Transform CreateRunnerRig(Transform parent, Material[] materials, Mesh[] meshes)
    {
        Transform hips = new GameObject("Hips").transform;
        hips.SetParent(parent, false);
        hips.localPosition = RunnerJoint("Torso") * RunnerScale;
        Dictionary<string, Transform> made = new Dictionary<string, Transform>();
        for (int i = 0; i < RunnerParts.Length; i++)
        {
            string part = RunnerParts[i];
            string parentName = RunnerParent(part);
            Transform up = parentName == "" ? hips : made[parentName];
            Vector3 upJoint = parentName == "" ? RunnerJoint("Torso") : RunnerJoint(parentName);
            Transform t = new GameObject(part).transform;
            t.SetParent(up, false);
            t.localPosition = (RunnerJoint(part) - upJoint) * RunnerScale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes != null && meshes[i] != null ? meshes[i] : BuildRunnerPart(part);
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            made[part] = t;
        }
        return hips;
    }

    // Body, head (laid-back ears for speed), headband with knot tails, tail.
    private static void AddRunnerTorso()
    {
        VHex(Body, 0.42f, new Vector3(0.32f, -0.26f, 0.24f), 0.85f, new Vector3(0.32f, -0.25f, 0.27f));
        VHex(Body, 0.85f, new Vector3(0.32f, -0.25f, 0.27f), 1.22f, new Vector3(0.26f, -0.18f, 0.24f));
        VHex(Body, 1.22f, new Vector3(0.26f, -0.18f, 0.24f), 1.34f, new Vector3(0.22f, -0.15f, 0.20f));

        Hex(Body, -0.17f, new Vector3(0.24f, 1.28f, 1.66f), 0.20f, new Vector3(0.18f, 1.32f, 1.60f));
        Hex(Body, 0.20f, new Vector3(0.18f, 1.32f, 1.60f), 0.44f, new Vector3(0.07f, 1.40f, 1.51f));
        Box(Pink, new Vector3(0f, 1.455f, 0.46f), new Vector3(0.10f, 0.08f, 0.06f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion tilt = Quaternion.Euler(-50f, side * 20f, side * -25f);
            Vector3 earPos = new Vector3(side * 0.17f, 1.72f, -0.10f);
            Box(Body, earPos, new Vector3(0.28f, 0.28f, 0.05f), tilt);
            Box(Pink, earPos + tilt * new Vector3(0f, 0f, 0.03f), new Vector3(0.20f, 0.20f, 0.02f), tilt);
            Box(Dark, new Vector3(side * 0.165f, 1.52f, 0.13f), new Vector3(0.06f, 0.07f, 0.07f), Quaternion.identity);
            for (int k = 0; k < 2; k++)
            {
                Quaternion fan = Quaternion.Euler(k == 0 ? -6f : 8f, side * (65f + k * 15f), 0f);
                Vector3 root = new Vector3(side * 0.06f, 1.44f + k * 0.03f, 0.40f);
                Box(Dark, root + fan * new Vector3(0f, 0f, 0.15f), new Vector3(0.014f, 0.014f, 0.30f), fan);
            }
        }

        // Headband across the forehead (white, red stripe), knot at the back with two tails.
        Hex(Band, 0.00f, new Vector3(0.235f, 1.46f, 1.655f), 0.12f, new Vector3(0.215f, 1.46f, 1.635f));
        Hex(Stripe, 0.045f, new Vector3(0.24f, 1.53f, 1.66f), 0.075f, new Vector3(0.235f, 1.53f, 1.655f));
        Box(Band, new Vector3(0f, 1.56f, -0.19f), new Vector3(0.12f, 0.10f, 0.08f), Quaternion.identity);
        Segment(Band, new Vector3(0.03f, 1.56f, -0.21f), new Vector3(0.16f, 1.46f, -0.40f), 0.07f, 0.06f);
        Segment(Band, new Vector3(-0.03f, 1.56f, -0.21f), new Vector3(-0.14f, 1.42f, -0.38f), 0.07f, 0.06f);

        Vector3 start = new Vector3(0f, 0.50f, -0.26f);
        Vector3 dir = Quaternion.Euler(-10f, 0f, 0f) * Vector3.back;
        float thick = 0.10f;
        for (int i = 0; i < 5; i++)
        {
            Vector3 next = start + dir * 0.24f;
            float end = thick * 0.75f;
            Segment(Pink, start, next, thick, end);
            start = next;
            thick = end;
            dir = Quaternion.Euler(-2f, 12f, 0f) * dir;
            dir.Normalize();
        }
    }

    // Upper arm down to the elbow, forearm bent forward, paw, white wristband.
    private static void AddRunnerArm(float side)
    {
        Vector3 shoulder = RunnerJoint(side < 0f ? "ArmL" : "ArmR");
        Vector3 elbow = new Vector3(side * 0.39f, 0.88f, -0.03f);
        Vector3 wrist = new Vector3(side * 0.37f, 0.98f, 0.27f);
        Segment(Body, shoulder, elbow, 0.16f, 0.14f);
        Segment(Body, elbow, wrist, 0.14f, 0.13f);
        Box(Pink, wrist + new Vector3(0f, 0.01f, 0.06f), new Vector3(0.14f, 0.14f, 0.12f), Quaternion.identity);
        Segment(Band, Vector3.Lerp(elbow, wrist, 0.55f), Vector3.Lerp(elbow, wrist, 0.9f), 0.17f, 0.17f);
    }

    // ---- Swarm mouse (2026-11-21, new rats step 5; user's pick: a tiny, simple baby mouse). Feet y 0, nose +Z, world
    // units (the SwarmMouse prefab's Visual is unscaled). Submeshes 0 fur, 1 pink, 2 dark. ~0.6 long with the tail.
    public const string SwarmMousePath = "Assets/Models/SwarmMouse.asset";

    [MenuItem("Tools/Models/Build Swarm Mouse")]
    public static void BuildSwarmMouse()
    {
        Begin();
        AddBabyMouse();
        Save(Finish("SwarmMouse"), SwarmMousePath);
    }

    // One round body with a small snout, two plain pink round ears, eye dots, a short two-piece tail. 204 tris.
    private static void AddBabyMouse()
    {
        Ball(Body, new Vector3(0f, 0.11f, -0.02f), new Vector3(0.11f, 0.10f, 0.14f), 6);
        Hex(Body, 0.09f, new Vector3(0.07f, 0.04f, 0.17f), 0.21f, new Vector3(0.025f, 0.07f, 0.12f));
        Box(Pink, new Vector3(0f, 0.095f, 0.215f), new Vector3(0.035f, 0.03f, 0.02f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion tilt = Quaternion.Euler(-105f, side * 20f, side * -15f);
            Frustum(Pink, new Vector3(side * 0.075f, 0.19f, 0.05f), tilt, new Vector2(0.07f, 0.07f), new Vector2(0.07f, 0.07f), 0.015f, 6);
            Box(Dark, new Vector3(side * 0.055f, 0.14f, 0.11f), new Vector3(0.03f, 0.035f, 0.03f), Quaternion.identity);
        }
        Segment(Pink, new Vector3(0f, 0.06f, -0.15f), new Vector3(0.03f, 0.03f, -0.28f), 0.025f, 0.02f);
        Segment(Pink, new Vector3(0.03f, 0.03f, -0.28f), new Vector3(0.09f, 0.02f, -0.37f), 0.02f, 0.015f);
    }

    // ---- Armor pieces (2026-11-20, enemy layers step 2; user's pick: option 3's bottle cap, option 1's iron lames). Same
    // space as BuildMesh (feet y 0, nose +Z), so a piece sits right as a child of RatModel. Submeshes: 0 main, 1 accent,
    // 2 dark. A helmeted rat wears the earless body (BuildMesh(false)): the ears are under the cap.
    public const string HelmetPath = "Assets/Models/RatHelmet.asset";
    public const string PlatePath = "Assets/Models/RatPlate.asset";
    public const string NoEarsPath = "Assets/Models/RatLowPolyNoEars.asset";

    [MenuItem("Tools/Models/Build Rat Armor")]
    public static void BuildArmor()
    {
        Save(BuildHelmet(), HelmetPath);
        Save(BuildPlate(), PlatePath);
        Save(BuildMesh(false), NoEarsPath);
    }

    // A red bottle cap pulled down on the head, tipped a little: crimped skirt and a darker inner top.
    public static Mesh BuildHelmet()
    {
        Begin();
        Quaternion tip = Quaternion.Euler(-8f, 0f, 6f);
        Vector3 c = new Vector3(0f, 0.36f, 0.41f);
        Frustum(Body, c, tip, new Vector2(0.21f, 0.21f), new Vector2(0.205f, 0.205f), 0.09f, 14);
        for (int i = 0; i < 14; i++)
        {
            Quaternion around = Quaternion.Euler(0f, i * (360f / 14f), 0f);
            Box(Pink, c + tip * (around * new Vector3(0f, 0.045f, 0.212f)), new Vector3(0.05f, 0.09f, 0.025f), tip * around);
        }
        Frustum(Pink, c + tip * new Vector3(0f, 0.09f, 0f), tip, new Vector2(0.15f, 0.15f), new Vector2(0.15f, 0.15f), 0.008f, 14);
        return Finish("RatHelmet");
    }

    // Three overlapping iron lames down the back (rump to shoulders), side skirts and rivets.
    public static Mesh BuildPlate()
    {
        Begin();
        float[] z = { -0.44f, -0.22f, 0.00f };
        for (int i = 0; i < z.Length; i++)
        {
            float top = 0.535f - i * 0.02f;
            Hex(Body, z[i], new Vector3(0.30f, top - 0.05f, top), z[i] + 0.25f, new Vector3(0.29f, top - 0.07f, top - 0.02f));
            Hex(Pink, z[i] + 0.21f, new Vector3(0.305f, top - 0.065f, top + 0.005f), z[i] + 0.25f, new Vector3(0.295f, top - 0.075f, top - 0.012f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(Dark, new Vector3(side * 0.22f, top + 0.005f, z[i] + 0.08f), new Vector3(0.04f, 0.02f, 0.04f), Quaternion.identity);
                Box(Body, new Vector3(side * 0.31f, top - 0.13f, z[i] + 0.125f), new Vector3(0.035f, 0.17f, 0.24f), Quaternion.Euler(0f, 0f, side * -12f));
            }
        }
        return Finish("RatPlate");
    }

    // An n-sided flat-shaded frustum: bottom centre c, radii (x, z) at the bottom and top, height h along rot's up, capped.
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
        if (r1.x > 0.001f)
        {
            AddPoly(material, top, centre);
        }
        if (r0.x > 0.001f)
        {
            AddPoly(material, bottom, centre);
        }
    }

    // A flat polygon (fan), facing away from `inside`.
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
