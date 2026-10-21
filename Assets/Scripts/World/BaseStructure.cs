/*
 * BaseStructure
 * The central base the player defends (temporary version): a Damageable in the Structure category that enemies
 * target and damage. Prefab Prefabs/Base.prefab, spawned at the BasePoint by BaseSpawner.
 *
 * - Health / damage numbers / health bar come from Damageable (set Max Health and the category on the prefab).
 * - Only enemies hurt it: enemy contact damage and ranged-enemy bullets (their target mask includes Structure).
 *   No player weapon has Structure in its targets, so they deal 0 and player bullets pass through it.
 * - targetRadius is the flat radius of the box: enemies stop at its edge, touch it and shoot at it from there.
 * - Active lists every enabled base, so enemies can pick the closest of the player and the bases.
 * - It cannot be destroyed yet (health can reach 0 and nothing happens); losing the base ends the run later.
 */

using System.Collections.Generic;
using UnityEngine;

public class BaseStructure : Damageable
{
    public static readonly List<BaseStructure> Active = new List<BaseStructure>();

    [SerializeField, Min(0f)] private float targetRadius = 2f;

    public override float TargetRadius => targetRadius;
    protected override bool CanDie => false;

    protected override void OnEnable()
    {
        base.OnEnable();
        Active.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Active.Remove(this);
    }
}
