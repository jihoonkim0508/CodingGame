using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    public sealed class TitleButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        bool hovered, pressed;
        Button button;
        float hoverAmount;
        public TitleButtonHighlight highlight;
        [Tooltip("마우스를 올리면 버튼 왼쪽에 나타나 버튼 쪽으로 다가오는 화살표(스테이지 맵의 ui_arrow, Tools/Learning UI/Title Hover Arrows)")]
        public Image arrow;
        float arrowX, shownAmount = -1;
        public bool IsHovered => hovered;
        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            if (button && button.IsInteractable()) UISound.Play("hover", .6f);
        }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)pressed = true; }
        public void OnPointerUp(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)pressed = false; }
        void Awake()
        {
            button = GetComponent<Button>();
            if (arrow) arrowX = arrow.rectTransform.anchoredPosition.x;
        }
        void OnDisable()
        {
            hovered = pressed = false; hoverAmount = 0;
            transform.localScale = Vector3.one;
            if (highlight) highlight.SetState(0, false);
            if (arrow) { arrow.enabled = false; shownAmount = -1; }
        }
        void Update()
        {
            if (!button) button=GetComponent<Button>();
            if (!button || !button.IsInteractable()) hovered=pressed=false;
            float target = pressed ? .98f : hovered ? 1.025f : 1;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1 - Mathf.Exp(-16 * Time.unscaledDeltaTime));
            hoverAmount=Mathf.MoveTowards(hoverAmount,hovered?1:0,Time.unscaledDeltaTime/.12f);
            if(highlight)highlight.SetState(hoverAmount,pressed);
            if (arrow && shownAmount != hoverAmount)
            {
                shownAmount = hoverAmount;
                var color = arrow.color; color.a = hoverAmount; arrow.color = color; arrow.enabled = hoverAmount > 0;
                arrow.rectTransform.anchoredPosition = new Vector2(arrowX - 8 * (1 - hoverAmount), 0); // slides in toward the button
            }
        }
    }
}
