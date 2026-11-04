using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame
{
    // A candy button's feel: it swells a little under the mouse and squashes while pressed (UIRestyle adds it).
    [RequireComponent(typeof(Selectable))]
    public sealed class UIPressScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Selectable control;
        float target = 1, scale = 1;
        bool over, down;

        void Awake() => control = GetComponent<Selectable>();
        void OnDisable() { over = down = false; scale = target = 1; transform.localScale = Vector3.one; }
        public void OnPointerEnter(PointerEventData e) { over = true; Aim(); }
        public void OnPointerExit(PointerEventData e) { over = false; Aim(); }
        public void OnPointerDown(PointerEventData e) { down = true; Aim(); }
        public void OnPointerUp(PointerEventData e) { down = false; Aim(); }
        void Aim() => target = !control.IsInteractable() ? 1 : down ? .94f : over ? 1.05f : 1;

        void Update()
        {
            if (!control.IsInteractable() && target != 1) target = 1;
            if (Mathf.Approximately(scale, target)) return;
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime * 1.6f);
            transform.localScale = Vector3.one * scale;
        }
    }
}
