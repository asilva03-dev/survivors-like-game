/*
 * ChunkMinimap
 * Builds small point-filtered textures of wall chunks for the draft cards, in the painting inspector's colours
 * (Path cyan, Wall grey, Build strip blue, Base orange, Empty dark).
 *
 * - Chunk(): one chunk turned `rotation` quarter turns, `scale` pixels per cell, with a border of `scale` pixels where
 *   each door spot is green (open) or red (closed).
 * - Layout(): the whole usable slot area of a BaseLayout (every slot within its reach), placed chunks painted, empty
 *   slots dark with faint slot outlines.
 * Textures are reused when passed back in at the same size; the caller owns (and destroys) them.
 */

using UnityEngine;

public static class ChunkMinimap
{
    public static readonly Color[] CellColors =
    {
        new Color(0.2f, 0.2f, 0.22f),
        new Color(0.3f, 0.8f, 0.95f),
        new Color(0.62f, 0.62f, 0.62f),
        new Color(0.3f, 0.45f, 1f),
        new Color(1f, 0.6f, 0.1f)
    };

    private static readonly Color Frame = new Color(0.08f, 0.08f, 0.09f);
    private static readonly Color OpenDoor = new Color(0.2f, 1f, 0.3f);
    private static readonly Color ClosedDoor = new Color(1f, 0.25f, 0.2f);
    private static readonly Color EmptySlot = new Color(0.1f, 0.1f, 0.11f);
    private static readonly Color SlotLine = new Color(0.2f, 0.2f, 0.22f);

    public static Texture2D Chunk(WallChunk chunk, int rotation, int scale, Texture2D reuse)
    {
        int w = chunk.RotatedWidth(rotation);
        int h = chunk.RotatedHeight(rotation);
        int texW = (w + 2) * scale;
        int texH = (h + 2) * scale;
        Texture2D tex = Prepare(reuse, texW, texH);
        Color[] pixels = new Color[texW * texH];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Frame;
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Fill(pixels, texW, (x + 1) * scale, (y + 1) * scale, scale, scale,
                    CellColors[(int)chunk.GetCell(x, y, rotation)]);
            }
        }

        for (int s = 0; s < 4; s++)
        {
            ChunkSide side = (ChunkSide)s;
            for (int door = 0; door < chunk.DoorCount(side, rotation); door++)
            {
                Color color = chunk.IsDoorOpen(side, door, rotation) ? OpenDoor : ClosedDoor;
                int start = (door * WallChunk.ModuleSize + WallChunk.DoorStart + 1) * scale;
                int length = WallChunk.DoorWidth * scale;
                switch (side)
                {
                    case ChunkSide.North: Fill(pixels, texW, start, texH - scale, length, scale, color); break;
                    case ChunkSide.South: Fill(pixels, texW, start, 0, length, scale, color); break;
                    case ChunkSide.East: Fill(pixels, texW, texW - scale, start, scale, length, color); break;
                    default: Fill(pixels, texW, 0, start, scale, length, color); break;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public static Texture2D Layout(BaseLayout layout, int scale, Texture2D reuse)
    {
        int reach = layout.MaxSlotReach;
        int module = WallChunk.ModuleSize;
        int cells = (reach * 2 + 1) * module;
        int texSize = cells * scale;
        Texture2D tex = Prepare(reuse, texSize, texSize);
        Color[] pixels = new Color[texSize * texSize];
        Vector2Int firstCell = layout.SlotOrigin(new Vector2Int(-reach, -reach));

        for (int y = 0; y < cells; y++)
        {
            for (int x = 0; x < cells; x++)
            {
                Vector2Int slot = new Vector2Int(x / module - reach, y / module - reach);
                BaseLayout.ChunkPlacement placement = layout.PlacementAt(slot);
                Color color;
                if (placement == null)
                {
                    bool line = x % module == 0 || y % module == 0;
                    color = line ? SlotLine : EmptySlot;
                }
                else
                {
                    Vector2Int local = firstCell + new Vector2Int(x, y) - layout.SlotOrigin(placement.slot);
                    color = CellColors[(int)placement.chunk.GetCell(local.x, local.y, placement.rotation & 3)];
                }
                Fill(pixels, texSize, x * scale, y * scale, scale, scale, color);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D Prepare(Texture2D reuse, int width, int height)
    {
        if (reuse != null && reuse.width == width && reuse.height == height)
        {
            return reuse;
        }
        if (reuse != null)
        {
            Object.Destroy(reuse);
        }
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static void Fill(Color[] pixels, int texWidth, int x0, int y0, int w, int h, Color color)
    {
        for (int y = y0; y < y0 + h; y++)
        {
            for (int x = x0; x < x0 + w; x++)
            {
                pixels[y * texWidth + x] = color;
            }
        }
    }
}
