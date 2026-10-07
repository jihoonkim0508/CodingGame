using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class TutorialSpotlight : MaskableGraphic, ICanvasRaycastFilter
    {
        readonly List<Rect> holes = new List<Rect>();
        readonly List<Rect> outlines = new List<Rect>();
        const float OutlineWidth = 3;
        public void SetHoles(IEnumerable<Rect> areas, float padding)
        {
            holes.Clear(); outlines.Clear();
            var bounds = rectTransform.rect;
            foreach (var area in areas)
            {
                var expanded = Rect.MinMaxRect(area.xMin - padding, area.yMin - padding,
                    area.xMax + padding, area.yMax + padding);
                expanded = Intersect(bounds, expanded);
                if (expanded.width > 0 && expanded.height > 0) { holes.Add(expanded); outlines.Add(expanded); }
            }
            SetVerticesDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)) return true;
            foreach (var hole in holes) if (hole.Contains(local)) return false;
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var bounds = rectTransform.rect;
            var xs = new List<float> { bounds.xMin, bounds.xMax };
            var ys = new List<float> { bounds.yMin, bounds.yMax };
            foreach (var r in holes) { xs.Add(r.xMin); xs.Add(r.xMax); ys.Add(r.yMin); ys.Add(r.yMax); }
            xs.Sort(); ys.Sort();
            for (int x = 0; x < xs.Count - 1; x++)
            for (int y = 0; y < ys.Count - 1; y++)
            {
                float x0 = xs[x], x1 = xs[x + 1], y0 = ys[y], y1 = ys[y + 1];
                if (x1 <= x0 || y1 <= y0) continue;
                var center = new Vector2((x0 + x1) * .5f, (y0 + y1) * .5f);
                if (holes.Exists(h => h.Contains(center))) continue;
                AddQuad(vh, Rect.MinMaxRect(x0, y0, x1, y1), color);
            }
            var outline = new Color(1, .82f, .38f, 1);
            foreach (var r in outlines)
            {
                AddQuad(vh, Rect.MinMaxRect(r.xMin - OutlineWidth, r.yMin - OutlineWidth, r.xMax + OutlineWidth, r.yMin), outline);
                AddQuad(vh, Rect.MinMaxRect(r.xMin - OutlineWidth, r.yMax, r.xMax + OutlineWidth, r.yMax + OutlineWidth), outline);
                AddQuad(vh, Rect.MinMaxRect(r.xMin - OutlineWidth, r.yMin, r.xMin, r.yMax), outline);
                AddQuad(vh, Rect.MinMaxRect(r.xMax, r.yMin, r.xMax + OutlineWidth, r.yMax), outline);
            }
        }

        static void AddQuad(VertexHelper vh, Rect r, Color color)
        {
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert; v.color = color;
            v.position = new Vector3(r.xMin, r.yMin); vh.AddVert(v);
            v.position = new Vector3(r.xMin, r.yMax); vh.AddVert(v);
            v.position = new Vector3(r.xMax, r.yMax); vh.AddVert(v);
            v.position = new Vector3(r.xMax, r.yMin); vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start + 2, start + 3, start);
        }

        static Rect Intersect(Rect a, Rect b)
        { return Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin), Mathf.Max(a.yMin,b.yMin), Mathf.Min(a.xMax,b.xMax), Mathf.Min(a.yMax,b.yMax)); }
    }
}
