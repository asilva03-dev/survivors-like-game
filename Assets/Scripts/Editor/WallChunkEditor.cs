/*
 * WallChunkEditor
 * Painting inspector for WallChunk assets: pick a brush, then click or drag over the cell grid to paint; right-click
 * (or drag with the right button) erases to Empty. North is up, matching the game camera.
 *
 * - Colours match the R grid overlay (Path cyan, Wall grey, Build strip blue, Base orange); Empty is dark.
 * - Thick lines mark module borders. Door spots are outlined on the outer edge: green when open (all 4 cells Path),
 *   red when closed. Doors are read from the painting, so painting path to an edge's door spot opens it.
 * - Size sliders resize the chunk (painting that still fits is kept). Fill paints every cell with the brush, Clear
 *   empties the chunk. Every change supports Undo (Ctrl+Z) and saves with the asset.
 * - Painting mistakes (path on an edge outside a door spot, half-painted doors) are listed as warnings below the grid.
 */

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WallChunk))]
public class WallChunkEditor : Editor
{
    private static readonly string[] BrushNames = { "Empty", "Path", "Wall", "Build strip", "Base" };
    private static readonly Color[] CellColors =
    {
        new Color(0.16f, 0.16f, 0.16f),
        new Color(0.3f, 0.8f, 0.95f),
        new Color(0.62f, 0.62f, 0.62f),
        new Color(0.3f, 0.45f, 1f),
        new Color(1f, 0.6f, 0.1f)
    };
    private static readonly Color LineColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color ModuleLineColor = new Color(0f, 0f, 0f, 0.9f);
    private static readonly Color OpenDoorColor = new Color(0.2f, 1f, 0.3f);
    private static readonly Color ClosedDoorColor = new Color(1f, 0.25f, 0.2f);

    private const float MaxCellPixels = 22f;
    private const float MinCellPixels = 6f;
    private const float DoorMarker = 4f;

    private static ChunkCell brush = ChunkCell.Wall;

    private int controlId;
    private ChunkCell dragValue;

    public override void OnInspectorGUI()
    {
        WallChunk chunk = (WallChunk)target;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        DrawSize(chunk);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        brush = (ChunkCell)GUILayout.Toolbar((int)brush, BrushNames);
        DrawLegend();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fill with " + BrushNames[(int)brush]))
            {
                Undo.RecordObject(chunk, "Fill Wall Chunk");
                chunk.Fill(brush);
                EditorUtility.SetDirty(chunk);
            }
            if (GUILayout.Button("Clear"))
            {
                Undo.RecordObject(chunk, "Clear Wall Chunk");
                chunk.Fill(ChunkCell.Empty);
                EditorUtility.SetDirty(chunk);
            }
        }

