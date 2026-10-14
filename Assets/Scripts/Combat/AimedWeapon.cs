/*
 * AimedWeapon<TStats>
 * Weapon layer for types that ride an invisible ring around the owner and always face the nearest
 * enemy in range (guns, and melee later). Subclasses only implement Fire().
 *
 * - Two directions are tracked: the ring slot (where on the ring the weapon sits) and the facing
 *   (where it points). The facing turns at turnSpeed; the slot turns at turnSpeed normally and at
 *   the slower displacedTurnSpeed while sliding to or from a displaced spot.
 * - Normally the slot is on the line from the owner to the target. If an enemy is within
 *   slotClearance of that spot, the weapon slides to the nearest unoccupied spot on the ring and
 *   still turns to face the target from there. When the blocker leaves, it slides back.
 * - The displaced spot is always the free ring spot closest to the target direction and never more
 *   than 90 degrees from it, so the weapon never ends up behind the owner shooting through them.
 * - Anti-jitter: the displaced spot only changes when an enemy blocks it or a spot at least 15 degrees
 *   closer to the target opens up, and the normal spot must be clear by an extra 0.25 units before
 *   the weapon returns to it.
 * - If a WeaponLoadout is on a parent (the player), targets and the wanted ring spot come from it so
 *   several weapons split enemies and keep their distance; otherwise the weapon works alone.
 * - With no enemy in range the weapon stays on the ring and faces outward without attacking.
 * - Fire(target) is called only when the weapon faces the target within fireAngleTolerance and the
 *   cooldown is ready; the first attack happens immediately once aimed.
 * - Ring radius, turn speed, clearance and tolerance come from the AimedWeaponStats asset.
 */

using UnityEngine;

public abstract class AimedWeapon<TStats> : WeaponBase<TStats>, IAimedWeapon where TStats : AimedWeaponStats
{
    private WeaponLoadout loadout;

    private const float SlotSearchStep = 15f;
    private const int SlotSearchSteps = 6;
    private const float ReleaseMargin = 0.25f;
    private const float SwitchHysteresis = 15f;

    private Vector3 slotDirection = Vector3.forward;
    private Vector3 slotGoal = Vector3.forward;
    private Vector3 facing = Vector3.forward;
    private bool displaced;
    private bool transitioning;

    public Vector3 SlotDirection => slotDirection;

    // The target the shared targeting currently gives this weapon (same source Tick uses).
    protected Damageable CurrentTarget => loadout != null
        ? loadout.GetTarget(this)
        : Damageable.FindNearest(Owner.position, Stats.Range, Stats.Targets);
    public float RingRadius => Stats.RingRadius;
    public float SeparationDistance => Stats.SeparationDistance;

    public bool FocusWeakestResource => Stats.FocusWeakestResource;

    public bool CanTarget(TargetCategory category)
    {
        return Stats.CanTarget(category);
    }

    protected virtual void OnEnable()
    {
        loadout = GetComponentInParent<WeaponLoadout>();
        if (loadout != null)
        {
            loadout.Register(this);
        }
    }

    protected virtual void OnDisable()
    {
        if (loadout != null)
        {
            loadout.Unregister(this);
        }
    }

    protected override void Tick()
    {
        Damageable target;
        Vector3 wantedSlot = Vector3.zero;
        bool hasWantedSlot;

        if (loadout != null)
        {
            target = loadout.GetTarget(this);
            hasWantedSlot = target != null && loadout.TryGetSlotDirection(this, out wantedSlot);
        }
        else
        {
            target = Damageable.FindNearest(Owner.position, Stats.Range, Stats.Targets);
            hasWantedSlot = false;

            if (target != null)
            {
                Vector3 fromOwner = Flat(target.transform.position - Owner.position);
                if (fromOwner.sqrMagnitude > 0.0001f)
                {
                    wantedSlot = fromOwner.normalized;
                    hasWantedSlot = true;
                }
            }
        }

        float maxStep = Stats.TurnSpeed * Mathf.Deg2Rad * Time.deltaTime;
        Vector3 desiredFacing = slotDirection;
        bool hasTarget = false;

        if (hasWantedSlot)
        {
            UpdateSlotGoal(wantedSlot);
        }
        else if (target == null)
        {
            displaced = false;
            slotGoal = slotDirection;
        }

        float slotSpeed = transitioning ? Stats.DisplacedTurnSpeed : Stats.TurnSpeed;
        slotDirection = Vector3.RotateTowards(slotDirection, slotGoal, slotSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        if (transitioning && Vector3.Angle(slotDirection, slotGoal) < 2f)
        {
            transitioning = false;
        }

        transform.position = Owner.position + slotDirection * Stats.RingRadius;

        if (target != null)
        {
            Vector3 fromWeapon = Flat(target.transform.position - transform.position);
            if (fromWeapon.sqrMagnitude > 0.0001f)
            {
                desiredFacing = fromWeapon.normalized;
                hasTarget = true;
            }
        }

        facing = Vector3.RotateTowards(facing, desiredFacing, maxStep, 0f);
        transform.rotation = Quaternion.LookRotation(facing);

        bool aimed = hasTarget && Vector3.Angle(facing, desiredFacing) <= Stats.FireAngleTolerance;
        if (aimed && IsReady)
        {
            Fire(target);
            StartCooldown();
        }
    }

    protected abstract void Fire(Damageable target);

    private void UpdateSlotGoal(Vector3 desired)
    {
        float releaseExtra = displaced ? ReleaseMargin : 0f;
        bool desiredFree = !IsSlotOccupied(desired, Stats.SlotClearance + releaseExtra);

        if (desiredFree)
        {
            if (displaced)
            {
                displaced = false;
                transitioning = true;
            }

            slotGoal = desired;
            return;
        }

        Vector3 best = FindFreeSlot(desired);
        bool switchGoal = !displaced
            || IsSlotOccupied(slotGoal, Stats.SlotClearance)
            || Vector3.Angle(best, desired) + SwitchHysteresis < Vector3.Angle(slotGoal, desired);

        displaced = true;
        if (switchGoal)
        {
            slotGoal = best;
            transitioning = true;
        }
    }

    private Vector3 FindFreeSlot(Vector3 desired)
    {
        float side = Mathf.Sign(Vector3.SignedAngle(desired, slotDirection, Vector3.up));
        if (side == 0f)
        {
            side = 1f;
        }

        for (int step = 1; step <= SlotSearchSteps; step++)
        {
            float angle = step * SlotSearchStep * side;

            Vector3 preferred = Quaternion.AngleAxis(angle, Vector3.up) * desired;
            if (!IsSlotOccupied(preferred, Stats.SlotClearance))
            {
                return preferred;
            }

            Vector3 opposite = Quaternion.AngleAxis(-angle, Vector3.up) * desired;
            if (!IsSlotOccupied(opposite, Stats.SlotClearance))
            {
                return opposite;
            }
        }

        return desired;
    }

    private bool IsSlotOccupied(Vector3 direction, float clearance)
    {
        Vector3 slot = Owner.position + direction * Stats.RingRadius;
        float sqrClearance = clearance * clearance;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Vector3 offset = Flat(Damageable.All[i].transform.position - slot);
            if (offset.sqrMagnitude < sqrClearance)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
