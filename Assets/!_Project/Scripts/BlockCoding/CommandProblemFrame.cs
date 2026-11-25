using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    // A rounded ring round a block's line for 오류 (red) and 에러 (yellow); it takes no pointer hits.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CommandProblemFrame : MaskableGraphic
    {
        public float Width = 4, Radius = 14;
        const int Segments = 6;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width <= 2 * Width || r.height <= 2 * Width) return;
            float outer = Mathf.Min(Radius, Mathf.Min(r.width, r.height) * .5f), inner = Mathf.Max(0, outer - Width);
            var inside = new Rect(r.x + Width, r.y + Width, r.width - 2 * Width, r.height - 2 * Width);
            int points = (Segments + 1) * 4;
            for (int corner = 0; corner < 4; corner++)
                for (int j = 0; j <= Segments; j++)
                {
                    vh.AddVert(Corner(r, outer, corner, j), color, Vector2.zero);
                    vh.AddVert(Corner(inside, inner, corner, j), color, Vector2.zero);
                }
            for (int i = 0; i < points; i++)
            {
                int a = i * 2, b = (i + 1) % points * 2;
                vh.AddTriangle(a, b, a + 1); vh.AddTriangle(a + 1, b, b + 1);
            }
        }
        static Vector2 Corner(Rect rect, float radius, int corner, int segment)
        {
            var center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius, corner == 0 || corner == 3 ? rect.yMin + radius : rect.yMax - radius);
            float angle = (-90 + corner * 90 + segment * 90f / Segments) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
