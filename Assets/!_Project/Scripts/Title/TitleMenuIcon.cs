using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleMenuIcon : MaskableGraphic
    {
        public bool power;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * .4f;
            if (!power)
            {
                Vertex(vh, c + new Vector2(-radius * .65f, -radius));
                Vertex(vh, c + new Vector2(-radius * .65f, radius));
                Vertex(vh, c + new Vector2(radius, 0));
                vh.AddTriangle(0, 1, 2);
                return;
            }
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(40, 320, i / (float)segments) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(40, 320, (i + 1f) / segments) * Mathf.Deg2Rad;
                Quad(vh, c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius,
                    c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius * .68f,
                    c + new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * radius * .68f,
                    c + new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * radius);
            }
            Quad(vh, c + new Vector2(-radius * .15f, radius * .2f), c + new Vector2(-radius * .15f, radius * 1.12f),
                c + new Vector2(radius * .15f, radius * 1.12f), c + new Vector2(radius * .15f, radius * .2f));
        }
        void Vertex(VertexHelper vh, Vector2 p) => vh.AddVert(p, color, Vector2.zero);
        void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int n = vh.currentVertCount;
            Vertex(vh, a); Vertex(vh, b); Vertex(vh, c); Vertex(vh, d);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
