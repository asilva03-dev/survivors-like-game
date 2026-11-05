/*
 * RunHintUI
 * The spawn hint (round system step 4, 2026-11-05): when RunDirector.HintDue fires, one random line for the upcoming
 * wave's intensity (RunTimeline hint lines) fades in in the upper middle of the screen, holds, and fades out. An
 * intensity without lines shows nothing. A new hint replaces one still showing.
 *
 * - Lives on a Text (Minecraftia font) under the HUD canvas. Fade times and one colour per intensity are inspector
 *   values. The fade runs on game time, so it pauses with the game.
 */

using UnityEngine;
using UnityEngine.UI;

public class RunHintUI : MonoBehaviour
{
    [SerializeField] private RunDirector director;
    [SerializeField] private Text label;
    [SerializeField, Min(0f)] private float fadeIn = 1f;
    [SerializeField, Min(0f)] private float hold = 3f;
    [SerializeField, Min(0f)] private float fadeOut = 1.5f;
    [SerializeField] private Color calmColor = new Color(0.72f, 0.95f, 0.72f, 1f);
    [SerializeField] private Color steadyColor = Color.white;
    [SerializeField] private Color risingColor = new Color(1f, 0.72f, 0.4f, 1f);
    [SerializeField] private Color hardColor = new Color(0.95f, 0.45f, 0.42f, 1f);

    private float shownFor = float.MaxValue;
    private Color baseColor;
    private Outline outline;
    private float outlineAlpha;

    public string CurrentLine { get; private set; }

    private void Awake()
    {
        outline = label.GetComponent<Outline>();
        outlineAlpha = outline != null ? outline.effectColor.a : 0f;
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        if (director == null)
        {
            director = RunDirector.Instance;
        }
        if (director != null)
        {
            director.HintDue += Show;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.HintDue -= Show;
        }
    }

    private void Show(int waveIndex)
    {
        RunTimeline timeline = director.Timeline;
        WaveIntensity intensity = timeline.GetWave(waveIndex).intensity;
        string line = timeline.PickHint(intensity);
        if (string.IsNullOrEmpty(line))
        {
            return;
        }
        CurrentLine = line;
        label.text = line;
        baseColor = ColorFor(intensity);
        shownFor = 0f;
        SetAlpha(0f);
    }

    private void Update()
    {
        float total = fadeIn + hold + fadeOut;
        if (shownFor > total)
        {
            return;
        }
        shownFor += Time.deltaTime;
        float alpha;
        if (shownFor < fadeIn)
        {
            alpha = fadeIn > 0f ? shownFor / fadeIn : 1f;
        }
        else if (shownFor < fadeIn + hold)
        {
            alpha = 1f;
        }
        else
        {
            alpha = fadeOut > 0f ? 1f - (shownFor - fadeIn - hold) / fadeOut : 0f;
        }
        SetAlpha(Mathf.Clamp01(alpha));
        if (shownFor > total)
        {
            CurrentLine = null;
        }
    }

    /// <summary>Current opacity of the line (0 = hidden), for testing.</summary>
    public float Alpha => label.color.a;

    private void SetAlpha(float alpha)
    {
        Color c = baseColor;
        c.a = alpha;
        label.color = c;
        if (outline != null)
        {
            Color o = outline.effectColor;
            o.a = outlineAlpha * alpha;
            outline.effectColor = o;
        }
    }

    private Color ColorFor(WaveIntensity intensity)
    {
        switch (intensity)
        {
            case WaveIntensity.Calm: return calmColor;
            case WaveIntensity.Rising: return risingColor;
            case WaveIntensity.Hard: return hardColor;
            default: return steadyColor;
        }
    }
}
