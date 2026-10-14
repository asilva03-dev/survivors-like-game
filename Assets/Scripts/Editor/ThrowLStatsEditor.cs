/*
 * ThrowLStatsEditor
 * Inspector for ThrowLStats assets, same layout as GunStatsEditor: "Stats" (damage, timing, range,
 * puddle size) and "Visual" (model, colors, pool size, ring position, turn speeds, slot spacing).
 * Any field not listed here is drawn under "Other" so new fields never go missing.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ThrowLStats))]
public class ThrowLStatsEditor : Editor
{
    private static readonly string[] StatFields =
    {
        "attackInterval", "damage", "resourceDamageMultiplier", "targets",
        "range", "focusWeakestResource", "throwArc", "flightTime", "predictionStrength", "projectileCount", "spread", "landingSpread", "radius", "lifetime", "lingerDamageInterval", "fireAngleTolerance"
    };

    private static readonly string[] VisualFields =
    {
        "throwableModel", "lingerColor", "explosionColor", "prewarmCount",
        "ringRadius", "turnSpeed", "displacedTurnSpeed", "slotClearance", "separationDistance"
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
