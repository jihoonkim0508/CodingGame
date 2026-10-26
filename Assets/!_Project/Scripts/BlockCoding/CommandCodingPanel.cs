using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    public sealed class CommandCodingPanel : MonoBehaviour
    {
        [Serializable]
        public struct PaletteItem { public CommandBlockView view; public int category; }
        [SerializeField] TMP_InputField functionName;
        [SerializeField] TMP_Text pythonPreview;
        [SerializeField] TMP_Text status;
        [SerializeField] CommandDropZone program;
        [SerializeField] RectTransform definitionPanel;
        [SerializeField] RectTransform dragLayer;
        [SerializeField] RectTransform snapMarker;
        [SerializeField] PaletteItem[] palette = Array.Empty<PaletteItem>();
        [SerializeField] Button[] categoryButtons = Array.Empty<Button>();
        [SerializeField] RectTransform variablePalette;
        [SerializeField] CommandBlockView variablePrefab;
        [SerializeField] GameObject variableEmptyHint;
        [SerializeField, Tooltip("Optional hint shown while the program has no blocks.")] GameObject programEmptyHint;
        readonly HashSet<string> enemyVariables = new HashSet<string>();
        List<CommandBlockView> dragging = new List<CommandBlockView>();
        readonly List<CommandBlockView> ghosts = new List<CommandBlockView>();
        readonly List<Vector2> ghostOffsets = new List<Vector2>();
        readonly Vector3[] dragCorners = new Vector3[4];
        CommandDropZone selected;
        CommandDropZone snapTarget;
        Vector2 programGrabOffset, dragGrabOffset;
        bool updating;
        bool initialized;
        bool loading;
        bool restoringHistory;
        internal sealed class Draft
        {
            public string name;
            public List<CommandDropZone.DraftGroup> groups;
        }
        public sealed class EditHistory
        {
            internal readonly List<Draft> states = new List<Draft>();
            internal int index = -1;
        }
        public EditHistory History { get; private set; } = new EditHistory();
        public bool CanUndo => History.index > 0;
        public bool CanRedo => History.index + 1 < History.states.Count;
        public bool TextInputFocused => functionName.isFocused || program.AllBlocks().Any(b => b.TextInputFocused);
        public Func<string, IReadOnlyList<CodeBlock>, string> ApplyToTarget { get; set; }
        public Func<BlockKind, bool> IsAvailable { get; set; }
        public Func<BlockKind, int> Remaining { get; set; }
        public event Action<BlockKind, RectTransform, RectTransform> BlockDropped;
        readonly HashSet<CommandDropZone> zones = new HashSet<CommandDropZone>();
        internal void RegisterZone(CommandDropZone zone) => zones.Add(zone);
        internal bool CanTake(CommandBlockView source) => !source.IsPalette || (Remaining?.Invoke(source.Kind) ?? int.MaxValue) > 0;
        int currentCategory;
        public void RefreshPalette() => ShowCategory(currentCategory);
        bool dropAccepted;
        static int nextName;
        public CommandDropZone Program => program;
#if UNITY_EDITOR
        public CommandDropZone EditorSnapTarget => snapTarget;
#endif
        public IReadOnlyList<PaletteItem> Palette => palette;
        public CommandBlockView VariableTemplate => variablePrefab;
        public CommandBlockView Dragged { get; private set; }
        public bool IsApplied { get; private set; }
        public string AppliedSource { get; private set; }
        public string FunctionName { get => functionName.text; set => functionName.text = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetNames() => nextName = 0;
        void Start() => Initialize();
        void Initialize()
        {
            if (initialized) return;
            if (!functionName || !pythonPreview || !status || !program || !definitionPanel ||
                !dragLayer || !variablePalette || !variablePrefab || !variableEmptyHint)
                throw new InvalidOperationException(name + ": CommandCodingPanel의 Inspector 참조가 누락되었습니다.");
            initialized = true;
            if (string.IsNullOrWhiteSpace(functionName.text)) functionName.SetTextWithoutNotify("f" + nextName++);
            pythonPreview.richText = false;
            functionName.onValueChanged.AddListener(_ => Changed());
            program.Bind(this, null);
            foreach (var item in palette) item.view.Bind(this, null, true);
            variablePrefab.Bind(this, null, true);
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int category = i;
                categoryButtons[i].onClick.AddListener(() => ShowCategory(category));
            }
            selected = program;
            ShowCategory(0);
            Changed();
        }
        public void ShowCategory(int category)
        {
            currentCategory = category;
            foreach (var item in palette)
            {
                item.view.gameObject.SetActive(item.category == category && (IsAvailable?.Invoke(item.view.Kind) ?? true));
                item.view.SetStock(Remaining?.Invoke(item.view.Kind) ?? 0);
            }
            variablePrefab.SetStock(Remaining?.Invoke(BlockKind.Variable) ?? 0);
            variablePrefab.gameObject.SetActive(IsAvailable?.Invoke(BlockKind.Variable) ?? true);
            variablePalette.gameObject.SetActive(category == 2);
            for (int i = 0; i < categoryButtons.Length; i++)
                categoryButtons[i].targetGraphic.color = i == category ? new Color(0.15f, 0.45f, 0.65f) : new Color(0.13f, 0.17f, 0.24f);
        }
        public CommandDropZone SelectedZone => selected;
        internal void Select(CommandDropZone zone)
        {
            selected = zone;
            // Selecting the command area says nothing: the message line keeps showing the last syntax error.
            if (zone.Slot != BlockSlotKind.Statement)
                ShowMessage(zone.Slot == BlockSlotKind.Condition ? "조건 슬롯 선택됨 · 논리 블록을 선택하세요." : "값 슬롯 선택됨 · 값 블록을 선택하세요.");
        }
        public void AddFromPalette(CommandBlockView source)
        {
            if (!CanTake(source)) { ShowMessage("보유 수량을 모두 사용했습니다."); return; }
            var target = selected ? selected : program;
            if (PythonTreeCompiler.OutputSlot(source.Kind) == BlockSlotKind.Statement && target.Slot != BlockSlotKind.Statement) target = program;
            if (target == program && program.FreePlacement)
            {
                if (PythonTreeCompiler.OutputSlot(source.Kind) != BlockSlotKind.Statement && source.BaseKind != BlockKind.Variable)
                { ShowMessage("값을 넣을 빈 슬롯을 선택하세요."); return; }
                program.Insert(source, program.Blocks.Count);
                return;
            }
            if (!target.Insert(source, target.Blocks.Count)) ShowMessage("먼저 맞는 모양의 빈 슬롯을 선택하거나, 블록을 원하는 위치로 드래그하세요.");
        }
        public void ShowMessage(string text) => status.text = text;
        internal void FillNewLoop(CommandBlockView block)
        {
            if (block.Kind != BlockKind.For || block.Arguments[0].Blocks.Count != 0) return;
            var number = palette.FirstOrDefault(item => item.view.BaseKind == BlockKind.Number).view;
            if (number && (IsAvailable?.Invoke(BlockKind.Number) ?? true) && CanTake(number))
                block.Arguments[0].Load(new[] { new CodeBlock(BlockKind.Number, "10") });
        }
        internal bool IsEnemyVariable(string name) => enemyVariables.Contains(name);
        internal void RenameVariable(string previous, string current)
        {
            if (string.IsNullOrEmpty(previous)) return;
            if (program.AllBlocks().Any(b => b.BaseKind == BlockKind.DeclareVariable && b.VariableName == previous)) return;
            foreach (var block in program.AllBlocks())
                if (block.BaseKind == BlockKind.Variable && block.VariableName == previous) block.SetVariable(current);
        }
        void SyncVariables()
        {
            enemyVariables.Clear();
            foreach (var block in program.AllBlocks())
            {
                if (block.BaseKind != BlockKind.DeclareVariable) continue;
                string name;
                try { name = PythonTreeCompiler.NormalizeIdentifier(block.VariableName); }
                catch (FormatException) { name = block.RetainedName; }
                if (string.IsNullOrEmpty(name)) continue;
                var value = block.Arguments[0].ReadArgument();
                if (value != null && (value.Kind == BlockKind.NearestEnemy || value.Kind == BlockKind.Variable && enemyVariables.Contains(value.Value))) enemyVariables.Add(name);
                else enemyVariables.Remove(name);
            }
            foreach (var block in program.AllBlocks()) if (block.BaseKind == BlockKind.Variable) block.RefreshVariableOptions();
            variableEmptyHint.SetActive(false);
        }
        internal void Changed()
        {
            if (updating) return;
            updating = true;
            IsApplied = false;
            try
            {
                SyncVariables();
                var blocks = CompilePreview(out string source);
                if (!loading) Publish(blocks, source);
            }
            catch (FormatException error)
            {
                pythonPreview.text = "# 미완성: 블록 연결과 입력을 완성해 주세요.";
                if (!loading) ClearTarget();
                ShowMessage("미완성 · 로봇이 행동하지 않음 · " + error.Message);
            }
            finally
            {
                updating = false;
                if (programEmptyHint) programEmptyHint.SetActive(program.Blocks.Count == 0);
                RefreshPalette();
                LayoutRebuilder.MarkLayoutForRebuild(program.Rect);
                if (!loading && !restoringHistory) RecordHistory();
            }
        }
        void RecordHistory()
        {
            var state = new Draft { name = functionName.text, groups = program.CaptureDraft() };
            if (History.index >= 0 && SameDraft(History.states[History.index], state)) return;
            History.states.RemoveRange(History.index + 1, History.states.Count - History.index - 1);
            History.states.Add(state);
            if (History.states.Count > 100) History.states.RemoveAt(0);
            History.index = History.states.Count - 1;
        }
        static bool SameDraft(Draft left, Draft right) => left.name == right.name && left.groups.Count == right.groups.Count &&
            left.groups.Zip(right.groups, (a, b) => a.position == b.position && SameBlocks(a.blocks, b.blocks)).All(equal => equal);
        static bool SameBlocks(IReadOnlyList<CodeBlock> left, IReadOnlyList<CodeBlock> right) =>
            left.Count == right.Count && left.Zip(right, SameBlock).All(equal => equal);
        static bool SameBlock(CodeBlock left, CodeBlock right) => left == null || right == null ? left == right :
            left.Kind == right.Kind && left.Value == right.Value && SameBlocks(left.Arguments, right.Arguments) && SameBlocks(left.Body, right.Body);
        public void Undo() { if (CanUndo && !Dragged) { History.index--; RestoreHistory(); } }
        public void Redo() { if (CanRedo && !Dragged) { History.index++; RestoreHistory(); } }
        public void LoadHistory(EditHistory history)
        {
            if (history == null || history.index < 0) throw new ArgumentException("편집 기록이 비어 있습니다.");
            Initialize(); History = history; RestoreHistory();
        }
        void RestoreHistory()
        {
            EndDrag(); restoringHistory = true; updating = true;
            try
            {
                var draft = History.states[History.index];
                functionName.SetTextWithoutNotify(draft.name);
                selected = program;
                program.RestoreDraft(draft.groups);
                updating = false;
                Changed(); // Revalidate syntax and current inventory before applying.
            }
            finally { updating = false; restoringHistory = false; }
        }
        public void ClearProgram()
        {
            EndDrag(); updating = true;
            try { selected = program; program.Load(Array.Empty<CodeBlock>()); }
            finally { updating = false; }
            Changed();
        }
        public void Apply()
        {
            try
            {
                var blocks = CompilePreview(out string source);
                Publish(blocks, source);
            }
            catch (FormatException error) { IsApplied = false; AppliedSource = null; ClearTarget(); ShowMessage("미완성 · 로봇이 행동하지 않음 · " + error.Message); }
        }
        // A draft with a syntax error is still saved: the robot gets no code (it stops) instead of keeping the last working one.
        void ClearTarget()
        {
            try { ApplyToTarget?.Invoke(functionName.text, Array.Empty<CodeBlock>()); }
            catch (FormatException) { } // no robot to apply to (editor closing); the draft stays in the editor
            AppliedSource = null;
        }
        List<CodeBlock> CompilePreview(out string source)
        {
            var blocks = program.Read();
            source = PythonTreeCompiler.Compile(functionName.text, blocks);
            pythonPreview.text = source;
            return blocks;
        }
        void Publish(IReadOnlyList<CodeBlock> blocks, string source)
        {
            string appliedMessage = ApplyToTarget?.Invoke(functionName.text, blocks);
            AppliedSource = source; IsApplied = true;
            ShowMessage((appliedMessage ?? "자동 적용됨") + (PythonTreeCompiler.HasStaleEnemyLoop(blocks)
                ? " · 주의: 반복문 안에서 enemy를 다시 정하지 않으면 첫 적이 사라진 뒤 멈춰요. 반복문 안에 enemy = get_nearest_enemy()를 넣어보세요." : ""));
        }
        public void LoadProgram(string name, IReadOnlyList<CodeBlock> blocks)
        {
            loading = true;
            History = new EditHistory();
            try
            {
                Initialize(); EndDrag(); updating = true;
                functionName.SetTextWithoutNotify(name); selected = program; program.Load(blocks);
            }
            finally { updating = false; loading = false; }
            Changed();
        }
        internal CommandBlockView CreateBlock(CodeBlock data, Transform parent)
        {
            if (data.Kind == BlockKind.Attack) data = new CodeBlock(BlockKind.Shot, data.Value, data.Arguments.ToArray());
            bool variable = data.Kind == BlockKind.Variable || data.Kind == BlockKind.Distance || data.Kind == BlockKind.Hp;
            var template = variable ? variablePrefab : palette.FirstOrDefault(item => item.view.BaseKind == data.Kind).view;
            if (!template) throw new FormatException("블록 프리팹이 연결되지 않았습니다: " + data.Kind);
            var view = Instantiate(template, parent);
            view.Bind(this, null); view.gameObject.SetActive(true); view.LoadData(data);
            return view;
        }
        public void BeginDrag(CommandBlockView source, PointerEventData e)
        {
            if (Dragged || !CanTake(source)) return;
            Dragged = source;
            dropAccepted = false;
            dragging = CommandDropZone.Moving(source);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(program.Rect, e.pressPosition, e.pressEventCamera, out var programPress);
            programGrabOffset = programPress - TopLeft(program.Rect, source.Rect);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.pressPosition, e.pressEventCamera, out var dragPress);
            var sourceDragTopLeft = TopLeft(dragLayer, source.Rect);
            dragGrabOffset = dragPress - sourceDragTopLeft;
            ghosts.Clear(); ghostOffsets.Clear();
            foreach (var block in dragging)
            {
                ghostOffsets.Add(TopLeft(dragLayer, block.Rect) - sourceDragTopLeft);
                var copy = block.Copy(dragLayer);
                copy.SetDragAppearance(true);
                copy.Rect.anchorMin = copy.Rect.anchorMax = copy.Rect.pivot = new Vector2(0, 1);
                copy.Rect.sizeDelta = new Vector2(copy.preferredWidth, copy.preferredHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(copy.Rect);
                ghosts.Add(copy);
            }
            foreach (var block in dragging) block.SetDragAppearance(true);
            MoveDrag(e);
        }
        // Top-left corner of block, in space's local coordinates.
        static Vector2 TopLeft(RectTransform space, RectTransform block) =>
            space.InverseTransformPoint(block.TransformPoint(new Vector3(block.rect.xMin, block.rect.yMax, 0)));
        public void MoveDrag(PointerEventData e)
        {
            if (ghosts.Count == 0) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, e.pressEventCamera, out var point))
                for (int i = 0; i < ghosts.Count; i++) ghosts[i].Rect.localPosition = point - dragGrabOffset + ghostOffsets[i];
            UpdateSnapPreview(e);
        }
        void UpdateSnapPreview(PointerEventData e)
        {
            if (!snapMarker) return;
            snapMarker.gameObject.SetActive(false);
            snapTarget = null;
            zones.RemoveWhere(zone => !zone);
            CommandDropZone target = null;
            Rect targetPreview = default;
            bool targetIsSocket = false;
            float targetOverlap = -1;
            float targetDistance = float.MaxValue;
            foreach (var zone in zones)
            {
                if (!zone.gameObject.activeInHierarchy || ghosts.Any(ghost => zone.Rect.IsChildOf(ghost.transform)) ||
                    (zone.ParentBlock && zone.ParentBlock.IsPalette) || dragging.Any(b => zone.Rect.IsChildOf(b.transform))) continue;
                bool socket = !zone.FreePlacement && zone.Slot != BlockSlotKind.Statement;
                var bounds = DraggedBounds(zone.Rect);
                var area = zone.Rect.rect;
                bool pointerOver = RectTransformUtility.RectangleContainsScreenPoint(zone.Rect, e.position, e.pressEventCamera);
                // Keep the original pointer hit area and also accept overlap anywhere on the block.
                if ((!pointerOver && !bounds.Overlaps(area)) || !zone.Preview(Dragged, e, out var preview)) continue;
                float overlap = Mathf.Max(0, Mathf.Min(bounds.xMax, area.xMax) - Mathf.Max(bounds.xMin, area.xMin)) *
                    Mathf.Max(0, Mathf.Min(bounds.yMax, area.yMax) - Mathf.Max(bounds.yMin, area.yMin));
                overlap /= Mathf.Max(1, Mathf.Min(bounds.width * bounds.height, area.width * area.height));
                float distance = (bounds.center - area.center).sqrMagnitude;
                if (!target || socket && !targetIsSocket || socket == targetIsSocket &&
                    (socket ? overlap > targetOverlap || Mathf.Approximately(overlap, targetOverlap) && distance < targetDistance :
                        zone.Rect.IsChildOf(target.Rect)))
                { target = zone; targetPreview = preview; targetIsSocket = socket; targetOverlap = overlap; targetDistance = distance; }
            }
            if (!target) return;
            snapTarget = target;
            // As in e085adf, only the marker snaps; the dragged blocks remain under the hand.
            snapMarker.position = target.Rect.TransformPoint(new Vector3(target.Rect.rect.xMin + targetPreview.x,
                target.Rect.rect.yMax + targetPreview.y, 0));
            var scale = target.Rect.lossyScale.x / snapMarker.parent.lossyScale.x;
            snapMarker.sizeDelta = new Vector2(targetPreview.width, targetPreview.height) * scale;
            snapMarker.gameObject.SetActive(true); snapMarker.SetAsLastSibling();
        }
        Rect DraggedBounds(RectTransform target)
        {
            ghosts[0].Rect.GetWorldCorners(dragCorners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in dragCorners)
            {
                Vector2 point = target.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        internal Vector2 DropPosition(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(program.Rect, e.position, e.pressEventCamera, out var point);
            return point - new Vector2(program.Rect.rect.xMin, program.Rect.rect.yMax) - programGrabOffset;
        }
        internal bool DraggedBlockCenter(Camera camera, out Vector2 center)
        {
            if (ghosts.Count == 0) { center = default; return false; }
            var rect = ghosts[0].Rect;
            center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            return true;
        }
        internal void CommitDrop(CommandBlockView block, RectTransform source, RectTransform destination)
        {
            dropAccepted = true;
            BlockDropped?.Invoke(block.Kind, source, destination);
        }

        public void FinishDrag(PointerEventData e)
        {
            var source = Dragged;
            if (!source) return;
            MoveDrag(e);
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(definitionPanel, e.position, e.pressEventCamera);
            if (!dropAccepted && snapTarget) snapTarget.TryDrop(e);
            if (!dropAccepted && inside && program.FreePlacement) program.TryDrop(e);
            bool discard = !source.IsPalette && !dropAccepted && !inside;
            EndDrag();
            if (discard) Delete(source);
        }

        public void EndDrag()
        {
            if (snapMarker) snapMarker.gameObject.SetActive(false);
            foreach (var block in dragging) if (block) block.SetDragAppearance(false);
            dragging.Clear();
            Dragged = null;
            foreach (var ghost in ghosts) if (ghost) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); }
            ghosts.Clear(); ghostOffsets.Clear();
            snapTarget = null;
        }
        void Delete(CommandBlockView block)
        {
            if (!block || block.IsPalette || block.Owner == null) return;
            EndDrag();
            foreach (var member in block.Owner.Group(block)) RemoveSingle(member);
            Changed();
        }
        public void DeleteOne(CommandBlockView block)
        {
            if (!block || block.IsPalette || block.Owner == null) return;
            EndDrag(); RemoveSingle(block, true); Changed();
        }
        void RemoveSingle(CommandBlockView block, bool reconnect = false)
        {
            if (selected && selected.transform.IsChildOf(block.transform)) selected = program;
            block.Owner?.Detach(block, reconnect);
            block.gameObject.SetActive(false);
            Destroy(block.gameObject);
        }
        void OnDisable() => EndDrag();
    }
}
