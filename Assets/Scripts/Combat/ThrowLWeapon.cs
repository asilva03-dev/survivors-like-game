/*
 * ThrowLWeapon
 * Throw weapon (molotov): where a throwable lands it explodes in a particle burst and leaves a
 * LingerPuddle that keeps damaging whatever stands in it.
 *
 * - Targeting, throwing, prediction and the multi-projectile pattern are all ThrowWeapon; this class only
 *   adds the puddle pool and the landing.
 * - Prefab refs: the puddle prefab is set on the weapon prefab (throwable and burst live on ThrowWeapon).
 */

using UnityEngine;

public class ThrowLWeapon : ThrowWeapon<ThrowLStats>
{
    [SerializeField] private LingerPuddle puddlePrefab;

    private ComponentPool<LingerPuddle> puddlePool;

    protected override void Awake()
    {
        base.Awake();
        puddlePool = new ComponentPool<LingerPuddle>(puddlePrefab, PoolRoot, Mathf.Max(2, Stats.ProjectileCount));
    }

    protected override void Land(Vector3 point)
    {
        PlayBurst(point);

        LingerPuddle puddle = puddlePool.Get(point, Quaternion.identity);
        puddle.Begin(puddlePool, Stats, point);
    }
}
