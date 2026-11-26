/*
 * Projectile
 * Pooled projectile. Normally flies straight toward where the target was when fired; homing is opt-in
 * per gun (GunStats.homing / homingStrength).
 * Extra bullets of a multi-shot (GunStats.projectileCount > 3) are launched angled away from the aim
 * and turn back to the aim direction at a fixed rate (an arc at the start), then fly straight.
 * Homing bullets (every bullet of a homing gun, including the curving ones) skip the fixed settle
 * heading and steer at the target from launch. If the target dies mid-flight they ask the firing
 * weapon (retarget callback = the loadout's current target for that weapon) for a new one. They give
 * up for good only after they have lined up on the target and then flown past it.
 * Pierce (GunStats.pierce, BTD meaning since 2026-11-20): a bullet makes that many damageable hits (min 1) and is
 * released on the last;
 * each target is hit at most once per bullet. Non-damageable colliders still stop it; Damageables the weapon
 * cannot target (the base, a Structure) are flown through.
 *
 * - Never created or destroyed at runtime: the weapon's ComponentPool owns it. Launch()
 *   fully resets state and the pool returns it with Release() on hit or when lifetime ends.
 * - Damage is looked up per target category from the firing weapon's stats (WeaponStats.DamageFor),
 *   so the same projectile hurts enemies and resources by different amounts.
 * - Hits are detected with a SphereCast along each frame's movement, so fast projectiles
 *   do not tunnel through targets.
 * - SphereCast ignores colliders it starts inside, so Launch checks for a target overlapping the
 *   spawn point (point-blank shots) and every Update checks for one overlapping the current position
 *   before casting. The second matters because colliders only move to their new positions at the
 *   physics step, so an enemy collider can land on a flying bullet between two casts; without the
 *   overlap check that bullet would pass straight through the enemy.
 * - Only Damageable colliders take damage; the owner's own colliders are ignored.
 *   Hitting any other collider also returns the projectile to the pool. Colliders on the "Wall" layer (base wall
 *   blocks) are skipped entirely (HitMask), so bullets fly over the walls.
 * - Nearby homing (2026-11-23): with a homingRadius > 0 (GunStats.HomingRadius) a homing bullet ignores the gun's target
 *   and steers at the nearest enemy within that radius of itself that it hasn't hit and can see (ShadowSight), keeping its
 *   current one while it stays in range; with none that close it flies straight. The flew-past give-up still applies.
 * - Slowing bullets (2026-11-23): a gun with GunStats.SlowPerStack > 0 gives every enemy it hits (also a pot's refused hit)
 *   a slow stack (Enemy.AddSlowStack), and with InfectChance each other enemy within InfectRadius of it gets one by chance
 *   (event StackSpread(from, to) for the look; InfectProc(from, radius) once per hit that spread at least one stack).
 * - Explosive bullets (2026-11-22): a gun whose GunStats.ExplosionChance > 0 rolls it on every enemy hit (also one a pot
 *   refuses) and sets off BulletBlasts.Explode at the enemy's aim point.
 * - The trail is cleared on launch so a reused projectile does not streak from its old position.
 * - Pooled prefab setup: shared unlit material, shadows off, TrailRenderer on a child at the back.
 * - Spent hook (2026-11-24, Fishercat spears): a component on the projectile that implements IProjectileSpent is told
 *   where and in which direction the projectile ended (the cast's hit point, else its position) just before it goes back
 *   to the pool. Nothing listens on ordinary bullets, so they behave as before.
 * - Hit hook (2026-11-25, Angel Cat arrows): a component that implements IProjectileHit is told about every damageable it
 *   hits (where the projectile was, and whether that hit killed it), before the spent hook. Opt-in like the spent hook.
 */

using UnityEngine;

public class Projectile : MonoBehaviour
{
    private static int hitMask;

    // Every layer except "Wall" (looked up once; all layers if the project has no Wall layer).
    private static int HitMask
    {
        get
        {
            if (hitMask == 0)
            {
                int wall = LayerMask.NameToLayer("Wall");
                hitMask = wall >= 0 ? Physics.AllLayers & ~(1 << wall) : Physics.AllLayers;
            }
            return hitMask;
        }
    }

    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float hitRadius = 0.15f;

    private static readonly Collider[] overlapBuffer = new Collider[8];

    private ComponentPool<Projectile> pool;
    private WeaponStats stats;
    private Transform ignoreRoot;
    /// <summary>The owner it was launched with (read-only; the Angel Cat's arrows find their tower through it).</summary>
    public Transform Shooter => ignoreRoot;
    private Vector3 direction;
    private float speed;
    /// <summary>The current flight speed (read-only; the Fishercat's SpearStick flies the spear's last stretch at it).</summary>
    public float Speed => speed;
    private float lifetime;
    private float age;
    private Vector3 finalDirection;
    private float steerSpeed;
    private bool steering;
    private Damageable homingTarget;
    private float homingSpeed;
    private bool homingActive;
    private bool homingAcquired;
    private System.Func<Damageable> retarget;
    private int piercesLeft;
    private float homingRadius;
    private readonly System.Collections.Generic.List<Damageable> hitTargets = new System.Collections.Generic.List<Damageable>();
    private IProjectileSpent spent;
    private bool spentLooked;
    private IProjectileHit hitListener;
    private bool hitLooked;

