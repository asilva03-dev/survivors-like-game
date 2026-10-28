/*
 * MapGreyboxBuilder
 * Builds the grey blocky map from the painted height image (slice 8 step 2): Assets/MapDraft/MapGreybox.prefab, its
 * meshes in Assets/MapDraft/MapGreyboxMeshes.asset and its materials in Assets/MapDraft/Greybox/. Run it from the
 * MapDraftSettings inspector ("Build greybox") or Tools > Map > Build Greybox. It refuses to build while the checker
 * reports errors. Nothing is placed in a scene; prefab instances update when it is rebuilt.
 *
 * - Every pixel is a column whose top has 4 corner heights: a level pixel is flat at level x levelHeight; a ramp pixel
 *   slopes from its ramp's low level to its high level along the ramp's length (read from MapDraft's ramp ends).
 * - Meshes (vertices in world space, mapCenter = the image's centre): one flat top mesh per level (same-level pixels
 *   merged into rectangles by WallBoxes.Merge), one ramp mesh (a sloped quad per ramp pixel) and one wall mesh: a
 *   wall quad on every pixel edge where this side is higher than the neighbour (down to the neighbour's surface, so
 *   walls along sloped walkways are trapezoids); along the map border walls reach edgeSkirt below the floor.
 * - Collision: one combined mesh of every face on a MeshCollider on the root (no seams between pieces), plus four
 *   invisible box walls (Edges) just outside the border, edgeWallHeight tall. Layer Default, like the old Ground.
 * - Materials are created only when missing (grey shades per level, tan ramps, dark brown walls), so colours tuned by
 *   hand survive a rebuild. Meshes are rewritten in place (Clear + setters) so prefab references stay valid.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MapGreyboxBuilder
{
    public const string PrefabPath = "Assets/MapDraft/MapGreybox.prefab";
    public const string MeshPath = "Assets/MapDraft/MapGreyboxMeshes.asset";
    public const string MaterialFolder = "Assets/MapDraft/Greybox";

    private const float Epsilon = 0.001f;
    private const float EdgeThickness = 4f;

    private static readonly Color[] LevelShades =
    {
        new Color(0.30f, 0.32f, 0.30f),
        new Color(0.40f, 0.42f, 0.40f),
        new Color(0.50f, 0.52f, 0.50f),
        new Color(0.60f, 0.62f, 0.60f),
        new Color(0.70f, 0.72f, 0.70f)
    };
    private static readonly Color RampShade = new Color(0.72f, 0.60f, 0.42f);
    private static readonly Color WallShade = new Color(0.27f, 0.22f, 0.18f);

    private class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();
        public int Quads;

        // A quad a-b-c-d (in order around it); flipped if needed so it faces `facing`.
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, bool wallUv)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                Vector3 swap = b;
                b = d;
                d = swap;
            }
            int start = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            Vector3[] corners = { a, b, c, d };
            foreach (Vector3 v in corners)
            {
                Uvs.Add(wallUv ? new Vector2((v.x + v.z) * 0.25f, v.y * 0.25f) : new Vector2(v.x * 0.25f, v.z * 0.25f));
            }
            Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
            Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 3);
            Quads++;
        }

        public void AppendTo(MeshData other)
        {
            int offset = other.Vertices.Count;
            other.Vertices.AddRange(Vertices);
            other.Uvs.AddRange(Uvs);
            foreach (int index in Triangles)
            {
                other.Triangles.Add(index + offset);
            }
            other.Quads += Quads;
        }
    }

    [MenuItem("Tools/Map/Build Greybox")]
    private static void BuildFromMenu()
    {
        string[] guids = AssetDatabase.FindAssets("t:MapDraftSettings");
        if (guids.Length == 0)
        {
            Debug.LogWarning("Map greybox: no MapDraftSettings asset found.");
            return;
        }
        MapDraftSettings settings = AssetDatabase.LoadAssetAtPath<MapDraftSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        Selection.activeObject = settings;
        string summary;
        Build(settings, out summary);
    }

    /// <summary>Builds the prefab. False (with the reason in summary) if the draft has errors or can't be read.</summary>
    public static bool Build(MapDraftSettings settings, out string summary)
    {
        List<MapDraftSettingsEditor.Line> check = MapDraftSettingsEditor.Check(settings, false);
        if (check.Count > 0 && check[0].Type == MessageType.Error)
        {
            summary = "Map greybox not built: the draft has errors. Run Check image. (" + check[0].Text + ")";
            Debug.LogWarning(summary);
            return false;
        }

        MapDraft draft;
        string error;
        if (!MapDraftSettingsEditor.TryLoad(settings, out draft, out error))
        {
            summary = "Map greybox not built: " + error;
            Debug.LogWarning(summary);
            return false;
        }

        float unit = settings.UnitsPerPixel;
        Vector3 southWest = settings.MapCenter - new Vector3(draft.Width * unit * 0.5f, 0f, draft.Height * unit * 0.5f);
        float[] corners = CornerHeights(draft, settings);

        // Tops: one mesh per level, merged rectangles.
        MeshData[] levels = new MeshData[settings.LevelCount];
        for (int level = 0; level < levels.Length; level++)
        {
            levels[level] = new MeshData();
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
            for (int i = 0; i < draft.Level.Length; i++)
            {
                if (draft.Level[i] == level)
                {
                    cells.Add(new Vector2Int(i % draft.Width, i / draft.Width));
                }
            }
            float height = level * settings.LevelHeight;
            foreach (RectInt r in WallBoxes.Merge(cells))
            {
                levels[level].AddQuad(
                    Point(southWest, unit, r.xMin, r.yMin, height), Point(southWest, unit, r.xMin, r.yMax, height),
                    Point(southWest, unit, r.xMax, r.yMax, height), Point(southWest, unit, r.xMax, r.yMin, height),
                    Vector3.up, false);
            }
        }

        // Ramps: a sloped quad per ramp pixel.
        MeshData ramps = new MeshData();
        for (int y = 0; y < draft.Height; y++)
        {
            for (int x = 0; x < draft.Width; x++)
            {
                int i = draft.Index(x, y);
                if (draft.RampDirection[i] == MapDraft.NotRamp)
                {
                    continue;
                }
                ramps.AddQuad(
                    Point(southWest, unit, x, y, corners[i * 4]), Point(southWest, unit, x, y + 1, corners[i * 4 + 3]),
                    Point(southWest, unit, x + 1, y + 1, corners[i * 4 + 2]), Point(southWest, unit, x + 1, y, corners[i * 4 + 1]),
                    Vector3.up, false);
            }
        }

        // Walls on every edge where this pixel is higher than its neighbour.
        MeshData walls = new MeshData();
        float skirt = -settings.EdgeSkirt;
        for (int y = 0; y < draft.Height; y++)
        {
            for (int x = 0; x < draft.Width; x++)
            {
                int i = draft.Index(x, y) * 4;
                // Corner order per pixel: 0 = (x, y), 1 = (x+1, y), 2 = (x+1, y+1), 3 = (x, y+1).
                AddWall(walls, draft, corners, southWest, unit, skirt, x, y, x + 1, y, x + 1, y + 1, i + 1, i + 2, 0, 3, Vector3.right);
                AddWall(walls, draft, corners, southWest, unit, skirt, x, y, x - 1, y, x, y + 1, i + 0, i + 3, 1, 2, Vector3.left);
                AddWall(walls, draft, corners, southWest, unit, skirt, x, y, x, y + 1, x + 1, y + 1, i + 3, i + 2, 0, 1, Vector3.forward);
                AddWall(walls, draft, corners, southWest, unit, skirt, x, y, x, y - 1, x + 1, y, i + 0, i + 1, 3, 2, Vector3.back);
            }
        }

        MeshData collision = new MeshData();
        foreach (MeshData level in levels)
        {
            level.AppendTo(collision);
        }
        ramps.AppendTo(collision);
        walls.AppendTo(collision);

        // Save meshes (in place), materials (only if missing) and the prefab.
        Dictionary<string, Mesh> existing = new Dictionary<string, Mesh>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
        {
            Mesh mesh = asset as Mesh;
            if (mesh != null)
            {
                existing[mesh.name] = mesh;
            }
        }
        HashSet<string> used = new HashSet<string>();
        Mesh[] levelMeshes = new Mesh[levels.Length];
        for (int level = 0; level < levels.Length; level++)
        {
            levelMeshes[level] = SaveMesh("Level" + level, levels[level], existing, used);
        }
        Mesh rampMesh = SaveMesh("Ramps", ramps, existing, used);
        Mesh wallMesh = SaveMesh("Walls", walls, existing, used);
        Mesh collisionMesh = SaveMesh("Collision", collision, existing, used);
        foreach (KeyValuePair<string, Mesh> pair in existing)
        {
            if (!used.Contains(pair.Key) && AssetDatabase.IsSubAsset(pair.Value))
            {
                Object.DestroyImmediate(pair.Value, true);
            }
        }
        AssetDatabase.SaveAssets();

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/MapDraft", "Greybox");
        }

        Scene stage = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject("MapGreybox");
            SceneManager.MoveGameObjectToScene(root, stage);
            root.AddComponent<MeshCollider>().sharedMesh = collisionMesh;
            for (int level = 0; level < levels.Length; level++)
            {
                AddPiece(root, "Level" + level, levelMeshes[level], GetMaterial("Level" + level, LevelShades[Mathf.Min(level, LevelShades.Length - 1)]));
            }
            AddPiece(root, "Ramps", rampMesh, GetMaterial("Ramps", RampShade));
            AddPiece(root, "Walls", wallMesh, GetMaterial("Walls", WallShade));
            AddEdges(root, settings, southWest, draft.Width * unit, draft.Height * unit);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(stage);
        }

        int quads = collision.Quads;
        summary = string.Format("Map greybox built: {0} ({1} x {2} units). Faces: {3} level tops, {4} ramp, {5} wall ({6} quads, {7} vertices in the collision mesh).",
            PrefabPath, draft.Width * unit, draft.Height * unit, Sum(levels), ramps.Quads, walls.Quads, quads, collision.Vertices.Count);
        Debug.Log(summary);
        return true;
    }

    // 4 corner heights per pixel (world y offset from the floor): level pixels flat, ramp pixels along their ramp.
    private static float[] CornerHeights(MapDraft draft, MapDraftSettings settings)
    {
        Dictionary<int, MapDraft.Ramp> rampOfRegion = new Dictionary<int, MapDraft.Ramp>();
        foreach (MapDraft.Ramp ramp in draft.Ramps)
        {
            rampOfRegion[ramp.Region.Id] = ramp;
        }

        float[] corners = new float[draft.Width * draft.Height * 4];
        for (int y = 0; y < draft.Height; y++)
        {
            for (int x = 0; x < draft.Width; x++)
            {
                int i = draft.Index(x, y);
                if (draft.Level[i] != MapDraft.NotLevel)
                {
                    float h = draft.Level[i] * settings.LevelHeight;
                    corners[i * 4] = corners[i * 4 + 1] = corners[i * 4 + 2] = corners[i * 4 + 3] = h;
                    continue;
                }

                MapDraft.Ramp ramp = rampOfRegion[draft.RegionOf[i]];
                float low = Extreme(draft, ramp.LowEnd, true) * settings.LevelHeight;
                float high = Extreme(draft, ramp.HighEnd, false) * settings.LevelHeight;
                corners[i * 4] = RampHeight(ramp, x, y, low, high);
                corners[i * 4 + 1] = RampHeight(ramp, x + 1, y, low, high);
                corners[i * 4 + 2] = RampHeight(ramp, x + 1, y + 1, low, high);
                corners[i * 4 + 3] = RampHeight(ramp, x, y + 1, low, high);
            }
        }
        return corners;
    }

    private static float RampHeight(MapDraft.Ramp ramp, int cornerX, int cornerY, float low, float high)
    {
        RectInt b = ramp.Region.Bounds;
        float t;
        switch (ramp.Direction)
        {
            case 0: t = (cornerY - b.yMin) / (float)b.height; break;
            case 1: t = (cornerX - b.xMin) / (float)b.width; break;
            case 2: t = (b.yMax - cornerY) / (float)b.height; break;
            default: t = (b.xMax - cornerX) / (float)b.width; break;
        }
        return Mathf.Lerp(low, high, Mathf.Clamp01(t));
    }

    private static int Extreme(MapDraft draft, HashSet<int> regions, bool lowest)
    {
        int level = lowest ? int.MaxValue : int.MinValue;
        foreach (int id in regions)
        {
            level = lowest ? Mathf.Min(level, draft.Regions[id].Level) : Mathf.Max(level, draft.Regions[id].Level);
        }
        return level;
    }

    // Wall on the edge from corner (ax, ay) to (bx, by) of pixel (x, y), facing its neighbour (nx, ny).
    // ownA/ownB index this pixel's corner heights there; nbA/nbB are the neighbour's corner slots at the same points.
    private static void AddWall(MeshData walls, MapDraft draft, float[] corners, Vector3 southWest, float unit, float skirt,
        int x, int y, int nx, int ny, int bx, int by, int ownA, int ownB, int nbA, int nbB, Vector3 facing)
    {
        int ax = facing == Vector3.right ? x + 1 : x;
        int ay = facing == Vector3.forward ? y + 1 : y;
        float topA = corners[ownA];
        float topB = corners[ownB];
        float bottomA = skirt;
        float bottomB = skirt;
        if (draft.InBounds(nx, ny))
        {
            int n = draft.Index(nx, ny) * 4;
            bottomA = corners[n + nbA];
            bottomB = corners[n + nbB];
        }
        if (topA <= bottomA + Epsilon && topB <= bottomB + Epsilon)
        {
            return;
        }
        bottomA = Mathf.Min(bottomA, topA);
        bottomB = Mathf.Min(bottomB, topB);
        walls.AddQuad(
            Point(southWest, unit, ax, ay, bottomA), Point(southWest, unit, ax, ay, topA),
            Point(southWest, unit, bx, by, topB), Point(southWest, unit, bx, by, bottomB),
            facing, true);
    }

    private static Vector3 Point(Vector3 southWest, float unit, int cornerX, int cornerY, float height)
    {
        return new Vector3(southWest.x + cornerX * unit, southWest.y + height, southWest.z + cornerY * unit);
    }

    private static Mesh SaveMesh(string name, MeshData data, Dictionary<string, Mesh> existing, HashSet<string> used)
    {
        Mesh mesh;
        bool isNew = !existing.TryGetValue(name, out mesh);
        if (isNew)
        {
            mesh = new Mesh { name = name };
        }
        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(data.Vertices);
        mesh.SetUVs(0, data.Uvs);
        mesh.SetTriangles(data.Triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        if (isNew)
        {
            if (AssetDatabase.LoadMainAssetAtPath(MeshPath) == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(mesh, MeshPath);
            }
            existing[name] = mesh;
        }
        EditorUtility.SetDirty(mesh);
        used.Add(name);
        return mesh;
    }

    private static Material GetMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/Greybox" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.color = color;
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0f);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void AddPiece(GameObject root, string name, Mesh mesh, Material material)
    {
        GameObject piece = new GameObject(name);
        piece.transform.SetParent(root.transform, false);
        piece.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    private static void AddEdges(GameObject root, MapDraftSettings settings, Vector3 southWest, float width, float depth)
    {
        GameObject edges = new GameObject("Edges");
        edges.transform.SetParent(root.transform, false);
        float height = settings.EdgeWallHeight;
        float y = southWest.y - settings.EdgeSkirt + height * 0.5f;
        Vector3 center = settings.MapCenter;
        float t = EdgeThickness;
        AddBox(edges, new Vector3(southWest.x - t * 0.5f, y, center.z), new Vector3(t, height, depth + t * 2f));
        AddBox(edges, new Vector3(southWest.x + width + t * 0.5f, y, center.z), new Vector3(t, height, depth + t * 2f));
        AddBox(edges, new Vector3(center.x, y, southWest.z - t * 0.5f), new Vector3(width + t * 2f, height, t));
        AddBox(edges, new Vector3(center.x, y, southWest.z + depth + t * 0.5f), new Vector3(width + t * 2f, height, t));
    }

    private static void AddBox(GameObject owner, Vector3 center, Vector3 size)
    {
        BoxCollider box = owner.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }

    private static int Sum(MeshData[] levels)
    {
        int total = 0;
        foreach (MeshData level in levels)
        {
            total += level.Quads;
        }
        return total;
    }
}
