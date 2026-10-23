/*
 * DraftCard
 * One card of the preset draft (built at runtime by PresetDraftUI): reports clicks and mouse-wheel scrolls over it
 * and brightens its background while hovered. Read-only cards (the "Your base" reference) ignore all of it.
 */

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraftCard : MonoBehaviour, IPointerClickHandler, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image Background;
    public RawImage Map;
    public Text Title;
    public Text Hint;
    public bool Interactive = true;
    public Color NormalColor = new Color(0.12f, 0.12f, 0.14f, 0.96f);
    public Color HoverColor = new Color(0.22f, 0.22f, 0.27f, 0.98f);

    public event Action Clicked;
    public event Action<int> Scrolled;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Interactive && eventData.button == PointerEventData.InputButton.Left)
        {
            Clicked?.Invoke();
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (Interactive && Mathf.Abs(eventData.scrollDelta.y) > 0.01f)
        {
            Scrolled?.Invoke(eventData.scrollDelta.y > 0f ? 1 : -1);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Interactive)
        {
            Background.color = HoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Background.color = NormalColor;
    }
}
