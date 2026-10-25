/*
 * TurretDefinition
 * One turret type (Create > TD > Turret Definition): which prefab to build, its cost, how much room it needs and its
 * upgrade paths. The gun's base numbers (range, damage, fire rate) live on the GunStats asset of the prefab's
 * TurretGun.
 *
 * - displayName and woodCost (15 for the gun turret) are shown on its build-bar slot; woodCost is spent when placed.
 * - sellRefundShare (0.5): selling refunds this share of the wood spent on the turret, rounded down.
 * - spacingRadius: the turret's no-build circle; the strip tiles it touches turn red and can't be built on (tile
 *   rule, slice 6). Each turret takes up one build-strip tile.
 * - Idle scan: with no enemy in range the gun sweeps idleScanAngle degrees to each side of its rest direction
 *   (toward the nearest path cell), one full left-right-left cycle every idleScanPeriod seconds.
 * - Upgrades (Bloons-style, slice 6 step 5): `paths` (3), each a name and its tiers (TurretUpgrade assets, bought in
 *   order). One path may go up to mainPathMaxTier (3), one other path up to secondPathMaxTier (1), the third stays 0.
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
    public float SellRefundShare => sellRefundShare;
    public float IdleScanAngle => idleScanAngle;
    public float IdleScanPeriod => idleScanPeriod;
    public IReadOnlyList<UpgradePath> Paths => paths;
    public int MainPathMaxTier => mainPathMaxTier;
    public int SecondPathMaxTier => secondPathMaxTier;
}
