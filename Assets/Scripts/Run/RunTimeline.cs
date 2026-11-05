/*
 * RunTimeline
 * The run's script of enemy waves (round system step 1, 2026-11-05): a time-based run like Vampire Survivors. The
 * player only sees a timer counting up; underneath, waves start at set times, each with its own enemy budget and rate.
 *
 * - runLength: seconds from the run's start (the base chunk placed) until the run ends.
 * - Waves (in start-time order): a wave lasts from its startTime until the next wave's startTime (the last one until
 *   runLength). It spawns `budget` enemies at `spawnRate` per second; whatever is left when the wave ends is dropped.
 *   rangedChance = share of the spawns that come for the player in the dark that are ranged hunters (the light/dark
 *   split itself stays on EnemySpawner). intensity only picks hint text; it does not change spawning.
 * - Hints: hintLeadTime seconds before a wave whose intensity differs from the current wave's, one random line from
 *   that intensity's list is shown (an empty list = no hint).
 * - WaveEnd(i) / WaveAt(time) / SpawnSeconds(i) are helpers for the run director and the inspector.
 */

using System;
using UnityEngine;

public enum WaveIntensity { Calm, Steady, Rising, Hard }

[CreateAssetMenu(menuName = "TD/Run Timeline", fileName = "RunTimeline")]
public class RunTimeline : ScriptableObject
{
    [Serializable]
    public class Wave
    {
        [Tooltip("Only a label for the inspector.")]
        public string name = "Wave";
        [Tooltip("Seconds into the run when this wave starts. It lasts until the next wave starts.")]
        [Min(0f)] public float startTime;
        [Tooltip("How many enemies this wave spawns in total. Any not spawned when the wave ends are dropped.")]
        [Min(0)] public int budget = 20;
        [Tooltip("Enemies per second while the budget lasts.")]
        [Min(0.01f)] public float spawnRate = 1f;
        [Tooltip("Share of the player's dark spawns that are ranged hunters (ranged never come from the base share).")]
        [Range(0f, 1f)] public float rangedChance = 0.2f;
        [Tooltip("Picks the hint text only; it doesn't change spawning.")]
        public WaveIntensity intensity = WaveIntensity.Steady;
    }

    [Serializable]
    public class HintLines
    {
        public WaveIntensity intensity;
        [Tooltip("One is picked at random. Empty = no hint before waves of this intensity.")]
        public string[] lines = new string[0];
    }

    [Tooltip("Seconds from the run's start (base chunk placed) until the run ends.")]
    [SerializeField, Min(1f)] private float runLength = 300f;
    [Tooltip("Seconds before a wave of a different intensity that its hint appears.")]
    [SerializeField, Min(0f)] private float hintLeadTime = 10f;
    [SerializeField] private Wave[] waves = new Wave[0];
    [SerializeField] private HintLines[] hints = new HintLines[0];

    public float RunLength => runLength;
    public float HintLeadTime => hintLeadTime;
    public int WaveCount => waves != null ? waves.Length : 0;
    public Wave GetWave(int index) => waves[index];

    /// <summary>When wave `index` ends: the next wave's start, or the run's end for the last one.</summary>
    public float WaveEnd(int index)
    {
        return index + 1 < WaveCount ? waves[index + 1].startTime : runLength;
    }

    /// <summary>The wave running at `time` (the last one started), or -1 before the first.</summary>
    public int WaveAt(float time)
    {
        int found = -1;
        for (int i = 0; i < WaveCount; i++)
        {
            if (waves[i].startTime <= time)
            {
                found = i;
            }
        }
        return found;
    }

    /// <summary>Seconds wave `index` needs to spawn its whole budget at its rate.</summary>
    public float SpawnSeconds(int index)
    {
        Wave wave = waves[index];
        return wave.spawnRate > 0f ? wave.budget / wave.spawnRate : float.PositiveInfinity;
    }

    /// <summary>A random hint line for an intensity, or null if it has none.</summary>
    public string PickHint(WaveIntensity intensity)
    {
        if (hints == null)
        {
            return null;
        }
        foreach (HintLines entry in hints)
        {
            if (entry != null && entry.intensity == intensity && entry.lines != null && entry.lines.Length > 0)
            {
                return entry.lines[UnityEngine.Random.Range(0, entry.lines.Length)];
            }
        }
        return null;
    }
}
