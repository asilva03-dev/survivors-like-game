/*
 * SpawnStateUI
 * Screen-space text (top right) showing whether enemy spawning is on or paused.
 *
 * - Lives on a Text (Minecraftia font) under the HUD canvas, anchored top right.
 * - Redraws only when EnemySpawner.SpawnsEnabledChanged fires. Text and color per state are
 *   serialized so they can be changed in the inspector.
 */

using UnityEngine;
using UnityEngine.UI;

public class SpawnStateUI : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private Text label;
    [SerializeField] private string enabledText = "Enemy spawns: ON";
    [SerializeField] private string disabledText = "Enemy spawns: OFF";
    [SerializeField] private Color enabledColor = new Color(0.55f, 1f, 0.55f, 1f);
    [SerializeField] private Color disabledColor = new Color(1f, 0.45f, 0.45f, 1f);

    private void OnEnable()
    {
        spawner.SpawnsEnabledChanged += Refresh;
        Refresh(spawner.SpawnsEnabled);
    }

    private void OnDisable()
    {
        spawner.SpawnsEnabledChanged -= Refresh;
    }

    private void Refresh(bool spawnsEnabled)
    {
        label.text = spawnsEnabled ? enabledText : disabledText;
        label.color = spawnsEnabled ? enabledColor : disabledColor;
    }
}
