/*
 * CameraFollow
 * Keeps the target centered on screen for a fixed-angle top-down camera, with
 * extra lag while the player dashes or sprints.
 *
 * - Camera rotation is never changed; set it in the scene (pitch controls the view angle).
 * - Position is placed behind the target along the camera's forward axis, so the
 *   target sits at the exact center of the view whenever there is no lag.
 * - While the target's PlayerController.IsBoosting is true, the camera smooths toward the
 *   target over boostLagTime seconds (higher = more lag). The lag distance grows with speed.
 * - When boosting ends, the lag offset eases back to zero over recoverTime seconds.
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

    private PlayerController player;
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
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 focus;

        if (player != null && player.IsBoosting)
        {
            focus = Vector3.SmoothDamp(lastFocus, target.position, ref boostVelocity, boostLagTime);
            lagOffset = focus - target.position;
            recoverVelocity = Vector3.zero;
        }
        else
        {
            lagOffset = Vector3.SmoothDamp(lagOffset, Vector3.zero, ref recoverVelocity, recoverTime);
            focus = target.position + lagOffset;
            boostVelocity = Vector3.zero;
        }

        lastFocus = focus;
        transform.position = focus - transform.forward * distance;
    }
}
