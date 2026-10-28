/*
 * MapDraft
 * The painted height image read into data (slice 8): what every pixel is (a level, a ramp, or an unknown colour), the
 * connected areas, and what each ramp joins. Used by the checker (Editor/MapDraftSettingsEditor) and, later, by the
 * greybox builder.
 *
 * - Grid coordinates are texture coordinates: x to the east, y to the north, (0, 0) = the image's bottom-left pixel
 *   (so y maps straight to world +Z). Aseprite shows y from the top: AsepriteY(y) converts for messages.
 * - Read(pixels, width, height, settings) matches colours exactly (RGB, alpha must be 255); anything else is Unknown.
 * - Regions: 4-neighbour connected areas of one level, or of one ramp colour. Each has its bounds and its largest flat
 *   square (SquareSide, SquareMin = bottom-left corner).
 * - Ramps: one per ramp region. Its low end is the row/column just past the ramp on the side opposite its direction,
 *   its high end the one just past it on the side it rises toward; LowEnd / HighEnd hold the level regions found
 *   there, LowEndOther / HighEndOther count end pixels that are not a level (map edge, another ramp, unknown).
 * - Links(): which level regions each ramp joins (both ways), for reachability checks.
 */

using System.Collections.Generic;
using UnityEngine;

public class MapDraft
{
    public const int NotLevel = -1;
    public const int NotRamp = -1;

    public static readonly string[] DirectionNames = { "north", "east", "south", "west" };

    public class Region
    {
        public int Id;
        public bool IsRamp;
        public int Level = NotLevel;
        public int Direction = NotRamp;
        public int PixelCount;
        public RectInt Bounds;
        public int SquareSide;
        public Vector2Int SquareMin;
    }

    public class Ramp
    {
        public Region Region;
        public int Direction;
        public int Length;
        public int Width;
        public readonly HashSet<int> LowEnd = new HashSet<int>();
        public readonly HashSet<int> HighEnd = new HashSet<int>();
        public int LowEndOther;
        public int HighEndOther;
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int[] Level { get; private set; }
    public int[] RampDirection { get; private set; }
    public int[] RegionOf { get; private set; }
    public List<Region> Regions { get; } = new List<Region>();
    public List<Ramp> Ramps { get; } = new List<Ramp>();
    public int UnknownCount { get; private set; }
    public Dictionary<string, List<Vector2Int>> UnknownColours { get; } = new Dictionary<string, List<Vector2Int>>();

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public int Index(int x, int y) => y * Width + x;
    public int AsepriteY(int y) => Height - 1 - y;

    public static MapDraft Read(Color32[] pixels, int width, int height, MapDraftSettings settings)
    {
        MapDraft draft = new MapDraft();
        draft.Width = width;
        draft.Height = height;
        int count = width * height;
        draft.Level = new int[count];
        draft.RampDirection = new int[count];
        draft.RegionOf = new int[count];

        for (int i = 0; i < count; i++)
        {
            Color32 c = pixels[i];
            draft.Level[i] = NotLevel;
            draft.RampDirection[i] = NotRamp;
            draft.RegionOf[i] = -1;
            if (c.a == 255)
            {
                for (int level = 0; level < settings.LevelCount; level++)
                {
                    if (Same(c, settings.LevelColour(level)))
                    {
                        draft.Level[i] = level;
                        break;
                    }
                }
                if (draft.Level[i] == NotLevel)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        if (Same(c, settings.RampColour(d)))
                        {
                            draft.RampDirection[i] = d;
                            break;
                        }
                    }
                }
            }

            if (draft.Level[i] == NotLevel && draft.RampDirection[i] == NotRamp)
            {
                draft.UnknownCount++;
                string key = c.a == 255
                    ? string.Format("#{0:X2}{1:X2}{2:X2}", c.r, c.g, c.b)
                    : string.Format("#{0:X2}{1:X2}{2:X2} alpha {3}", c.r, c.g, c.b, c.a);
                List<Vector2Int> samples;
                if (!draft.UnknownColours.TryGetValue(key, out samples))
                {
                    samples = new List<Vector2Int>();
                    draft.UnknownColours[key] = samples;
                }
                if (samples.Count < 5)
                {
                    samples.Add(new Vector2Int(i % width, i / width));
                }
            }
        }

