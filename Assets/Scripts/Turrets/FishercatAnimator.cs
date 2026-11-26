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
 * - Upgrade modes (2026-11-26, Fishercat Path 1):
 *   Double Fist (turret's doubleFistPath reached doubleFistTier, no spear gun shown): the paws take turns. Each paw runs
 *   the same cycle as above over two recharges, half a cycle apart: the one that threw last (gun.Fired flips it) is in the
 *   first half, the other in the second half and throws next. The left paw's poses are the right's mirrored (x flipped),
 *   both reach for the real barrel; heldSpearL is the left paw's spear. The body follows both (follow-through of the last
 *   throw, wind-up of the next).
 *   Spear gun (the spearGun object active; FishercatLooks shows it from Path 1 tier 3): no held spears; the gun (a lean
 *   child) sits at its prefab spot and kicks back on each shot (recoilTime share: back, up, pitched up; the body leans
 *   back), the left paw pumps the foregrip (pumpFrom..pumpTo), a slight lean in while a target is in range. The paws sit
 *   on the gun's grips (gripRight / gripLeft, gun-local).
 *   Fishing (optional `fishing`, Path 3): while FishercatFishing.Holding, PoseFishing takes over (lean back and crank
 *   while reeling, wind back and cast forward; paws on the rod's handle and reel, which FishercatFishing poses).
 *   Dinner Time (optional `reel`): while FishercatReel.Jerk is up, the recoil pose is pushed to reelJerkScale x (the yank).
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

    [Header("Double Fist (upgrade)")]
    [SerializeField] private Turret turret;
    [SerializeField, Range(1, 3)] private int doubleFistPath = 1;
    [SerializeField, Range(1, 5)] private int doubleFistTier = 1;
    [Tooltip("The left paw's held spear (Double Fist).")]
    [SerializeField] private Transform heldSpearL;

    [Header("Spear gun (upgrade)")]
    [Tooltip("The spear gun (a lean child); while it is shown the cat holds and fires it instead of throwing.")]
    [SerializeField] private Transform spearGun;
    [SerializeField] private Vector3 gripRight = new Vector3(0f, -0.14f, -0.06f);
    [SerializeField] private Vector3 gripLeft = new Vector3(0f, -0.12f, 0.54f);
    [SerializeField, Range(0.01f, 1f)] private float recoilTime = 0.18f;
    [SerializeField] private float recoilBack = 0.14f;
    [SerializeField] private float recoilUp = 0.03f;
    [SerializeField] private float recoilPitch = 10f;
    [SerializeField] private float recoilLean = -8f;
    [SerializeField, Range(0f, 1f)] private float pumpFrom = 0.25f;
    [SerializeField, Range(0f, 1f)] private float pumpTo = 0.5f;
    [SerializeField] private float pumpDistance = 0.16f;
    [SerializeField] private float gunAimLean = 5f;
    [Tooltip("Dinner Time (optional): its Jerk yanks the gun back and up, this many times the recoil.")]
    [SerializeField] private FishercatReel reel;
    [SerializeField, Min(0f)] private float reelJerkScale = 2.5f;

    [Header("Fishing (upgrade)")]
    [Tooltip("Path 3 (optional): while it is Holding the rod, the cat reels and casts instead of throwing.")]
    [SerializeField] private FishercatFishing fishing;
    [SerializeField] private float fishReelLean = -18f;
    [SerializeField] private float fishCastBackLean = -22f;
    [SerializeField] private float fishCastForwardLean = 22f;
    [SerializeField] private float crankRadius = 0.07f;
    [SerializeField] private float crankSpeed = 30f;

    private float windWeight;
    private Vector3 barrelScale = Vector3.one;
    private Quaternion barrelRotation = Quaternion.identity;
    private Vector3 gunRestPosition;
    private Quaternion gunRestRotation = Quaternion.identity;
    private bool lastThrowLeft;

    private void Awake()
    {
        if (barrel != null)
        {
            barrelScale = barrel.localScale;
            barrelRotation = barrel.localRotation;
        }
        if (spearGun != null)
        {
            gunRestPosition = spearGun.localPosition;
            gunRestRotation = spearGun.localRotation;
        }
    }

    private void OnEnable()
    {
        if (gun != null)
        {
            gun.Fired += OnFired;
        }
    }

    private void OnDisable()
    {
        if (gun != null)
        {
            gun.Fired -= OnFired;
        }
    }

    private void OnFired()
    {
        lastThrowLeft = !lastThrowLeft;
    }

    private bool GunMode => spearGun != null && spearGun.gameObject.activeSelf;
    private bool DoubleFist => !GunMode && heldSpearL != null && turret != null && turret.Tier(doubleFistPath - 1) >= doubleFistTier;

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
        if (fishing != null && fishing.Holding)
        {
            PoseFishing();
            return;
        }
        if (GunMode)
        {
            PoseGun(p, wind);
            return;
        }
        if (DoubleFist)
        {
            PoseDoubleFist(p, wind);
            return;
        }
        if (heldSpearL != null && heldSpearL.gameObject.activeSelf)
        {
            heldSpearL.gameObject.SetActive(false);
        }

        Vector3 right = ThrowPaw(p, GrabPoint());
        float follow = Follow(p);
        Vector3 left = Vector3.Lerp(restLeft, throwLeft, follow);
        float leanAngle = throwLean * follow;
        Vector3 scale = Vector3.Lerp(Vector3.one, throwSquash, follow);
        Vector3 aim = readyAim;

        // Wind-up on top, toward the end of the recharge, while there is a target.
        float w = Wind(p, wind);
        if (w > 0f)
        {
            right = Vector3.Lerp(right, windRight, w);
            left = Vector3.Lerp(left, windLeft, w);
            leanAngle = Mathf.Lerp(leanAngle, windLean, w);
            scale = Vector3.Lerp(scale, windStretch, w);
            aim = Vector3.Lerp(readyAim, windAim, w);
        }

        ApplyBody(leanAngle, scale, left, right, Rattle(p));
        PlaceHeld(heldSpear, p, right, aim);
    }

    // Double Fist: each paw runs the throw cycle over two recharges, half a cycle apart.
    private void PoseDoubleFist(float p, float wind)
    {
        float last = p * 0.5f;
        float next = 0.5f + p * 0.5f;
        float qR = lastThrowLeft ? next : last;
        float qL = lastThrowLeft ? last : next;
        Vector3 grab = GrabPoint();
        Vector3 right = ThrowPaw(qR, grab);
        Vector3 left = Mirror(ThrowPaw(qL, Mirror(grab)));
        float wR = Wind(qR, wind);
        float wL = Wind(qL, wind);
        right = Vector3.Lerp(right, windRight, wR);
        left = Vector3.Lerp(left, Mirror(windRight), wL);

        float follow = Follow(last);
        float w = Mathf.Max(wR, wL);
        float leanAngle = Mathf.Lerp(throwLean * follow, windLean, w);
        Vector3 scale = Vector3.Lerp(Vector3.Lerp(Vector3.one, throwSquash, follow), windStretch, w);

        ApplyBody(leanAngle, scale, left, right, Rattle(qR) + Rattle(qL));
        PlaceHeld(heldSpear, qR, right, Vector3.Lerp(readyAim, windAim, wR));
        PlaceHeld(heldSpearL, qL, left, Mirror(Vector3.Lerp(readyAim, windAim, wL)));
    }

    // Fishing (Path 3 shark attacks): the cat holds the rod (FishercatFishing poses it), leans back reeling in jerks with
    // the right paw cranking the reel, then winds back and casts forward.
    private void PoseFishing()
    {
        if (heldSpear != null && heldSpear.gameObject.activeSelf)
        {
            heldSpear.gameObject.SetActive(false);
        }
        if (heldSpearL != null && heldSpearL.gameObject.activeSelf)
        {
            heldSpearL.gameObject.SetActive(false);
        }
        float k = fishing.PhaseProgress;
        float leanAngle = 0f;
        Vector3 scale = Vector3.one;
        bool crank = false;
        switch (fishing.CurrentPhase)
        {
            case FishercatFishing.Phase.Grab:
                leanAngle = Mathf.Lerp(0f, fishReelLean * 0.5f, Ease(k));
                break;
            case FishercatFishing.Phase.Reel:
                leanAngle = fishReelLean + 6f * Mathf.Sin(k * Mathf.PI * 7f);
                scale = windStretch;
                crank = true;
                break;
            case FishercatFishing.Phase.Retract:
                leanAngle = fishReelLean * 0.7f;
                crank = true;
                break;
            case FishercatFishing.Phase.Cast:
                leanAngle = k < 0.4f ? Mathf.Lerp(fishReelLean * 0.7f, fishCastBackLean, Ease(k / 0.4f))
                    : Mathf.Lerp(fishCastBackLean, fishCastForwardLean, Ease((k - 0.4f) / 0.6f));
                scale = k < 0.4f ? windStretch : Vector3.Lerp(throwSquash, Vector3.one, Ease((k - 0.4f) / 0.6f));
                break;
            case FishercatFishing.Phase.PutBack:
                leanAngle = Mathf.Lerp(fishCastForwardLean, 0f, Ease(k));
                break;
        }
        if (lean != null)
        {
            lean.localRotation = Quaternion.Euler(leanAngle, 0f, 0f);
            lean.localScale = scale;
        }
        if (lean == null)
        {
            return;
        }
        Vector3 left = lean.InverseTransformPoint(fishing.HandleWorld);
        Vector3 right = lean.InverseTransformPoint(fishing.ReelWorld);
        if (crank)
        {
            float a = Time.time * crankSpeed;
            right += new Vector3(0.05f, Mathf.Cos(a) * crankRadius, Mathf.Sin(a) * crankRadius);
        }
        else
        {
            right += new Vector3(0.05f, 0f, 0f);
        }
        if (handL != null)
        {
            handL.localPosition = left;
        }
        if (handR != null)
        {
            handR.localPosition = right;
        }
    }

    // Spear gun: held at the hip, kicks back on each shot, the left paw pumps the foregrip.
    private void PoseGun(float p, float wind)
    {
        if (heldSpear != null && heldSpear.gameObject.activeSelf)
        {
            heldSpear.gameObject.SetActive(false);
        }
        if (heldSpearL != null && heldSpearL.gameObject.activeSelf)
        {
            heldSpearL.gameObject.SetActive(false);
        }
        float kick = p < recoilTime ? 1f - Ease(p / recoilTime) : 0f;
        if (reel != null && reel.Jerk > 0f)
        {
            kick = Mathf.Max(kick, reel.Jerk * reelJerkScale);
        }
        float w = Wind(p, wind);
        Vector3 position = gunRestPosition + new Vector3(0f, recoilUp * kick + readyBob * Mathf.Sin(p * Mathf.PI * 6f) * (1f - w), -recoilBack * kick);
        Quaternion rotation = gunRestRotation * Quaternion.Euler(-recoilPitch * kick, 0f, 0f);
        spearGun.localPosition = position;
        spearGun.localRotation = rotation;

        float pump = p > pumpFrom && p < pumpTo ? Mathf.Sin(Mathf.InverseLerp(pumpFrom, pumpTo, p) * Mathf.PI) : 0f;
        Vector3 right = position + rotation * gripRight;
        Vector3 left = position + rotation * (gripLeft + Vector3.back * (pumpDistance * pump));
        float leanAngle = recoilLean * kick + gunAimLean * w;
        Vector3 scale = Vector3.Lerp(Vector3.one, throwSquash, kick * 0.5f);
        ApplyBody(leanAngle, scale, left, right, Vector2.zero);
    }

    // The throwing paw (right-side poses) over one cycle: follow-through -> settle -> barrel -> ready (no wind-up).
    private Vector3 ThrowPaw(float p, Vector3 grab)
    {
        if (p < settleTime)
        {
            return Vector3.Lerp(throwRight, settleRight, Ease(p / Mathf.Max(0.0001f, settleTime)));
        }
        if (p < reachFrom)
        {
            return settleRight;
        }
        if (p < grabFrom)
        {
            return Vector3.Lerp(settleRight, grab + Vector3.up * grabDip, Ease(Mathf.InverseLerp(reachFrom, grabFrom, p)));
        }
        if (p < grabTo)
        {
            float k = Mathf.InverseLerp(grabFrom, grabTo, p);
            return grab + Vector3.up * (grabDip * Mathf.Abs(Mathf.Cos(k * Mathf.PI * grabDips)));
        }
        if (p < readyAt)
        {
            return Vector3.Lerp(grab + Vector3.up * grabDip, readyRight, Ease(Mathf.InverseLerp(grabTo, readyAt, p)));
        }
        return readyRight + Vector3.up * (readyBob * Mathf.Sin(p * Mathf.PI * 6f));
    }

    // 1 right after a throw, easing to 0 by settleTime.
    private float Follow(float p)
    {
        return p < settleTime ? 1f - Ease(p / Mathf.Max(0.0001f, settleTime)) : 0f;
    }

    private float Wind(float p, float wind)
    {
        return wind * Ease(Mathf.InverseLerp(windFrom, 1f, p));
    }

    // The barrel's rattle while a paw rummages: (squash wave, tilt wave).
    private Vector2 Rattle(float p)
    {
        float u = Mathf.InverseLerp(rummageFrom, rummageTo, p);
        float envelope = p > rummageFrom && p < rummageTo ? Mathf.Sin(u * Mathf.PI) : 0f;
        return new Vector2(Mathf.Sin(u * rummageWobbles * Mathf.PI * 2f) * envelope,
            Mathf.Sin(u * rummageWobbles * Mathf.PI * 2.7f + 1f) * envelope);
    }

    private void ApplyBody(float leanAngle, Vector3 scale, Vector3 left, Vector3 right, Vector2 rattle)
    {
        if (barrel != null)
        {
            float wave = rattle.x;
            barrel.localScale = Vector3.Scale(barrelScale, new Vector3(1f + rummageSquash * wave, 1f - rummageSquash * wave, 1f + rummageSquash * wave));
            barrel.localRotation = barrelRotation * Quaternion.Euler(rummageTilt * rattle.y, 0f, rummageTilt * wave);
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
    }

    // A held spear: pulled out standing up at spearShown, turned to point ahead by readyAt, gone after the throw.
    private void PlaceHeld(Transform spear, float p, Vector3 paw, Vector3 aim)
    {
        if (spear == null)
        {
            return;
        }
        bool held = p >= spearShown;
        if (spear.gameObject.activeSelf != held)
        {
            spear.gameObject.SetActive(held);
        }
        if (held)
        {
            float level = Ease(Mathf.InverseLerp(spearShown, readyAt, p));
            Vector3 dir = Vector3.Slerp(Vector3.up, aim.normalized, level);
            spear.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            spear.localPosition = paw + dir * gripOffset;
        }
    }

    private static Vector3 Mirror(Vector3 v)
    {
        return new Vector3(-v.x, v.y, v.z);
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
