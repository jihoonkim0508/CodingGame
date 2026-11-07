using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    // Vector artwork sized independently of the font glyphs on block headers.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CommandParenthesis : MaskableGraphic
    {
        [SerializeField] bool closing;
        [SerializeField] bool paired;

        internal static void ReplaceIn(TMP_Text label)
        {
            string content = label.text;
            bool opening = content.EndsWith("(") || content.EndsWith("()");
            bool closing = content.StartsWith(")") || content.EndsWith("()");
            if (!opening && !closing) return;

            label.text = content.Replace("(", "").Replace(")", "");
            int index = label.transform.GetSiblingIndex();
            if (label.text.Length == 0) label.gameObject.SetActive(false);
            else if (!content.StartsWith(")")) index++;
            Insert(label, index, closing && !opening, opening && closing);
        }

        static void Insert(TMP_Text label, int index, bool closing, bool paired)
        {
            string name = paired ? "Parentheses" : closing ? "Close Parenthesis" : "Open Parenthesis";
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CommandParenthesis), typeof(LayoutElement));
            obj.layer = label.gameObject.layer;
            obj.transform.SetParent(label.transform.parent, false);
            obj.transform.SetSiblingIndex(index);
            var glyph = obj.GetComponent<CommandParenthesis>();
            glyph.closing = closing;
            glyph.paired = paired;
            glyph.color = label.color;
            glyph.raycastTarget = false;
            var layout = obj.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = paired ? 14 : 7;
            layout.minHeight = layout.preferredHeight = 18;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // Feather the stroke in screen pixels so it stays smooth as the canvas scales.
            float scale = canvas ? canvas.scaleFactor : 1;
            float feather = .5f / Mathf.Max(.65f, scale);
            var center = rectTransform.rect.center;
            if (paired)
            {
                AddStroke(mesh, center + Vector2.left * 3, 1, feather);
                AddStroke(mesh, center + Vector2.right * 3, -1, feather);
            }
            else AddStroke(mesh, center, closing ? -1 : 1, feather);
        }

        void AddStroke(VertexHelper mesh, Vector2 center, float direction, float feather)
        {
            const int curveSteps = 16, capSteps = 5;
            const float halfHeight = 6.8f, bend = 2.7f, radius = 1.05f;
            Color edge = color;
            edge.a = 0;

            for (int step = -capSteps; step <= curveSteps + capSteps; step++)
            {
                int curveStep = Mathf.Clamp(step, 0, curveSteps);
                float t = 2f * curveStep / curveSteps - 1;
                var point = center + new Vector2(direction * bend * (t * t - .5f), t * halfHeight);
                var tangent = new Vector2(direction * 2 * bend * t, halfHeight).normalized;
                var normal = new Vector2(tangent.y, -tangent.x);
                float cap = (step - curveStep) * (Mathf.PI / (2 * capSteps));
                var along = tangent * Mathf.Sin(cap);
                var across = normal * Mathf.Cos(cap);
                float inner = radius - feather, outer = radius + feather;

                int row = mesh.currentVertCount;
                mesh.AddVert(point + (along - across) * outer, edge, Vector2.zero);
                mesh.AddVert(point + (along - across) * inner, color, Vector2.zero);
                mesh.AddVert(point + (along + across) * inner, color, Vector2.zero);
                mesh.AddVert(point + (along + across) * outer, edge, Vector2.zero);
                if (step == -capSteps) continue;

                for (int column = 0; column < 3; column++)
                {
                    int bottom = row - 4 + column, top = row + column;
                    mesh.AddTriangle(bottom, top, bottom + 1);
                    mesh.AddTriangle(bottom + 1, top, top + 1);
                }
            }
        }
    }
}
