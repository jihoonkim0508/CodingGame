using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleButtonHighlight : MaskableGraphic
    {
        public float Visibility { get; private set; }
        bool pressed;
        public Vector2[] spriteOutline = new Vector2[0];
        public float outlineGap = 5;
        public bool showOutline;
        public void SetState(float visibility, bool isPressed)
        {
            visibility = Mathf.Clamp01(visibility);
            if (Mathf.Approximately(Visibility, visibility) && pressed == isPressed) return;
            Visibility = visibility;
            pressed = isPressed;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Visibility <= 0) return;
            Rect r = rectTransform.rect;
            Color cyan = new Color(.1f, .92f, 1, Visibility * (pressed ? .75f : 1));
            if (showOutline)
            {
            var points = new Vector2[spriteOutline.Length];
            var normals = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(r.xMin + spriteOutline[i].x * r.width, r.yMin + spriteOutline[i].y * r.height);
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 previous = (points[i] - points[(i + points.Length - 1) % points.Length]).normalized;
                Vector2 next = (points[(i + 1) % points.Length] - points[i]).normalized;
                Vector2 a = new Vector2(previous.y, -previous.x), b = new Vector2(next.y, -next.x);
                Vector2 normal = (a + b).normalized;
                normals[i] = normal / Mathf.Max(.4f, Vector2.Dot(normal, b));
            }
            Ring(vh, points, normals, 10, WithAlpha(cyan, .07f));
            Ring(vh, points, normals, 5, WithAlpha(cyan, .20f));
            Ring(vh, points, normals, 2.4f, WithAlpha(cyan, .95f));
            }
            // The play triangle left of the hovered button: it slides in toward the button, then gently nudges toward it.
            float nudge = Mathf.Sin(Time.unscaledTime * 5) * 2.5f * Visibility;
            Pointer(vh, new Vector2(r.xMin - 40 - 8 * (1 - Visibility) + nudge, r.center.y), cyan.a);
        }
        // Keeps the nudge moving while the triangle shows.
        void Update() { if (Visibility > 0) SetVerticesDirty(); }

        // A soft cyan glow, a navy rim like the buttons' outlines, a light-to-deep cyan fill and a gloss on its upper half,
        // all one rounded triangle (scaled about its incentre, so the rim is even on every side).
        const float HalfHeight = 21, Length = 38, Corner = 5;
        static readonly Color Rim = new Color(.04f, .11f, .22f), Light = new Color(.66f, 1, 1), Deep = new Color(0, .66f, 1),
            Glow = new Color(.1f, .92f, 1);
        static void Pointer(VertexHelper vh, Vector2 at, float alpha)
        {
            var shape = RoundedTriangle(at, out var incentre);
            float top = at.y + HalfHeight, bottom = at.y - HalfHeight;
            Fan(vh, Scale(shape, incentre, 2.2f), incentre, _ => WithAlpha(Glow, 0), WithAlpha(Glow, .4f * alpha));
            var rim = WithAlpha(Rim, alpha);
            Fan(vh, Scale(shape, incentre, 1.3f), incentre, _ => rim, rim);
            Color Shade(Vector2 p) => WithAlpha(Color.Lerp(Deep, Light, Mathf.InverseLerp(bottom, top, p.y)), alpha);
            Fan(vh, shape, incentre, Shade, Shade(incentre));
            var gloss = incentre + new Vector2(-2, 5);
            Color Shine(Vector2 p) => new Color(1, 1, 1, .55f * alpha * Mathf.InverseLerp(gloss.y - 4, top, p.y));
            Fan(vh, Scale(shape, gloss, .55f), gloss, Shine, Shine(gloss));
        }
        // Pointing right, centred on at; each corner an arc of radius Corner.
        static Vector2[] RoundedTriangle(Vector2 at, out Vector2 incentre)
        {
            var corners = new[] { at + new Vector2(-Length * .42f, -HalfHeight), at + new Vector2(-Length * .42f, HalfHeight), at + new Vector2(Length * .58f, 0) };
            float a = (corners[1] - corners[2]).magnitude, b = (corners[2] - corners[0]).magnitude, c = (corners[0] - corners[1]).magnitude;
            incentre = (a * corners[0] + b * corners[1] + c * corners[2]) / (a + b + c);
            var points = new Vector2[18];
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = corners[i], toPrevious = (corners[(i + 2) % 3] - p).normalized, toNext = (corners[(i + 1) % 3] - p).normalized;
                float half = Mathf.Acos(Mathf.Clamp(Vector2.Dot(toPrevious, toNext), -1, 1)) * .5f;
                Vector2 centre = p + (toPrevious + toNext).normalized * (Corner / Mathf.Sin(half));
                Vector2 start = p + toPrevious * (Corner / Mathf.Tan(half)), end = p + toNext * (Corner / Mathf.Tan(half));
                float from = Mathf.Atan2(start.y - centre.y, start.x - centre.x);
                float sweep = Mathf.DeltaAngle(from * Mathf.Rad2Deg, Mathf.Atan2(end.y - centre.y, end.x - centre.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                for (int k = 0; k < 6; k++) { float t = from + sweep * k / 5; points[i * 6 + k] = centre + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * Corner; }
            }
            return points;
        }
        static Vector2[] Scale(Vector2[] points, Vector2 about, float k)
        {
            var scaled = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) scaled[i] = about + (points[i] - about) * k;
            return scaled;
        }
        static void Fan(VertexHelper vh, Vector2[] ring, Vector2 centre, System.Func<Vector2, Color> tint, Color centreColor)
        {
            int first = vh.currentVertCount;
            vh.AddVert(centre, centreColor, Vector2.zero);
            foreach (var p in ring) vh.AddVert(p, tint(p), Vector2.zero);
            for (int i = 0; i < ring.Length; i++) vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % ring.Length);
        }
        void Ring(VertexHelper vh, Vector2[] points, Vector2[] normals, float width, Color color)
        {
            int first = vh.currentVertCount;
            for (int i = 0; i < points.Length; i++)
            {
                vh.AddVert(points[i] + normals[i] * (outlineGap - width * .5f), color, Vector2.zero);
                vh.AddVert(points[i] + normals[i] * (outlineGap + width * .5f), color, Vector2.zero);
            }
            for (int i = 0; i < points.Length; i++)
            {
                int a = first + i * 2, b = first + ((i + 1) % points.Length) * 2;
                vh.AddTriangle(a, a + 1, b + 1); vh.AddTriangle(a, b + 1, b);
            }
        }
        static Color WithAlpha(Color c,float multiplier){c.a*=multiplier;return c;}
    }
}