    // Tells an IProjectileSpent listener where the projectile ended, then returns it to the pool.
    private void Finish(Vector3 where)
    {
        if (!spentLooked)
        {
            spent = GetComponent<IProjectileSpent>();
            spentLooked = true;
        }
        if (spent != null)
        {
            spent.OnProjectileSpent(where, direction);
        }
        pool.Release(this);
    }

    public void Launch(ComponentPool<Projectile> owningPool, Vector3 travelDirection, float travelSpeed,
        WeaponStats weaponStats, float maxLifetime, Transform ignore,
        Vector3? settleDirection = null, float steerDegreesPerSecond = 0f,
        Damageable homeOn = null, float homingDegreesPerSecond = 0f, System.Func<Damageable> retargetCallback = null,
        int pierce = 1, float nearbyHomingRadius = 0f)
    {
        piercesLeft = Mathf.Max(1, pierce);
        homingRadius = homingDegreesPerSecond > 0f ? nearbyHomingRadius : 0f;
        hitTargets.Clear();
        homingActive = homingDegreesPerSecond > 0f && (homeOn != null || homingRadius > 0f);
        homingTarget = homingActive && homingRadius <= 0f ? homeOn : null;
        homingSpeed = homingDegreesPerSecond;
        homingAcquired = false;
        retarget = homingActive ? retargetCallback : null;
        steerSpeed = steerDegreesPerSecond;
        finalDirection = settleDirection.GetValueOrDefault();
        steering = !homingActive && settleDirection.HasValue && steerSpeed > 0f;
        pool = owningPool;
        direction = travelDirection;
        speed = travelSpeed;
        stats = weaponStats;
        lifetime = maxLifetime;
        ignoreRoot = ignore;
        age = 0f;

        transform.rotation = Quaternion.LookRotation(direction);

        if (trail != null)
        {
            trail.Clear();
        }

        if (HitOverlappingTarget())
        {
            Finish(transform.position);
        }
    }

    private bool HitOverlappingTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, overlapBuffer,
            HitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapBuffer[i];
            if (ignoreRoot != null && other.transform.IsChildOf(ignoreRoot))
            {
                continue;
            }

