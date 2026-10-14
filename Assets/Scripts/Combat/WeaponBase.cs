/*
 * WeaponBase / WeaponBase<TStats>
 * Foundation for every weapon type. Handles the owner, the stats asset and the attack cooldown;
 * subclasses decide what an attack looks like.
 *
 * - WeaponBase is the non-generic handle other systems use (e.g. WeaponRangeIndicator reads
 *   Owner and Range from it).
 * - WeaponBase<TStats> holds the stats asset and the cooldown. Implement Tick() each frame and call
 *   StartCooldown() after attacking; IsReady is true once attackInterval seconds have passed.
 * - Layers: WeaponBase<TStats> -> AimedWeapon<TStats> (ring + aiming) -> GunWeapon. Non-aimed types
 *   (AOE, spawn) derive straight from WeaponBase<TStats>.
 * - The owner defaults to the weapon's parent. Weapons are repositioned in world space, so they need
 *   no collider.
 */

using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [SerializeField] private Transform owner;

    public Transform Owner => owner;
    public abstract float Range { get; }

    protected virtual void Awake()
    {
        if (owner == null)
        {
            owner = transform.parent;
        }
    }
}

public abstract class WeaponBase<TStats> : WeaponBase where TStats : WeaponStats
{
    [SerializeField] private TStats stats;

    private float cooldown;

    protected TStats Stats => stats;
    protected bool IsReady => cooldown <= 0f;

    public override float Range => stats.Range;

    private void Update()
    {
        cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
        Tick();
    }

    protected abstract void Tick();

    protected void StartCooldown()
    {
        cooldown = Mathf.Max(stats.AttackInterval, 0.01f);
    }
}
