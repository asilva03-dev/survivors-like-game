/*
 * PlayerController
 * Top-down 3D player movement: walk, dash, slide and (in the base light) jump, using the new Input System.
 *
 * - Requires a Rigidbody and PlayerStamina; rotation is frozen on Awake so the capsule never tips over.
 * - Movement is on the XZ plane (diagonals normalized). Vertical velocity is left to physics (plus the jump's gravity).
 * - Dash (right mouse button / gamepad left stick click, on the ground): a short burst at dashSpeed for dashDuration
 *   that costs dashStaminaCost, along the current input (or the last moved direction). There is no sprint (removed
 *   2026-10-23); holding the button does nothing extra.
 * - Push(direction, distance, duration) shoves the player along a direction (the axe lunge): an ease-out
 *   shove that adds to the normal movement, covering `distance` units over `duration` seconds; a new push
 *   replaces the old one.
 * - MoveSpeedMultiplier slows walking (set by the melee weapon while charging).
 * - Inputs are defined here with default bindings and can be edited in the inspector.
 * - Slide (Ctrl / gamepad east, anywhere): pressed on the ground while moving faster than slideMinSpeed (in practice a
 *   dash), the player slides along the current velocity at slideSpeedKept x that speed; the speed above walking fades
 *   exponentially (slideDecayTime) and the slide curves toward the input at slideTurnSpeed. It ends on releasing Ctrl or
 *   once within slideEndMargin of walking speed, then slideCooldown. Landing from any jump or fall with Ctrl held
 *   starts a slide at once at the landing speed (no minimum speed, no cooldown). Jumping during a slide (in the light)
 *   = a leap at the slide's speed, steered like the dash jump; sliding off a ledge carries the speed through the fall
 *   the same way. No stamina cost.
 * - Light buffs (TD slice 4, only while BaseLight says the player is inside; they end the moment they count as
 *   outside, a jump already in the air finishes):
 *   - Jump (Space / gamepad south) from the ground: rises jumpHeight in jumpAirTime / 2 seconds (its own gravity while
 *     airborne, so the numbers hold; also used when walking off a wall), and falls with fallGravityMultiplier times
 *     that gravity so it doesn't float down. It clears the 1.5-tall base walls and the
 *     player can land and walk on wall tops (grounded = a short sphere cast down hits groundMask). A frictionless
 *     material on the capsule lets it slide up wall faces instead of sticking. While a jump is still rising (faster than
 *     MaxGroundedRiseSpeed) it never counts as grounded; other rising (walking/dashing/sliding up a map ramp, slice 8)
 *     does, so ramps keep dash, jump and slides working.
 *   - Dash jump: jumping during a dash keeps the dash speed until landing (a long leap, no extra cost). Air control:
 *     while in the light the leap turns toward the movement input at dashJumpTurnSpeed deg/s (speed unchanged).
 *     Plain jumps already steer like walking.
 *   - Stamina regen x lightRegenMultiplier (PlayerStamina.RegenMultiplier).
 * - FeetHeight = how far the feet are above the world ground (y 0); OutOfBiteReach (feet above biteClearHeight, in the
 *   air or on a wall top) stops enemy contact damage (Enemy). Enemy bullets still hit.
 */

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody), typeof(PlayerStamina))]
public class PlayerController : MonoBehaviour
{
    private enum Mode { Normal, Dashing, Sliding }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashStaminaCost = 50f;

    [Header("Slide (Ctrl, anywhere)")]
    [Tooltip("Flat speed needed to start a slide with a Ctrl press (walking is 5, so it needs a dash). Landing with Ctrl held ignores it.")]
    [SerializeField, Min(0f)] private float slideMinSpeed = 6f;
    [Tooltip("Share of the entry speed the slide starts with.")]
    [SerializeField, Range(0.1f, 1.5f)] private float slideSpeedKept = 1f;
    [Tooltip("Seconds for the speed above walking to fall to about a third (exponential decay). Higher = longer slides.")]
    [SerializeField, Min(0.05f)] private float slideDecayTime = 0.5f;
    [Tooltip("The slide ends once its speed is within this of walking speed.")]
    [SerializeField, Min(0.01f)] private float slideEndMargin = 0.3f;
    [Tooltip("Degrees per second the slide curves toward the movement input.")]
    [SerializeField, Min(0f)] private float slideTurnSpeed = 120f;
    [SerializeField, Min(0f)] private float slideCooldown = 0.5f;

