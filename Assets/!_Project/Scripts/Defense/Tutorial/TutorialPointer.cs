using UnityEngine;

namespace CodingGame.Defense
{
    // A bobbing arrow above a tutorial target. Areas come from TutorialInputBridge.TryGetArea (spotlight-local rects),
    // so this object must sit under the spotlight's RectTransform with a centred anchor.
    public sealed class TutorialPointer : MonoBehaviour
    {
        [SerializeField] RectTransform arrow;
        [SerializeField, Min(0)] float gap = 12, bob = 10, bobSpeed = 4;
        Rect target;
        void Awake() { if (!arrow) throw new System.InvalidOperationException(name + ": 화살표 RectTransform 참조 누락"); Hide(); }
        public bool IsVisible => arrow.gameObject.activeSelf;
        public void Show(Rect area) { target = area; arrow.gameObject.SetActive(true); Place(); }
        public void Hide() => arrow.gameObject.SetActive(false);
        void LateUpdate() { if (IsVisible) Place(); }
        void Place() => arrow.anchoredPosition = new Vector2(target.center.x,
            target.yMax + gap + arrow.rect.height * .5f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * bobSpeed)) * bob);
    }
}
