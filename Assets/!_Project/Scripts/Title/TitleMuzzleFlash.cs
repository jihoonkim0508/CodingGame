using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleMuzzleFlash : MaskableGraphic
    {
        public float Intensity { get; set; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Intensity <= 0) return;
            var r = rectTransform.rect;
            Color center = new Color(1, .91f, .47f, .8f * Intensity);
            vh.AddVert(r.center, center, Vector2.zero);
            for (int i = 0; i <= 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                float radius = (i & 1) == 0 ? .5f : .16f;
                vh.AddVert(r.center + new Vector2(Mathf.Cos(angle) * r.width, Mathf.Sin(angle) * r.height) * radius,
                    new Color(1, .56f, .12f, 0), Vector2.zero);
            }
            for (int i = 0; i < 16; i++) vh.AddTriangle(0, i + 1, i + 2);
        }
    }
}
