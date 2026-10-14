/*
 * ResourceTree
 * A choppable tree: a Damageable in the Resource category with very high health, so guns and the area
 * weapon (low resourceDamageMultiplier) barely scratch it and the axe in slice 3 is the intended tool.
 *
 * - Health, damage events, health bar and damage numbers all come from Damageable; nothing here
 *   changes targeting. TreeSpawner pools trees and swaps a dead tree for a stump.
 * - The leaves child wobbles on every hit (DOTween punch rotation + scale) while the trunk stays
 *   still. The tween is killed and the leaves reset when the tree is disabled, so pooled trees come
 *   back clean.
 * - woodYield is how many wood drops (one wood each) WoodDropSpawner bursts out when the tree dies.
 *   Edit it on the Tree prefab (Resource Tree > Wood Yield).
 * - Named ResourceTree because UnityEngine already has a Tree class.
 */

using DG.Tweening;
using UnityEngine;

public class ResourceTree : Damageable
{
    [SerializeField] private Transform leaves;
    [SerializeField, Min(0)] private int woodYield = 5;

    [Header("Leaf wobble")]
    [SerializeField] private Vector3 wobbleRotation = new Vector3(5f, 0f, 5f);
    [SerializeField] private Vector3 wobbleScale = new Vector3(0.04f, 0.04f, 0.04f);
    [SerializeField, Min(0.05f)] private float wobbleDuration = 0.45f;
    [SerializeField, Min(1)] private int wobbleVibrato = 8;
    [SerializeField, Range(0f, 1f)] private float wobbleElasticity = 0.6f;

    private Vector3 leavesScale;
    private Quaternion leavesRotation;
    private bool leavesCached;

    public int WoodYield => woodYield;

    private void CacheLeaves()
    {
        if (leavesCached || leaves == null)
        {
            return;
        }

        leavesScale = leaves.localScale;
        leavesRotation = leaves.localRotation;
        leavesCached = true;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheLeaves();
        ResetLeaves();
        Damaged += HandleDamaged;
    }

    protected override void OnDisable()
    {
        Damaged -= HandleDamaged;
        if (leaves != null)
        {
            leaves.DOKill();
            ResetLeaves();
        }

        base.OnDisable();
    }

    private void HandleDamaged(Damageable target, float amount)
    {
        if (target != this || leaves == null)
        {
            return;
        }

        leaves.DOKill();
        ResetLeaves();
        leaves.DOPunchRotation(wobbleRotation, wobbleDuration, wobbleVibrato, wobbleElasticity);
        leaves.DOPunchScale(wobbleScale, wobbleDuration, wobbleVibrato, wobbleElasticity);
    }

    private void ResetLeaves()
    {
        if (leaves == null || !leavesCached)
        {
            return;
        }

        leaves.localScale = leavesScale;
        leaves.localRotation = leavesRotation;
    }
}
