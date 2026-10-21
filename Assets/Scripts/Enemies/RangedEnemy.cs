/*
 * RangedEnemy
 * Enemy that walks toward its target (the player or the base, whichever is closer; see Enemy.Target), stands
 * still and shoots once the target is within range, and now and then walks a bit closer. All numbers come from a RangedEnemyStats asset (set as the Enemy's Stats) (range, fire interval, projectile
 * speed / lifetime / damage / count, how often it advances).
 *
 * - States: Approach (walk until the target is within range), Shoot (stand still, fire a volley every
 *   fireInterval), Advance (after a random number of volleys it rolls moveCloserChance; on success it walks
 *   toward the target for advanceDistance units, then goes back to Shoot). If the target moves out of range it
 *   goes back to Approach. Range is measured to the target's edge (TargetRadius), so a big base counts from its
 *   walls.
 * - It never moves and shoots at once: a volley only fires while the enemy is in Shoot, not knocked back, and has
 *   actually stood still (below StillSpeed) for SettleTime, so after an Advance it must come to a full stop
 *   first, and being pushed by a crowd or knocked back holds fire until it settles. The fire timer keeps
 *   counting meanwhile, so the volley goes off as soon as it is still.
 * - Walking itself is EnemyMovementSystem's (seek + separation); this class only says whether the enemy wants
 *   to move (WantsToMove), so it always keeps its spacing from neighbours even while standing.
 * - A volley leaves the Muzzle transform (a child of Visual placed at the gun on the model, so it turns with
 *   the model) and flies straight at the target's flat position. projectileCount > 1 fans the projectiles
 *   evenly across spreadAngle. Projectiles come from a shared pool per prefab (EnemyProjectile.PoolFor).
 */

using UnityEngine;

public class RangedEnemy : Enemy
{
    private enum State { Approach, Shoot, Advance }

    private const float StillSpeed = 0.1f;
    private const float SettleTime = 0.15f;

    [SerializeField] private Transform muzzle;

    private RangedEnemyStats rangedStats;
    private ComponentPool<EnemyProjectile> projectilePool;
    private State state;
    private float fireTimer;
    private int shotsFired;
    private int shotsUntilDecision;
    private float advanceLeft;
    private Vector3 previousPosition;
    private float stillTime;

    public override bool WantsToMove => state != State.Shoot;

    protected override void Awake()
    {
        base.Awake();
        rangedStats = (RangedEnemyStats)Stats;
        projectilePool = EnemyProjectile.PoolFor(rangedStats.ProjectilePrefab, 16);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        state = State.Approach;
        shotsFired = 0;
        shotsUntilDecision = RollShotsBeforeMove();
        advanceLeft = 0f;
        previousPosition = transform.position;
        stillTime = 0f;
    }

    protected override void Update()
    {
        base.Update();

        if (Target == null)
        {
            return;
        }

        float distance = EdgeDistance(transform.position, Target);

        Vector3 step = transform.position - previousPosition;
        step.y = 0f;
        float speed = Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;
        stillTime = speed < StillSpeed && !IsKnockedBack ? stillTime + Time.deltaTime : 0f;

        switch (state)
        {
            case State.Approach:
                if (distance <= rangedStats.Range)
                {
                    EnterShoot();
                }

                break;

            case State.Shoot:
                if (distance > rangedStats.Range)
                {
                    state = State.Approach;
                    break;
                }

                fireTimer = Mathf.Max(0f, fireTimer - Time.deltaTime);
                if (fireTimer <= 0f && stillTime >= SettleTime)
                {
                    Fire();
                    fireTimer = rangedStats.FireInterval;
                    AfterVolley();
                }

                break;

            case State.Advance:
                advanceLeft -= step.magnitude;
                if (advanceLeft <= 0f || distance <= rangedStats.Range * 0.25f)
                {
                    EnterShoot();
                }

                break;
        }

        previousPosition = transform.position;
    }

    private void EnterShoot()
    {
        state = State.Shoot;
        fireTimer = Random.Range(0.2f, 0.6f) * rangedStats.FireInterval;
    }

    // After every few volleys, maybe walk closer.
    private void AfterVolley()
    {
        shotsFired++;
        if (shotsFired < shotsUntilDecision)
        {
            return;
        }

        shotsFired = 0;
        shotsUntilDecision = RollShotsBeforeMove();

        if (Random.value < rangedStats.MoveCloserChance)
        {
            state = State.Advance;
            advanceLeft = rangedStats.AdvanceDistance;
            previousPosition = transform.position;
        }
    }

    private int RollShotsBeforeMove()
    {
        return Random.Range(rangedStats.ShotsBeforeMoveMin, rangedStats.ShotsBeforeMoveMax + 1);
    }

    private void Fire()
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector3 toTarget = Target.transform.position - origin;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 aim = toTarget.normalized;
        int count = rangedStats.ProjectileCount;

        for (int i = 0; i < count; i++)
        {
            float yaw = count == 1
                ? 0f
                : Mathf.Lerp(-rangedStats.SpreadAngle * 0.5f, rangedStats.SpreadAngle * 0.5f, i / (float)(count - 1));
            Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * aim;

            EnemyProjectile projectile = projectilePool.Get(origin, Quaternion.LookRotation(direction));
            projectile.Launch(projectilePool, direction, rangedStats.ProjectileSpeed, rangedStats.ProjectileLifetime,
                rangedStats.HitRadius, rangedStats.Damage, rangedStats.Targets);
        }
    }
}
