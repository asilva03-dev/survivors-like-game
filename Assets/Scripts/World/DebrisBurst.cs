/*
 * DebrisBurst
 * A pooled one-shot burst of small falling chips (tree death: green leaves and brown bark).
 *
 * - The prefab root sits on the ground at the dead tree's base. Its ParticleSystem emits cube chips
 *   from the tree's mid-height, they fall, hit the ground plane (a child transform listed in the
 *   particle Collision module), settle, shrink and vanish. All look and motion values (count, colors,
 *   speed, size, gravity, lifetime) are tuned on the ParticleSystem in the prefab.
 * - Play() restarts the system and the burst returns itself to its pool after releaseAfter seconds,
 *   which should be a bit longer than the particles' longest lifetime.
 */

using UnityEngine;

public class DebrisBurst : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;
    [SerializeField, Min(0.1f)] private float releaseAfter = 4f;

    private ComponentPool<DebrisBurst> pool;
    private float age;

    public void Play(ComponentPool<DebrisBurst> owningPool, Color? tint = null)
    {
        pool = owningPool;
        age = 0f;
        particles.Clear();

        if (tint.HasValue)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startColor = tint.Value;
        }

        particles.Play();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= releaseAfter && pool != null)
        {
            pool.Release(this);
        }
    }
}
