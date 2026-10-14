using UnityEngine;

namespace CodingGame.Defense
{
    // A bobbing arrow above a tutorial target. Areas come from TutorialInputBridge.TryGetArea (spotlight-local rects),
    // so this object must fill the spotlight's RectTransform with a centred pivot.
    public sealed class TutorialPointer : MonoBehaviour
    {
        [SerializeField] RectTransform arrow;
        [SerializeField, Min(0)] float gap = 12, bob = 10, bobSpeed = 4;
        Rect target;
        bool fromBelow;
        void Awake() { if (!arrow) throw new System.InvalidOperationException(name + ": 화살표 RectTransform 참조 누락"); Hide(); }
        public bool IsVisible => arrow.gameObject.activeSelf;
        public void Show(Rect area, bool below = false) { target = area; fromBelow = below; arrow.gameObject.SetActive(true); Place(); }
        public void Hide() => arrow.gameObject.SetActive(false);
        void LateUpdate() { if (IsVisible) Place(); }
        void Place()
        {
            float half = arrow.rect.height * .5f, bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * bobSpeed)) * bob;
            // Targets on the top edge (e.g. the editor's hint button) get the arrow underneath, pointing up.
            bool above = !fromBelow && target.yMax + gap + half * 2 + bob <= ((RectTransform)transform).rect.yMax;
            arrow.localEulerAngles = new Vector3(0, 0, above ? 0 : 180);
            arrow.anchoredPosition = new Vector2(target.center.x, above ? target.yMax + gap + half + bounce : target.yMin - gap - half - bounce);
        }
    }
}