    [Header("Light buffs (inside the base light only)")]
    [SerializeField, Min(0.1f)] private float jumpHeight = 1.8f;
    [Tooltip("Air time of the jump without the fall boost (rise = half of it). Lower = snappier.")]
    [SerializeField, Min(0.1f)] private float jumpAirTime = 0.6f;
    [Tooltip("Gravity multiplier while falling (1 = a symmetric arc). Higher = less floaty landings.")]
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.6f;
    [Tooltip("Air control: degrees per second a dash jump turns toward the movement input (0 = locked to the dash direction).")]
    [SerializeField, Min(0f)] private float dashJumpTurnSpeed = 270f;
    [SerializeField, Min(0f)] private float lightRegenMultiplier = 1.5f;
    [Tooltip("What counts as ground to stand on (the ground and the base walls).")]
    [SerializeField] private LayerMask groundMask = (1 << 0) | (1 << 9);
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.08f;
    [Tooltip("Feet higher than this above the world ground (jumping or on a wall top) are out of enemy bite reach.")]
    [SerializeField, Min(0f)] private float biteClearHeight = 0.5f;

    [Header("Input")]
    [SerializeField] private InputAction moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
    [FormerlySerializedAs("sprintAction")]
    [SerializeField] private InputAction dashAction = new InputAction("Dash", InputActionType.Button);
    [SerializeField] private InputAction jumpAction = new InputAction("Jump", InputActionType.Button);
    [SerializeField] private InputAction slideAction = new InputAction("Slide", InputActionType.Button);

    private Rigidbody body;
    private PlayerStamina stamina;
    private Mode mode;
    private Vector3 targetVelocity;
    private Vector3 lastDirection = Vector3.forward;
    private Vector3 dashDirection;
    private float dashTimer;
    private Vector3 pushDirection;
    private float pushDistance;
    private float pushDuration;
    private float pushElapsed;
    private CapsuleCollider capsule;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private bool jumpRequested;
    private bool jumpRising;
    private bool dashJump;
    private float jumpGrace;
    private bool landedThisStep;
    private float leapSpeed;
    private float slideSpeed;
    private float slideCooldownLeft;

    // Seconds after take-off during which the ground check can't count as a landing (the cast still touches the floor).
    private const float JumpGraceTime = 0.1f;
    private const float MaxGroundedRiseSpeed = 0.5f;

    public static PlayerController Instance { get; private set; }

    public bool IsBoosting => mode != Mode.Normal;
    public bool IsSliding => mode == Mode.Sliding;
    // A ground dash (not a leap).
    public bool IsDashing => mode == Mode.Dashing && !dashJump;
    public float SlideSpeed => slideSpeed;
    // Direction of the current dash / leap / slide (flat, normalized).
    public Vector3 SlideDirection => dashDirection;
    public bool IsGrounded { get; private set; } = true;
    public bool IsAirborne => !IsGrounded;
    public bool InLight => BaseLight.Instance != null && BaseLight.Instance.IsPlayerInside;
    public float JumpSpeed => 4f * jumpHeight / jumpAirTime;
    public float FeetHeight => transform.position.y - HalfHeight;
    public bool OutOfBiteReach => FeetHeight > biteClearHeight;

    private float JumpGravity => 8f * jumpHeight / (jumpAirTime * jumpAirTime);
    private float HalfHeight => capsule != null ? capsule.height * 0.5f * transform.lossyScale.y : 1f;

    // Ease-out shove along `direction` (flat): speed starts at 2 x distance / duration and falls to zero, so the
    // total travelled is `distance`.
    public void Push(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f)
        {
            return;
        }

