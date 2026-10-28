/*
 * MapDraftSettingsEditor
 * Inspector for MapDraftSettings with the draft checker (slice 8 step 1): "Check image" reads the height image's PNG
 * file and lists what it found and anything wrong, as boxes under the settings and as one Console message. Nothing is
 * built or changed. Menu: Tools > Map > Check Draft (selects the settings asset and runs the check).
 *
 * - Positions in messages are Aseprite pixel coordinates (x from the left, y from the top), so they can be found in
 *   the painting directly; sizes are given in pixels and world units.
 * - Errors: unknown colours, ramps whose ends don't both touch a level, ramps joining equal levels, ramps painted the
 *   wrong way (the low end is higher: says which colour it should be). Warnings: ramps that aren't rectangles, narrow
 *   ramps, levels no ramp connects to the floor. Info: size, levels, every ramp (levels, length, slope), and the flat
 *   areas big enough for a base spawn (minPlateauSize).
 * - Reachability ignores walking direction and drops: a level counts as reachable when ramps join it to the floor.
 * - "Build greybox" (step 2) runs MapGreyboxBuilder.Build and shows its result in the same place.
 */

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapDraftSettings))]
public class MapDraftSettingsEditor : Editor
{
    public struct Line
    {
        public MessageType Type;
        public string Text;
    }

