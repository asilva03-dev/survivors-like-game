/*
 * MeleeWeapon
 * Manual melee weapon (the axe): Mouse 1 swings an arc toward the cursor. Not part of WeaponLoadout;
 * the player carries one, separate from the aimed-weapon cap.
 *
 * - Press starts a charge. Releasing before the charge is full does a normal swing; releasing after
 *   chargeTime does the charged swing (more damage, pushes enemies away, costs more stamina). The swing
 *   direction is read from the cursor at the moment of release (ray onto the owner's ground plane), so
 *   the player can keep aiming while charging.
 * - Stamina is spent on release. A full charge without enough stamina for the charged swing does not
 *   fall back to a normal swing: nothing happens except StaminaBar.PlayDeniedFeedback (shake + red
 *   flash). Same for a normal release without stamina for a normal swing.
 * - Attack speed: releasing a swing locks out Mouse 1 for attackInterval (normal) or
 *   chargedAttackInterval (charged) seconds, counted from the release. Presses during the lockout are
 *   ignored, not buffered. A failed attempt (no stamina) does not start a lockout.
 * - Tilt: the model also rotates about its own center by tiltAngle * tiltCurve(swing time) around
 *   tiltAxis (pivot space), so the axe starts cocked back and snaps forward through the strike
 *   instead of staying rigid. Purely visual; hit detection ignores it.
 * - Hit stop: hitting a resource freezes the swing for hitStopDuration / chargedHitStopDuration seconds
 *   (the lockout is extended by the same time). Only the swing pauses; the world keeps running.
 * - While charging the player is slowed (PlayerController.MoveSpeedMultiplier). The axe is only
 *   visible during the swing itself, not while charging.
 * - The swing sweeps the pivot across arcAngle over the swing duration using swingCurve. At the same
 *   time extensionCurve pushes the model outward by extendDistance and scales it up by growAmount,
 *   easing back to normal by the end. Targets are hit as the blade passes them (angle between last and
 *   current sweep angle, within reach plus the current extension; each target counts as a disc of
 *   targetRadius so close targets are caught by their edge), once per target per swing, with
 *   damage per category from MeleeStats.DamageFor. Big targets add their Damageable.TargetRadius.
 * - Things the axe cannot damage but that carry an IMeleeReactive (the base) are still swept: they get
 *   OnMeleeHit once per swing and no damage, knockback or hit stop.
 * - All numbers come from the MeleeStats asset. Visual setup (pivot, model rotation and scale, holdHeight)
 *   lives in the prefab: the pivot is rotated about Y and the model child holds the axe mesh.
 * - ChargeNormalized / IsCharging / IsChargedLockout feed MeleeChargeIndicator.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeWeapon : WeaponBase<MeleeStats>
{
    private enum State { Idle, Charging, Swinging }

    [SerializeField] private Transform pivot;
    [SerializeField] private Transform model;
    [SerializeField] private float holdHeight = 0.2f;
    [SerializeField] private InputAction attackAction = new InputAction("Attack", InputActionType.Button);

    private readonly HashSet<Damageable> hitThisSwing = new HashSet<Damageable>();
    private readonly List<Damageable> candidates = new List<Damageable>();

    private PlayerController controller;
    private Camera cam;
    private PlayerStamina stamina;
    private StaminaBar staminaBar;
    private State state;
    private Vector3 lastAim = Vector3.forward;
    private Vector3 modelRestPosition;
    private Vector3 modelRestScale;
    private Quaternion modelRestRotation = Quaternion.identity;
    private float aimYaw;
    private float chargeElapsed;
    private float swingElapsed;
    private float swingDuration;
    private float lastAngle;
    private float lastSwingT;
    private float lockedUntil;
    private bool lockoutIsCharged;
    private float hitStopRemaining;
    private bool strongSwing;

    public bool IsCharging => state == State.Charging;

    // True from releasing a charged swing until its chargedAttackInterval has passed.
    public bool IsChargedLockout => lockoutIsCharged && Time.time < lockedUntil;

    public float ChargeNormalized
    {
        get { return Mathf.Clamp01(chargeElapsed / Mathf.Max(Stats.ChargeTime, 0.01f)); }
    }

    private void Reset()
    {
        attackAction.AddBinding("<Mouse>/leftButton");
    }

    protected override void Awake()
    {
        base.Awake();

        stamina = Owner.GetComponent<PlayerStamina>();
        controller = Owner.GetComponent<PlayerController>();
        staminaBar = Owner.GetComponentInChildren<StaminaBar>(true);

        if (attackAction.bindings.Count == 0)
        {
            attackAction.AddBinding("<Mouse>/leftButton");
        }

        if (model != null)
        {
            modelRestPosition = model.localPosition;
            modelRestScale = model.localScale;
            modelRestRotation = model.localRotation;
        }

        SetVisible(false);
    }

    private void OnEnable() => attackAction.Enable();

    private void OnDisable()
    {
        attackAction.Disable();
        SetCharging(false);
        state = State.Idle;
        SetVisible(false);
    }

    protected override void Tick()
    {
        switch (state)
        {
            case State.Idle:
                if (attackAction.WasPressedThisFrame() && Time.time >= lockedUntil)
                {
                    BeginCharge();
                }

                break;

            case State.Charging:
                chargeElapsed += Time.deltaTime;
                if (!attackAction.IsPressed())
                {
                    Release();
                }

                break;

            case State.Swinging:
                AdvanceSwing();
                break;
        }
    }

    private void BeginCharge()
    {
        chargeElapsed = 0f;
        state = State.Charging;
        SetCharging(true);
    }

    private void Release()
    {
        SetCharging(false);

        Vector3 aim = ReadAim();
        aimYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;

        bool full = chargeElapsed >= Stats.ChargeTime;
        if (full ? stamina.TrySpend(Stats.ChargedStaminaCost) : stamina.TrySpend(Stats.StaminaCost))
        {
            strongSwing = full;
        }
        else
        {
            state = State.Idle;
            SetVisible(false);

            if (staminaBar != null)
            {
                staminaBar.PlayDeniedFeedback();
            }

            return;
        }

        state = State.Swinging;
        swingElapsed = 0f;
        swingDuration = strongSwing ? Stats.ChargedSwingDuration : Stats.SwingDuration;
        lockedUntil = Time.time + (strongSwing ? Stats.ChargedAttackInterval : Stats.AttackInterval);
        lockoutIsCharged = strongSwing;
        lastAngle = -Stats.ArcAngle * 0.5f;
        lastSwingT = 0f;
        hitStopRemaining = 0f;
        hitThisSwing.Clear();

        ApplyPose(lastAngle);
        SetVisible(true);

        if (controller != null)
        {
            controller.Push(aim, strongSwing ? Stats.ChargedLungeDistance : Stats.LungeDistance, Stats.LungeDuration);
        }
    }

    private void AdvanceSwing()
    {
        if (hitStopRemaining > 0f)
        {
            hitStopRemaining -= Time.deltaTime;
            return;
        }

        swingElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(swingElapsed / Mathf.Max(swingDuration, 0.01f));
        float progress = Stats.SwingCurve.Evaluate(t);
        float half = Stats.ArcAngle * 0.5f;
        float angle = Mathf.LerpUnclamped(-half, half, progress);

        float extension = Mathf.Clamp01(Stats.ExtensionCurve.Evaluate(t));
        ApplyPose(angle, extension, t);
        HitSweep(lastAngle, angle, Stats.ExtendDistance * PeakExtension(lastSwingT, t));
        lastAngle = angle;
        lastSwingT = t;

        if (t >= 1f)
        {
            state = State.Idle;
            SetVisible(false);
        }
    }

    // Largest extension reached between two swing times, so a slow frame cannot skip the peak.
    private float PeakExtension(float fromT, float toT)
    {
        float peak = 0f;
        for (int i = 0; i <= 4; i++)
        {
            float sample = Stats.ExtensionCurve.Evaluate(Mathf.Lerp(fromT, toT, i / 4f));
            peak = Mathf.Max(peak, sample);
        }

        return Mathf.Clamp01(peak);
    }

    private void HitSweep(float from, float to, float extraReach)
    {
        float low = Mathf.Min(from, to);
        float high = Mathf.Max(from, to);
        Vector3 origin = Owner.position;
        Vector3 aim = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
        candidates.Clear();
        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable target = Damageable.All[i];
            if (hitThisSwing.Contains(target) || target.transform.IsChildOf(Owner))
            {
                continue;
            }

            if (!Stats.CanTarget(target.Category) && target.GetComponent<IMeleeReactive>() == null)
            {
                continue;
            }

            // The target is a disc of targetRadius (plus its own TargetRadius for big things like the base),
            // not a point: it is hit when any part of it falls inside the swept angle, which matters most up
            // close where it spans a wide angle.
            float radius = Stats.TargetRadius + target.TargetRadius;
            float maxDistance = Stats.Reach + extraReach + radius;
            Vector3 offset = target.transform.position - origin;
            offset.y = 0f;
            if (offset.sqrMagnitude > maxDistance * maxDistance)
            {
                continue;
            }

            float distance = offset.magnitude;
            float angle = distance > 0.0001f ? Vector3.SignedAngle(aim, offset, Vector3.up) : 0f;
            float halfWidth = distance <= radius
                ? 180f
                : Mathf.Asin(radius / distance) * Mathf.Rad2Deg;

            if (angle + halfWidth >= low && angle - halfWidth <= high)
            {
                candidates.Add(target);
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            Hit(candidates[i], origin);
        }
    }

    private void Hit(Damageable target, Vector3 origin)
    {
        hitThisSwing.Add(target);

        IMeleeReactive reactive = target.GetComponent<IMeleeReactive>();
        if (reactive != null)
        {
            reactive.OnMeleeHit(origin, strongSwing);
        }

        if (!Stats.CanTarget(target.Category))
        {
            return;
        }

        float amount = Stats.DamageFor(target.Category);
        if (strongSwing)
        {
            amount *= Stats.ChargedDamageMultiplier;
        }

        if (amount > 0f)
        {
            target.TakeDamage(amount);
        }

        if (target.Category == TargetCategory.Resource)
        {
            float stop = strongSwing ? Stats.ChargedHitStopDuration : Stats.HitStopDuration;
            if (stop > hitStopRemaining)
            {
                lockedUntil += stop - hitStopRemaining;
                hitStopRemaining = stop;
            }
        }

        float knockback = strongSwing ? Stats.KnockbackDistance : Stats.KnockbackDistanceNormal;
        if (target is Enemy enemy && enemy.isActiveAndEnabled && knockback > 0f)
        {
            Vector3 away = target.transform.position - origin;
            enemy.Knockback(new Vector2(away.x, away.z), knockback, Stats.KnockbackDuration);
        }
    }

    private Vector3 ReadAim()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        Mouse mouse = Mouse.current;
        if (cam != null && mouse != null)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane ground = new Plane(Vector3.up, Owner.position);

            if (ground.Raycast(ray, out float distance))
            {
                Vector3 direction = ray.GetPoint(distance) - Owner.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    lastAim = direction.normalized;
                }
            }
        }

        return lastAim;
    }

    private void ApplyPose(float angle, float extension = 0f, float swingT = 0f)
    {
        pivot.position = Owner.position + Vector3.up * holdHeight;
        pivot.rotation = Quaternion.Euler(0f, aimYaw + angle, 0f);

        if (model != null)
        {
            float tilt = Stats.TiltAngle * Stats.TiltCurve.Evaluate(swingT);
            Vector3 axis = Stats.TiltAxis.sqrMagnitude > 0.0001f ? Stats.TiltAxis.normalized : Vector3.right;
            model.localRotation = Quaternion.AngleAxis(tilt, axis) * modelRestRotation;
            model.localPosition = modelRestPosition + Vector3.forward * (Stats.ExtendDistance * extension);
            model.localScale = modelRestScale * (1f + Stats.GrowAmount * extension);
        }
    }

    private void SetVisible(bool visible)
    {
        if (pivot != null && pivot.gameObject.activeSelf != visible)
        {
            pivot.gameObject.SetActive(visible);
        }
    }

    private void SetCharging(bool charging)
    {
        if (controller != null)
        {
            controller.MoveSpeedMultiplier = charging ? Stats.ChargeMoveSpeedMultiplier : 1f;
        }
    }
}
