using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    // A resizable UI graphic; the editable block hierarchy is authored in prefabs.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CommandBlockShape : MaskableGraphic
    {
        [SerializeField] BlockSlotKind slot;
        [SerializeField] bool control;
        public float HeaderHeight { get; set; } = 48;
        public bool JoinNext { get; set; }
        readonly List<UIVertex> hitTriangles = new List<UIVertex>();
#if UNITY_EDITOR
        public IReadOnlyList<UIVertex> EditorTriangles => hitTriangles;
#endif

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var point)) return false;
            for (int i = 0; i < hitTriangles.Count; i += 3)
            {
                Vector2 a = hitTriangles[i].position, b = hitTriangles[i + 1].position, c = hitTriangles[i + 2].position;
                float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b), ca = Cross(a - c, point - c);
                if (Mathf.Abs(Cross(b - a, c - a)) > .0001f &&
                    (ab >= 0 && bc >= 0 && ca >= 0 || ab <= 0 && bc <= 0 && ca <= 0)) return true;
            }
            return false;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public void Configure(BlockSlotKind output, bool hasBody)
        {
            slot = output;
            control = hasBody;
            SetVerticesDirty();
        }

        // Sticker look (play feedback, Docs/References/비주얼_레퍼런스.md 4): every shape gets an ink outline, a block a soft drop shadow
        // under it too, and rectangles round corners. Shadow, outline and face are the same polygons grown and shifted, drawn in that
        // order, so a loop's header, arm and foot (overlapping rectangles) show one outline round the whole C and its mouth.
        const float Corner = 8, Shadow = 3, LiftedShadow = 9;
        // A dragged block's copy: its shadow falls further, as if held above the editor.
        public bool Lifted { get; set; }
        // 0..1: an unavailable block (out of stock, or left behind while dragged) mixed towards the paper, drawn opaque; alpha
        // would show the loop's overlapping header, arm and foot as darker seams.
        public float Fade { get; set; }
        static readonly Color FadeTo = new Color(.945f, .906f, .827f); // UITheme.PaperDeep
        Color Faded(Color c) => Fade > 0 ? Color.Lerp(c, new Color(FadeTo.r, FadeTo.g, FadeTo.b, c.a), Fade) : c;
        static readonly Color Ink = new Color(.149f, .165f, .243f);
        bool? isBlock;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            hitTriangles.Clear();
            Rect r = rectTransform.rect;
            float w = r.width, h = r.height;
            if (w <= 0 || h <= 0) return;
            if (isBlock == null) isBlock = GetComponent<CommandBlockView>();
            var parts = new List<Vector2[]>();
            if (slot == BlockSlotKind.Condition)
                parts.Add(new[] { new Vector2(0, h / 2), new Vector2(12, 0), new Vector2(w - 12, 0),
                    new Vector2(w, h / 2), new Vector2(w - 12, h), new Vector2(12, h) });
            else if (slot == BlockSlotKind.Value)
            {
                float radius = Mathf.Min(18, h / 2);
                var points = new Vector2[26];
                for (int i = 0; i < 13; i++)
                {
                    float angle = (-90 + i * 15) * Mathf.Deg2Rad;
                    points[i] = new Vector2(w - radius + Mathf.Cos(angle) * radius, h / 2 + Mathf.Sin(angle) * h / 2);
                    angle = (90 + i * 15) * Mathf.Deg2Rad;
                    points[i + 13] = new Vector2(radius + Mathf.Cos(angle) * radius, h / 2 + Mathf.Sin(angle) * h / 2);
                }
                parts.Add(points);
            }
            else
            {
                float header = control ? Mathf.Min(HeaderHeight, h) : h;
                parts.Add(Rounded(0, 0, w, header));
                if (control)
                {
                    // The arm reaches into the header and down over the foot, so the rounded corners leave no notch where they meet;
                    // the foot stays under the mouth (reaching up, it covered the mouth's last 8 px and crowded the lines inside).
                    parts.Add(Rounded(0, header - Corner, 18, h - header + Corner));
                    if (!JoinNext) parts.Add(Rounded(0, h - 18, w, 18));
                }
            }
            float line = isBlock.Value ? 3 : 2;
            var ink = Faded(new Color(Ink.r, Ink.g, Ink.b, color.a));
            if (isBlock.Value && Fade < 1) foreach (var part in parts) Polygon(vh, r, Grow(part, line), new Color(Ink.r, Ink.g, Ink.b, (Lifted ? .3f : .25f) * color.a * (1 - Fade)), Lifted ? LiftedShadow : Shadow);
            foreach (var part in parts) Polygon(vh, r, Grow(part, line), ink, 0);
            foreach (var part in parts) Polygon(vh, r, part, Faded(color), 0);
            vh.GetUIVertexStream(hitTriangles);
        }

        // A rectangle with round corners, clockwise from the top left (y downwards).
        static Vector2[] Rounded(float x, float y, float w, float h)
        {
            float c = Mathf.Min(Corner, w / 2, h / 2);
            var points = new Vector2[20];
            Vector2[] centres = { new Vector2(x + c, y + c), new Vector2(x + w - c, y + c), new Vector2(x + w - c, y + h - c), new Vector2(x + c, y + h - c) };
            for (int corner = 0; corner < 4; corner++)
                for (int i = 0; i < 5; i++)
                {
                    float angle = (180 + corner * 90 + i * 22.5f) * Mathf.Deg2Rad;
                    points[corner * 5 + i] = centres[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * c;
                }
            return points;
        }
        // The convex polygon pushed out by d along each corner's mitre.
        static Vector2[] Grow(Vector2[] points, float d)
        {
            var grown = new Vector2[points.Length];
            var centre = Vector2.zero; foreach (var p in points) centre += p; centre /= points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[(i + points.Length - 1) % points.Length], b = points[i], c = points[(i + 1) % points.Length];
                Vector2 n1 = Normal(a, b, centre), n2 = Normal(b, c, centre), n = (n1 + n2).normalized;
                grown[i] = b + n * (d / Mathf.Max(.35f, Vector2.Dot(n, n1)));
            }
            return grown;
        }
        static Vector2 Normal(Vector2 a, Vector2 b, Vector2 centre)
        {
            var edge = (b - a).normalized; var n = new Vector2(-edge.y, edge.x);
            if (b == a) return (b - centre).normalized;
            return Vector2.Dot(n, (a + b) * .5f - centre) < 0 ? -n : n; // outward
        }

        void Polygon(VertexHelper vh, Rect r, Vector2[] points, Color tint, float drop)
        {
            int start = vh.currentVertCount;
            foreach (var p in points) vh.AddVert(new Vector3(r.xMin + p.x, r.yMax - p.y - drop), tint, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
