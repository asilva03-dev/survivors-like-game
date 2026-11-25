/*
 * EnemyStuckArrow
 * An Angel Cat arrow stuck in a rat (tower 6, 2026-11-25, user): pooled by AngelArrowStick. It keeps the pose it had
 * when it hit, relative to the rat's facing transform (its `Visual` child, else the rat itself), so it walks and turns
 * with the rat; it stays for `stay` seconds (the volley's TotalTime), then shrinks away and goes back to the pool.
 * If the rat dies or disappears first, the arrow stays where it last was until its time is up. Purely visual.
 * The object's origin is the arrow's tip (the model points +Z, like StuckSpear).
 */

using UnityEngine;

public class EnemyStuckArrow : MonoBehaviour
{
    private ComponentPool<EnemyStuckArrow> pool;
    private Damageable host;
    private Transform anchor;
    private Vector3 localPosition;
    private Quaternion localRotation;
    private float stay;
    private float shrinkTime;
    private float timer;

    public void Attach(ComponentPool<EnemyStuckArrow> owningPool, Damageable rat, float stayTime, float shrink)
    {
        pool = owningPool;
        host = rat;
        Transform visual = rat != null ? rat.transform.Find("Visual") : null;
        anchor = visual != null ? visual : rat != null ? rat.transform : null;
        if (anchor != null)
        {
            localPosition = anchor.InverseTransformPoint(transform.position);
            localRotation = Quaternion.Inverse(anchor.rotation) * transform.rotation;
        }
        stay = stayTime;
        shrinkTime = Mathf.Max(0.01f, shrink);
        timer = 0f;
        transform.localScale = Vector3.one;
    }

    private void LateUpdate()
    {
        if (pool == null)
        {
            return;
        }

        if (anchor != null && host != null && host.isActiveAndEnabled)
        {
            transform.SetPositionAndRotation(anchor.TransformPoint(localPosition), anchor.rotation * localRotation);
        }
        else
        {
            anchor = null;
        }

        timer += Time.deltaTime;
        if (timer <= stay)
        {
            return;
        }
        float k = 1f - Mathf.Clamp01((timer - stay) / shrinkTime);
        transform.localScale = Vector3.one * k;
        if (k <= 0f)
        {
            ComponentPool<EnemyStuckArrow> owner = pool;
            pool = null;
            host = null;
            anchor = null;
            owner.Release(this);
        }
    }
}
