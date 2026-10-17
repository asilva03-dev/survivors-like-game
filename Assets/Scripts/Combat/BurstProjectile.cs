/*
 * BurstProjectile
 * Pooled projectile of the BurstR weapon. Flies straight at a fixed speed for its lifetime, spinning its
 * model on its own axis, with a trail. It never stops on a hit: it damages every target it touches (each
 * target once per projectile), reports each hit point to its weapon (which plays the hit burst), and
 * returns to the pool when the lifetime ends.
 *
 * - Never created or destroyed at runtime: the weapon's ComponentPool owns it.
 * - The model lives under the "Model" child; SetModel() puts an instance of any model there and scales it so
 *   its largest dimension is modelSize, centred on the holder's pivot so the spin is in place. Done once on
 *   the weapon's pool template.
 * - Hits are a distance check from each target to the segment travelled this frame (flat, XZ), so fast
 *   projectiles never tunnel. Other colliders are ignored.
 * - Damage comes from the firing weapon's stats (WeaponStats.DamageFor) for targets its mask allows.
 * - Needs a TrailRenderer (cleared on launch so a reused projectile does not streak).
 */

using System.Collections.Generic;
using UnityEngine;

public class BurstProjectile : MonoBehaviour
{
    [SerializeField] private Transform modelHolder;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private Vector3 spinAxis = new Vector3(1f, 0.6f, 0.3f);

    private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
    private ComponentPool<BurstProjectile> pool;
    private WeaponStats stats;
    private System.Action<Vector3> onHit;
    private Vector3 direction;
    private float speed;
    private float lifetime;
    private float hitRadius;
    private float spinSpeed;
    private float age;

    public void SetModel(GameObject model, float size)
    {
        if (model == null)
        {
            return;
        }

        GameObject instance = Instantiate(model, modelHolder);
        ModelFit.Fit(modelHolder, instance.transform, size);
    }

    public void Launch(ComponentPool<BurstProjectile> owningPool, Vector3 travelDirection, float travelSpeed,
        float maxLifetime, float touchRadius, float spinDegreesPerSecond, WeaponStats weaponStats,
        System.Action<Vector3> hitCallback)
    {
        pool = owningPool;
        direction = travelDirection.normalized;
        speed = travelSpeed;
        lifetime = maxLifetime;
        hitRadius = touchRadius;
        spinSpeed = spinDegreesPerSecond;
        stats = weaponStats;
        onHit = hitCallback;
        age = 0f;
        hitTargets.Clear();

        transform.rotation = Quaternion.LookRotation(direction);
        modelHolder.localRotation = Quaternion.identity;

        if (trail != null)
        {
            trail.Clear();
        }
    }

    private void Update()
    {
        Vector3 from = transform.position;
        Vector3 to = from + direction * (speed * Time.deltaTime);
        transform.position = to;
        modelHolder.Rotate(spinAxis.normalized, spinSpeed * Time.deltaTime, Space.Self);

        HitAlong(from, to);

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            pool.Release(this);
        }
    }

    private void HitAlong(Vector3 from, Vector3 to)
    {
        Vector2 a = new Vector2(from.x, from.z);
        Vector2 segment = new Vector2(to.x, to.z) - a;
        float sqrLength = segment.sqrMagnitude;
        float sqrRadius = hitRadius * hitRadius;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!stats.CanTarget(candidate.Category) || hitTargets.Contains(candidate))
            {
                continue;
            }

            Vector2 point = new Vector2(candidate.transform.position.x, candidate.transform.position.z);
            float t = sqrLength > 0.000001f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / sqrLength) : 0f;
            if ((point - (a + segment * t)).sqrMagnitude > sqrRadius)
            {
                continue;
            }

            hitTargets.Add(candidate);

            float amount = stats.DamageFor(candidate.Category);
            if (amount > 0f)
            {
                candidate.TakeDamage(amount);
            }

            onHit?.Invoke(Vector3.Lerp(from, to, t));
        }
    }
}
