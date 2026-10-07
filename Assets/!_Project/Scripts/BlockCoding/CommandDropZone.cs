using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    public sealed class CommandDropZone : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerDownHandler,
        IPointerEnterHandler, IPointerExitHandler, ILayoutElement, ILayoutGroup
    {
        [SerializeField] RectTransform rect;
        [SerializeField] BlockSlotKind slot;
        [SerializeField] TMP_Text hint;
        [SerializeField] Graphic background;
        [SerializeField] float minimumWidth = 64;
        [SerializeField] float minimumHeight = 32;
        [SerializeField] bool freePlacement;
        [SerializeField] List<CommandBlockView> blocks = new List<CommandBlockView>();
        // Only the workspace has free roots; nested bodies and argument slots keep their layout.
        readonly Dictionary<CommandBlockView, CommandBlockView> next = new Dictionary<CommandBlockView, CommandBlockView>();
        static readonly Vector2 FirstPosition = new Vector2(24, -24);
        const float SnapDistance = 30;
        public bool FreePlacement => freePlacement;
        public RectTransform Rect => rect;
        public BlockSlotKind Slot => slot;
        public CommandCodingPanel Panel { get; private set; }
        public CommandBlockView ParentBlock { get; private set; }
        public IReadOnlyList<CommandBlockView> Blocks => blocks;
        Color normalColor;
        public float minWidth => preferredWidth;
        public float preferredWidth => Mathf.Max(minimumWidth, blocks.Count == 0 ? 0 : blocks.Max(b => b.preferredWidth + (freePlacement ? b.Rect.anchoredPosition.x : 0)) + 8);
        public float flexibleWidth => 0;
        public float minHeight => preferredHeight;
        public float preferredHeight
        {
            get
            {
                if (slot != BlockSlotKind.Statement) return minimumHeight;
                if (freePlacement) return Mathf.Max(minimumHeight, blocks.Count == 0 ? 0 : blocks.Max(b => -b.Rect.anchoredPosition.y + b.preferredHeight) + 32);
                float height = 8;
                for (int i = 0; i < blocks.Count; i++) height += blocks[i].preferredHeight - (i > 0 ? Overlap(i) : 0);
                return Mathf.Max(minimumHeight, height);
            }
        }
        public float flexibleHeight => 0;
        public int layoutPriority => 1;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
        bool IsBranch(CommandBlockView block) => block.Kind == BlockKind.Elif || block.Kind == BlockKind.Else;
        float Overlap(int index) => slot == BlockSlotKind.Statement && IsBranch(blocks[index]) ? 18 : 0;

        public void Bind(CommandCodingPanel panel, CommandBlockView parent)
        {
            if (!rect || !hint || !background) throw new InvalidOperationException(name + ": DropZone 참조를 연결해 주세요.");
            Panel = panel;
            panel.RegisterZone(this);
            ParentBlock = parent;
            normalColor = background.color;
            foreach (var block in blocks) block.Bind(panel, this);
            Refresh();
        }
        public List<CodeBlock> Read()
        {
            if (!freePlacement) return blocks.Select(b => b.Read()).ToList();
            var roots = Roots().ToList();
            if (roots.Count == 0) return new List<CodeBlock>();
            if (roots.Count != 1) throw new FormatException("미완성 · 모든 명령 블록을 하나의 흐름으로 연결해 주세요.");
            return Group(roots[0]).Select(b => b.Read()).ToList();
        }
        internal void Load(IReadOnlyList<CodeBlock> source)
        {
            foreach (var old in blocks) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            blocks.Clear(); next.Clear();
            foreach (var data in source)
            {
                var view = Panel.CreateBlock(data, rect); view.Bind(Panel, this);
                if (freePlacement && blocks.Count > 0) next[blocks[blocks.Count - 1]] = view;
                blocks.Add(view);
            }
            if (freePlacement && blocks.Count > 0) blocks[0].Rect.anchoredPosition = new Vector2(24, -24);
            Refresh(); SetLayoutHorizontal();
        }
        IEnumerable<CommandBlockView> Roots() => blocks.Where(b => !next.ContainsValue(b));
        internal void BringToFront(CommandBlockView block)
        {
            if (!freePlacement) return;
            var root = Roots().FirstOrDefault(candidate => Group(candidate).Contains(block));
            if (!root) return;
            // Sibling order controls drawing only. Keep the execution list and links intact.
            foreach (var member in Group(root)) member.transform.SetAsLastSibling();
        }
        [Serializable]
        public sealed class DraftGroup
        {
            public Vector2 position;
            public List<CodeBlock> blocks = new List<CodeBlock>();
        }
        // Read() deliberately rejects disconnected roots. Drafts must retain them.
        public List<DraftGroup> CaptureDraft() => Roots().Select(root => new DraftGroup {
            position = root.Rect.anchoredPosition,
            blocks = Group(root).Select(block => block.Read()).ToList()
        }).ToList();
        public void RestoreDraft(IReadOnlyList<DraftGroup> groups)
        {
            Load(Array.Empty<CodeBlock>());
            foreach (var group in groups)
            {
                CommandBlockView previous = null;
                foreach (var data in group.blocks)
                {
                    var view = Panel.CreateBlock(data, rect);
                    view.Bind(Panel, this);
                    blocks.Add(view);
                    if (previous) next[previous] = view;
                    else view.Rect.anchoredPosition = group.position;
                    previous = view;
                }
            }
            Refresh();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
        public CodeBlock ReadArgument() => blocks.Count == 1 ? blocks[0].Read() : null;
        public IEnumerable<CommandBlockView> AllBlocks() => (freePlacement ? Roots().SelectMany(Group) : blocks).SelectMany(b => b.Descendants());
        public List<CommandBlockView> Group(CommandBlockView first)
        {
            int index = blocks.IndexOf(first);
            var group = new List<CommandBlockView> { first };
            if (freePlacement)
            {
                while (next.TryGetValue(group[group.Count - 1], out var following)) group.Add(following);
                return group;
            }
            if (slot == BlockSlotKind.Statement && (first.Kind == BlockKind.If || first.Kind == BlockKind.Elif))
                for (int i = index + 1; i < blocks.Count && IsBranch(blocks[i]); i++) group.Add(blocks[i]);
            return group;
        }
        bool InLoop()
        {
            for (var ancestor = ParentBlock; ancestor; ancestor = ancestor.Owner?.ParentBlock)
                if (ancestor.Kind == BlockKind.For || ancestor.Kind == BlockKind.While) return true;
            return false;
        }
        public bool Insert(CommandBlockView source, int index)
        {
            if (!source || source.Panel != Panel || index < 0 || index > blocks.Count) return false;
            if (!Panel.CanTake(source)) return false;
            if (freePlacement)
            {
                var last = blocks.LastOrDefault();
                return PlaceFree(source, last ? last.Rect.anchoredPosition + new Vector2(0, -last.preferredHeight) : FirstPosition);
            }
            if (ParentBlock && ParentBlock.IsPalette) return false;
            if (!PythonTreeCompiler.Accepts(slot, source.Kind)) return false;
            if (!source.IsPalette && (transform.IsChildOf(source.transform) || source.Owner == this && slot != BlockSlotKind.Statement)) return false;
            var moved = source.IsPalette ? new List<CommandBlockView> { source } : source.Owner.Group(source);
            if (moved.Any(b => transform.IsChildOf(b.transform))) return false;
            if (slot == BlockSlotKind.Statement)
            {
                if ((source.Kind == BlockKind.Break || source.Kind == BlockKind.Continue) && !InLoop()) return false;
                var siblings = blocks.Where(b => !moved.Contains(b)).Select(b => new CodeBlock(b.Kind)).ToList();
                int adjusted = index - blocks.Take(index).Count(moved.Contains);
                for (int i = 0; i < moved.Count; i++)
                {
                    var candidate = new CodeBlock(moved[i].Kind);
                    if (!PythonTreeCompiler.CanInsert(siblings, adjusted + i, candidate)) return false;
                    siblings.Insert(adjusted + i, candidate);
                }
                index = adjusted;
            }
            else if (blocks.Count != 0) return false; // Never destroy a filled input by dropping over it.
            foreach (var item in moved)
            {
                var placed = source.IsPalette ? item.Copy(rect) : item;
                if (!source.IsPalette) item.Owner.Detach(item);
                placed.transform.SetParent(rect, false);
                blocks.Insert(index++, placed);
                placed.Bind(Panel, this);
                if (source.IsPalette) Panel.FillNewLoop(placed);
            }
            Refresh();
            blocks[index - 1].BringToFront();
            Panel.Changed();
            return true;
        }
        public void Detach(CommandBlockView block, bool reconnect = false)
        {
            next.TryGetValue(block, out var following);
            foreach (var previous in next.Where(pair => pair.Value == block).Select(pair => pair.Key).ToArray())
                if (reconnect && following) next[previous] = following; else next.Remove(previous);
            next.Remove(block);
            blocks.Remove(block);
            Refresh();
        }
        public bool PlaceFree(CommandBlockView source, Vector2 position)
        {
            if (!freePlacement || !source || source.Panel != Panel) return false;
            if (!Panel.CanTake(source)) return false;
            var moved = source.IsPalette ? new List<CommandBlockView> { source } : source.Owner.Group(source);
            var placed = new List<CommandBlockView>();
            foreach (var item in moved)
            {
                var block = source.IsPalette ? item.Copy(rect) : item;
                if (!source.IsPalette) item.Owner.Detach(item);
                block.transform.SetParent(rect, false);
                block.Bind(Panel, this);
                blocks.Add(block);
                placed.Add(block);
                if (source.IsPalette) Panel.FillNewLoop(block);
            }
            for (int i = 1; i < placed.Count; i++) next[placed[i - 1]] = placed[i];
            var first = placed[0];
            var last = placed[placed.Count - 1];
            position = new Vector2(Mathf.Max(4, position.x), Mathf.Min(-4, position.y));
            if (SnapsToFirstPosition(placed, position)) position = FirstPosition;
            CommandBlockView.Place(first.Rect, position.x, -position.y, first.preferredWidth, first.preferredHeight);

            FindSnap(placed, position, out var attachAfter, out var attachBefore);
            if (attachAfter) next[attachAfter] = first;
            else if (attachBefore)
            {
                // Insert at the visible first row and move its chain down instead of clipping above the workspace.
                first.Rect.anchoredPosition = attachBefore.Rect.anchoredPosition;
                next[last] = attachBefore;
            }
            Refresh();
            SetLayoutHorizontal();
            first.BringToFront();
            Panel.Changed();
            return true;
        }
        bool SnapsToFirstPosition(List<CommandBlockView> moved, Vector2 position) =>
            moved.All(block => PythonTreeCompiler.OutputSlot(block.Kind) == BlockSlotKind.Statement) &&
            !blocks.Any(block => !moved.Contains(block)) && Vector2.Distance(position, FirstPosition) < SnapDistance;

        void FindSnap(List<CommandBlockView> placed, Vector2 position, out CommandBlockView attachAfter, out CommandBlockView attachBefore)
        {
            var first = placed[0];
            float nearest = SnapDistance;
            attachAfter = null; attachBefore = null;
            if (placed.All(b => PythonTreeCompiler.OutputSlot(b.Kind) == BlockSlotKind.Statement))
                foreach (var target in blocks.Where(b => !placed.Contains(b) && PythonTreeCompiler.OutputSlot(b.Kind) == BlockSlotKind.Statement))
                {
                    if (!next.TryGetValue(target, out var following) || placed.Contains(following))
                    {
                        var root = Roots().First(r => Group(r).Contains(target));
                        var chain = Group(root).Where(b => !placed.Contains(b)).Concat(placed).ToList();
                        var edge = target.Rect.anchoredPosition + new Vector2(0, -target.preferredHeight + (IsBranch(first) ? 18 : 0));
                        float distance = Vector2.Distance(position, edge);
                        if (distance < nearest && ValidChain(chain)) { nearest = distance; attachAfter = target; attachBefore = null; }
                    }
                    if (!next.Any(pair => pair.Value == target && !placed.Contains(pair.Key)))
                    {
                        float height = placed.Sum(b => b.preferredHeight) - placed.Skip(1).Count(IsBranch) * 18;
                        var edge = position + new Vector2(0, -height + (IsBranch(target) ? 18 : 0));
                        float distance = Mathf.Min(Vector2.Distance(edge, target.Rect.anchoredPosition), Vector2.Distance(position, target.Rect.anchoredPosition));
                        if (distance < nearest && ValidChain(placed.Concat(Group(target)).ToList())) { nearest = distance; attachBefore = target; attachAfter = null; }
                    }
                }
        }
        public bool Preview(CommandBlockView source, PointerEventData e, out Rect preview)
        {
            preview = default;
            if (!source || !Panel.CanTake(source) || !PythonTreeCompiler.Accepts(slot, source.Kind)) return false;
            var moved = source.IsPalette ? new List<CommandBlockView> { source } : source.Owner.Group(source);
            if (moved.Any(b => transform.IsChildOf(b.transform))) return false;
            if (freePlacement)
            {
                var position = Panel.DropPosition(e);
                position = new Vector2(Mathf.Max(4, position.x), Mathf.Min(-4, position.y));
                // Share the drop rule: the first-line anchor is a snap target only nearby.
                if (SnapsToFirstPosition(moved, position))
                {
                    var first = FirstPosition;
                    preview = new Rect(Mathf.Max(4, first.x), Mathf.Min(-4, first.y) + 3, source.preferredWidth, 6);
                    return true;
                }
                FindSnap(moved, position, out var after, out var before);
                var target = after ? after : before;
                if (!target) return false;
                var edge = target.Rect.anchoredPosition + (after ? new Vector2(0, -target.preferredHeight + (IsBranch(source) ? 18 : 0)) : Vector2.zero);
                preview = new Rect(edge.x, edge.y + 3, Mathf.Max(target.preferredWidth, source.preferredWidth), 6);
                return true;
            }
            if (slot != BlockSlotKind.Statement)
            {
                if (blocks.Count != 0) return false;
                preview = new Rect(0, 0, rect.rect.width, rect.rect.height); return true;
            }
            if ((source.Kind == BlockKind.Break || source.Kind == BlockKind.Continue) && !InLoop()) return false;
            int index = PointerIndex(e);
            var siblings = blocks.Where(b => !moved.Contains(b)).Select(b => new CodeBlock(b.Kind)).ToList();
            int adjusted = index - blocks.Take(index).Count(moved.Contains);
            for (int i = 0; i < moved.Count; i++)
            {
                var candidate = new CodeBlock(moved[i].Kind);
                if (!PythonTreeCompiler.CanInsert(siblings, adjusted + i, candidate)) return false;
                siblings.Insert(adjusted + i, candidate);
            }
            float y = index < blocks.Count ? blocks[index].Rect.anchoredPosition.y : blocks.Count == 0 ? -4 : blocks.Last().Rect.anchoredPosition.y - blocks.Last().preferredHeight;
            preview = new Rect(4, y + 3, Mathf.Max(source.preferredWidth, rect.rect.width - 8), 6); return true;
        }
        int PointerIndex(PointerEventData e)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                var center = RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, blocks[i].Rect.TransformPoint(blocks[i].Rect.rect.center));
                if (e.position.y > center.y) return i;
            }
            return blocks.Count;
        }
        static bool ValidChain(List<CommandBlockView> chain)
        {
            var siblings = new List<CodeBlock>();
            foreach (var view in chain)
            {
                var block = new CodeBlock(view.Kind);
                if (!PythonTreeCompiler.CanInsert(siblings, siblings.Count, block)) return false;
                siblings.Add(block);
            }
            return true;
        }
        public void Refresh()
        {
            if (hint) hint.gameObject.SetActive(blocks.Count == 0);
            for (int i = 0; i < blocks.Count; i++) blocks[i].JoinNext(freePlacement ?
                next.TryGetValue(blocks[i], out var following) && IsBranch(following) : i + 1 < blocks.Count && IsBranch(blocks[i + 1]));
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        public void SetLayoutHorizontal()
        {
            if (freePlacement)
            {
                foreach (var root in Roots())
                {
                    var position = root.Rect.anchoredPosition;
                    var chain = Group(root);
                    for (int i = 0; i < chain.Count; i++)
                    {
                        var block = chain[i];
                        if (i > 0 && IsBranch(block)) position.y += 18;
                        float width = block.preferredWidth;
                        if (block.Kind == BlockKind.If || IsBranch(block))
                        {
                            int start = i;
                            while (start > 0 && IsBranch(chain[start])) start--;
                            for (int j = start; j < chain.Count && (j == start || IsBranch(chain[j])); j++) width = Mathf.Max(width, chain[j].preferredWidth);
                        }
                        CommandBlockView.Place(block.Rect, position.x, -position.y, width, block.preferredHeight);
                        position.y -= block.preferredHeight;
                    }
                }
                return;
            }
            float y = slot == BlockSlotKind.Statement ? 4 : 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0) y -= Overlap(i);
                var block = blocks[i];
                float width = block.preferredWidth;
                if (block.Kind == BlockKind.If || IsBranch(block))
                {
                    int start = i;
                    while (start > 0 && IsBranch(blocks[start])) start--;
                    for (int j = start; j < blocks.Count && (j == start || IsBranch(blocks[j])); j++)
                        width = Mathf.Max(width, blocks[j].preferredWidth);
                }
                CommandBlockView.Place(block.Rect, 4, y, width, slot == BlockSlotKind.Statement ? block.preferredHeight : minimumHeight);
                y += block.preferredHeight;
            }
        }
        public void SetLayoutVertical() => SetLayoutHorizontal();
        public void OnDrop(PointerEventData e)
        {
            if (!Panel || !Panel.Dragged) return;
            var dragged = Panel.Dragged;
            var source = dragged.Owner ? dragged.Owner.Rect : dragged.Rect;
            if (freePlacement)
            {
                if (PlaceFree(dragged, Panel.DropPosition(e))) { Panel.ReportSuccessfulDrop(dragged, source, rect); Panel.AcceptDrop(); }
                e.Use();
                return;
            }
            int index = PointerIndex(e);
            if (Insert(dragged, index)) { Panel.ReportSuccessfulDrop(dragged, source, rect); Panel.AcceptDrop(); }
            else Panel.ShowMessage("이 위치에는 연결할 수 없습니다. 슬롯 모양과 분기 순서를 확인하세요.");
            e.Use();
        }
        public void OnPointerDown(PointerEventData e) { if (ParentBlock) ParentBlock.BringToFront(); }
        public void OnPointerClick(PointerEventData e)
        {
            if (Panel && e.button == PointerEventData.InputButton.Right && ParentBlock && !ParentBlock.IsPalette) Panel.DeleteOne(ParentBlock);
            else if (Panel && !(ParentBlock && ParentBlock.IsPalette)) Panel.Select(this);
            e.Use();
        }
        public void OnPointerEnter(PointerEventData e)
        {
            // The panel draws one marker at the valid snap edge or input slot.
        }
        public void OnPointerExit(PointerEventData e) { if (background) background.color = normalColor; }
    }
}
