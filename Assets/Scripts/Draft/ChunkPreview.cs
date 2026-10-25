/*
 * ChunkPreview
 * 3D pictures of wall chunks for the draft cards. A hidden stage far outside the map (stageOrigin, beyond the game
 * camera's far plane) holds one small model per card; one disabled perspective camera looks straight down at a model
 * and renders it into that card's RenderTexture, only when something changed (shown, or turning).
 *
 * - A model = a floor quad showing the chunk's ChunkMinimap texture (painting colours plus the green/red door strips
 *   around the edge, unlit floorMaterial) + the chunk's walls as boxes, merged exactly like the real walls (WallBoxes,
 *   wallMaterial = WallBlock.mat, BaseLayout's wall height). Lit by the scene's own lights, switched to daylight for the render by PreviewLighting
 *   (slice 7); no shadows.
 * - ShowChunk() builds a card's model at rotation 0 and turns it to the card's rotation; Turn() animates the model a
 *   quarter turn over turnDuration (ease-out), re-rendering every frame while it turns, then it holds still.
 * - ShowLayout() builds the whole current base (every slot within the layout's reach) for the "Your base" card and
 *   frames the placed chunks plus one ring of slots around them (where new chunks can attach).
 * - Hide() switches the models off; box objects are reused between drafts.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ChunkPreview : MonoBehaviour
{
    private class Stage
    {
        public Transform root;
        public Transform pivot;
        public MeshRenderer floor;
        public Material floorMaterial;
        public Texture2D floorTexture;
        public readonly List<Transform> boxes = new List<Transform>();
        public RenderTexture texture;
        public float viewSize;
        public float yaw;
        public float fromYaw;
        public float toYaw;
        public float turnTime = -1f;
        public bool dirty;
    }

    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private BaseLayout layout;
    [SerializeField] private Vector3 stageOrigin = new Vector3(5000f, 0f, 5000f);
    [SerializeField, Min(64)] private int textureSize = 512;
    [SerializeField, Range(10f, 80f)] private float fieldOfView = 40f;
    [Tooltip("Empty space around the model, in cells.")]
    [SerializeField, Min(0f)] private float margin = 0.5f;
    [SerializeField] private Color background = new Color(0.08f, 0.08f, 0.09f, 1f);
    [SerializeField, Min(0.01f)] private float turnDuration = 0.25f;
    [Tooltip("Mini-map pixels per cell on the chunk floors.")]
    [SerializeField, Min(1)] private int floorPixelsPerCell = 8;

    private readonly List<Stage> stages = new List<Stage>();
    private Stage layoutStage;
    private Camera previewCamera;
    private Mesh quadMesh;
    private Mesh cubeMesh;

    private const int LayoutStageIndex = 99;

    private void Awake()
    {
        GameObject cameraObject = new GameObject("ChunkPreviewCamera");
        cameraObject.transform.SetParent(transform, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = background;
        previewCamera.fieldOfView = fieldOfView;
        previewCamera.nearClipPlane = 0.3f;
        previewCamera.farClipPlane = 500f;
        quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
    }

    private void OnDestroy()
    {
        foreach (Stage stage in stages)
        {
            Release(stage);
        }
        if (layoutStage != null)
        {
            Release(layoutStage);
        }
    }

    private void LateUpdate()
    {
        foreach (Stage stage in stages)
        {
            Advance(stage);
        }
        if (layoutStage != null)
        {
            Advance(layoutStage);
        }
    }

    /// <summary>Builds the model for card `index` and returns the texture the card should show.</summary>
    public RenderTexture ShowChunk(int index, WallChunk chunk, int rotation)
    {
        while (stages.Count <= index)
        {
            stages.Add(CreateStage(stages.Count));
        }
        Stage stage = stages[index];
        int w = chunk.Width;
        int h = chunk.Height;

        stage.floorTexture = ChunkMinimap.Chunk(chunk, 0, floorPixelsPerCell, stage.floorTexture);
        SetFloor(stage, w + 2f, h + 2f);

        HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (chunk.GetCell(x, y) == ChunkCell.Wall)
                {
                    walls.Add(new Vector2Int(x, y));
                }
            }
        }
        SetBoxes(stage, walls, new Vector2(w * 0.5f, h * 0.5f));

        stage.viewSize = Mathf.Max(w, h) + 2f;
        stage.yaw = stage.fromYaw = stage.toYaw = (rotation & 3) * 90f;
        stage.turnTime = -1f;
        stage.root.gameObject.SetActive(true);
        stage.dirty = true;
        return stage.texture;
    }

    /// <summary>Animates card `index`'s model a quarter turn (+1 clockwise from above, -1 counter-clockwise).</summary>
    public void Turn(int index, int direction)
    {
        if (index < 0 || index >= stages.Count)
        {
            return;
        }
        Stage stage = stages[index];
        stage.fromYaw = stage.yaw;
        stage.toYaw += direction > 0 ? 90f : -90f;
        stage.turnTime = 0f;
        stage.dirty = true;
    }

    /// <summary>Builds the whole current base (all slots within reach) and returns its texture.</summary>
    public RenderTexture ShowLayout()
    {
        if (layoutStage == null)
        {
            layoutStage = CreateStage(LayoutStageIndex);
        }
        int reach = layout.MaxSlotReach;
        int cells = (reach * 2 + 1) * WallChunk.ModuleSize;
        Vector2Int first = layout.SlotOrigin(new Vector2Int(-reach, -reach));

        layoutStage.floorTexture = ChunkMinimap.Layout(layout, 2, layoutStage.floorTexture);
        SetFloor(layoutStage, cells, cells);

        HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
        BuildGrid grid = layout.Grid;
        for (int y = 0; y < cells; y++)
        {
            for (int x = 0; x < cells; x++)
            {
                if (grid.GetState(first + new Vector2Int(x, y)) == CellState.Wall)
                {
                    walls.Add(new Vector2Int(x, y));
                }
            }
        }
        SetBoxes(layoutStage, walls, new Vector2(cells * 0.5f, cells * 0.5f));

        // Frame the placed slots plus one ring around them (clamped to the reach).
        Vector2Int minSlot = new Vector2Int(reach, reach);
        Vector2Int maxSlot = new Vector2Int(-reach, -reach);
        for (int sy = -reach; sy <= reach; sy++)
        {
            for (int sx = -reach; sx <= reach; sx++)
            {
                if (layout.PlacementAt(new Vector2Int(sx, sy)) != null)
                {
                    minSlot = Vector2Int.Min(minSlot, new Vector2Int(sx, sy));
                    maxSlot = Vector2Int.Max(maxSlot, new Vector2Int(sx, sy));
                }
            }
        }
        if (minSlot.x > maxSlot.x)
        {
            minSlot = maxSlot = Vector2Int.zero;
        }
        minSlot = Vector2Int.Max(minSlot - Vector2Int.one, new Vector2Int(-reach, -reach));
        maxSlot = Vector2Int.Min(maxSlot + Vector2Int.one, new Vector2Int(reach, reach));
        int module = WallChunk.ModuleSize;
        Vector2 middle = (Vector2)(minSlot + maxSlot) * 0.5f * module;
        layoutStage.pivot.localPosition = new Vector3(-middle.x, 0f, -middle.y);
        layoutStage.viewSize = Mathf.Max(maxSlot.x - minSlot.x + 1, maxSlot.y - minSlot.y + 1) * module;
        layoutStage.yaw = layoutStage.toYaw = 0f;
        layoutStage.turnTime = -1f;
        layoutStage.root.gameObject.SetActive(true);
        layoutStage.dirty = true;
        return layoutStage.texture;
    }

    public void Hide()
    {
        foreach (Stage stage in stages)
        {
            stage.root.gameObject.SetActive(false);
        }
        if (layoutStage != null)
        {
            layoutStage.root.gameObject.SetActive(false);
        }
    }

    private void Advance(Stage stage)
    {
        if (!stage.root.gameObject.activeSelf)
        {
            return;
        }
        if (stage.turnTime >= 0f)
        {
            stage.turnTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(stage.turnTime / turnDuration);
            float eased = 1f - (1f - t) * (1f - t);
            stage.yaw = Mathf.Lerp(stage.fromYaw, stage.toYaw, eased);
            if (t >= 1f)
            {
                stage.turnTime = -1f;
            }
            stage.dirty = true;
        }
        if (stage.dirty)
        {
            stage.dirty = false;
            Render(stage);
        }
    }

    private void Render(Stage stage)
    {
        stage.pivot.localRotation = Quaternion.Euler(0f, stage.yaw, 0f);
        float half = stage.viewSize * 0.5f + margin;
        float distance = half / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) + layout.WallHeight;
        previewCamera.fieldOfView = fieldOfView;
        previewCamera.transform.SetPositionAndRotation(stage.root.position + Vector3.up * distance,
            Quaternion.LookRotation(Vector3.down, Vector3.forward));
        previewCamera.farClipPlane = distance + 10f;
        previewCamera.targetTexture = stage.texture;
        PreviewLighting.Begin();
        previewCamera.Render();
        PreviewLighting.End();
        previewCamera.targetTexture = null;
    }

    private Stage CreateStage(int index)
    {
        Stage stage = new Stage();
        stage.root = new GameObject("ChunkPreview" + index).transform;
        stage.root.SetParent(transform, false);
        stage.root.position = stageOrigin + new Vector3(index * 150f, 0f, 0f);
        stage.pivot = new GameObject("Pivot").transform;
        stage.pivot.SetParent(stage.root, false);

        GameObject floor = new GameObject("Floor");
        floor.transform.SetParent(stage.pivot, false);
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floor.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        stage.floor = floor.AddComponent<MeshRenderer>();
        stage.floorMaterial = new Material(floorMaterial);
        stage.floor.sharedMaterial = stage.floorMaterial;
        NoShadows(stage.floor);

        stage.texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32);
        stage.texture.antiAliasing = 4;
        stage.texture.name = "ChunkPreview" + index;
        return stage;
    }

    private void SetFloor(Stage stage, float width, float height)
    {
        stage.floorMaterial.mainTexture = stage.floorTexture;
        stage.floor.transform.localScale = new Vector3(width, height, 1f);
    }

    // Walls in chunk cells (0..w, 0..h); `center` is the chunk's middle in cells, placed on the pivot.
    private void SetBoxes(Stage stage, HashSet<Vector2Int> walls, Vector2 center)
    {
        List<RectInt> rects = WallBoxes.Merge(walls);
        float height = layout.WallHeight;
        for (int i = 0; i < rects.Count; i++)
        {
            if (i >= stage.boxes.Count)
            {
                stage.boxes.Add(CreateBox(stage.pivot));
            }
            RectInt r = rects[i];
            Transform box = stage.boxes[i];
            box.gameObject.SetActive(true);
            box.localPosition = new Vector3(r.x + r.width * 0.5f - center.x, height * 0.5f, r.y + r.height * 0.5f - center.y);
            box.localScale = new Vector3(r.width, height, r.height);
        }
        for (int i = rects.Count; i < stage.boxes.Count; i++)
        {
            stage.boxes[i].gameObject.SetActive(false);
        }
    }

    private Transform CreateBox(Transform parent)
    {
        GameObject box = new GameObject("Wall");
        box.transform.SetParent(parent, false);
        box.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        MeshRenderer renderer = box.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = wallMaterial;
        NoShadows(renderer);
        return box.transform;
    }

    private static void NoShadows(MeshRenderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void Release(Stage stage)
    {
        if (stage.texture != null)
        {
            stage.texture.Release();
            Destroy(stage.texture);
        }
        if (stage.floorMaterial != null)
        {
            Destroy(stage.floorMaterial);
        }
        if (stage.floorTexture != null)
        {
            Destroy(stage.floorTexture);
        }
    }
}
