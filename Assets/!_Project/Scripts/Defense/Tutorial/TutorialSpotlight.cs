using System;
using System.Collections.Generic;
using System.Linq;
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
        // The round focus (TutorialStep.roundFocus): convex holes with rounded corners whose edge fades into the dark over
        // Feather units instead of a hard cut; drawn as a box left out of the grid, dimmed outside the feathered shape.
        readonly List<Vector2[]> softs = new List<Vector2[]>();
        readonly List<bool> softOutlined = new List<bool>();
        const float Feather = 28; // canvas units: px at the 1920x1080 reference
        const int FeatherRings = 4, CornerSegments = 8;
        // The frame (Docs/References/튜토리얼_포커스_조사.md 3-2): a sunny band with a thin ink edge outside it, 3:1 or more against
        // both the cream UI and the dark (the cyan line read 1.15:1 on cream).
        const float FrameWidth = 5, EdgeWidth = 2;
        // α .78 on the prefab dimmed only about half on screen (linear colour space); .92 gives about 70%.
        const float DimAlpha = .92f;
        const int RoundSegments = 64; // a multiple of 8, so the rect corners (45°) are sampled exactly
        const int RoundSlices = 4;
        bool dim = true;
        // Motion (the report's 3-3): a new target's hole and frame start a beat after its line (IntroDelay), close in from wide
        // with an ease-out, then a ripple leaves the frame every RippleEvery seconds: an onset catches the eye, a constant breath
        // does not. The dark fades in.
        const float IntroDelay = .35f, IntroSeconds = .5f, IntroGrow = 140, DimSeconds = .25f, RippleEvery = 1.75f, RippleSeconds = .6f, RippleGrow = 22;
        float introStart = -10, dimStart = -10;
        public const float IntroTotal = IntroDelay + IntroSeconds; // until a new hole has closed in on its target
        public void Pulse() => introStart = GameClock.Now;
        float Intro => Mathf.Clamp01((GameClock.Now - introStart - IntroDelay) / IntroSeconds);
        bool Waiting => GameClock.Now - introStart < IntroDelay;
        void Update() { if (dim || outlines.Count > 0 || softs.Count > 0) SetVerticesDirty(); }
        // Shown again after a wait (spotlight off): fade the dark in rather than snapping to it.
        protected override void OnEnable() { base.OnEnable(); dimStart = GameClock.Now; }
        // Call before SetHoles. boxes are not padded.
        public void SetRoad(IEnumerable<Rect> boxes, IEnumerable<Vector2[]> dimmed)
        {
            roadBoxes.Clear(); roadBoxes.AddRange(boxes); roadDim.Clear(); roadDim.AddRange(dimmed);
        }
        // Call before SetHoles. Each shape is convex (see RoundedRect), already padded.
        public void SetSoft(IEnumerable<(Vector2[] shape, bool outlined)> shapes)
        {
            softs.Clear(); softOutlined.Clear();
            foreach (var (shape, outlined) in shapes) { softs.Add(shape); softOutlined.Add(outlined); }
        }
        public void SetHoles(IEnumerable<Rect> clickable, IEnumerable<Rect> visible, IEnumerable<Rect> outlined, IEnumerable<Rect> round, float padding, bool darken)
        {
            clicks.Clear(); holes.Clear(); outlines.Clear(); rounds.Clear();
            if (darken && !dim) dimStart = GameClock.Now;
            dim = darken;
            foreach (var area in clickable) if (Expand(area, padding, out var r)) clicks.Add(r);
            foreach (var area in visible) if (Expand(area, padding, out var r)) holes.Add(r);
            // Frames are drawn outside their rect (7 px); keep them that far inside the screen so a target on the edge (the start
            // button) shows its whole frame.
            var screen = rectTransform.rect; const float Edge = 10;
            var inner = Rect.MinMaxRect(screen.xMin + Edge, screen.yMin + Edge, screen.xMax - Edge, screen.yMax - Edge);
            foreach (var area in outlined) if (Expand(area, padding, out var r)) outlines.Add(Intersect(inner, r));
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
            float k = Intro, grow = Mathf.Pow(1 - k, 3) * IntroGrow;
            // Grown for this frame only (the stored rects stay as set), so the hole closes in on its target.
            if (grow > .01f) { Grow(holes, grow); Grow(outlines, grow); Grow(rounds, grow); }
            var color = this.color; color.a = DimAlpha * Mathf.SmoothStep(0, 1, (GameClock.Now - dimStart) / DimSeconds);
            var soft = softs.ConvertAll(s => Offset(s, grow));
            try { Populate(vh, color, soft); }
            finally { if (grow > .01f) { Grow(holes, -grow); Grow(outlines, -grow); Grow(rounds, -grow); } }
        }
        static void Grow(List<Rect> rects, float by)
        {
            for (int i = 0; i < rects.Count; i++) { var r = rects[i]; rects[i] = Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by); }
        }

        void Populate(VertexHelper vh, Color color, List<List<Vector2>> soft)
        {
            var bounds = rectTransform.rect;
            var outer = soft.ConvertAll(s => Offset(s, Feather));
            var softBoxes = outer.ConvertAll(Box);
            var lit = new List<Rect>(holes); lit.AddRange(roadBoxes); lit.AddRange(softBoxes);
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
                    foreach (var h in holes) pieces = SubtractAll(pieces, rounds.Contains(h) ? EllipsePolygon(h) : Corners(h));
                    foreach (var s in soft) pieces = SubtractAll(pieces, s);
                    foreach (var p in pieces) AddPolygon(vh, p, color);
                }
                // A round-focus hole: its box minus the feathered shape (and the other holes in it), then the feather, clear
                // at the shape's edge and as dark as the rest at its outer edge (smoothstep over a few rings).
                for (int i = 0; i < soft.Count; i++)
                {
                    var pieces = Around(outer[i], softBoxes[i]);
                    foreach (var h in holes) pieces = SubtractAll(pieces, rounds.Contains(h) ? EllipsePolygon(h) : Corners(h));
                    foreach (var box in roadBoxes) pieces = SubtractAll(pieces, Corners(box));
                    for (int j = 0; j < soft.Count; j++) if (j != i) pieces = SubtractAll(pieces, soft[j]);
                    foreach (var p in pieces) AddPolygon(vh, p, color);
                    for (int k = 0; k < FeatherRings; k++)
                    {
                        float t0 = k / (float)FeatherRings, t1 = (k + 1) / (float)FeatherRings;
                        Color c0 = color, c1 = color; c0.a *= Mathf.SmoothStep(0, 1, t0); c1.a *= Mathf.SmoothStep(0, 1, t1);
                        Strip(vh, Offset(soft[i], t0 * Feather), Offset(soft[i], t1 * Feather), c0, c1);
                    }
                }
                // A round hole: the grid left its whole rect lit; dim the corners between the rect and the ellipse in small
                // pieces, leaving out pieces that lie wholly inside another hole and the parts in a road box (dimmed above).
                foreach (var r in rounds)
                    for (int i = 0; i < RoundSegments; i++)
                    {
                        Vector2 e0 = Ellipse(r, i), e1 = Ellipse(r, i + 1), s0 = Square(r, i), s1 = Square(r, i + 1);
                        for (int k = 0; k < RoundSlices; k++)
                        {
                            float t0 = k / (float)RoundSlices, t1 = (k + 1) / (float)RoundSlices;
                            Vector2 a = Vector2.Lerp(e0, s0, t0), b = Vector2.Lerp(e0, s0, t1), c = Vector2.Lerp(e1, s1, t1), d = Vector2.Lerp(e1, s1, t0);
                            if (holes.Exists(h => h != r && h.Contains(a) && h.Contains(b) && h.Contains(c) && h.Contains(d))) continue;
                            var pieces = new List<List<Vector2>> { new List<Vector2> { a, b, c, d } };
                            foreach (var box in roadBoxes) pieces = SubtractAll(pieces, Corners(box));
                            foreach (var p in pieces) AddPolygon(vh, p, color);
                        }
                    }
            }
            // Frames: drawn once the target's beat has come; a ripple leaves each one now and then after it has closed in.
            if (Waiting) return;
            float since = GameClock.Now - introStart - IntroDelay - IntroSeconds;
            float ripple = since < 0 ? -1 : since % RippleEvery / RippleSeconds;
            var sunny = UITheme.Sunny; var ripples = sunny; ripples.a = .8f * (1 - Mathf.Clamp01(ripple));
            void Frame(Action<float, float, Color> band)
            {
                band(0, FrameWidth, sunny);
                band(FrameWidth, FrameWidth + EdgeWidth, UITheme.Ink);
                if (ripple >= 0 && ripple < 1)
                {
                    float from = FrameWidth + EdgeWidth + ripple * RippleGrow;
                    band(from, from + 3, ripples);
                }
            }
            foreach (var r in outlines) Frame((from, to, c) => Band(vh, r, from, to, c));
            for (int i = 0; i < soft.Count; i++)
                if (softOutlined[i]) Frame((from, to, c) => Strip(vh, Offset(soft[i], from), Offset(soft[i], to), c, c));
        }
        // The ring between two offsets of one convex shape, each edge a quad from colour a (inner) to b (outer).
        static void Strip(VertexHelper vh, List<Vector2> inner, List<Vector2> outer, Color a, Color b)
        {
            int start = vh.currentVertCount, n = inner.Count;
            UIVertex v = UIVertex.simpleVert;
            for (int i = 0; i < n; i++)
            {
                v.color = a; v.position = inner[i]; vh.AddVert(v);
                v.color = b; v.position = outer[i]; vh.AddVert(v);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                vh.AddTriangle(start + 2 * i, start + 2 * i + 1, start + 2 * j + 1);
                vh.AddTriangle(start + 2 * j + 1, start + 2 * j, start + 2 * i);
            }
        }
        // A convex shape pushed out by d along each point's normal (the bisector of its two edges).
        static List<Vector2> Offset(IList<Vector2> shape, float d)
        {
            int n = shape.Count; var middle = Vector2.zero; foreach (var p in shape) middle += p; middle /= n;
            var result = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                var along = shape[(i + 1) % n] - shape[(i + n - 1) % n];
                var normal = new Vector2(along.y, -along.x).normalized;
                if (Vector2.Dot(normal, shape[i] - middle) < 0) normal = -normal;
                result.Add(shape[i] + normal * d);
            }
            return result;
        }
        // A rect with corners rounded by radius, as a convex polygon (CornerSegments per corner, counter-clockwise).
        public static Vector2[] RoundedRect(Rect r, float radius)
        {
            var points = new Vector2[4 * (CornerSegments + 1)];
            for (int q = 0; q < 4; q++)
            {
                var center = new Vector2(q == 0 || q == 3 ? r.xMax - radius : r.xMin + radius, q < 2 ? r.yMax - radius : r.yMin + radius);
                for (int i = 0; i <= CornerSegments; i++)
                {
                    float a = (q + i / (float)CornerSegments) * Mathf.PI * .5f;
                    points[q * (CornerSegments + 1) + i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                }
            }
            return points;
        }
        // The box minus a convex shape inside it, as one convex wedge per shape edge between rays from the shape's middle:
        // the wedges share the shape's own points, so no pinholes open along the feather (as cut pieces left).
        static List<List<Vector2>> Around(List<Vector2> shape, Rect box)
        {
            int n = shape.Count; var middle = Vector2.zero; foreach (var p in shape) middle += p; middle /= n;
            Vector2 Border(Vector2 p)
            {
                var d = p - middle;
                float tx = d.x > 0 ? (box.xMax - middle.x) / d.x : d.x < 0 ? (box.xMin - middle.x) / d.x : float.MaxValue;
                float ty = d.y > 0 ? (box.yMax - middle.y) / d.y : d.y < 0 ? (box.yMin - middle.y) / d.y : float.MaxValue;
                var hit = middle + d * Mathf.Min(tx, ty);
                // Exactly on the border, so the wedges meet the grid's quads without cracks.
                if (tx <= ty) hit.x = d.x > 0 ? box.xMax : box.xMin; else hit.y = d.y > 0 ? box.yMax : box.yMin;
                return hit;
            }
            float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
            var hits = new List<Vector2>(n); foreach (var p in shape) hits.Add(Border(p));
            var corners = Corners(box); var result = new List<List<Vector2>>(n);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 a = hits[i] - middle, b = hits[j] - middle; float turn = Mathf.Sign(Cross(a, b));
                var piece = new List<Vector2> { shape[i], hits[i] };
                // Box corners inside the wedge, in order from a to b.
                piece.AddRange(corners.Where(c => Cross(a, c - middle) * turn > 0 && Cross(c - middle, b) * turn > 0)
                    .OrderBy(c => Vector2.Angle(a, c - middle)));
                piece.Add(hits[j]); piece.Add(shape[j]);
                result.Add(piece);
            }
            return result;
        }
        static Rect Box(List<Vector2> points)
        {
            Vector2 min = points[0], max = points[0];
            foreach (var p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        // The ring between from and to px outside r (an ellipse for a round hole).
        void Band(VertexHelper vh, Rect r, float from, float to, Color color)
        {
            if (rounds.Contains(r))
            {
                for (int i = 0; i < RoundSegments; i++)
                    AddQuad(vh, Ellipse(r, i, from), Ellipse(r, i, to), Ellipse(r, i + 1, to), Ellipse(r, i + 1, from), color);
                return;
            }
            var inner = Rect.MinMaxRect(r.xMin - from, r.yMin - from, r.xMax + from, r.yMax + from);
            var outer = Rect.MinMaxRect(r.xMin - to, r.yMin - to, r.xMax + to, r.yMax + to);
            AddQuad(vh, Rect.MinMaxRect(outer.xMin, outer.yMin, outer.xMax, inner.yMin), color);
            AddQuad(vh, Rect.MinMaxRect(outer.xMin, inner.yMax, outer.xMax, outer.yMax), color);
            AddQuad(vh, Rect.MinMaxRect(outer.xMin, inner.yMin, inner.xMin, inner.yMax), color);
            AddQuad(vh, Rect.MinMaxRect(inner.xMax, inner.yMin, outer.xMax, inner.yMax), color);
        }

        public static List<Vector2> Corners(Rect r) =>
            new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) };
        // Each convex piece minus a convex cut.
        static List<List<Vector2>> SubtractAll(List<List<Vector2>> pieces, List<Vector2> cut)
        {
            var result = new List<List<Vector2>>();
            var sides = InsideEdges(cut);
            foreach (var poly in pieces)
            {
                if (!Overlaps(poly, cut)) { result.Add(poly); continue; }
                // The part outside each cut edge that is inside the edges before it.
                var rest = poly;
                foreach (var side in sides)
                {
                    if (rest.Count < 3) break;
                    var outside = Clip(rest, p => -side(p));
                    if (outside.Count >= 3) result.Add(outside);
                    rest = Clip(rest, side);
                }
            }
            return result;
        }
        // The part of a convex polygon inside a convex one.
        public static List<Vector2> ClipInside(List<Vector2> poly, IList<Vector2> convex)
        {
            foreach (var side in InsideEdges(convex)) if (poly.Count >= 3) poly = Clip(poly, side);
            return poly;
        }
        // Per edge of a convex polygon: >= 0 on the side facing its middle.
        static Func<Vector2, float>[] InsideEdges(IList<Vector2> convex)
        {
            var middle = Vector2.zero; foreach (var p in convex) middle += p; middle /= convex.Count;
            var sides = new Func<Vector2, float>[convex.Count];
            for (int i = 0; i < convex.Count; i++)
            {
                Vector2 a = convex[i], edge = convex[(i + 1) % convex.Count] - a;
                float sign = Mathf.Sign(edge.x * (middle - a).y - edge.y * (middle - a).x);
                sides[i] = p => sign * (edge.x * (p - a).y - edge.y * (p - a).x);
            }
            return sides;
        }
        static bool Overlaps(List<Vector2> a, List<Vector2> b) => Box(a).Overlaps(Box(b));
        static List<Vector2> EllipsePolygon(Rect r)
        {
            var points = new List<Vector2>(RoundSegments);
            for (int i = 0; i < RoundSegments; i++) points.Add(Ellipse(r, i));
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
        static Vector2 Ellipse(Rect r, int i, float grow = 0) =>
            r.center + new Vector2(Mathf.Cos(Angle(i)) * (r.width * .5f + grow), Mathf.Sin(Angle(i)) * (r.height * .5f + grow));
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