            Damageable target = other.GetComponentInParent<Damageable>();
            if (target != null && !hitTargets.Contains(target) && stats.CanTarget(target.Category))
            {
                if (HitAndCheckDone(target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Curving projectiles (extra bullets of a multi-shot) are launched angled away from the aim and
    // turn back to the aim direction at a fixed rate. It is an opening arc only: once the bullet
    // points along the aim direction it keeps that heading and flies dead straight.
    private void SteerToHeading()
    {
        if (!steering)
        {
            Home();
            return;
        }

        direction = Vector3.RotateTowards(direction, finalDirection, steerSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        transform.rotation = Quaternion.LookRotation(direction);

        if (Vector3.Angle(direction, finalDirection) < 0.1f)
        {
            direction = finalDirection;
            steering = false;
        }
    }

    // Optional homing (GunStats.homing): turn toward the target's current position at a limited rate.
    // A dead target is replaced by the firing weapon's current target. A bullet that was lined up on
    // its target (within 90 degrees) and then ends up facing away from it has flown past: it gives up
    // for good so misses fly off instead of orbiting. Bullets launched angled away start unacquired
    // and keep turning in until they line up.
    private void Home()
    {
        if (!homingActive)
        {
            return;
        }

        if (homingTarget != null && (!homingTarget.isActiveAndEnabled || hitTargets.Contains(homingTarget)))
        {
            homingTarget = null;
            homingAcquired = false;
        }

        if (homingRadius > 0f)
        {
            Damageable next = NearestInReach(homingTarget);
            if (next != homingTarget)
            {
                homingAcquired = false;
            }
            homingTarget = next;
            if (homingTarget == null)
            {
                return;
            }
        }
        else if (homingTarget == null && retarget != null)
        {
            homingTarget = retarget();
            if (homingTarget != null && hitTargets.Contains(homingTarget))
            {
                homingTarget = null;
            }
        }

        if (homingTarget == null)
        {
            return;
        }

        Vector3 wanted = homingTarget.AimPoint - transform.position;
        if (wanted.sqrMagnitude < 0.0001f)
        {
            return;
        }

        wanted.Normalize();
        float angle = Vector3.Angle(direction, wanted);
        if (angle <= 90f)
        {
            homingAcquired = true;
        }
        else if (homingAcquired)
        {
            homingActive = false;
            homingTarget = null;
            return;
        }

        direction = Vector3.RotateTowards(direction, wanted, homingSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        transform.rotation = Quaternion.LookRotation(direction);
    }

    // Infectious Fatigue: each enemy near `source` gets a stack with the gun's InfectChance (no further spreading).
    private static void Infect(Enemy source, GunStats gun)
    {
        Vector3 here = source.transform.position;
        float sqrRadius = gun.InfectRadius * gun.InfectRadius;
        System.Collections.Generic.List<Enemy> enemies = Enemy.Active;
        bool procced = false;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy other = enemies[i];
            if (other == source)
            {
                continue;
            }
            Vector3 offset = other.transform.position - here;
            offset.y = 0f;
            if (offset.sqrMagnitude <= sqrRadius && Random.value < gun.InfectChance
                && other.AddSlowStack(gun.SlowPerStack, gun.SlowMaxStacks, gun.SlowDuration, gun.StunDuration, gun.StunImmunity))
            {
                InfectedStacks++;
                StackSpread?.Invoke(source, other);
                procced = true;
            }
        }
        if (procced)
        {
            InfectProc?.Invoke(source, gun.InfectRadius);
        }
    }

    /// <summary>Infectious Fatigue spread at least one stack from this enemy, within this radius (once per hit).</summary>
    public static event System.Action<Enemy, float> InfectProc;

    /// <summary>A slow stack spread from the hit enemy (first) to a nearby one (second): EnemyStatusLook shows it.</summary>
    public static event System.Action<Enemy, Enemy> StackSpread;

    /// <summary>Stacks spread by Infectious Fatigue (for tests).</summary>
    public static int InfectedStacks { get; private set; }

    // Nearby homing: keep `current` while it is still within homingRadius, else the nearest enemy in reach (null = none).
    private Damageable NearestInReach(Damageable current)
    {
        Vector3 here = transform.position;
        float sqrRadius = homingRadius * homingRadius;
        AimedWeaponStats aimed = stats as AimedWeaponStats;
        bool seesShadows = aimed != null && aimed.DetectsShadow;
        if (current != null && current.isActiveAndEnabled && !hitTargets.Contains(current)
            && (current.AimPoint - here).sqrMagnitude <= sqrRadius)
        {
            return current;
        }
        Damageable best = null;
        float bestSqr = sqrRadius;
        System.Collections.Generic.List<Enemy> enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            float sqr = (enemy.AimPoint - here).sqrMagnitude;
            if (sqr > bestSqr || hitTargets.Contains(enemy) || !ShadowSight.CanTarget(enemy, seesShadows))
            {
                continue;
            }
            best = enemy;
            bestSqr = sqr;
        }
        return best;
    }

    // Deals damage and remembers the target so a piercing bullet never hits it twice.
    // Returns true when this was the bullet's last hit (pierce used up) and it must be released.
    private bool HitAndCheckDone(Damageable target)
    {
        Hit(target);

        piercesLeft--;
        if (piercesLeft <= 0)
        {
            return true;
        }

        hitTargets.Add(target);
        return false;
    }

    private void Hit(Damageable target)
    {
        float amount = stats.DamageFor(target.Category);
        if (amount > 0f)
        {
            target.TakeHit(amount, stats.HitFlags);
        }
        GunStats gun = stats as GunStats;
        Enemy enemy = target as Enemy;
        if (gun != null && gun.SlowPerStack > 0f && enemy != null
            && enemy.AddSlowStack(gun.SlowPerStack, gun.SlowMaxStacks, gun.SlowDuration, gun.StunDuration, gun.StunImmunity)
            && gun.InfectChance > 0f)
        {
            Infect(enemy, gun);
        }
        if (gun != null && gun.ExplosionChance > 0f && target is Enemy && Random.value < gun.ExplosionChance)
        {
            BulletBlasts.Explode(target.AimPoint, gun, target);
        }
        if (!hitLooked)
        {
            hitListener = GetComponent<IProjectileHit>();
            hitLooked = true;
        }
        if (hitListener != null)
        {
            hitListener.OnProjectileHit(target, transform.position, !target.gameObject.activeInHierarchy || target.Health <= 0f);
        }
    }

    private void Update()
    {
        // Colliders only jump to their new positions at the physics step, so one can land on the bullet
        // between two casts; a cast ignores what it starts inside, so check the current position first.
        if (HitOverlappingTarget())
        {
            Finish(transform.position);
            return;
        }

        SteerToHeading();

        float step = speed * Time.deltaTime;

        if (Physics.SphereCast(transform.position, hitRadius, direction, out RaycastHit hit, step,
                HitMask, QueryTriggerInteraction.Ignore))
        {
            bool isOwner = ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot);
            Damageable target = isOwner ? null : hit.collider.GetComponentInParent<Damageable>();
            bool passThrough = target != null && !stats.CanTarget(target.Category);
            if (!isOwner && !passThrough)
            {
                if (target == null || !hitTargets.Contains(target))
                {
                    if (target == null || HitAndCheckDone(target))
                    {
                        Finish(hit.point);
                        return;
                    }
                }
            }
        }

        transform.position += direction * step;

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Finish(transform.position);
        }
    }
}

/// <summary>On a projectile: told where (and heading which way) it ended, just before it returns to the pool.</summary>
public interface IProjectileSpent
{
    void OnProjectileSpent(Vector3 where, Vector3 direction);
}

/// <summary>On a projectile: told about each damageable it hits (where the projectile was, and whether it died).</summary>
public interface IProjectileHit
{
    void OnProjectileHit(Damageable target, Vector3 where, bool killed);
}
