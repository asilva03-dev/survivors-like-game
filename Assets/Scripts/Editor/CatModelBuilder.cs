/*
 * CatModelBuilder
 * Builds the player cat mesh (2026-11-20, user: a cuter, more cat-like round cat; picked option D of four, then tuned
 * it; the other options and the tabby stripes were removed). Built from smooth ellipsoids and rounded ear prisms, in
 * world units with the feet on y 0 and the face toward +Z (the scene's CatModelD turns it 180 degrees, the rest pose
 * PlayerFacing expects).
 *
 * - One navy ball; the face sits high on the front (facePitch) so it reads from the 68 degree game camera: big shiny
 *   eyes, two white cheek puffs with a pink nose and three whiskers a side, a white chest patch. No tail, no feet.
 * - Ears: short rounded-tip cat ears, built once and mirrored so both sides always match, set 52 degrees to the sides,
 *   tilted outward and tipped back so their pink inside faces the game camera.
 * - Submeshes: 0 fur, 1 white, 2 pink, 3 dark (eyes, whiskers).
 * - Tools > Models > Cat > Build Player Cat rewrites Assets/Models/CatVariantD.asset in place (the scene keeps its link).
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CatModelBuilder
{
    public const string MeshPath = "Assets/Models/CatVariantD.asset";

    private const int Fur = 0;
    private const int White = 1;
    private const int Pink = 2;
    private const int Dark = 3;
    private const int MaterialCount = 4;

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles =
    {
        new List<int>(), new List<int>(), new List<int>(), new List<int>()
    };

    private struct Ball
    {
        public Vector3 centre;
        public Vector3 radii;
    }

    [MenuItem("Tools/Models/Cat/Build Player Cat")]
    public static void Save()
    {
        Mesh mesh = Build();
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
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
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CatModelBuilder: wrote " + MeshPath);
    }

    public static Mesh Build()
    {
        Vertices.Clear();
        Normals.Clear();
        foreach (List<int> list in Triangles)
        {
            list.Clear();
        }

        Ball head = new Ball { centre = new Vector3(0f, 0.86f, 0f), radii = new Vector3(0.84f, 0.86f, 0.82f) };
        Ellipsoid(Fur, head.centre, head.radii, Quaternion.identity, 16, 10);

        // The face sits high on the front so it reads from the 68 degree game camera.
        const float facePitch = 30f;

        // Ears: the right ear built once and mirrored for the left (user: symmetric), 52 degrees to the side (user: a
        // bit farther apart), tilted outward and tipped back so the pink inside faces the game camera; shorter than
        // wide with a rounded tip (user: shorter, less pointy).
        const float earSize = 0.50f;
        {
            Vector3 normal;
            Vector3 basePoint = OnSurface(head, 52f, facePitch + 30f, out normal);
            Vector3 up = (normal * 0.3f + Vector3.up * 0.6f + Vector3.right * 0.32f - Vector3.forward * 0.25f).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            int[] before = SubmeshCounts();
            int firstVertex = Vertices.Count;
            Ear(basePoint - up * 0.07f, up, forward, earSize, earSize * 0.78f, 0.12f);
            MirrorX(firstVertex, before);
        }

        // Eyes: dark ovals with a white shine.
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 normal;
            Vector3 p = OnSurface(head, side * 24f, facePitch + 8f, out normal);
            Quaternion onFace = Quaternion.LookRotation(normal, Vector3.up);
            Ellipsoid(Dark, p + normal * 0.01f, new Vector3(0.13f, 0.17f, 0.05f), onFace, 10, 6);
            Ellipsoid(White, p + normal * 0.05f + (onFace * new Vector3(-side * 0.035f, 0.06f, 0f)),
                new Vector3(0.045f, 0.045f, 0.02f), onFace, 6, 4);
        }

        // Muzzle: two white cheek puffs, a pink nose on top, whiskers out of the cheeks.
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 normal;
            Vector3 cheek = OnSurface(head, side * 8f, facePitch - 10f, out normal);
            Ellipsoid(White, cheek + normal * 0.02f, new Vector3(0.13f, 0.10f, 0.09f),
                Quaternion.LookRotation(normal, Vector3.up), 8, 6);
            for (int w = -1; w <= 1; w++)
            {
                Vector3 dir = Quaternion.Euler(w * 10f, side * 80f, 0f) * Vector3.forward;
                Vector3 root = cheek + normal * 0.04f + Vector3.right * side * 0.08f;
                Stick(Dark, root, root + dir * 0.34f, 0.016f);
            }
        }
        {
            Vector3 normal;
            Vector3 nose = OnSurface(head, 0f, facePitch - 3f, out normal);
            Ellipsoid(Pink, nose + normal * 0.05f, new Vector3(0.07f, 0.05f, 0.05f), Quaternion.LookRotation(normal, Vector3.up), 6, 4);
        }

        // A small white chest patch.
        {
            Vector3 normal;
            Vector3 chest = OnSurface(head, 0f, -14f, out normal);
            Ellipsoid(White, chest - normal * 0.05f, new Vector3(0.28f, 0.22f, 0.08f), Quaternion.LookRotation(normal, Vector3.up), 10, 6);
        }

        Mesh mesh = new Mesh { name = "PlayerCat" };
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.subMeshCount = MaterialCount;
        for (int i = 0; i < MaterialCount; i++)
        {
            mesh.SetTriangles(Triangles[i], i);
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    // A point on a ball's surface: yaw (0 = +Z face, + = toward +X) and pitch (+ = up) in degrees; outward normal.
    private static Vector3 OnSurface(Ball ball, float yaw, float pitch, out Vector3 normal)
    {
        float y = yaw * Mathf.Deg2Rad;
        float p = pitch * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p));
        Vector3 point = Vector3.Scale(dir, ball.radii);
        normal = new Vector3(point.x / (ball.radii.x * ball.radii.x), point.y / (ball.radii.y * ball.radii.y),
            point.z / (ball.radii.z * ball.radii.z)).normalized;
        return ball.centre + point;
    }

    // Smooth ellipsoid with `lon` x `lat` segments.
    private static void Ellipsoid(int material, Vector3 centre, Vector3 radii, Quaternion rotation, int lon, int lat)
    {
        int start = Vertices.Count;
        for (int i = 0; i <= lat; i++)
        {
            float v = Mathf.PI * i / lat - Mathf.PI * 0.5f;
            for (int j = 0; j <= lon; j++)
            {
                float u = 2f * Mathf.PI * j / lon;
                Vector3 dir = new Vector3(Mathf.Cos(v) * Mathf.Sin(u), Mathf.Sin(v), Mathf.Cos(v) * Mathf.Cos(u));
                Vector3 local = Vector3.Scale(dir, radii);
                Vector3 n = new Vector3(local.x / (radii.x * radii.x), local.y / (radii.y * radii.y), local.z / (radii.z * radii.z));
                Vertices.Add(centre + rotation * local);
                Normals.Add((rotation * n).normalized);
            }
        }
        List<int> tris = Triangles[material];
        int row = lon + 1;
        for (int i = 0; i < lat; i++)
        {
            for (int j = 0; j < lon; j++)
            {
                int a = start + i * row + j;
                int b = a + 1;
                int c = a + row;
                int d = c + 1;
                // Outward-facing (clockwise seen from outside).
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }
    }

    // A thin stick (whisker) from a to b.
    private static void Stick(int material, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 mid = (a + b) * 0.5f;
        Vector3 dir = b - a;
        Ellipsoid(material, mid, new Vector3(thickness, thickness, dir.magnitude * 0.5f), Quaternion.LookRotation(dir.normalized, Vector3.up), 4, 3);
    }

    // A cat ear with a rounded tip (outline y = height x (1 - |u|^2.4) across the base, so the sides curve in to a soft
    // point) and some thickness: base on the head, rising along `up`, facing `forward`; a smaller pink ear inside.
    private static void Ear(Vector3 basePoint, Vector3 up, Vector3 forward, float width, float height, float thickness)
    {
        const int Segments = 12;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 back = -forward * thickness;
        Vector3[] outline = new Vector3[Segments + 1];
        for (int i = 0; i <= Segments; i++)
        {
            float u = 1f - 2f * i / Segments;
            float y = height * (1f - Mathf.Pow(Mathf.Abs(u), 2.4f));
            float x = width * 0.5f * u;
            outline[i] = basePoint + right * x + up * y;
        }

        // Front and back faces as fans from the base centre, the rim as quads, and the base.
        for (int i = 0; i < Segments; i++)
        {
            Tri(Fur, basePoint, outline[i], outline[i + 1], forward);
            Tri(Fur, basePoint + back, outline[i] + back, outline[i + 1] + back, -forward);
            Vector3 rimOut = Vector3.Cross(outline[i + 1] - outline[i], forward);
            Vector3 mid = (outline[i] + outline[i + 1]) * 0.5f - basePoint;
            if (Vector3.Dot(rimOut, mid) < 0f)
            {
                rimOut = -rimOut;
            }
            Tri(Fur, outline[i], outline[i + 1], outline[i + 1] + back, rimOut);
            Tri(Fur, outline[i], outline[i + 1] + back, outline[i] + back, rimOut);
        }
        Tri(Fur, outline[0], outline[Segments], outline[Segments] + back, -up);
        Tri(Fur, outline[0], outline[Segments] + back, outline[0] + back, -up);

        // Pink inside: the same outline shrunk toward its middle, just in front.
        Vector3 centre = basePoint + up * (height * 0.36f) + forward * 0.012f;
        for (int i = 0; i < Segments; i++)
        {
            Vector3 a = Vector3.Lerp(centre, outline[i] + forward * 0.012f, 0.68f);
            Vector3 b = Vector3.Lerp(centre, outline[i + 1] + forward * 0.012f, 0.68f);
            Tri(Pink, centre, a, b, forward);
        }
    }

    private static int[] SubmeshCounts()
    {
        int[] counts = new int[MaterialCount];
        for (int i = 0; i < MaterialCount; i++)
        {
            counts[i] = Triangles[i].Count;
        }
        return counts;
    }

    // Copies everything added since (firstVertex, triangle counts) mirrored across x = 0, with the winding flipped so
    // the copy still faces outward.
    private static void MirrorX(int firstVertex, int[] trianglesBefore)
    {
        int lastVertex = Vertices.Count;
        int offset = lastVertex - firstVertex;
        for (int v = firstVertex; v < lastVertex; v++)
        {
            Vector3 p = Vertices[v];
            Vector3 n = Normals[v];
            Vertices.Add(new Vector3(-p.x, p.y, p.z));
            Normals.Add(new Vector3(-n.x, n.y, n.z));
        }
        for (int m = 0; m < MaterialCount; m++)
        {
            List<int> tris = Triangles[m];
            int end = tris.Count;
            for (int i = trianglesBefore[m]; i < end; i += 3)
            {
                tris.Add(tris[i] + offset);
                tris.Add(tris[i + 2] + offset);
                tris.Add(tris[i + 1] + offset);
            }
        }
    }

    private static void Tri(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
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
        Triangles[material].Add(start);
        Triangles[material].Add(start + 1);
        Triangles[material].Add(start + 2);
    }
}
