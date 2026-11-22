/*
 * ArmorPopEffect
 * The chip burst when a rat's armor piece pops off (2026-11-20, enemy layers step 2). Scene object "ArmorPops".
 *
 * - RatArmor calls the static Play(point, tint) at the piece's centre; the burst is a pooled DebrisBurst (burstPrefab,
 *   Prefabs/ArmorPopBurst.prefab: a small copy of the enemy death burst) tinted with the piece's pop colour.
 * - Bursts come from a ComponentPool<DebrisBurst> (prewarmed, grows when empty), never Instantiate/Destroy.
 *   Without this object in the scene armor just disappears.
 * - Also plays a spark in blockedHitColor when a hit bounces off an enemy that needs lead (Damageable.HitBlocked, pots).
 */

using UnityEngine;

public class ArmorPopEffect : MonoBehaviour
{
    [SerializeField] private DebrisBurst burstPrefab;
    [SerializeField, Min(0)] private int prewarmCount = 12;
    [Tooltip("Colour of the spark when a hit bounces off an enemy that needs lead (a pot). Clear = no spark.")]
    [SerializeField] private Color blockedHitColor = new Color(0.85f, 0.87f, 0.9f);
    [Tooltip("Height above the enemy's root where the bounce spark plays.")]
    [SerializeField] private float blockedHitHeight = -0.4f;

    private static ArmorPopEffect instance;
    private ComponentPool<DebrisBurst> pool;

    private void Awake()
    {
        instance = this;
        Transform root = new GameObject("Armor Pop Bursts").transform;
        pool = new ComponentPool<DebrisBurst>(burstPrefab, root, prewarmCount);
    }

    private void OnEnable()
    {
        Damageable.HitBlocked += HandleHitBlocked;
    }

    private void OnDisable()
    {
        Damageable.HitBlocked -= HandleHitBlocked;
    }

    // A non-lead hit bounced off a pot: a small "ping" of sparks.
    private void HandleHitBlocked(Damageable target)
    {
        if (blockedHitColor.a <= 0f || target == null)
        {
            return;
        }
        Play(target.transform.position + Vector3.up * blockedHitHeight, blockedHitColor);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void Play(Vector3 point, Color tint)
    {
        if (instance == null || instance.pool == null)
        {
            return;
        }
        DebrisBurst burst = instance.pool.Get(point, Quaternion.identity);
        burst.Play(instance.pool, tint);
    }
}
