/*
 * TrapperAnimator
 * The Trapper's throw cycle (2026-11-23, user's idea), procedural like RatRunCycle. On Trapper.prefab's root, driven by
 * TrapThrower: Run(duration, pickSpot, release) loops one cycle per throw interval, so the whole animation speeds up or
 * slows down with the attack speed. Every cycle starts and ends in the rest pose (facing the path, paws in front).
 *
 * One cycle (times = share of the cycle, all in the inspector):
 * - Turn part way to the bag (yaw rest -> bagTurn of the way to the bag by atBag; user 2026-11-23: only halfway, 0.5)
 *   while the sack starts to rustle (rustleFrom..rustleTo: squash and tilt
 *   wobbles, `rustles` of them, under a rise-and-fall envelope). The right paw reaches into the sack and is hidden inside
 *   it (handHiddenFrom..handHiddenTo); the left paw stays out. Because the cat only turns part way, the right paw's poses
 *   are swung the rest of the way toward the bag (around the cat) from bagAimFrom, fully by bagAimFull, back by bagAimTo.
 * - The right paw comes out holding a trap (heldTrap shown from heldFrom), in front of the chest.
 * - Arm it: the left paw grabs the kill bar and pulls it back (heldBar swings from barClosedAngle to 0 over
 *   armFrom..armTo). Meanwhile the cat turns from the bag to the target (leaveBag..atTarget); the spot is picked at
 *   pickTime (pickSpot callback; null = nowhere to throw, the cycle still plays but nothing is launched).
 * - Wind up and throw: the right paw goes up and back, then snaps forward (that segment eases in); at releaseTime the
 *   real trap is launched from the right paw (release callback) and the held trap disappears.
 * - Turn back to rest (leaveTarget..1) and the paws settle.
 * - Paw poses = `keys` (Body-local, world units; each segment smoothstepped, or eased in when the key's easeIn is set).
 * - Yaw is set on `body` in world space (the cat only: the crates and sack never turn). RestDirection comes from the
 *   thrower (the turret's rest direction); the bag direction is read from `sack` every cycle.
 * - Preview(t, spot) poses a frozen frame for renders/tests.
 */

using System;
using UnityEngine;

public class TrapperAnimator : MonoBehaviour
{
    [Serializable]
    public struct PawKey
    {
        public float time;
        public Vector3 left;
        public Vector3 right;
        [Tooltip("Ease in (accelerate) into this key instead of smoothstep: the throw.")]
        public bool easeIn;

        public PawKey(float time, Vector3 left, Vector3 right, bool easeIn = false)
        {
            this.time = time;
            this.left = left;
            this.right = right;
            this.easeIn = easeIn;
        }
    }

