/*
 * FishercatFishing
 * The Fishercat's fishing (Path 3 "water", 2026-11-26, user's spec + animation notes). On Fishercat.prefab's root; does
 * nothing until its stats are unlocked (tier 1).
 *
 * - Pool: the line is cast onto the path: the pool (`pool`, a tower child placed in world space) goes on the Path cell in
 *   range (stats range, 0 = the gun's range) with the most Path cells within its radius (ties: nearest the tower), at
 *   least baseClearance + its radius from every base (rats dive into the box near it), picked again on every upgrade. Its scale = poolRadius (the pool, sharks and float are built for radius 1). The pole stand
 *   (`stand`) turns to face it; the line (LineRenderer `line`) runs from the rod tip to the float (`bobber`) bobbing in it.
 * - Slow: every enemy within the pool gets Enemy.SlowInZone(slow) each frame. Reveal (tier 2): the pool's RevealLight is
 *   on, its radius = the pool's, so shadow rats in it can be targeted by every tower.
 * - Sharks (tier 3+): with at least sharkMinEnemies enemies in the pool and the cooldown up, a shark attack plays:
 *   Grab (the cat stops shooting (TurretGun.HoldFire), turns to the pool and takes the rod off the stand) -> Reel (an
 *   exaggerated reel-in; the small shark jumps out in an arc and bites at the top, or the great white rises mouth open,
 *   comes up biting: it bursts up with its lower jaw (`bigSharkJaw`, hinged at the mouth's corner) dropped wide
 *   (jawGapeAngle), the jaw snaps shut (jawShutAngle) as it reaches the top, then it sinks with its mouth shut; the bite deals sharkDamage to every enemy in the pool; the float
 *   rides on the shark) -> Retract (the line comes back to the rod) -> Cast (a quick cast: the float arcs back to the
 *   pool's middle) -> PutBack (the rod goes back on the stand). Then the cooldown.
 * - The rod: the active one of `rods` (FishercatLooks shows one per tier). While held its world pose is set here from the
 *   cat's `lean` (grip lean-local, rod pitch by phase); it rests on the stand at identity otherwise. FishercatAnimator
 *   reads Holding / CurrentPhase / PhaseProgress and HandleWorld / ReelWorld to pose the paws.
 * - Visual numbers on this component are placeholders.
 */

using System.Collections.Generic;
using UnityEngine;

public class FishercatFishing : MonoBehaviour
{
    public enum Phase { Idle, Grab, Reel, Retract, Cast, PutBack }

    [SerializeField] private FishercatFishingStats stats;
    [SerializeField] private Turret turret;
    [SerializeField] private TurretGun gun;
    [SerializeField] private Transform lean;

    [Header("Parts")]
    [SerializeField] private Transform stand;
    [Tooltip("The rods (one per pole look); the active one is used.")]
    [SerializeField] private Transform[] rods = new Transform[0];
    [SerializeField] private Transform pool;
    [SerializeField] private RevealLight revealLight;
    [SerializeField] private Transform bobber;
    [SerializeField] private LineRenderer line;
    [SerializeField] private Transform smallShark;
    [SerializeField] private Transform bigShark;
    [Tooltip("The great white's lower jaw (hinged at the mouth's corner); the bite swings it shut.")]
    [SerializeField] private Transform bigSharkJaw;
    [Tooltip("Jaw angle (about its hinge) when shut; negative = up against the upper teeth.")]
    [SerializeField] private float jawShutAngle = -60f;
    [Tooltip("How much wider the jaw drops just before the bite.")]
    [SerializeField] private float jawGapeAngle = 12f;
    [Tooltip("Jaw angle while the great white breaks the surface (half open).")]
    [SerializeField] private float jawRiseAngle = -25f;

