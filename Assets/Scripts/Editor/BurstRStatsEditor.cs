/*
 * BurstRStatsEditor
 * Inspector for BurstRStats assets: "Stats" (damage, volley timing, count, flight, hit size) and "Visual" (model,
 * size, spin, explosion color, pool size). Any unlisted field is drawn under "Other".
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BurstRStats))]
public class BurstRStatsEditor : Editor
{
    private static readonly string[] StatFields =
    {
        "attackInterval", "damage", "resourceDamageMultiplier", "targets", "projectileCount", "speed", "lifetime",
        "spawnRadius", "spawnHeight", "hitRadius"
    };

    private static readonly string[] VisualFields =
    {
        "projectileModel", "modelSize", "spinSpeed", "explosionColor", "prewarmCount"
    };


    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        DrawGroup("Stats", StatFields);
        DrawGroup("Visual", VisualFields);

        List<string> known = new List<string>(StatFields);
        known.AddRange(VisualFields);
        known.Add("m_Script");

        List<string> others = new List<string>();
        SerializedProperty property = serializedObject.GetIterator();
        for (bool enter = true; property.NextVisible(enter); enter = false)
        {
            if (!known.Contains(property.name))
            {
                others.Add(property.name);
            }
        }

        DrawGroup("Other", others.ToArray());
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGroup(string title, string[] fields)
    {
        bool headerDrawn = false;
        foreach (string field in fields)
        {
            SerializedProperty property = serializedObject.FindProperty(field);
            if (property == null)
            {
                continue;
            }

            if (!headerDrawn)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                headerDrawn = true;
            }

            EditorGUILayout.PropertyField(property, true);
        }
    }
}
