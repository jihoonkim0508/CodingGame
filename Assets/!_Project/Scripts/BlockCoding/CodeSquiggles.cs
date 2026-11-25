using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    // Wavy underlines under whole lines of the Python tab's text: red for 오류, yellow for 에러. A child of the text, so it
    // scrolls and clips with it.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CodeSquiggles : MaskableGraphic
    {
        readonly List<(int line, Color color)> marks = new List<(int, Color)>();
        readonly List<(float from, float to, float y, Color color)> waves = new List<(float, float, float, Color)>();
        public IReadOnlyList<(int line, Color color)> Marks => marks;

        // lines count from 1.
        public void Show(TMP_Text text, IEnumerable<(int line, Color color)> lines)
        {
            marks.Clear(); marks.AddRange(lines);
            waves.Clear();
            text.ForceMeshUpdate();
            var info = text.textInfo;
            foreach (var (line, color) in marks)
            {
                if (line < 1 || line > info.lineCount) continue;
                var row = info.lineInfo[line - 1];
                float from = float.MaxValue, to = float.MinValue;
                for (int i = row.firstCharacterIndex; i <= row.lastCharacterIndex && i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i];
                    if (!c.isVisible) continue;
                    from = Mathf.Min(from, c.bottomLeft.x); to = Mathf.Max(to, c.topRight.x);
                }
                // A line of spaces only (a missing body): a short wave where it starts.
                if (from > to) { from = row.lineExtents.min.x; to = from + 40; }
                // Kept from the top-left corner (the text is laid out from there): the rect may be resized before it draws.
                var corner = text.rectTransform.rect;
                waves.Add((from - corner.xMin, Mathf.Max(to, from + 16) - corner.xMin, row.descender - 1 - corner.yMax, color));
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const float Step = 4, Height = 3f, Thick = 2.2f;
            var r = rectTransform.rect;
            foreach (var (left, right, top, tone) in waves)
            {
                float from = left + r.xMin, to = right + r.xMin, y = top + r.yMax;
                Vector2 previous = new Vector2(from, y);
                for (float x = from + Step; x <= to + .01f; x += Step)
                {
                    var point = new Vector2(x, y + (((int)((x - from) / Step)) % 2 == 0 ? 0 : -Height));
                    var normal = new Vector2(-(point - previous).y, (point - previous).x).normalized * Thick * .5f;
                    int start = vh.currentVertCount;
                    vh.AddVert(previous - normal, tone, Vector2.zero); vh.AddVert(previous + normal, tone, Vector2.zero);
                    vh.AddVert(point + normal, tone, Vector2.zero); vh.AddVert(point - normal, tone, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
                    previous = point;
                }
            }
        }
    }
}
