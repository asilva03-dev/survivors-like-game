/*
 * Enemy
 * Placeholder enemy: a Damageable in the Enemy category that is also tracked in its own list.
 *
 * - Health, rank, category, damage events and death handling all come from Damageable.
 * - Active holds every enabled enemy (typed), for systems that only care about enemies: movement,
 *   spawning and the debug kill key.
 * - Enemy movement lives in EnemyMovementSystem and spawning / pooling in EnemySpawner.
 * - Serialized fields (maxHealth, rank) are inherited, so existing enemy prefabs keep their values.
 * - An optional EnemyStats asset gives the enemy type its health and move speed (applied in Awake, so it
 *   overrides the prefab's Max Health); without one the prefab health and EnemySettings speed apply.
 *   WantsToMove lets a subclass (ranged enemies) stand still; EnemyMovementSystem only seeks while it is true.
 * - Visual is the child that holds the model; EnemyMovementSystem turns it to face the player (the root, with
 *   the collider and health bar, never rotates).
 * - Target (refreshed every frame; movement, contact damage and ranged fire all use it) follows the light rule
 *   (TD slice 4): each enemy has an Assignment (Base or Player; reset on enable: Player for hunters, Base for the
 *   rest; the spawner may change it right after spawning).
 *   - Hunters (EnemyStats focus Hunter) only ever target the player, never a base, and never enter the light
 *     (EnemyMovementSystem keeps them outside it). While the player is in the light: Target is null (no fire),
 *     they walk up to the edge and linger for the stats' hunterLingerTime, then retreat straight away from the base
 *     (MoveGoal) and ask to be despawned once off-screen (DespawnRequested; the spawner returns them to their pool,
 *     no death effect or drops). If the player steps out first they hunt again.
 *   - A non-hunter inside the BaseLight switches to Base for good.
 *   - Player-assigned: the player (straight seek), wherever the player is; it follows them into the light and
 *     switches there.
 *   - Base-assigned: the closest base (flow field), except while the player is within the stats' playerAggroRadius
 *     (released AggroRelease units further out, so it doesn't flicker).
 *   - No base: the player; no player: the closest base.
 *   - Player on a structure (2026-11-22, user: "don't track the player if they are on top of a fence or the box even
 *     if the player is close"): while PlayerController.OnStructure, a non-hunter never aggros on the player and a
 *     player-assigned one goes for the closest base instead (its assignment is kept, so it hunts again once the player
 *     steps down). Hunters are unchanged (fences and the box are in the light, where they already back off).
 * - Contact damage: an enemy within its stats' contactRange of its target's edge hurts it, at most once per
 *   contactInterval (see EnemyStats), except a player whose feet are out of bite reach of this enemy's feet (jumping,
 *   on a wall top, or above/below it across a cliff: PlayerController.OutOfBiteReachFrom, measured from FeetY = the
 *   root minus rootAboveFeet, 1 on the rat prefabs). Subclasses that override Update must call base.Update().
 *   Against the base (2026-11-20, user): with a BaseDive in the scene, an enemy reaching contactRange of the base's
 *   edge hands itself to BaseDive (which plays the leap into the box and deals the stats' baseHitDamage once) and
 *   leaves at once through EnterBase (event EnteredBase: no death effect, no drops, no wave refund). Without a
 *   BaseDive it bites the base as before.
 * - Layers (2026-11-20): with EnemyStats layers, health is a stack of layers (see EnemyStats). The enemy comes out of
 *   the pool bare; the spawner calls SetExtraLayers(n) right after Get. Layers / TopLayerName say what is left; each
 *   hit that strips a layer fires LayerPopped(enemy, before, after) (layer visuals and pop effects listen to it).
 * - Properties (2026-11-21): IsShadow (per spawn, SetShadow; see ShadowSight) and, from the stats, NeedsLead (pots refuse
 *   weapon hits without HitFlags.Lead through Damageable.BlocksHit).
 * - Swarm (2026-11-21): the RatSwarms group this enemy belongs to (null for every other enemy; cleared on enable,
 *   set by RatSwarms right after spawning); EnemyMovementSystem pulls members toward the group's centre.
 * - Slow / stun (2026-11-23, gun turret Optics T3-T5): AddSlowStack(perStack, maxStacks, duration, stunDuration,
 *   stunImmunity) adds one stack (up to maxStacks; the strongest perStack seen is kept); all stacks drop together
 *   `duration` seconds after the last one. SpeedMultiplier = 1 - stacks x perStack (min 0.1), 0 while stunned
 *   (EnemyMovementSystem multiplies the move speed by it). With stunDuration > 0, reaching maxStacks stuns the enemy for
 *   stunDuration (stacks cleared), then it can't be stunned again for stunImmunity seconds; an enemy whose stats are
 *   StunImmune (bag rat) is never stunned and just stays at the full stack. A stunned enemy doesn't bite
 *   or dive. All reset on enable (pooling).
 * - Knockback(direction, distance, duration) pushes the enemy away with an ease-out; while it lasts
 *   EnemyMovementSystem pauses seeking and applies TakeKnockbackStep each frame.
 */

