/*
 * MapDraftSettings
 * Settings for the painted map draft (slice 8): which image holds the height layer, how big a pixel and a level are in
 * the world, and the exact palette colours the image is painted with. One asset: Assets/MapDraft/MapDraftSettings.
 *
 * - heightImage = map_height.png (painted in Aseprite). It is read straight from the PNG file (MapDraft.Read), so the
 *   texture's import settings never change the colours.
 * - unitsPerPixel / levelHeight: one pixel covers unitsPerPixel x unitsPerPixel world units; each level is levelHeight
 *   units above the one below. Changing them rescales the map without repainting.
 * - minPlateauSize: the smallest flat square (world units) the checker lists as big enough for a base spawn (base +
 *   light + its enemy spawn ring, ~150 today).
 * - Palette: levelColours[i] = level i (0 = the floor); one colour per ramp direction (the side it rises toward; north
 *   = up in the image). Must match Assets/MapDraft/map-palette.gpl.
 * - Greybox (step 2, Editor/MapGreyboxBuilder): mapCenter = world position of the image's centre (the floor sits at
 *   its y); edgeSkirt = how far the walls along the map border reach below the floor; edgeWallHeight = height of the
 *   invisible walls that keep everyone on the map.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "TD/Map Draft Settings", fileName = "MapDraftSettings")]
public class MapDraftSettings : ScriptableObject
{
    [Tooltip("The painted height layer (map_height.png). Read from the PNG file itself, so its import settings don't matter.")]
    [SerializeField] private Texture2D heightImage;
    [Tooltip("World units covered by one pixel (each way).")]
    [SerializeField, Min(0.25f)] private float unitsPerPixel = 4f;
    [Tooltip("World units between two levels.")]
    [SerializeField, Min(0.25f)] private float levelHeight = 4f;
    [Tooltip("Smallest flat square (world units) listed as big enough for a base spawn plateau.")]
    [SerializeField, Min(1f)] private float minPlateauSize = 150f;

    [Header("Palette (exact colours)")]
    [Tooltip("Level 0 (the floor) first, then each level up.")]
    [SerializeField] private Color32[] levelColours =
    {
        new Color32(40, 40, 40, 255),
        new Color32(80, 80, 80, 255),
        new Color32(120, 120, 120, 255),
        new Color32(160, 160, 160, 255),
        new Color32(200, 200, 200, 255)
    };
    [SerializeField] private Color32 rampNorth = new Color32(255, 0, 0, 255);
    [SerializeField] private Color32 rampEast = new Color32(255, 255, 0, 255);
    [SerializeField] private Color32 rampSouth = new Color32(0, 120, 255, 255);
    [SerializeField] private Color32 rampWest = new Color32(255, 0, 255, 255);

    [Header("Greybox")]
    [Tooltip("World position of the image's centre; the floor (level 0) is at this y.")]
    [SerializeField] private Vector3 mapCenter = Vector3.zero;
    [Tooltip("How far the walls along the map border reach below the floor.")]
    [SerializeField, Min(0f)] private float edgeSkirt = 2f;
    [Tooltip("Height of the invisible walls around the map.")]
    [SerializeField, Min(1f)] private float edgeWallHeight = 40f;

    public Vector3 MapCenter => mapCenter;
    public float EdgeSkirt => edgeSkirt;
    public float EdgeWallHeight => edgeWallHeight;

    public Texture2D HeightImage => heightImage;
    public float UnitsPerPixel => unitsPerPixel;
    public float LevelHeight => levelHeight;
    public float MinPlateauSize => minPlateauSize;
    public int LevelCount => levelColours != null ? levelColours.Length : 0;

    public Color32 LevelColour(int level) => levelColours[level];

    /// <summary>The ramp colour for a direction (0 north, 1 east, 2 south, 3 west).</summary>
    public Color32 RampColour(int direction)
    {
        switch (direction)
        {
            case 0: return rampNorth;
            case 1: return rampEast;
            case 2: return rampSouth;
            default: return rampWest;
        }
    }
}
