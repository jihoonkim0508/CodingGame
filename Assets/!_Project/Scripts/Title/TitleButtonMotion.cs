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
        public bool IsHovered => hovered;
        public void OnPointerEnter(PointerEventData e) => hovered = true;
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)pressed = true; }
        public void OnPointerUp(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)pressed = false; }
        void Awake() => button = GetComponent<Button>();
        void OnDisable()
        {
            hovered = pressed = false; hoverAmount = 0;
            transform.localScale = Vector3.one;
            if (highlight) highlight.SetState(0, false);
        }
        void Update()
        {
            if (!button) button=GetComponent<Button>();
            if (!button || !button.IsInteractable()) hovered=pressed=false;
            float target = pressed ? .98f : hovered ? 1.025f : 1;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1 - Mathf.Exp(-16 * Time.unscaledDeltaTime));
            hoverAmount=Mathf.MoveTowards(hoverAmount,hovered?1:0,Time.unscaledDeltaTime/.12f);
            if(highlight)highlight.SetState(hoverAmount,pressed);
        }
    }
}
