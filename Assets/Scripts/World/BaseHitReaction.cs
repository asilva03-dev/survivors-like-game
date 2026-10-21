/*
 * BaseHitReaction
 * On the Base prefab: purely visual reaction when the player's melee weapon (the axe) hits the base. No damage.
 *
 * - visual is a container at the base's ground pivot holding the model (Base > Visual > Model), so the bounce
 *   and the squash/stretch scale from the bottom of the box. This script owns its local position and scale.
 * - On a hit (IMeleeReactive.OnMeleeHit from MeleeWeapon): the box hops up bounceHeight over airTime,
 *   stretching tall at take-off and before landing, then squashes for landTime on landing. Width changes as
 *   1/sqrt(height scale). An optional sideways shake exists but is off (shakeAmplitude 0).
 * - flaps are the loose cardboard flaps (separate meshes). On Awake they move out of the scaling container into
 *   an unscaled "Flaps" container, so they never stretch or squash. Each flap's hinge is the box rim it hangs
 *   from: the side of box (the box body renderer) it sits on, at the flap's edge nearest that rim. Every frame
 *   the hinge follows the bouncing, squashing box rim, and the flap swings up and down around it (flapTilt
 *   degrees about the rim line, flapFrequency with a random phase/speed per flap, fading over flapDuration).
 *   Flaps only rotate about the hinge, and always end at their exact rest position and rotation.
 * - A charged swing multiplies the amplitudes by chargedMultiplier. A hit during a reaction restarts it.
 */

using UnityEngine;

public class BaseHitReaction : MonoBehaviour, IMeleeReactive
{
    [SerializeField] private Transform visual;
    [SerializeField] private Renderer box;
    [SerializeField] private Transform[] flaps;
    [SerializeField, Min(0f)] private float bounceHeight = 0.3f;
    [SerializeField, Min(0.05f)] private float airTime = 0.2f;
    [SerializeField, Min(0.01f)] private float landTime = 0.1f;
    [SerializeField, Range(0f, 1f)] private float stretch = 0.15f;
    [SerializeField, Range(0f, 0.9f)] private float squash = 0.2f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 0f;
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.3f;
    [SerializeField, Range(0f, 90f)] private float flapTilt = 15f;
    [SerializeField, Min(0.1f)] private float flapFrequency = 6f;
    [SerializeField, Min(0.01f)] private float flapDuration = 0.6f;
    [SerializeField, Min(1f)] private float chargedMultiplier = 1.5f;

    private Vector3 visualRest;
    private Vector3[] flapRest;
    private Quaternion[] flapRestRotation;
    private Vector3[] hinge;
    private Vector3[] hingeAxis;
    private float[] flapPhase;
    private float[] flapSpeed;
    private float elapsed = float.MaxValue;
    private float strength = 1f;

    private void Awake()
    {
        if (visual != null)
        {
            visualRest = visual.localPosition;
        }

        int count = flaps != null ? flaps.Length : 0;
        flapRest = new Vector3[count];
        flapRestRotation = new Quaternion[count];
        hinge = new Vector3[count];
        hingeAxis = new Vector3[count];
        flapPhase = new float[count];
        flapSpeed = new float[count];

        Transform holder = new GameObject("Flaps").transform;
        holder.SetParent(transform, false);

        // box extents in this object's space (rest pose: visual at the pivot, no scale)
        Bounds body = box != null ? LocalBounds(box) : new Bounds(Vector3.zero, Vector3.one);

        for (int i = 0; i < count; i++)
        {
            Transform flap = flaps[i];
            if (flap == null)
            {
                continue;
            }

            flap.SetParent(holder, true);
            flapRest[i] = flap.localPosition;
            flapRestRotation[i] = flap.localRotation;
            FindHinge(flap, body, out hinge[i], out hingeAxis[i]);
        }
    }

