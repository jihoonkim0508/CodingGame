using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class TutorialSpotlight : MaskableGraphic, ICanvasRaycastFilter
    {
        // clicks: where input passes through; holes: what stays undimmed (may differ, e.g. the placement area is clickable but not drawn).
        readonly List<Rect> clicks = new List<Rect>();
        readonly List<Rect> holes = new List<Rect>();
        readonly List<Rect> outlines = new List<Rect>();
        // Holes (and outlines) cut as the ellipse inside the rect, e.g. a robot's attack range on the ground.
        readonly List<Rect> rounds = new List<Rect>();
        // Holes whose outer corners are rounded (the road's pieces).
        readonly List<Rect> cornered = new List<Rect>();
        const float CornerRadius = 24;
        const float OutlineWidth = 3;
        const int RoundSegments = 64; // a multiple of 8, so the rect corners (45°) are sampled exactly
        const int RoundSlices = 4;
        bool dim = true;
        public void SetHoles(IEnumerable<Rect> clickable, IEnumerable<Rect> visible, IEnumerable<Rect> outlined, IEnumerable<Rect> round, IEnumerable<Rect> rounded, float padding, bool darken)
        {
            clicks.Clear(); holes.Clear(); outlines.Clear(); rounds.Clear(); cornered.Clear(); dim = darken;
            foreach (var area in clickable) if (Expand(area, padding, out var r)) clicks.Add(r);
            foreach (var area in visible) if (Expand(area, padding, out var r)) holes.Add(r);
            foreach (var area in outlined) if (Expand(area, padding, out var r)) outlines.Add(r);
            foreach (var area in round) if (Expand(area, padding, out var r)) rounds.Add(r);
            foreach (var area in rounded) if (Expand(area, padding, out var r)) cornered.Add(r);
            SetVerticesDirty();
        }
        bool Expand(Rect area, float padding, out Rect expanded)
        {
            expanded = Intersect(rectTransform.rect, Rect.MinMaxRect(area.xMin - padding, area.yMin - padding, area.xMax + padding, area.yMax + padding));
            return expanded.width > 0 && expanded.height > 0;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)) return true;
            foreach (var hole in clicks) if (hole.Contains(local)) return false;
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
                if (!dim || holes.Exists(h => h.Contains(center))) continue;
                AddQuad(vh, Rect.MinMaxRect(x0, y0, x1, y1), color);
            }
            // A round hole: the grid left its whole rect lit; dim the corners between the rect and the ellipse in small
            // pieces, leaving out only pieces that lie wholly inside another hole (e.g. the road).
            if (dim)
            {
                foreach (var r in rounds) DimOutside(vh, r, r, 0, RoundSegments, true);
                // A rounded corner: the same, on the quarter of a small circle tucked into that corner. Only outer corners of the
                // whole shape: no other piece just beside, above or past it (pieces overlap, and a turn's two pieces share it).
                var done = new List<Vector2>();
                foreach (var r in cornered)
                {
                    float radius = Mathf.Min(CornerRadius, r.width * .5f, r.height * .5f), step = radius * .5f;
                    for (int q = 0; q < 4; q++)
                    {
                        float sx = q == 0 || q == 3 ? 1 : -1, sy = q < 2 ? 1 : -1;
                        var corner = new Vector2(sx > 0 ? r.xMax : r.xMin, sy > 0 ? r.yMax : r.yMin);
                        if (done.Exists(p => (p - corner).sqrMagnitude < radius * radius) ||
                            Covered(r, corner + new Vector2(sx, -sy) * step) || Covered(r, corner + new Vector2(-sx, sy) * step) || Covered(r, corner + new Vector2(sx, sy) * step))
                            continue;
                        done.Add(corner);
                        var center = corner - new Vector2(sx, sy) * radius;
                        DimOutside(vh, new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), r, q * RoundSegments / 4, (q + 1) * RoundSegments / 4, false);
                    }
                }
            }
            var outline = new Color(1, .82f, .38f, 1);
            foreach (var r in outlines)
            {
                if (rounds.Contains(r))
                {
                    for (int i = 0; i < RoundSegments; i++)
                        AddQuad(vh, Ellipse(r, i, 1), Ellipse(r, i, 1, OutlineWidth), Ellipse(r, i + 1, 1, OutlineWidth), Ellipse(r, i + 1, 1), outline);
                    continue;
                }
                AddQuad(vh, Rect.MinMaxRect(r.xMin - OutlineWidth, r.yMin - OutlineWidth, r.xMax + OutlineWidth, r.yMin), outline);
                AddQuad(vh, Rect.MinMaxRect(r.xMin - OutlineWidth, r.yMax, r.xMax + OutlineWidth, r.yMax + OutlineWidth), outline);
                AddQuad(vh, Rect.MinMaxRect(r.xMin - OutlineWidth, r.yMin, r.xMin, r.yMax), outline);
                AddQuad(vh, Rect.MinMaxRect(r.xMax, r.yMin, r.xMax + OutlineWidth, r.yMax), outline);
            }
        }

        // Dims segments from..to of the gap between shape's border and its inscribed ellipse; skipInside leaves out pieces inside a hole other than owner.
        void DimOutside(VertexHelper vh, Rect shape, Rect owner, int from, int to, bool skipInside)
        {
            for (int i = from; i < to; i++)
            {
                Vector2 e0 = Ellipse(shape, i, 1), e1 = Ellipse(shape, i + 1, 1), s0 = Square(shape, i), s1 = Square(shape, i + 1);
                for (int k = 0; k < RoundSlices; k++)
                {
                    float t0 = k / (float)RoundSlices, t1 = (k + 1) / (float)RoundSlices;
                    Vector2 a = Vector2.Lerp(e0, s0, t0), b = Vector2.Lerp(e0, s0, t1), c = Vector2.Lerp(e1, s1, t1), d = Vector2.Lerp(e1, s1, t0);
                    if (skipInside && holes.Exists(h => h != owner && Inside(h, a) && Inside(h, b) && Inside(h, c) && Inside(h, d))) continue;
                    AddQuad(vh, a, b, c, d, color);
                }
            }
        }
        bool Covered(Rect self, Vector2 p) => holes.Exists(h => h != self && h.Contains(p));
        // Rect.Contains leaves out the max edges; neighbouring road pieces share edges, so allow a pixel.
        static bool Inside(Rect h, Vector2 p) => p.x >= h.xMin - 1 && p.x <= h.xMax + 1 && p.y >= h.yMin - 1 && p.y <= h.yMax + 1;

        static void AddQuad(VertexHelper vh, Rect r, Color color) =>
            AddQuad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), color);
        static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert; v.color = color;
            v.position = a; vh.AddVert(v);
            v.position = b; vh.AddVert(v);
            v.position = c; vh.AddVert(v);
            v.position = d; vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start + 2, start + 3, start);
        }

        static float Angle(int i) => i * 2 * Mathf.PI / RoundSegments;
        // Point i on the rect's inscribed ellipse, pushed out by grow units.
        static Vector2 Ellipse(Rect r, int i, float scale, float grow = 0) =>
            r.center + new Vector2(Mathf.Cos(Angle(i)) * (r.width * .5f * scale + grow), Mathf.Sin(Angle(i)) * (r.height * .5f * scale + grow));
        // Point i on the rect's border along the same direction (the 45° samples land on the corners).
        static Vector2 Square(Rect r, int i)
        {
            float c = Mathf.Cos(Angle(i)), s = Mathf.Sin(Angle(i)), m = Mathf.Max(Mathf.Abs(c), Mathf.Abs(s));
            return r.center + new Vector2(c / m * r.width * .5f, s / m * r.height * .5f);
        }

        static Rect Intersect(Rect a, Rect b)
        { return Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin), Mathf.Max(a.yMin,b.yMin), Mathf.Min(a.xMax,b.xMax), Mathf.Min(a.yMax,b.yMax)); }
    }
}
