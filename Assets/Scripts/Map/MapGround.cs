/*
 * MapGround
 * The painted map's ground as data, for anything that needs the height under a point or whether two neighbouring map
 * pixels join at the same height (an open edge) or meet at a cliff (map slice step 3, 2026-10-29). No physics calls.
 *
 * - Lives on the MapGreybox prefab's root; Editor/MapGreyboxBuilder fills it (Bake) every time it builds the greybox, so
 *   it always matches the mesh. Instance = the active one in the scene (one map at a time).
 * - Pixels: x east, y north, (0, 0) = the south-west pixel (MapDraft's grid). Each pixel is a level (flat) or part of a
 *   ramp (rises from its low level to its high level along its direction, over its whole bounds), as in the greybox.
 * - HeightAt(world): world y of the ground at a point (false outside the map). Matches the greybox's surface.
 * - CanStep(pixel, neighbour pixel): true when the two (4-neighbour) pixels meet at the same height along their whole
 *   shared edge (same level, a ramp's low/high end onto its level, ramp onto the same ramp); false at a cliff (any step
 *   in height, including a ramp's sides) or the map edge. Diagonal moves are left to the caller (both straight edges).
 * - CanDrop(pixel, neighbour pixel): the neighbour is lower along the whole shared edge (a ledge enemies may walk off;
 *   step 4, user: "can drop down", climbing only by ramps).
 */

using System;
using UnityEngine;

public class MapGround : MonoBehaviour
{
    public const byte RampCell = 255;
    private const float StepTolerance = 0.01f;

    [Serializable]
    public struct Ramp
    {
        public int direction;
        public RectInt bounds;
        public float low;
        public float high;
    }

    [SerializeField, HideInInspector] private int width;
    [SerializeField, HideInInspector] private int height;
    [SerializeField, HideInInspector] private float unitsPerPixel = 1f;
    [SerializeField, HideInInspector] private float levelHeight = 1f;
    [SerializeField, HideInInspector] private Vector3 southWest;
    [SerializeField, HideInInspector] private byte[] levels;
    [SerializeField, HideInInspector] private byte[] rampOf;
    [SerializeField, HideInInspector] private Ramp[] ramps;

    public static MapGround Instance { get; private set; }

    public int Width => width;
    public int Height => height;
    public float UnitsPerPixel => unitsPerPixel;
    /// <summary>World position of the map's south-west corner (the floor's height).</summary>
    public Vector3 SouthWest => southWest;
    public bool HasData => levels != null && levels.Length == width * height && width > 0;

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Fills the data from a checked draft (called by the greybox builder).</summary>
    public void Bake(MapDraft draft, MapDraftSettings settings, Vector3 southWestCorner)
    {
        width = draft.Width;
        height = draft.Height;
        unitsPerPixel = settings.UnitsPerPixel;
        levelHeight = settings.LevelHeight;
        southWest = southWestCorner;
        levels = new byte[width * height];
        rampOf = new byte[width * height];
        ramps = new Ramp[draft.Ramps.Count];
        if (draft.Ramps.Count > 255)
        {
            throw new InvalidOperationException("MapGround: more than 255 ramps.");
        }

        int[] rampOfRegion = new int[draft.Regions.Count];
        for (int r = 0; r < draft.Ramps.Count; r++)
        {
            MapDraft.Ramp ramp = draft.Ramps[r];
            rampOfRegion[ramp.Region.Id] = r;
            ramps[r] = new Ramp
            {
                direction = ramp.Direction,
                bounds = ramp.Region.Bounds,
                low = Extreme(draft, ramp.LowEnd, true) * levelHeight,
                high = Extreme(draft, ramp.HighEnd, false) * levelHeight
            };
        }

        for (int i = 0; i < levels.Length; i++)
        {
            if (draft.Level[i] != MapDraft.NotLevel)
            {
                levels[i] = (byte)draft.Level[i];
            }
            else
            {
                levels[i] = RampCell;
                rampOf[i] = (byte)rampOfRegion[draft.RegionOf[i]];
            }
        }
    }

    private static int Extreme(MapDraft draft, System.Collections.Generic.HashSet<int> regions, bool lowest)
    {
        int level = lowest ? int.MaxValue : int.MinValue;
        foreach (int id in regions)
        {
            level = lowest ? Mathf.Min(level, draft.Regions[id].Level) : Mathf.Max(level, draft.Regions[id].Level);
        }
        return level;
    }

    /// <summary>Whether a world point is on the map, at least `margin` units inside its edge (flat XZ).</summary>
    public bool Contains(Vector3 world, float margin)
    {
        if (!HasData)
        {
            return false;
        }
        float x = world.x - southWest.x;
        float z = world.z - southWest.z;
        return x >= margin && z >= margin && x <= width * unitsPerPixel - margin && z <= height * unitsPerPixel - margin;
    }