    [Header("Rod (stand space)")]
    [SerializeField] private Vector3 rodButt = new Vector3(0f, 0.42f, -0.5f);
    [SerializeField] private Vector3 rodTip = new Vector3(0f, 1.75f, 1.45f);
    [Tooltip("Where the cat holds the rod's butt, lean-local.")]
    [SerializeField] private Vector3 grip = new Vector3(0.12f, 0.45f, 0.3f);

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.01f)] private float grabTime = 0.25f;
    [SerializeField, Min(0.1f)] private float reelTime = 1.1f;
    [SerializeField, Min(0.01f)] private float retractTime = 0.3f;
    [SerializeField, Min(0.1f)] private float castTime = 0.5f;
    [SerializeField, Min(0.01f)] private float putBackTime = 0.25f;

    [Header("Look")]
    [SerializeField] private float reelPitchFrom = 25f;
    [SerializeField] private float reelPitchTo = 75f;
    [SerializeField] private float castBackPitch = 100f;
    [SerializeField] private float castForwardPitch = 10f;
    [SerializeField] private float smallJumpHeight = 1.6f;
    [SerializeField] private float bigSharkDepth = 3.1f;
    [SerializeField] private float bobHeight = 0.03f;
    [SerializeField] private float castArc = 1.5f;
    [SerializeField] private float turnSpeed = 10f;
    [Tooltip("The pool stays at least this far (plus its radius) from every base, where rats dive in.")]
    [SerializeField, Min(0f)] private float baseClearance = 3f;

    private Phase phase;
    private float timer;
    private float cooldown;
    private bool placed;
    private bool bitten;
    private Vector3 poolCentre;
    private Vector3 castFrom;
    private readonly List<Enemy> inPool = new List<Enemy>();
    private Vector3 bigSharkScale = Vector3.one;

    public FishercatFishingStats Stats => stats;
    public bool Holding => phase != Phase.Idle;
    public Phase CurrentPhase => phase;
    /// <summary>0..1 through the current phase.</summary>
    public float PhaseProgress { get; private set; }
    /// <summary>Shark bites since Start (testing).</summary>
    public int Bites { get; private set; }
    /// <summary>Enemies hit by shark bites since Start (testing).</summary>
    public int BiteHits { get; private set; }
    public Vector3 PoolCentre => poolCentre;
    public bool Placed => placed;

    public void UseStats(FishercatFishingStats upgraded)
    {
        stats = upgraded;
        placed = false;
    }

    private void Awake()
    {
        if (bigShark != null)
        {
            bigSharkScale = bigShark.localScale;
        }
        if (line != null)
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
        }
    }

    private void OnDisable()
    {
        if (gun != null && phase != Phase.Idle)
        {
            gun.HoldFire = false;
        }
        phase = Phase.Idle;
    }

    private Transform Rod
    {
        get
        {
            for (int i = 0; i < rods.Length; i++)
            {
                if (rods[i] != null && rods[i].gameObject.activeInHierarchy)
                {
                    return rods[i];
                }
            }
            return null;
        }
    }

    /// <summary>The rod's handle (left paw) in world space.</summary>
    public Vector3 HandleWorld => Rod != null ? Rod.TransformPoint(rodButt + RodDir * 0.15f) : transform.position;
    /// <summary>The reel (right paw cranks it) in world space.</summary>
    public Vector3 ReelWorld => Rod != null ? Rod.TransformPoint(rodButt + RodDir * 0.33f + Vector3.down * 0.1f) : transform.position;
    private Vector3 RodDir => (rodTip - rodButt).normalized;
    private Vector3 TipWorld => Rod != null ? Rod.TransformPoint(rodTip) : transform.position + Vector3.up * 3f;

    private void Update()
    {
        bool on = stats != null && stats.Unlocked;
        if (pool != null && pool.gameObject.activeSelf != on)
        {
            pool.gameObject.SetActive(on);
        }
        if (line != null && line.enabled != on)
        {
            line.enabled = on;
        }
        if (!on && bobber != null && bobber.gameObject.activeSelf)
        {
            bobber.gameObject.SetActive(false);
        }
        if (!on)
        {
            return;
        }
        if (!placed)
        {
            Place();
        }
        if (!placed)
        {
            return;
        }
        if (revealLight != null)
        {
            revealLight.enabled = stats.Reveals;
            revealLight.SetRadius(stats.PoolRadius);
        }

        // Who is in the water: slowed.
        inPool.Clear();
        float r2 = stats.PoolRadius * stats.PoolRadius;
        List<Enemy> enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || enemy.Health <= 0f)
            {
                continue;
            }
            Vector3 p = enemy.transform.position;
            if (new Vector2(p.x - poolCentre.x, p.z - poolCentre.z).sqrMagnitude <= r2)
            {
                inPool.Add(enemy);
                enemy.SlowInZone(stats.Slow, 0.2f);
            }
        }

        if (phase == Phase.Idle)
        {
            cooldown -= Time.deltaTime;
            if (stats.SharkDamage > 0f && cooldown <= 0f && inPool.Count >= stats.SharkMinEnemies && Rod != null)
            {
                StartAttack();
            }
        }
        else
        {
            Advance(Time.deltaTime);
        }
    }

    // Puts the pool on the best Path cell in range.
    private void Place()
    {
        BuildGrid grid = BuildGrid.Instance;
        if (grid == null || pool == null)
        {
            return;
        }
        float range = stats.Range > 0f ? stats.Range : gun != null ? gun.Range : 8f;
        Vector3 centre = turret != null ? turret.Center : new Vector3(transform.position.x, 0f, transform.position.z);
        Vector2Int c0 = grid.WorldToCell(centre);
        int reach = Mathf.CeilToInt(range / grid.CellSize) + 1;
        int around = Mathf.CeilToInt(stats.PoolRadius / grid.CellSize);
        float r2 = stats.PoolRadius * stats.PoolRadius;
        int bestScore = -1;
        float bestDistance = float.MaxValue;
        Vector3 best = Vector3.zero;
        for (int dx = -reach; dx <= reach; dx++)
        {
            for (int dz = -reach; dz <= reach; dz++)
            {
                Vector2Int cell = c0 + new Vector2Int(dx, dz);
                if (!grid.InBounds(cell) || grid.GetState(cell) != CellState.Path)
                {
                    continue;
                }
                Vector3 world = grid.CellToWorld(cell);
                float distance = new Vector2(world.x - centre.x, world.z - centre.z).magnitude;
                if (distance > range || NearBase(world))
                {
                    continue;
                }
                int score = 0;
                for (int ax = -around; ax <= around; ax++)
                {
                    for (int az = -around; az <= around; az++)
                    {
                        Vector2Int other = cell + new Vector2Int(ax, az);
                        if (grid.InBounds(other) && grid.GetState(other) == CellState.Path && (ax * ax + az * az) * grid.CellSize * grid.CellSize <= r2)
                        {
                            score++;
                        }
                    }
                }
                if (score > bestScore || (score == bestScore && distance < bestDistance))
                {
                    bestScore = score;
                    bestDistance = distance;
                    best = world;
                }
            }
        }
        if (bestScore < 0)
        {
            return;
        }
        poolCentre = new Vector3(best.x, 0f, best.z);
        pool.position = poolCentre;
        pool.rotation = Quaternion.identity;
        pool.localScale = Vector3.one * stats.PoolRadius;
        ResetSharks();
        placed = true;
    }

    // Rats dive into the box near it, so the pool stays baseClearance + its radius away from every base.
    private bool NearBase(Vector3 world)
    {
        List<BaseStructure> bases = BaseStructure.Active;
        float keep = stats.PoolRadius + baseClearance;
        for (int i = 0; i < bases.Count; i++)
        {
            Vector3 b = bases[i].transform.position;
            if (new Vector2(world.x - b.x, world.z - b.z).sqrMagnitude < keep * keep)
            {
                return true;
            }
        }
        return false;
    }

    private void ResetSharks()
    {
        if (smallShark != null)
        {
            smallShark.gameObject.SetActive(false);
        }
        if (bigShark != null)
        {
            bigShark.gameObject.SetActive(false);
            bigShark.localScale = bigSharkScale;
        }
        if (bigSharkJaw != null)
        {
            bigSharkJaw.localRotation = Quaternion.identity;
        }
    }

    private void LateUpdate()
    {
        if (!placed || stats == null || !stats.Unlocked)
        {
            return;
        }
        Vector3 flat = poolCentre - (stand != null ? stand.position : transform.position);
        flat.y = 0f;
        if (stand != null && flat.sqrMagnitude > 0.01f)
        {
            stand.rotation = Quaternion.LookRotation(flat);
        }
        if (phase != Phase.Idle && gun != null)
        {
            Vector3 toPool = poolCentre - gun.transform.position;
            toPool.y = 0f;
            Vector3 facing = Vector3.Slerp(gun.transform.forward, toPool.normalized, Mathf.Clamp01(turnSpeed * Time.deltaTime));
            facing.y = 0f;
            gun.SnapTo(facing);
        }
        PoseRod();
        PlaceBobberAndLine();
    }

    private void StartAttack()
    {
        phase = Phase.Grab;
        timer = 0f;
        bitten = false;
        if (gun != null)
        {
            gun.HoldFire = true;
        }
    }

    private void Advance(float dt)
    {
        timer += dt;
        float length = PhaseLength(phase);
        PhaseProgress = Mathf.Clamp01(timer / length);
        if (phase == Phase.Reel)
        {
            TickShark(PhaseProgress);
        }
        if (timer < length)
        {
            return;
        }
        timer = 0f;
        PhaseProgress = 0f;
        switch (phase)
        {
            case Phase.Grab:
                phase = Phase.Reel;
                break;
            case Phase.Reel:
                ResetSharks();
                phase = Phase.Retract;
                break;
            case Phase.Retract:
                castFrom = TipWorld;
                phase = Phase.Cast;
                break;
            case Phase.Cast:
                phase = Phase.PutBack;
                break;
            default:
                phase = Phase.Idle;
                cooldown = stats.SharkCooldown;
                if (gun != null)
                {
                    gun.HoldFire = false;
                }
                break;
        }
    }

    private float PhaseLength(Phase p)
    {
        switch (p)
        {
            case Phase.Grab: return grabTime;
            case Phase.Reel: return reelTime;
            case Phase.Retract: return retractTime;
            case Phase.Cast: return castTime;
            case Phase.PutBack: return putBackTime;
        }
        return 1f;
    }

    // The shark during the reel (u 0..1): the small one jumps (bite at the top), the great white rises, bites and sinks.
    private void TickShark(float u)
    {
        bool big = stats.BigShark && bigShark != null;
        Transform shark = big ? bigShark : smallShark;
        if (shark == null)
        {
            return;
        }
        if (!big)
        {
            float k = Mathf.InverseLerp(0.15f, 0.85f, u);
            bool up = u > 0.15f && u < 0.85f;
            shark.gameObject.SetActive(up);
            if (up)
            {
                shark.localPosition = new Vector3(0f, smallJumpHeight * 4f * k * (1f - k) - 0.3f, 0f);
                shark.localRotation = Quaternion.Euler(Mathf.Lerp(-70f, 70f, k), 30f, 0f);
                float snap = Mathf.Clamp01(1f - Mathf.Abs(k - 0.5f) * 8f);
                shark.localScale = new Vector3(1f, 1f - 0.25f * snap, 1f);
            }
            if (!bitten && k >= 0.5f)
            {
                Bite();
            }
        }
        else
        {
            shark.gameObject.SetActive(true);
            // Comes up biting, then goes down (user, 2026-11-26): it bursts up with the jaw dropped wide, the jaw snaps shut
            // as it reaches the top (the damage), a short pause, then it sinks with its mouth shut.
            float rise = u < 0.25f ? Mathf.SmoothStep(0f, 1f, u / 0.25f) : u < 0.38f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.38f) / 0.37f);
            shark.localPosition = new Vector3(0f, -bigSharkDepth * (1f - rise), 0f);
            if (bigSharkJaw != null)
            {
                float angle;
                // Half open while it breaks the surface (so the open jaw doesn't lie on the ground), wide near the top,
                // then the snap.
                if (u < 0.12f)
                {
                    angle = jawRiseAngle;
                }
                else if (u < 0.19f)
                {
                    angle = Mathf.Lerp(jawRiseAngle, jawGapeAngle, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.19f, u)));
                }
                else if (u < 0.27f)
                {
                    float k = Mathf.InverseLerp(0.19f, 0.27f, u);
                    angle = Mathf.Lerp(jawGapeAngle, jawShutAngle, k * k);
                }
                else
                {
                    angle = jawShutAngle;
                }
                bigSharkJaw.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            if (!bitten && u >= 0.27f)
            {
                Bite();
            }
        }
    }

    private void Bite()
    {
        bitten = true;
        Bites++;
        for (int i = inPool.Count - 1; i >= 0; i--)
        {
            Enemy enemy = inPool[i];
            if (enemy != null && enemy.isActiveAndEnabled && enemy.Health > 0f)
            {
                enemy.TakeDamage(stats.SharkDamage);
                BiteHits++;
            }
        }
    }

    // The rod: on the stand, or in the cat's paws (Grab/PutBack blend between the two).
    private void PoseRod()
    {
        Transform rod = Rod;
        if (rod == null)
        {
            return;
        }
        float held;
        float pitch = reelPitchFrom;
        switch (phase)
        {
            case Phase.Grab: held = Mathf.SmoothStep(0f, 1f, PhaseProgress); break;
            case Phase.Reel:
                held = 1f;
                // Exaggerated: up in jerks.
                pitch = Mathf.Lerp(reelPitchFrom, reelPitchTo, PhaseProgress) + 8f * Mathf.Sin(PhaseProgress * Mathf.PI * 7f);
                break;
            case Phase.Retract: held = 1f; pitch = reelPitchTo; break;
            case Phase.Cast:
                held = 1f;
                pitch = PhaseProgress < 0.4f ? Mathf.Lerp(reelPitchTo, castBackPitch, Mathf.SmoothStep(0f, 1f, PhaseProgress / 0.4f))
                    : Mathf.Lerp(castBackPitch, castForwardPitch, Mathf.SmoothStep(0f, 1f, (PhaseProgress - 0.4f) / 0.6f));
                break;
            case Phase.PutBack: held = 1f - Mathf.SmoothStep(0f, 1f, PhaseProgress); pitch = castForwardPitch; break;
            default: held = 0f; break;
        }
        rod.localPosition = Vector3.zero;
        rod.localRotation = Quaternion.identity;
        if (held <= 0f || lean == null)
        {
            return;
        }
        Vector3 restPos = rod.position;
        Quaternion restRot = rod.rotation;
        Vector3 d0 = RodDir;
        Vector3 want = lean.rotation * (Quaternion.Euler(-pitch, 0f, 0f) * Vector3.forward);
        Quaternion heldRot = Quaternion.LookRotation(want, lean.up) * Quaternion.Inverse(Quaternion.LookRotation(d0, Vector3.up));
        Vector3 heldPos = lean.TransformPoint(grip) - heldRot * rodButt;
        rod.position = Vector3.Lerp(restPos, heldPos, held);
        rod.rotation = Quaternion.Slerp(restRot, heldRot, held);
    }

    // The float: bobbing in the pool, riding the shark, coming back along the line, or arcing back out on the cast.
    private void PlaceBobberAndLine()
    {
        if (bobber == null)
        {
            return;
        }
        Vector3 rest = poolCentre + Vector3.up * (0.02f + bobHeight * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f)));
        Vector3 at = rest;
        bool show = true;
        switch (phase)
        {
            case Phase.Reel:
            {
                Transform shark = stats.BigShark && bigShark != null ? bigShark : smallShark;
                if (shark != null && shark.gameObject.activeInHierarchy)
                {
                    at = shark.position + Vector3.up * (stats.BigShark ? 1.6f * stats.PoolRadius : 0.3f);
                }
                break;
            }
            case Phase.Retract:
                at = Vector3.Lerp(rest, TipWorld, Mathf.SmoothStep(0f, 1f, PhaseProgress));
                break;
            case Phase.Cast:
                if (PhaseProgress < 0.55f)
                {
                    at = TipWorld;
                    show = false;
                }
                else
                {
                    float k = Mathf.InverseLerp(0.55f, 1f, PhaseProgress);
                    at = Vector3.Lerp(castFrom, rest, k) + Vector3.up * (castArc * 4f * k * (1f - k));
                }
                break;
        }
        bobber.position = at;
        if (bobber.gameObject.activeSelf != show)
        {
            bobber.gameObject.SetActive(show);
        }
        if (line != null)
        {
            line.SetPosition(0, TipWorld);
            line.SetPosition(1, show ? at + Vector3.up * 0.18f : TipWorld);
        }
    }
}
