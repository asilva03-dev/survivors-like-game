/*
 * StuckSpear
 * A Fishercat spear stuck in the ground (2026-11-24): pooled by SpearStick, placed with its tip (the object's origin, the
 * spear model pointing +Z) below the ground; it stands for stickTime, then shrinks to nothing over shrinkTime and goes
 * back to the pool. Purely visual (no collider).
 */

using UnityEngine;

public class StuckSpear : MonoBehaviour
{
    private ComponentPool<StuckSpear> pool;
    private float stickTime;
    private float shrinkTime;
    private float timer;

    public void Plant(ComponentPool<StuckSpear> owningPool, float stay, float shrink)
    {
        pool = owningPool;
        stickTime = stay;
        shrinkTime = Mathf.Max(0.01f, shrink);
        timer = 0f;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        if (pool == null)
        {
            return;
        }
        timer += Time.deltaTime;
        if (timer <= stickTime)
        {
            return;
        }
        float k = 1f - Mathf.Clamp01((timer - stickTime) / shrinkTime);
        transform.localScale = Vector3.one * k;
        if (k <= 0f)
        {
            pool.Release(this);
        }
    }
}
