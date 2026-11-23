/*
 * RunDebugUI
 * Testing aids for the run (round system step 3, 2026-11-05): a small debug line on the HUD (current wave and its
 * intensity, rats still queued (+ despawn refunds this wave) and the rolled threat spent / budget (spawn director step 2,
 * 2026-11-21), alive enemies split base-bound / coming for the player, the dark-spawn share; a second line (spawn
 * director step 4, 2026-11-22) with the seconds to the next pack, what is still queued by type and the wave's fronts as
 * compass directions around the light, N = +Z = up on screen)
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
        string line = string.Format("W{0} {1} · {2} queued{3} · threat {4:0}/{5:0} · {6} · {7}",
            director.WaveIndex + 1, wave.intensity, director.QueuedRats, refunds, director.ThreatSpent, director.ThreatBudget,
            alive, share);
        float next = director.NextReleaseIn;
        string queued = director.DescribeQueue();
        string nextPack = next < 0f ? "all released" : string.Format("next pack {0:0}s", next);
        return line + "\n" + nextPack + (queued.Length > 0 ? " · " + queued : "") + " · fronts " + Fronts();
    }

    private string Fronts()
    {
        if (director.Fronts.Count == 0)
        {
            return "none";
        }
        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        string text = "";
        for (int i = 0; i < director.Fronts.Count; i++)
        {
            int index = Mathf.RoundToInt(Mathf.Repeat(director.Fronts[i], 360f) / 45f) % 8;
            text += (i > 0 ? ", " : "") + names[index];
        }
        return text;
    }
}
