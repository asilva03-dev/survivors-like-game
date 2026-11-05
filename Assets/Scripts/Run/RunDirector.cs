/*
 * RunDirector
 * Runs the run's clock and its waves (round system step 2, 2026-11-05): a timer counting up from the moment the base
 * chunk is placed, and at the RunTimeline's wave times the current wave's enemy budget spawned at its rate.
 *
 * - The clock starts once BaseLayout.HasBaseChunk (the run starts with the base draft) and counts game time (it
 *   follows Time.timeScale). It stops at the timeline's runLength: Ended, RunEnded fires once, nothing more spawns.
 * - When a new wave starts, its budget becomes BudgetLeft and whatever the last wave hadn't spawned is dropped.
 * - Spawning: spawnRate x frame time is added to a counter; each whole 1 asks EnemySpawner.TrySpawn(rangedChance) for
 *   one enemy and takes 1 off the budget. A spawn the spawner refuses (no valid spot, or spawns paused with Q) uses no
 *   budget: while paused nothing builds up; a missed spot is retried next frame. The counter never holds more than
 *   one frame's worth plus one, so there is no burst afterwards.
 * - Exposes Elapsed, WaveIndex, CurrentWave, BudgetLeft, SpawnedThisWave and the WaveStarted / RunEnded events (HUD,
 *   hints and the end panel use them). SkipToNextWave() jumps the clock to the next wave's start (debug key, step 3).
 * - Hints (step 4): once per wave, when the next wave starts within the timeline's hintLeadTime and its intensity
 *   differs from the current wave's, HintDue(next wave index) fires (RunHintUI picks and shows the line). The first
 *   wave gets none. Waves skipped past with N get none.
 * - Run end (step 5): EndRun(survived) stops the run and fires RunEnded(survived) once; the clock reaching runLength
 *   calls EndRun(true). It is public so base death (pinned) can call EndRun(false) later.
 * - Despawn refund (user, 2026-11-05): an enemy that despawns without dying (Enemy.DespawnRequested: a hunter that
 *   retreated off-screen after the player went into the light) gives its slot back: +1 to the current wave's budget,
 *   so ducking into the light can't thin a wave. Only while the run is going; deaths are never refunded. A refund
 *   is dropped like any leftover when its wave ends. RefundedThisWave / TotalRefunded count them.
 */

using System;
using UnityEngine;

public class RunDirector : MonoBehaviour
{
    [SerializeField] private RunTimeline timeline;
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private BaseLayout layout;

    private float spawnCounter;
    private int hintedWave = -1;

    public static RunDirector Instance { get; private set; }

    public event Action<int> WaveStarted;
    /// <summary>A hint is due for this (upcoming) wave index.</summary>
    public event Action<int> HintDue;
    /// <summary>The run ended: true = survived to the end, false = lost.</summary>
    public event Action<bool> RunEnded;

    public bool Survived { get; private set; }

    public RunTimeline Timeline => timeline;
    public bool Started { get; private set; }
    public bool Ended { get; private set; }
    /// <summary>Seconds since the run started (the base chunk was placed).</summary>
    public float Elapsed { get; private set; }
    public int WaveIndex { get; private set; } = -1;
    public RunTimeline.Wave CurrentWave => timeline != null && WaveIndex >= 0 ? timeline.GetWave(WaveIndex) : null;
    public int BudgetLeft { get; private set; }
    public int SpawnedThisWave { get; private set; }
    public int RefundedThisWave { get; private set; }
    public int TotalRefunded { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (layout == null)
        {
            layout = FindAnyObjectByType<BaseLayout>();
        }
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<EnemySpawner>();
        }
    }

    private void OnEnable()
    {
        Enemy.DespawnRequested += HandleDespawn;
    }

    private void OnDisable()
    {
        Enemy.DespawnRequested -= HandleDespawn;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // A despawned (not killed) enemy gives its slot back to the current wave.
    private void HandleDespawn(Enemy enemy)
    {
        if (!Started || Ended || WaveIndex < 0)
        {
            return;
        }
        BudgetLeft++;
        RefundedThisWave++;
        TotalRefunded++;
    }

    private void Update()
    {
        if (timeline == null || spawner == null || Ended)
        {
            return;
        }
        if (!Started)
        {
            if (layout != null && !layout.HasBaseChunk)
            {
                return;
            }
            Started = true;
            Elapsed = 0f;
        }
        else
        {
            Elapsed += Time.deltaTime;
        }

        if (Elapsed >= timeline.RunLength)
        {
            Elapsed = timeline.RunLength;
            EndRun(true);
            return;
        }

        int wave = timeline.WaveAt(Elapsed);
        if (wave != WaveIndex)
        {
            StartWave(wave);
        }
        CheckHint();
        SpawnFromBudget();
    }

    /// <summary>Ends the run once: survived (the clock ran out) or lost (later: the base died).</summary>
    public void EndRun(bool survived)
    {
        if (Ended)
        {
            return;
        }
        Ended = true;
        Survived = survived;
        BudgetLeft = 0;
        RunEnded?.Invoke(survived);
    }

    private void CheckHint()
    {
        int next = WaveIndex + 1;
        if (WaveIndex < 0 || next >= timeline.WaveCount || next == hintedWave)
        {
            return;
        }
        RunTimeline.Wave upcoming = timeline.GetWave(next);
        if (upcoming.startTime - Elapsed > timeline.HintLeadTime)
        {
            return;
        }
        hintedWave = next;
        if (upcoming.intensity != timeline.GetWave(WaveIndex).intensity)
        {
            HintDue?.Invoke(next);
        }
    }

    private void StartWave(int index)
    {
        WaveIndex = index;
        BudgetLeft = index >= 0 ? timeline.GetWave(index).budget : 0;
        SpawnedThisWave = 0;
        RefundedThisWave = 0;
        spawnCounter = 0f;
        if (index >= 0)
        {
            WaveStarted?.Invoke(index);
        }
    }

    private void SpawnFromBudget()
    {
        RunTimeline.Wave wave = CurrentWave;
        if (wave == null || BudgetLeft <= 0 || !spawner.SpawnsEnabled)
        {
            spawnCounter = 0f;
            return;
        }
        float step = wave.spawnRate * Time.deltaTime;
        spawnCounter = Mathf.Min(spawnCounter + step, step + 1f);
        while (spawnCounter >= 1f && BudgetLeft > 0)
        {
            if (!spawner.TrySpawn(wave.rangedChance))
            {
                break;
            }
            spawnCounter -= 1f;
            BudgetLeft--;
            SpawnedThisWave++;
        }
    }

    /// <summary>Debug: jumps the clock to the next wave's start (or the run's end after the last wave).</summary>
    public void SkipToNextWave()
    {
        if (!Started || Ended || timeline == null)
        {
            return;
        }
        Elapsed = WaveIndex >= 0 ? timeline.WaveEnd(WaveIndex) : (timeline.WaveCount > 0 ? timeline.GetWave(0).startTime : timeline.RunLength);
    }
}
