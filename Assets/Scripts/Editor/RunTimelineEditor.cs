/*
 * RunTimelineEditor
 * Inspector help for RunTimeline (round system step 1; Director cards since spawn director step 5, 2026-11-22): under the
 * normal fields, one line per wave with its clock, length, card budget, menu, fronts and release span, plus warnings for
 * waves out of start-time order, starting after the run's end, or with an empty menu and no must-includes; and the
 * run's total threat. Read-only: it changes nothing.
 * - Director preview (spawn director step 1, 2026-11-21): "Roll every wave" runs WaveShopper.Buy on each wave card with
 *   the timeline's SpawnPrices and lists what it bought (rolled budget, spent, rats by type). Press again for a new roll;
 *   nothing is saved.
 */

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RunTimeline))]
public class RunTimelineEditor : Editor
{
    private string[] rolls;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        RunTimeline timeline = (RunTimeline)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Waves at a glance", EditorStyles.boldLabel);

        float total = 0f;
        for (int i = 0; i < timeline.WaveCount; i++)
        {
            RunTimeline.Wave wave = timeline.GetWave(i);
            string label = string.Format("{0} ({1}, {2}):", wave.name, Clock(wave.startTime), wave.intensity);
            if (i > 0 && wave.startTime < timeline.GetWave(i - 1).startTime)
            {
                EditorGUILayout.HelpBox(label + " starts before the wave above it. Keep waves in start-time order.", MessageType.Error);
                continue;
            }
            if (wave.startTime >= timeline.RunLength)
            {
                EditorGUILayout.HelpBox(label + " starts after the run ends, so it never happens.", MessageType.Warning);
                continue;
            }
            total += wave.threatBudget;
            float length = timeline.WaveEnd(i) - wave.startTime;
            bool hasMenu = wave.menu != null && wave.menu.Length > 0;
            bool hasMust = wave.mustInclude != null && wave.mustInclude.Length > 0;
            if (!hasMenu && !hasMust)
            {
                EditorGUILayout.HelpBox(label + " has nothing on its menu and no must-includes, so it spawns nothing.", MessageType.Warning);
                continue;
            }
            string menu = "";
            if (hasMenu)
            {
                foreach (RunTimeline.MenuItem item in wave.menu)
                {
                    if (item != null)
                    {
                        menu += (menu.Length > 0 ? ", " : "") + item.type;
                    }
                }
            }
            string fronts = wave.fronts.x == wave.fronts.y ? wave.fronts.x.ToString() : wave.fronts.x + "-" + wave.fronts.y;
            EditorGUILayout.HelpBox(string.Format("{0} {1:0.#} s long, threat {2:0.#} (+-{3:0}%), menu {4}{5}, shadows {6:0}%, {7} front(s), released over {8:0.#} s.",
                label, length, wave.threatBudget, wave.budgetVariance * 100f, menu.Length > 0 ? menu : "none",
                hasMust ? " + must-includes" : "", wave.shadowChance * 100f, fronts, length * wave.releaseSpan), MessageType.Info);
        }
        EditorGUILayout.HelpBox(string.Format("Run: {0}, {1} waves, {2:0} threat budgeted in all.",
            Clock(timeline.RunLength), timeline.WaveCount, total), MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Director preview", EditorStyles.boldLabel);
        if (timeline.Prices == null)
        {
            EditorGUILayout.HelpBox("Set Prices (a SpawnPrices asset) to preview what each wave card buys.", MessageType.Warning);
            return;
        }
        if (GUILayout.Button("Roll every wave"))
        {
            rolls = new string[timeline.WaveCount];
            for (int i = 0; i < timeline.WaveCount; i++)
            {
                RunTimeline.Wave wave = timeline.GetWave(i);
                WaveShopper.Result result = WaveShopper.Buy(wave, timeline.Prices);
                rolls[i] = string.Format("{0} ({1}): budget {2:0.#}, spent {3:0.#}, {4} packs: {5}", wave.name,
                    Clock(wave.startTime), result.budget, result.spent, result.packs.Count, WaveShopper.Describe(result));
            }
        }
        if (rolls != null)
        {
            foreach (string line in rolls)
            {
                EditorGUILayout.HelpBox(line, MessageType.None);
            }
        }
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return string.Format("{0}:{1:00}", s / 60, s % 60);
    }
}
