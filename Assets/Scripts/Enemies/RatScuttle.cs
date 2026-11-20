/*
 * RatScuttle
 * A procedural scuttle for the rats while they move (2026-11-20, user: they just slid along). On the enemy prefab's
 * root; it only animates its `model` (the RatModel child under Visual), so EnemyMovementSystem keeps turning the
 * Visual and BaseDive's copies (made from the Visual) stay at the rest pose.
 *
 * - Speed = the enemy's measured flat Damageable.Velocity. The step cycle advances with the distance moved
 *   (stepsPerUnit), so slow rats step slowly and fast ones scurry; amount fades in from 0 at a stop to 1 at fullSpeed.
 * - Each step: a hop of bobHeight (world units, |sin| so it bounces on every step), a side-to-side roll of rollAngle
 *   around the feet (a waddle), a nose dip of pitchAngle, and a small squash on each landing (squash). All scaled by
 *   amount, so a standing rat (a ranged rat shooting, a knocked-back rat settling) is back at rest.
 * - Each rat starts at a random point of the cycle so a crowd doesn't move in step. The pose resets when disabled
 *   (pooling). Rats off-screen (model renderer not visible) or standing still at rest skip the pose work (perf).
 */

using UnityEngine;

public class RatScuttle : MonoBehaviour
{
    [Tooltip("The model child to animate (Visual > RatModel).")]
    [SerializeField] private Transform model;
    [Tooltip("Speed (units/s) at which the scuttle is at full strength.")]
    [SerializeField, Min(0.01f)] private float fullSpeed = 1.5f;
    [Tooltip("Steps per unit walked (two hops per step cycle).")]
    [SerializeField, Min(0.1f)] private float stepsPerUnit = 2.2f;
    [Tooltip("Height of each hop (world units).")]
    [SerializeField, Min(0f)] private float bobHeight = 0.06f;
    [Tooltip("Side-to-side roll around the feet (degrees).")]
    [SerializeField, Min(0f)] private float rollAngle = 7f;
    [Tooltip("Nose dip on each hop (degrees, + = nose down).")]
    [SerializeField] private float pitchAngle = 4f;
    [Tooltip("Squash on each landing (0-1 of the height).")]
    [SerializeField, Range(0f, 0.5f)] private float squash = 0.08f;

    private Damageable body;
    private Renderer modelRenderer;
    private bool atRest = true;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 restScale;
    private bool cached;
    private float phase;

    private void Awake()
    {
        body = GetComponent<Damageable>();
        Cache();
    }

    private void Cache()
    {
        if (cached || model == null)
        {
            return;
        }
        restPosition = model.localPosition;
        restRotation = model.localRotation;
        restScale = model.localScale;
        modelRenderer = model.GetComponent<Renderer>();
        cached = true;
    }

    private void OnEnable()
    {
        phase = Random.value * Mathf.PI * 2f;
        ResetPose();
    }

    private void OnDisable()
    {
        ResetPose();
    }

    private void ResetPose()
    {
        if (!cached)
        {
            return;
        }
        model.localPosition = restPosition;
        model.localRotation = restRotation;
        model.localScale = restScale;
        atRest = true;
    }

    private void LateUpdate()
    {
        if (!cached || body == null)
        {
            return;
        }
        Vector3 velocity = body.Velocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        float amount = Mathf.Clamp01(speed / fullSpeed);
        amount = amount * amount * (3f - 2f * amount);

        phase += speed * Time.deltaTime * stepsPerUnit * Mathf.PI;
        if (phase > Mathf.PI * 200f)
        {
            phase -= Mathf.PI * 200f;
        }

        // Nothing to show: off-screen, or standing still and already at rest.
        if (modelRenderer != null && !modelRenderer.isVisible)
        {
            return;
        }
        if (amount < 0.001f)
        {
            if (!atRest)
            {
                ResetPose();
            }
            return;
        }
        atRest = false;

        float hop = Mathf.Abs(Mathf.Sin(phase));
        // World units -> the model's parent space (the Visual is scaled).
        float parentScale = model.parent != null ? model.parent.lossyScale.y : 1f;
        float lift = bobHeight * hop * amount / Mathf.Max(parentScale, 0.0001f);
        float roll = rollAngle * Mathf.Sin(phase) * amount;
        float pitch = pitchAngle * (hop - 0.5f) * amount;
        float landing = (1f - hop) * (1f - hop);
        float squashNow = squash * landing * amount;

        model.localPosition = restPosition + Vector3.up * lift;
        model.localRotation = restRotation * Quaternion.Euler(pitch, 0f, roll);
        model.localScale = Vector3.Scale(restScale, new Vector3(1f + squashNow * 0.5f, 1f - squashNow, 1f + squashNow * 0.5f));
    }
}
