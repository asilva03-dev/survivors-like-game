/*
 * GunStatsEditor
 * Inspector for GunStats assets. Groups the fields into "Stats" (attack rate, damage, range,
 * projectile speed...) and "Visual" (projectile prefab, ring position, turn speeds, slot spacing)
 * regardless of which base class declares them. Any field not listed here is drawn under "Other"
 * so new fields never go missing.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GunStats))]
public class GunStatsEditor : Editor
{
    private static readonly string[] StatFields =
    {
        "attackInterval", "damage", "resourceDamageMultiplier", "targets",
        "range", "focusWeakestResource", "avoidCloseRange", "preferFarTargets", "stickyTargets", "useRing", "claimsTargets", "preferClusters", "clusterRadius", "projectileCount", "pierce", "homing", "homingStrength", "projectileSpeed", "lifetime", "fireAngleTolerance"
    };

    private static readonly string[] VisualFields =
    {
        "projectilePrefab", "prewarmCount", "muzzleOffset",
        "projectileSpacing", "curveStartAngle", "curveAngleStep", "curveTurnSpeed",
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
