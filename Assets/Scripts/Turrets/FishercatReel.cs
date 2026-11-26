/*
 * FishercatReel
 * The Fishercat's "Dinner Time" (Path 1 tier 4, 2026-11-26, user's spec): every so often the cat fires the big spear on a
 * line at the toughest rat it can catch, jerks the gun back, the rat flies into the cooler, the lid slams shut and opens
 * again: the rat is dead. On Fishercat.prefab's root; does nothing until its stats are unlocked (tier 4).
 *
 * - Gameplay numbers: FishercatReelStats (Turret hands over this tower's upgraded copy with UseStats). The target is the
 *   rat with the most health (all layers) that is at most HpCap, in range (stats range, 0 = the gun's range, measured
 *   flat from the tower's centre; UseLightRange = anywhere inside the BaseLight), visible to the gun (ShadowSight: shadow
 *   rats only with shadow detection or in a RevealLight), and not already on another Fishercat's line.
 * - The hooked rat's colliders are off until the kill (spears already in flight can't hit it); restored with its renderers.
 * - Steps: Cast (the gun's big spear is hidden, `reelSpear` flies from `muzzle` to the rat at castSpeed, the line
 *   (LineRenderer `line`) trails back to the muzzle; the gun faces the rat and holds its shots via TurretGun.MeleeTarget)
 *   -> Hook (the real rat is hidden, unlisted (no other weapon targets it), held in place and stunned if it can be; a
 *   pooled copy of its Visual (like BaseDive: RatArmor / ShadowLook applied) stands in with the spear stuck in it for
 *   hookHold) -> Pull (Jerk rises for the animator's yank; the copy arcs into the cooler's mouth over pullTime, shrinking
 *   to entryScale, spear and line following) -> Shut (the lid swings closed over lidCloseTime; when closed the real rat is
 *   shown again, moved into the cooler and killed in the same frame, so its death effect and kill count happen there;
 *   the cooler jolts) -> Hold (shutHold) -> Open (lidOpenTime) -> cooldown (stats) -> next.
 * - Children (2026-11-26, user): the kill hit is huge, so a bag / pot rat's spilled rats (EnemySpawner spill, which carries the
 *   leftover damage) die with it at the cooler; any that still live at the cooler's mouth that frame are killed too.
 * - If the rat is gone before the hook (killed, dived, despawned) the spear just comes back (failCooldown). If it leaves
 *   after the hook, its renderers are restored at once (pool safety) and the catch plays out without a kill.
 * - Visual numbers are on this component; placeholders.
 */

using System.Collections.Generic;
using UnityEngine;

public class FishercatReel : MonoBehaviour
{
    private enum Step { Idle, Cast, Hook, Pull, Shut, Hold, Open }

    private static readonly HashSet<Enemy> Claimed = new HashSet<Enemy>();
    private const float EatDamage = 100000f;

    [SerializeField] private FishercatReelStats stats;
    [SerializeField] private Turret turret;
    [SerializeField] private TurretGun gun;

    [Header("Parts")]
    [Tooltip("Where the spear leaves and the line starts (the gun's mouth).")]
    [SerializeField] private Transform muzzle;
    [Tooltip("The big spear shown in the gun; hidden while it is out on the line.")]
    [SerializeField] private GameObject gunSpear;
    [Tooltip("The spear that flies (a tower child, placed in world space each frame).")]
    [SerializeField] private Transform reelSpear;
    [SerializeField] private LineRenderer line;
    [SerializeField] private Transform cooler;
    [SerializeField] private Transform lid;
    [Tooltip("Where caught rats go in, in the cooler's space.")]
    [SerializeField] private Vector3 coolerMouth = new Vector3(0f, 0.5f, 0f);

    [Header("Timing and look")]
    [SerializeField, Min(1f)] private float castSpeed = 26f;
    [SerializeField, Min(0f)] private float hookHold = 0.2f;
    [SerializeField, Min(0.01f)] private float jerkTime = 0.12f;
    [SerializeField, Min(0.05f)] private float pullTime = 0.55f;
    [SerializeField, Min(0f)] private float arcHeight = 2.2f;
    [SerializeField, Range(0.05f, 1f)] private float entryScale = 0.4f;
    [SerializeField, Min(0.01f)] private float lidCloseTime = 0.1f;
    [SerializeField, Min(0f)] private float shutHold = 0.4f;
    [SerializeField, Min(0.01f)] private float lidOpenTime = 0.3f;
    [SerializeField] private float lidOpenAngle = -105f;
    [Tooltip("How far the spear's tip sinks into the rat.")]
    [SerializeField, Min(0f)] private float spearDepth = 0.25f;
    [SerializeField, Min(0f)] private float coolerJolt = 0.08f;
    [SerializeField, Min(0.1f)] private float failCooldown = 1f;

