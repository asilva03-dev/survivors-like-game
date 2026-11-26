/*
 * TurretSwing
 * A tower's close-range sword swing (the SwordsCat, tower 5, 2026-11-25): while a rat is within reach, the tower faces it
 * and, every attackInterval, swings an arc that hits every rat inside it (no pierce cap). Numbers on SwordSwingStats.
 *
 * - Owner = the tower's aim point; distances are flat. Shadow rats only when the stats detect shadows (or a RevealLight).
 * - Modes (user 2026-11-25): melee mode while a rat is within reach and for ModeLinger seconds after the last one left;
 *   ranged mode otherwise. In melee mode the gun's HoldFire is set (no waves). While a rat is within reach it is also the
 *   gun's MeleeTarget, so the tower turns to it; during the linger the gun aims as usual but still holds fire. InMelee.
 * - Swung (direction) is raised after each swing, Swings counts them; for an animation or effect later. No visual yet.
 * - Path wedge (user 2026-11-25): with SwordSwingStats.pathReach set, the reach is longer (pathReach) inside a cone of
 *   pathConeAngle facing the tower's nearest path (Turret.RestDirection), both for entering melee mode and for who a
 *   swing hits (ReachToward / HitReachToward; the slash effect draws the same shape).
 */

using System.Collections.Generic;
using UnityEngine;

public class TurretSwing : WeaponBase<SwordSwingStats>
{
    [SerializeField] private TurretGun gun;

    private readonly List<Damageable> hits = new List<Damageable>();

    /// <summary>Raised after every swing with its flat direction.</summary>
    public event System.Action<Vector3> Swung;
    public int Swings { get; private set; }
    public Damageable CloseTarget { get; private set; }
    /// <summary>Melee mode: a rat is within reach, or one was less than ModeLinger seconds ago.</summary>
    public bool InMelee { get; private set; }

    private float lastCloseTime = float.NegativeInfinity;
    /// <summary>Seconds between swings (for the animation's timing).</summary>
    public float AttackInterval => Stats.AttackInterval;
    /// <summary>How far a swing reaches (reach + target radius, flat) and its arc, for the slash effect.</summary>
    public float HitReach => Stats.Reach + Stats.TargetRadius;
    public float ArcAngle => Stats.ArcAngle;

    private Turret turret;
    private Turret Turret => turret != null ? turret : (turret = GetComponentInParent<Turret>());

    /// <summary>The reach toward a flat direction: PathReach inside the cone facing the tower's nearest path (when the
    /// stats have a wedge), else Reach.</summary>
    public float ReachToward(Vector3 direction)
    {
        if (!Stats.HasPathWedge || Turret == null)
        {
            return Stats.Reach;
        }
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Stats.Reach;
        }
        return Vector3.Angle(direction, Turret.RestDirection) <= Stats.PathConeAngle * 0.5f ? Stats.PathReach : Stats.Reach;
    }

    /// <summary>ReachToward plus the target radius: how far a swing hits in that direction (for the slash effect).</summary>
    public float HitReachToward(Vector3 direction)
    {
        return ReachToward(direction) + Stats.TargetRadius;
    }

    private void OnDisable()
    {
        if (gun != null)
        {
            gun.MeleeTarget = null;
            gun.HoldFire = false;
        }
        CloseTarget = null;
        InMelee = false;
        lastCloseTime = float.NegativeInfinity;
    }

    protected override void Tick()
    {
        Vector3 from = Owner.position;
        Damageable nearest = null;
        float nearestScore = float.MaxValue;
        hits.Clear();

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!Stats.CanTarget(candidate.Category) || !ShadowSight.CanTarget(candidate, Stats.DetectsShadow))
            {
                continue;
            }

            Vector3 offset = Flat(candidate.transform.position - from);
            float distance = offset.magnitude - candidate.TargetRadius - Stats.TargetRadius;
            if (distance > ReachToward(offset))
            {
                continue;
            }

            hits.Add(candidate);
            if (distance < nearestScore)
            {
                nearestScore = distance;
                nearest = candidate;
            }
        }

        CloseTarget = nearest;
        if (nearest != null)
        {
            lastCloseTime = Time.time;
        }
        InMelee = nearest != null || Time.time - lastCloseTime < Stats.ModeLinger;
        if (gun != null)
        {
            gun.MeleeTarget = nearest;
            gun.HoldFire = InMelee;
        }

        if (nearest == null || !IsReady)
        {
            return;
        }

        Vector3 direction = Flat(nearest.transform.position - from);
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Owner.forward;
        float halfArc = Stats.ArcAngle * 0.5f;
        for (int i = 0; i < hits.Count; i++)
        {
            Damageable target = hits[i];
            Vector3 offset = Flat(target.transform.position - from);
            bool inArc = offset.sqrMagnitude < 0.04f || Vector3.Angle(direction, offset) <= halfArc;
            if (inArc && target != null && target.isActiveAndEnabled)
            {
                target.TakeHit(Stats.DamageFor(target.Category), Stats.HitFlags);
            }
        }

        StartCooldown();
        Swings++;
        Swung?.Invoke(direction);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
