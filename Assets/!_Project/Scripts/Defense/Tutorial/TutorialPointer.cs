using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // A bobbing arrow above a tutorial target, and a drag demo: a see-through copy of a palette block gliding to its slot.
    // Areas come from TutorialInputBridge.TryGetArea (spotlight-local rects), so this object must fill the spotlight's
    // RectTransform with a centred pivot.
    public sealed class TutorialPointer : MonoBehaviour
    {
        [SerializeField] RectTransform arrow;
        [SerializeField, Min(0)] float gap = 12, bob = 10, bobSpeed = 4;
        const float DragSeconds = 1f, HoldSeconds = .5f, RestSeconds = .5f, GhostAlpha = .65f;
        Rect target;
        bool fromBelow;
        RectTransform ghostSource, ghost;
        CanvasGroup ghostGroup;
        Rect dragFrom, dragTo;
        float dragStarted;
        void Awake() { if (!arrow) throw new System.InvalidOperationException(name + ": 화살표 RectTransform 참조 누락"); Hide(); }
        public bool IsVisible => arrow.gameObject.activeSelf;
        public void Show(Rect area, bool below = false) { target = area; fromBelow = below; arrow.gameObject.SetActive(true); Place(); }
        public void Hide() { HideArrow(); HideDrag(); }
        public void HideArrow() => arrow.gameObject.SetActive(false);

        public void ShowDrag(RectTransform source, Rect from, Rect to)
        {
            if (source != ghostSource) { HideDrag(); ghost = Ghost(source); ghostSource = source; dragStarted = Time.unscaledTime; }
            dragFrom = from; dragTo = to;
            Drag();
        }
        public void HideDrag()
        {
            if (ghost) Destroy(ghost.gameObject);
            ghost = null; ghostSource = null;
        }

        void LateUpdate() { if (IsVisible) Place(); if (ghost) Drag(); }
        void Place()
        {
            float half = arrow.rect.height * .5f, bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * bobSpeed)) * bob;
            // Targets on the top edge (e.g. the editor's hint button) get the arrow underneath, pointing up.
            bool above = !fromBelow && target.yMax + gap + half * 2 + bob <= ((RectTransform)transform).rect.yMax;
            arrow.localEulerAngles = new Vector3(0, 0, above ? 0 : 180);
            arrow.anchoredPosition = new Vector2(target.center.x, above ? target.yMax + gap + half + bounce : target.yMin - gap - half - bounce);
        }

        // Glide from the palette item to the slot (the block's left edge to the slot's left edge), hold, fade, rest, repeat.
        void Drag()
        {
            float t = (Time.unscaledTime - dragStarted) % (DragSeconds + HoldSeconds + RestSeconds);
            float move = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / DragSeconds));
            float fade = t < DragSeconds ? 1 : 1 - Mathf.Clamp01((t - DragSeconds) / HoldSeconds);
            // The palette lays its items out a frame after a tab switch, so the size is followed rather than copied once.
            ghost.sizeDelta = ghostSource.rect.size;
            var size = ghost.rect.size * ghost.localScale.x;
            Vector2 start = new Vector2(dragFrom.xMin + size.x * .5f, dragFrom.center.y), end = new Vector2(dragTo.xMin + size.x * .5f, dragTo.center.y);
            ghost.anchoredPosition = Vector2.Lerp(start, end, move);
            ghostGroup.alpha = GhostAlpha * fade;
        }

        // A look-alike of the palette block: copied while inactive so none of its scripts run, then stripped to graphics and layout.
        RectTransform Ghost(RectTransform source)
        {
            var holder = new GameObject("Drag demo", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            var copy = (RectTransform)Instantiate(source.gameObject, holder.transform, false).transform;
            // Reversed: a component that requires another goes first.
            foreach (var behaviour in System.Linq.Enumerable.Reverse(copy.GetComponentsInChildren<MonoBehaviour>(true)))
                if (!(behaviour is Graphic || behaviour is LayoutGroup || behaviour is ContentSizeFitter || behaviour is LayoutElement || behaviour is RectMask2D || behaviour is Mask))
                    DestroyImmediate(behaviour);
            // The palette item's stock count ("x1") is not part of the block.
            foreach (var label in copy.GetComponentsInChildren<Transform>(true)) if (label && label.name == "Stock") DestroyImmediate(label.gameObject);
            var rect = (RectTransform)holder.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one * (source.lossyScale.x / transform.lossyScale.x);
            copy.anchorMin = Vector2.zero; copy.anchorMax = Vector2.one; copy.pivot = new Vector2(.5f, .5f);
            copy.offsetMin = copy.offsetMax = Vector2.zero; copy.localScale = Vector3.one;
            ghostGroup = holder.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false; ghostGroup.interactable = false;
            holder.SetActive(true);
            return rect;
        }
    }
}
