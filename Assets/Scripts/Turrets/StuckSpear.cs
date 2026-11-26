/*
 * StuckSpear
 * A Fishercat spear stuck in the ground (2026-11-24): pooled by SpearStick, placed with its tip (the object's origin, the
 * spear model pointing +Z) below the ground; it stands for stickTime, then shrinks to nothing over shrinkTime and goes
 * back to the pool. Purely visual (no collider). Also used by AngelArrowStick for arrows stuck in the ground.
 *
 * - PlantFrom (2026-11-25, user: the spear used to jump ahead into the ground): it first flies from where the spear's
 *   flight ended (and its heading then) to its planted pose in a straight line at the spear's speed, turning into the
 *   planted angle on the way, then sticks. Plant (the Angel Cat) plants at once, as before.
 */

using UnityEngine;

public class StuckSpear : MonoBehaviour
{
    private ComponentPool<StuckSpear> pool;
    private float stickTime;
    private float shrinkTime;
    private float timer;
    private Vector3 fromPosition;
    private Quaternion fromRotation;
    private Vector3 toPosition;
    private Quaternion toRotation;
    private float flyTime;
    private float flyTimer;

    public void Plant(ComponentPool<StuckSpear> owningPool, float stay, float shrink)
    {
        pool = owningPool;
        stickTime = stay;
        shrinkTime = Mathf.Max(0.01f, shrink);
        timer = 0f;
        flyTime = 0f;
        flyTimer = 0f;
        transform.localScale = Vector3.one;
    }

    /// <summary>Plants at the current pose (set by the pool's Get), after flying there from `from` at `speed`.</summary>
    public void PlantFrom(ComponentPool<StuckSpear> owningPool, float stay, float shrink, Vector3 from, Quaternion fromHeading, float speed)
    {
        Plant(owningPool, stay, shrink);
        toPosition = transform.position;
        toRotation = transform.rotation;
        fromPosition = from;
        fromRotation = fromHeading;
        flyTime = speed > 0.01f ? Vector3.Distance(from, toPosition) / speed : 0f;
        if (flyTime > 0f)
        {
            transform.SetPositionAndRotation(fromPosition, fromRotation);
        }
    }

    private void Update()
    {
        if (pool == null)
        {
            return;
        }

        if (flyTimer < flyTime)
        {
            flyTimer += Time.deltaTime;
            float k = Mathf.Clamp01(flyTimer / flyTime);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, k), Quaternion.Slerp(fromRotation, toRotation, k));
            return;
        }

        timer += Time.deltaTime;
        if (timer <= stickTime)
        {
            return;
        }
        float s = 1f - Mathf.Clamp01((timer - stickTime) / shrinkTime);
        transform.localScale = Vector3.one * s;
        if (s <= 0f)
        {
            pool.Release(this);
        }
    }
}
