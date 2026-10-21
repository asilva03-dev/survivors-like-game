/*
 * PlayerFacing
 * On the Player: turns the cat model around the vertical axis so its face points at the mouse cursor, always.
 *
 * - model is the cat child (Player/Model/CatModel). Its local rotation at start is the rest pose: standing
 *   upright with the face toward -Z (catplayer.fbx is Z-up with the face on its -Y side, so the rest pose is
 *   X 270, Y 180). The script only adds a yaw around the world up axis on top of that pose.
 * - The cursor is read as a ray onto the player's ground plane (same as the axe). Does nothing while the
 *   cursor is within deadZone of the player (no clear direction) or when there is no mouse.
 * - The turn is smoothed with an ease-out: each frame it closes a fraction of the remaining angle, so big
 *   turns start fast and settle softly. turnSharpness sets how quickly (about 1/turnSharpness seconds to
 *   cover ~63% of the gap; 10 = snappy but soft, 5 = floaty, 0 = snap instantly). It always takes the short
 *   way round.
 * - The root never rotates (Rigidbody, collider and the hit jitter stay put); only the model turns.
 */

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerFacing : MonoBehaviour
{
    [SerializeField] private Transform model;
    [SerializeField, Min(0f)] private float turnSharpness = 10f;
    [SerializeField, Min(0.01f)] private float deadZone = 0.3f;

    private Quaternion restRotation;
    private float yaw;
    private Camera cam;

    private void Awake()
    {
        restRotation = model != null ? model.localRotation : Quaternion.identity;
        yaw = 0f;
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        if (model == null || mouse == null)
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return;
            }
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, transform.position);
        if (ground.Raycast(ray, out float distance))
        {
            Vector3 toCursor = ray.GetPoint(distance) - transform.position;
            toCursor.y = 0f;
            if (toCursor.sqrMagnitude > deadZone * deadZone)
            {
                float wanted = Quaternion.LookRotation(-toCursor).eulerAngles.y;
                yaw = turnSharpness > 0f
                    ? Mathf.LerpAngle(yaw, wanted, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime))
                    : wanted;
            }
        }

        model.rotation = Quaternion.Euler(0f, yaw, 0f) * (model.parent != null ? model.parent.rotation : Quaternion.identity) * restRotation;
    }
}
