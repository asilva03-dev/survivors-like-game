/*
 * WheelOStats
 * Stats asset for a wheel weapon: objects that orbit the owner on a ring and damage whatever they touch.
 * Create via Assets > Create > Weapons > WheelO Stats.
 *
 * - projectileCount is how many objects sit on the wheel. They take spots around the ring: the first four
 *   at north, east, south, west, the next four in between those (NE, SE, SW, NW), the next eight in
 *   between again, and so on. North is world +Z, rotation is clockwise seen from above.
 * - orbitRadius is the ring size (also the weapon's Range); orbitHeight is how high above the owner's
 *   position the objects travel; rotateSpeed is degrees per second (negative = counter-clockwise). The
 *   wheel keeps turning while objects are away, so they return to their spot on the wheel.
 * - duration is how long (seconds) the objects stay out; cooldown is how long they stay away afterwards.
 *   All objects share the same cycle (they start out together).
 * - perEnemyCooldown: one object can hit the same target only once per this many seconds. Each object
 *   keeps its own list, so two objects can both hit the same enemy.
 * - hitRadius is how close (units, flat) an object's centre must be to a target's centre to hit it, so it
 *   includes the target's own size.
 * - objectModel is any model/prefab (a plain sphere when empty), scaled so its largest dimension is
 *   objectSize. damage, resourceDamageMultiplier and targets come from WeaponStats; attackInterval is
 *   unused by this weapon.
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/WheelO Stats", fileName = "WheelO")]
public class WheelOStats : WeaponStats
{
    [SerializeField, Min(1)] private int projectileCount = 4;
    [SerializeField, Min(0.1f)] private float orbitRadius = 2.5f;
    [SerializeField, Min(0f)] private float orbitHeight = 1f;
    [SerializeField] private float rotateSpeed = 180f;
    [SerializeField, Min(0.1f)] private float duration = 6f;
    [SerializeField, Min(0f)] private float cooldown = 3f;
    [SerializeField, Min(0f)] private float perEnemyCooldown = 0.5f;
    [SerializeField, Min(0.05f)] private float hitRadius = 0.9f;
    [SerializeField] private GameObject objectModel;
    [SerializeField, Min(0.05f)] private float objectSize = 0.8f;

    public override float Range => orbitRadius;
    public int ProjectileCount => projectileCount;
    public float OrbitRadius => orbitRadius;
    public float OrbitHeight => orbitHeight;
    public float RotateSpeed => rotateSpeed;
    public float Duration => duration;
    public float Cooldown => cooldown;
    public float PerEnemyCooldown => perEnemyCooldown;
    public float HitRadius => hitRadius;
    public GameObject ObjectModel => objectModel;
    public float ObjectSize => objectSize;
}
