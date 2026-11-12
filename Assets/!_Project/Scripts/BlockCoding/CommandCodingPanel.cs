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
        // See-through copies of the dragged lines where they will land (snap preview draft 1), and the zone opened for them.
        readonly List<CommandBlockView> landing = new List<CommandBlockView>();
        CommandDropZone gapZone;
        readonly List<Vector2> ghostOffsets = new List<Vector2>();
        readonly Vector3[] dragCorners = new Vector3[4];
        readonly List<Image> tabDots = new List<Image>();
        CommandDropZone selected;
        CommandDropZone snapTarget;
        CommandBlockView openingBody;
        PointerEventData dragEvent;
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
        // "block-pick" when a drag takes a block, "block-delete" when blocks leave the program; the host plays a sound.
        public event Action<string> Feedback;
        readonly HashSet<CommandDropZone> zones = new HashSet<CommandDropZone>();
        internal void RegisterZone(CommandDropZone zone) => zones.Add(zone);
        internal bool CanTake(CommandBlockView source) => !source.IsPalette || (Remaining?.Invoke(source.Kind) ?? int.MaxValue) > 0;
        int currentCategory;
        public void RefreshPalette() => ShowCategory(currentCategory);
        // The player clicked a palette tab (not raised when code switches the tab).
        public event Action<int> CategoryChosen;
        public RectTransform CategoryTab(int category) =>
            category >= 0 && category < categoryButtons.Length && categoryButtons[category] ? (RectTransform)categoryButtons[category].transform : null;
        bool dropAccepted;
        static int nextName;
        public CommandDropZone Program => program;
        // The line under the code that reports what is wrong with it (the tutorial calls it the debugging line).
        public RectTransform StatusLine => status.rectTransform;
        public CommandDropZone SelectedZone => selected;
