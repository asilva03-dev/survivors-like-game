/*
 * RatTrap
 * One thrown snap trap of the Trapper (2026-11-23). Pooled (TrapThrower's ComponentPool), never instantiated per throw.
 *
 * - Launch(pool, from, to, stats, owner): flies from the cat's paw to `to` (a path spot) on a parabola (stats flightTime,
 *   arcHeight), tumbling a little, lands flat with a small bounce and is set.
 * - While set (Active list): the first enemy whose centre comes within triggerRadius (flat) and whose feet are near the
 *   ground (not leaping over it) sets it off: that one rat takes `damage` once (Damageable.TakeHit, no hit flags: a pot
 *   shrugs it off but the trap is still spent), and the trap snaps.
 * - Unused for trapLifetime seconds: it snaps shut on nothing.
 * - Snap: the kill bar (bar, hinged at the spring) swings over at snapSpeed, the trap hops, then shrinks away and goes
 *   back to the pool. Owner (the TrapThrower) counts its set traps through Owner/IsSet.
 */

using System.Collections.Generic;
using UnityEngine;

public class RatTrap : MonoBehaviour
{
    private enum State { Flying, Set, Snapping }

    public static readonly List<RatTrap> Active = new List<RatTrap>();

    [Tooltip("The kill bar (hinged at the spring); swings over when the trap snaps.")]
    [SerializeField] private Transform bar;
    [Tooltip("Degrees the bar swings when it snaps.")]
    [SerializeField] private float snapAngle = 180f;
    [SerializeField, Min(0.01f)] private float snapTime = 0.07f;
    [Tooltip("Seconds the snapped trap stays before shrinking away.")]
    [SerializeField, Min(0f)] private float snappedHold = 0.35f;
    [SerializeField, Min(0.01f)] private float shrinkTime = 0.15f;
    [SerializeField, Min(0f)] private float snapHop = 0.12f;

    private ComponentPool<RatTrap> pool;
    private TrapperStats stats;
    private State state;
    private Vector3 from;
    private Vector3 to;
    private float timer;
    private Quaternion barRest;
    private bool barCached;
    private Vector3 spin;

    public TrapThrower Owner { get; private set; }
    public bool IsSet => state == State.Set;
    public bool IsFlying => state == State.Flying;
    /// <summary>Where it lands / lies.</summary>
    public Vector3 Spot => to;

    /// <summary>Traps that went off on a rat (for tests).</summary>
    public static int Hits { get; private set; }
    /// <summary>Traps that ran out and snapped on nothing (for tests).</summary>
    public static int Expired { get; private set; }

    public void Launch(ComponentPool<RatTrap> owningPool, Vector3 start, Vector3 target, TrapperStats trapStats, TrapThrower owner)
    {
        pool = owningPool;
        stats = trapStats;
        Owner = owner;
        from = start;
        to = target;
        timer = 0f;
        state = State.Flying;
        if (!barCached && bar != null)
        {
            barRest = bar.localRotation;
            barCached = true;
        }
        if (bar != null)
        {
            bar.localRotation = barRest;
        }
        transform.localScale = Vector3.one;
        Vector3 flat = target - start;
        flat.y = 0f;
        transform.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 0.0001f ? flat : Vector3.forward);
        spin = new Vector3(Random.Range(-360f, 360f), Random.Range(-180f, 180f), 0f);
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        timer += dt;
        switch (state)
        {
            case State.Flying:
                Fly();
                break;
            case State.Set:
                if (timer >= stats.TrapLifetime)
                {
                    Expired++;
                    Snap();
                }
                else
                {
                    CheckTrigger();
                }
                break;
            case State.Snapping:
                SnapAnimation();
                break;
        }
    }

    private void Fly()
    {
        float t = Mathf.Clamp01(timer / stats.FlightTime);
        Vector3 p = Vector3.Lerp(from, to, t);
        p.y += stats.ArcHeight * 4f * t * (1f - t);
        transform.position = p;
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        if (t >= 1f)
        {
            // Land flat, facing along the throw, set.
            Vector3 flat = to - from;
            flat.y = 0f;
            transform.SetPositionAndRotation(to, Quaternion.LookRotation(flat.sqrMagnitude > 0.0001f ? flat : Vector3.forward));
            state = State.Set;
            timer = 0f;
        }
    }

    private void CheckTrigger()
    {
        float sqr = stats.TriggerRadius * stats.TriggerRadius;
        List<Enemy> enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            Vector3 offset = enemy.transform.position - to;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqr || Mathf.Abs(enemy.FeetY - to.y) > 0.5f || enemy.Health <= 0f)
            {
                continue;
            }
            enemy.TakeHit(stats.Damage, HitFlags.None);
            Hits++;
            Snap();
            return;
        }
    }

    private void Snap()
    {
        state = State.Snapping;
        timer = 0f;
        Active.Remove(this);
    }

    private void SnapAnimation()
    {
        if (bar != null)
        {
            float swing = Mathf.Clamp01(timer / snapTime);
            bar.localRotation = barRest * Quaternion.Euler(-snapAngle * swing, 0f, 0f);
        }
        float hop = timer < 0.2f ? Mathf.Sin(timer / 0.2f * Mathf.PI) * snapHop : 0f;
        transform.position = to + Vector3.up * hop;
        float shrinkStart = snapTime + snappedHold;
        if (timer > shrinkStart)
        {
            float k = 1f - Mathf.Clamp01((timer - shrinkStart) / shrinkTime);
            transform.localScale = Vector3.one * k;
            if (k <= 0f)
            {
                pool.Release(this);
            }
        }
    }
}