using System.Collections.Generic;
using UnityEngine;

public enum EnemyAssignment
{
    Base,
    Player
}

public class Enemy : Damageable
{
    private enum HunterState { Hunt, Linger, Retreat }

    // A base-bound enemy that turned on the player lets go this much past its aggro radius.
    private const float AggroRelease = 1f;
    // How far a retreating hunter aims past itself, and the viewport margin that counts as off-screen.
    private const float RetreatReach = 50f;
    private const float OffscreenMargin = 0.05f;

    public static readonly List<Enemy> Active = new List<Enemy>();

    // A hunter that retreated off-screen asks to be removed (EnemySpawner releases it to its pool).
    public static event System.Action<Enemy> DespawnRequested;
    // An enemy dived into the base (BaseDive): gone without dying, never refunded (EnemySpawner releases it).
    public static event System.Action<Enemy> EnteredBase;

    [SerializeField] private Transform visual;
    [SerializeField] private EnemyStats stats;
    [Tooltip("How far the root sits above the model's feet (the rat prefabs: 1). Used to stand enemies on the map ground and for bite reach.")]
    [SerializeField] private float rootAboveFeet = 1f;

    private Vector2 knockDirection;
    private float knockDistance;
    private float knockDuration;
    private float knockRemaining;
    private float contactCooldown;
    private bool aggroed;
    private float slowPerStack;
    private float slowTimer;
    private float stunTimer;
    private float stunImmuneTimer;
    private HunterState hunterState;
    private float lingerTimer;

    // What this enemy is going for: the player or a base (light rule, refreshed every frame).
    public Damageable Target { get; private set; }
    // Downward speed while falling off a ledge (EnemyMovementSystem; 0 on the ground). Reset on enable.
    public float FallSpeed { get; set; }
    public float RootAboveFeet => rootAboveFeet;
    /// <summary>World y of the enemy's feet (root minus rootAboveFeet).</summary>
    public float FeetY => transform.position.y - rootAboveFeet;
    public EnemyAssignment Assignment { get; set; }
    public bool IsHunter => stats != null && stats.IsHunter;
    // Set while a hunter retreats: EnemyMovementSystem walks it to MoveGoal instead of its target.
    public bool HasMoveGoal { get; private set; }
    public Vector3 MoveGoal { get; private set; }
    public bool IsLingering => hunterState == HunterState.Linger;
    public bool IsRetreating => hunterState == HunterState.Retreat;
    public bool IsKnockedBack => knockRemaining > 0f;
    /// <summary>Slow stacks on this enemy (Optics upgrades); 0 = not slowed.</summary>
    public int SlowStacks { get; private set; }
    public bool IsStunned => stunTimer > 0f;
    /// <summary>Move speed multiplier from slows / stun (1 = normal, 0 = stunned).</summary>
    public float SpeedMultiplier => IsStunned ? 0f : Mathf.Max(0.1f, 1f - SlowStacks * slowPerStack);

    /// <summary>Adds one slow stack (see the header). Returns false when it couldn't (dead/disabled).</summary>
    public bool AddSlowStack(float perStack, int maxStacks, float duration, float stunDuration, float stunImmunity)
    {
        if (!isActiveAndEnabled || Health <= 0f || maxStacks <= 0)
        {
            return false;
        }
        slowPerStack = Mathf.Max(slowPerStack, perStack);
        slowTimer = duration;
        if (IsStunned)
        {
            return true;
        }
        SlowStacks = Mathf.Min(maxStacks, SlowStacks + 1);
        bool canStun = stats == null || !stats.StunImmune;
        if (canStun && stunDuration > 0f && SlowStacks >= maxStacks && stunImmuneTimer <= 0f)
        {
            stunTimer = stunDuration;
            stunImmuneTimer = stunDuration + stunImmunity;
            SlowStacks = 0;
        }
        return true;
    }
    public Transform Visual => visual;
    public EnemyStats Stats => stats;
    public virtual bool WantsToMove => true;

    // A layer was stripped: (enemy, layers before, layers left). Fires once per hit, even when it strips several.
    public static event System.Action<Enemy, int, int> LayerPopped;