#if UNITY_EDITOR
        public CommandDropZone EditorSnapTarget => snapTarget;
        // A snap preview is up: the marker (a slot) or the landing copies (a line).
        public bool SnapPreviewShown => snapMarker && snapMarker.gameObject.activeSelf || landing.Any(mark => mark && mark.gameObject.activeSelf);
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
            // The def name's box follows what is typed, like the blocks' fields.
            functionName.onValueChanged.AddListener(_ => { CommandBlockView.FitInput(functionName, 80); Changed(); });
            CommandBlockView.FitInput(functionName, 80);
            program.Bind(this, null);
            foreach (var item in palette) item.view.Bind(this, null, true);
            variablePrefab.Bind(this, null, true);
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int category = i;
                categoryButtons[i].onClick.AddListener(() => { ShowCategory(category); CategoryChosen?.Invoke(category); });
                // Tab names in Jalnan like the other labels, and on a paper tab a dot of its blocks' colour (비주얼_레퍼런스.md 3).
                var label = categoryButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label && UITheme.Display) { label.font = UITheme.Display; label.fontStyle &= ~FontStyles.Bold; label.fontSize = 16; label.margin = new Vector4(14, 0, 0, 0); }
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                dot.rectTransform.SetParent(categoryButtons[i].transform, false);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0, .5f);
                dot.rectTransform.sizeDelta = new Vector2(9, 9); dot.rectTransform.anchoredPosition = new Vector2(14, 0);
                dot.sprite = UITheme.Circle; dot.color = UITheme.BlockCategories[Mathf.Min(i, UITheme.BlockCategories.Length - 1)]; dot.raycastTarget = false;
                tabDots.Add(dot);
            }
            Breathe();
            // The shortcut line sits on the blue backdrop: bold, outlined and 18 px at least (3.7:1 bare white).
            if (UITheme.Bold && UITheme.Caption) { status.font = UITheme.Bold; status.fontSharedMaterial = UITheme.Caption; status.fontSize = Mathf.Max(18, status.fontSize); }
            selected = program;
            ShowCategory(0);
            Changed();
        }
        // Room to breathe (play feedback: no padding looked cramped; the palette's edge also clipped the blocks' outlines, which
        // are drawn 3 px outside them): tabs squarer than pills and a little apart, the palette inset from its card with a
        // margin round its blocks and one column the width of the card, and the code area plain paper without the grid.
        void Breathe()
        {
            const float Inset = 12;
            var tabs = categoryButtons.Length > 0 && categoryButtons[0] ? categoryButtons[0].transform.parent as RectTransform : null;
            if (tabs)
            {
                tabs.sizeDelta = new Vector2(-2 * Inset, tabs.sizeDelta.y);
                if (tabs.TryGetComponent<HorizontalLayoutGroup>(out var row)) row.spacing = 6;
            }
            var content = palette.Length > 0 && palette[0].view ? palette[0].view.transform.parent as RectTransform : null;
            if (content && content.parent is RectTransform viewport)
            {
                viewport.sizeDelta = new Vector2(-2 * Inset, viewport.sizeDelta.y);
                content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
                content.sizeDelta = new Vector2(0, content.sizeDelta.y);
                // Left and top room for the outlines and shadows; the counts take the right edge (CommandBlockView.LateUpdate).
                if (content.TryGetComponent<VerticalLayoutGroup>(out var column)) { column.padding = new RectOffset(8, 0, 10, 24); column.spacing = 14; }
            }
            // Where a dragged block will land: a rounded sky bar between lines, or the block's own outline in an empty place.
            if (snapMarker && snapMarker.TryGetComponent<Image>(out var mark) && UITheme.Fill)
            { mark.sprite = UITheme.Fill; mark.type = Image.Type.Sliced; mark.color = new Color(UITheme.Sky.r, UITheme.Sky.g, UITheme.Sky.b, .6f); }
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
            // The chosen tab is filled with its blocks' colour, white text and a little larger; the others are paper with muted
            // text (play feedback: a beige tab on the beige panel did not read as chosen).
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                bool chosen = i == category;
                categoryButtons[i].targetGraphic.color = chosen ? new Color(0.15f, 0.45f, 0.65f) : new Color(0.13f, 0.17f, 0.24f);
                var label = categoryButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label) label.color = chosen ? Color.white : new Color(.72f, .8f, .86f);
                if (i < tabDots.Count) tabDots[i].enabled = !chosen;
                categoryButtons[i].transform.localScale = Vector3.one * (chosen ? 1.06f : 1);
            }
        }
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
                functionName.SetTextWithoutNotify(draft.name); CommandBlockView.FitInput(functionName, 80);
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
                functionName.SetTextWithoutNotify(name); CommandBlockView.FitInput(functionName, 80); selected = program; program.Load(blocks);
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
            Dragged = source; Feedback?.Invoke("block-pick");
            dropAccepted = false;
            dragging = CommandDropZone.Moving(source);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(program.Rect, e.pressPosition, e.pressEventCamera, out var programPress);
            programGrabOffset = programPress - TopLeft(program.Rect, source.Rect);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.pressPosition, e.pressEventCamera, out var dragPress);
            var sourceDragTopLeft = TopLeft(dragLayer, source.Rect);
            dragGrabOffset = dragPress - sourceDragTopLeft;
            ghosts.Clear(); ghostOffsets.Clear(); landing.Clear();
            foreach (var block in dragging)
            {
                ghostOffsets.Add(TopLeft(dragLayer, block.Rect) - sourceDragTopLeft);
                var copy = block.Copy(dragLayer);
                copy.SetGhostAppearance();
                copy.Rect.anchorMin = copy.Rect.anchorMax = copy.Rect.pivot = new Vector2(0, 1);
                copy.Rect.sizeDelta = new Vector2(copy.preferredWidth, copy.preferredHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(copy.Rect);
                ghosts.Add(copy);
                var mark = block.Copy(dragLayer);
                mark.SetLandingAppearance();
                mark.Rect.anchorMin = mark.Rect.anchorMax = mark.Rect.pivot = new Vector2(0, 1);
                mark.Rect.sizeDelta = new Vector2(mark.preferredWidth, mark.preferredHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(mark.Rect);
                mark.gameObject.SetActive(false);
                landing.Add(mark);
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
            dragEvent=e;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, e.pressEventCamera, out var point))
                for (int i = 0; i < ghosts.Count; i++) ghosts[i].Rect.localPosition = point - dragGrabOffset + ghostOffsets[i];
            UpdateSnapPreview(e);
        }
        void LateUpdate(){if(Dragged && dragEvent!=null)UpdateSnapPreview(dragEvent);}
        void UpdateBodyPreview(PointerEventData e)
        {
            CommandBlockView candidate=null;
            if(Dragged && PythonTreeCompiler.OutputSlot(Dragged.Kind)==BlockSlotKind.Statement)
            foreach(var zone in zones){
                var block=zone?zone.ParentBlock:null;
                if(!block || block.Body!=zone || block.IsPalette || !block.gameObject.activeInHierarchy ||
                    ghosts.Any(g=>block.transform.IsChildOf(g.transform)) || landing.Any(g=>g&&block.transform.IsChildOf(g.transform)) || dragging.Any(b=>block.transform.IsChildOf(b.transform)))continue;
                if(!zone.Preview(Dragged,e,out _))continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(block.Rect,e.position,e.pressEventCamera,out var point);
                float down=block.Rect.rect.yMax-point.y,left=point.x-block.Rect.rect.xMin;
                var draggedBounds=DraggedBounds(block.Rect);
                float draggedLeft=draggedBounds.xMin-block.Rect.rect.xMin;
                // Enter through the lower half of the header; retain the opening while inside its cavity.
                float bottom=block==openingBody?Mathf.Max(block.Rect.rect.height,block.HeaderHeight+zone.preferredHeight+CommandBlockShape.ControlFooterHeight):block.HeaderHeight+Dragged.HeaderHeight*.5f+18;
                if(left<24 || left>block.Rect.rect.width+18 || down<block.HeaderHeight*.5f || down>bottom ||
                    block!=openingBody && down>block.HeaderHeight && draggedLeft<12)continue;
                if(!candidate || block.transform.IsChildOf(candidate.transform))candidate=block;
            }
            if(openingBody!=candidate){if(openingBody)openingBody.PreviewBody(false);openingBody=candidate;if(openingBody)openingBody.PreviewBody(true);}
            LayoutRebuilder.ForceRebuildLayoutImmediate(program.Rect);
        }
        void UpdateSnapPreview(PointerEventData e)
        {
            if (!snapMarker) return;
            UpdateBodyPreview(e);
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
                if (!zone.gameObject.activeInHierarchy || ghosts.Any(ghost => zone.Rect.IsChildOf(ghost.transform)) || landing.Any(mark => mark && zone.Rect.IsChildOf(mark.transform)) ||
                    (zone.ParentBlock && zone.ParentBlock.IsPalette) || dragging.Any(b => zone.Rect.IsChildOf(b.transform))) continue;
                if(zone.ParentBlock && zone.ParentBlock.Body==zone && !zone.ParentBlock.BodyExpanded)continue;
                bool socket = !zone.FreePlacement && zone.Slot != BlockSlotKind.Statement;
                var bounds = DraggedBounds(zone.Rect);
                var area = zone.Rect.rect;
                bool pointerOver = (openingBody && zone==openingBody.Body) || RectTransformUtility.RectangleContainsScreenPoint(zone.Rect, e.position, e.pressEventCamera);
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
            if (!target || targetIsSocket) ShowLanding(null, default);
            if (!target) return;
            snapTarget = target;
            if (!targetIsSocket)
            {
                // A line: room opens where it will land and a see-through copy sits in it; the dragged blocks stay under the hand.
                var at = target.OpenGap(Dragged, e, LandingHeight());
                if (gapZone && gapZone != target) gapZone.CloseGap();
                gapZone = target;
                LayoutRebuilder.ForceRebuildLayoutImmediate(program.Rect);
                ShowLanding(target, at);
                return;
            }
            // As in e085adf, only the marker snaps; the dragged blocks remain under the hand.
            snapMarker.position = target.Rect.TransformPoint(new Vector3(target.Rect.rect.xMin + targetPreview.x,
                target.Rect.rect.yMax + targetPreview.y, 0));
            var scale = target.Rect.lossyScale.x / snapMarker.parent.lossyScale.x;
            snapMarker.sizeDelta = new Vector2(targetPreview.width, targetPreview.height) * scale;
            if (snapMarker.TryGetComponent<Image>(out var mark)) mark.pixelsPerUnitMultiplier = 16f / Mathf.Clamp(snapMarker.sizeDelta.y * .5f, 1, 8);
            snapMarker.gameObject.SetActive(true); snapMarker.SetAsLastSibling();
        }
        float LandingHeight()
        {
            float height = 0;
            for (int i = 0; i < dragging.Count; i++)
            {
                height += dragging[i].preferredHeight;
                if (i > 0 && PythonTreeCompiler.IsBranch(dragging[i].Kind)) height -= dragging[i - 1].FooterOverlap;
            }
            return height;
        }
        // Puts the landing copies at zone-local top-left at (none when zone is null, closing any room left open).
        void ShowLanding(CommandDropZone zone, Vector2 at)
        {
            if (!zone && gapZone) { gapZone.CloseGap(); gapZone = null; }
            Vector2 top = zone ? (Vector2)dragLayer.InverseTransformPoint(zone.Rect.TransformPoint(new Vector3(zone.Rect.rect.xMin + at.x, zone.Rect.rect.yMax + at.y, 0))) : default;
            float scale = zone ? zone.Rect.lossyScale.x / dragLayer.lossyScale.x : 1;
            for (int i = 0; i < landing.Count; i++)
            {
                if (!landing[i]) continue;
                landing[i].gameObject.SetActive(zone);
                if (!zone) continue;
                landing[i].Rect.localScale = Vector3.one * scale;
                landing[i].Rect.localPosition = top + ghostOffsets[i];
                landing[i].Rect.SetAsFirstSibling(); // under the hand's copies
            }
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
            // The drop reads the lines where the preview showed them (the room closes at the next layout pass).
            if (gapZone) { gapZone.CloseGap(); gapZone = null; }
            if (!dropAccepted && snapTarget) snapTarget.TryDrop(e);
            if (!dropAccepted && inside && program.FreePlacement) program.TryDrop(e);
            bool discard = !source.IsPalette && !dropAccepted && !inside;
            EndDrag();
            if (discard) Delete(source);
        }

        public void EndDrag()
        {
            if(openingBody)openingBody.PreviewBody(false);
            openingBody=null;dragEvent=null;
            if (snapMarker) snapMarker.gameObject.SetActive(false);
            foreach (var block in dragging) if (block) block.SetDragAppearance(false);
            dragging.Clear();
            Dragged = null;
            foreach (var ghost in ghosts) if (ghost) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); }
            foreach (var mark in landing) if (mark) { mark.gameObject.SetActive(false); Destroy(mark.gameObject); }
            if (gapZone) { gapZone.CloseGap(); gapZone = null; }
            ghosts.Clear(); ghostOffsets.Clear(); landing.Clear();
            snapTarget = null;
        }
        void Delete(CommandBlockView block)
        {
            if (!block || block.IsPalette || block.Owner == null) return;
            EndDrag();
            foreach (var member in block.Owner.Group(block)) RemoveSingle(member);
            Changed(); Feedback?.Invoke("block-delete");
        }
        // Right-click menu (play feedback 2026-11-12): on a placed block 복사 (Stage 2 on, CanCopy) and 삭제, each for the block
        // with what is inside it (an if with its elif/else); on the empty code area 붙여넣기, which sets the copied lines down where
        // it was clicked if the inventory still holds every block in them, and warns when it does not.
        public Func<bool> CanCopy { get; set; }
        List<CodeBlock> clipboard;
        GameObject menu;
        public bool MenuOpen => menu;
        internal void RightClick(CommandBlockView block, PointerEventData e)
        {
            if (!block || block.IsPalette || block.Owner == null) return;
            EndDrag();
            var items = new List<(string, Action)>();
            if (CanCopy?.Invoke() == true && block.Owner.Slot == BlockSlotKind.Statement) items.Add(("복사", () => Copy(block)));
            items.Add(("삭제", () => DeleteUnit(block)));
            OpenMenu(e.position, e.pressEventCamera, items);
        }
        internal void RightClickEmpty(PointerEventData e)
        {
            if (CanCopy?.Invoke() != true || !program.FreePlacement) return;
            EndDrag();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(program.Rect, e.position, e.pressEventCamera, out var point);
            var at = point - new Vector2(program.Rect.rect.xMin, program.Rect.rect.yMax);
            OpenMenu(e.position, e.pressEventCamera, new List<(string, Action)> { ("붙여넣기", () => Paste(at)) });
        }
        static List<CommandBlockView> Unit(CommandBlockView block)
        {
            var group = block.Owner.Group(block); // on the free canvas: the block and every line under it
            int count = 1;
            if (block.Kind == BlockKind.If || block.Kind == BlockKind.Elif)
                while (count < group.Count && (group[count].Kind == BlockKind.Elif || group[count].Kind == BlockKind.Else))
                    if (group[count++].Kind == BlockKind.Else) break;
            return group.GetRange(0, count);
        }
        public void Copy(CommandBlockView block)
        {
            if (!block || block.IsPalette || block.Owner == null) return;
            clipboard = Unit(block).Select(b => b.Read()).ToList();
            ShowMessage("복사했어요. 코드 칸의 빈 곳을 우클릭해 붙여넣으세요.");
        }
        public void DeleteUnit(CommandBlockView block)
        {
            if (!block || block.IsPalette || block.Owner == null) return;
            EndDrag();
            foreach (var member in Unit(block)) RemoveSingle(member, true);
            Changed(); Feedback?.Invoke("block-delete");
        }
        // at: from the code area's top-left, y down negative (as DropPosition).
        public bool Paste(Vector2 at)
        {
            if (clipboard == null) { Warn("먼저 블록을 우클릭해 복사하세요."); return false; }
            var views = clipboard.Select(data => CreateBlock(data, dragLayer)).ToList();
            bool enough = views.SelectMany(v => v.Descendants()).GroupBy(b => b.BaseKind)
                .All(need => (Remaining?.Invoke(need.Key) ?? int.MaxValue) >= need.Count());
            if (!enough || !program.PlaceNew(views, at))
            {
                foreach (var view in views) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
                Warn(enough ? "여기에는 붙여넣을 수 없어요." : "블록이 없어서 배치할 수 없어요.");
                return false;
            }
            Feedback?.Invoke("block-drop");
            return true;
        }
        Vector2 menuScreen; Camera menuCamera;
        void OpenMenu(Vector2 screen, Camera camera, List<(string label, Action act)> items)
        {
            CloseMenu();
            menuScreen = screen; menuCamera = camera;
            var box = MenuBox();
            foreach (var (label, act) in items)
            {
                var button = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                button.transform.SetParent(box, false);
                var size = button.GetComponent<LayoutElement>(); size.preferredWidth = 150; size.preferredHeight = 46;
                var image = UITheme.Style(button.GetComponent<Image>(), UITheme.Button, label == "삭제" ? UITheme.Coral : UITheme.Sky);
                var click = button.GetComponent<Button>(); click.targetGraphic = image;
                click.onClick.AddListener(() => { CloseMenu(); act(); });
                Text(button.transform, label, 22, Color.white);
            }
        }
        // A red note where the menu was, for two seconds (the status line under the code is easy to miss).
        void Warn(string text)
        {
            ShowMessage(text); CloseMenu();
            var box = MenuBox(false);
            var note = new GameObject("Note", typeof(RectTransform), typeof(LayoutElement));
            note.transform.SetParent(box, false);
            var size = note.GetComponent<LayoutElement>(); size.preferredWidth = 330; size.preferredHeight = 44;
            Text(note.transform, text, 20, UITheme.Hex(0xE03131));
            StartCoroutine(CloseLater(menu));
        }
        System.Collections.IEnumerator CloseLater(GameObject note)
        {
            yield return new WaitForSecondsRealtime(2);
            if (note && note == menu) CloseMenu();
        }
        // The menu on the root canvas over everything; under it a clear sheet that shuts it on a click anywhere else.
        RectTransform MenuBox(bool modal = true)
        {
            var canvas = (RectTransform)definitionPanel.GetComponentInParent<Canvas>().rootCanvas.transform;
            menu = new GameObject("Block menu", typeof(RectTransform));
            var root = (RectTransform)menu.transform; root.SetParent(canvas, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            if (modal)
            {
                var sheet = menu.AddComponent<Image>(); sheet.color = Color.clear;
                var trigger = menu.AddComponent<EventTrigger>();
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(_ => CloseMenu()); trigger.triggers.Add(click);
            }
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            box.SetParent(root, false); box.anchorMin = box.anchorMax = new Vector2(.5f, .5f); box.pivot = new Vector2(0, 1);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, menuScreen, menuCamera, out var point);
            box.anchoredPosition = point + new Vector2(4, -4) - root.rect.center;
            var image = box.GetComponent<Image>(); image.sprite = UITheme.Card; image.type = Image.Type.Sliced; image.color = UITheme.Paper;
            var layout = box.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(10, 10, 10, 10); layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = box.GetComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            root.SetAsLastSibling();
            return box;
        }
        static void Text(Transform parent, string text, float size, Color color)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            if (UITheme.Bold) label.font = UITheme.Bold;
            label.text = text; label.fontSize = size; label.color = color; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        }
        public void CloseMenu() { if (menu) Destroy(menu); menu = null; }
        public void DeleteOne(CommandBlockView block)
        {
            if (!block || block.IsPalette || block.Owner == null) return;
            EndDrag(); RemoveSingle(block, true); Changed(); Feedback?.Invoke("block-delete");
        }
        void RemoveSingle(CommandBlockView block, bool reconnect = false)
        {
            if (selected && selected.transform.IsChildOf(block.transform)) selected = program;
            block.Owner?.Detach(block, reconnect);
            block.gameObject.SetActive(false);
            Destroy(block.gameObject);
        }
        void OnDisable() { EndDrag(); CloseMenu(); }
    }
}
