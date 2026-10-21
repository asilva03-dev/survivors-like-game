/*
 * PlayerHop
 * On the Player: purely visual hopping while moving. The model hops in a loop, stretches tall in the air and
 * squashes briefly on each landing, then goes straight into the next hop, so it never seems to slide.
 *
 * - hop is a container between Player/Model and the cat (Player/Model/Hop/CatModel) whose pivot sits on the
 *   ground under the player, so squash/stretch scales from the feet. This script owns its local position (rest +
 *   hop height) and local scale; the rotation stays 0 (PlayerFacing turns the cat inside it).
 * - Cycle: Air (airTime seconds, parabola up to hopHeight) > Land (landTime seconds, squash) > Air again while
 *   moving. Stopping mid-hop finishes the hop and its landing, then rests. Moving = the Rigidbody's flat speed
 *   above moveThreshold (dash, sprint and the axe lunge count too).
 * - Faster than referenceSpeed (sprint) speeds the whole cycle up, at most maxRateMultiplier times.
 * - Stretch is strongest at take-off and just before landing (vertical speed), zero at the top of the hop.
 *   Squash dips and recovers once over landTime. Width changes as 1/sqrt(height scale) so the volume looks kept.
 * - Only visual: the collider, Rigidbody and everything targeting the player stay on the root.
 */

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerHop : MonoBehaviour
{
    private enum Phase { Rest, Air, Land }

    [SerializeField] private Transform hop;
    [SerializeField, Min(0f)] private float hopHeight = 0.35f;
    [SerializeField, Min(0.05f)] private float airTime = 0.28f;
    [SerializeField, Min(0.01f)] private float landTime = 0.07f;
    [SerializeField, Range(0f, 1f)] private float stretch = 0.18f;
    [SerializeField, Range(0f, 0.9f)] private float squash = 0.25f;
    [SerializeField, Min(0f)] private float moveThreshold = 0.5f;
    [SerializeField, Min(0.1f)] private float referenceSpeed = 5f;
    [SerializeField, Min(1f)] private float maxRateMultiplier = 1.5f;

    private Rigidbody body;
    private Vector3 restPosition;
    private Phase phase;
    private float progress;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (hop != null)
        {
            restPosition = hop.localPosition;
        }
    }

    private void OnDisable()
    {
        phase = Phase.Rest;
        Apply(0f, 1f);
    }

    private void LateUpdate()
    {
        if (hop == null)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        bool moving = speed > moveThreshold;
        float rate = Mathf.Clamp(speed / referenceSpeed, 1f, maxRateMultiplier);
        float dt = Time.deltaTime;

        if (phase == Phase.Rest)
        {
            if (!moving)
            {
                Apply(0f, 1f);
                return;
            }

            phase = Phase.Air;
            progress = 0f;
        }

        if (phase == Phase.Air)
        {
            progress += dt * rate / airTime;
            if (progress < 1f)
            {
                float height = hopHeight * 4f * progress * (1f - progress);
                Apply(height, 1f + stretch * Mathf.Abs(1f - 2f * progress));
                return;
            }

            phase = Phase.Land;
            progress = 0f;
        }

        progress += dt * rate / landTime;
        if (progress < 1f)
        {
            Apply(0f, 1f - squash * Mathf.Sin(progress * Mathf.PI));
            return;
        }

        phase = moving ? Phase.Air : Phase.Rest;
        progress = 0f;
        Apply(0f, 1f);
    }

    private void Apply(float height, float scaleY)
    {
        float width = 1f / Mathf.Sqrt(scaleY);
        hop.localPosition = restPosition + Vector3.up * height;
        hop.localScale = new Vector3(width, scaleY, width);
    }
}
