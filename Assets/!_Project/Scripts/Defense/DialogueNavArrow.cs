using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // A text-free triangle with the Button's larger, transparent pointer target around it.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DialogueNavArrow : MaskableGraphic
    {
        [SerializeField] bool pointsLeft;
        public void Configure(bool left) { pointsLeft = left; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float tip = pointsLeft ? r.xMin : r.xMax, back = pointsLeft ? r.xMax : r.xMin;
            vh.AddVert(new Vector3(back, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(back, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(tip, r.center.y), color, Vector2.zero);
            if (pointsLeft) vh.AddTriangle(0, 2, 1); else vh.AddTriangle(0, 1, 2);
        }
    }
}
