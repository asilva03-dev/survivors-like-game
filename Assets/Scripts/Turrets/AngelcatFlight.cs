/*
 * AngelcatFlight
 * On the Angel Cat tower (tower 6, 2026-11-25, user's design): once placed, the cat sits on its cloud for a moment,
 * takes off and then flies a circle around the base box forever. The cloud stays on its tiles.
 *
 * - It moves the tower's AimPoint (the TurretGun's owner); the gun puts itself on its owner every frame, so the cat,
 *   its bow and its range all travel with it. Runs early (execution order -50) so the gun reads this frame's spot.
 * - The circle: centred on the base box (BaseLight.BasePosition), radius = the light's current Radius minus lightInset
 *   (user: "a little bit smaller than the light radius"), followed live as the light grows; at flightHeight.
 * - Takeoff: after takeoffDelay it rises and glides along a curve from the perch to the circle point at its own angle
 *   from the base, taking at least takeoffTime (longer if the circle is far: never faster than flightSpeed x 2).
 * - Flying: the angle advances at flightSpeed (units per second along the circle); clockwise or not. It bobs up and
 *   down by bobHeight once every bobPeriod seconds (user, 2026-11-25), starting at 0 so the takeoff joins smoothly.
 * - While flying with no target the gun faces the flight direction (TurretGun.IdleDirection, unset on other towers).
 * - All numbers are placeholders (user: speed and height tuned later).
 */

using UnityEngine;

[DefaultExecutionOrder(-50)]
public class AngelcatFlight : MonoBehaviour
{
    [Tooltip("The point that flies (the TurretGun's owner).")]
    [SerializeField] private Transform flyer;
    [SerializeField] private TurretGun gun;
    [Tooltip("Height of the flying point above the ground.")]
    [SerializeField, Min(0f)] private float flightHeight = 4.5f;
    [Tooltip("Speed along the circle, units per second.")]
    [SerializeField, Min(0.1f)] private float flightSpeed = 3f;
    [Tooltip("The circle's radius = the light's radius minus this.")]
    [SerializeField, Min(0f)] private float lightInset = 2.5f;
    [Tooltip("Smallest circle radius (a tiny light).")]
    [SerializeField, Min(0.5f)] private float minRadius = 4f;
    [SerializeField] private bool clockwise;
    [Tooltip("Seconds sitting on the cloud after being placed.")]
    [SerializeField, Min(0f)] private float takeoffDelay = 0.6f;
    [Tooltip("Shortest takeoff, seconds from the cloud to the circle.")]
    [SerializeField, Min(0.1f)] private float takeoffTime = 1.5f;
    [Tooltip("While flying: how far it bobs up and down from flightHeight.")]
    [SerializeField, Min(0f)] private float bobHeight = 0.25f;
    [Tooltip("While flying: seconds for one full bob (up and back down).")]
    [SerializeField, Min(0.1f)] private float bobPeriod = 2.5f;

    private enum State { Perched, TakingOff, Flying }

    private State state = State.Perched;
    private float timer;
    private float angle;
    private float takeoffDuration;
    private Vector3 perch;

    public bool IsFlying => state == State.Flying;
    /// <summary>0 while perched, rising to 1 through the takeoff, 1 while flying (for the animator).</summary>
    public float Airborne => state == State.Flying ? 1f : state == State.TakingOff ? Mathf.Clamp01(timer / Mathf.Max(0.01f, takeoffDuration)) : 0f;
    public float FlightHeight => flightHeight;

    private void Start()
    {
        if (flyer != null)
        {
            perch = flyer.position;
        }
    }

    private void Update()
    {
        BaseLight light = BaseLight.Instance;
        if (flyer == null || light == null)
        {
            return;
        }

        timer += Time.deltaTime;
        Vector3 centre = light.BasePosition;
        float radius = Mathf.Max(minRadius, light.Radius - lightInset);

        if (state == State.Perched)
        {
            if (timer < takeoffDelay)
            {
                return;
            }

            Vector3 away = perch - centre;
            away.y = 0f;
            angle = away.sqrMagnitude > 0.0001f ? Mathf.Atan2(away.z, away.x) : 0f;
            float distance = Vector3.Distance(perch, CirclePoint(centre, radius));
            takeoffDuration = Mathf.Max(takeoffTime, distance / (flightSpeed * 2f));
            timer = 0f;
            state = State.TakingOff;
        }

        if (state == State.TakingOff)
        {
            float t = Mathf.Clamp01(timer / takeoffDuration);
            float s = t * t * (3f - 2f * t);
            Vector3 end = CirclePoint(centre, radius);
            Vector3 control = new Vector3(perch.x, flightHeight + 0.5f, perch.z);
            Vector3 a = Vector3.Lerp(perch, control, s);
            Vector3 b = Vector3.Lerp(control, end, s);
            flyer.position = Vector3.Lerp(a, b, s);
            SetIdleDirection(b - a);
            if (t >= 1f)
            {
                state = State.Flying;
                timer = 0f;
            }
            return;
        }

        float step = flightSpeed / radius * Time.deltaTime;
        angle += clockwise ? -step : step;
        flyer.position = CirclePoint(centre, radius) + Vector3.up * (Mathf.Sin(timer * 2f * Mathf.PI / bobPeriod) * bobHeight);
        Vector3 tangent = clockwise ? new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) : new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        SetIdleDirection(tangent);
    }

    private Vector3 CirclePoint(Vector3 centre, float radius)
    {
        return new Vector3(centre.x + Mathf.Cos(angle) * radius, flightHeight, centre.z + Mathf.Sin(angle) * radius);
    }

    private void SetIdleDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (gun != null && direction.sqrMagnitude > 0.0001f)
        {
            gun.IdleDirection = direction.normalized;
        }
    }
}
