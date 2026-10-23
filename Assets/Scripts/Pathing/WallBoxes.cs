/*
 * WallBoxes
 * Covers a set of wall cells with as few rectangles as possible (greedy: grow each run east, then north). Used by
 * BaseLayout for the real wall blocks and by ChunkPreview for the 3D draft cards, so both always look the same.
 */

using System.Collections.Generic;
using UnityEngine;

public static class WallBoxes
{
    public static List<RectInt> Merge(HashSet<Vector2Int> walls)
    {
        List<RectInt> boxes = new List<RectInt>();
        if (walls.Count == 0)
        {
            return boxes;
        }

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);
        foreach (Vector2Int cell in walls)
        {
            min = Vector2Int.Min(min, cell);
            max = Vector2Int.Max(max, cell);
        }

        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2Int start = new Vector2Int(x, y);
                if (!walls.Contains(start) || used.Contains(start))
                {
                    continue;
                }

                int width = 1;
                while (IsFree(walls, used, new Vector2Int(x + width, y)))
                {
                    width++;
                }
                int height = 1;
                while (RowIsFree(walls, used, x, y + height, width))
                {
                    height++;
                }
                for (int dy = 0; dy < height; dy++)
                {
                    for (int dx = 0; dx < width; dx++)
                    {
                        used.Add(new Vector2Int(x + dx, y + dy));
                    }
                }
                boxes.Add(new RectInt(x, y, width, height));
            }
        }
        return boxes;
    }

    private static bool IsFree(HashSet<Vector2Int> walls, HashSet<Vector2Int> used, Vector2Int cell)
    {
        return walls.Contains(cell) && !used.Contains(cell);
    }

    private static bool RowIsFree(HashSet<Vector2Int> walls, HashSet<Vector2Int> used, int x, int y, int width)
    {
        for (int dx = 0; dx < width; dx++)
        {
            if (!IsFree(walls, used, new Vector2Int(x + dx, y)))
            {
                return false;
            }
        }
        return true;
    }
}
