/*
 * RunTimeline
 * The run's script of enemy waves (round system step 1, 2026-11-05): a time-based run like Vampire Survivors. The
 * player only sees a timer counting up; underneath, waves start at set times, each with its own Director card.
 *
 * - runLength: seconds from the run's start (the base chunk placed) until the run ends.
 * - Waves (in start-time order): a wave lasts from its startTime until the next wave's startTime (the last one until
 *   runLength). intensity only picks hint text; it does not change spawning.
 * - Director card (spawn director, 2026-11-21/22; user: guided-random waves, a threat-budget director): what the wave
 *   spawns. RunDirector has WaveShopper spend it at the wave's start: threatBudget (+- budgetVariance), a menu
 *   (RatType + weight, and a shadowChance), mustInclude (bought first, even past the budget), minShadowThreat (at least
 *   this much threat bought as shadows), limits (max rats of a type), fronts (min-max directions the base-bound packs
 *   come from) and releaseSpan (the share of the wave's length its packs are released over). prices = the SpawnPrices
 *   asset. The old per-wave columns (budget, spawnRate, rangedChance, armorWeights, bagChance) were removed in step 5
 *   (user OK, 2026-11-22).
 * - Hints: hintLeadTime seconds before a wave whose intensity differs from the current wave's, one random line from
 *   that intensity's list is shown (an empty list = no hint).
 * - WaveEnd(i) / WaveAt(time) are helpers for the run director and the inspector.
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
        [Tooltip("Picks the hint text only; it doesn't change spawning.")]
        public WaveIntensity intensity = WaveIntensity.Steady;

        [Header("Director card")]
        [Tooltip("Threat to spend this wave (SpawnPrices costs).")]
        [Min(0f)] public float threatBudget = 20f;
        [Tooltip("The budget is rolled within +- this share (0.15 = +-15%).")]
        [Range(0f, 0.5f)] public float budgetVariance = 0.15f;
        [Tooltip("What the director may buy, with weights (bigger = picked more often).")]
        public MenuItem[] menu = new MenuItem[0];
        [Tooltip("Chance that each purchase is a shadow version (costs more).")]
        [Range(0f, 1f)] public float shadowChance;
        [Tooltip("Always bought first, even past the budget.")]
        public MustInclude[] mustInclude = new MustInclude[0];
        [Tooltip("At least this much threat is bought as shadows (from the menu).")]
        [Min(0f)] public float minShadowThreat;
        [Tooltip("Most rats (swarms: packs) of a type this wave.")]
        public Limit[] limits = new Limit[0];
        [Tooltip("How many directions (min, max) the base-bound packs come from.")]
        public Vector2Int fronts = new Vector2Int(1, 1);
        [Tooltip("Packs are released over this share of the wave's length (the rest is a quiet tail).")]
        [Range(0.1f, 1f)] public float releaseSpan = 0.8f;
    }

    [Serializable]
    public class MenuItem
    {
        public RatType type;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public class MustInclude
    {
        public RatType type;
        [Min(1)] public int count = 1;
        public bool shadow;
    }

    [Serializable]
    public class Limit
    {
        public RatType type;
        [Min(0)] public int max = 1;
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
    [Tooltip("The spawn director's price list (threat cost of each rat type).")]
    [SerializeField] private SpawnPrices prices;
    [SerializeField] private Wave[] waves = new Wave[0];
    [SerializeField] private HintLines[] hints = new HintLines[0];

    public SpawnPrices Prices => prices;
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
