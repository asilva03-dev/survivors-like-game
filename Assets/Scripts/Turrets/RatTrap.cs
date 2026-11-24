/*
 * RatTrap
 * One thrown snap trap of the Trapper (2026-11-23). Pooled (TrapThrower's ComponentPool), never instantiated per throw.
 *
 * - Launch(pool, from, to, stats, owner): flies from the cat's paw to `to` (a path spot) on a parabola (stats flightTime,
 *   arcHeight), tumbling a little, lands flat with a small bounce and is set. It lands at a random yaw (randomLandingYaw,
 *   on by default; user, 2026-11-23: several traps on one spot should look like a pile), else facing along the throw.
 * - Piles (2026-11-23, Trapper change 4: traps may share a spot): it lands up to pileJitter off its spot; a trap lying
 *   within pileRadius of traps that landed before it rests pileLift higher per such trap, tilted up to pileTilt, and
 *   drops down (pileFallSpeed) when one under it snaps. Each trap of a pile triggers on its own.
 * - While set (Active list): the first enemy whose centre comes within triggerRadius (flat) and whose feet are near the
 *   ground (not leaping over it) sets it off: that one rat takes `damage` once (Damageable.TakeHit, no hit flags: a pot
 *   shrugs it off but the trap is still spent), and the trap snaps.
 * - Unused for trapLifetime seconds: it snaps shut on nothing.
 * - Snap: the kill bar (bar, hinged at the spring) swings over at snapSpeed, the trap hops, then shrinks away and goes
 *   back to the pool. Owner (the TrapThrower) counts its set traps through Owner/IsSet.
 * - Fling (2026-11-23, user: "abruptly flung up", no falling): a trap set off by a rat snaps shut, then shoots up
 *   at flingUpSpeed along a line leaning a random flingTiltMin..flingTiltMax degrees from straight up toward a random
 *   compass direction (user: a different direction each time), tumbling at flingSpin deg/s, flingGravity, and vanishes mid-air:
 *   it shrinks over shrinkTime from flingVanishAt seconds into the flight. Expired traps keep the hop + shrink unless
 *   flingOnExpire is ticked.
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
    [Header("Fling when set off")]
    [SerializeField, Min(0f)] private float flingUpSpeed = 9f;
    [Tooltip("Degrees the fling leans from straight up, picked between min and max each time, toward a random direction.")]
    [SerializeField, Range(0f, 89f)] private float flingTiltMin = 15f;
    [SerializeField, Range(0f, 89f)] private float flingTiltMax = 40f;
    [SerializeField, Min(0f)] private float flingSpin = 720f;
    [SerializeField, Min(0f)] private float flingGravity = 0f;
    [Tooltip("Seconds into the flight when it starts to vanish (shrinkTime).")]
    [SerializeField, Min(0f)] private float flingVanishAt = 0.08f;
    [SerializeField] private bool flingOnExpire = false;
    [Tooltip("Land at a random yaw (so traps on one spot look piled); off = facing along the throw.")]
    [SerializeField] private bool randomLandingYaw = true;
    [Tooltip("How far (flat) a trap may land from its spot, so a pile doesn't stack perfectly.")]
    [SerializeField, Min(0f)] private float pileJitter = 0.12f;
    [Tooltip("Traps closer than this rest on each other.")]
    [SerializeField, Min(0f)] private float pileRadius = 0.35f;
    [Tooltip("Height of one trap in a pile.")]
    [SerializeField, Min(0f)] private float pileLift = 0.09f;
    [Tooltip("Most degrees a piled trap leans.")]
    [SerializeField, Range(0f, 30f)] private float pileTilt = 8f;
    [SerializeField, Min(0.1f)] private float pileFallSpeed = 2f;

    private ComponentPool<RatTrap> pool;
    private TrapperStats stats;
    private State state;
    private Vector3 from;
    private Vector3 to;
    private float timer;
    private Quaternion barRest;
    private bool barCached;
    private Vector3 spin;
    private Quaternion landing;
    private Vector3 spot;
    private Vector3 lean;
    private float pileY;
    private int landOrder;
    private Vector3 snapBase;
    private bool flung;
    private Vector3 flingVelocity;
    private Vector3 flingAxis;
    private static int landCounter;

    public TrapThrower Owner { get; private set; }
    public bool IsSet => state == State.Set;
    public bool IsFlying => state == State.Flying;
    /// <summary>Where it lands / lies.</summary>
    public Vector3 Spot => spot;

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
        spot = target;
        Vector2 jitter = Random.insideUnitCircle * pileJitter;
        to = target + new Vector3(jitter.x, 0f, jitter.y);
        pileY = 0f;
        landOrder = int.MaxValue;
        lean = new Vector3(Random.Range(-pileTilt, pileTilt), 0f, Random.Range(-pileTilt, pileTilt));
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
        landing = randomLandingYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : transform.rotation;
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
                    Snap(flingOnExpire);
                }
                else
                {
                    Rest();
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
        p.y += stats.ArcHeight * 4f * t * (1f - t) + PileHeight() * t;
        transform.position = p;
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        if (t >= 1f)
        {
            // Land at the landing yaw (on top of any pile there), set.
            pileY = PileHeight();
            landOrder = ++landCounter;
            state = State.Set;
            timer = 0f;
            Rest();
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
            Snap(true);
            return;
        }
    }

    // Height of the traps under it: every set trap within pileRadius that landed before it adds pileLift.
    private float PileHeight()
    {
        int under = 0;
        float sqr = pileRadius * pileRadius;
        for (int i = 0; i < Active.Count; i++)
        {
            RatTrap other = Active[i];
            if (other == this || !other.IsSet || other.landOrder >= landOrder)
            {
                continue;
            }
            Vector3 offset = other.to - to;
            offset.y = 0f;
            if (offset.sqrMagnitude < sqr)
            {
                under++;
            }
        }
        return under * pileLift;
    }

    // Lie on the pile: drop when a trap under it goes, lean while it rests on another.
    private void Rest()
    {
        pileY = Mathf.MoveTowards(pileY, PileHeight(), pileFallSpeed * Time.deltaTime);
        float leanWeight = pileLift > 0f ? Mathf.Clamp01(pileY / pileLift) : 0f;
        transform.SetPositionAndRotation(to + Vector3.up * pileY, landing * Quaternion.Euler(lean * leanWeight));
    }

    private void Snap(bool fling)
    {
        snapBase = transform.position;
        flung = fling;
        float tilt = Random.Range(Mathf.Min(flingTiltMin, flingTiltMax), Mathf.Max(flingTiltMin, flingTiltMax)) * Mathf.Deg2Rad;
        float heading = Random.Range(0f, Mathf.PI * 2f);
        flingVelocity = new Vector3(Mathf.Cos(heading) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Sin(heading) * Mathf.Sin(tilt)) * flingUpSpeed;
        flingAxis = Random.onUnitSphere;
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
        if (flung)
        {
            Fling();
            return;
        }
        float hop = timer < 0.2f ? Mathf.Sin(timer / 0.2f * Mathf.PI) * snapHop : 0f;
        transform.position = snapBase + Vector3.up * hop;
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

    // Snapped on the ground for snapTime, then flies up tumbling and shrinks away mid-air.
    private void Fling()
    {
        float flight = timer - snapTime;
        if (flight <= 0f)
        {
            transform.position = snapBase;
            return;
        }
        transform.position = snapBase + flingVelocity * flight + Vector3.down * (0.5f * flingGravity * flight * flight);
        transform.Rotate(flingAxis, flingSpin * Time.deltaTime, Space.World);
        if (flight > flingVanishAt)
        {
            float k = 1f - Mathf.Clamp01((flight - flingVanishAt) / shrinkTime);
            transform.localScale = Vector3.one * k;
            if (k <= 0f)
            {
                pool.Release(this);
            }
        }
    }
}
