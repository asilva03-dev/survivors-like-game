/*
 * WheelOWeapon
 * Wheel weapon: objects orbit the owner on a ring and damage every target they touch. No targeting.
 *
 * - All numbers come from a WheelOStats asset (count, radius, rotate speed, duration, cooldown, per-enemy
 *   cooldown, hit radius, model). Not an AimedWeapon: it never registers with the WeaponLoadout, so it takes
 *   no part in targeting, claiming or ring spacing, and has no range ring.
 * - The wheel angle turns continuously. Object i sits at wheelAngle + SlotAngle(i): the first four at
 *   N/E/S/W (0/90/180/270 degrees, clockwise from +Z), then the spots in between, doubling each level.
 * - Every object follows the same cycle: out for `duration`, hidden for `cooldown`, then back at its spot
 *   on the wheel. The cycle starts with the objects out.
 * - Each object is turned to face its direction of travel (a model's +Z axis leads, e.g. a knife tip).
 * - Each object keeps its own last-hit time per target and hits the same target at most once per
 *   `perEnemyCooldown`. A hit deals stats.DamageFor(category) to targets inside `hitRadius` (flat distance).
 * - Objects are created once in Awake (and only grown if projectileCount is raised live) under a separate
 *   scene object, so they do not move with the player; nothing is destroyed at runtime.
 */

using System.Collections.Generic;
using UnityEngine;

public class WheelOWeapon : WeaponBase<WheelOStats>
{
    private const int PruneThreshold = 48;

    private class Orb
    {
        public Transform transform;
        public readonly Dictionary<Damageable, float> lastHit = new Dictionary<Damageable, float>();
    }

    private readonly List<Orb> orbs = new List<Orb>();
    private readonly List<Damageable> stale = new List<Damageable>();
    private Transform root;
    private float wheelAngle;
    private float phaseTimer;
    private bool isOut = true;

    protected override void Awake()
    {
        base.Awake();
        root = new GameObject(name + " Orbs").transform;
        EnsureOrbs(Stats.ProjectileCount);
        phaseTimer = Stats.Duration;
    }

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root.gameObject);
        }
    }

    protected override void Tick()
    {
        EnsureOrbs(Stats.ProjectileCount);

        wheelAngle = Mathf.Repeat(wheelAngle + Stats.RotateSpeed * Time.deltaTime, 360f);
        AdvanceCycle();

        int count = Stats.ProjectileCount;
        for (int i = 0; i < orbs.Count; i++)
        {
            Orb orb = orbs[i];
            bool visible = isOut && i < count;

            if (orb.transform.gameObject.activeSelf != visible)
            {
                orb.transform.gameObject.SetActive(visible);
                if (visible)
                {
                    orb.lastHit.Clear();
                }
            }

            if (!visible)
            {
                continue;
            }

            float radians = (wheelAngle + SlotAngle(i)) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            orb.transform.position = Owner.position + direction * Stats.OrbitRadius + Vector3.up * Stats.OrbitHeight;
            orb.transform.rotation = Quaternion.LookRotation((Stats.RotateSpeed < 0f ? -1f : 1f) * new Vector3(direction.z, 0f, -direction.x));
            HitTouching(orb);
        }
    }

    private void AdvanceCycle()
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f)
        {
            return;
        }

        isOut = !isOut;
        phaseTimer = isOut ? Stats.Duration : Mathf.Max(Stats.Cooldown, 0.01f);
    }

    private void HitTouching(Orb orb)
    {
        Vector3 center = orb.transform.position;
        float sqrRadius = Stats.HitRadius * Stats.HitRadius;
        float now = Time.time;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!Stats.CanTarget(candidate.Category))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrRadius)
            {
                continue;
            }

            float last;
            if (orb.lastHit.TryGetValue(candidate, out last) && now - last < Stats.PerEnemyCooldown)
            {
                continue;
            }

            float amount = Stats.DamageFor(candidate.Category);
            if (amount > 0f)
            {
                orb.lastHit[candidate] = now;
                candidate.TakeDamage(amount);
            }
        }

        if (orb.lastHit.Count > PruneThreshold)
        {
            Prune(orb, now);
        }
    }

    private void Prune(Orb orb, float now)
    {
        stale.Clear();
        foreach (KeyValuePair<Damageable, float> entry in orb.lastHit)
        {
            if (entry.Key == null || now - entry.Value >= Stats.PerEnemyCooldown)
            {
                stale.Add(entry.Key);
            }
        }

        for (int i = 0; i < stale.Count; i++)
        {
            orb.lastHit.Remove(stale[i]);
        }
    }

    // 0-3: N, E, S, W. Then each level fills the gaps of the previous one: 4 spots (45 deg offsets), then 8
    // (22.5 deg offsets), and so on.
    public static float SlotAngle(int index)
    {
        if (index < 4)
        {
            return index * 90f;
        }

        int j = index - 4;
        int levelCount = 4;
        while (j >= levelCount)
        {
            j -= levelCount;
            levelCount *= 2;
        }

        return (2 * j + 1) * (360f / (2f * levelCount));
    }

    private void EnsureOrbs(int count)
    {
        while (orbs.Count < count)
        {
            orbs.Add(CreateOrb());
        }
    }

    private Orb CreateOrb()
    {
        GameObject holder = new GameObject("Orb");
        holder.transform.SetParent(root, false);

        GameObject model;
        GameObject prefab = Stats.ObjectModel;
        if (prefab != null)
        {
            model = Instantiate(prefab, holder.transform);
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(model.GetComponent<Collider>());
            model.transform.SetParent(holder.transform, false);
        }

        ModelFit.Fit(holder.transform, model.transform, Stats.ObjectSize);
        holder.SetActive(false);
        return new Orb { transform = holder.transform };
    }
}
