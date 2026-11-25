/*
 * AngelcatAnimator
 * Flight animation of the Angel Cat (tower 6, 2026-11-25, user): sitting upright on its cloud; through the takeoff it
 * tips forward until it lies face down (head toward where it flies, back up) and its wings flap the whole time it is
 * in the air. Driven by AngelcatFlight.Airborne (0 perched -> 1 flying).
 *
 * - Lying down: the Body (child of the turning Gun) pitches forward by flyPitch degrees about the middle of the round
 *   cat (BodyCentre), so it doesn't swing around its feet.
 * - Flapping: each wing turns about the body's up axis (which points along the flight once the cat lies down, so the
 *   wing tips go up and down) by flapAngle each way, once every flapPeriod seconds, starting with the takeoff.
 * - Runs in LateUpdate so it poses after the gun has turned. Wings' rest rotations are read in Awake (the prefab pose).
 * - Numbers are placeholders.
 */

using UnityEngine;

public class AngelcatAnimator : MonoBehaviour
{
    [SerializeField] private AngelcatFlight flight;
    [SerializeField] private Transform body;
    [SerializeField] private Transform wingL;
    [SerializeField] private Transform wingR;
    [Tooltip("The middle of the round cat in the Body's space (the point it pitches about).")]
    [SerializeField] private Vector3 bodyCentre = new Vector3(0f, 0.36f, 0f);
    [Tooltip("Degrees the cat tips forward while flying (90 = flat, face straight down).")]
    [SerializeField, Range(0f, 110f)] private float flyPitch = 75f;
    [Tooltip("Degrees each wing swings up and down from its rest pose.")]
    [SerializeField, Range(0f, 90f)] private float flapAngle = 35f;
    [Tooltip("Seconds for one full flap (down and back up).")]
    [SerializeField, Min(0.05f)] private float flapPeriod = 0.45f;

    private Vector3 bodyRestPosition;
    private Quaternion bodyRestRotation;
    private Quaternion wingLRest;
    private Quaternion wingRRest;
    private float flapTime;

    private void Awake()
    {
        if (body != null)
        {
            bodyRestPosition = body.localPosition;
            bodyRestRotation = body.localRotation;
        }
        if (wingL != null)
        {
            wingLRest = wingL.localRotation;
        }
        if (wingR != null)
        {
            wingRRest = wingR.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (flight == null || body == null)
        {
            return;
        }

        float air = flight.Airborne;
        float lie = air * air * (3f - 2f * air);

        // Pitch about the cat's middle: rotate, then shift so the middle stays put (in the Body's parent space).
        Quaternion pitch = Quaternion.AngleAxis(flyPitch * lie, Vector3.right);
        Quaternion rotation = bodyRestRotation * pitch;
        Vector3 scaledCentre = Vector3.Scale(bodyCentre, body.localScale);
        body.localRotation = rotation;
        body.localPosition = bodyRestPosition + bodyRestRotation * scaledCentre - rotation * scaledCentre;

        float flap = 0f;
        if (air > 0f)
        {
            flapTime += Time.deltaTime;
            flap = Mathf.Sin(flapTime * 2f * Mathf.PI / flapPeriod) * flapAngle * Mathf.Clamp01(air * 3f);
        }
        else
        {
            flapTime = 0f;
        }

        if (wingR != null)
        {
            wingR.localRotation = Quaternion.AngleAxis(flap, Vector3.up) * wingRRest;
        }
        if (wingL != null)
        {
            wingL.localRotation = Quaternion.AngleAxis(-flap, Vector3.up) * wingLRest;
        }
    }
}
