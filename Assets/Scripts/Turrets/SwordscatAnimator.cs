/*
 * SwordscatAnimator
 * The SwordsCat's sword animations (tower 5, user 2026-11-25), driven by its attacks; purely visual, nothing about
 * firing or swinging changes.
 *
 * - Wave shot (TurretGun.Fired): a quick "flick" - the blade slides out of the sheath (flickDistance) while the scabbard
 *   hand pulls the sheath back (sheathPull, tilted by sheathTilt; the sheath paw is the sheath's child) and the body
 *   leans and twists into the draw (flickLean, flickTwist); holds a moment, then everything snaps back, like an iaido cut
 *   (exaggerated, user 2026-11-25).
 * - Slash effect (user 2026-11-25, shows the swing's range): a flat pale arc at slashHeight above the tower, from the
 *   aim point out to the swing's HitReach over its ArcAngle, centred on the swing direction; it sweeps in from the cat's
 *   right to its left in step with the blade, then fades over slashFade. Built at run time from the swing's stats
 *   (vertex colours on a Sprites/Default material), so it always matches the real hit area. Since the same day it is
 *   layered: a tapered crescent with a pale glow band, a few trailing speed-line streaks and a white-hot core on the
 *   edge; fading, it thins toward the edge and drifts outward (slashGlow, slashCore, slashStreaks, slashExpand).
 *   With the swing's path wedge (2026-11-25) its edge follows TurretSwing.HitReachToward in each direction, so it
 *   reaches further inside the cone facing the nearest path; band widths still follow the base HitReach.
 * - Blade gleam (user 2026-11-25): a four-point star glint (Gleam child of the blade, run-time mesh, turned to the camera)
 *   runs from the guard to the tip late in the sweep, and the steel (bladeRenderer slot steelIndex, a property block)
 *   flashes toward flashColor through the sweep and fades back over flashFade. The wave flick gets the same glint
 *   (along the drawn steel, flickGleamTime) and flash (user, same day).
 * - Swing (TurretSwing.Swung): the blade is drawn fully out along the sheath, turned out to the cat's right, swept in an
 *   arc across the front to the left, held there in a follow-through pose (holdTime) with a small forward lean, then
 *   brought back and slid quickly into the sheath. While a swing plays, wave flicks are ignored.
 * - On each swing the cat snaps to face the swing direction (TurretGun.SnapTo, offset by the body's stance yaw), so the
 *   blade's arc and the slash line up with where the hit lands (user 2026-11-25); for the rest of the swing the body
 *   keeps facing that direction even while the gun turns.
 * - The blade transform (hilt, guard, steel; the hilt paw is its child) moves in the Body's space; its rest pose is the
 *   sheath's. Poses are the blade's hilt point (the paw) and its direction (+Z = toward the tip), in the Body's space.
 * - Both timelines shrink to fit within timeShare of their attack interval, so they scale with attack speed.
 */

using UnityEngine;

public class SwordscatAnimator : MonoBehaviour
{
    private enum State { Idle, Flick, Swing }

