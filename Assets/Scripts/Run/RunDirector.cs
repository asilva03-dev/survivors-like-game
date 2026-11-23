/*
 * RunDirector
 * Runs the run's clock and its waves (round system step 2, 2026-11-05): a timer counting up from the moment the base
 * chunk is placed, and at the RunTimeline's wave times the current wave's enemy budget spawned at its rate.
 *
 * - The clock starts once BaseLayout.HasBaseChunk (the run starts with the base draft) and counts game time (it
 *   follows Time.timeScale). It stops at the timeline's runLength: Ended, RunEnded fires once, nothing more spawns.
 * - Spawn director (step 2, 2026-11-21; user: a threat-budget director, "guided random" waves): when a wave starts, its
 *   Director card is spent by WaveShopper (the timeline's SpawnPrices), whatever the last wave hadn't released is
 *   dropped, and the bought packs are shuffled and scheduled evenly over the card's releaseSpan of the wave's length.
 *   Each due pack goes to EnemySpawner.SpawnPack (the light/dark split is rolled per pack there; side rules from the
 *   prices). A pack with no valid spot is retried next frame; a DarkOnly pack (ranged) rolled for the base waits
 *   darkRetry seconds and tries again (dropped at the wave's end like any leftover). While spawns are paused (Q) the
 *   schedule is frozen, so nothing bursts afterwards. Rats spilled from a bag/pot rat and swarm refills use no budget.
 *   (The old per-wave count/rate/chance columns were removed in step 5, 2026-11-22.)
 * - Fronts (spawn director step 3, 2026-11-21): at each wave start the card's fronts (min-max) are rolled into that many
 *   directions around the light (evenly spaced from a random start, each nudged by up to frontWobble of its share);
 *   every pack is given one of them at random and EnemySpawner spawns it there if it goes for the base. Fronts lists them.
 * - Debug readout (step 4, 2026-11-22): DescribeQueue() ("Bare 12, Helmet 4, Ranged 2 (1 shadow)") and NextReleaseIn
 *   (seconds to the next due pack, -1 when none) for RunDebugUI.
 * - Exposes Elapsed, WaveIndex, CurrentWave, QueuedRats / QueuedPacks (still to release this wave), ThreatBudget /
 *   ThreatSpent (the rolled card), SpawnedThisWave and the WaveStarted / RunEnded events (HUD,
 *   hints and the end panel use them). SkipToNextWave() jumps the clock to the next wave's start (debug key, step 3).
 * - Hints (step 4): once per wave, when the next wave starts within the timeline's hintLeadTime and its intensity
 *   differs from the current wave's, HintDue(next wave index) fires (RunHintUI picks and shows the line). The first
 *   wave gets none. Waves skipped past with N get none.
 * - Run end (step 5): EndRun(survived) stops the run and fires RunEnded(survived) once; the clock reaching runLength
 *   calls EndRun(true). It is public so base death (pinned) can call EndRun(false) later.
 * - Despawn refund (user, 2026-11-05): an enemy that despawns without dying (Enemy.DespawnRequested: a hunter that
 *   retreated off-screen after the player went into the light) gives its slot back: since the director (step 2) the same
 *   rat (type, shadow) goes back on this wave's queue, released refundDelay seconds later, so ducking into the light
 *   can't thin a wave. Only rats this director spawned (tracked until they die, dive or despawn), only while the run is
 *   going; deaths are never refunded. A refund is dropped like any leftover when its wave ends. RefundedThisWave /
 *   TotalRefunded count them.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public class RunDirector : MonoBehaviour
{
    [SerializeField] private RunTimeline timeline;
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private BaseLayout layout;
    [Tooltip("A dark-only pack (ranged) rolled for the base tries again after this many seconds.")]
    [SerializeField, Min(0.1f)] private float darkRetry = 3f;
    [Tooltip("A refunded (despawned) rat goes back on the queue this many seconds later.")]
    [SerializeField, Min(0f)] private float refundDelay = 1f;
    [Tooltip("Fronts: each direction is nudged by up to this share of its even spacing (0 = exactly even).")]
    [SerializeField, Range(0f, 0.5f)] private float frontWobble = 0.3f;

    private struct Queued
    {
        public RatType type;
        public bool shadow;
        public int count;
        public float time;
        public float front;
    }

    private struct Tracked
    {
        public RatType type;
        public bool shadow;
    }

    private readonly List<Queued> queue = new List<Queued>();
    private readonly Dictionary<Enemy, Tracked> tracked = new Dictionary<Enemy, Tracked>();
    private readonly List<Enemy> justSpawned = new List<Enemy>();
    private readonly List<float> fronts = new List<float>();
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
    /// <summary>Rats (a swarm counts as one) still queued for this wave.</summary>
    public int QueuedRats
    {
        get
        {
            int total = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                total += queue[i].count;
            }
            return total;
        }
    }
    public int QueuedPacks => queue.Count;

    /// <summary>Seconds until the next queued pack is due (0 if one is due now), -1 when nothing is queued.</summary>
    public float NextReleaseIn
    {
        get
        {
            if (queue.Count == 0)
            {
                return -1f;
            }
            float soonest = float.MaxValue;
            for (int i = 0; i < queue.Count; i++)
            {
                soonest = Mathf.Min(soonest, queue[i].time);
            }
            return Mathf.Max(0f, soonest - Elapsed);
        }
    }

    /// <summary>What is still queued this wave by type, e.g. "Bare 12, Helmet 4, Ranged 2 (1 shadow)"; "" when empty.</summary>
    public string DescribeQueue()
    {
        if (queue.Count == 0)
        {
            return "";
        }
        int types = Enum.GetValues(typeof(RatType)).Length;
        int[] counts = new int[types];
        int[] shadows = new int[types];
        for (int i = 0; i < queue.Count; i++)
        {
            int t = (int)queue[i].type;
            counts[t] += queue[i].count;
            if (queue[i].shadow)
            {
                shadows[t] += queue[i].count;
            }
        }
        List<string> parts = new List<string>();
        for (int t = 0; t < types; t++)
        {
            if (counts[t] == 0)
            {
                continue;
            }
            string part = (RatType)t + " " + counts[t];
            if (shadows[t] > 0)
            {
                part += " (" + shadows[t] + " shadow)";
            }
            parts.Add(part);
        }
        return string.Join(", ", parts.ToArray());
    }
    /// <summary>This wave's front directions (degrees around the light, 0 = +Z).</summary>
    public IList<float> Fronts => fronts;
    public float ThreatBudget { get; private set; }
    public float ThreatSpent { get; private set; }
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
        Enemy.EnteredBase += Untrack;
        Damageable.Died += HandleDied;
    }

    private void OnDisable()
    {
        Enemy.DespawnRequested -= HandleDespawn;
        Enemy.EnteredBase -= Untrack;
        Damageable.Died -= HandleDied;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // A despawned (not killed) enemy this director spawned goes back on the current wave's queue.
    private void HandleDespawn(Enemy enemy)
    {
        Tracked what;
        if (enemy == null || !tracked.TryGetValue(enemy, out what))
        {
            return;
        }
        tracked.Remove(enemy);
        if (!Started || Ended || WaveIndex < 0)
        {
            return;
        }
        queue.Add(new Queued { type = what.type, shadow = what.shadow, count = 1, time = Elapsed + refundDelay, front = PickFront() });
        RefundedThisWave++;
        TotalRefunded++;
    }

    private void HandleDied(Damageable target)
    {
        Untrack(target as Enemy);
    }

    private void Untrack(Enemy enemy)
    {
        if (enemy != null)
        {
            tracked.Remove(enemy);
        }
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
        ReleasePacks();
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
        queue.Clear();
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
        SpawnedThisWave = 0;
        RefundedThisWave = 0;
        queue.Clear();
        fronts.Clear();
        ThreatBudget = 0f;
        ThreatSpent = 0f;
        if (index >= 0)
        {
            BuyWave(index);
            WaveStarted?.Invoke(index);
        }
    }

    // Spends the wave's card and schedules the packs evenly over its releaseSpan, in random order.
    private void BuyWave(int index)
    {
        RunTimeline.Wave wave = timeline.GetWave(index);
        WaveShopper.Result result = WaveShopper.Buy(wave, timeline.Prices);
        ThreatBudget = result.budget;
        ThreatSpent = result.spent;
        RollFronts(wave);
        List<WaveShopper.Pack> packs = result.packs;
        for (int i = packs.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            WaveShopper.Pack swap = packs[i];
            packs[i] = packs[j];
            packs[j] = swap;
        }
        float start = Mathf.Max(wave.startTime, Elapsed);
        float span = Mathf.Max(0f, timeline.WaveEnd(index) - start) * wave.releaseSpan;
        for (int i = 0; i < packs.Count; i++)
        {
            queue.Add(new Queued
            {
                type = packs[i].type,
                shadow = packs[i].shadow,
                count = packs[i].count,
                time = start + span * i / Mathf.Max(1, packs.Count),
                front = PickFront()
            });
        }
    }

    // fronts.x-fronts.y directions, evenly spaced from a random start, each nudged by up to frontWobble of the spacing.
    private void RollFronts(RunTimeline.Wave wave)
    {
        fronts.Clear();
        int min = Mathf.Max(1, wave.fronts.x);
        int count = UnityEngine.Random.Range(min, Mathf.Max(min, wave.fronts.y) + 1);
        float spacing = 360f / count;
        float start = UnityEngine.Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float yaw = start + i * spacing + UnityEngine.Random.Range(-frontWobble, frontWobble) * spacing;
            fronts.Add(Mathf.Repeat(yaw, 360f));
        }
    }

    private float PickFront()
    {
        return fronts.Count > 0 ? fronts[UnityEngine.Random.Range(0, fronts.Count)] : float.NaN;
    }

    private void ReleasePacks()
    {
        if (queue.Count == 0)
        {
            return;
        }
        // Paused (Q): freeze the schedule so nothing bursts afterwards.
        if (!spawner.SpawnsEnabled)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                Queued frozen = queue[i];
                frozen.time += Time.deltaTime;
                queue[i] = frozen;
            }
            return;
        }
        SpawnPrices prices = timeline.Prices;
        for (int i = 0; i < queue.Count; i++)
        {
            Queued pack = queue[i];
            if (pack.time > Elapsed)
            {
                continue;
            }
            SpawnPrices.Price price = prices != null ? prices.Get(pack.type) : null;
            SpawnSide side = price != null ? price.side : SpawnSide.Either;
            justSpawned.Clear();
            EnemySpawner.PackResult released = spawner.SpawnPack(pack.type, pack.count, pack.shadow, side, justSpawned, pack.front);
            if (released == EnemySpawner.PackResult.NoSpot)
            {
                // No valid spot this frame: try the rest of the due packs next frame.
                return;
            }
            if (released == EnemySpawner.PackResult.NeedsDark)
            {
                pack.time = Elapsed + darkRetry;
                queue[i] = pack;
                continue;
            }
            for (int k = 0; k < justSpawned.Count; k++)
            {
                tracked[justSpawned[k]] = new Tracked { type = pack.type, shadow = pack.shadow };
            }
            SpawnedThisWave += pack.count;
            queue.RemoveAt(i);
            i--;
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
