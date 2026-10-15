using System;
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
        // A ground shape (the road): its box is left out of the grid and roadDim, already in perspective, dims the box off the road.
        readonly List<Rect> roadBoxes = new List<Rect>();
        readonly List<Vector2[]> roadDim = new List<Vector2[]>();
        const float OutlineWidth = 3;
        const int RoundSegments = 64; // a multiple of 8, so the rect corners (45°) are sampled exactly
        const int RoundSlices = 4;
        bool dim = true;
        // Call before SetHoles. boxes are not padded.
        public void SetRoad(IEnumerable<Rect> boxes, IEnumerable<Vector2[]> dimmed)
        {
            roadBoxes.Clear(); roadBoxes.AddRange(boxes); roadDim.Clear(); roadDim.AddRange(dimmed);
        }
        public void SetHoles(IEnumerable<Rect> clickable, IEnumerable<Rect> visible, IEnumerable<Rect> outlined, IEnumerable<Rect> round, float padding, bool darken)
        {
            clicks.Clear(); holes.Clear(); outlines.Clear(); rounds.Clear(); dim = darken;
            foreach (var area in clickable) if (Expand(area, padding, out var r)) clicks.Add(r);
            foreach (var area in visible) if (Expand(area, padding, out var r)) holes.Add(r);
            foreach (var area in outlined) if (Expand(area, padding, out var r)) outlines.Add(r);
            foreach (var area in round) if (Expand(area, padding, out var r)) rounds.Add(r);
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
            var lit = new List<Rect>(holes); lit.AddRange(roadBoxes);
            var xs = new List<float> { bounds.xMin, bounds.xMax };
            var ys = new List<float> { bounds.yMin, bounds.yMax };
            foreach (var r in lit) { xs.Add(r.xMin); xs.Add(r.xMax); ys.Add(r.yMin); ys.Add(r.yMax); }
            xs.Sort(); ys.Sort();
            for (int x = 0; x < xs.Count - 1; x++)
            for (int y = 0; y < ys.Count - 1; y++)
            {
                float x0 = xs[x], x1 = xs[x + 1], y0 = ys[y], y1 = ys[y + 1];
                if (x1 <= x0 || y1 <= y0) continue;
                var center = new Vector2((x0 + x1) * .5f, (y0 + y1) * .5f);
                if (!dim || lit.Exists(h => h.Contains(center))) continue;
                AddQuad(vh, Rect.MinMaxRect(x0, y0, x1, y1), color);
            }
            if (dim)
            {
                // The road box's dim, minus the other holes in it (a round hole as its ellipse).
                foreach (var shape in roadDim)
                {
                    var pieces = new List<List<Vector2>> { new List<Vector2>(shape) };
                    foreach (var h in holes)
                    {
                        var cut = rounds.Contains(h) ? EllipsePolygon(h) : new List<Vector2> { new Vector2(h.xMin, h.yMin), new Vector2(h.xMin, h.yMax), new Vector2(h.xMax, h.yMax), new Vector2(h.xMax, h.yMin) };
                        var next = new List<List<Vector2>>();
                        foreach (var p in pieces) Subtract(p, cut, next);
                        pieces = next;
                    }
                    foreach (var p in pieces) AddPolygon(vh, p, color);
                }
                // A round hole: the grid left its whole rect lit; dim the corners between the rect and the ellipse in small
                // pieces, leaving out pieces that lie wholly inside another hole and the parts in a road box (dimmed above).
                foreach (var r in rounds)
                    for (int i = 0; i < RoundSegments; i++)
                    {
                        Vector2 e0 = Ellipse(r, i, 1), e1 = Ellipse(r, i + 1, 1), s0 = Square(r, i), s1 = Square(r, i + 1);
                        for (int k = 0; k < RoundSlices; k++)
                        {
                            float t0 = k / (float)RoundSlices, t1 = (k + 1) / (float)RoundSlices;
                            Vector2 a = Vector2.Lerp(e0, s0, t0), b = Vector2.Lerp(e0, s0, t1), c = Vector2.Lerp(e1, s1, t1), d = Vector2.Lerp(e1, s1, t0);
                            if (holes.Exists(h => h != r && h.Contains(a) && h.Contains(b) && h.Contains(c) && h.Contains(d))) continue;
                            var pieces = new List<List<Vector2>> { new List<Vector2> { a, b, c, d } };
                            foreach (var box in roadBoxes)
                            {
                                var next = new List<List<Vector2>>();
                                foreach (var p in pieces) Subtract(p, new List<Vector2> { new Vector2(box.xMin, box.yMin), new Vector2(box.xMin, box.yMax), new Vector2(box.xMax, box.yMax), new Vector2(box.xMax, box.yMin) }, next);
                                pieces = next;
                            }
                            foreach (var p in pieces) AddPolygon(vh, p, color);
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

        // Convex polygon minus a convex cut, as convex pieces: the part outside each cut edge that is inside the edges before it.
        static void Subtract(List<Vector2> poly, List<Vector2> cut, List<List<Vector2>> into)
        {
            if (!Overlaps(poly, cut)) { into.Add(poly); return; }
            var inside = Vector2.zero; foreach (var p in cut) inside += p; inside /= cut.Count;
            var rest = poly;
            for (int i = 0; i < cut.Count && rest.Count >= 3; i++)
            {
                Vector2 a = cut[i], edge = cut[(i + 1) % cut.Count] - a;
                float sign = Mathf.Sign(edge.x * (inside - a).y - edge.y * (inside - a).x);
                Func<Vector2, float> side = p => sign * (edge.x * (p - a).y - edge.y * (p - a).x);
                var outside = Clip(rest, p => -side(p));
                if (outside.Count >= 3) into.Add(outside);
                rest = Clip(rest, side);
            }
        }
        static bool Overlaps(List<Vector2> a, List<Vector2> b)
        {
            Rect Box(List<Vector2> ps)
            {
                Vector2 min = ps[0], max = ps[0];
                foreach (var p in ps) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            return Box(a).Overlaps(Box(b));
        }
        static List<Vector2> EllipsePolygon(Rect r)
        {
            var points = new List<Vector2>(RoundSegments);
            for (int i = 0; i < RoundSegments; i++) points.Add(Ellipse(r, i, 1));
            return points;
        }
        // Keeps the part of a convex polygon where the linear function side is >= 0.
        public static List<Vector2> Clip(List<Vector2> poly, Func<Vector2, float> side)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float sa = side(a), sb = side(b);
                if (sa >= 0) result.Add(a);
                if (sa >= 0 != sb >= 0) result.Add(Vector2.Lerp(a, b, sa / (sa - sb)));
            }
            return result;
        }

        static void AddPolygon(VertexHelper vh, List<Vector2> points, Color color)
        {
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert; v.color = color;
            foreach (var p in points) { v.position = p; vh.AddVert(v); }
            for (int i = 1; i < points.Count - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
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
