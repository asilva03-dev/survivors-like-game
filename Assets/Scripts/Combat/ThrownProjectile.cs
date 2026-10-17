/*
 * ThrownProjectile
 * Pooled lobbed throwable. Flies one continuous parabola from where it was thrown to a fixed landing
 * point (straight-line travel plus an up-and-down arc of height throwArc), tumbling on the way, with a
 * trail for the whole trajectory. On landing it reports the point to its weapon and returns to the pool.
 *
 * - Never created or destroyed at runtime: the weapon's ComponentPool owns it.
 * - The model lives under the "Model" child; SetModel() puts an instance of any model there and scales
 *   it so its largest dimension is modelSize. Done once on the weapon's pool template, so every pooled
 *   copy carries it.
 * - Needs a TrailRenderer (trail is cleared on launch so a reused throwable does not streak).
 */

using UnityEngine;

public class ThrownProjectile : MonoBehaviour
{
    [SerializeField] private Transform modelHolder;
    [SerializeField] private TrailRenderer trail;
    [SerializeField, Min(0.05f)] private float modelSize = 0.6f;
    [SerializeField] private float spinSpeed = 540f;

    private ComponentPool<ThrownProjectile> pool;
    private System.Action<Vector3> landed;
    private Vector3 start;
    private Vector3 end;
    private float arc;
    private float duration;
    private float time;

    public void SetModel(GameObject model)
    {
        if (model == null)
        {
            return;
        }

        GameObject instance = Instantiate(model, modelHolder);
        ModelFit.Fit(modelHolder, instance.transform, modelSize);
    }

    public void Launch(ComponentPool<ThrownProjectile> owningPool, Vector3 from, Vector3 to, float arcHeight,
        float flightSeconds, System.Action<Vector3> onLanded)
    {
        pool = owningPool;
        start = from;
        end = to;
        arc = arcHeight;
        duration = Mathf.Max(flightSeconds, 0.05f);
        landed = onLanded;
        time = 0f;

        transform.position = start;
        if (trail != null)
        {
            trail.Clear();
        }
    }

    private void Update()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / duration);

        Vector3 position = Vector3.Lerp(start, end, t);
        position.y += 4f * arc * t * (1f - t);
        transform.position = position;
        modelHolder.Rotate(spinSpeed * Time.deltaTime, 0f, 0f, Space.Self);

        if (t >= 1f)
        {
            landed?.Invoke(end);
            pool.Release(this);
        }
    }
}
