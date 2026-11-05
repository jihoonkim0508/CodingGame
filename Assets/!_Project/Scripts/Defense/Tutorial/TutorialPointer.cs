using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // A bobbing arrow above a tutorial target, and a drag demo: a see-through copy of a palette block gliding to its slot.
    // The arrow is a sticker (ui_pointer, Editor/UI~/pointer_sticker.py: a chunky arrow with an ink outline) tinted the road
    // chevrons' sky blue on its own ink shadow, popping in when it appears; the arrow object's old yellow ▼ glyph is switched
    // off (play feedback: the small triangle did not read, and on the yellow tabs not at all).
    // Areas come from TutorialInputBridge.TryGetArea (spotlight-local rects), so this object must fill the spotlight's
    // RectTransform with a centred pivot.
    public sealed class TutorialPointer : MonoBehaviour
    {
        [SerializeField] RectTransform arrow;
        [SerializeField, Min(0)] float gap = 12, bob = 10, bobSpeed = 4;
        const float DragSeconds = 1f, HoldSeconds = .5f, RestSeconds = .5f, GhostAlpha = .65f, Lift = 14, GhostScale = 1.05f;
        const float PointerSize = 72, PointerShadow = 6, PointerPop = .3f;
        Image pointerShadow;
        float shownAt;
        Rect target;
        bool fromBelow;
        RectTransform ghostSource, ghost;
        CanvasGroup ghostGroup;
        Rect dragFrom, dragTo;
        float dragStarted;
        void Awake()
        {
            if (!arrow) throw new System.InvalidOperationException(name + ": 화살표 RectTransform 참조 누락");
            if (arrow.TryGetComponent<Graphic>(out var glyph)) glyph.enabled = false;
            arrow.sizeDelta = new Vector2(PointerSize, PointerSize);
            pointerShadow = Sticker("Shadow", new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, .3f));
            Sticker("Face", PathColor);
            Hide();
        }
        Image Sticker(string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(arrow, false);
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one; image.rectTransform.sizeDelta = Vector2.zero;
            image.sprite = Resources.Load<Sprite>("UI/ui_pointer"); image.color = color; image.raycastTarget = false;
            return image;
        }
        public bool IsVisible => arrow.gameObject.activeSelf;
        public bool IsDragVisible => ghost && ghost.gameObject.activeInHierarchy;
        public void Show(Rect area, bool below = false)
        {
            if (!IsVisible) shownAt = Time.unscaledTime;
            target = area; fromBelow = below; arrow.gameObject.SetActive(true); Place();
        }
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

        // Path arrows: a pack of ten chevron stickers (ui_chevron: open ›, ink outline, shadow, tinted sky blue) running fast along
        // a polyline (spotlight-local points) from its start to its end, then setting off again (play feedback: faster, and a
        // look of its own rather than copies of the yellow pointer). They grow in at the start and shrink out before the core,
        // and turn smoothly at corners (heading from a little behind to a little ahead).
        const int PathPack = 10;
        const float PathSpacing = 70, PathSpeed = 800, PathSize = 56, PathIn = 70, PathOut = 90, PathRest = .2f, PathTurn = 20;
        static readonly Color PathColor = new Color(.34f, .72f, 1); // sky #56B8FF; the outline carries the contrast on the cream road
        readonly List<RectTransform> chevrons = new List<RectTransform>();
        Sprite chevronSprite;
        public void ShowPath(IReadOnlyList<Vector2> points)
        {
            float total = 0;
            for (int i = 1; i < points.Count; i++) total += Vector2.Distance(points[i - 1], points[i]);
            if (!chevronSprite) chevronSprite = Resources.Load<Sprite>("UI/ui_chevron");
            while (chevrons.Count < PathPack)
            {
                var image = new GameObject("Path arrow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(transform, false);
                image.sprite = chevronSprite; image.preserveAspect = true; image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(PathSize, PathSize * 102 / 96f);
                chevrons.Add(image.rectTransform);
            }
            // The lead arrow's distance along the path: it runs past the end until the whole pack is off, rests, and restarts.
            float lap = total + (PathPack - 1) * PathSpacing + PathRest * PathSpeed;
            float lead = Time.unscaledTime * PathSpeed % lap;
            for (int k = 0; k < chevrons.Count; k++)
            {
                var chevron = chevrons[k];
                float s = lead - k * PathSpacing;
                if (s < 0 || s > total) { chevron.gameObject.SetActive(false); continue; }
                var dir = (At(points, Mathf.Min(total, s + PathTurn)) - At(points, Mathf.Max(0, s - PathTurn))).normalized;
                chevron.gameObject.SetActive(true);
                chevron.anchoredPosition = At(points, s);
                // The chevron sprite points right (0°); turn it to the travel direction.
                chevron.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                float grow = Mathf.SmoothStep(0, 1, s / PathIn), shrink = Mathf.SmoothStep(0, 1, (total - s) / PathOut);
                chevron.localScale = Vector3.one * Mathf.Lerp(.6f, 1, grow) * Mathf.Lerp(.85f, 1, shrink);
                var graphic = chevron.GetComponent<Graphic>();
                if (graphic) graphic.color = new Color(PathColor.r, PathColor.g, PathColor.b, Mathf.Min(grow, shrink));
            }
        }
        // The point at distance s along the polyline.
        static Vector2 At(IReadOnlyList<Vector2> points, float s)
        {
            for (int i = 1; i < points.Count; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]);
                if (s <= length || i == points.Count - 1) return Vector2.Lerp(points[i - 1], points[i], length > 0 ? Mathf.Clamp01(s / length) : 0);
                s -= length;
            }
            return points[0];
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
            // The shadow falls down the screen whichever way the arrow points (it turns with the arrow).
            pointerShadow.rectTransform.anchoredPosition = new Vector2(0, above ? -PointerShadow : PointerShadow);
            // Pops in with a small overshoot (ease-out-back), so the eye catches it starting.
            float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / PointerPop) - 1;
            arrow.localScale = Vector3.one * (1 + 2.70158f * t * t * t + 1.70158f * t * t);
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
            // Lifted off the palette block, so the copy reads as picked up rather than as the block's text printed twice.
            Vector2 start = new Vector2(dragFrom.xMin + size.x * .5f + Lift, dragFrom.center.y + Lift), end = new Vector2(dragTo.xMin + size.x * .5f, dragTo.center.y);
            ghost.anchoredPosition = Vector2.Lerp(start, end, move);
            ghostGroup.alpha = GhostAlpha * fade;
        }

        // A look-alike of the palette block: copied while inactive so none of its scripts run, then stripped to graphics and layout.
        RectTransform Ghost(RectTransform source)
        {
            var rect = DefenseViews.GraphicCopy(source, transform);
            rect.name = "Drag demo";
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one * (GhostScale * source.lossyScale.x / transform.lossyScale.x);
            ghostGroup = rect.GetComponent<CanvasGroup>();
            rect.gameObject.SetActive(true);
            return rect;
        }
    }
}
