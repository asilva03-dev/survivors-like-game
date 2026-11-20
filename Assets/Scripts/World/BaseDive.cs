/*
 * BaseDive
 * Enemies don't bite the base any more (2026-11-20, user): a rat that reaches the box dives into it, hits it once and
 * is gone. On scene object "BaseDive".
 *
 * - Enemy (TryContactDamage) calls Dive(enemy, base) when a base-bound enemy reaches the base's edge, then leaves the
 *   game at once (Enemy.EnterBase: no death effect, no drops, no wave refund), so turrets and weapons stop seeing it.
 * - What you see is a pooled copy of the enemy's Visual (the model only, no scripts or collider) at the enemy's pose:
 *   it crouches for windUpTime (squash), leaps in an arc (leapHeight above the higher end) over leapTime to just
 *   inside the box's open top (the BoxCollider's top + entryDepth, a small random spread), stretching on take-off,
 *   tipping nose-down and shrinking to entryScale as it goes in.
 * - On landing: the base takes the enemy's EnemyStats.baseHitDamage (damage number + white flash come from
 *   Damageable / HitFlash as usual), BaseHitReaction.OnEnemyHit makes the box jolt and its flaps flutter (dives landing
 *   together add up), and a pooled cardboard-scrap DebrisBurst (scrapsPrefab) pops out of the top.
 * - Copies come from one ComponentPool per enemy type (EnemyStats), the rat's prewarmed from ratPrefab; bursts from
 *   their own pool. Never Instantiate/Destroy per dive (a pool only grows when empty).
 * - Without this object in the scene, enemies bite the base as before.
 */

using System.Collections.Generic;
using UnityEngine;

public class BaseDive : MonoBehaviour
{
    private class DiveState
    {
        public Transform diver;
        public ComponentPool<Transform> pool;
        public BaseStructure target;
        public Vector3 start;
        public Vector3 end;
        public Vector3 scale;
        public Quaternion startRotation;
        public float damage;
        public float time;
    }

    public static BaseDive Instance { get; private set; }

    [Tooltip("Enemy prefab whose Visual is prewarmed as dive copies (the basic rat).")]
    [SerializeField] private Enemy ratPrefab;
    [SerializeField, Min(0)] private int prewarmCount = 12;
    [Tooltip("Cardboard scraps burst played at the box when a dive lands.")]
    [SerializeField] private DebrisBurst scrapsPrefab;
    [SerializeField, Min(0)] private int scrapsPrewarm = 6;

    [Header("Leap")]
    [Tooltip("Seconds of crouching before the leap.")]
    [SerializeField, Min(0f)] private float windUpTime = 0.12f;
    [Tooltip("How much the crouch squashes the model (0-1).")]
    [SerializeField, Range(0f, 0.8f)] private float crouchSquash = 0.3f;
    [Tooltip("Seconds in the air.")]
    [SerializeField, Min(0.05f)] private float leapTime = 0.38f;
    [Tooltip("Arc height above the higher end of the leap.")]
    [SerializeField, Min(0f)] private float leapHeight = 1.4f;
    [Tooltip("Stretch along the leap at take-off (0-1).")]
    [SerializeField, Range(0f, 0.8f)] private float leapStretch = 0.2f;
    [Tooltip("Nose pitch at take-off (negative = nose up) and when it goes in (degrees).")]
    [SerializeField] private Vector2 pitch = new Vector2(-25f, 60f);
    [Tooltip("Scale (of its own) the copy shrinks to as it enters the box.")]
    [SerializeField, Range(0.05f, 1f)] private float entryScale = 0.5f;
    [Tooltip("Where the leap ends relative to the box's top (negative = inside the box).")]
    [SerializeField] private float entryDepth = -0.4f;
    [Tooltip("Random spread of the landing spot around the box's centre (units).")]
    [SerializeField, Min(0f)] private float entrySpread = 0.35f;

    private readonly Dictionary<EnemyStats, ComponentPool<Transform>> pools = new Dictionary<EnemyStats, ComponentPool<Transform>>();
    private readonly List<DiveState> active = new List<DiveState>();
    private readonly Stack<DiveState> spare = new Stack<DiveState>();
    private ComponentPool<DebrisBurst> scrapsPool;
    private Transform root;

    /// <summary>Dives in the air right now (testing).</summary>
    public int ActiveDives => active.Count;
    /// <summary>Dives that reached the box since Start (testing).</summary>
    public int Landed { get; private set; }