    // The rim the flap hangs from is the box side it lies toward; the hinge is the flap's edge nearest that rim.
    private void FindHinge(Transform flap, Bounds body, out Vector3 point, out Vector3 axis)
    {
        Bounds flapBounds = LocalBounds(flap.GetComponent<Renderer>());
        Vector3 offset = flapBounds.center - body.center;
        bool alongX = Mathf.Abs(offset.x) >= Mathf.Abs(offset.z);
        float rim = alongX
            ? body.center.x + Mathf.Sign(offset.x) * body.extents.x
            : body.center.z + Mathf.Sign(offset.z) * body.extents.z;

        Mesh mesh = flap.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh.vertices;
        float best = float.MaxValue;
        for (int v = 0; v < vertices.Length; v++)
        {
            Vector3 p = transform.InverseTransformPoint(flap.TransformPoint(vertices[v]));
            best = Mathf.Min(best, Mathf.Abs((alongX ? p.x : p.z) - rim));
        }

        // average the vertices along that edge (within a small band) to get the hinge line's middle
        Vector3 sum = Vector3.zero;
        int n = 0;
        for (int v = 0; v < vertices.Length; v++)
        {
            Vector3 p = transform.InverseTransformPoint(flap.TransformPoint(vertices[v]));
            if (Mathf.Abs((alongX ? p.x : p.z) - rim) <= best + 0.05f)
            {
                sum += p;
                n++;
            }
        }

        point = n > 0 ? sum / n : flapBounds.center;
        axis = alongX ? Vector3.forward : Vector3.right;
    }

    private Bounds LocalBounds(Renderer renderer)
    {
        Bounds world = renderer.bounds;
        Vector3 min = transform.InverseTransformPoint(world.min);
        Vector3 max = transform.InverseTransformPoint(world.max);
        Bounds local = new Bounds((min + max) * 0.5f, Vector3.zero);
        local.Encapsulate(min);
        local.Encapsulate(max);
        return local;
    }

    private void OnDisable()
    {
        elapsed = float.MaxValue;
        ResetPose();
    }

    public void OnMeleeHit(Vector3 from, bool charged)
    {
        elapsed = 0f;
        strength = charged ? chargedMultiplier : 1f;
        for (int i = 0; i < flapPhase.Length; i++)
        {
            flapPhase[i] = Random.Range(0f, 2f * Mathf.PI);
            flapSpeed[i] = Random.Range(0.85f, 1.2f);
        }
    }

    private void LateUpdate()
    {
        float total = Mathf.Max(airTime + landTime, Mathf.Max(shakeDuration, flapDuration));
        if (elapsed >= total)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= total)
        {
            ResetPose();
            return;
        }

        float height = 0f;
        float scaleY = 1f;
        if (elapsed < airTime)
        {
            float p = elapsed / airTime;
            height = bounceHeight * strength * 4f * p * (1f - p);
            scaleY = 1f + stretch * strength * Mathf.Abs(1f - 2f * p);
        }
        else if (elapsed < airTime + landTime)
        {
            float u = (elapsed - airTime) / landTime;
            scaleY = 1f - Mathf.Min(0.9f, squash * strength) * Mathf.Sin(u * Mathf.PI);
        }

        Vector3 shake = Vector3.zero;
        if (elapsed < shakeDuration && shakeAmplitude > 0f)
        {
            Vector2 r = Random.insideUnitCircle * (shakeAmplitude * strength * (1f - elapsed / shakeDuration));
            shake = new Vector3(r.x, 0f, r.y);
        }

        float width = 1f / Mathf.Sqrt(scaleY);
        Vector3 offset = visualRest + shake + Vector3.up * height;
        Vector3 boxScale = new Vector3(width, scaleY, width);
        if (visual != null)
        {
            visual.localPosition = offset;
            visual.localScale = boxScale;
        }

        float fade = elapsed < flapDuration ? 1f - elapsed / flapDuration : 0f;
        for (int i = 0; i < flapRest.Length; i++)
        {
            if (flaps[i] == null)
            {
                continue;
            }

            // the hinge rides on the bouncing, squashing rim; the flap itself never scales
            Vector3 movedHinge = offset + Vector3.Scale(boxScale, hinge[i] - visualRest);
            float angle = flapTilt * strength * fade
                * Mathf.Sin(elapsed * flapFrequency * flapSpeed[i] * 2f * Mathf.PI + flapPhase[i]);
            Quaternion swing = Quaternion.AngleAxis(angle, hingeAxis[i]);
            flaps[i].localPosition = movedHinge + swing * (flapRest[i] - hinge[i]);
            flaps[i].localRotation = swing * flapRestRotation[i];
        }
    }

    private void ResetPose()
    {
        if (visual != null)
        {
            visual.localPosition = visualRest;
            visual.localScale = Vector3.one;
        }

        for (int i = 0; flapRest != null && i < flapRest.Length; i++)
        {
            if (flaps[i] != null)
            {
                flaps[i].localPosition = flapRest[i];
                flaps[i].localRotation = flapRestRotation[i];
            }
        }
    }
}