    [SerializeField] private TurretGun gun;
    [SerializeField] private TurretSwing swing;
    [Tooltip("The scaled Body that holds the cat, the sheath and the blade.")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform blade;
    [Tooltip("Blade-space distance from the guard back to the hilt paw.")]
    [SerializeField] private float hiltOffset = 0.17f;
    [Tooltip("An animation never takes longer than this share of its attack interval.")]
    [SerializeField, Range(0.1f, 1f)] private float timeShare = 0.9f;

    [SerializeField] private Transform sheath;

    [Header("Wave shot: flick")]
    [SerializeField] private float flickDistance = 0.5f;
    [Tooltip("How far the scabbard hand pulls the sheath back along its length.")]
    [SerializeField] private float sheathPull = 0.14f;
    [Tooltip("Degrees the sheath tilts down at its tip while pulled.")]
    [SerializeField] private float sheathTilt = 18f;
    [SerializeField] private float flickLean = 9f;
    [SerializeField] private float flickTwist = -16f;
    [SerializeField] private float flickOut = 0.06f;
    [SerializeField] private float flickHold = 0.16f;
    [SerializeField] private float flickIn = 0.07f;
    [Tooltip("How long the star glint takes to run along the drawn steel during a flick.")]
    [SerializeField] private float flickGleamTime = 0.16f;

    [Header("Slash effect")]
    [SerializeField] private MeshFilter slash;
    [SerializeField] private float slashHeight = 1.0f;
    [Tooltip("Inner edge of the arc as a share of the reach (0 = a full fan).")]
    [SerializeField, Range(0f, 0.95f)] private float slashInner = 0.45f;
    [SerializeField] private float slashFade = 0.25f;
    [SerializeField] private Color slashColor = new Color(0.82f, 0.92f, 1f, 0.75f);
    [SerializeField, Range(4, 64)] private int slashSegments = 28;
    [Tooltip("How far the pale glow reaches past the edge (share of the reach).")]
    [SerializeField, Range(0f, 0.3f)] private float slashGlow = 0.07f;
    [Tooltip("Width of the white-hot core on the edge (share of the reach).")]
    [SerializeField, Range(0f, 0.3f)] private float slashCore = 0.07f;
    [Tooltip("How much the arc drifts outward while it fades (share of the reach).")]
    [SerializeField, Range(0f, 0.3f)] private float slashExpand = 0.08f;
    [SerializeField, Range(0, 6)] private int slashStreaks = 3;

    [Header("Blade gleam")]
    [SerializeField] private MeshFilter gleam;
    [Tooltip("World size of the star glint at its biggest.")]
    [SerializeField] private float gleamSize = 0.55f;
    [SerializeField] private float gleamTime = 0.18f;
    [Tooltip("When the glint starts, as a share of the sweep.")]
    [SerializeField, Range(0f, 1f)] private float gleamStart = 0.5f;
    [Tooltip("How far along the blade (blade space, guard = 0) the glint runs.")]
    [SerializeField] private float gleamReach = 0.8f;
    [SerializeField] private Renderer bladeRenderer;
    [Tooltip("The blade renderer's steel material slot.")]
    [SerializeField] private int steelIndex = 5;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashFade = 0.2f;

    [Header("Swing")]
    [SerializeField] private float drawDistance = 0.9f;
    [SerializeField] private float drawTime = 0.05f;
    [SerializeField] private float turnTime = 0.04f;
    [SerializeField] private float sweepTime = 0.08f;
    [SerializeField] private float holdTime = 0.35f;
    [SerializeField] private float returnTime = 0.1f;
    [SerializeField] private float sheatheTime = 0.06f;
    [SerializeField] private Vector3 startHilt = new Vector3(0.38f, 0.32f, 0.32f);
    [SerializeField] private Vector3 startDirection = new Vector3(1f, 0.1f, 0.35f);
    [SerializeField] private Vector3 endHilt = new Vector3(-0.32f, 0.28f, 0.42f);
    [SerializeField] private Vector3 endDirection = new Vector3(-1f, -0.2f, 0.3f);
    [Tooltip("How far the hilt bulges forward mid-sweep.")]
    [SerializeField] private float sweepBulge = 0.12f;
    [Tooltip("Forward lean of the body (degrees) through the sweep and hold.")]
    [SerializeField] private float holdLean = 10f;

    private State state;
    private float time;
    private float speed = 1f;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Quaternion bodyRest;
    private Vector3 sheathPosition;
    private Quaternion sheathRotation;
    private Vector3 slashDirection = Vector3.forward;
    private Mesh slashMesh;
    private MeshRenderer slashRenderer;
    private readonly System.Collections.Generic.List<Vector3> slashVertices = new System.Collections.Generic.List<Vector3>();
    private readonly System.Collections.Generic.List<Color> slashColors = new System.Collections.Generic.List<Color>();
    private readonly System.Collections.Generic.List<int> slashTriangles = new System.Collections.Generic.List<int>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MeshRenderer gleamRenderer;
    private Mesh gleamMesh;
    private MaterialPropertyBlock flashBlock;
    private Color steelColor = Color.white;

    private void Awake()
    {
        restPosition = blade.localPosition;
        restRotation = blade.localRotation;
        bodyRest = body.localRotation;
        if (sheath != null)
        {
            sheathPosition = sheath.localPosition;
            sheathRotation = sheath.localRotation;
        }
        if (slash != null)
        {
            slashMesh = new Mesh { name = "SwordscatSlash" };
            slashMesh.MarkDynamic();
            slash.sharedMesh = slashMesh;
            slashRenderer = slash.GetComponent<MeshRenderer>();
            slashRenderer.enabled = false;
        }
        if (gleam != null)
        {
            gleamMesh = BuildGleamMesh();
            gleam.sharedMesh = gleamMesh;
            gleamRenderer = gleam.GetComponent<MeshRenderer>();
            gleamRenderer.enabled = false;
        }
        flashBlock = new MaterialPropertyBlock();
        if (bladeRenderer != null && steelIndex < bladeRenderer.sharedMaterials.Length && bladeRenderer.sharedMaterials[steelIndex] != null
            && bladeRenderer.sharedMaterials[steelIndex].HasProperty(BaseColorId))
        {
            steelColor = bladeRenderer.sharedMaterials[steelIndex].GetColor(BaseColorId);
        }
    }

    private void OnDestroy()
    {
        if (slashMesh != null)
        {
            Destroy(slashMesh);
        }
        if (gleamMesh != null)
        {
            Destroy(gleamMesh);
        }
    }

    // A soft four-point star in the XY plane (size 1 = tip to centre): bright centre, long clear tips on the axes,
    // short dim points on the diagonals; vertex colours (Sprites/Default).
    private static Mesh BuildGleamMesh()
    {
        var vertices = new System.Collections.Generic.List<Vector3> { Vector3.zero };
        var colors = new System.Collections.Generic.List<Color> { Color.white };
        var triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            bool tip = i % 2 == 0;
            float r = tip ? 1f : 0.22f;
            vertices.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            colors.Add(new Color(1f, 1f, 1f, tip ? 0f : 0.55f));
        }
        for (int i = 0; i < 8; i++)
        {
            triangles.Add(0);
            triangles.Add(1 + i);
            triangles.Add(1 + (i + 1) % 8);
        }
        var mesh = new Mesh { name = "SwordscatGleam" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnEnable()
    {
        if (gun != null)
        {
            gun.Fired += OnFired;
        }
        if (swing != null)
        {
            swing.Swung += OnSwung;
        }
    }

    private void OnDisable()
    {
        if (gun != null)
        {
            gun.Fired -= OnFired;
        }
        if (swing != null)
        {
            swing.Swung -= OnSwung;
        }
        state = State.Idle;
        ResetPose();
    }

    private void ResetPose()
    {
        blade.localPosition = restPosition;
        blade.localRotation = restRotation;
        body.localRotation = bodyRest;
        if (sheath != null)
        {
            sheath.localPosition = sheathPosition;
            sheath.localRotation = sheathRotation;
        }
        if (slashRenderer != null)
        {
            slashRenderer.enabled = false;
        }
        if (gleamRenderer != null)
        {
            gleamRenderer.enabled = false;
        }
        if (bladeRenderer != null && flashBlock != null)
        {
            flashBlock.Clear();
            bladeRenderer.SetPropertyBlock(flashBlock, steelIndex);
        }
    }

    private void OnFired()
    {
        if (state == State.Swing)
        {
            return;
        }
        Begin(State.Flick, flickOut + flickHold + flickIn, gun.CurrentStats.AttackInterval);
    }

    private void OnSwung(Vector3 direction)
    {
        slashDirection = direction;
        if (gun != null)
        {
            // Face the swing at once: the body sits turned on the gun (the stance yaw), so offset by it.
            gun.SnapTo(Quaternion.Inverse(Quaternion.Euler(0f, bodyRest.eulerAngles.y, 0f)) * direction);
        }
        Begin(State.Swing, drawTime + turnTime + sweepTime + holdTime + returnTime + sheatheTime, swing.AttackInterval);
    }

    private void Begin(State next, float total, float interval)
    {
        state = next;
        time = 0f;
        float room = interval * timeShare;
        speed = total > room && room > 0.01f ? total / room : 1f;
    }

    // LateUpdate: after the gun has turned this frame, so the pose (and the swing's body facing) always wins.
    private void LateUpdate()
    {
        if (state == State.Idle)
        {
            return;
        }
        time += Time.deltaTime * speed;
        bool done = state == State.Flick ? Flick(time) : Swing(time);
        if (done)
        {
            state = State.Idle;
            ResetPose();
        }
    }

    // Returns true when finished.
    private bool Flick(float t)
    {
        float amount;
        if (t < flickOut)
        {
            amount = EaseOut(t / flickOut);
        }
        else if (t < flickOut + flickHold)
        {
            amount = 1f;
        }
        else if (t < flickOut + flickHold + flickIn)
        {
            amount = 1f - EaseIn((t - flickOut - flickHold) / flickIn);
        }
        else
        {
            return true;
        }
        if (sheath != null)
        {
            // The sheath slides back and tilts; the blade is drawn out of it along its new line (blade rest = sheath rest).
            Quaternion tilted = sheathRotation * Quaternion.Euler(sheathTilt * amount, 0f, 0f);
            Vector3 mouth = sheathPosition + sheathRotation * Vector3.forward * (sheathPull * amount);
            sheath.localPosition = mouth;
            sheath.localRotation = tilted;
            SetPose(mouth - tilted * Vector3.forward * (flickDistance * amount), tilted);
        }
        else
        {
            SetDrawn(amount * flickDistance);
        }
        body.localRotation = bodyRest * Quaternion.Euler(flickLean * amount, flickTwist * amount, 0f);
        // The glint runs along the bit of steel that is out of the sheath; the steel flashes as it clears the mouth.
        ShowGleam((t - flickOut * 0.5f) / Mathf.Max(0.01f, flickGleamTime), 0.08f, flickDistance * 0.85f);
        SetFlash(t < flickOut ? t / flickOut : 1f - (t - flickOut) / Mathf.Max(0.01f, flashFade));
        return false;
    }

    private bool Swing(float t)
    {
        UpdateSlash(t - drawTime - turnTime);
        if (sheath != null)
        {
            // A swing can cut a flick short: the sheath goes straight back to rest.
            sheath.localPosition = sheathPosition;
            sheath.localRotation = sheathRotation;
        }
        float lean = 0f;
        if (t < drawTime)
        {
            SetDrawn(EaseOut(t / drawTime) * drawDistance);
        }
        else if ((t -= drawTime) < turnTime)
        {
            Blend(DrawnPosition(drawDistance), restRotation, HiltPose(startHilt, startDirection), Look(startDirection), EaseOut(t / turnTime));
        }
        else if ((t -= turnTime) < sweepTime)
        {
            float u = EaseOut(t / sweepTime);
            Vector3 hilt = Vector3.Lerp(startHilt, endHilt, u) + Vector3.forward * (sweepBulge * Mathf.Sin(u * Mathf.PI));
            Quaternion rotation = Quaternion.Slerp(Look(startDirection), Look(endDirection), u);
            blade.localRotation = rotation;
            blade.localPosition = hilt + rotation * Vector3.forward * hiltOffset;
            lean = holdLean * u;
        }
        else if ((t -= sweepTime) < holdTime)
        {
            SetPose(HiltPose(endHilt, endDirection), Look(endDirection));
            lean = holdLean;
        }
        else if ((t -= holdTime) < returnTime)
        {
            float u = EaseInOut(t / returnTime);
            Blend(HiltPose(endHilt, endDirection), Look(endDirection), DrawnPosition(drawDistance), restRotation, u);
            lean = holdLean * (1f - u);
        }
        else if ((t -= returnTime) < sheatheTime)
        {
            SetDrawn((1f - EaseIn(t / sheatheTime)) * drawDistance);
        }
        else
        {
            return true;
        }
        // The body keeps facing the swing for the whole swing, whatever the gun does meanwhile.
        Vector3 facing = new Vector3(slashDirection.x, 0f, slashDirection.z);
        body.rotation = (facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : body.parent.rotation) * Quaternion.Euler(lean, 0f, 0f);
        return false;
    }

    // The slash arc and the blade gleam: s = seconds since the sweep began (negative before it). The arc reveals over
    // sweepTime, then fades over slashFade.
    private void UpdateSlash(float s)
    {
        UpdateGleam(s);
        if (slashRenderer == null)
        {
            return;
        }
        float reveal = Mathf.Clamp01(s / Mathf.Max(0.01f, sweepTime));
        float fade = Mathf.Clamp01((s - sweepTime) / Mathf.Max(0.01f, slashFade));
        if (s <= 0f || fade >= 1f)
        {
            slashRenderer.enabled = false;
            return;
        }
        slashRenderer.enabled = true;
        Transform t = slash.transform;
        t.position = new Vector3(swing.Owner.position.x, transform.position.y + slashHeight, swing.Owner.position.z);
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one;
        BuildSlash(EaseOut(reveal), fade);
    }

    // Layers, back to front: a pale glow band around the edge, speed-line streaks trailing behind, a white-hot core on
    // the cutting edge. The crescent is thickest mid-arc and tapers at both ends (the head stays full while it sweeps);
    // while fading it thins toward its edge and drifts outward by slashExpand.
    private void BuildSlash(float reveal, float fade)
    {
        slashVertices.Clear();
        slashColors.Clear();
        slashTriangles.Clear();
        Vector3 forward = new Vector3(slashDirection.x, 0f, slashDirection.z);
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        float alpha = 1f - fade;
        // The edge sits at the swing's reach in each direction (longer inside the path wedge); widths follow the base reach.
        float expand = 1f + slashExpand * fade;
        float reach = swing.HitReach * expand;
        float arc = swing.ArcAngle;
        float thin = 1f - 0.75f * fade;
        int count = Mathf.Max(2, Mathf.CeilToInt(slashSegments * reveal));
        Color white = Color.white;

        // Glow: from the crescent's inner edge up to the edge (fading in), then a little past it (fading out).
        for (int layer = 0; layer < 2; layer++)
        {
            for (int i = 0; i <= count; i++)
            {
                float u = reveal * i / count;
                float taper = Taper(u, reveal, fade);
                float lead = Mathf.Lerp(0.55f, 1f, i / (float)count);
                Color glow = slashColor;
                glow.a *= alpha * lead;
                Color clear = glow;
                clear.a = 0f;
                float width = reach * (1f - slashInner) * Mathf.Max(0.06f, taper) * thin;
                Vector3 d = ArcDirection(forward, arc, u);
                float edge = swing.HitReachToward(d) * expand;
                if (layer == 0)
                {
                    StripPoint(d, edge - width, edge, clear, glow, i > 0);
                }
                else
                {
                    StripPoint(d, edge, edge + reach * slashGlow * Mathf.Max(0.2f, taper), glow, clear, i > 0);
                }
            }
        }

        // Streaks: thin lines inside the crescent, trailing behind the head, fading faster than the rest.
        for (int k = 0; k < slashStreaks; k++)
        {
            float share = slashStreaks > 1 ? k / (float)(slashStreaks - 1) : 0.5f;
            float radiusShare = Mathf.Lerp(slashInner + 0.12f, 0.9f, share);
            float end = reveal * (0.5f + 0.2f * share);
            int n = Mathf.Max(2, Mathf.CeilToInt(slashSegments * end));
            Color streak = white;
            streak.a = 0.6f * alpha * alpha;
            for (int i = 0; i <= n; i++)
            {
                float u = end * i / n;
                Color c = streak;
                c.a *= Mathf.Max(0f, Mathf.Sin(Mathf.PI * i / n)) * Taper(u, reveal, fade);
                float w = reach * 0.018f;
                Vector3 d = ArcDirection(forward, arc, u);
                float radius = swing.HitReachToward(d) * expand * radiusShare;
                StripPoint(d, radius - w, radius + w, c, c, i > 0);
            }
        }

        // Core: the white-hot cutting edge.
        for (int i = 0; i <= count; i++)
        {
            float u = reveal * i / count;
            float taper = Taper(u, reveal, fade);
            Color hot = white;
            hot.a = alpha;
            Color soft = white;
            soft.a = 0.3f * alpha;
            float width = reach * slashCore * Mathf.Max(0.15f, taper) * (1f - 0.5f * fade);
            Vector3 d = ArcDirection(forward, arc, u);
            float edge = swing.HitReachToward(d) * expand;
            StripPoint(d, edge - width, edge, soft, hot, i > 0);
        }

        slashMesh.Clear();
        slashMesh.SetVertices(slashVertices);
        slashMesh.SetColors(slashColors);
        slashMesh.SetTriangles(slashTriangles, 0);
        slashMesh.RecalculateBounds();
    }

    // Thickness along the arc (u = 0 at the cat's right .. 1 at its left): a crescent, but the head stays full while the
    // arc is still sweeping out.
    private static float Taper(float u, float reveal, float fade)
    {
        // Max(0, ...): sin(pi) is a hair below zero, and a negative base gives Pow a NaN.
        float crescent = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(u))), 0.6f);
        if (fade <= 0f && reveal < 1f)
        {
            float head = Mathf.Clamp01((reveal - u) / 0.15f);
            crescent = Mathf.Max(crescent, 1f - head);
        }
        return crescent;
    }

    // From the cat's right (+half, clockwise seen from above) to its left.
    private static Vector3 ArcDirection(Vector3 forward, float arc, float u)
    {
        return Quaternion.AngleAxis(arc * 0.5f - arc * u, Vector3.up) * forward;
    }

    private void StripPoint(Vector3 direction, float innerRadius, float outerRadius, Color innerColor, Color outerColor, bool join)
    {
        slashVertices.Add(direction * innerRadius);
        slashColors.Add(innerColor);
        slashVertices.Add(direction * outerRadius);
        slashColors.Add(outerColor);
        if (join)
        {
            int k = slashVertices.Count - 4;
            slashTriangles.Add(k);
            slashTriangles.Add(k + 1);
            slashTriangles.Add(k + 3);
            slashTriangles.Add(k);
            slashTriangles.Add(k + 3);
            slashTriangles.Add(k + 2);
        }
    }

    // The swing's blade gleam: the star glint runs from the guard to the tip late in the sweep, and the steel flashes
    // toward flashColor through the sweep and fades back.
    private void UpdateGleam(float s)
    {
        ShowGleam((s - sweepTime * gleamStart) / Mathf.Max(0.01f, gleamTime), 0.12f, gleamReach);
        SetFlash(s <= 0f ? 0f : s < sweepTime ? s / sweepTime : 1f - (s - sweepTime) / Mathf.Max(0.01f, flashFade));
    }

    // The four-point star glint at progress p (0..1, hidden outside), sliding along the blade from `from` to `to` (blade
    // space, guard = 0), turned to the camera, popping up and back down.
    private void ShowGleam(float p, float from, float to)
    {
        if (gleamRenderer == null)
        {
            return;
        }
        bool show = p > 0f && p < 1f;
        gleamRenderer.enabled = show;
        if (!show)
        {
            return;
        }
        Transform g = gleam.transform;
        g.position = blade.TransformPoint(new Vector3(0f, 0.02f, Mathf.Lerp(from, to, EaseOut(p))));
        Camera cam = Camera.main;
        Quaternion face = cam != null ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up) : Quaternion.identity;
        g.rotation = face * Quaternion.Euler(0f, 0f, 45f * p);
        float size = gleamSize * Mathf.Sin(Mathf.PI * p);
        g.localScale = Vector3.one * (size / Mathf.Max(0.0001f, blade.lossyScale.x));
    }

    // The steel's flash, 0 = its own colour, 1 = flashColor.
    private void SetFlash(float amount)
    {
        if (bladeRenderer == null)
        {
            return;
        }
        amount = Mathf.Clamp01(amount);
        flashBlock.Clear();
        if (amount > 0f)
        {
            flashBlock.SetColor(BaseColorId, Color.Lerp(steelColor, flashColor, amount));
        }
        bladeRenderer.SetPropertyBlock(flashBlock, steelIndex);
    }

    private Vector3 DrawnPosition(float distance)
    {
        return restPosition - restRotation * Vector3.forward * distance;
    }

    private void SetDrawn(float distance)
    {
        SetPose(DrawnPosition(distance), restRotation);
    }

    private Vector3 HiltPose(Vector3 hilt, Vector3 direction)
    {
        return hilt + direction.normalized * hiltOffset;
    }

    private static Quaternion Look(Vector3 direction)
    {
        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void SetPose(Vector3 position, Quaternion rotation)
    {
        blade.localPosition = position;
        blade.localRotation = rotation;
    }

    private void Blend(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, float u)
    {
        SetPose(Vector3.Lerp(fromPosition, toPosition, u), Quaternion.Slerp(fromRotation, toRotation, u));
    }

    private static float EaseOut(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - (1f - x) * (1f - x);
    }

    private static float EaseIn(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x;
    }

    private static float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    /// <summary>For pose checks in the editor: sets the pose at time t of a swing (or a flick) without playing it.</summary>
    public void PreviewPose(bool swingPose, float t)
    {
        if (swingPose)
        {
            Swing(t);
        }
        else
        {
            Flick(t);
        }
    }
}
