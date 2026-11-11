/*
 * DecorBush
 * A purely decorative bush (2026-11-10, ground decorations step 3): no collider, no health. GroundDecorations scatters
 * them (rarer than grass, flat ground only) from a pool; the player wades through them like trees (PlayerWading: slowed,
 * no dash/jump start) and they rustle.
 *
 * - Visual: the child holding the mesh; Rustle(push) tips it away along `push` (DOTween punch rotation + a small
 *   squash), skipped while a shake is still playing. The tween is killed and the visual reset when disabled (pooling).
 * - WadeRadius: flat distance from the centre at which the player is inside it (set by GroundDecorations per bush).
 */

using DG.Tweening;
using UnityEngine;

public class DecorBush : MonoBehaviour, IWadeable
{
    [SerializeField] private Transform visual;
    [Tooltip("Degrees the bush tips away from the player wading through.")]
    [SerializeField, Min(0f)] private float rustleAngle = 8f;
    [SerializeField] private Vector3 rustleScale = new Vector3(0.06f, -0.06f, 0.06f);
    [SerializeField, Min(0.05f)] private float rustleDuration = 0.45f;
    [SerializeField, Min(1)] private int rustleVibrato = 6;
    [SerializeField, Range(0f, 1f)] private float rustleElasticity = 0.5f;

    private Vector3 restScale = Vector3.one;
    private Quaternion restRotation = Quaternion.identity;

    public float WadeRadius { get; set; } = 1f;
    public Transform Visual => visual;

    public void SetVisual(Transform child)
    {
        visual = child;
    }

    /// <summary>Sets the visual's resting rotation/scale (call after changing them, before any rustle).</summary>
    public void CaptureRest()
    {
        if (visual != null)
        {
            restScale = visual.localScale;
            restRotation = visual.localRotation;
        }
    }

    public void Rustle(Vector3 push)
    {
        if (visual == null || DOTween.IsTweening(visual))
        {
            return;
        }
        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Vector3 axis = Vector3.Cross(Vector3.up, push.normalized);
        Transform space = visual.parent != null ? visual.parent : visual;
        Vector3 punch = space.InverseTransformDirection(axis).normalized * rustleAngle;
        visual.DOPunchRotation(punch, rustleDuration, rustleVibrato, rustleElasticity);
        visual.DOPunchScale(Vector3.Scale(rustleScale, restScale), rustleDuration, rustleVibrato, rustleElasticity);
    }

    private void OnDisable()
    {
        if (visual != null)
        {
            visual.DOKill();
            visual.localScale = restScale;
            visual.localRotation = restRotation;
        }
    }
}