    public bool HasLayers => stats != null && stats.HasLayers;
    /// <summary>Shadow rat (per spawn): towers and auto-aim can only target it with shadow detection or in a RevealLight.</summary>
    public bool IsShadow { get; private set; }
    /// <summary>The swarm this enemy belongs to (RatSwarms), or null.</summary>
    public RatSwarms.Group Swarm { get; set; }

    /// <summary>Spawn-time: makes this enemy a shadow (reset to false whenever it comes out of the pool).</summary>
    public void SetShadow(bool shadow)
    {
        IsShadow = shadow;
    }

    // Pots (stats NeedsLead) refuse hits without the Lead property.
    protected override bool BlocksHit(HitFlags flags)
    {
        return stats != null && stats.NeedsLead && (flags & HitFlags.Lead) == 0;
    }

    /// <summary>Damage the killing hit dealt past 0 health (health isn't clamped); 0 while alive.</summary>
    public float Overkill => Health < 0f ? -Health : 0f;
    /// <summary>Layers left (1 = bare); 0 for enemies without layers.</summary>
    public int Layers { get; private set; }
    /// <summary>Name of the outermost layer left (EnemyStats.layers), "" without layers.</summary>
    public string TopLayerName => HasLayers ? stats.LayerName(Layers - 1) : "";

    /// <summary>Spawn-time armor: the bare layer plus `extra` armor layers (clamped to the stats' list), at full health.</summary>
    public void SetExtraLayers(int extra)
    {
        if (!HasLayers)
        {
            return;
        }
        Layers = Mathf.Clamp(1 + extra, 1, stats.LayerCount);
        SetMaxHealth(Layers * stats.LayerHealth);
        RestoreFullHealth();
    }

    // Layered enemies: damage carries through the layers; LayerPopped fires when the outermost layer count drops.
    public override void TakeDamage(float amount)
    {
        int before = Layers;
        base.TakeDamage(amount);
        if (!HasLayers || Health <= 0f)
        {
            return;
        }
        int left = Mathf.Clamp(Mathf.CeilToInt(Health / stats.LayerHealth - 0.0001f), 1, stats.LayerCount);
        if (left < before)
        {
            Layers = left;
            LayerPopped?.Invoke(this, before, left);
        }
    }

    protected virtual void Awake()
    {
        if (stats != null)
        {
            SetMaxHealth(stats.HasLayers ? stats.LayerHealth : stats.MaxHealth);
        }
    }

    protected override void OnEnable()
    {
        // A layered enemy comes back from the pool bare; the spawner adds armor with SetExtraLayers.
        if (HasLayers)
        {
            SetMaxHealth(stats.LayerHealth);
            Layers = 1;
        }
        base.OnEnable();
        Active.Add(this);
        IsShadow = false;
        Swarm = null;
        knockRemaining = 0f;
        FallSpeed = 0f;
        contactCooldown = 0f;
        aggroed = false;
        SlowStacks = 0;
        slowPerStack = 0f;
        slowTimer = 0f;
        stunTimer = 0f;
        stunImmuneTimer = 0f;
        hunterState = HunterState.Hunt;
        HasMoveGoal = false;
        Assignment = IsHunter ? EnemyAssignment.Player : EnemyAssignment.Base;
        RefreshTarget();
    }

    protected virtual void Update()
    {
        TickStatus();
        RefreshTarget();
        if (!IsStunned)
        {
            TryContactDamage();
        }
    }

    private void TickStatus()
    {
        float dt = Time.deltaTime;
        if (stunTimer > 0f)
        {
            stunTimer -= dt;
        }
        if (stunImmuneTimer > 0f)
        {
            stunImmuneTimer -= dt;
        }
        if (SlowStacks > 0)
        {
            slowTimer -= dt;
            if (slowTimer <= 0f)
            {
                SlowStacks = 0;
                slowPerStack = 0f;
            }
        }
    }

    // The light rule (see the header).
    public void RefreshTarget()
    {
        Vector3 position = transform.position;
        Damageable player = PlayerHealth.Instance;
        Damageable closestBase = ClosestBase(position);

        BaseLight baseLight = BaseLight.Instance;
        if (IsHunter)
        {
            aggroed = false;
            RefreshHunter(player, baseLight);
            return;
        }

        if (Assignment == EnemyAssignment.Player && baseLight != null && baseLight.Contains(position))
        {
            Assignment = EnemyAssignment.Base;
        }

        bool playerPerched = PlayerController.Instance != null && PlayerController.Instance.OnStructure;
        if (closestBase == null || (Assignment == EnemyAssignment.Player && player != null))
        {
            aggroed = false;
            Target = player != null && (!playerPerched || closestBase == null) ? player : closestBase;
            return;
        }

        if (player != null && stats != null && !playerPerched)
        {
            float reach = stats.PlayerAggroRadius + (aggroed ? AggroRelease : 0f);
            Vector3 offset = player.transform.position - position;
            offset.y = 0f;
            aggroed = offset.sqrMagnitude <= reach * reach;
        }
        else
        {
            aggroed = false;
        }
        Target = aggroed ? player : closestBase;
    }

