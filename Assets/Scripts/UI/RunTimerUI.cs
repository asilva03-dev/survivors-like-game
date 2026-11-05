/*
 * RunTimerUI
 * The run timer at the top centre of the HUD (round system step 3, 2026-11-05): m:ss counting up from RunDirector.
 * The only thing the player sees of the run's waves (no wave number or round indicator, by design).
 *
 * - Lives on a Text (Minecraftia font) under the HUD canvas. Shows 0:00 until the run starts (base chunk placed) and
 *   stops at the run's length. Redraws only when the shown second changes.
 */

using UnityEngine;
using UnityEngine.UI;

public class RunTimerUI : MonoBehaviour
{
    [SerializeField] private RunDirector director;
    [SerializeField] private Text label;

    private int shownSeconds = -1;

    private void Update()
    {
        if (director == null)
        {
            director = RunDirector.Instance;
        }
        int seconds = director != null ? Mathf.FloorToInt(director.Elapsed) : 0;
        if (seconds == shownSeconds)
        {
            return;
        }
        shownSeconds = seconds;
        label.text = string.Format("{0}:{1:00}", seconds / 60, seconds % 60);
    }
}
