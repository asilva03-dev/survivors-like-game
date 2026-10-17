/*
 * ThrowStats
 * Shared stats for every lobbed-throwable weapon (ThrowLStats, ThrowEStats, ...). Subclasses add only what
 * happens where the throwable lands.
 *
 * - throwArc is how high (units above the straight line) the throwable peaks; flightTime is how long it is
 *   in the air. It rises and falls in one continuous parabola from the weapon to the target's position at
 *   throw time.
 * - predictionStrength is how much the throw leads a moving target: 0 lands on where it is now, 1 lands
 *   where it will be when the throwable arrives (target velocity x flightTime, never past the owner).
 *   Below 1 hedges against enemies changing direction.
 * - projectileCount is how many throwables leave per throw, all from the same point at the same time.
 *   Targeting aims at the middle of the pattern: projectile 1 lands on the intended spot and the rest are
 *   laid out around it within `spread` units (5 = middle, left, right, one nearer, one further; more fill
 *   outer rings). A smaller spread keeps the same pattern, only tighter.
 * - landingSpread is the bloom: each throwable lands at a random point within this many units of its
 *   intended spot, uniformly over the disc, so it is never perfectly accurate. 0 = exact.
 * - throwableModel is any model/prefab; it is scaled to the Throwable prefab's model size, so a new
 *   throwable is just a new model here. explosionColor tints the landing particle burst.
 * - Aiming (range, targeting flags) comes from AimedWeaponStats; attackInterval, damage, resource multiplier
 *   and targets come from WeaponStats.
 */

using UnityEngine;

public abstract class ThrowStats : AimedWeaponStats
{
    [SerializeField, Min(0f)] private float throwArc = 3f;
    [SerializeField, Min(0.05f)] private float flightTime = 0.7f;
    [SerializeField, Range(0f, 1f)] private float predictionStrength = 0.8f;
    [SerializeField, Min(1)] private int projectileCount = 1;
    [SerializeField, Min(0f)] private float spread = 2.5f;
    [SerializeField, Min(0f)] private float landingSpread = 0.8f;
    [SerializeField] private GameObject throwableModel;
    [SerializeField] private Color explosionColor = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField, Min(0)] private int prewarmCount = 4;

    public float ThrowArc => throwArc;
    public float FlightTime => flightTime;
    public float PredictionStrength => predictionStrength;
    public int ProjectileCount => projectileCount;
    public float Spread => spread;
    public float LandingSpread => landingSpread;
    public GameObject ThrowableModel => throwableModel;
    public Color ExplosionColor => explosionColor;
    public int PrewarmCount => prewarmCount;
}
