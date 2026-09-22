using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    public sealed class CodeLineView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] BlockDropZone blockZone;
        [SerializeField] TMP_Text lineNumber;
        [SerializeField] LayoutElement indentSpace;
        [SerializeField] Button removeButton;
        [SerializeField, Min(0)] int indent;
        [SerializeField, Min(1)] float indentWidth = 28;
        BlockCodingPanel panel;

        public void Bind(BlockCodingPanel owner)
        {
            if (!blockZone || !lineNumber || !indentSpace || !removeButton)
                throw new InvalidOperationException($"{name}: 줄의 Inspector 참조를 모두 연결해 주세요.");
            panel = owner;
            blockZone.Bind(panel);
            removeButton.onClick.AddListener(Remove);
            UpdateIndent();
        }
        public void SetNumber(int number) => lineNumber.text = number.ToString();
        public IEnumerable<CodeBlockView> EnumerateBlocks() => blockZone.EnumerateBlocks();
        public CodeLine Read(bool allowIncomplete = false)
        {
            var line = new CodeLine { Indent = indent };
            foreach (var block in blockZone.Blocks) line.Blocks.Add(block.Read(allowIncomplete));
            return line;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) indent++;
            else if (eventData.button == PointerEventData.InputButton.Right) indent = Math.Max(0, indent - 1);
            else return;
            UpdateIndent();
            panel.MarkDirty();
            eventData.Use();
        }
        void UpdateIndent()
        {
            indentSpace.minWidth = indent * indentWidth;
            indentSpace.preferredWidth = indent * indentWidth;
        }
        void Remove() => panel.RemoveLine(this);
    }
}