    [Header("Parts")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform handL;
    [SerializeField] private Transform handR;
    [SerializeField] private Transform heldTrap;
    [SerializeField] private Transform heldBar;
    [SerializeField] private Transform sack;

    [Header("Turning (share of the cycle)")]
    [SerializeField, Range(0f, 1f)] private float atBag = 0.14f;
    [Tooltip("How far the cat turns toward the bag (1 = facing it); the paw swings the rest of the way.")]
    [SerializeField, Range(0f, 1f)] private float bagTurn = 0.5f;
    [SerializeField, Range(0f, 1f)] private float bagAimFrom = 0f;
    [SerializeField, Range(0f, 1f)] private float bagAimFull = 0.12f;
    [SerializeField, Range(0f, 1f)] private float bagAimHold = 0.4f;
    [SerializeField, Range(0f, 1f)] private float bagAimTo = 0.47f;
    [SerializeField, Range(0f, 1f)] private float leaveBag = 0.48f;
    [SerializeField, Range(0f, 1f)] private float atTarget = 0.64f;
    [SerializeField, Range(0f, 1f)] private float leaveTarget = 0.86f;

    [Header("Bag")]
    [SerializeField, Range(0f, 1f)] private float rustleFrom = 0.05f;
    [SerializeField, Range(0f, 1f)] private float rustleTo = 0.45f;
    [SerializeField, Min(0f)] private float rustles = 3.5f;
    [SerializeField, Range(0f, 0.3f)] private float rustleSquash = 0.07f;
    [SerializeField, Range(0f, 20f)] private float rustleTilt = 5f;
    [SerializeField, Range(0f, 1f)] private float handHiddenFrom = 0.17f;
    [SerializeField, Range(0f, 1f)] private float handHiddenTo = 0.4f;

    [Header("Trap")]
    [SerializeField, Range(0f, 1f)] private float heldFrom = 0.4f;
    [SerializeField, Range(0f, 1f)] private float pickTime = 0.48f;
    [SerializeField, Range(0f, 1f)] private float armFrom = 0.53f;
    [SerializeField, Range(0f, 1f)] private float armTo = 0.6f;
    [SerializeField] private float barClosedAngle = -180f;
    [SerializeField, Range(0f, 1f)] private float releaseTime = 0.8f;

    [Header("Paw poses (Body-local, world units)")]
    [SerializeField] private PawKey[] keys =
    {
        new PawKey(0.00f, new Vector3(-0.2f, 0.08f, 0.4f), new Vector3(0.2f, 0.08f, 0.4f)),
        new PawKey(0.12f, new Vector3(-0.2f, 0.1f, 0.42f), new Vector3(0.1f, 0.38f, 0.78f)),
        new PawKey(0.17f, new Vector3(-0.22f, 0.14f, 0.45f), new Vector3(0.06f, 0.15f, 0.95f)),
        new PawKey(0.28f, new Vector3(-0.2f, 0.1f, 0.42f), new Vector3(0.1f, 0.12f, 0.92f)),
        new PawKey(0.40f, new Vector3(-0.22f, 0.14f, 0.45f), new Vector3(0.06f, 0.15f, 0.95f)),
        new PawKey(0.47f, new Vector3(-0.2f, 0.1f, 0.42f), new Vector3(0.12f, 0.4f, 0.62f)),
        new PawKey(0.53f, new Vector3(-0.04f, 0.46f, 0.66f), new Vector3(0.12f, 0.4f, 0.62f)),
        new PawKey(0.60f, new Vector3(-0.08f, 0.52f, 0.44f), new Vector3(0.12f, 0.38f, 0.6f)),
        new PawKey(0.66f, new Vector3(-0.2f, 0.1f, 0.42f), new Vector3(0.14f, 0.42f, 0.6f)),
        new PawKey(0.76f, new Vector3(-0.22f, 0.12f, 0.4f), new Vector3(0.38f, 0.85f, 0.02f)),
        new PawKey(0.82f, new Vector3(-0.2f, 0.1f, 0.42f), new Vector3(0.16f, 0.5f, 0.92f), true),
        new PawKey(0.88f, new Vector3(-0.2f, 0.1f, 0.42f), new Vector3(0.16f, 0.5f, 0.92f)),
        new PawKey(1.00f, new Vector3(-0.2f, 0.08f, 0.4f), new Vector3(0.2f, 0.08f, 0.4f)),
    };

    private Func<float> duration;
    private Func<Vector3?> pickSpot;
    private Action<Vector3, Vector3> release;
    private float t;
    private float restYaw;
    private float bagYaw;
    private float bagAim;
    private float targetYaw;
    private Vector3? spot;
    private bool released;
    private Vector3 sackScale = Vector3.one;
    private Quaternion sackRotation = Quaternion.identity;
    private Quaternion barRest = Quaternion.identity;

    public bool Running => duration != null;
    /// <summary>Where in the cycle it is (0-1).</summary>
    public float Phase => t;
    public Transform ReleasePoint => handR;

    /// <summary>The way the cat faces at rest (the turret's rest direction).</summary>
    public Vector3 RestDirection
    {
        set { restYaw = Yaw(value); }
    }

    private void Awake()
    {
        if (sack != null)
        {
            sackScale = sack.localScale;
            sackRotation = sack.localRotation;
        }
        if (heldBar != null)
        {
            barRest = heldBar.localRotation;
        }
    }

    /// <summary>Loops the cycle: one cycle per cycleSeconds(); pick = the throw spot (or null), launch(from, to).</summary>
    public void Run(Func<float> cycleSeconds, Func<Vector3?> pick, Action<Vector3, Vector3> launch)
    {
        duration = cycleSeconds;
        pickSpot = pick;
        release = launch;
        StartCycle(0f);
        Pose(0f);
    }

    public void Stop()
    {
        duration = null;
        Pose(0f);
    }

    /// <summary>Poses a frozen frame at phase p (0-1) throwing at `target` (renders and tests).</summary>
    public void Preview(float p, Vector3 target)
    {
        SetBagYaw();
        targetYaw = YawTo(target);
        Pose(Mathf.Clamp01(p));
    }

    private void StartCycle(float leftover)
    {
        t = leftover;
        spot = null;
        released = false;
        SetBagYaw();
        targetYaw = restYaw;
    }

    private void Update()
    {
        if (duration == null)
        {
            return;
        }
        float seconds = Mathf.Max(0.05f, duration());
        float previous = t;
        t += Time.deltaTime / seconds;
        if (previous < pickTime && t >= pickTime)
        {
            spot = pickSpot != null ? pickSpot() : null;
            targetYaw = spot.HasValue ? YawTo(spot.Value) : restYaw;
        }
        if (!released && t >= releaseTime)
        {
            Pose(releaseTime);
            released = true;
            if (spot.HasValue && release != null)
            {
                release(handR != null ? handR.position : transform.position, spot.Value);
            }
        }
        if (t >= 1f)
        {
            StartCycle(Mathf.Repeat(t - 1f, 1f));
        }
        Pose(t);
    }

    private void Pose(float p)
    {
        if (body != null)
        {
            body.rotation = Quaternion.Euler(0f, CycleYaw(p), 0f);
        }

        Vector3 left;
        Vector3 right;
        Paws(p, out left, out right);
        if (handL != null)
        {
            handL.localPosition = left;
        }
        if (handR != null)
        {
            float aim = Mathf.InverseLerp(bagAimFrom, bagAimFull, p) * (1f - Mathf.InverseLerp(bagAimHold, bagAimTo, p));
            handR.localPosition = Quaternion.Euler(0f, bagAim * Ease(aim), 0f) * right;
            handR.localScale = p >= handHiddenFrom && p < handHiddenTo ? Vector3.zero : Vector3.one;
        }

        if (heldTrap != null)
        {
            bool held = p >= heldFrom && p < releaseTime;
            if (heldTrap.gameObject.activeSelf != held)
            {
                heldTrap.gameObject.SetActive(held);
            }
        }
        if (heldBar != null)
        {
            float arm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(armFrom, armTo, p));
            heldBar.localRotation = barRest * Quaternion.Euler(barClosedAngle * (1f - arm), 0f, 0f);
        }

        if (sack != null)
        {
            float u = Mathf.InverseLerp(rustleFrom, rustleTo, p);
            float envelope = p > rustleFrom && p < rustleTo ? Mathf.Sin(u * Mathf.PI) : 0f;
            float wave = Mathf.Sin(u * rustles * Mathf.PI * 2f) * envelope;
            float wave2 = Mathf.Sin(u * rustles * Mathf.PI * 2.6f + 1f) * envelope;
            sack.localScale = Vector3.Scale(sackScale, new Vector3(1f + rustleSquash * wave, 1f - rustleSquash * wave, 1f + rustleSquash * wave));
            sack.localRotation = sackRotation * Quaternion.Euler(rustleTilt * wave2, 0f, rustleTilt * wave);
        }
    }

