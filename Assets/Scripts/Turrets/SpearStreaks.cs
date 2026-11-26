/*
 * SpearStreaks
 * Wind trails on the Fishercat's spear (2026-11-25, user: thin white streaks like speed lines). On SpearProjectile.prefab
 * and StuckSpear.prefab: a few child TrailRenderers placed around the spear's head leave thin white streaks that fade
 * fast. Both objects are pooled, so the trails are cleared whenever one is taken out again (OnEnable), or a reused
 * spear would streak from its old spot. The stuck spear has them too, so the streaks carry on through its last glide
 * into the ground and fade once it stops. Purely visual.
 */

using UnityEngine;

public class SpearStreaks : MonoBehaviour
{
    private TrailRenderer[] trails;
    private bool clearPending;

    private void Awake()
    {
        trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    // A trail can re-add its old spot while it is being re-enabled, so it is cleared again on the first frame.
    private void OnEnable()
    {
        ClearAll();
        clearPending = true;
    }

    private void LateUpdate()
    {
        if (clearPending)
        {
            clearPending = false;
            ClearAll();
        }
    }

    private void OnDisable()
    {
        ClearAll();
    }

    private void ClearAll()
    {
        if (trails == null)
        {
            return;
        }
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].Clear();
            trails[i].emitting = true;
        }
    }
}
