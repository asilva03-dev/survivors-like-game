/*
 * RunDebugUI
 * Testing aids for the run (round system step 3, 2026-11-05): a small debug line on the HUD (current wave and its
 * intensity, budget left (+ despawn refunds this wave), spawn rate, alive enemies split base-bound / coming for the
 * player, the dark-spawn share)
 * and a key (N by default) that skips to the next wave (RunDirector.SkipToNextWave; the current wave's unspawned
 * budget is dropped; after the last wave it jumps to the run's end).
 *
 * - Lives on a Text under the HUD canvas. showDebugLine hides the text (the key still works). The line is redrawn
 *   refreshInterval times a second. Same build switch as the other debug keys (enableInBuilds).
 */

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RunDebugUI : MonoBehaviour
{
    [SerializeField] private RunDirector director;
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private Text label;
    [SerializeField] private bool showDebugLine = true;
    [SerializeField] private Key skipKey = Key.N;
    [SerializeField] private bool enableInBuilds = true;
    [Tooltip("Seconds between redraws of the line.")]
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private float nextRefresh;

    private void Update()
    {
        if (director == null)
        {
            director = RunDirector.Instance;
        }
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<EnemySpawner>();
        }

        bool debugAllowed = Application.isEditor || enableInBuilds;
        Keyboard keyboard = Keyboard.current;
        if (debugAllowed && keyboard != null && keyboard[skipKey].wasPressedThisFrame && director != null)
        {
            director.SkipToNextWave();
            nextRefresh = 0f;
        }

        label.enabled = showDebugLine && debugAllowed;
        if (!label.enabled || Time.unscaledTime < nextRefresh)
        {
            return;
        }
        nextRefresh = Time.unscaledTime + refreshInterval;
        label.text = BuildLine();
    }

    private string BuildLine()
    {
        if (director == null || spawner == null)
        {
            return "Run: no director";
        }
        string alive = string.Format("alive {0} (base {1}, you {2})", spawner.AliveCount, spawner.BaseAliveCount, spawner.PlayerAliveCount);
        string share = string.Format("dark share {0:0}%", spawner.PlayerShare * 100f);
        if (!director.Started)
        {
            return "Run: waiting for the base chunk · " + alive;
        }
        if (director.Ended)
        {
            return "Run ended · " + alive;
        }
        RunTimeline.Wave wave = director.CurrentWave;
        if (wave == null)
        {
            return "Before the first wave · " + alive + " · " + share;
        }
        string refunds = director.RefundedThisWave > 0 ? string.Format(" (+{0} refunded)", director.RefundedThisWave) : "";
        return string.Format("W{0} {1} · budget {2} left{3} · {4}/s · {5} · {6}",
            director.WaveIndex + 1, wave.intensity, director.BudgetLeft, refunds, wave.spawnRate, alive, share);
    }
}
