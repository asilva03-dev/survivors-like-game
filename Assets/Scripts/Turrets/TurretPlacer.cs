/*
 * TurretPlacer
 * The build function for defenses (scene object TurretPlacer). Build mode's ghost (BuildGhost) calls it when the
 * player left-clicks a tile; the old P debug key was removed in slice 6 step 4.
 *
 * - CanPlace(cell, definition, rotation, out reason) = the placement rules (tile rule, slice 6), for every tile of the
 *   turret (cell = its corner tile + the definition's footprint turned `rotation` quarter turns; 2026-11-23): a
 *   BuildStrip cell, its centre inside the base light (BaseLight.Contains), no turret on it, not touched by another
 *   turret's spacing circle (a red tile); and the new turret's own spacing circle (around its corner tile) must not touch
 *   a tile holding a turret. The reason is a short player-facing line shown under the ghost. Overloads without a
 *   rotation use 0.
 * - TryBuild(definition, cell, out reason) also needs enough wood: it spends definition.WoodCost from the
 *   ResourceInventory and builds the turret on its corner tile's centre, its root turned rotation x 90 deg. TryBuildFree
 *   skips the wood (tests).
 * - Turrets are built with Instantiate (they are placed buildings, not pooled effects) under a runtime "Turrets"
 *   object.
 */

using System.Collections.Generic;
using UnityEngine;

public class TurretPlacer : MonoBehaviour
{
    public const string NotEnoughWood = "Not enough wood";

    [SerializeField] private ResourceInventory inventory;

    private Transform root;
    private static readonly List<Vector2Int> newCells = new List<Vector2Int>();

    public static TurretPlacer Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (inventory == null)
        {
            inventory = FindAnyObjectByType<ResourceInventory>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Placement rules plus the wood check, without building anything (the ghost's green/red).
    public bool CanBuild(TurretDefinition definition, Vector2Int cell, out string reason)
    {
        return CanBuild(definition, cell, 0, out reason);
    }

    public bool CanBuild(TurretDefinition definition, Vector2Int cell, int rotation, out string reason)
    {
        if (!CanPlace(cell, definition, rotation, out reason))
        {
            return false;
        }

        if (inventory == null || inventory.Wood < definition.WoodCost)
        {
            reason = NotEnoughWood;
            return false;
        }

        return true;
    }

    public bool TryBuild(TurretDefinition definition, Vector2Int cell, out string reason)
    {
        return TryBuild(definition, cell, 0, out reason);
    }

    public bool TryBuild(TurretDefinition definition, Vector2Int cell, int rotation, out string reason)
    {
        if (!CanBuild(definition, cell, rotation, out reason) || !inventory.TrySpend(definition.WoodCost))
        {
            if (reason == null)
            {
                reason = NotEnoughWood;
            }
            return false;
        }

        Build(definition, cell, rotation);
        return true;
    }

    public bool TryBuildFree(TurretDefinition definition, Vector2Int cell, out string reason)
    {
        return TryBuildFree(definition, cell, 0, out reason);
    }

    public bool TryBuildFree(TurretDefinition definition, Vector2Int cell, int rotation, out string reason)
    {
        if (!CanPlace(cell, definition, rotation, out reason))
        {
            return false;
        }

        Build(definition, cell, rotation);
        return true;
    }

    private Turret Build(TurretDefinition definition, Vector2Int cell, int rotation)
    {
        if (root == null)
        {
            root = new GameObject("Turrets").transform;
        }

        int turn = definition.Rotatable ? ((rotation % 4) + 4) % 4 : 0;
        Turret built = Instantiate(definition.Prefab, BuildGrid.Instance.CellToWorld(cell), Quaternion.Euler(0f, turn * 90f, 0f), root);
        built.Init(definition, cell, turn);
        built.name = definition.Prefab.name + " " + cell.x + "," + cell.y + (turn != 0 ? " r" + turn : "");
        return built;
    }

    public static bool CanPlace(Vector2Int cell, TurretDefinition definition, out string reason)
    {
        return CanPlace(cell, definition, 0, out reason);
    }

    public static bool CanPlace(Vector2Int cell, TurretDefinition definition, int rotation, out string reason)
    {
        BuildGrid grid = BuildGrid.Instance;
        if (definition == null || definition.Prefab == null)
        {
            reason = "No defense selected";
            return false;
        }

        if (grid == null)
        {
            reason = "Build on the blue build strips";
            return false;
        }

        definition.Cells(cell, definition.Rotatable ? rotation : 0, newCells);
        for (int c = 0; c < newCells.Count; c++)
        {
            Vector2Int tile = newCells[c];
            if (!grid.InBounds(tile) || grid.GetState(tile) != CellState.BuildStrip)
            {
                reason = "Build on the blue build strips";
                return false;
            }

            if (BaseLight.Instance != null && !BaseLight.Instance.Contains(grid.CellToWorld(tile)))
            {
                reason = "Outside the light";
                return false;
            }
        }

        Vector3 center = grid.CellToWorld(cell);
        for (int i = 0; i < Turret.Active.Count; i++)
        {
            Turret other = Turret.Active[i];
            for (int c = 0; c < newCells.Count; c++)
            {
                if (other.Occupies(newCells[c]))
                {
                    reason = "Tile already taken";
                    return false;
                }

                if (other.CircleTouches(newCells[c]))
                {
                    reason = "Too close to another defense";
                    return false;
                }
            }

            IReadOnlyList<Vector2Int> otherCells = other.Cells;
            for (int c = 0; c < otherCells.Count; c++)
            {
                if (Turret.CircleTouches(center, definition.SpacingRadius, otherCells[c]))
                {
                    reason = "Too close to another defense";
                    return false;
                }
            }
        }

        reason = null;
        return true;
    }
}