    public bool InBounds(Vector2Int pixel)
    {
        return pixel.x >= 0 && pixel.y >= 0 && pixel.x < width && pixel.y < height;
    }

    /// <summary>The pixel containing a world point (may be outside the map; check InBounds).</summary>
    public Vector2Int WorldToPixel(Vector3 world)
    {
        return new Vector2Int(Mathf.FloorToInt((world.x - southWest.x) / unitsPerPixel),
            Mathf.FloorToInt((world.z - southWest.z) / unitsPerPixel));
    }

    /// <summary>World centre of a pixel, at its ground height.</summary>
    public Vector3 PixelCenter(Vector2Int pixel)
    {
        float gx = pixel.x + 0.5f;
        float gy = pixel.y + 0.5f;
        return new Vector3(southWest.x + gx * unitsPerPixel, southWest.y + LocalHeight(pixel, gx, gy), southWest.z + gy * unitsPerPixel);
    }

    /// <summary>Whether a pixel is part of a ramp (else it is a flat level).</summary>
    public bool IsRamp(Vector2Int pixel)
    {
        return InBounds(pixel) && levels[pixel.y * width + pixel.x] == RampCell;
    }

    /// <summary>World y of the ground under a point; false outside the map.</summary>
    public bool TryGetHeight(Vector3 world, out float y)
    {
        y = southWest.y;
        if (!HasData)
        {
            return false;
        }
        float gx = (world.x - southWest.x) / unitsPerPixel;
        float gy = (world.z - southWest.z) / unitsPerPixel;
        Vector2Int pixel = new Vector2Int(Mathf.FloorToInt(gx), Mathf.FloorToInt(gy));
        if (!InBounds(pixel))
        {
            return false;
        }
        y = southWest.y + LocalHeight(pixel, gx, gy);
        return true;
    }

    /// <summary>World y of the ground under a point (the floor's height outside the map).</summary>
    public float HeightAt(Vector3 world)
    {
        float y;
        TryGetHeight(world, out y);
        return y;
    }

    /// <summary>True when two 4-neighbour pixels meet at the same height along their whole shared edge.</summary>
    public bool CanStep(Vector2Int from, Vector2Int to)
    {
        float fromA, toA, fromB, toB;
        if (!EdgeHeights(from, to, out fromA, out toA, out fromB, out toB))
        {
            return false;
        }
        return Mathf.Abs(fromA - toA) <= StepTolerance && Mathf.Abs(fromB - toB) <= StepTolerance;
    }

    /// <summary>True when `to` (a 4-neighbour) is lower than `from` along their whole shared edge: a ledge to drop off.</summary>
    public bool CanDrop(Vector2Int from, Vector2Int to)
    {
        float fromA, toA, fromB, toB;
        if (!EdgeHeights(from, to, out fromA, out toA, out fromB, out toB))
        {
            return false;
        }
        return toA < fromA - StepTolerance && toB < fromB - StepTolerance;
    }

    // Both pixels' surface heights at the two end points of their shared edge; false if not 4-neighbours on the map.
    private bool EdgeHeights(Vector2Int from, Vector2Int to, out float fromA, out float toA, out float fromB, out float toB)
    {
        fromA = toA = fromB = toB = 0f;
        if (!HasData || !InBounds(from) || !InBounds(to))
        {
            return false;
        }
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1)
        {
            return false;
        }

        float ax, ay, bx, by;
        if (dx != 0)
        {
            ax = bx = dx > 0 ? from.x + 1 : from.x;
            ay = from.y;
            by = from.y + 1;
        }
        else
        {
            ay = by = dy > 0 ? from.y + 1 : from.y;
            ax = from.x;
            bx = from.x + 1;
        }
        fromA = LocalHeight(from, ax, ay);
        toA = LocalHeight(to, ax, ay);
        fromB = LocalHeight(from, bx, by);
        toB = LocalHeight(to, bx, by);
        return true;
    }

    // Height above southWest.y of a pixel's own surface at grid point (gx, gy) (may be on its border).
    private float LocalHeight(Vector2Int pixel, float gx, float gy)
    {
        int i = pixel.y * width + pixel.x;
        byte level = levels[i];
        if (level != RampCell)
        {
            return level * levelHeight;
        }
        Ramp ramp = ramps[rampOf[i]];
        RectInt b = ramp.bounds;
        float t;
        switch (ramp.direction)
        {
            case 0: t = (gy - b.yMin) / b.height; break;
            case 1: t = (gx - b.xMin) / b.width; break;
            case 2: t = (b.yMax - gy) / b.height; break;
            default: t = (b.xMax - gx) / b.width; break;
        }
        return Mathf.Lerp(ramp.low, ramp.high, Mathf.Clamp01(t));
    }
}
