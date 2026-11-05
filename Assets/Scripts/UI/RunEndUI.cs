/*
 * RunEndUI
 * The end-of-run panel (round system step 5, 2026-11-05): when RunDirector.RunEnded fires, the game freezes
 * (Time.timeScale 0) and a panel shows a title ("You survived" / "The base has fallen"), the run's time and a Restart
 * button that reloads the scene.
 *
 * - Lives on an object under the HUD canvas; builds its own children at Awake (a dark full-screen backdrop, the panel,
 *   two texts and the button) and keeps them hidden until the run ends. Texts, colours and sizes are inspector values.
 * - Makes sure an EventSystem exists so the button can be clicked (same as BuildBarUI). Restart sets timeScale back
 *   to 1 before reloading; so does leaving the scene.
 * - Losing (survived = false) has no trigger yet: base death is pinned; RunDirector.EndRun(false) will show it.
 */

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunEndUI : MonoBehaviour
{
    [SerializeField] private RunDirector director;
    [SerializeField] private Font font;
    [SerializeField] private string survivedTitle = "You survived";
    [SerializeField] private string lostTitle = "The base has fallen";
    [SerializeField] private string timeFormat = "Time {0}";
    [SerializeField] private string restartLabel = "Restart";
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
    [SerializeField] private Color survivedColor = new Color(0.75f, 1f, 0.75f, 1f);
    [SerializeField] private Color lostColor = new Color(1f, 0.5f, 0.45f, 1f);
    [SerializeField] private Vector2 panelSize = new Vector2(640f, 340f);
    [SerializeField, Min(8)] private int titleSize = 52;
    [SerializeField, Min(8)] private int textSize = 30;

    private GameObject root;
    private Text title;
    private Text timeText;

    public bool Shown => root != null && root.activeSelf;
    public string TitleText => title != null ? title.text : null;
    public string TimeText => timeText != null ? timeText.text : null;

    private void Awake()
    {
        Build();
        root.SetActive(false);
    }

    private void OnEnable()
    {
        if (director == null)
        {
            director = RunDirector.Instance;
        }
        if (director != null)
        {
            director.RunEnded += Show;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.RunEnded -= Show;
        }
    }

    private void OnDestroy()
    {
        if (Shown)
        {
            Time.timeScale = 1f;
        }
    }

    private void Show(bool survived)
    {
        EnsureEventSystem();
        int seconds = Mathf.FloorToInt(director.Elapsed);
        title.text = survived ? survivedTitle : lostTitle;
        title.color = survived ? survivedColor : lostColor;
        timeText.text = string.Format(timeFormat, string.Format("{0}:{1:00}", seconds / 60, seconds % 60));
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        Time.timeScale = 0f;
    }

    /// <summary>Reloads the scene for a new run (the Restart button).</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
            ? SceneManager.GetActiveScene().name
            : SceneManager.GetActiveScene().path);
    }

    private void Build()
    {
        root = new GameObject("RunEndPanel", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch((RectTransform)root.transform);
        Image backdrop = root.AddComponent<Image>();
        backdrop.color = backdropColor;

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panel.AddComponent<Image>().color = panelColor;

        title = NewText(panel.transform, "Title", titleSize, new Vector2(0f, 95f));
        timeText = NewText(panel.transform, "Time", textSize, new Vector2(0f, 20f));

        GameObject button = new GameObject("RestartButton", typeof(RectTransform));
        button.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = (RectTransform)button.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -90f);
        buttonRect.sizeDelta = new Vector2(260f, 70f);
        Image buttonImage = button.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        Button click = button.AddComponent<Button>();
        click.targetGraphic = buttonImage;
        click.onClick.AddListener(Restart);
        Text buttonText = NewText(button.transform, "Label", textSize, Vector2.zero);
        buttonText.text = restartLabel;
        Stretch((RectTransform)buttonText.transform);
    }

    private Text NewText(Transform parent, string name, int size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(panelSize.x - 40f, size * 1.6f);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }
    }
}
