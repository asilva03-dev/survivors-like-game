/*
 * PlayerController
 * Top-down 3D player movement, dash and sprint using the new Input System.
 *
 * - Requires a Rigidbody and PlayerStamina; rotation is frozen on Awake so the capsule never tips over.
 * - Movement is on the XZ plane (diagonals normalized). Vertical velocity is left to physics.
 * - Tapping Sprint (Left Shift / gamepad left stick click) performs a short dash that costs
 *   dashStaminaCost. If the button is still held when the dash ends, the player sprints:
 *   speed ramps from moveSpeed to sprintMaxSpeed over sprintTimeToMax seconds while
 *   draining sprintStaminaDrain per second. Sprint ends on release or empty stamina.
 * - Dash direction is the current input, or the last moved direction if there is none.
 * - MoveSpeedMultiplier slows walking and sprinting (set by the melee weapon while charging).
 * - Inputs are defined here with default bindings and can be edited in the inspector.
 */

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerStamina))]
public class PlayerController : MonoBehaviour
{
    private enum Mode { Normal, Dashing, Sprinting }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashStaminaCost = 50f;

    [Header("Sprint")]
    [SerializeField] private float sprintMaxSpeed = 9f;
    [SerializeField] private float sprintTimeToMax = 1.5f;
    [SerializeField] private float sprintStaminaDrain = 15f;

    [Header("Input")]
    [SerializeField] private InputAction moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
    [SerializeField] private InputAction sprintAction = new InputAction("Sprint", InputActionType.Button);

    private Rigidbody body;
    private PlayerStamina stamina;
    private Mode mode;
    private Vector3 targetVelocity;
    private Vector3 lastDirection = Vector3.forward;
    private Vector3 dashDirection;
    private float dashTimer;
    private float sprintTime;

    public bool IsBoosting => mode != Mode.Normal;

    // Scales walking and sprinting speed (not the dash); the melee weapon lowers it while charging.
    public float MoveSpeedMultiplier { get; set; } = 1f;

    private void Reset()
    {
        AddMoveBindings();
        AddSprintBindings();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        stamina = GetComponent<PlayerStamina>();
        body.freezeRotation = true;

        if (moveAction.bindings.Count == 0)
        {
            AddMoveBindings();
        }

        if (sprintAction.bindings.Count == 0)
        {
            AddSprintBindings();
        }
    }

    private void OnEnable()
    {
        moveAction.Enable();
        sprintAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        sprintAction.Disable();
    }

    private void Update()
    {
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        bool hasInput = direction.sqrMagnitude > 0.01f;
        bool held = sprintAction.IsPressed();

        if (hasInput)
        {
            lastDirection = direction.normalized;
        }

        switch (mode)
        {
            case Mode.Normal:
                if (sprintAction.WasPressedThisFrame() && stamina.TrySpend(dashStaminaCost))
                {
                    mode = Mode.Dashing;
                    dashTimer = dashDuration;
                    dashDirection = lastDirection;
                }
                break;

            case Mode.Dashing:
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    if (held && stamina.Current > 0f)
                    {
                        mode = Mode.Sprinting;
                        sprintTime = 0f;
                    }
                    else
                    {
                        mode = Mode.Normal;
                    }
                }
                break;

            case Mode.Sprinting:
                if (!held || stamina.Current <= 0f)
                {
                    mode = Mode.Normal;
                }
                else if (hasInput)
                {
                    sprintTime += Time.deltaTime;
                    stamina.Drain(sprintStaminaDrain * Time.deltaTime);
                }
                else
                {
                    sprintTime = 0f;
                }
                break;
        }

        switch (mode)
        {
            case Mode.Dashing:
                targetVelocity = dashDirection * dashSpeed;
                break;

            case Mode.Sprinting:
                float t = Mathf.Clamp01(sprintTime / Mathf.Max(sprintTimeToMax, 0.01f));
                targetVelocity = direction * (Mathf.Lerp(moveSpeed, sprintMaxSpeed, t) * MoveSpeedMultiplier);
                break;

            default:
                targetVelocity = direction * (moveSpeed * MoveSpeedMultiplier);
                break;
        }
    }

    private void FixedUpdate()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.x = targetVelocity.x;
        velocity.z = targetVelocity.z;
        body.linearVelocity = velocity;
    }

    private void AddMoveBindings()
    {
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    private void AddSprintBindings()
    {
        sprintAction.AddBinding("<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");
    }
}
