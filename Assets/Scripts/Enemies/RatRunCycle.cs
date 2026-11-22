/*
 * RatRunCycle
 * The athletic rat's sprint (2026-11-21, user: a convincing, funny run so the player thinks "oh no, he's running for the
 * base"). On the prefab root; poses the runner rig under Visual > RatModel, built by RatModelBuilder:
 * Hips > Torso > ArmL / ArmR, and Hips > ThighL / ThighR > ShinL / ShinR. The other rats use RatScuttle instead.
 *
 * - The cycle advances with the distance moved (cyclesPerUnit; one cycle = two steps), so the feet keep pace with the
 *   ground at any speed; the pose fades in from a stand (amount 0) to fullSpeed (amount 1), from the measured flat
 *   Damageable.Velocity.
 * - Per cycle: the thighs swing opposite each other (thighSwing), each knee folds while its leg swings forward
 *   (kneeBend; kneeStance while on the ground), the arms pump against the legs (armSwing), the hips bounce on every step
 *   (bounce, world units), the torso leans forward (lean) and twists with the shoulders (twist).
 * - Each rat starts at a random point of the cycle. Rats off-screen (torso renderer not visible) skip the pose; the pose
 *   resets when disabled (pooling). Pose(phase, amount) sets a frame by hand (renders, tests).
 * - Vault (2026-11-21, user: a quick single-arm vault over walls): while WallHop lifts the model, the pose blends from the
 *   sprint into a speed vault by WallHop.Vault (sharpened by vaultSharpness): the hips roll so the tucked legs
 *   (vaultTuck, vaultKnee) swing out to the left (vaultSwing), the body tips onto the right arm, which points straight down
 *   onto the fence (vaultPlant), and the left arm flings up (vaultFling).
 * - Grind (2026-11-21, user: "like they are grinding the fence"): while WallHop rides a fence (WallHop.Grind), the pose
 *   blends into a surfer stance: hips (legs) turned grindTurn side-on to the rail, feet spread along it (grindStance), knees
 *   bent (grindKnee); the upper body turned back by grindTorsoTurn to face where it's going (user), crouched
 *   (grindCrouch), both arms out for balance (grindArms); wobbling side to side (grindWobble at grindWobbleSpeed).
 * - All numbers are placeholders (user tunes them).
 */

using UnityEngine;

public class RatRunCycle : MonoBehaviour
{
    [Header("Rig (Visual > RatModel > Hips ...)")]
    [SerializeField] private Transform hips;
    [SerializeField] private Transform torso;
    [SerializeField] private Transform armL;
    [SerializeField] private Transform armR;
    [SerializeField] private Transform thighL;
    [SerializeField] private Transform thighR;
    [SerializeField] private Transform shinL;
    [SerializeField] private Transform shinR;

    [Header("Run")]
    [Tooltip("Speed (units/s) at which the run is at full strength.")]
    [SerializeField, Min(0.01f)] private float fullSpeed = 2f;
    [Tooltip("Run cycles (two steps each) per unit moved.")]
    [SerializeField, Min(0.05f)] private float cyclesPerUnit = 1f;
    [Tooltip("Thigh swing to each side (degrees).")]
    [SerializeField] private float thighSwing = 55f;
    [Tooltip("Extra knee fold while a leg swings forward (degrees).")]
    [SerializeField] private float kneeBend = 95f;
    [Tooltip("Knee fold while the leg is on the ground (degrees).")]
    [SerializeField] private float kneeStance = 10f;
    [Tooltip("Arm pump to each side (degrees).")]
    [SerializeField] private float armSwing = 75f;
    [Tooltip("Hip bounce on each step (world units).")]
    [SerializeField, Min(0f)] private float bounce = 0.07f;
    [Tooltip("Forward lean of the upper body at full speed (degrees).")]
    [SerializeField] private float lean = 22f;
    [Tooltip("Shoulder twist with each step (degrees).")]
    [SerializeField] private float twist = 10f;

    [Header("Vault (over walls, from WallHop)")]
    [Tooltip("The WallHop on this rat (empty = no vault pose).")]
    [SerializeField] private WallHop hop;
    [Tooltip("How quickly the vault pose takes over as the leap rises (1 = only at the top).")]
    [SerializeField, Min(1f)] private float vaultSharpness = 1.6f;
    [Tooltip("Hips roll swinging the tucked legs out to the left (degrees).")]
    [SerializeField] private float vaultSwing = 50f;
    [Tooltip("Thighs tucked forward (degrees).")]
    [SerializeField] private float vaultTuck = 75f;
    [Tooltip("Knee fold while tucked (degrees).")]
    [SerializeField] private float vaultKnee = 110f;
    [Tooltip("Extra roll of the upper body back toward upright (degrees).")]
    [SerializeField] private float vaultBodyRoll = 15f;
    [Tooltip("Planting (right) arm: pitch from straight down (degrees, - = forward).")]
    [SerializeField] private float vaultPlant = -10f;
    [Tooltip("Free (left) arm flung up (degrees).")]
    [SerializeField] private float vaultFling = 150f;

