/*
 * ComponentPool<T>
 * Generic object pool for any prefab with a component of type T (projectiles, enemies, pickups...).
 *
 * - Prewarms `prewarm` inactive instances under `root` at construction.
 * - Get() pops an inactive instance (or creates one if the pool is empty, so it grows
 *   instead of skipping), positions it, and activates it. The caller resets its own state.
 * - Release() deactivates the instance and returns it to the pool. Never Destroy pooled objects.
 * - Instances are parented under `root`; keep root unparented from moving objects (e.g. the player).
 */

using System.Collections.Generic;
using UnityEngine;

public class ComponentPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform root;
    private readonly Stack<T> free = new Stack<T>();

    public ComponentPool(T prefab, Transform root, int prewarm)
    {
        this.prefab = prefab;
        this.root = root;

        for (int i = 0; i < prewarm; i++)
        {
            free.Push(Create());
        }
    }

    public T Get(Vector3 position, Quaternion rotation)
    {
        T item = free.Count > 0 ? free.Pop() : Create();
        item.transform.SetPositionAndRotation(position, rotation);
        item.gameObject.SetActive(true);
        return item;
    }

    public void Release(T item)
    {
        item.gameObject.SetActive(false);
        free.Push(item);
    }

    private T Create()
    {
        T item = Object.Instantiate(prefab, root);
        item.gameObject.SetActive(false);
        return item;
    }
}
