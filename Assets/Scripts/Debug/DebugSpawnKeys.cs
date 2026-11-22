/*
 * DebugSpawnKeys
 * Testing shortcut (2026-11-21, new rats): keys that spawn one rat heading for the base, at a normal base-share spot
 * (off screen just outside the light), through EnemySpawner.SpawnForBase. No wave budget; works while spawns are paused.
 * Hold `shadowModifier` (Left Shift) to make it a shadow rat. Scene object "DebugSpawnKeys".
 *
 * - entries: key -> kind (EnemySpawner.Kind) + armor layers (0 bare, 1 helmet, 2 plate). Defaults: F1 bare, F2 helmet,
 *   F3 plated, F4 bag rat, F5 pot rat, F6 athletic rat, F7 rat swarm (2026-11-21, a whole pack). New rat types get their own entries as they are built.
 * - Same build switch as DebugKillAll / DebugWood (enableInBuilds).
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugSpawnKeys : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Key key = Key.F1;
        public EnemySpawner.Kind kind = EnemySpawner.Kind.Basic;
        [Min(0)] public int armorLayers;
    }

    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private Key shadowModifier = Key.LeftShift;
    [SerializeField] private bool enableInBuilds = true;
    [SerializeField] private List<Entry> entries = new List<Entry>
    {
        new Entry { key = Key.F1, kind = EnemySpawner.Kind.Basic, armorLayers = 0 },
        new Entry { key = Key.F2, kind = EnemySpawner.Kind.Basic, armorLayers = 1 },
        new Entry { key = Key.F3, kind = EnemySpawner.Kind.Basic, armorLayers = 2 },
        new Entry { key = Key.F4, kind = EnemySpawner.Kind.Bag, armorLayers = 0 },
        new Entry { key = Key.F5, kind = EnemySpawner.Kind.Pot, armorLayers = 0 },
        new Entry { key = Key.F6, kind = EnemySpawner.Kind.Athletic, armorLayers = 0 },
        new Entry { key = Key.F7, kind = EnemySpawner.Kind.Swarm, armorLayers = 0 }
    };

    private void Update()
    {
        if (!Application.isEditor && !enableInBuilds)
        {
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }
        for (int i = 0; i < entries.Count; i++)
        {
            if (keyboard[entries[i].key].wasPressedThisFrame)
            {
                Spawn(i, keyboard[shadowModifier].isPressed);
            }
        }
    }

    /// <summary>Spawns entries[index] (also the test hook). Null when no spot was found.</summary>
    public Enemy Spawn(int index, bool shadow)
    {
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<EnemySpawner>();
        }
        if (spawner == null || index < 0 || index >= entries.Count)
        {
            return null;
        }
        Entry entry = entries[index];
        return spawner.SpawnForBase(entry.kind, entry.armorLayers, shadow);
    }
}
