using System.Collections.Generic;
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
        public bool IsDragVisible => ghost && ghost.gameObject.activeInHierarchy;
        public void Show(Rect area, bool below = false) { target = area; fromBelow = below; arrow.gameObject.SetActive(true); Place(); }
        public void Hide() { HideArrow(); HideDrag(); HidePath(); }
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

        // Path arrows: copies of the arrow marching along a polyline (spotlight-local points), fading in and out at the ends.
        const float PathSpacing = 80, PathSpeed = 140, PathScale = .7f, PathFade = 40;
        static readonly Color PathColor = new Color(.45f, .85f, 1); // sky blue (planner, 1-1 A04a)
        readonly List<RectTransform> chevrons = new List<RectTransform>();
        public void ShowPath(IReadOnlyList<Vector2> points)
        {
            float total = 0;
            for (int i = 1; i < points.Count; i++) total += Vector2.Distance(points[i - 1], points[i]);
            int count = Mathf.Max(1, Mathf.FloorToInt(total / PathSpacing));
            while (chevrons.Count < count)
            {
                var copy = (RectTransform)Instantiate(arrow.gameObject, transform, false).transform;
                copy.name = "Path arrow"; copy.localScale = Vector3.one * PathScale; chevrons.Add(copy);
            }
            float offset = Time.unscaledTime * PathSpeed % PathSpacing;
            for (int k = 0; k < chevrons.Count; k++)
            {
                var chevron = chevrons[k];
                float s = k * PathSpacing + offset;
                if (k >= count || s > total) { chevron.gameObject.SetActive(false); continue; }
                // Walk to distance s along the polyline.
                int seg = 1; float left = s;
                while (seg < points.Count - 1 && left > Vector2.Distance(points[seg - 1], points[seg])) { left -= Vector2.Distance(points[seg - 1], points[seg]); seg++; }
                Vector2 a = points[seg - 1], b = points[seg], dir = (b - a).normalized;
                chevron.gameObject.SetActive(true);
                chevron.anchoredPosition = a + dir * left;
                // The arrow sprite points down (0°); turn it to the travel direction.
                chevron.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90);
                var image = chevron.GetComponent<Graphic>();
                if (image) image.color = new Color(PathColor.r, PathColor.g, PathColor.b, .95f * Mathf.Clamp01(Mathf.Min(s, total - s) / PathFade));
            }
        }
        public void HidePath() { foreach (var chevron in chevrons) if (chevron) chevron.gameObject.SetActive(false); }

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
            var rect = DefenseViews.GraphicCopy(source, transform);
            rect.name = "Drag demo";
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one * (source.lossyScale.x / transform.lossyScale.x);
            ghostGroup = rect.GetComponent<CanvasGroup>();
            rect.gameObject.SetActive(true);
            return rect;
        }
    }
}
