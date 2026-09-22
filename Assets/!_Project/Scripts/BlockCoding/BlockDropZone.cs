using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CodingGame.BlockCoding
{
    public enum SlotValue { Any, Number, Enemy }

    public sealed class BlockDropZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] RectTransform content;
        [SerializeField] bool argumentSlot;
        [SerializeField] SlotValue acceptedValue;
        [SerializeField] GameObject emptyHint;
        [SerializeField] List<CodeBlockView> blocks = new List<CodeBlockView>();
        public IReadOnlyList<CodeBlockView> Blocks => blocks;
        public BlockCodingPanel Panel { get; private set; }

        public void Bind(BlockCodingPanel panel)
        {
            if (!content) throw new InvalidOperationException($"{name}: Content 참조가 필요합니다.");
            Panel = panel;
            if (argumentSlot && !emptyHint) throw new InvalidOperationException($"{name}: Empty Hint 참조가 필요합니다.");
            foreach (var block in blocks)
            {
                if (!block) throw new InvalidOperationException($"{name}: 초기 블록 참조가 비어 있습니다.");
                block.Bind(this);
            }
            RefreshHint();
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = CodeBlockView.Dragged;
            if (!source || !Panel) return;
            // A nested argument zone must never accept its own ancestor.
            if (transform.IsChildOf(source.transform)) return;
            if (argumentSlot && source.Owner == this) return;
            if (argumentSlot && (!PythonBlockCompiler.IsExpression(source.Kind) || !Accepts(source.Kind))) return;
            if (source.Owner != null && source.Owner.Panel != Panel) return;
            var block = source.IsPalette ? source.CopyTo(content) : source;
            if (!source.IsPalette) source.Owner?.Remove(source, false);
            if (argumentSlot && blocks.Count > 0)
            {
                var previous = blocks[0];
                blocks.Clear();
                previous.gameObject.SetActive(false);
                Destroy(previous.gameObject);
            }
            block.transform.SetParent(content, false);
            int insertion = blocks.Count;
            if (!argumentSlot)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    var rect = (RectTransform)blocks[i].transform;
                    var center = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, rect.TransformPoint(rect.rect.center));
                    if (eventData.position.x < center.x) { insertion = i; break; }
                }
            }
            blocks.Insert(insertion, block);
            block.transform.SetSiblingIndex(insertion);
            block.Bind(this);
            source.AcceptDrop();
            RefreshHint();
            Panel.MarkDirty();
            eventData.Use();
        }

        bool Accepts(BlockKind kind)
        {
            if (kind == BlockKind.Variable) return true;
            if (acceptedValue == SlotValue.Enemy) return kind == BlockKind.NearestEnemy;
            if (acceptedValue == SlotValue.Number)
                return kind == BlockKind.Number || kind == BlockKind.Distance ||
                       kind == BlockKind.PositionX || kind == BlockKind.PositionY;
            return true;
        }

        public void Remove(CodeBlockView block, bool notify = true)
        {
            blocks.Remove(block);
            RefreshHint();
            if (notify) Panel.MarkDirty();
        }

        void RefreshHint()
        {
            if (emptyHint) emptyHint.SetActive(blocks.Count == 0);
        }

        public IEnumerable<CodeBlockView> EnumerateBlocks()
        {
            foreach (var block in blocks)
                foreach (var nested in block.EnumerateBlocks()) yield return nested;
        }

        public CodeBlock ReadArgument(bool allowIncomplete = false)
        {
            if (allowIncomplete && blocks.Count == 0) return null;
            if (blocks.Count != 1) throw new FormatException($"{name}: 인자 블록 하나를 넣어 주세요.");
            return blocks[0].Read(allowIncomplete);
        }
    }
}
