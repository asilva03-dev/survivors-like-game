/*
 * ArrowVolleyStats
 * Numbers for the Angel Cat's arrow volley (tower 6, 2026-11-25, user's design): where one of its arrows ends, a volley
 * of arrows appears above it and rains down. Damage, targets and lead come from WeaponStats (attackInterval unused).
 *
 * - radius: flat distance from the arrow's spot within which rats are hit (plus their own Damageable.TargetRadius);
 *   every rat inside is hit once per volley (no pierce cap). Range = radius.
 * - detectsShadow: like the guns' flag (ShadowSight).
 * - The look: arrowCount arrows spread over the circle, spawnHeight above the ground; after delay they appear over
 *   appearTime, drop over fallTime (the damage lands with them), stay stickTime, then shrink over shrinkTime.
 * - The tower's own arrow (stuck in the rat, or in the ground where it killed the rat) stays for the whole volley
 *   (TotalTime) and shrinks with it.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/Arrow Volley Stats", fileName = "ArrowVolleyStats")]
public class ArrowVolleyStats : WeaponStats
{
    [SerializeField, Min(0.1f)] private float radius = 1.8f;
    [Tooltip("Can hit shadow rats (else only those in a RevealLight).")]
    [SerializeField] private bool detectsShadow;
    [SerializeField, Range(1, 24)] private int arrowCount = 8;
    [SerializeField, Min(0.5f)] private float spawnHeight = 2.5f;
    [SerializeField, Min(0f)] private float delay = 0.25f;
    [SerializeField, Min(0.01f)] private float appearTime = 0.12f;
    [SerializeField, Min(0.01f)] private float fallTime = 0.18f;
    [SerializeField, Min(0f)] private float stickTime = 0.5f;
    [SerializeField, Min(0.01f)] private float shrinkTime = 0.15f;

    public override float Range => radius;
    public float Radius => radius;
    public bool DetectsShadow => detectsShadow;
    public int ArrowCount => arrowCount;
    public float SpawnHeight => spawnHeight;
    public float Delay => delay;
    public float AppearTime => appearTime;
    public float FallTime => fallTime;
    public float StickTime => stickTime;
    public float ShrinkTime => shrinkTime;
    /// <summary>Seconds from the arrow's hit to the volley's end, shrink not included.</summary>
    public float TotalTime => delay + appearTime + fallTime + stickTime;
}
