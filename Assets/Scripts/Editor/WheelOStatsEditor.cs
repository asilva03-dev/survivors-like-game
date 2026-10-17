/*
 * WheelOStatsEditor
 * Inspector for WheelOStats assets: "Stats" (damage, count, timing, size) and "Visual" (model). attackInterval is
 * hidden because the wheel does not use it. Any other unlisted field is drawn under "Other".
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WheelOStats))]
public class WheelOStatsEditor : Editor
{
    private static readonly string[] StatFields =
    {
        "damage", "resourceDamageMultiplier", "targets", "projectileCount", "orbitRadius", "orbitHeight",
        "rotateSpeed", "duration", "cooldown", "perEnemyCooldown", "hitRadius"
    };

    private static readonly string[] VisualFields =
    {
        "objectModel", "objectSize"
    };

    private static readonly string[] HiddenFields =
    {
        "attackInterval"
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
        known.AddRange(HiddenFields);
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