    [Header("Grind (riding a fence, from WallHop)")]
    [Tooltip("Hips turned side-on to the rail (degrees).")]
    [SerializeField] private float grindTurn = 90f;
    [Tooltip("Upper body turned back from the hips toward where it's going (degrees; = Grind Turn faces it straight ahead).")]
    [SerializeField] private float grindTorsoTurn = 90f;
    [Tooltip("Feet spread along the rail (degrees each way).")]
    [SerializeField] private float grindStance = 24f;
    [Tooltip("Knee bend while grinding (degrees).")]
    [SerializeField] private float grindKnee = 45f;
    [Tooltip("Upper body crouch while grinding (degrees).")]
    [SerializeField] private float grindCrouch = 15f;
    [Tooltip("Arms held out for balance (degrees from hanging down).")]
    [SerializeField] private float grindArms = 80f;
    [Tooltip("Side-to-side wobble while grinding (degrees).")]
    [SerializeField] private float grindWobble = 9f;
    [Tooltip("Wobbles per second (x 2 pi).")]
    [SerializeField] private float grindWobbleSpeed = 11f;

    private Damageable body;
    private Renderer torsoRenderer;
    private Transform[] parts;
    private Vector3[] restPositions;
    private Quaternion[] restRotations;
    private bool cached;
    private bool atRest = true;
    private float phase;

    private void Awake()
    {
        body = GetComponent<Damageable>();
        Cache();
    }

    private void Cache()
    {
        if (cached || hips == null || torso == null)
        {
            return;
        }
        parts = new[] { hips, torso, armL, armR, thighL, thighR, shinL, shinR };
        restPositions = new Vector3[parts.Length];
        restRotations = new Quaternion[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null)
            {
                restPositions[i] = parts[i].localPosition;
                restRotations[i] = parts[i].localRotation;
            }
        }
        torsoRenderer = torso.GetComponent<Renderer>();
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
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null)
            {
                parts[i].localPosition = restPositions[i];
                parts[i].localRotation = restRotations[i];
            }
        }
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

        phase += speed * Time.deltaTime * cyclesPerUnit * Mathf.PI * 2f;
        if (phase > Mathf.PI * 200f)
        {
            phase -= Mathf.PI * 200f;
        }

        if (torsoRenderer != null && !torsoRenderer.isVisible)
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
        Pose(phase, amount);
    }

    /// <summary>Sets the rig to a point of the cycle (phase in radians) at a strength (0 = standing, 1 = full sprint).</summary>
    public void Pose(float cyclePhase, float amount)
    {
        Cache();
        if (!cached)
        {
            return;
        }
        atRest = false;
        float s = Mathf.Sin(cyclePhase);
        float c = Mathf.Cos(cyclePhase);

        float v = 0f;
        if (hop != null)
        {
            v = Mathf.Clamp01(hop.Vault * vaultSharpness);
            v = v * v * (3f - 2f * v);
        }

        float g = 0f;
        if (hop != null)
        {
            g = Mathf.Clamp01(hop.Grind);
            g = g * g * (3f - 2f * g);
        }
        float wobble = grindWobble * Mathf.Sin(Time.time * grindWobbleSpeed) * g;

        // Legs: negative pitch swings a leg forward. A knee folds while its leg moves forward. Vault: both tucked.
        // Grind: feet spread along the rail (roll, the hips being side-on), knees bent.
        Rotate(4, Mathf.Lerp(Mathf.Lerp(-thighSwing * s * amount, -vaultTuck, v), 0f, g), -grindStance * g);
        Rotate(5, Mathf.Lerp(Mathf.Lerp(thighSwing * s * amount, -vaultTuck, v), 0f, g), grindStance * g);
        Rotate(6, Mathf.Lerp(Mathf.Lerp((kneeStance + kneeBend * Mathf.Max(0f, c)) * amount, vaultKnee, v), grindKnee, g), 0f);
        Rotate(7, Mathf.Lerp(Mathf.Lerp((kneeStance + kneeBend * Mathf.Max(0f, -c)) * amount, vaultKnee, v), grindKnee, g), 0f);

        // Vault: the hips roll the legs out to the left, the body tips onto the right arm (which points straight down).
        float hipsRoll = -vaultSwing * v;
        float bodyRoll = vaultBodyRoll * v;

        // Arms pump against the legs; vault: left arm flung up, right arm planted; grind: both out for balance.
        Rotate(2, Mathf.Lerp(Mathf.Lerp(armSwing * s * amount, -vaultFling, v), 0f, g), -(grindArms + wobble) * g);
        Rotate(3, Mathf.Lerp(Mathf.Lerp(-armSwing * s * amount, vaultPlant, v), 0f, g),
            Mathf.Lerp(-(hipsRoll + bodyRoll), grindArms - wobble, g));

        // Hips bounce on every step (not while vaulting or grinding); the torso leans in and twists with the shoulders.
        // Grind: hips turned side-on and wobbling, torso crouched.
        float parentScale = hips.parent != null ? hips.parent.lossyScale.y : 1f;
        float bob = bounce * Mathf.Abs(s) * amount * (1f - v) * (1f - g);
        hips.localPosition = restPositions[0] + Vector3.up * (bob / Mathf.Max(parentScale, 0.0001f));
        hips.localRotation = restRotations[0] * Quaternion.Euler(0f, grindTurn * g, hipsRoll + wobble);
        torso.localRotation = restRotations[1] * Quaternion.Euler(Mathf.Lerp(Mathf.Lerp(lean * amount, 5f, v), grindCrouch, g),
            -twist * s * amount * (1f - v) * (1f - g) - grindTorsoTurn * g, bodyRoll);
    }

    private void Rotate(int index, float pitch, float roll)
    {
        if (parts[index] != null)
        {
            parts[index].localRotation = restRotations[index] * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
