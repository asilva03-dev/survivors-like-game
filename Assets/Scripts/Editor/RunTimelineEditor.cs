/*
 * RunTimelineEditor
 * Inspector help for RunTimeline (round system step 1): under the normal fields, one line per wave saying how long it
 * spawns out of how long it lasts, or how many of its budget won't fit (dropped), plus warnings for waves out of
 * start-time order or starting after the run's end, and the run's total budget. Read-only: it changes nothing.
 */

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RunTimeline))]
public class RunTimelineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        RunTimeline timeline = (RunTimeline)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Waves at a glance", EditorStyles.boldLabel);

        int total = 0;
        for (int i = 0; i < timeline.WaveCount; i++)
        {
            RunTimeline.Wave wave = timeline.GetWave(i);
            total += wave.budget;
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

            float length = timeline.WaveEnd(i) - wave.startTime;
            float spawning = timeline.SpawnSeconds(i);
            if (spawning <= length + 0.001f)
            {
                EditorGUILayout.HelpBox(string.Format("{0} {1} enemies at {2}/s: spawns for {3:0.#} s of its {4:0.#} s.",
                    label, wave.budget, wave.spawnRate, spawning, length), MessageType.Info);
            }
            else
            {
                int fit = Mathf.FloorToInt(length * wave.spawnRate);
                EditorGUILayout.HelpBox(string.Format("{0} only {1} of {2} fit in its {3:0.#} s at {4}/s; the rest are dropped.",
                    label, fit, wave.budget, length, wave.spawnRate), MessageType.Warning);
            }
        }
        EditorGUILayout.HelpBox(string.Format("Run: {0}, {1} waves, {2} enemies budgeted in all.",
            Clock(timeline.RunLength), timeline.WaveCount, total), MessageType.None);
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return string.Format("{0}:{1:00}", s / 60, s % 60);
    }
}
