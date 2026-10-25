/*
 * CameraFollow
 * Keeps the target centered on screen for a fixed-angle top-down camera, with
 * extra lag while the player dashes, slides or leaps (PlayerController.IsBoosting).
 *
 * - Camera rotation is never changed; set it in the scene (pitch controls the view angle).
 * - Position is placed behind the target along the camera's forward axis, so the
 *   target sits at the exact center of the view whenever there is no lag.
 * - While the target's PlayerController.IsBoosting is true, the camera smooths toward the
 *   target over boostLagTime seconds (higher = more lag). The lag distance grows with speed.
 * - When boosting ends, the lag offset eases back to zero over recoverTime seconds.
 * - The target's height is followed with a verticalSmoothTime delay (0 = exact), so a jump doesn't bob the whole
 *   view while standing on a wall top still lifts it.
 * - Runs in LateUpdate so it uses the target's final position for the frame.
 * - If no target is assigned, it looks for an object tagged "Player" on Start.
 */

using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 15f;
    [SerializeField] private float boostLagTime = 5f;
    [SerializeField] private float recoverTime = 0.3f;
    [Tooltip("Seconds the camera takes to follow the target's height (jumps, wall tops); 0 = follow exactly.")]
    [SerializeField, Min(0f)] private float verticalSmoothTime = 0.6f;

    private PlayerController player;
    private float smoothY;
    private float smoothYVelocity;
    private Vector3 lagOffset;
    private Vector3 lastFocus;
    private Vector3 boostVelocity;
    private Vector3 recoverVelocity;

    private void Start()
    {
        if (target == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null)
            {
                target = found.transform;
            }
        }

        if (target != null)
        {
            player = target.GetComponent<PlayerController>();
            lastFocus = target.position;
            smoothY = target.position.y;
        }
    }

    private Vector3 TargetPosition()
    {
        Vector3 position = target.position;
        smoothY = verticalSmoothTime > 0f
            ? Mathf.SmoothDamp(smoothY, position.y, ref smoothYVelocity, verticalSmoothTime)
            : position.y;
        position.y = smoothY;
        return position;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 focus;
        Vector3 targetPosition = TargetPosition();

        if (player != null && player.IsBoosting)
        {
            focus = Vector3.SmoothDamp(lastFocus, targetPosition, ref boostVelocity, boostLagTime);
            lagOffset = focus - targetPosition;
            recoverVelocity = Vector3.zero;
        }
        else
        {
            lagOffset = Vector3.SmoothDamp(lagOffset, Vector3.zero, ref recoverVelocity, recoverTime);
            focus = targetPosition + lagOffset;
            boostVelocity = Vector3.zero;
        }

        lastFocus = focus;
        transform.position = focus - transform.forward * distance;
    }
}
