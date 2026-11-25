using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    // The code area's view (2026-11-25 request): a drag on empty space pans it (the ScrollRect above, unclamped), the wheel and
    // the +/− buttons zoom it round the pointer, and a note at the edge points to blocks lying out of view. On the Viewport,
    // so the wheel over the code reaches it before the ScrollRect; added by CommandCodingPanel.
    public sealed class CommandWorkspace : MonoBehaviour, IScrollHandler, IPointerClickHandler
    {
        public const float MinZoom = .5f, MaxZoom = 1.5f;
        const float Margin = 16;
        CommandCodingPanel panel;
        ScrollRect scroll;
        RectTransform view, content, note, arrow;
        Image noteImage;
        IReadOnlyList<RectTransform> target;
        Rect targetBounds;
        readonly Vector3[] corners = new Vector3[4];
        public float Zoom => content.localScale.x;
        // The out-of-view block the note points to (null while every block is in view).
        public RectTransform OffscreenTarget => note && note.gameObject.activeSelf ? target[0] : null;
        public RectTransform Note => note;

        internal void Bind(CommandCodingPanel owner, ScrollRect area)
        {
            panel = owner; scroll = area; content = area.content;
            view = (RectTransform)transform;
            area.movementType = ScrollRect.MovementType.Unrestricted;
            ZoomButton("+", 0, 1.15f); ZoomButton("−", 1, 1 / 1.15f);
            BuildNote();
        }
        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) < .01f) return;
            ZoomAt(e.position, e.pressEventCamera ?? e.enterEventCamera, Zoom * Mathf.Pow(1.1f, Mathf.Sign(e.scrollDelta.y)));
        }
        // Empty space beside the program's own rect: clicks go to the program as if on it (select, right-click 붙여넣기).
        public void OnPointerClick(PointerEventData e) { if (e.pointerPressRaycast.gameObject == gameObject) ExecuteEvents.Execute(content.gameObject, e, ExecuteEvents.pointerClickHandler); }
        // Scales the code round screen point (it stays over the same spot of code).
        public void ZoomAt(Vector2 screen, Camera camera, float zoom)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, camera, out var pointer)) return;
            scroll.StopMovement();
            var origin = new Vector2(view.rect.xMin, view.rect.yMax);
            var spot = (pointer - origin - content.anchoredPosition) / Zoom;
            content.localScale = new Vector3(zoom, zoom, 1);
            content.anchoredPosition = pointer - origin - spot * zoom;
        }
        void ZoomStep(float factor) => ZoomAt(RectTransformUtility.WorldToScreenPoint(Camera(), view.TransformPoint(view.rect.center)), Camera(), Zoom * factor);
        Camera Camera() { var canvas = GetComponentInParent<Canvas>(); return canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null; }
        // Pans so that the code-local top-left point sits Margin in from the view's top-left (zoom unchanged).
        public void ShowAtTopLeft(Vector2 codePoint)
        {
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(Margin, -Margin) - codePoint * Zoom;
        }
        // Pans the block the note points to into the middle of the view.
        public void RevealTarget()
        {
            if (target == null) return;
            scroll.StopMovement();
            content.anchoredPosition += view.rect.center - targetBounds.center;
        }
        // Pans up just enough to bring block's bottom into the view (a line added under a long program).
        public void KeepInView(RectTransform block)
        {
            float below = view.rect.yMin + Margin - Bounds(new[] { block }).yMin;
            if (below <= 0) return;
            scroll.StopMovement();
            content.anchoredPosition += new Vector2(0, below);
        }
        // A chain's blocks together, in the view's units.
        Rect Bounds(IReadOnlyList<RectTransform> chain)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view, chain[0]);
            for (int i = 1; i < chain.Count; i++) bounds.Encapsulate(RectTransformUtility.CalculateRelativeRectTransformBounds(view, chain[i]));
            return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
        }

        void ZoomButton(string label, int index, float factor)
        {
            var button = new GameObject("Zoom" + (index == 0 ? "In" : "Out"), typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            button.SetParent(view, false);
            button.anchorMin = button.anchorMax = button.pivot = new Vector2(1, 0);
            button.sizeDelta = new Vector2(40, 40); button.anchoredPosition = new Vector2(-10 - index * 46, 10);
            var click = button.GetComponent<Button>(); click.targetGraphic = UITheme.Style(button.GetComponent<Image>(), UITheme.Button, UITheme.Sky);
            click.onClick.AddListener(() => ZoomStep(factor));
            Label(button, label, 26, Color.white);
        }
        void BuildNote()
        {
            note = new GameObject("OffscreenNote", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            note.SetParent(view, false);
            note.anchorMin = note.anchorMax = note.pivot = new Vector2(.5f, .5f); note.sizeDelta = new Vector2(268, 44);
            noteImage = UITheme.Style(note.GetComponent<Image>(), UITheme.Pill, UITheme.Hex(0xFFD43B));
            var click = note.GetComponent<Button>(); click.targetGraphic = noteImage;
            click.onClick.AddListener(RevealTarget);
            arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            arrow.SetParent(note, false);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0, .5f); arrow.sizeDelta = new Vector2(28, 28); arrow.anchoredPosition = new Vector2(26, 0);
            var image = arrow.GetComponent<Image>(); image.sprite = Resources.Load<Sprite>("UI/ui_arrow"); image.preserveAspect = true; image.raycastTarget = false;
            var text = Label(note, "동떨어진 블록이 있어요", 19, UITheme.Ink);
            text.rectTransform.offsetMin = new Vector2(40, 0);
            note.gameObject.SetActive(false);
        }
        static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            if (UITheme.Bold) label.font = UITheme.Bold;
            label.text = text; label.fontSize = size; label.color = color; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return label;
        }

        // The note points to the nearest chain (or the def block) lying wholly outside the view, at the view's edge on its side.
        void LateUpdate()
        {
            if (!note) return;
            var area = view.rect;
            target = null; float nearest = float.MaxValue;
            foreach (var chain in panel.Chains())
            {
                var bounds = Bounds(chain);
                if (bounds.Overlaps(area)) continue;
                float distance = (bounds.center - area.center).sqrMagnitude;
                if (distance < nearest) { nearest = distance; target = chain; targetBounds = bounds; }
            }
            note.gameObject.SetActive(target != null && !panel.Dragged);
            if (!note.gameObject.activeSelf) return;
            var direction = (targetBounds.center - area.center).normalized;
            // On the edge the direction meets, kept whole inside the view (and clear of the zoom buttons at the bottom right).
            var half = note.sizeDelta * .5f + new Vector2(10, 10);
            var room = new Vector2(area.width * .5f - half.x, area.height * .5f - half.y - 30);
            float reach = Mathf.Min(Mathf.Abs(direction.x) > .001f ? room.x / Mathf.Abs(direction.x) : float.MaxValue,
                Mathf.Abs(direction.y) > .001f ? room.y / Mathf.Abs(direction.y) : float.MaxValue);
            note.anchoredPosition = direction * reach;
            // ui_arrow points down.
            arrow.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90);
        }
    }
}