        draft.FindRegions();
        draft.FindSquares();
        draft.FindRamps();
        return draft;
    }

    private static bool Same(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b;
    }

    private int Kind(int index)
    {
        if (Level[index] != NotLevel)
        {
            return Level[index];
        }
        if (RampDirection[index] != NotRamp)
        {
            return 100 + RampDirection[index];
        }
        return -1;
    }

    private void FindRegions()
    {
        int[] stack = new int[Width * Height];
        for (int start = 0; start < Width * Height; start++)
        {
            int kind = Kind(start);
            if (kind < 0 || RegionOf[start] >= 0)
            {
                continue;
            }

            Region region = new Region
            {
                Id = Regions.Count,
                IsRamp = kind >= 100,
                Level = Level[start],
                Direction = RampDirection[start]
            };
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            int top = 0;
            stack[top++] = start;
            RegionOf[start] = region.Id;
            while (top > 0)
            {
                int index = stack[--top];
                int x = index % Width;
                int y = index / Width;
                region.PixelCount++;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                    int ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!InBounds(nx, ny))
                    {
                        continue;
                    }
                    int next = Index(nx, ny);
                    if (RegionOf[next] < 0 && Kind(next) == kind)
                    {
                        RegionOf[next] = region.Id;
                        stack[top++] = next;
                    }
                }
            }
            region.Bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            Regions.Add(region);
        }
    }

    // Largest square of one region per region (dynamic programming over the grid).
    private void FindSquares()
    {
        int[] side = new int[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = Index(x, y);
                int region = RegionOf[index];
                if (region < 0)
                {
                    continue;
                }
                int value = 1;
                if (x > 0 && y > 0)
                {
                    int left = Index(x - 1, y);
                    int down = Index(x, y - 1);
                    int diagonal = Index(x - 1, y - 1);
                    if (RegionOf[left] == region && RegionOf[down] == region && RegionOf[diagonal] == region)
                    {
                        value = Mathf.Min(Mathf.Min(side[left], side[down]), side[diagonal]) + 1;
                    }
                }
                side[index] = value;
                Region r = Regions[region];
                if (value > r.SquareSide)
                {
                    r.SquareSide = value;
                    r.SquareMin = new Vector2Int(x - value + 1, y - value + 1);
                }
            }
        }
    }

    private void FindRamps()
    {
        foreach (Region region in Regions)
        {
            if (!region.IsRamp)
            {
                continue;
            }
            Ramp ramp = new Ramp { Region = region, Direction = region.Direction };
            RectInt b = region.Bounds;
            bool alongY = ramp.Direction == 0 || ramp.Direction == 2;
            ramp.Length = alongY ? b.height : b.width;
            ramp.Width = alongY ? b.width : b.height;

            // North rises toward +y, east toward +x, south toward -y, west toward -x.
            int lowLine, highLine;
            switch (ramp.Direction)
            {
                case 0: lowLine = b.yMin - 1; highLine = b.yMax; break;
                case 1: lowLine = b.xMin - 1; highLine = b.xMax; break;
                case 2: lowLine = b.yMax; highLine = b.yMin - 1; break;
                default: lowLine = b.xMax; highLine = b.xMin - 1; break;
            }

            int from = alongY ? b.xMin : b.yMin;
            int to = alongY ? b.xMax : b.yMax;
            for (int across = from; across < to; across++)
            {
                AddEnd(ramp, alongY ? across : lowLine, alongY ? lowLine : across, true);
                AddEnd(ramp, alongY ? across : highLine, alongY ? highLine : across, false);
            }
            Ramps.Add(ramp);
        }
    }

    private void AddEnd(Ramp ramp, int x, int y, bool low)
    {
        int region = InBounds(x, y) ? RegionOf[Index(x, y)] : -1;
        if (region >= 0 && !Regions[region].IsRamp)
        {
            (low ? ramp.LowEnd : ramp.HighEnd).Add(region);
        }
        else if (low)
        {
            ramp.LowEndOther++;
        }
        else
        {
            ramp.HighEndOther++;
        }
    }

    /// <summary>Level region -> the level regions a ramp joins it to (both directions).</summary>
    public Dictionary<int, HashSet<int>> Links()
    {
        Dictionary<int, HashSet<int>> links = new Dictionary<int, HashSet<int>>();
        foreach (Ramp ramp in Ramps)
        {
            foreach (int a in ramp.LowEnd)
            {
                foreach (int b in ramp.HighEnd)
                {
                    Link(links, a, b);
                    Link(links, b, a);
                }
            }
        }
        return links;
    }

    private static void Link(Dictionary<int, HashSet<int>> links, int a, int b)
    {
        HashSet<int> set;
        if (!links.TryGetValue(a, out set))
        {
            set = new HashSet<int>();
            links[a] = set;
        }
        set.Add(b);
    }
}
