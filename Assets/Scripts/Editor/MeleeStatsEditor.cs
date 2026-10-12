/*
 * MeleeStatsEditor
 * Inspector for MeleeStats assets. Hides the inherited attacksPerSecond field, which melee ignores
 * (attack speed there is attackInterval / chargedAttackInterval).
 */

using UnityEditor;

[CustomEditor(typeof(MeleeStats))]
public class MeleeStatsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "attacksPerSecond");
        serializedObject.ApplyModifiedProperties();
    }
}