        EditorGUILayout.Space();
        DrawGrid(chunk);
        EditorGUILayout.Space();
        DrawDoors(chunk);
        DrawProblems(chunk);
    }

    private void DrawSize(WallChunk chunk)
    {
        EditorGUILayout.LabelField("Size (modules of " + WallChunk.ModuleSize + "x" + WallChunk.ModuleSize + " cells)",
            EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int modulesX = EditorGUILayout.IntSlider("Modules east-west", chunk.ModulesX, 1, 4);
        int modulesY = EditorGUILayout.IntSlider("Modules north-south", chunk.ModulesY, 1, 4);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(chunk, "Resize Wall Chunk");
            chunk.Resize(modulesX, modulesY);
            EditorUtility.SetDirty(chunk);
        }
        EditorGUILayout.LabelField(" ", chunk.Width + " x " + chunk.Height + " cells");
    }

    private void DrawLegend()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < BrushNames.Length; i++)
            {
                Rect swatch = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f));
                EditorGUI.DrawRect(swatch, CellColors[i]);
                GUILayout.Label(BrushNames[i], EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            }
        }
        EditorGUILayout.LabelField("Left click / drag paints, right click erases. North is up.", EditorStyles.miniLabel);
    }

    private void DrawGrid(WallChunk chunk)
    {
        float available = EditorGUIUtility.currentViewWidth - 40f - DoorMarker * 2f;
        float cell = Mathf.Clamp(Mathf.Floor(available / chunk.Width), MinCellPixels, MaxCellPixels);
        float gridWidth = cell * chunk.Width;
        float gridHeight = cell * chunk.Height;

        Rect outer = GUILayoutUtility.GetRect(gridWidth + DoorMarker * 2f, gridHeight + DoorMarker * 2f,
            GUILayout.ExpandWidth(false));
        Rect grid = new Rect(outer.x + DoorMarker, outer.y + DoorMarker, gridWidth, gridHeight);

        HandleMouse(chunk, grid, cell);

        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                EditorGUI.DrawRect(CellRect(grid, cell, chunk.Height, x, y), CellColors[(int)chunk.GetCell(x, y)]);
            }
        }

        for (int x = 0; x <= chunk.Width; x++)
        {
            bool module = x % WallChunk.ModuleSize == 0;
            float width = module ? 2f : 1f;
            EditorGUI.DrawRect(new Rect(grid.x + x * cell - width * 0.5f, grid.y, width, gridHeight),
                module ? ModuleLineColor : LineColor);
        }
        for (int y = 0; y <= chunk.Height; y++)
        {
            bool module = y % WallChunk.ModuleSize == 0;
            float width = module ? 2f : 1f;
            EditorGUI.DrawRect(new Rect(grid.x, grid.y + y * cell - width * 0.5f, gridWidth, width),
                module ? ModuleLineColor : LineColor);
        }

        DrawDoorMarkers(chunk, grid, cell);
    }

    // Row 0 (south) is drawn at the bottom so the grid reads like the game's top-down view.
    private static Rect CellRect(Rect grid, float cell, int height, int x, int y)
    {
        return new Rect(grid.x + x * cell, grid.y + (height - 1 - y) * cell, cell, cell);
    }

    /// <summary>The chunk cell under a GUI point, or (-1,-1) outside the grid.</summary>
    public static Vector2Int CellAt(Rect grid, float cell, int width, int height, Vector2 point)
    {
        if (!grid.Contains(point))
        {
            return new Vector2Int(-1, -1);
        }
        int x = Mathf.Clamp(Mathf.FloorToInt((point.x - grid.x) / cell), 0, width - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt((point.y - grid.y) / cell), 0, height - 1);
        return new Vector2Int(x, height - 1 - row);
    }

    private void HandleMouse(WallChunk chunk, Rect grid, float cell)
    {
        controlId = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;

        switch (e.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if ((e.button == 0 || e.button == 1) && grid.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    dragValue = e.button == 0 ? brush : ChunkCell.Empty;
                    Undo.RecordObject(chunk, "Paint Wall Chunk");
                    PaintAt(chunk, grid, cell, e.mousePosition);
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId)
                {
                    PaintAt(chunk, grid, cell, e.mousePosition);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;

            case EventType.ContextClick:
                if (grid.Contains(e.mousePosition))
                {
                    e.Use();
                }
                break;
        }
    }

    private void PaintAt(WallChunk chunk, Rect grid, float cell, Vector2 point)
    {
        Vector2Int c = CellAt(grid, cell, chunk.Width, chunk.Height, point);
        if (c.x < 0 || chunk.GetCell(c.x, c.y) == dragValue)
        {
            return;
        }
        chunk.SetCell(c.x, c.y, dragValue);
        EditorUtility.SetDirty(chunk);
        Repaint();
    }

    private static void DrawDoorMarkers(WallChunk chunk, Rect grid, float cell)
    {
        for (int s = 0; s < 4; s++)
        {
            ChunkSide side = (ChunkSide)s;
            for (int door = 0; door < chunk.DoorCount(side); door++)
            {
                Color color = chunk.IsDoorOpen(side, door) ? OpenDoorColor : ClosedDoorColor;
                float start = (door * WallChunk.ModuleSize + WallChunk.DoorStart) * cell;
                float length = WallChunk.DoorWidth * cell;
                Rect marker;
                switch (side)
                {
                    case ChunkSide.North:
                        marker = new Rect(grid.x + start, grid.y - DoorMarker, length, DoorMarker);
                        break;
                    case ChunkSide.South:
                        marker = new Rect(grid.x + start, grid.yMax, length, DoorMarker);
                        break;
                    case ChunkSide.East:
                        marker = new Rect(grid.xMax, grid.yMax - start - length, DoorMarker, length);
                        break;
                    default:
                        marker = new Rect(grid.x - DoorMarker, grid.yMax - start - length, DoorMarker, length);
                        break;
                }
                EditorGUI.DrawRect(marker, color);
            }
        }
    }

    private static void DrawDoors(WallChunk chunk)
    {
        EditorGUILayout.LabelField("Doors (read from the painting)", EditorStyles.boldLabel);
        for (int s = 0; s < 4; s++)
        {
            ChunkSide side = (ChunkSide)s;
            string text = "";
            for (int door = 0; door < chunk.DoorCount(side); door++)
            {
                text += (door > 0 ? ", " : "") + (chunk.IsDoorOpen(side, door) ? "open" : "closed");
            }
            EditorGUILayout.LabelField(side.ToString(), text);
        }
    }

    private static void DrawProblems(WallChunk chunk)
    {
        var problems = chunk.GetProblems();
        if (problems.Count == 0)
        {
            EditorGUILayout.HelpBox("No painting problems.", MessageType.Info);
            return;
        }
        EditorGUILayout.HelpBox(string.Join("\n", problems.ToArray()), MessageType.Warning);
    }
}
