/*
 * TestWallRing
 * Temporary test layout for slice 1 (until wall presets exist): writes a square ring of Wall cells around the
 * BuildGrid centre (the base) with an opening on each chosen side, so the flow field has something to route around.
 *
 * - ringRadius = cells from the base centre to the ring's inner edge; thickness = ring width in cells. Openings are
 *   openingWidth cells wide, centred on their side. Base cells are never overwritten.
 * - Applied on Start (applyOnStart) and re-applied when a setting changes in Play mode; Clear/Apply are also in the
 *   component's context menu. Writes all cells, then raises one grid Changed (one flow-field rebuild).
 * - showBlocks adds a plain cube per wall cell (wallHeight tall, material) so the ring is visible without the R
 *   overlay. The cubes have no colliders: the player and bullets pass through (walls blocking the player is slice 2).
 */

using System.Collections.Generic;
using UnityEngine;

public class TestWallRing : MonoBehaviour
{
    [SerializeField] private BuildGrid grid;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField, Min(2)] private int ringRadius = 7;
    [SerializeField, Min(1)] private int thickness = 1;
    [SerializeField, Min(1)] private int openingWidth = 4;
    [SerializeField] private bool openNorth = true;
    [SerializeField] private bool openEast;
    [SerializeField] private bool openSouth;
    [SerializeField] private bool openWest = true;

    [Header("Blocks")]
    [SerializeField] private bool showBlocks = true;
    [SerializeField] private Material material;
    [SerializeField, Min(0.1f)] private float wallHeight = 1.5f;

    private readonly List<Vector2Int> written = new List<Vector2Int>();
    private Transform blocks;
    private bool applied;

    private void Start()
    {
        if (grid == null)
        {
            grid = BuildGrid.Instance;
        }
        if (applyOnStart)
        {
            Apply();
        }
    }

    private bool reapply;

    private void OnValidate()
    {
        // Objects can't be created inside OnValidate, so the re-apply waits for the next Update.
        if (Application.isPlaying && applied)
        {
            reapply = true;
        }
    }

    private void Update()
    {
        if (reapply)
        {
            reapply = false;
            Apply();
        }
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        if (grid == null)
        {
            return;
        }

        ClearCells();
        int centerX = grid.Width / 2;
        int centerY = grid.Height / 2;
        float inner = ringRadius;
        float outer = ringRadius + thickness;
        float openingHalf = openingWidth * 0.5f;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                // Offsets of the cell centre from the grid centre (which sits on a cell corner), in cells.
                float u = x - centerX + 0.5f;
                float v = y - centerY + 0.5f;
                float ring = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                if (ring < inner || ring > outer)
                {
                    continue;
                }
                if (IsOpening(u, v, inner, openingHalf))
                {
                    continue;
                }

                Vector2Int cell = new Vector2Int(x, y);
                if (grid.GetState(cell) == CellState.Base)
                {
                    continue;
                }
                grid.SetState(cell, CellState.Wall, false);
                written.Add(cell);
            }
        }

        applied = true;
        grid.NotifyChanged();
        RebuildBlocks();
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        ClearCells();
        applied = false;
        if (grid != null)
        {
            grid.NotifyChanged();
        }
        RebuildBlocks();
    }

    // The opening on a side is the band |along| < openingHalf through that side's straight part.
    private bool IsOpening(float u, float v, float inner, float openingHalf)
    {
        if (openNorth && v >= inner && Mathf.Abs(u) < openingHalf) return true;
        if (openSouth && v <= -inner && Mathf.Abs(u) < openingHalf) return true;
        if (openEast && u >= inner && Mathf.Abs(v) < openingHalf) return true;
        if (openWest && u <= -inner && Mathf.Abs(v) < openingHalf) return true;
        return false;
    }

    private void ClearCells()
    {
        if (grid != null)
        {
            foreach (Vector2Int cell in written)
            {
                if (grid.GetState(cell) == CellState.Wall)
                {
                    grid.SetState(cell, CellState.Free, false);
                }
            }
        }
        written.Clear();
    }

    private void RebuildBlocks()
    {
        if (blocks != null)
        {
            Destroy(blocks.gameObject);
            blocks = null;
        }
        if (!showBlocks || written.Count == 0)
        {
            return;
        }

        blocks = new GameObject("TestWallBlocks").transform;
        blocks.SetParent(transform, false);
        float size = grid.CellSize;
        foreach (Vector2Int cell in written)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(block.GetComponent<Collider>());
            block.name = "Wall " + cell.x + "," + cell.y;
            block.transform.SetParent(blocks, false);
            block.transform.position = grid.CellToWorld(cell) + Vector3.up * (wallHeight * 0.5f);
            block.transform.localScale = new Vector3(size, wallHeight, size);
            if (material != null)
            {
                block.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