    private Step step;
    private float timer;
    private float cooldown;
    private Enemy target;
    private Transform standIn;
    private ComponentPool<Transform> standInPool;
    private Vector3 standInScale;
    private Vector3 standInStart;
    private Quaternion standInRotation;
    private float spearLength;
    private Vector3 coolerScale;
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly List<Collider> offColliders = new List<Collider>();
    private readonly Dictionary<EnemyStats, ComponentPool<Transform>> pools = new Dictionary<EnemyStats, ComponentPool<Transform>>();
    private Transform poolRoot;

    public FishercatReelStats Stats => stats;
    /// <summary>0..1: how hard the cat is yanking the gun back right now (FishercatAnimator reads it).</summary>
    public float Jerk { get; private set; }
    public bool Busy => step != Step.Idle;
    /// <summary>Rats reeled in and killed (testing).</summary>
    public int Catches { get; private set; }
    /// <summary>Rats hooked (testing): Hooks - Catches = catches that left the game before the cooler shut.</summary>
    public int Hooks { get; private set; }
    /// <summary>Spilled rats eaten with their parent (testing; most die from the leftover damage before this counts).</summary>
    public int ChildrenEaten { get; private set; }

    public void UseStats(FishercatReelStats upgraded)
    {
        stats = upgraded;
    }

