/*
 * FishercatAnimator
 * The Fishercat's throw (2026-11-24, user: "a bit of an exaggerated" throw), procedural, on Fishercat.prefab's root. It is
 * timed by the tower's TurretGun (Recharge: 0 right after a throw, 1 when the next may fire), so it speeds up and slows down
 * with the attack speed. The gun still aims (yaws the Gun object) and launches the spear; this only poses the cat.
 *
 * One recharge (shares of it, all in the inspector):
 * - Follow-through right after the throw: leaning forward (throwLean) and squashed, the throwing paw out in front, then
 *   settling (by settleTime).
 * - Reach for a new spear: the right paw goes to the barrel (barrelTop, wherever it is as the cat turns; reach capped by
 *   maxReach), rummages (dips in grabDips times over grabFrom..grabTo while the barrel rattles: rummageFrom..rummageTo,
 *   rummageWobbles squash/tilt wobbles; user 2026-11-24), pulls a spear up (heldSpear shown from spearShown, standing up)
 *   and brings it to the shoulder (readyAt), pointing forward and a little up.
 * - The throwing paw stays out to the side of the head (readyRight, windRight past the hat brim, ~0.66 out; user: the old
 *   over-the-head arm clipped through the hat).
 * - Ready: holding the spear at the shoulder, a slow bob.
 * - Wind-up (windFrom..1, only while the gun has a target; it blends in and out at windBlendSpeed): leaning far back
 *   (windLean) and stretched tall, the throwing paw far back over the head, the other paw pointing ahead.
 * - The gun fires at Recharge 1: the next frame starts the follow-through again and the held spear is gone (the real one
 *   is the gun's projectile).
 * - Parts: `lean` (pivot at the cat's feet: lean + squash), `handL` / `handR` (lean-local positions), `heldSpear` (placed
 *   each frame from the right paw along the pose's spear direction). Poses are lean-local, world units.
 * - Preview(recharge, windWeight) poses a frozen frame for renders/tests.
 */

using UnityEngine;

