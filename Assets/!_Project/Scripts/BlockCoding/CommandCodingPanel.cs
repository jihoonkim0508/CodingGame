using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] UnityEvent<string> onApplied = new UnityEvent<string>();
        readonly Dictionary<string, CommandBlockView> variables = new Dictionary<string, CommandBlockView>();
        readonly HashSet<string> enemyVariables = new HashSet<string>();
        List<CommandBlockView> dragging = new List<CommandBlockView>();
        CommandDropZone selected;
        CommandBlockView ghost;
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
        internal void ReportSuccessfulDrop(CommandBlockView block, RectTransform source, RectTransform destination)
        { if (block && source && destination) BlockDropped?.Invoke(block.Kind, source, destination); }
        readonly HashSet<CommandDropZone> zones = new HashSet<CommandDropZone>();
        public void RegisterZone(CommandDropZone zone) => zones.Add(zone);
        public bool CanTake(CommandBlockView source) => !source.IsPalette || (Remaining?.Invoke(source.Kind) ?? int.MaxValue) > 0;
        int currentCategory;
        public void RefreshPalette() => ShowCategory(currentCategory);
        bool dropAccepted;
        Vector2 grabOffset;
        static int nextName;
        public CommandDropZone Program => program;
        public IReadOnlyList<PaletteItem> Palette => palette;
        public IReadOnlyDictionary<string, CommandBlockView> VariablePalette => variables;
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
            variables.Add("variable", variablePrefab);
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
        public void Select(CommandDropZone zone)
        {
            selected = zone;
            ShowMessage(zone.Slot == BlockSlotKind.Statement ? "명령 영역 선택됨 · 블록을 드래그하거나 목록에서 클릭하세요." :
                zone.Slot == BlockSlotKind.Condition ? "조건 슬롯 선택됨 · 논리 블록을 선택하세요." : "값 슬롯 선택됨 · 값 블록을 선택하세요.");
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
        public bool IsEnemyVariable(string name) => enemyVariables.Contains(name);
        public void RenameVariable(string previous, string current)
        {
            if (string.IsNullOrEmpty(previous)) return;
            if (program.AllBlocks().Any(b => b.BaseKind == BlockKind.DeclareVariable && b.VariableName == previous)) return;
            foreach (var block in program.AllBlocks())
                if (block.BaseKind == BlockKind.Variable && block.VariableName == previous) block.SetVariable(current);
        }
        void SyncVariables()
        {
            enemyVariables.Clear();
            enemyVariables.Add("enemy");
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
        public void Changed()
        {
            if (updating) return;
            updating = true;
            IsApplied = false;
            try
            {
                SyncVariables();
                var blocks = program.Read();
                string source = PythonTreeCompiler.Compile(functionName.text, blocks);
                pythonPreview.text = source;
                if (!loading) Publish(blocks, source);
            }
            catch (FormatException error) { pythonPreview.text = "# 미완성: 블록 연결과 입력을 완성해 주세요."; ShowMessage("미완성 · 이전 적용 코드 유지 · " + error.Message); }
            finally
            {
                updating = false;
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
                var blocks = program.Read();
                string source = PythonTreeCompiler.Compile(functionName.text, blocks);
                pythonPreview.text = source;
                Publish(blocks, source);
            }
            catch (FormatException error) { IsApplied = false; AppliedSource = null; ShowMessage("미완성 · " + error.Message); }
        }
        void Publish(IReadOnlyList<CodeBlock> blocks, string source)
        {
            string appliedMessage = ApplyToTarget?.Invoke(functionName.text, blocks);
            AppliedSource = source; IsApplied = true;
            ShowMessage(appliedMessage ?? "자동 적용됨");
            onApplied.Invoke(source);
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
            bool variable = data.Kind == BlockKind.Variable || data.Kind == BlockKind.Distance;
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
            dragging = source.IsPalette ? new List<CommandBlockView> { source } : source.Owner.Group(source);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(source.Rect, e.pressPosition, e.pressEventCamera, out var grab);
            grabOffset = grab - new Vector2(source.Rect.rect.xMin, source.Rect.rect.yMax);
            ghost = source.Copy(dragLayer);
            ghost.SetDragAppearance(true);
            ghost.Rect.anchorMin = ghost.Rect.anchorMax = ghost.Rect.pivot = new Vector2(0, 1);
            ghost.Rect.sizeDelta = new Vector2(ghost.preferredWidth, ghost.preferredHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ghost.Rect);
            foreach (var block in dragging) block.SetDragAppearance(true);
            MoveDrag(e);
        }
        public void MoveDrag(PointerEventData e)
        {
            if (!ghost) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, e.pressEventCamera, out var point))
                ghost.Rect.localPosition = point - grabOffset;
            UpdateSnapPreview(e);
        }
        void UpdateSnapPreview(PointerEventData e)
        {
            if (!snapMarker) return;
            snapMarker.gameObject.SetActive(false);
            zones.RemoveWhere(zone => !zone);
            CommandDropZone target = null;
            foreach (var zone in zones)
            {
                if (!zone.gameObject.activeInHierarchy || zone.Rect.IsChildOf(ghost.transform) ||
                    (zone.ParentBlock && zone.ParentBlock.IsPalette) || dragging.Any(b => zone.Rect.IsChildOf(b.transform)) ||
                    !RectTransformUtility.RectangleContainsScreenPoint(zone.Rect, e.position, e.pressEventCamera)) continue;
                if (!target || zone.Rect.IsChildOf(target.Rect)) target = zone;
            }
            if (!target || !target.Preview(Dragged, e, out var preview)) return;
            var topLeft = target.Rect.TransformPoint(new Vector3(target.Rect.rect.xMin + preview.x, target.Rect.rect.yMax + preview.y, 0));
            snapMarker.position = topLeft;
            var scale = target.Rect.lossyScale.x / snapMarker.parent.lossyScale.x;
            snapMarker.sizeDelta = new Vector2(preview.width, preview.height) * scale;
            snapMarker.gameObject.SetActive(true); snapMarker.SetAsLastSibling();
        }
        public Vector2 DropPosition(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(program.Rect, e.position, e.pressEventCamera, out var point);
            return point - new Vector2(program.Rect.rect.xMin, program.Rect.rect.yMax) - grabOffset;
        }
        public void AcceptDrop() => dropAccepted = true;

        public void FinishDrag(PointerEventData e)
        {
            var source = Dragged;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(definitionPanel, e.position, e.pressEventCamera);
            if (source && !dropAccepted && inside && program.FreePlacement) program.PlaceFree(source, DropPosition(e));
            bool discard = source && !source.IsPalette && !dropAccepted && !inside;
            EndDrag();
            if (discard) Delete(source);
        }

        public void EndDrag()
        {
            if (snapMarker) snapMarker.gameObject.SetActive(false);
            foreach (var block in dragging) if (block) block.SetDragAppearance(false);
            dragging.Clear();
            Dragged = null;
            if (ghost) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); ghost = null; }
        }
        public void Delete(CommandBlockView block)
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