    // Hunt while the player is in the dark; once they are in the light: linger at the edge, retreat, despawn off-screen.
    private void RefreshHunter(Damageable player, BaseLight baseLight)
    {
        HasMoveGoal = false;
        if (player == null || baseLight == null || !baseLight.IsPlayerInside)
        {
            hunterState = HunterState.Hunt;
            Target = player;
            return;
        }

        Target = null;
        if (hunterState == HunterState.Hunt)
        {
            hunterState = HunterState.Linger;
            lingerTimer = stats.HunterLingerTime;
        }
        if (hunterState == HunterState.Linger)
        {
            lingerTimer -= Time.deltaTime;
            if (lingerTimer > 0f)
            {
                return;
            }
            hunterState = HunterState.Retreat;
        }

        Vector3 away = transform.position - baseLight.Center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector3.forward;
        }
        MoveGoal = transform.position + away.normalized * RetreatReach;
        HasMoveGoal = true;

        if (IsOffscreen())
        {
            RequestDespawn();
        }
    }

    private bool IsOffscreen()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return true;
        }
        Vector3 view = cam.WorldToViewportPoint(transform.position);
        return view.z <= 0f || view.x < -OffscreenMargin || view.x > 1f + OffscreenMargin
            || view.y < -OffscreenMargin || view.y > 1f + OffscreenMargin;
    }

    // Removed without dying (no death effect, no drops): the spawner releases it, else it just switches off.
    public void RequestDespawn()
    {
        if (DespawnRequested != null)
        {
            DespawnRequested(this);
        }
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    // Dived into the base: removed without dying and without a wave refund (only the spawner listens: it releases it to
    // its pool), else it just switches off.
    public void EnterBase()
    {
        if (EnteredBase != null)
        {
            EnteredBase(this);
        }
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private static Damageable ClosestBase(Vector3 position)
    {
        List<BaseStructure> bases = BaseStructure.Active;
        Damageable best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < bases.Count; i++)
        {
            float distance = EdgeDistance(position, bases[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = bases[i];
            }
        }
        return best;
    }

    public static float EdgeDistance(Vector3 from, Damageable target)
    {
        Vector3 offset = target.transform.position - from;
        offset.y = 0f;
        return offset.magnitude - target.TargetRadius;
    }

    // Hurts the current target while within the stats' contactRange of its edge, at most once per contactInterval.
    private void TryContactDamage()
    {
        contactCooldown -= Time.deltaTime;
        if (stats == null || Target == null || stats.ContactDamage <= 0f || contactCooldown > 0f
            || Target.IsInvulnerable)
        {
            return;
        }

        // A player whose feet are well above or below this enemy's feet (its root) is out of bite reach: jumping, on a
        // wall top, or across a cliff (PlayerController.OutOfBiteReachFrom).
        if (Target is PlayerHealth && PlayerController.Instance != null
            && PlayerController.Instance.OutOfBiteReachFrom(FeetY))
        {
            return;
        }

        if (EdgeDistance(transform.position, Target) <= stats.ContactRange)
        {
            // Against the base: dive into the box once (BaseDive plays it and deals the damage) instead of biting.
            BaseStructure targetBase = Target as BaseStructure;
            if (targetBase != null && BaseDive.Instance != null)
            {
                BaseDive.Instance.Dive(this, targetBase);
                EnterBase();
                return;
            }
            Target.TakeDamage(stats.ContactDamage);
            contactCooldown = stats.ContactInterval;
        }
    }

    // Starts a push of `distance` units away along `direction` (XZ), easing out over `duration` seconds.
    public void Knockback(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        knockDirection = direction.normalized;
        knockDistance = distance;
        knockDuration = Mathf.Max(duration, 0.01f);
        knockRemaining = knockDuration;
    }

    // Called once per frame by EnemyMovementSystem; returns this frame's displacement and advances the push.
    public Vector2 TakeKnockbackStep(float deltaTime)
    {
        if (knockRemaining <= 0f)
        {
            return Vector2.zero;
        }

        float before = knockRemaining / knockDuration;
        knockRemaining = Mathf.Max(0f, knockRemaining - deltaTime);
        float after = knockRemaining / knockDuration;

        // quadratic ease-out: the integral of 2u over the remaining fraction sums to the full distance
        return knockDirection * (knockDistance * (before * before - after * after));
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Active.Remove(this);
    }
}
