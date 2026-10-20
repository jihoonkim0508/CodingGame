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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            hitTriangles.Clear();
            Rect r = rectTransform.rect;
            float w = r.width, h = r.height;
            if (w <= 0 || h <= 0) return;
            if (slot == BlockSlotKind.Condition)
                Polygon(vh, r, new[] { new Vector2(0, h / 2), new Vector2(12, 0), new Vector2(w - 12, 0),
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
                Polygon(vh, r, points);
            }
            else
            {
                float header = control ? Mathf.Min(HeaderHeight, h) : h;
                Quad(vh, r, 0, 0, w, header);
                if (control)
                {
                    Quad(vh, r, 0, header, 18, h - header);
                    if (!JoinNext) Quad(vh, r, 0, h - 18, w, 18);
                }
            }
            vh.GetUIVertexStream(hitTriangles);
        }

        void Quad(VertexHelper vh, Rect r, float x, float y, float w, float h)
        {
            if (w <= 0 || h <= 0) return;
            Polygon(vh, r, new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) });
        }

        void Polygon(VertexHelper vh, Rect r, Vector2[] points)
        {
            int start = vh.currentVertCount;
            foreach (var p in points) vh.AddVert(new Vector3(r.xMin + p.x, r.yMax - p.y), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
