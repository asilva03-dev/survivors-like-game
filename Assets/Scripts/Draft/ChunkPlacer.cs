/*
 * ChunkPlacer
 * Placement mode for a picked preset: a see-through ghost of the chunk follows the cursor, snapped to the slot under
 * it, green when BaseLayout.CanPlace allows that spot and red when not (the hint bar says why).
 *
 * - Mouse wheel turns the ghost a quarter turn (up = clockwise). It starts at the rotation the card had.
 * - Left click on a valid spot places the chunk (BaseLayout.Place) and ends the mode. Esc discards the piece (there is
 *   no inventory yet), except for the base chunk: it is locked to the core slot, can only be turned, and must be placed.
 * - While active, player mouse attacks are blocked (PlayerInputGate).
 * - The ghost is one runtime mesh (floor tiles per cell, boxes for walls) with vertex colours, drawn with a tinted
 *   instance of ghostMaterial (an unlit transparent vertex-colour material, e.g. GridDebug.mat).
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ChunkPlacer : MonoBehaviour
{
    [SerializeField] private BaseLayout layout;
    [SerializeField] private PresetDraftUI ui;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Color validColor = new Color(0.45f, 1f, 0.5f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.3f, 0.6f);
    [SerializeField, Min(0f)] private float lift = 0.06f;

    private WallChunk chunk;
    private int rotation;
    private bool isBase;
    private Action<bool> onDone;
    private Vector2Int slot;
    private bool valid;
    private string reason = "";
    private bool dirty;
    private int meshRotation = -1;

    private GameObject ghost;
    private Mesh mesh;
    private Material material;

    public bool Active => chunk != null;
    public Vector2Int Slot => slot;
    public int Rotation => rotation;
    public bool Valid => valid;
    public string Reason => reason;

    /// <summary>Starts placing `picked`; onFinished(true) after it was placed, (false) if it was discarded.</summary>
    public void Begin(WallChunk picked, int startRotation, bool baseChunk, Action<bool> onFinished)
    {
        chunk = picked;
        rotation = startRotation & 3;
        isBase = baseChunk;
        onDone = onFinished;
        slot = Vector2Int.zero;
        dirty = true;
        meshRotation = -1;
        EnsureGhost();
        ghost.SetActive(false);
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
        if (material != null)
        {
            Destroy(material);
        }
    }

    private void Update()
    {
        if (!Active)
        {
            return;
        }
        PlayerInputGate.BlockAttacks();

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f)
            {
                Turn(1);
            }
            else if (scroll < -0.01f)
            {
                Turn(-1);
            }
        }

        Vector2Int target = slot;
        if (isBase)
        {
            target = Vector2Int.zero;
        }
        else if (mouse != null && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            float distance;
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance))
            {
                target = SlotUnder(ray.GetPoint(distance));
            }
        }
        MoveTo(target);

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && valid)
        {
            Confirm();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (!isBase && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Discard();
        }
    }

    /// <summary>Turns the ghost a quarter turn (+1 clockwise, -1 counter-clockwise).</summary>
    public void Turn(int direction)
    {
        rotation = (rotation + (direction > 0 ? 1 : 3)) & 3;
        dirty = true;
    }

    /// <summary>Moves the ghost to a slot (the base chunk stays in the core slot).</summary>
    public void MoveTo(Vector2Int target)
    {
        if (isBase)
        {
            target = Vector2Int.zero;
        }
        if (target != slot)
        {
            slot = target;
            dirty = true;
        }
        if (dirty)
        {
            dirty = false;
            Refresh();
        }
    }

    /// <summary>Places the chunk if the current spot is valid.</summary>
    public bool Confirm()
    {
        if (!Active || !valid)
        {
            return false;
        }
        layout.Place(chunk, slot, rotation);
        Finish(true);
        return true;
    }

    /// <summary>Throws the piece away (not allowed for the base chunk).</summary>
    public void Discard()
    {
        if (Active && !isBase)
        {
            Finish(false);
        }
    }

    // The slot a chunk should snap to so it is centred on the cursor (one module = the slot under the cursor).
    private Vector2Int SlotUnder(Vector3 point)
    {
        float cell = layout.Grid.CellSize;
        int module = WallChunk.ModuleSize;
        Vector3 shift = new Vector3((chunk.RotatedWidth(rotation) - module) * 0.5f * cell, 0f,
            (chunk.RotatedHeight(rotation) - module) * 0.5f * cell);
        return layout.SlotAt(point - shift);
    }

    private void Refresh()
    {
        valid = layout.CanPlace(chunk, slot, rotation, out reason);
        if (meshRotation != rotation)
        {
            BuildMesh();
            meshRotation = rotation;
        }
        ghost.SetActive(true);
        ghost.transform.position = layout.CellCorner(layout.SlotOrigin(slot)) + Vector3.up * lift;
        material.color = valid ? validColor : invalidColor;

        string controls = isBase
            ? "Place your base   Scroll: rotate   Click: place"
            : "Scroll: rotate   Click: place   Esc: discard";
        ui.ShowHint(chunk.name + " (" + rotation * 90 + "°)   " + controls, valid ? "Click to place here." : reason, valid);
    }

    private void Finish(bool placed)
    {
        chunk = null;
        ghost.SetActive(false);
        ui.HideHint();
        Action<bool> done = onDone;
        onDone = null;
        done?.Invoke(placed);
    }

    private void EnsureGhost()
    {
        if (ghost != null)
        {
            return;
        }
        ghost = new GameObject("ChunkGhost");
        ghost.transform.SetParent(transform, false);
        mesh = new Mesh { name = "ChunkGhost", indexFormat = IndexFormat.UInt32 };
        ghost.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = ghost.AddComponent<MeshRenderer>();
        material = new Material(ghostMaterial);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void BuildMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();
        float cell = layout.Grid.CellSize;
        float inset = cell * 0.06f;
        float wall = layout.WallHeight;

        for (int y = 0; y < chunk.RotatedHeight(rotation); y++)
        {
            for (int x = 0; x < chunk.RotatedWidth(rotation); x++)
            {
                ChunkCell value = chunk.GetCell(x, y, rotation);
                Vector3 min = new Vector3(x * cell + inset, 0f, y * cell + inset);
                Vector3 max = new Vector3((x + 1) * cell - inset, 0f, (y + 1) * cell - inset);
                if (value == ChunkCell.Wall)
                {
                    max.y = wall;
                    AddBox(vertices, colors, triangles, min, max, new Color(1f, 1f, 1f, 0.9f));
                }
                else
                {
                    Color color = value == ChunkCell.Empty
                        ? new Color(0.6f, 0.6f, 0.6f, 0.25f)
                        : ChunkMinimap.CellColors[(int)value] * new Color(1f, 1f, 1f, 0.7f);
                    AddQuad(vertices, colors, triangles, new Vector3(min.x, 0f, min.z), new Vector3(min.x, 0f, max.z),
                        new Vector3(max.x, 0f, max.z), new Vector3(max.x, 0f, min.z), color);
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private static void AddBox(List<Vector3> v, List<Color> c, List<int> t, Vector3 min, Vector3 max, Color color)
    {
        Color side = color * new Color(0.8f, 0.8f, 0.8f, 1f);
        AddQuad(v, c, t, new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), color);
        AddQuad(v, c, t, new Vector3(min.x, 0f, min.z), new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, 0f, min.z), side);
        AddQuad(v, c, t, new Vector3(max.x, 0f, max.z), new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z), new Vector3(min.x, 0f, max.z), side);
        AddQuad(v, c, t, new Vector3(min.x, 0f, max.z), new Vector3(min.x, max.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(min.x, 0f, min.z), side);
        AddQuad(v, c, t, new Vector3(max.x, 0f, min.z), new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z), new Vector3(max.x, 0f, max.z), side);
    }

    private static void AddQuad(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 d, Vector3 e,
        Color color)
    {
        int start = v.Count;
        v.Add(a);
        v.Add(b);
        v.Add(d);
        v.Add(e);
        for (int i = 0; i < 4; i++)
        {
            c.Add(color);
        }
        t.Add(start);
        t.Add(start + 1);
        t.Add(start + 2);
        t.Add(start);
        t.Add(start + 2);
        t.Add(start + 3);
    }
}
