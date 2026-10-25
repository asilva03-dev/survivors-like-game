/*
 * Turret
 * Root component of a built turret. Knows its definition and the build-strip tile it stands on, and keeps the
 * static Active list that placement checks against.
 *
 * - Placed by TurretPlacer (P key for now) with Init(definition, cell); the root sits on the tile centre at
 *   ground height and never rotates (the TurretGun child turns the body).
 * - No health and never targeted (walls and turrets can't be damaged). No collider that blocks the player.
 * - RestDirection (set in Init): toward the nearest Path cell within 12 cells (else the nearest walkable cell,
 *   else north). The gun starts facing it and scans around it while idle (see TurretGun).
 * - Range comes from its TurretGun; SpacingRadius from the definition. The no-build area is the set of tiles its
 *   spacing circle touches (CircleTouches; the tile rule, slice 6); BuildStripHighlight paints those strip tiles red.
 *   Static event ActiveChanged fires when a turret is placed or removed.
 * - Upgrades (slice 6 step 5): Tier(path), NextUpgrade(path), CanUpgrade(path, out reason) (the Bloons rule from the
 *   definition: one path to MainPathMaxTier, one other to SecondPathMaxTier, the third 0; reasons "Max tier" /
 *   "Path locked"), TryUpgrade(path, inventory, out reason) spends the tier's wood. After an upgrade the gun gets
 *   this turret's own copy of the base stats with every bought tier applied (the shared asset is never changed).
 *   TotalSpent = build cost + upgrades. Event Upgraded.
 * - Selling (slice 6 step 7): SellValue = definition.SellRefundShare (0.5) x TotalSpent, rounded down; Sell(inventory)
 *   refunds it and removes the turret.
 */

using System.Collections.Generic;
using UnityEngine;

public class Turret : MonoBehaviour
{
    public static readonly List<Turret> Active = new List<Turret>();

    // Raised when a turret is placed (after Init) or removed, so no-build tiles can be recoloured.
    public static event System.Action ActiveChanged;

    private const int RestSearchRadius = 12;
    private const float TouchEpsilon = 0.001f;

    [SerializeField] private TurretDefinition definition;
    [SerializeField] private TurretGun gun;