    private List<Line> lines;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Check image", GUILayout.Height(28)))
        {
            lines = Check((MapDraftSettings)target, true);
        }
        if (GUILayout.Button("Build greybox", GUILayout.Height(28)))
        {
            string summary;
            bool built = MapGreyboxBuilder.Build((MapDraftSettings)target, out summary);
            lines = new List<Line> { new Line { Type = built ? MessageType.Info : MessageType.Error, Text = summary } };
        }
        EditorGUILayout.EndHorizontal();
        if (lines == null)
        {
            return;
        }
        foreach (Line line in lines)
        {
            EditorGUILayout.HelpBox(line.Text, line.Type);
        }
    }

    [MenuItem("Tools/Map/Check Draft")]
    private static void CheckFromMenu()
    {
        string[] guids = AssetDatabase.FindAssets("t:MapDraftSettings");
        if (guids.Length == 0)
        {
            Debug.LogWarning("Map draft: no MapDraftSettings asset found (Create > TD > Map Draft Settings).");
            return;
        }
        MapDraftSettings settings = AssetDatabase.LoadAssetAtPath<MapDraftSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        Selection.activeObject = settings;
        Check(settings, true);
    }

    /// <summary>Reads the settings' height image from its PNG file. False (with the reason) if it can't.</summary>
    public static bool TryLoad(MapDraftSettings settings, out MapDraft draft, out string error)
    {
        draft = null;
        error = null;
        if (settings == null || settings.HeightImage == null)
        {
            error = "No height image set.";
            return false;
        }
        if (settings.LevelCount == 0)
        {
            error = "The palette has no level colours.";
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(settings.HeightImage);
        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        if (!File.Exists(fullPath))
        {
            error = "Image file not found: " + assetPath;
            return false;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(fullPath), false))
            {
                error = "Could not read " + assetPath + " as an image.";
                return false;
            }
            draft = MapDraft.Read(texture.GetPixels32(), texture.width, texture.height, settings);
            return true;
        }
        finally
        {
            DestroyImmediate(texture);
        }
    }

    public static List<Line> Check(MapDraftSettings settings, bool log)
    {
        List<Line> result = new List<Line>();
        MapDraft draft;
        string error;
        if (!TryLoad(settings, out draft, out error))
        {
            result.Add(new Line { Type = MessageType.Error, Text = error });
            Log(result, log);
            return result;
        }

        float unit = settings.UnitsPerPixel;
        int errors = 0;
        int warnings = 0;

        result.Add(Info(string.Format("Image {0} x {1} px = a {2} x {3} unit map ({4} units per pixel, {5} units per level).",
            draft.Width, draft.Height, draft.Width * unit, draft.Height * unit, unit, settings.LevelHeight)));

        // Unknown colours.
        if (draft.UnknownCount > 0)
        {
            foreach (KeyValuePair<string, List<Vector2Int>> pair in draft.UnknownColours)
            {
                StringBuilder at = new StringBuilder();
                foreach (Vector2Int p in pair.Value)
                {
                    at.AppendFormat(" ({0}, {1})", p.x, draft.AsepriteY(p.y));
                }
                result.Add(Error(string.Format("Colour {0} is not in the palette, e.g. at{1}.", pair.Key, at), ref errors));
            }
            result.Add(Error(draft.UnknownCount + " pixels in all have unknown colours.", ref errors));
        }

        // Levels.
        int[] levelAreas = new int[settings.LevelCount];
        int[] levelPixels = new int[settings.LevelCount];
        foreach (MapDraft.Region region in draft.Regions)
        {
            if (!region.IsRamp)
            {
                levelAreas[region.Level]++;
                levelPixels[region.Level] += region.PixelCount;
            }
        }
        StringBuilder levels = new StringBuilder("Levels:");
        for (int i = 0; i < settings.LevelCount; i++)
        {
            levels.AppendFormat("\n  L{0} (height {1}): {2} area(s), {3} px", i, i * settings.LevelHeight, levelAreas[i], levelPixels[i]);
        }
        result.Add(Info(levels.ToString()));

        // Ramps.
        StringBuilder rampList = new StringBuilder("Ramps (" + draft.Ramps.Count + "):");
        foreach (MapDraft.Ramp ramp in draft.Ramps)
        {
            string where = Where(draft, ramp.Region.Bounds);
            string name = "Ramp rising " + MapDraft.DirectionNames[ramp.Direction] + " " + where;
            if (ramp.Region.PixelCount != ramp.Region.Bounds.width * ramp.Region.Bounds.height)
            {
                result.Add(Warning(name + " is not a rectangle; its ends are read from its outer box.", ref warnings));
            }
            if (ramp.Width < 2)
            {
                result.Add(Warning(name + " is only " + ramp.Width + " px wide (" + ramp.Width * unit + " units).", ref warnings));
            }
            if (ramp.LowEnd.Count == 0 || ramp.HighEnd.Count == 0 || ramp.LowEndOther > 0 || ramp.HighEndOther > 0)
            {
                result.Add(Error(name + ": an end doesn't touch a level all the way across (map edge, another ramp or an unknown colour).", ref errors));
                continue;
            }
            if (ramp.LowEnd.Count > 1 || ramp.HighEnd.Count > 1)
            {
                result.Add(Warning(name + ": one end touches more than one area.", ref warnings));
            }

            int low = MinLevel(draft, ramp.LowEnd);
            int high = MaxLevel(draft, ramp.HighEnd);
            if (low == high)
            {
                result.Add(Error(name + " joins L" + low + " to L" + high + " (no climb).", ref errors));
                continue;
            }
            if (low > high)
            {
                int reversed = (ramp.Direction + 2) % 4;
                result.Add(Error(string.Format("{0} is painted the wrong way: it climbs from L{1} to L{2} toward the {3}. Paint it as \"rises {3}\".",
                    name, high, low, MapDraft.DirectionNames[reversed]), ref errors));
                continue;
            }
            float rise = (high - low) * settings.LevelHeight;
            float run = ramp.Length * unit;
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            rampList.AppendFormat("\n  {0}: L{1} -> L{2}, {3} px long ({4} units), {5} px wide, slope {6:0.0} deg",
                where + " rising " + MapDraft.DirectionNames[ramp.Direction], low, high, ramp.Length, run, ramp.Width, slope);
        }
        result.Add(Info(rampList.ToString()));

        // Reachability from the biggest floor area.
        MapDraft.Region floor = null;
        foreach (MapDraft.Region region in draft.Regions)
        {
            if (!region.IsRamp && region.Level == 0 && (floor == null || region.PixelCount > floor.PixelCount))
            {
                floor = region;
            }
        }
        if (floor == null)
        {
            result.Add(Warning("There is no floor (L0) area, so reachability wasn't checked.", ref warnings));
        }
        else
        {
            Dictionary<int, HashSet<int>> links = draft.Links();
            HashSet<int> reached = new HashSet<int> { floor.Id };
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(floor.Id);
            while (queue.Count > 0)
            {
                HashSet<int> next;
                if (links.TryGetValue(queue.Dequeue(), out next))
                {
                    foreach (int id in next)
                    {
                        if (reached.Add(id))
                        {
                            queue.Enqueue(id);
                        }
                    }
                }
            }
            int unreached = 0;
            foreach (MapDraft.Region region in draft.Regions)
            {
                if (!region.IsRamp && !reached.Contains(region.Id))
                {
                    unreached++;
                    result.Add(Warning(string.Format("L{0} area {1} ({2} px) has no ramp route to the floor.",
                        region.Level, Where(draft, region.Bounds), region.PixelCount), ref warnings));
                }
            }
            if (unreached == 0)
            {
                result.Add(Info("Every level area is joined to the floor by ramps."));
            }
        }

        // Plateau candidates.
        int minSide = Mathf.CeilToInt(settings.MinPlateauSize / unit);
        StringBuilder plateaus = new StringBuilder(string.Format("Flat areas with a square of at least {0} px ({1} units), big enough for a base spawn:", minSide, minSide * unit));
        int plateauCount = 0;
        foreach (MapDraft.Region region in draft.Regions)
        {
            if (region.IsRamp || region.SquareSide < minSide)
            {
                continue;
            }
            plateauCount++;
            RectInt square = new RectInt(region.SquareMin.x, region.SquareMin.y, region.SquareSide, region.SquareSide);
            plateaus.AppendFormat("\n  L{0} area {1}: square {2} px ({3} units) at {4}",
                region.Level, Where(draft, region.Bounds), region.SquareSide, region.SquareSide * unit, Where(draft, square));
        }
        if (plateauCount == 0)
        {
            plateaus.Append("\n  none");
        }
        result.Add(Info(plateaus.ToString()));

        result.Insert(0, new Line
        {
            Type = errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info,
            Text = string.Format("Map draft check: {0} error(s), {1} warning(s).", errors, warnings)
        });
        Log(result, log);
        return result;
    }

    // Aseprite coordinates of a rect: x left->right, y top->down.
    private static string Where(MapDraft draft, RectInt r)
    {
        return string.Format("x {0}-{1}, y {2}-{3}", r.xMin, r.xMax - 1, draft.AsepriteY(r.yMax - 1), draft.AsepriteY(r.yMin));
    }

    private static int MinLevel(MapDraft draft, HashSet<int> regions)
    {
        int level = int.MaxValue;
        foreach (int id in regions)
        {
            level = Mathf.Min(level, draft.Regions[id].Level);
        }
        return level;
    }

    private static int MaxLevel(MapDraft draft, HashSet<int> regions)
    {
        int level = int.MinValue;
        foreach (int id in regions)
        {
            level = Mathf.Max(level, draft.Regions[id].Level);
        }
        return level;
    }

    private static Line Info(string text)
    {
        return new Line { Type = MessageType.Info, Text = text };
    }

    private static Line Warning(string text, ref int warnings)
    {
        warnings++;
        return new Line { Type = MessageType.Warning, Text = text };
    }

    private static Line Error(string text, ref int errors)
    {
        errors++;
        return new Line { Type = MessageType.Error, Text = text };
    }

    private static void Log(List<Line> lines, bool log)
    {
        if (!log)
        {
            return;
        }
        StringBuilder text = new StringBuilder();
        bool bad = false;
        foreach (Line line in lines)
        {
            text.AppendLine(line.Text);
            bad |= line.Type == MessageType.Error;
        }
        if (bad)
        {
            Debug.LogWarning(text.ToString());
        }
        else
        {
            Debug.Log(text.ToString());
        }
    }
}
