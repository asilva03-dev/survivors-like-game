/*
 * ThrowLStats
 * Stats asset for a lobbed throwable that leaves a lingering damage puddle (molotov-style).
 * Create via Assets > Create > Weapons > ThrowL Stats.
 *
 * - Aiming (range, ring position, turn speed...) comes from AimedWeaponStats; attackInterval,
 *   damage ("Enemy Damage"), resourceDamageMultiplier and targets come from WeaponStats.
 * - throwArc is how high (units above the straight line) the throwable peaks; flightTime is how long
 *   it is in the air. It rises and falls in one continuous parabola from the weapon to the target's
 *   position at throw time.
 * - predictionStrength is how much the throw leads a moving target: 0 lands on where it is now, 1
 *   lands where it will be when the throwable arrives (target velocity x flightTime, never past the
 *   owner). Below 1 hedges against enemies changing direction.
 * - radius is the size of the explosion circle / puddle on the ground; lifetime is how long (seconds)
 *   the puddle stays; lingerDamageInterval is the seconds between damage ticks while it is there
 *   (the first tick lands with the explosion). Each tick deals the weapon's damage to every target
 *   inside (enemies take damage, resources take damage x resourceDamageMultiplier rounded up).
 * - throwableModel is any model/prefab; it is scaled to the Throwable prefab's model size, so a new
 *   throwable is just a new model here. lingerColor tints the puddle (alpha = opacity),
 *   explosionColor tints the landing particle burst.
 * - Edits apply live to puddles in Play mode; lifetime applies to puddles created afterwards.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/ThrowL Stats", fileName = "ThrowL")]
public class ThrowLStats : AimedWeaponStats
{
    [SerializeField, Min(0f)] private float throwArc = 3f;
    [SerializeField, Min(0.05f)] private float flightTime = 0.7f;
    [SerializeField, Range(0f, 1f)] private float predictionStrength = 0.8f;
    [SerializeField, Min(0.1f)] private float radius = 2.5f;
    [SerializeField, Min(0.1f)] private float lifetime = 4f;
    [SerializeField, Min(0.05f)] private float lingerDamageInterval = 0.5f;
    [SerializeField] private GameObject throwableModel;
    [SerializeField] private Color lingerColor = new Color(1f, 0.4f, 0.05f, 0.6f);
    [SerializeField] private Color explosionColor = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField, Min(0)] private int prewarmCount = 4;

    public float ThrowArc => throwArc;
    public float FlightTime => flightTime;
    public float PredictionStrength => predictionStrength;
    public float Radius => radius;
    public float Lifetime => lifetime;
    public float LingerDamageInterval => lingerDamageInterval;
    public GameObject ThrowableModel => throwableModel;
    public Color LingerColor => lingerColor;
    public Color ExplosionColor => explosionColor;
    public int PrewarmCount => prewarmCount;
}