        pushDirection = direction.normalized;
        pushDistance = distance;
        pushDuration = Mathf.Max(duration, 0.02f);
        pushElapsed = 0f;
    }

    // Scales walking speed (not the dash); the melee weapon lowers it while charging.
    public float MoveSpeedMultiplier { get; set; } = 1f;

    private void Reset()
    {
        AddMoveBindings();
        AddDashBindings();
        AddJumpBindings();
        AddSlideBindings();
    }

    private void Awake()
    {
        Instance = this;
        body = GetComponent<Rigidbody>();
        stamina = GetComponent<PlayerStamina>();
        capsule = GetComponent<CapsuleCollider>();
        body.freezeRotation = true;

        // Slide along (and up) wall faces instead of sticking to them while pressing into them mid-jump.
        if (capsule != null && capsule.sharedMaterial == null)
        {
            capsule.sharedMaterial = new PhysicsMaterial("PlayerNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }

        if (moveAction.bindings.Count == 0)
        {
            AddMoveBindings();
        }

        if (dashAction.bindings.Count == 0)
        {
            AddDashBindings();
        }

        if (jumpAction.bindings.Count == 0)
        {
            AddJumpBindings();
        }

        if (slideAction.bindings.Count == 0)
        {
            AddSlideBindings();
        }
    }

    private void OnEnable()
    {
        moveAction.Enable();
        dashAction.Enable();
        jumpAction.Enable();
        slideAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        dashAction.Disable();
        jumpAction.Disable();
        slideAction.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Starts a jump if allowed (grounded, inside the light). Used by the input and by tests.</summary>
    public bool TryJump()
    {
        if (!IsGrounded || jumpRequested || !InLight)
        {
            return false;
        }
        jumpRequested = true;
        if (mode == Mode.Dashing)
        {
            dashJump = true;
            leapSpeed = dashSpeed;
        }
        else if (mode == Mode.Sliding)
        {
            StartLeap(slideSpeed);
        }
        return true;
    }

    /// <summary>Starts a slide if allowed (grounded, fast enough, off cooldown). Used by the input and by tests.</summary>
    public bool TrySlide()
    {
        Vector3 flat = body.linearVelocity;
        flat.y = 0f;
        float speed = flat.magnitude;
        if (!IsGrounded || jumpRequested || mode == Mode.Sliding || dashJump || slideCooldownLeft > 0f
            || speed < slideMinSpeed)
        {
            return false;
        }
        StartSlide(flat / speed, speed);
        return true;
    }

    private void StartSlide(Vector3 direction, float speed)
    {
        mode = Mode.Sliding;
        dashDirection = direction;
        slideSpeed = speed * slideSpeedKept;
    }

    // Landing with Ctrl held: slide at once at the landing speed (a leap keeps its own speed and direction).
    private void SlideOnLanding()
    {
        Vector3 direction;
        float speed;
        if (dashJump)
        {
            direction = dashDirection;
            speed = leapSpeed;
        }
        else
        {
            Vector3 flat = body.linearVelocity;
            flat.y = 0f;
            speed = flat.magnitude;
            direction = speed > 0.01f ? flat / speed : lastDirection;
        }
        dashJump = false;
        dashTimer = 0f;
        StartSlide(direction, speed);
    }

    // A leap = the dash-jump state at a given speed (dash jump, slide jump, sliding off a ledge): kept until landing.
    private void StartLeap(float speed)
    {
        mode = Mode.Dashing;
        dashJump = true;
        leapSpeed = speed;
        slideCooldownLeft = slideCooldown;
    }

    private void Update()
    {
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        bool hasInput = direction.sqrMagnitude > 0.01f;

        if (hasInput)
        {
            lastDirection = direction.normalized;
        }

        bool inLight = InLight;
        stamina.RegenMultiplier = inLight ? lightRegenMultiplier : 1f;

        if (jumpAction.WasPressedThisFrame())
        {
            TryJump();
        }

        slideCooldownLeft -= Time.deltaTime;
        if (landedThisStep && !jumpRequested && mode != Mode.Sliding && slideAction.IsPressed())
        {
            SlideOnLanding();
        }
        else if (slideAction.WasPressedThisFrame())
        {
            TrySlide();
        }

        switch (mode)
        {
            case Mode.Sliding:
                if (!IsGrounded && !jumpRequested)
                {
                    // Slid off a ledge: the speed carries through the fall.
                    StartLeap(slideSpeed);
                    break;
                }
                if (hasInput)
                {
                    SteerDash(direction, Time.deltaTime, slideTurnSpeed);
                }
                // The speed above walking fades exponentially.
                slideSpeed = moveSpeed + (slideSpeed - moveSpeed) * Mathf.Exp(-Time.deltaTime / slideDecayTime);
                if (!jumpRequested && (!slideAction.IsPressed() || slideSpeed <= moveSpeed + slideEndMargin))
                {
                    mode = Mode.Normal;
                    slideCooldownLeft = slideCooldown;
                }
                break;

            case Mode.Normal:
                if (IsGrounded && !jumpRequested && dashAction.WasPressedThisFrame() && !PlayerInputGate.DashBlocked
                    && stamina.TrySpend(dashStaminaCost))
                {
                    mode = Mode.Dashing;
                    dashTimer = dashDuration;
                    dashDirection = lastDirection;
                }
                break;

            case Mode.Dashing:
                if (dashJump)
                {
                    // The dash lasts the whole jump; it ends on landing. In the light it steers toward the input.
                    if (landedThisStep)
                    {
                        dashJump = false;
                        dashTimer = 0f;
                    }
                    else if (inLight && hasInput)
                    {
                        SteerDash(direction, Time.deltaTime, dashJumpTurnSpeed);
                    }
                }
                else
                {
                    dashTimer -= Time.deltaTime;
                }
                if (!dashJump && dashTimer <= 0f)
                {
                    mode = Mode.Normal;
                }
                break;
        }

        switch (mode)
        {
            case Mode.Dashing:
                targetVelocity = dashDirection * (dashJump ? leapSpeed : dashSpeed);
                break;

            case Mode.Sliding:
                targetVelocity = dashDirection * slideSpeed;
                break;

            default:
                targetVelocity = direction * (moveSpeed * MoveSpeedMultiplier);
                break;
        }

        landedThisStep = false;
    }

    private void FixedUpdate()
    {
        bool wasGrounded = IsGrounded;
        jumpGrace -= Time.fixedDeltaTime;
        // A rising jump never counts as grounded (passing a wall's top edge on the way up must not cut the jump's
        // gravity). Only the jump's own rise: walking, dashing or sliding up a ramp also rises and stays grounded.
        if (jumpRising && body.linearVelocity.y <= MaxGroundedRiseSpeed)
        {
            jumpRising = false;
        }
        IsGrounded = jumpGrace <= 0f && !jumpRising && CheckGround();
        if (IsGrounded && !wasGrounded)
        {
            landedThisStep = true;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.x = targetVelocity.x;
        velocity.z = targetVelocity.z;

        if (jumpRequested)
        {
            jumpRequested = false;
            velocity.y = JumpSpeed;
            IsGrounded = false;
            jumpRising = true;
            jumpGrace = JumpGraceTime;
        }
        else if (!IsGrounded)
        {
            // Swap Unity's gravity for the jump's (stronger while falling), so height and air time come out as set.
            float gravity = JumpGravity * (velocity.y < 0f ? fallGravityMultiplier : 1f);
            velocity.y += (-gravity - Physics.gravity.y) * Time.fixedDeltaTime;
        }

        if (pushElapsed < pushDuration)
        {
            // speed at the middle of this physics step, so the total comes out at `distance`
            float mid = Mathf.Min(pushElapsed + Time.fixedDeltaTime * 0.5f, pushDuration);
            float speed = 2f * pushDistance / pushDuration * (1f - mid / pushDuration);
            velocity.x += pushDirection.x * speed;
            velocity.z += pushDirection.z * speed;
            pushElapsed += Time.fixedDeltaTime;
        }

        body.linearVelocity = velocity;
    }

    // Steering for leaps (air control) and slides: turns dashDirection toward `input` (flat) at `turnSpeed` deg/s.
    private void SteerDash(Vector3 input, float deltaTime, float turnSpeed)
    {
        input.y = 0f;
        if (input.sqrMagnitude < 0.0001f)
        {
            return;
        }
        float maxRadians = turnSpeed * Mathf.Deg2Rad * deltaTime;
        dashDirection = Vector3.RotateTowards(dashDirection, input.normalized, maxRadians, 0f).normalized;
    }

    // A short sphere cast down from the capsule centre; anything on groundMask that isn't the player counts.
    private bool CheckGround()
    {
        float radius = (capsule != null ? capsule.radius : 0.5f) * 0.9f;
        float distance = HalfHeight - radius + groundCheckDistance;
        int count = Physics.SphereCastNonAlloc(body.position, radius, Vector3.down, groundHits, distance, groundMask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].collider.attachedRigidbody != body)
            {
                return true;
            }
        }
        return false;
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

    private void AddDashBindings()
    {
        dashAction.AddBinding("<Mouse>/rightButton");
        dashAction.AddBinding("<Gamepad>/leftStickPress");
    }

    private void AddSlideBindings()
    {
        slideAction.AddBinding("<Keyboard>/leftCtrl");
        slideAction.AddBinding("<Keyboard>/rightCtrl");
        slideAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void AddJumpBindings()
    {
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
    }
}
