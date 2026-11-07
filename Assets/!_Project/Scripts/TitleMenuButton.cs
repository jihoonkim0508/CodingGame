using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame
{
    [RequireComponent(typeof(Button))]
    public sealed class TitleMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        static readonly Color HighlightColor = new Color(0.435f, 1f, 0.824f);
        Button button;
        TMP_Text label;
        TextMeshProUGUI marker;
        Color normalColor;
        float normalSize, emphasis;
        bool focused;

        void Awake()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>();
            if (!label) { enabled = false; return; }
            normalColor = label.color;
            normalSize = label.fontSize;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            // The fixed button rectangle includes padding; only the text grows on hover.
            label.rectTransform.offsetMin = new Vector2(64, 8);
            label.rectTransform.offsetMax = new Vector2(-24, -8);

            marker = new GameObject("SelectionMarker", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            marker.rectTransform.SetParent(transform, false);
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0, .5f);
            marker.rectTransform.anchoredPosition = new Vector2(30, 0);
            marker.rectTransform.sizeDelta = new Vector2(48, 84);
            marker.font = label.font;
            marker.fontSize = normalSize;
            marker.text = ">";
            marker.alignment = TextAlignmentOptions.Midline;
            marker.raycastTarget = false;
            Draw();
        }

        void Update()
        {
            float target = focused && button.IsInteractable() ? 1 : 0;
            emphasis = Mathf.MoveTowards(emphasis, target, Time.unscaledDeltaTime / .12f);
            Draw();
        }

        void Draw()
        {
            label.fontSize = normalSize * Mathf.Lerp(1, 1.08f, emphasis);
            label.color = Color.Lerp(normalColor, HighlightColor, emphasis);
            marker.color = new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, emphasis);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!button.IsInteractable()) return;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject);
            else focused = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            focused = false;
        }

        public void OnSelect(BaseEventData eventData) => focused = true;
        public void OnDeselect(BaseEventData eventData) => focused = false;

        void OnDisable()
        {
            focused = false;
            emphasis = 0;
            if (label && marker) Draw();
        }

    }
}
