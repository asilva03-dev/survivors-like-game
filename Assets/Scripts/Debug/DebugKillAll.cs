/*
 * DebugKillAll
 * Testing shortcut: press the key (Q by default) to deal damage to every active enemy and toggle
 * enemy spawning off. Press again to turn spawning back on (nothing is killed on that press).
 *
 * - Uses the new Input System keyboard directly; no action asset setup needed.
 * - Damage goes through IDamageable like any weapon hit, so damage numbers, pooling and death
 *   events behave normally.
 * - Spawning is toggled through EnemySpawner.SpawnsEnabled; SpawnStateUI shows the current state.
 * - Works in the editor and in any standalone build while enableInBuilds is on (default). Turn it
 *   off, or delete this object from the scene, before shipping a real release.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugKillAll : MonoBehaviour
{
    [SerializeField] private Key key = Key.Q;
    [SerializeField] private float damage = 100f;
    [SerializeField] private bool enableInBuilds = true;
    [SerializeField] private EnemySpawner spawner;

    private readonly List<Enemy> scratch = new List<Enemy>();

    private void Update()
    {
        if (!Application.isEditor && !enableInBuilds)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[key].wasPressedThisFrame)
        {
            return;
        }

        bool pausing = spawner == null || spawner.SpawnsEnabled;
        if (spawner != null)
        {
            spawner.SpawnsEnabled = !spawner.SpawnsEnabled;
        }

        if (!pausing)
        {
            return;
        }

        scratch.Clear();
        scratch.AddRange(Enemy.Active);

        for (int i = 0; i < scratch.Count; i++)
        {
            scratch[i].TakeDamage(damage);
        }
    }
}