    private void Awake()
    {
        if (reelSpear != null)
        {
            MeshFilter filter = reelSpear.GetComponentInChildren<MeshFilter>();
            spearLength = filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.bounds.size.z * filter.transform.lossyScale.z : 1.2f;
            reelSpear.gameObject.SetActive(false);
        }
        if (line != null)
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.enabled = false;
        }
        poolRoot = new GameObject(name + " Reel Catches").transform;
    }

    private void OnDisable()
    {
        Abort();
    }

    private void OnDestroy()
    {
        if (poolRoot != null)
        {
            Destroy(poolRoot.gameObject);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        switch (step)
        {
            case Step.Idle:
                Jerk = 0f;
                if (stats == null || !stats.Unlocked)
                {
                    return;
                }
                cooldown -= dt;
                if (cooldown <= 0f)
                {
                    Enemy pick = PickTarget();
                    if (pick != null)
                    {
                        StartCast(pick);
                    }
                }
                break;
            case Step.Cast:
                TickCast(dt);
                break;
            case Step.Hook:
                timer += dt;
                CheckTargetStillHere();
                PlaceSpearInCatch();
                if (timer >= hookHold)
                {
                    step = Step.Pull;
                    timer = 0f;
                }
                break;
            case Step.Pull:
                TickPull(dt);
                break;
            case Step.Shut:
                timer += dt;
                SetLid(Mathf.Lerp(lidOpenAngle, 0f, Mathf.Clamp01(timer / lidCloseTime)));
                if (timer >= lidCloseTime)
                {
                    KillCatch();
                    step = Step.Hold;
                    timer = 0f;
                }
                break;
            case Step.Hold:
                timer += dt;
                if (cooler != null)
                {
                    float jolt = coolerJolt * Mathf.Sin(Mathf.Clamp01(timer / 0.2f) * Mathf.PI);
                    cooler.localScale = Vector3.Scale(coolerScale, new Vector3(1f + jolt, 1f - jolt, 1f + jolt));
                }
                if (timer >= shutHold)
                {
                    if (cooler != null)
                    {
                        cooler.localScale = coolerScale;
                    }
                    step = Step.Open;
                    timer = 0f;
                }
                break;
            case Step.Open:
                timer += dt;
                SetLid(Mathf.Lerp(0f, lidOpenAngle, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / lidOpenTime))));
                if (timer >= lidOpenTime)
                {
                    step = Step.Idle;
                    cooldown = stats != null ? stats.Cooldown : failCooldown;
                }
                break;
        }
    }

    private Enemy PickTarget()
    {
        bool sees = gun != null && gun.CurrentStats != null && gun.CurrentStats.DetectsShadow;
        float range = stats.Range > 0f ? stats.Range : gun != null ? gun.Range : 0f;
        Vector3 centre = turret != null ? turret.Center : new Vector3(transform.position.x, 0f, transform.position.z);
        BaseLight light = BaseLight.Instance;
        Enemy best = null;
        float bestHealth = -1f;
        List<Enemy> enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || !enemy.Listed || enemy.Health <= 0f || enemy.Health > stats.HpCap || Claimed.Contains(enemy)
                || !ShadowSight.CanTarget(enemy, sees))
            {
                continue;
            }
            Vector3 p = enemy.transform.position;
            bool inRange = stats.UseLightRange && light != null
                ? light.Contains(p)
                : new Vector2(p.x - centre.x, p.z - centre.z).sqrMagnitude <= range * range;
            if (inRange && enemy.Health > bestHealth)
            {
                best = enemy;
                bestHealth = enemy.Health;
            }
        }
        return best;
    }

    private void StartCast(Enemy pick)
    {
        target = pick;
        Claimed.Add(target);
        if (gun != null)
        {
            gun.MeleeTarget = target;
        }
        if (gunSpear != null)
        {
            gunSpear.SetActive(false);
        }
        if (reelSpear != null)
        {
            reelSpear.gameObject.SetActive(true);
            reelSpear.position = MuzzlePoint;
        }
        if (line != null)
        {
            line.enabled = true;
        }
        step = Step.Cast;
        timer = 0f;
    }

    private Vector3 MuzzlePoint => muzzle != null ? muzzle.position : transform.position + Vector3.up * 2f;

    private void TickCast(float dt)
    {
        if (target == null || !target.isActiveAndEnabled || target.Health <= 0f)
        {
            Abort();
            cooldown = failCooldown;
            return;
        }
        Vector3 aim = target.AimPoint;
        Vector3 tip = reelSpear.position + reelSpear.forward * (spearLength * 0.5f);
        Vector3 toAim = aim - tip;
        float stepLength = castSpeed * dt;
        Vector3 dir = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : reelSpear.forward;
        reelSpear.rotation = Quaternion.LookRotation(dir);
        if (toAim.magnitude <= stepLength + spearDepth)
        {
            StartHook();
            return;
        }
        reelSpear.position += dir * stepLength;
        DrawLine();
    }

    private void StartHook()
    {
        Transform visual = target.Visual != null ? target.Visual : target.transform;
        standInPool = PoolFor(target, visual);
        standIn = standInPool.Get(visual.position, visual.rotation);
        RatArmor armor = target.GetComponent<RatArmor>();
        if (armor != null)
        {
            armor.ApplyTo(standIn);
        }
        ShadowLook shadowLook = target.GetComponent<ShadowLook>();
        if (shadowLook != null)
        {
            shadowLook.ApplyTo(standIn);
        }
        standInScale = visual.lossyScale;
        standIn.localScale = standInScale;
        standInStart = visual.position;
        standInRotation = visual.rotation;

        // The real rat: hidden, untargetable, held still (and stunned if it can be) until the cooler shuts.
        hidden.Clear();
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (r.enabled)
            {
                r.enabled = false;
                hidden.Add(r);
            }
        }
        // Colliders off too, so spears already in flight can't hit the hidden rat.
        offColliders.Clear();
        foreach (Collider c in target.GetComponentsInChildren<Collider>())
        {
            if (c.enabled)
            {
                c.enabled = false;
                offColliders.Add(c);
            }
        }
        target.Listed = false;
        Hooks++;
        float hold = hookHold + pullTime + lidCloseTime + 0.5f;
        target.Knockback(Vector2.right, 0f, hold);
        target.AddSlowStack(0.5f, 1, hold, hold, 0f);

        step = Step.Hook;
        timer = 0f;
        PlaceSpearInCatch();
    }

    private void TickPull(float dt)
    {
        timer += dt;
        CheckTargetStillHere();
        Jerk = timer < jerkTime ? Mathf.SmoothStep(0f, 1f, timer / jerkTime)
            : Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, (timer - jerkTime) / Mathf.Max(0.01f, pullTime - jerkTime)));
        float u = Mathf.Clamp01(timer / pullTime);
        Vector3 end = CoolerMouth;
        float arc = arcHeight + Mathf.Max(0f, end.y - standInStart.y);
        float eased = 1f - (1f - u) * (1f - u);
        Vector3 position = Vector3.Lerp(standInStart, end, eased) + Vector3.up * (arc * 4f * u * (1f - u));
        if (standIn != null)
        {
            standIn.position = position;
            standIn.rotation = standInRotation * Quaternion.Euler(Mathf.Lerp(0f, 300f, u), 0f, 0f);
            float shrink = u < 0.6f ? 1f : Mathf.Lerp(1f, entryScale, (u - 0.6f) / 0.4f);
            standIn.localScale = standInScale * shrink;
        }
        PlaceSpearInCatch();
        if (u >= 1f)
        {
            ReleaseStandIn();
            HideSpear();
            if (gun != null)
            {
                gun.MeleeTarget = null;
            }
            Jerk = 0f;
            coolerScale = cooler != null ? cooler.localScale : Vector3.one;
            step = Step.Shut;
            timer = 0f;
        }
    }

    // The spear stuck in the catch, pointing from the gun's mouth into it; the line from the mouth to its butt.
    private void PlaceSpearInCatch()
    {
        if (reelSpear == null || standIn == null)
        {
            return;
        }
        Vector3 at = standIn.position + Vector3.up * 0.3f;
        Vector3 dir = at - MuzzlePoint;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : reelSpear.forward;
        reelSpear.rotation = Quaternion.LookRotation(dir);
        reelSpear.position = at - dir * (spearLength * 0.5f - spearDepth);
        DrawLine();
    }

    private void DrawLine()
    {
        if (line == null || reelSpear == null)
        {
            return;
        }
        line.SetPosition(0, MuzzlePoint);
        line.SetPosition(1, reelSpear.position - reelSpear.forward * (spearLength * 0.5f));
    }

    private Vector3 CoolerMouth => cooler != null ? cooler.TransformPoint(coolerMouth) : transform.position + Vector3.up * 2f;

    // The rat left the game on its own after the hook (dived, despawned): give its renderers back now.
    private void CheckTargetStillHere()
    {
        if (target != null && !target.gameObject.activeInHierarchy)
        {
            RestoreTarget();
            Claimed.Remove(target);
            target = null;
        }
    }

    private void KillCatch()
    {
        if (target == null)
        {
            return;
        }
        Enemy caught = target;
        RestoreTarget();
        Claimed.Remove(caught);
        target = null;
        if (caught.isActiveAndEnabled && caught.Health > 0f)
        {
            Vector3 mouth = CoolerMouth;
            caught.transform.position = mouth;
            // A huge hit, so rats it spills (bag / pot rats) take the leftover and die too; anything still alive that came
            // out at the cooler's mouth this frame is eaten as well (user: "eat all of the children").
            caught.TakeDamage(caught.Health + EatDamage);
            Catches++;
            List<Enemy> enemies = Enemy.Active;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy other = enemies[i];
                if (other != null && other != caught && other.Health > 0f && (other.transform.position - mouth).sqrMagnitude < 0.01f)
                {
                    other.TakeDamage(other.Health + EatDamage);
                    ChildrenEaten++;
                }
            }
        }
    }

    private void RestoreTarget()
    {
        foreach (Renderer r in hidden)
        {
            if (r != null)
            {
                r.enabled = true;
            }
        }
        hidden.Clear();
        foreach (Collider c in offColliders)
        {
            if (c != null)
            {
                c.enabled = true;
            }
        }
        offColliders.Clear();
        if (target != null)
        {
            target.Listed = true;
        }
    }

    private void SetLid(float angle)
    {
        if (lid != null)
        {
            lid.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }

    private void ReleaseStandIn()
    {
        if (standIn != null && standInPool != null)
        {
            standIn.localScale = standInScale;
            standInPool.Release(standIn);
        }
        standIn = null;
        standInPool = null;
    }

    private void HideSpear()
    {
        if (reelSpear != null)
        {
            reelSpear.gameObject.SetActive(false);
        }
        if (line != null)
        {
            line.enabled = false;
        }
        if (gunSpear != null)
        {
            gunSpear.SetActive(true);
        }
    }

    // Puts everything back (cast lost its rat, or the tower was switched off / sold mid-reel).
    private void Abort()
    {
        if (target != null)
        {
            RestoreTarget();
            Claimed.Remove(target);
            target = null;
        }
        ReleaseStandIn();
        if (step != Step.Idle)
        {
            HideSpear();
        }
        if (gun != null && step != Step.Idle)
        {
            gun.MeleeTarget = null;
        }
        if (cooler != null && step == Step.Hold)
        {
            cooler.localScale = coolerScale;
        }
        SetLid(lidOpenAngle);
        Jerk = 0f;
        step = Step.Idle;
    }

    private ComponentPool<Transform> PoolFor(Enemy enemy, Transform visual)
    {
        ComponentPool<Transform> pool;
        EnemyStats key = enemy.Stats;
        if (key != null && pools.TryGetValue(key, out pool))
        {
            return pool;
        }
        pool = new ComponentPool<Transform>(visual, poolRoot, 0);
        if (key != null)
        {
            pools[key] = pool;
        }
        return pool;
    }
}
