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
            // Filled play triangle with a single color and no contrasting border.
            Vector2 center = new Vector2(r.xMin - 39 - 8 * (1 - Visibility), r.center.y);
            Triangle(vh, center, 20, cyan);
        }
        static void Triangle(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            int first = vh.currentVertCount;
            vh.AddVert(center + new Vector2(-radius * .65f, -radius), color, Vector2.zero);
            vh.AddVert(center + new Vector2(-radius * .65f, radius), color, Vector2.zero);
            vh.AddVert(center + new Vector2(radius, 0), color, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
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
