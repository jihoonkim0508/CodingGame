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

        public void Configure(BlockSlotKind output, bool hasBody)
        {
            slot = output;
            control = hasBody;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
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