public class FishercatAnimator : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TurretGun gun;
    [SerializeField] private Transform lean;
    [SerializeField] private Transform handL;
    [SerializeField] private Transform handR;
    [SerializeField] private Transform heldSpear;
    [SerializeField] private Transform barrelTop;
    [Tooltip("The barrel object (pivot at its bottom) that rattles while the cat rummages in it.")]
    [SerializeField] private Transform barrel;

    [Header("Timing (share of one recharge)")]
    [SerializeField, Range(0f, 1f)] private float settleTime = 0.14f;
    [SerializeField, Range(0f, 1f)] private float reachFrom = 0.16f;
    [SerializeField, Range(0f, 1f)] private float grabFrom = 0.27f;
    [SerializeField, Range(0f, 1f)] private float grabTo = 0.36f;
    [SerializeField, Range(0f, 1f)] private float spearShown = 0.33f;
    [SerializeField, Range(0f, 1f)] private float readyAt = 0.48f;
    [SerializeField, Range(0f, 1f)] private float windFrom = 0.72f;
    [SerializeField, Min(0.1f)] private float windBlendSpeed = 4f;

    [Header("Rummage in the barrel")]
    [SerializeField, Range(0f, 1f)] private float rummageFrom = 0.25f;
    [SerializeField, Range(0f, 1f)] private float rummageTo = 0.4f;
    [SerializeField, Min(0f)] private float rummageWobbles = 1f;
    [SerializeField, Range(0f, 0.3f)] private float rummageSquash = 0.03f;
    [SerializeField, Range(0f, 20f)] private float rummageTilt = 3f;
    [SerializeField, Min(1)] private int grabDips = 2;

    [Header("Poses (lean-local, world units)")]
    [SerializeField] private Vector3 restLeft = new Vector3(-0.2f, 0.15f, 0.42f);
    [SerializeField] private Vector3 throwRight = new Vector3(0.15f, 0.78f, 0.8f);
    [SerializeField] private Vector3 throwLeft = new Vector3(-0.18f, 0.35f, 0.5f);
    [SerializeField] private Vector3 settleRight = new Vector3(0.22f, 0.5f, 0.55f);
    [SerializeField] private Vector3 readyRight = new Vector3(0.7f, 0.72f, 0.25f);
    [SerializeField] private Vector3 windRight = new Vector3(0.86f, 0.85f, -0.35f);
    [SerializeField] private Vector3 windLeft = new Vector3(-0.22f, 0.78f, 0.68f);
    [SerializeField, Min(0f)] private float maxReach = 1f;
    [SerializeField, Min(0f)] private float grabDip = 0.12f;

    [Header("Body")]
    [SerializeField] private float throwLean = 24f;
    [SerializeField] private float windLean = -16f;
    [SerializeField] private Vector3 throwSquash = new Vector3(1.12f, 0.88f, 1.12f);
    [SerializeField] private Vector3 windStretch = new Vector3(0.93f, 1.1f, 0.93f);
    [SerializeField, Min(0f)] private float readyBob = 0.02f;

    [Header("Held spear")]
    [SerializeField] private Vector3 readyAim = new Vector3(0f, 0.35f, 1f);
    [SerializeField] private Vector3 windAim = new Vector3(0f, 0.6f, 1f);
    [Tooltip("How far ahead of the paw the spear's middle sits (it is held behind its middle).")]
    [SerializeField] private float gripOffset = 0.2f;

    private float windWeight;
    private Vector3 barrelScale = Vector3.one;
    private Quaternion barrelRotation = Quaternion.identity;

    private void Awake()
    {
        if (barrel != null)
        {
            barrelScale = barrel.localScale;
            barrelRotation = barrel.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (gun == null)
        {
            return;
        }
        windWeight = Mathf.MoveTowards(windWeight, gun.HasTarget ? 1f : 0f, windBlendSpeed * Time.deltaTime);
        Pose(gun.Recharge, windWeight);
    }

    /// <summary>Poses a frozen frame: recharge 0 = just thrown .. 1 = ready to throw; wind = how much a target pulls it into the wind-up.</summary>
    public void Preview(float recharge, float wind)
    {
        Pose(Mathf.Clamp01(recharge), Mathf.Clamp01(wind));
    }

    private void Pose(float p, float wind)
    {
        // Right paw: follow-through -> settle -> barrel -> ready.
        Vector3 grab = GrabPoint();
        Vector3 right;
        if (p < settleTime)
        {
            right = Vector3.Lerp(throwRight, settleRight, Ease(p / Mathf.Max(0.0001f, settleTime)));
        }
        else if (p < reachFrom)
        {
            right = settleRight;
        }
        else if (p < grabFrom)
        {
            right = Vector3.Lerp(settleRight, grab + Vector3.up * grabDip, Ease(Mathf.InverseLerp(reachFrom, grabFrom, p)));
        }
        else if (p < grabTo)
        {
            float k = Mathf.InverseLerp(grabFrom, grabTo, p);
            right = grab + Vector3.up * (grabDip * Mathf.Abs(Mathf.Cos(k * Mathf.PI * grabDips)));
        }
        else if (p < readyAt)
        {
            right = Vector3.Lerp(grab + Vector3.up * grabDip, readyRight, Ease(Mathf.InverseLerp(grabTo, readyAt, p)));
        }
        else
        {
            right = readyRight + Vector3.up * (readyBob * Mathf.Sin(p * Mathf.PI * 6f));
        }

        Vector3 left = p < settleTime ? Vector3.Lerp(throwLeft, restLeft, Ease(p / Mathf.Max(0.0001f, settleTime))) : restLeft;
        float leanAngle = p < settleTime ? Mathf.Lerp(throwLean, 0f, Ease(p / Mathf.Max(0.0001f, settleTime))) : 0f;
        Vector3 scale = p < settleTime ? Vector3.Lerp(throwSquash, Vector3.one, Ease(p / Mathf.Max(0.0001f, settleTime))) : Vector3.one;
        Vector3 aim = readyAim;

        // Wind-up on top, toward the end of the recharge, while there is a target.
        float w = wind * Ease(Mathf.InverseLerp(windFrom, 1f, p));
        if (w > 0f)
        {
            right = Vector3.Lerp(right, windRight, w);
            left = Vector3.Lerp(left, windLeft, w);
            leanAngle = Mathf.Lerp(leanAngle, windLean, w);
            scale = Vector3.Lerp(scale, windStretch, w);
            aim = Vector3.Lerp(readyAim, windAim, w);
        }

        if (barrel != null)
        {
            float u = Mathf.InverseLerp(rummageFrom, rummageTo, p);
            float envelope = p > rummageFrom && p < rummageTo ? Mathf.Sin(u * Mathf.PI) : 0f;
            float wave = Mathf.Sin(u * rummageWobbles * Mathf.PI * 2f) * envelope;
            float wave2 = Mathf.Sin(u * rummageWobbles * Mathf.PI * 2.7f + 1f) * envelope;
            barrel.localScale = Vector3.Scale(barrelScale, new Vector3(1f + rummageSquash * wave, 1f - rummageSquash * wave, 1f + rummageSquash * wave));
            barrel.localRotation = barrelRotation * Quaternion.Euler(rummageTilt * wave2, 0f, rummageTilt * wave);
        }

        if (lean != null)
        {
            lean.localRotation = Quaternion.Euler(leanAngle, 0f, 0f);
            lean.localScale = scale;
        }
        if (handL != null)
        {
            handL.localPosition = left;
        }
        if (handR != null)
        {
            handR.localPosition = right;
        }

        if (heldSpear != null)
        {
            bool held = p >= spearShown;
            if (heldSpear.gameObject.activeSelf != held)
            {
                heldSpear.gameObject.SetActive(held);
            }
            if (held)
            {
                // Pulled out standing up, then turned to point ahead by the time it is at the shoulder.
                float level = Ease(Mathf.InverseLerp(spearShown, readyAt, p));
                Vector3 dir = Vector3.Slerp(Vector3.up, aim.normalized, level);
                heldSpear.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                heldSpear.localPosition = right + dir * gripOffset;
            }
        }
    }

    // The barrel's top in lean-local space, brought within maxReach of the shoulder.
    private Vector3 GrabPoint()
    {
        Vector3 shoulder = new Vector3(0f, 0.8f, 0f);
        if (barrelTop == null || lean == null || lean.parent == null)
        {
            return readyRight;
        }
        // Use the unleaned frame (the lean's parent, moved to the lean's position) so the reach doesn't wobble with the lean.
        Vector3 local = lean.parent.InverseTransformPoint(barrelTop.position) - lean.localPosition;
        Vector3 offset = local - shoulder;
        if (offset.magnitude > maxReach)
        {
            local = shoulder + offset.normalized * maxReach;
        }
        return local;
    }

    private static float Ease(float k)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
    }
}