    // rest -> bag -> target -> rest.
    private float CycleYaw(float p)
    {
        if (p < atBag)
        {
            return Mathf.LerpAngle(restYaw, bagYaw, Ease(p / Mathf.Max(0.0001f, atBag)));
        }
        if (p < leaveBag)
        {
            return bagYaw;
        }
        if (p < atTarget)
        {
            return Mathf.LerpAngle(bagYaw, targetYaw, Ease(Mathf.InverseLerp(leaveBag, atTarget, p)));
        }
        if (p < leaveTarget)
        {
            return targetYaw;
        }
        return Mathf.LerpAngle(targetYaw, restYaw, Ease(Mathf.InverseLerp(leaveTarget, 1f, p)));
    }

    private void Paws(float p, out Vector3 left, out Vector3 right)
    {
        if (keys == null || keys.Length == 0)
        {
            left = Vector3.zero;
            right = Vector3.zero;
            return;
        }
        if (p <= keys[0].time)
        {
            left = keys[0].left;
            right = keys[0].right;
            return;
        }
        for (int i = 1; i < keys.Length; i++)
        {
            if (p <= keys[i].time)
            {
                PawKey a = keys[i - 1];
                PawKey b = keys[i];
                float k = Mathf.InverseLerp(a.time, b.time, p);
                k = b.easeIn ? k * k * k : Ease(k);
                left = Vector3.Lerp(a.left, b.left, k);
                right = Vector3.Lerp(a.right, b.right, k);
                return;
            }
        }
        left = keys[keys.Length - 1].left;
        right = keys[keys.Length - 1].right;
    }

    private static float Ease(float k)
    {
        return Mathf.SmoothStep(0f, 1f, k);
    }

    // The cat turns bagTurn of the way to the bag; the paw covers the rest (bagAim degrees).
    private void SetBagYaw()
    {
        float full = BagYaw();
        bagYaw = Mathf.LerpAngle(restYaw, full, bagTurn);
        bagAim = Mathf.DeltaAngle(bagYaw, full);
    }

    private float BagYaw()
    {
        return sack != null ? YawTo(sack.position) : restYaw;
    }

    private float YawTo(Vector3 point)
    {
        Vector3 from = body != null ? body.position : transform.position;
        Vector3 flat = point - from;
        flat.y = 0f;
        return flat.sqrMagnitude > 0.0001f ? Yaw(flat) : restYaw;
    }

    private static float Yaw(Vector3 direction)
    {
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }
}
