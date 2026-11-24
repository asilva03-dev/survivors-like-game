/*
 * TurretDefinition
 * One turret type (Create > TD > Turret Definition): which prefab to build, its cost, how much room it needs and its
 * upgrade paths. The gun's base numbers (range, damage, fire rate) live on the GunStats asset of the prefab's
 * TurretGun.
 *
 * - displayName and woodCost (15 for the gun turret) are shown on its build-bar slot; woodCost is spent when placed.
 * - sellRefundShare (0.5): selling refunds this share of the wood spent on the turret, rounded down.
 * - spacingRadius: the turret's no-build circle (around its corner tile); the strip tiles it touches turn red and can't
 *   be built on (tile rule, slice 6).
 * - footprint (2026-11-23, Trapper's L, user): the extra build-strip tiles the turret stands on besides its corner tile,
 *   as offsets at rotation 0 (x east, y north = up on screen). Empty = one tile (the gun turret). A turret with a
 *   footprint can be turned in quarter turns in build mode (scroll); Cells(anchor, rotation, list) lists every tile.
 * - Idle scan: with no enemy in range the gun sweeps idleScanAngle degrees to each side of its rest direction
 *   (toward the nearest path cell), one full left-right-left cycle every idleScanPeriod seconds.
 * - Upgrades (Bloons-style, slice 6 step 5): `paths` (3), each a name and its tiers (TurretUpgrade assets, bought in
 *   order). One path may go up to mainPathMaxTier, one other path up to secondPathMaxTier, the third stays 0 (gun
 *   turret since 2026-11-22: 5 tiers per path, the 5/2 rule).
 *   Turret applies the rule and the stat changes.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TD/Turret Definition", fileName = "TurretDefinition")]
public class TurretDefinition : ScriptableObject
{
    [Serializable]
    public class UpgradePath
    {
        public string name = "Path";
        public List<TurretUpgrade> tiers = new List<TurretUpgrade>();
    }

    [SerializeField] private string displayName = "Gun Turret";
    [SerializeField] private Turret prefab;
    [SerializeField, Min(0)] private int woodCost = 15;
    [SerializeField, Min(0f)] private float spacingRadius = 1f;
    [Tooltip("Extra tiles besides the corner tile, at rotation 0 (x east, y north). Empty = one tile.")]
    [SerializeField] private List<Vector2Int> footprint = new List<Vector2Int>();
    [Tooltip("Share of everything spent on a turret (build + upgrades) that selling refunds.")]
    [SerializeField, Range(0f, 1f)] private float sellRefundShare = 0.5f;

    [Header("Idle scan (no enemy in range)")]
    [SerializeField, Range(0f, 180f)] private float idleScanAngle = 40f;
    [SerializeField, Min(0.1f)] private float idleScanPeriod = 4f;

    [Header("Upgrades")]
    [SerializeField] private List<UpgradePath> paths = new List<UpgradePath>();
    [SerializeField, Min(0)] private int mainPathMaxTier = 3;
    [SerializeField, Min(0)] private int secondPathMaxTier = 1;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Turret Prefab => prefab;
    public int WoodCost => woodCost;
    public float SpacingRadius => spacingRadius;
    public bool Rotatable => footprint.Count > 0;

    // Every tile of the turret: the corner tile, then the footprint offsets turned `rotation` quarter turns clockwise
    // (seen from above; the same turn as the root's Euler(0, rotation * 90, 0)).
    public void Cells(Vector2Int anchor, int rotation, List<Vector2Int> cells)
    {
        cells.Clear();
        cells.Add(anchor);
        for (int i = 0; i < footprint.Count; i++)
        {
            cells.Add(anchor + Turn(footprint[i], rotation));
        }
    }

    public static Vector2Int Turn(Vector2Int offset, int rotation)
    {
        for (int i = 0; i < ((rotation % 4) + 4) % 4; i++)
        {
            offset = new Vector2Int(offset.y, -offset.x);
        }
        return offset;
    }
    public float SellRefundShare => sellRefundShare;
    public float IdleScanAngle => idleScanAngle;
    public float IdleScanPeriod => idleScanPeriod;
    public IReadOnlyList<UpgradePath> Paths => paths;
    public int MainPathMaxTier => mainPathMaxTier;
    public int SecondPathMaxTier => secondPathMaxTier;
}