    public TurretDefinition Definition => definition;
    public Vector2Int Cell { get; private set; }
    public Vector3 RestDirection { get; private set; } = Vector3.forward;
    public float SpacingRadius => definition != null ? definition.SpacingRadius : 0f;
    public float Range => gun != null ? gun.Range : 0f;
    public Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);

    public const string MaxTierReason = "Max tier";
    public const string LockedReason = "Path locked";

    private int[] tiers = new int[0];
    private GunStats baseStats;
    private GunStats upgradedStats;

    // Raised after an upgrade is bought (the panel refreshes).
    public event System.Action Upgraded;

    // Wood spent on this turret (build cost + upgrades); selling refunds a share of it (step 7).
    public int TotalSpent { get; private set; }

    // Wood back when sold: the definition's refund share of TotalSpent, rounded down.
    public int SellValue => definition != null ? Mathf.FloorToInt(TotalSpent * definition.SellRefundShare + 0.0001f) : 0;

    // Refunds SellValue and removes the turret (its red tiles clear through ActiveChanged).
    public int Sell(ResourceInventory inventory)
    {
        int refund = SellValue;
        if (inventory != null)
        {
            inventory.AddWood(refund);
        }
        gameObject.SetActive(false); // leaves Turret.Active at once
        Destroy(gameObject);
        return refund;
    }

    public int PathCount => definition != null ? definition.Paths.Count : 0;

    public int Tier(int path)
    {
        return path >= 0 && path < tiers.Length ? tiers[path] : 0;
    }

    // The next upgrade on a path, or null when the path has no more tiers.
    public TurretUpgrade NextUpgrade(int path)
    {
        if (definition == null || path < 0 || path >= definition.Paths.Count)
        {
            return null;
        }

        List<TurretUpgrade> pathTiers = definition.Paths[path].tiers;
        int next = Tier(path);
        return next < pathTiers.Count ? pathTiers[next] : null;
    }

    // Bloons rule: one path up to MainPathMaxTier, one other up to SecondPathMaxTier, the third stays 0.
    public bool CanUpgrade(int path, out string reason)
    {
        if (NextUpgrade(path) == null)
        {
            reason = MaxTierReason;
            return false;
        }

        int[] after = (int[])tiers.Clone();
        after[path]++;
        int used = 0;
        int aboveSecond = 0;
        for (int i = 0; i < after.Length; i++)
        {
            if (after[i] > definition.MainPathMaxTier)
            {
                reason = MaxTierReason;
                return false;
            }
            if (after[i] > 0)
            {
                used++;
            }
            if (after[i] > definition.SecondPathMaxTier)
            {
                aboveSecond++;
            }
        }

        if (used > 2 || aboveSecond > 1)
        {
            reason = LockedReason;
            return false;
        }

        reason = null;
        return true;
    }

    public bool TryUpgrade(int path, ResourceInventory inventory, out string reason)
    {
        if (!CanUpgrade(path, out reason))
        {
            return false;
        }

        TurretUpgrade upgrade = NextUpgrade(path);
        if (inventory == null || !inventory.TrySpend(upgrade.WoodCost))
        {
            reason = TurretPlacer.NotEnoughWood;
            return false;
        }

        tiers[path]++;
        TotalSpent += upgrade.WoodCost;
        RebuildStats();
        Upgraded?.Invoke();
        return true;
    }

    // Base stats copy + every bought tier (path by path, in order), handed to the gun.
    private void RebuildStats()
    {
        if (gun == null || baseStats == null)
        {
            return;
        }

        if (upgradedStats != null)
        {
            Destroy(upgradedStats);
        }

        upgradedStats = Instantiate(baseStats);
        upgradedStats.name = baseStats.name + " (" + name + ")";
        for (int path = 0; path < tiers.Length; path++)
        {
            for (int tier = 0; tier < tiers[path]; tier++)
            {
                definition.Paths[path].tiers[tier].ApplyTo(upgradedStats);
            }
        }
        gun.UseStats(upgradedStats);
    }

    public void Init(TurretDefinition turretDefinition, Vector2Int cell)
    {
        definition = turretDefinition;
        Cell = cell;
        tiers = new int[definition != null ? definition.Paths.Count : 0];
        TotalSpent = definition != null ? definition.WoodCost : 0;
        RestDirection = FindRestDirection(cell);
        if (gun != null)
        {
            gun.FaceRest();
        }
        ActiveChanged?.Invoke();
    }

    // Does this turret's no-build circle touch the cell (any part of the cell square inside the circle)?
    public bool CircleTouches(Vector2Int cell)
    {
        return CircleTouches(Center, SpacingRadius, cell);
    }

    // Shared tile rule: a circle (flat centre, radius) touches a grid cell when the cell square's nearest point is
    // closer than the radius. Touching exactly at the rim doesn't count.
    public static bool CircleTouches(Vector3 center, float radius, Vector2Int cell)
    {
        BuildGrid grid = BuildGrid.Instance;
        if (grid == null || radius <= 0f)
        {
            return false;
        }

        Vector3 cellCenter = grid.CellToWorld(cell);
        float half = grid.CellSize * 0.5f;
        float nearestX = Mathf.Clamp(center.x, cellCenter.x - half, cellCenter.x + half);
        float nearestZ = Mathf.Clamp(center.z, cellCenter.z - half, cellCenter.z + half);
        float dx = center.x - nearestX;
        float dz = center.z - nearestZ;
        return dx * dx + dz * dz < (radius - TouchEpsilon) * (radius - TouchEpsilon);
    }

    // Flat direction from the tile centre to the nearest Path cell (else the nearest walkable cell, else north).
    // Also used by the build ghost so it faces the same way the turret will.
    public static Vector3 FindRestDirection(Vector2Int cell)
    {
        BuildGrid grid = BuildGrid.Instance;
        if (grid == null)
        {
            return Vector3.forward;
        }

        Vector2Int best = cell;
        int bestSqr = int.MaxValue;
        bool bestIsPath = false;

        for (int dx = -RestSearchRadius; dx <= RestSearchRadius; dx++)
        {
            for (int dy = -RestSearchRadius; dy <= RestSearchRadius; dy++)
            {
                Vector2Int candidate = new Vector2Int(cell.x + dx, cell.y + dy);
                CellState state = grid.GetState(candidate);
                bool isPath = state == CellState.Path;
                if (!isPath && !grid.IsWalkable(candidate))
                {
                    continue;
                }

                int sqr = dx * dx + dy * dy;
                if ((isPath && !bestIsPath) || (isPath == bestIsPath && sqr < bestSqr))
                {
                    best = candidate;
                    bestSqr = sqr;
                    bestIsPath = isPath;
                }
            }
        }

        if (bestSqr == int.MaxValue)
        {
            return Vector3.forward;
        }

        Vector3 direction = grid.CellToWorld(best) - grid.CellToWorld(cell);
        direction.y = 0f;
        return direction.normalized;
    }

    private void Awake()
    {
        if (gun == null)
        {
            gun = GetComponentInChildren<TurretGun>();
        }
        baseStats = gun != null ? gun.CurrentStats : null;
    }

    private void OnDestroy()
    {
        if (upgradedStats != null)
        {
            Destroy(upgradedStats);
        }
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        if (Active.Remove(this))
        {
            ActiveChanged?.Invoke();
        }
    }
}
