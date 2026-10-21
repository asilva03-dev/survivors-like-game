/*
 * PlayerDamageSetup
 * One-click scene wiring for the player damage system: Tools > Player > Set Up Damage.
 *
 * - Adds PlayerHealth (category Player, 100 health, red damage numbers) and PlayerHitFeedback to the Player.
 * - Moves the Player's own mesh renderer onto a child called "Model" (so the hit jitter can move it without
 *   touching the Rigidbody) and assigns it to PlayerHitFeedback. Safe to run again; it does nothing twice.
 * - Saves the open scene when done.
 */

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerDamageSetup
{
    [MenuItem("Tools/Player/Set Up Damage")]
    private static void Run()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("PlayerDamageSetup: no object tagged Player in the open scene.");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = Undo.AddComponent<PlayerHealth>(player);
            SerializedObject so = new SerializedObject(health);
            so.FindProperty("maxHealth").floatValue = 100f;
            so.FindProperty("category").enumValueIndex = (int)TargetCategory.Player;
            so.FindProperty("damageNumberColor").colorValue = new Color(1f, 0.25f, 0.25f, 1f);
            so.ApplyModifiedProperties();
        }

        Transform model = player.transform.Find("Model");
        MeshRenderer rootRenderer = player.GetComponent<MeshRenderer>();
        MeshFilter rootFilter = player.GetComponent<MeshFilter>();
        if (model == null && rootRenderer != null && rootFilter != null)
        {
            GameObject child = new GameObject("Model");
            Undo.RegisterCreatedObjectUndo(child, "Create player model");
            child.transform.SetParent(player.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = rootFilter.sharedMesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = rootRenderer.sharedMaterials;
            renderer.shadowCastingMode = rootRenderer.shadowCastingMode;
            renderer.receiveShadows = rootRenderer.receiveShadows;
            Undo.DestroyObjectImmediate(rootRenderer);
            Undo.DestroyObjectImmediate(rootFilter);
            model = child.transform;
        }

        PlayerHitFeedback feedback = player.GetComponent<PlayerHitFeedback>();
        if (feedback == null)
        {
            feedback = Undo.AddComponent<PlayerHitFeedback>(player);
        }

        SerializedObject feedbackObject = new SerializedObject(feedback);
        feedbackObject.FindProperty("model").objectReferenceValue = model;
        feedbackObject.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
        Debug.Log("PlayerDamageSetup: Player now has PlayerHealth + PlayerHitFeedback (model: "
            + (model != null ? model.name : "none") + ").");
    }
}
