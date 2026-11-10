using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    // A handful of slow edge embers. The menu's central reading area stays quiet.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleAtmosphere : MaskableGraphic
    {
        public float MotionTime { get; set; }
        void Update() { MotionTime += Time.unscaledDeltaTime; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            for (int i = 0; i < 32; i++)
            {
                float phase = Mathf.Repeat(i * .618034f + MotionTime * (.018f + i % 4 * .003f), 1);
                bool left = (i & 1) == 0;
                float x = Mathf.Lerp(.04f, .29f, Mathf.Repeat(i * .371f, 1));
                if (!left) x = 1 - x;
                float y = .08f + phase * .72f;
                Vector2 p = new Vector2(Mathf.Lerp(r.xMin, r.xMax, x) + Mathf.Sin(phase * 8 + i) * 8,
                    Mathf.Lerp(r.yMin, r.yMax, y));
                float alpha = Mathf.Sin(phase * Mathf.PI) * .42f;
                Color tint = left ? new Color(.16f, .68f, 1, alpha * .55f) : new Color(1, .38f, .1f, alpha);
                Dot(vh, p, 1.1f + i % 3 * .65f, tint);
            }
        }
        static void Dot(VertexHelper vh, Vector2 p, float radius, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(p, tint, Vector2.zero);
            for (int j = 0; j <= 8; j++)
            {
                float a = j * Mathf.PI / 4;
                vh.AddVert(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 2,
                    new Color(tint.r, tint.g, tint.b, 0), Vector2.zero);
            }
            for (int j = 0; j < 8; j++) vh.AddTriangle(n, n + j + 1, n + j + 2);
        }
    }
}
