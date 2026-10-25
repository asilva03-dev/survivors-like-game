/*
 * TurretPlacer
 * The build function for defenses (scene object TurretPlacer). Build mode's ghost (BuildGhost) calls it when the
 * player left-clicks a tile; the old P debug key was removed in slice 6 step 4.
 *
 * - CanPlace(cell, definition, out reason) = the placement rules (tile rule, slice 6): a BuildStrip cell, its centre
 *   inside the base light (BaseLight.Contains), no turret on it, not touched by another turret's spacing circle (a
 *   red tile), and the new turret's own spacing circle must not touch a tile holding a turret. The reason is a short
 *   player-facing line shown under the ghost.
 * - TryBuild(definition, cell, out reason) also needs enough wood: it spends definition.WoodCost from the
 *   ResourceInventory and builds the turret on the tile centre (one turret per tile). TryBuildFree skips the wood
 *   (tests).
 * - Turrets are built with Instantiate (they are placed buildings, not pooled effects) under a runtime "Turrets"
 *   object.
 */

using UnityEngine;

public class TurretPlacer : MonoBehaviour
{
    public const string NotEnoughWood = "Not enough wood";

    [SerializeField] private ResourceInventory inventory;

    private Transform root;

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
        if (!CanPlace(cell, definition, out reason))
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
        if (!CanBuild(definition, cell, out reason) || !inventory.TrySpend(definition.WoodCost))
        {
            if (reason == null)
            {
                reason = NotEnoughWood;
            }
            return false;
        }

        Build(definition, cell);
        return true;
    }

    public bool TryBuildFree(TurretDefinition definition, Vector2Int cell, out string reason)
    {
        if (!CanPlace(cell, definition, out reason))
        {
            return false;
        }

        Build(definition, cell);
        return true;
    }

    private Turret Build(TurretDefinition definition, Vector2Int cell)
    {
        if (root == null)
        {
            root = new GameObject("Turrets").transform;
        }

        Turret built = Instantiate(definition.Prefab, BuildGrid.Instance.CellToWorld(cell), Quaternion.identity, root);
        built.Init(definition, cell);
        built.name = definition.Prefab.name + " " + cell.x + "," + cell.y;
        return built;
    }

    public static bool CanPlace(Vector2Int cell, TurretDefinition definition, out string reason)
    {
        BuildGrid grid = BuildGrid.Instance;
        if (definition == null || definition.Prefab == null)
        {
            reason = "No defense selected";
            return false;
        }

        if (grid == null || !grid.InBounds(cell) || grid.GetState(cell) != CellState.BuildStrip)
        {
            reason = "Build on the blue build strips";
            return false;
        }

        Vector3 center = grid.CellToWorld(cell);
        if (BaseLight.Instance != null && !BaseLight.Instance.Contains(center))
        {
            reason = "Outside the light";
            return false;
        }

        for (int i = 0; i < Turret.Active.Count; i++)
        {
            Turret other = Turret.Active[i];
            if (other.Cell == cell)
            {
                reason = "Tile already taken";
                return false;
            }

            if (other.CircleTouches(cell))
            {
                reason = "Too close to another defense";
                return false;
            }

            if (Turret.CircleTouches(center, definition.SpacingRadius, other.Cell))
            {
                reason = "Too close to another defense";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
