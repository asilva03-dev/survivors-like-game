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
 * - Rustle(push) (2026-11-10; IWadeable, shared with DecorBush): a smaller shake tipping the leaves away from the player wading through (PlayerWading);
 *   skipped while a shake is still playing. The root collider is a trigger since then, so the player walks through.
 * - woodYield is how many wood drops (one wood each) a tree gives in total. Edit it on the Tree prefab (Resource Tree >
 *   Wood Yield). Since 2026-11-20 (user) part of it drops on every hit: TakeHitWood(damage) picks the hitWood row with
 *   the highest minDamage the hit reaches (50: 2-3, 100: 4-5, 150: all 15), capped by what is left; when the tree
 *   breaks, TakeRemainingWood gives the rest (WoodDropSpawner launches both). Reset when the tree is enabled (pooling).
 * - showDamageNumbers (off since 2026-11-20, user): no floating numbers on trees (Damageable.ShowsDamageNumbers).
 * - Named ResourceTree because UnityEngine already has a Tree class.
 */

using DG.Tweening;
using UnityEngine;

public class ResourceTree : Damageable, IWadeable
{
    [System.Serializable]
    public struct HitWood
    {
        [Tooltip("A hit dealing at least this much damage drops wood from this row (the highest matching row wins).")]
        public float minDamage;
        public int minDrops;
        public int maxDrops;
    }

    [SerializeField] private Transform leaves;
    [SerializeField, Min(0)] private int woodYield = 5;
    [Tooltip("Wood dropped on each hit by its damage (never more than what is left of woodYield); the rest drops when the tree breaks.")]
    [SerializeField] private HitWood[] hitWood =
    {
        new HitWood { minDamage = 50f, minDrops = 2, maxDrops = 3 },
        new HitWood { minDamage = 100f, minDrops = 4, maxDrops = 5 },
        new HitWood { minDamage = 150f, minDrops = 15, maxDrops = 15 }
    };
    [Tooltip("Show floating damage numbers when the tree is hit.")]
    [SerializeField] private bool showDamageNumbers;

    [Header("Leaf wobble")]
    [SerializeField] private Vector3 wobbleRotation = new Vector3(5f, 0f, 5f);
    [SerializeField] private Vector3 wobbleScale = new Vector3(0.04f, 0.04f, 0.04f);
    [SerializeField, Min(0.05f)] private float wobbleDuration = 0.45f;
    [SerializeField, Min(1)] private int wobbleVibrato = 8;
    [SerializeField, Range(0f, 1f)] private float wobbleElasticity = 0.6f;

    [Header("Rustle (player wading through)")]
    [Tooltip("Degrees the leaves tip away from the player wading through.")]
    [SerializeField, Min(0f)] private float rustleAngle = 6f;
    [SerializeField, Min(0.05f)] private float rustleDuration = 0.5f;
    [SerializeField, Min(1)] private int rustleVibrato = 6;
    [SerializeField, Range(0f, 1f)] private float rustleElasticity = 0.5f;

    private Vector3 leavesScale;
    private Quaternion leavesRotation;
    private bool leavesCached;

    private int woodDropped;

    public int WoodYield => woodYield;
    /// <summary>Wood not dropped yet (woodYield minus what earlier hits dropped).</summary>
    public int WoodLeft => Mathf.Max(0, woodYield - woodDropped);
    public override bool ShowsDamageNumbers => showDamageNumbers;

    /// <summary>How much wood a hit of `damage` drops now (from hitWood, capped by what is left); counts it as dropped.</summary>
    public int TakeHitWood(float damage)
    {
        int best = -1;
        for (int i = 0; i < hitWood.Length; i++)
        {
            if (damage >= hitWood[i].minDamage && (best < 0 || hitWood[i].minDamage > hitWood[best].minDamage))
            {
                best = i;
            }
        }
        if (best < 0)
        {
            return 0;
        }
        int count = Random.Range(hitWood[best].minDrops, Mathf.Max(hitWood[best].minDrops, hitWood[best].maxDrops) + 1);
        count = Mathf.Clamp(count, 0, WoodLeft);
        woodDropped += count;
        return count;
    }

    /// <summary>The rest of the wood when the tree breaks; counts it as dropped.</summary>
    public int TakeRemainingWood()
    {
        int count = WoodLeft;
        woodDropped += count;
        return count;
    }

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
        woodDropped = 0;
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

    /// <summary>A small leaf shake tipping away along `push` (flat world direction); skipped while another shake plays.</summary>
    public void Rustle(Vector3 push)
    {
        if (leaves == null || DOTween.IsTweening(leaves))
        {
            return;
        }
        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Vector3 axis = Vector3.Cross(Vector3.up, push.normalized);
        Transform space = leaves.parent != null ? leaves.parent : leaves;
        Vector3 punch = space.InverseTransformDirection(axis).normalized * rustleAngle;
        leaves.DOPunchRotation(punch, rustleDuration, rustleVibrato, rustleElasticity);
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