    private void Awake()
    {
        Instance = this;
        root = new GameObject("Divers").transform;
        root.SetParent(transform, false);
        if (ratPrefab != null && ratPrefab.Visual != null)
        {
            pools[ratPrefab.Stats] = new ComponentPool<Transform>(ratPrefab.Visual, root, prewarmCount);
        }
        if (scrapsPrefab != null)
        {
            scrapsPool = new ComponentPool<DebrisBurst>(scrapsPrefab, root, scrapsPrewarm);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Starts a dive from the enemy's current pose into the base. Call before the enemy leaves.</summary>
    public void Dive(Enemy enemy, BaseStructure target)
    {
        Transform visual = enemy.Visual != null ? enemy.Visual : enemy.transform;
        ComponentPool<Transform> pool = PoolFor(enemy, visual);

        DiveState dive = spare.Count > 0 ? spare.Pop() : new DiveState();
        dive.pool = pool;
        dive.diver = pool.Get(visual.position, visual.rotation);
        dive.scale = visual.lossyScale;
        dive.diver.localScale = dive.scale;
        dive.start = visual.position;
        dive.startRotation = visual.rotation;
        dive.target = target;
        dive.damage = enemy.Stats != null ? enemy.Stats.BaseHitDamage : 0f;
        dive.time = 0f;

        Vector2 spread = Random.insideUnitCircle * entrySpread;
        Vector3 centre = target.transform.position;
        dive.end = new Vector3(centre.x + spread.x, BoxTop(target) + entryDepth, centre.z + spread.y);
        active.Add(dive);
    }

    private ComponentPool<Transform> PoolFor(Enemy enemy, Transform visual)
    {
        ComponentPool<Transform> pool;
        EnemyStats key = enemy.Stats;
        if (key != null && pools.TryGetValue(key, out pool))
        {
            return pool;
        }
        // An enemy type without a prewarmed pool: copy its own visual (once per type).
        pool = new ComponentPool<Transform>(visual, root, 0);
        if (key != null)
        {
            pools[key] = pool;
        }
        return pool;
    }

    private static float BoxTop(BaseStructure target)
    {
        Collider box = target.GetComponent<Collider>();
        return box != null ? box.bounds.max.y : target.transform.position.y + 2f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            DiveState dive = active[i];
            dive.time += dt;
            if (dive.target == null)
            {
                Finish(i, false);
                continue;
            }

            if (dive.time < windUpTime)
            {
                float c = crouchSquash * Mathf.Sin(dive.time / windUpTime * Mathf.PI * 0.5f);
                dive.diver.position = dive.start;
                dive.diver.rotation = dive.startRotation;
                dive.diver.localScale = Vector3.Scale(dive.scale, new Vector3(1f + c * 0.5f, 1f - c, 1f + c * 0.5f));
                continue;
            }

            float u = (dive.time - windUpTime) / leapTime;
            if (u >= 1f)
            {
                Finish(i, true);
                continue;
            }

            float arc = leapHeight + Mathf.Max(0f, dive.end.y - dive.start.y);
            Vector3 line = Vector3.Lerp(dive.start, dive.end, u);
            dive.diver.position = line + Vector3.up * (arc * 4f * u * (1f - u));

            Vector3 flat = dive.end - dive.start;
            flat.y = 0f;
            Quaternion facing = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : dive.startRotation;
            dive.diver.rotation = facing * Quaternion.Euler(Mathf.Lerp(pitch.x, pitch.y, u), 0f, 0f);

            // Stretch along the leap at take-off, then shrink into the box over the last third.
            float stretch = leapStretch * (1f - u);
            float shrink = u < 0.66f ? 1f : Mathf.Lerp(1f, entryScale, (u - 0.66f) / 0.34f);
            dive.diver.localScale = dive.scale * shrink
                + Vector3.Scale(dive.scale * shrink, new Vector3(-stretch * 0.5f, -stretch * 0.5f, stretch));
        }
    }

    private void Finish(int index, bool landed)
    {
        DiveState dive = active[index];
        active.RemoveAt(index);
        dive.diver.localScale = dive.scale;
        dive.pool.Release(dive.diver);

        if (landed && dive.target != null)
        {
            Landed++;
            if (dive.damage > 0f)
            {
                dive.target.TakeDamage(dive.damage);
            }
            BaseHitReaction reaction = dive.target.GetComponent<BaseHitReaction>();
            if (reaction != null)
            {
                reaction.OnEnemyHit();
            }
            if (scrapsPool != null)
            {
                scrapsPool.Get(dive.target.transform.position, Quaternion.identity).Play(scrapsPool);
            }
        }
        dive.diver = null;
        dive.pool = null;
        dive.target = null;
        spare.Push(dive);
    }
}
