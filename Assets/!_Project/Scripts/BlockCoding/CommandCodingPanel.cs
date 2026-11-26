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
        Dictionary<string, PythonTreeCompiler.Sort> sorts = new Dictionary<string, PythonTreeCompiler.Sort>();
        // The team board's names, so a robot's code may use them (DefenseCodeEditor gives the battle's); null outside a battle.
        public Func<PythonTreeCompiler.Shared> Shared { get; set; }
        // The team board's own lines are edited here too: then the code compiles as the board (`name = value` only).
        public bool BoardMode { get; set; }
        List<CommandBlockView> dragging = new List<CommandBlockView>();
        readonly List<CommandBlockView> ghosts = new List<CommandBlockView>();
        // See-through copies of the dragged lines where they will land (snap preview draft 1), and the zone opened for them.
        readonly List<CommandBlockView> landing = new List<CommandBlockView>();
        CommandDropZone gapZone;
        readonly List<Vector2> ghostOffsets = new List<Vector2>();
        readonly Vector3[] dragCorners = new Vector3[4];
        readonly List<Image> tabDots = new List<Image>();
        CommandDropZone selected;
        int selectedIndex = -1;
        CommandDropZone snapTarget;
        CommandBlockView openingBody;
        PointerEventData dragEvent;
        Vector2 programGrabOffset, dragGrabOffset;
        bool updating;
        bool initialized;
        bool loading;
        bool restoringHistory;
        string sizedFunctionName;
        float sizedTitleWidth = -1;
        // The def robot_N(): block round the start anchor (2026-11-25 request): a C like while's, its mouth holding the chain that
        // runs; drawn only (no CommandBlockView), so it cannot be dragged off or deleted, and the Python text is as before. Its
        // header is the name row that sat over the code area.
        CommandBlockShape defBlock;
        RectTransform titleBar;
        float defMouth;
        const float DefHeader = 52, DefEmptyMouth = 52;
        // The def block's top-left in the program's units: its spine and header round the start anchor.
        static Vector2 DefOrigin => CommandDropZone.FirstPosition + new Vector2(-CommandBlockShape.ControlSpineWidth, DefHeader);
        public CommandWorkspace Workspace { get; private set; }
        public RectTransform DefBlock => defBlock ? defBlock.rectTransform : null;
        internal sealed class Draft
        {
            public string name;
            public List<CommandDropZone.DraftGroup> groups;
        }
        public sealed class EditHistory
        {
            internal readonly List<Draft> states = new List<Draft>();
            internal int index = -1;
            // The three tabs (Docs/코딩_모드_3탭.md): the commit is the last code that became a block tree (the start chain),
            // numbered; each tab keeps a record of how it was left, tagged with the commit number then.
            internal List<CodeBlock> commit = new List<CodeBlock>();
            public int CommitNumber { get; internal set; }
            internal List<CommandDropZone.DraftGroup> blockRecord; internal int blockRecordCommit = -1;
            internal string pythonRecord; internal int pythonCaret, pythonRecordCommit = -1;
            public IReadOnlyList<CodeBlock> Commit => commit;
        }
        public EditHistory History { get; private set; } = new EditHistory();
        public bool CanUndo => History.index > 0;
        public bool CanRedo => History.index + 1 < History.states.Count;
        public bool TextInputFocused => functionName.isFocused || program.AllBlocks().Any(b => b.TextInputFocused);
        public Func<string, IReadOnlyList<CodeBlock>, string> ApplyToTarget { get; set; }
        public Func<BlockKind, bool> IsAvailable { get; set; }
        public Func<BlockKind, int> Remaining { get; set; }
        public event Action<BlockKind, RectTransform, RectTransform> BlockDropped;
        // A palette block clicked, before it is added: the host may choose where it goes (a tutorial step's demo place).
        public Action<CommandBlockView> PaletteClicking { get; set; }
        // "block-pick" when a drag takes a block, "block-delete" when blocks leave the program; the host plays a sound.
        public event Action<string> Feedback;
        // A placed block copied with the right-click menu (a tutorial step waits on it, 2-5).
        public event Action Copied;
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
            BuildDef();
            var scroll = program.GetComponentInParent<ScrollRect>(true);
            if (scroll)
            {
                var view = scroll.viewport ? scroll.viewport : (RectTransform)program.transform.parent;
                Workspace = view.gameObject.AddComponent<CommandWorkspace>();
                Workspace.Bind(this, scroll);
                ShowDefAtTopLeft();
            }
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
            // The chosen tab is filled with its blocks' colour, ink text and a little larger; the others are dark with muted
            // text and a dot of their blocks' colour (play feedback: a beige tab on the beige panel did not read as chosen).
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                bool chosen = i == category;
                categoryButtons[i].targetGraphic.color = chosen ? UITheme.BlockCategories[Mathf.Min(i, UITheme.BlockCategories.Length - 1)] : new Color(0.13f, 0.17f, 0.24f);
                var label = categoryButtons[i].GetComponentInChildren<TMP_Text>(true);
                // The dot's room on the left goes with the dot, so the chosen tab's name sits in its middle.
                if (label) { label.color = chosen ? UITheme.Ink : new Color(.72f, .8f, .86f); label.margin = new Vector4(chosen ? 0 : 14, 0, 0, 0); }
                if (i < tabDots.Count) tabDots[i].enabled = !chosen;
                categoryButtons[i].transform.localScale = Vector3.one * (chosen ? 1.06f : 1);
            }
        }
        // index: where a clicked block goes in the zone (a tutorial step's place mid-body); -1 at its end.
        internal void Select(CommandDropZone zone, int index = -1)
        {
            selected = zone; selectedIndex = index;
            // Selecting the command area says nothing: the message line keeps showing the last syntax error.
            if (zone.Slot != BlockSlotKind.Statement)
                ShowMessage(zone.Slot == BlockSlotKind.Condition ? "조건 슬롯 선택됨 · 논리 블록을 선택하세요." : "값 슬롯 선택됨 · 값 블록을 선택하세요.");
        }
        public void AddFromPalette(CommandBlockView source)
        {
            if (!CanTake(source)) { ShowMessage("보유 수량을 모두 사용했습니다."); return; }
            PaletteClicking?.Invoke(source);
            var target = selected ? selected : program;
            if (PythonTreeCompiler.OutputSlot(source.Kind) == BlockSlotKind.Statement && target.Slot != BlockSlotKind.Statement) target = program;
            if (target == program && program.FreePlacement)
            {
                if (PythonTreeCompiler.OutputSlot(source.Kind) != BlockSlotKind.Statement && source.BaseKind != BlockKind.Variable)
                { ShowMessage("값을 넣을 빈 슬롯을 선택하세요."); return; }
                if (program.Insert(source, program.Blocks.Count)) RevealProgramEnd();
                return;
            }
            int at = target == selected && selectedIndex >= 0 ? Mathf.Min(selectedIndex, target.Blocks.Count) : target.Blocks.Count;
            selectedIndex = -1;
            if (!target.Insert(source, at)) ShowMessage("먼저 맞는 모양의 빈 슬롯을 선택하거나, 블록을 원하는 위치로 드래그하세요.");
            else if (target == program && at == program.Blocks.Count - 1) RevealProgramEnd();
        }
        // A click-add at the program's end lands below a long program: pan the workspace up to it.
        // ponytail: only top-level appends; a block added inside a nested block at the bottom does not scroll.
        void RevealProgramEnd()
        {
            var last = program.StartChain().LastOrDefault();
            if (!Workspace || !last) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(program.Rect);
            Workspace.KeepInView(last.Rect);
        }
        void BuildDef()
        {
            defBlock = new GameObject("DefBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(CommandBlockShape)).GetComponent<CommandBlockShape>();
            var rect = defBlock.rectTransform;
            rect.SetParent(program.Rect, false); rect.SetAsFirstSibling();
            defBlock.Configure(BlockSlotKind.Statement, true, true);
            defBlock.HeaderHeight = DefHeader;
            defBlock.color = UITheme.BlockCategories[0]; // while's and if's
            titleBar = (RectTransform)functionName.transform.parent;
            // The name row's place over the code area goes to the code: the divider comes up to where the row began.
            var divider = titleBar.parent ? titleBar.parent.Find("HeaderDivider") as RectTransform : null;
            var area = program.GetComponentInParent<ScrollRect>(true);
            if (divider && area && divider.parent == titleBar.parent && area.transform.parent == titleBar.parent)
            {
                float freed = titleBar.anchoredPosition.y + titleBar.rect.height * (1 - titleBar.pivot.y) - divider.anchoredPosition.y - 4;
                divider.anchoredPosition += new Vector2(0, freed);
                ((RectTransform)area.transform).offsetMax += new Vector2(0, freed);
            }
            titleBar.SetParent(rect, false);
            // Wide enough for any name (RefreshFunctionNameSize fits the field to it); the block is as wide as what is shown.
            CommandBlockView.Place(titleBar, 14, 6, 600, DefHeader - 12);
            defMouth = DefEmptyMouth;
            LayoutDef();
        }
        // The def block's mouth follows the chain that runs (one line high when empty), easing like a C block's body.
        void LayoutDef()
        {
            var chain = program.StartChainSize();
            float mouth = Mathf.Max(DefEmptyMouth, chain.y);
            defMouth = Mathf.Abs(defMouth - mouth) < .5f ? mouth : Mathf.Lerp(defMouth, mouth, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 25));
            float width = Mathf.Max(LayoutUtility.GetPreferredWidth(titleBar) + 28, CommandBlockShape.ControlSpineWidth + Mathf.Max(160, chain.x));
            var size = new Vector2(width, DefHeader + defMouth + CommandBlockShape.ControlFooterHeight);
            if (defBlock.rectTransform.sizeDelta == size) return;
            CommandBlockView.Place(defBlock.rectTransform, DefOrigin.x, -DefOrigin.y, size.x, size.y);
            defBlock.SetVerticesDirty();
        }
        // Pans the code so the def block sits at the code area's top-left; zoom and blocks stay as they are. Done when the editor
        // shows another robot's code (the editor has no block/text modes: its Python view and preview toggles are switched off).
        public void ShowDefAtTopLeft() { if (Workspace) Workspace.ShowAtTopLeft(DefOrigin); }
        // What the off-screen note looks for: the def block (with the program in it) and each loose chain.
        internal IEnumerable<IReadOnlyList<RectTransform>> Chains()
        {
            if (defBlock) yield return new[] { defBlock.rectTransform };
            foreach (var root in program.LooseRoots()) yield return program.Group(root).Select(b => b.Rect).ToList();
        }
        public void ShowMessage(string text) => status.text = text;
        // The team board's def row reads team_board and cannot be renamed.
        public void SetNameEditable(bool editable) { Initialize(); functionName.interactable = editable; }
        internal void FillNewLoop(CommandBlockView block)
        {
            // range(10), or a for-each's get_enemies(): the loop comes with the one value it is made for.
            if (block.Kind != BlockKind.For && block.Kind != BlockKind.ForEach || block.Arguments[0].Blocks.Count != 0) return;
            var fill = block.Kind == BlockKind.For ? BlockKind.Number : BlockKind.GetEnemies;
            var value = palette.FirstOrDefault(item => item.view.BaseKind == fill).view;
            if (value && (IsAvailable?.Invoke(fill) ?? true) && CanTake(value))
                block.Arguments[0].Load(new[] { new CodeBlock(fill, fill == BlockKind.Number ? "10" : "0") });
        }
        internal PythonTreeCompiler.Sort SortOf(string name) => sorts.TryGetValue(name ?? "", out var sort) ? sort : PythonTreeCompiler.Sort.None;
        // False when another declaration already has the new name: the uses wait under the old one. Renamed into it they
        // became that variable's too (enemy_distance backspaced past enemy) and no longer followed their own declaration.
        // ponytail: a name finished on another variable's leaves its uses one edit short; they are retyped by hand.
        internal bool RenameVariable(CommandBlockView declaration, string previous, string current)
        {
            var others = program.AllBlocks().Where(b => b.BaseKind == BlockKind.DeclareVariable && b != declaration).Select(b => b.VariableName).ToList();
            if (others.Contains(current)) return false;
            if (string.IsNullOrEmpty(previous) || others.Contains(previous)) return true;
            foreach (var block in program.AllBlocks())
                if (block.BaseKind == BlockKind.Variable && block.VariableName == previous) block.SetVariable(current);
            return true;
        }
        void SyncVariables()
        {
            // What each name holds as the code assigns it (an enemy, a robot, a list, a list's item), the board's names first:
            // the members offered after `name.` follow it. A for-each's name is an enemy until its list says otherwise.
            var assigned = new List<CodeBlock>();
            foreach (var block in program.AllBlocks())
            {
                if (block.BaseKind != BlockKind.DeclareVariable && block.BaseKind != BlockKind.ForEach) continue;
                string name;
                try { name = PythonTreeCompiler.NormalizeIdentifier(block.VariableName); }
                catch (FormatException) { name = block.RetainedName; }
                if (string.IsNullOrEmpty(name)) continue;
                var line = new CodeBlock(block.BaseKind, name, block.Arguments.Count > 0 ? block.Arguments[0].ReadArgument() : null);
                assigned.Add(line);
            }
            sorts = PythonTreeCompiler.Sorts(assigned, BoardMode ? null : Shared?.Invoke());
            foreach (var block in program.AllBlocks()) if (block.BaseKind == BlockKind.Variable) block.RefreshVariableOptions();
            variableEmptyHint.SetActive(false);
        }
        internal void Changed()
        {
            RefreshFunctionNameSize();
            if (updating) return;
            updating = true;
            IsApplied = false;
            try
            {
                SyncVariables();
                UpdateCommit();
                Diagnose();
                var blocks = CompilePreview(out string source);
                if (!loading) Publish(blocks, source);
            }
            catch (FormatException error)
            {
                pythonPreview.text = "# 미완성: 블록 연결과 입력을 완성해 주세요.";
                if (!loading) ClearTarget();
                ShowMessage("미완성 · 로봇이 행동하지 않음 · " + Counts(Math.Max(1, ErrorCount), WarningCount) + ProblemText(error));
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
            Initialize(); History = history; quietProblems = true; HideTip();
            try { RestoreHistory(false); if (modeTabs.Length > 0) Enter(false); }
            finally { quietProblems = false; }
            ShowDefAtTopLeft();
        }
        // refreshText: an undo in the Python tab shows the restored code (LoadHistory leaves that to Enter and the record).
        void RestoreHistory(bool refreshText = true)
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
                // In the Python tab an undo shows the restored commit as text (undo goes commit by commit).
                if (refreshText && Mode == CodingMode.Python && pythonInput)
                {
                    if (program.LooseRoots().Any()) LoadSilently(History.commit);
                    ShowCommitText(); ReadPython();
                }
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
            source = BoardMode ? PythonTreeCompiler.CompileBoard(blocks, out _) : PythonTreeCompiler.Compile(functionName.text, blocks, Shared?.Invoke());
            pythonPreview.text = source;
            return blocks;
        }
        void Publish(IReadOnlyList<CodeBlock> blocks, string source)
        {
            string appliedMessage = ApplyToTarget?.Invoke(functionName.text, blocks);
            AppliedSource = source; IsApplied = true;
            // 에러 (CodeLint, the stale enemy loop among them) runs the robot all the same; the line says so.
            var warning = problems.FirstOrDefault(p => !p.error);
            ShowMessage((appliedMessage ?? "자동 적용됨") + (warning.statement != null ? " · " + Counts(0, WarningCount) + LineOf(warning.statement) + warning.message : ""));
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
            quietProblems = true; HideTip(); // a message of the code that was there goes with it
            try { Changed(); if (modeTabs.Length > 0) Enter(false); }
            finally { quietProblems = false; }
            ShowDefAtTopLeft();
        }
        internal CommandBlockView CreateBlock(CodeBlock data, Transform parent)
        {
            if (data.Kind == BlockKind.Attack) data = new CodeBlock(BlockKind.Shot, data.Value, data.Arguments.ToArray());
            // is_dead() and type are a variable's members only (typed after `enemy.`), like distance( and hp.
            bool member = data.Kind is BlockKind.IsDead or BlockKind.EnemyTypeOf or BlockKind.Shield or BlockKind.Front or BlockKind.Top or BlockKind.Size or BlockKind.Reflecting or BlockKind.Left or BlockKind.Right or
                BlockKind.WeakPoint or BlockKind.Role or BlockKind.Pop;
            if (member && data.Arguments.FirstOrDefault()?.Kind != BlockKind.Variable)
                throw new FormatException(PythonTreeCompiler.Label(data.Kind) + "는 변수 뒤에 점을 찍어 씁니다. 예: enemy." + PythonTreeCompiler.Label(data.Kind));
            bool variable = member || data.Kind == BlockKind.Variable || data.Kind == BlockKind.Distance || data.Kind == BlockKind.Hp;
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
                // As large as the block it lifts (the code area may be zoomed); a palette block's copy stays the palette's size.
                copy.Rect.localScale *= block.Rect.lossyScale.x / dragLayer.lossyScale.x;
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
        public void RefreshFunctionNameSize()
        {
            if (!functionName || !functionName.textComponent) return;
            var title = functionName.transform.parent as RectTransform;
            var layout = functionName.GetComponent<LayoutElement>();
            if (!title || !layout) return;
            float reserved = 0;
            var group = title.GetComponent<HorizontalLayoutGroup>();
            int siblings = 0;
            foreach (RectTransform child in title)
            {
                if (child == functionName.transform || !child.gameObject.activeSelf) continue;
                var siblingLayout = child.GetComponent<LayoutElement>();
                if (siblingLayout && siblingLayout.ignoreLayout) continue;
                reserved += LayoutUtility.GetPreferredWidth(child);
                siblings++;
            }
            if (group) reserved += group.padding.horizontal + siblings * group.spacing;
            float available = Mathf.Max(64, title.rect.width - reserved);
            float width = Mathf.Clamp(Mathf.Ceil(functionName.textComponent.GetPreferredValues(functionName.text).x + 36), 64, available);
            layout.minWidth = 64;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;
            sizedFunctionName = functionName.text;
            sizedTitleWidth = title.rect.width;
            LayoutRebuilder.MarkLayoutForRebuild(title);
        }
        void LateUpdate()
        {
            if (functionName && (sizedFunctionName != functionName.text ||
                ((RectTransform)functionName.transform.parent).rect.width != sizedTitleWidth)) RefreshFunctionNameSize();
            if (Dragged && dragEvent != null) UpdateSnapPreview(dragEvent);
            if (defBlock) LayoutDef();
        }
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
                if ((!pointerOver && !bounds.Overlaps(area)) || !zone.Preview(Dragged, e, out _)) continue;
                float overlap = Mathf.Max(0, Mathf.Min(bounds.xMax, area.xMax) - Mathf.Max(bounds.xMin, area.xMin)) *
                    Mathf.Max(0, Mathf.Min(bounds.yMax, area.yMax) - Mathf.Max(bounds.yMin, area.yMin));
                overlap /= Mathf.Max(1, Mathf.Min(bounds.width * bounds.height, area.width * area.height));
                float distance = (bounds.center - area.center).sqrMagnitude;
                if (!target || socket && !targetIsSocket || socket == targetIsSocket &&
                    (socket ? overlap > targetOverlap || Mathf.Approximately(overlap, targetOverlap) && distance < targetDistance :
                        zone.Rect.IsChildOf(target.Rect)))
                { target = zone; targetIsSocket = socket; targetOverlap = overlap; targetDistance = distance; }
            }
            if (!target) { ShowLanding(null, default); return; }
            snapTarget = target;
            // Room opens where it will land and a see-through copy sits in it: between lines for a line, the empty slot grown to
            // the block's size for a value or condition (play feedback: round blocks showed no snap preview). The dragged blocks
            // stay under the hand.
            var at = target.OpenGap(Dragged, e, LandingHeight());
            if (gapZone && gapZone != target) gapZone.CloseGap();
            gapZone = target;
            LayoutRebuilder.ForceRebuildLayoutImmediate(program.Rect);
            ShowLanding(target, at);
        }
        float LandingHeight()
        {
            float height = 0;
            // The landing copies, placed blocks: a palette C block is one line, its copy an open C.
            for (int i = 0; i < landing.Count; i++)
            {
                height += landing[i].preferredHeight;
                if (i > 0 && PythonTreeCompiler.IsBranch(landing[i].Kind)) height -= landing[i - 1].FooterOverlap;
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
            Copied?.Invoke();
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
        void OnDisable() { EndDrag(); CloseMenu(); CloseCompletion(); HideTip(); }

        // ---- The three tabs (Docs/코딩_모드_3탭.md) ----
        // The tab last chosen, kept while the editor closes and opens on another robot.
        static CodingMode chosenMode = CodingMode.Block;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMode() => chosenMode = CodingMode.Block;
        public CodingMode Mode { get; private set; } = CodingMode.Block;
        // 에러 for an action block on the robot being edited (the host asks Rules.Compatibility); null when it works.
        public Func<BlockKind, string> ActionWarning { get; set; }
        readonly List<(CodeBlock statement, string message, bool error)> problems = new List<(CodeBlock, string, bool)>();
        public IReadOnlyList<(CodeBlock statement, string message, bool error)> Problems => problems;
        int ErrorCount => problems.Count(p => p.error) + (Mode == CodingMode.Python && SyntaxError != null ? 1 : 0);
        int WarningCount => problems.Count(p => !p.error);
        static string Counts(int errors, int warnings) => "오류 " + errors + " · 에러 " + warnings + " · ";
        string LineOf(CodeBlock statement) => Mode == CodingMode.Python && statement != null && pythonLines.TryGetValue(statement, out int line) ? line + "번째 줄: " : "";
        string ProblemText(FormatException error)
        {
            var first = problems.FirstOrDefault(p => p.error);
            return first.statement != null ? LineOf(first.statement) + first.message : error.Message;
        }

        // Every edit in a block tab commits the start chain (errors in meaning included); an edit that leaves it as it was
        // (a loose block moved) does not.
        void UpdateCommit()
        {
            var tree = program.StartChain().Select(b => b.Read()).ToList();
            if (SameBlocks(tree, History.commit)) return;
            History.commit = tree;
            if (!loading) History.CommitNumber++;
        }
        // 오류 (the first compile error, on its line) or 에러 (CodeLint) for the commit, shown on the blocks and the Python lines.
        void Diagnose()
        {
            problems.Clear();
            try { PythonTreeCompiler.Compile(functionName.text, History.commit); }
            catch (CodeError error) { problems.Add((error.Statement, error.Message, true)); }
            catch (FormatException) { } // the function name: the status line says it
            // 에러 shows beside an 오류 on another line (it reads the lines, not the compiled code); on the same line red wins.
            foreach (var (statement, message) in CodeLint.Check(History.commit, ActionWarning)) problems.Add((statement, message, false));
            foreach (var block in program.AllBlocks()) block.SetProblem(null, false);
            MarkBlocks(History.commit, program.StartChain());
            ShowSquiggles();
            PopNewProblem();
        }
        void MarkBlocks(IReadOnlyList<CodeBlock> data, IReadOnlyList<CommandBlockView> views)
        {
            for (int i = 0; i < Math.Min(data.Count, views.Count); i++)
            {
                if (data[i] == null) continue;
                var found = problems.Where(p => p.statement == data[i]).OrderByDescending(p => p.error).ToList();
                if (found.Count > 0) views[i].SetProblem(found[0].message, found[0].error); // red wins
                if (views[i].Body) MarkBlocks(data[i].Body, views[i].Body.Blocks);
            }
        }

        Button[] modeTabs = Array.Empty<Button>();
        static readonly string[] ModeNames = { "자연어", "블록", "파이썬" };
        RectTransform workspace;
        CanvasGroup library;
        // The tabs right of `def name():`, in the look of the header's buttons (look: 비우기).
        public void AddModeTabs(Button look)
        {
            Initialize();
            if (modeTabs.Length > 0 || !look) return;
            var titleBar = (RectTransform)functionName.transform.parent;
            var colon = titleBar.Cast<Transform>().FirstOrDefault(t => t.name == "Colon");
            int at = colon ? colon.GetSiblingIndex() + 1 : titleBar.childCount;
            modeTabs = new Button[ModeNames.Length];
            for (int i = 0; i < ModeNames.Length; i++)
            {
                var tab = Instantiate(look, titleBar);
                tab.name = "Tab" + (CodingMode)i;
                tab.onClick.RemoveAllListeners();
                foreach (Transform child in tab.transform) if (!child.GetComponent<TMP_Text>()) child.gameObject.SetActive(false);
                var size = tab.GetComponent<LayoutElement>();
                if (!size) size = tab.gameObject.AddComponent<LayoutElement>();
                ((RectTransform)tab.transform).sizeDelta = new Vector2(78, 34);
                size.minWidth = size.preferredWidth = 78; size.minHeight = size.preferredHeight = 34; size.flexibleWidth = 0;
                var label = tab.GetComponentInChildren<TMP_Text>(true);
                label.text = ModeNames[i];
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.alignment = TextAlignmentOptions.Center;
                tab.transform.SetSiblingIndex(at + i);
                var mode = (CodingMode)i;
                tab.onClick.AddListener(() => SetMode(mode));
                modeTabs[i] = tab;
            }
            // A little room between `():` and the first tab.
            if (titleBar.TryGetComponent<HorizontalLayoutGroup>(out var row)) row.spacing = Mathf.Max(row.spacing, 8);
            workspace = (RectTransform)program.GetComponentInParent<ScrollRect>(true).transform;
            var paletteContent = palette.Length > 0 && palette[0].view ? palette[0].view.transform.parent : null;
            var libraryRoot = paletteContent ? paletteContent.GetComponentInParent<ScrollRect>(true) : null;
            if (libraryRoot && !libraryRoot.TryGetComponent(out library)) library = libraryRoot.gameObject.AddComponent<CanvasGroup>();
            BuildPythonInput();
            Mode = chosenMode;
            ShowTabs();
            Enter(false);
        }
        public void SetMode(CodingMode mode)
        {
            if (mode == Mode) return;
            Initialize();
            EndDrag(); CloseMenu(); CloseCompletion(); HideTip();
            // Leaving: a block tab keeps its layout (loose chains too); the Python tab keeps its text as typed (RecordPython).
            if (Mode == CodingMode.Python) { if (pythonDirty) ReadPython(); RecordPython(); }
            else { History.blockRecord = program.CaptureDraft(); History.blockRecordCommit = History.CommitNumber; }
            bool fromPython = Mode == CodingMode.Python;
            Mode = chosenMode = mode;
            ShowTabs();
            Enter(fromPython);
        }
        // The tab the next LoadProgram/LoadHistory opens on, without leaving the current one (the editor is switching robots).
        public void UseMode(CodingMode mode) { CloseCompletion(); Mode = chosenMode = mode; ShowTabs(); }
        public void FlushPython() { if (Mode == CodingMode.Python && pythonDirty) ReadPython(); CloseCompletion(); HideTip(); }
        void ShowTabs()
        {
            for (int i = 0; i < modeTabs.Length; i++)
            {
                bool chosen = (int)Mode == i;
                if (modeTabs[i].targetGraphic) modeTabs[i].targetGraphic.color = chosen ? UITheme.Sky : new Color(.08f, .15f, .21f);
                var label = modeTabs[i].GetComponentInChildren<TMP_Text>(true);
                if (label) label.color = chosen ? UITheme.Ink : new Color(.85f, .94f, 1);
            }
        }
        // Entering a tab: its record as left when the commit is still the one it was left at, else rebuilt from the commit
        // (a syntax error or a loose block never travels to another tab).
        void Enter(bool fromPython)
        {
            bool python = Mode == CodingMode.Python;
            if (workspace) workspace.gameObject.SetActive(!python);
            if (pythonInput) pythonInput.gameObject.SetActive(python);
            if (library) { library.alpha = python ? .4f : 1; library.interactable = library.blocksRaycasts = !python; }
            if (python)
            {
                bool kept = History.pythonRecord != null && History.pythonRecordCommit == History.CommitNumber;
                // The hidden blocks hold the commit, so the robot and the stock follow the code being typed.
                if (program.LooseRoots().Any()) LoadSilently(History.commit);
                if (kept) ShowPython(History.pythonRecord, History.pythonCaret);
                else ShowCommitText();
                Changed();
                ReadPython(); // its underline and status line
                return;
            }
            if (fromPython && History.blockRecord != null && History.blockRecordCommit == History.CommitNumber)
            {
                updating = true;
                try { program.RestoreDraft(History.blockRecord); }
                finally { updating = false; }
            }
            foreach (var block in program.AllBlocks().Concat(palette.Select(p => p.view)).Append(variablePrefab)) block.ShowNatural(Mode == CodingMode.Natural);
            LayoutRebuilder.MarkLayoutForRebuild(program.Rect);
            Changed();
            if (fromPython && SyntaxError != null)
                ShowMessage("파이썬 탭에 문법 오류가 있어 마지막 정상 코드로 열었어요 (" + SyntaxError.Text + ")");
        }
        void LoadSilently(IReadOnlyList<CodeBlock> blocks)
        {
            updating = true;
            try { selected = program; program.Load(blocks); }
            finally { updating = false; }
        }

        // ---- The Python tab ----
        TMP_InputField pythonInput;
        CodeSquiggles squiggles;
        Dictionary<CodeBlock, int> pythonLines = new Dictionary<CodeBlock, int>();
        public PythonSyntaxError SyntaxError { get; private set; }
        public string PythonText => pythonInput ? pythonInput.text : null;
        public bool PythonFocused => pythonInput && pythonInput.isFocused;
        bool pythonDirty; float pythonEdited;
        const float ReadDelay = .5f;
        void BuildPythonInput()
        {
            var resources = new TMP_DefaultControls.Resources();
            var root = TMP_DefaultControls.CreateInputField(resources);
            root.name = "PythonInput";
            var box = (RectTransform)root.transform;
            box.SetParent(workspace.parent, false);
            box.SetSiblingIndex(workspace.GetSiblingIndex() + 1);
            box.anchorMin = workspace.anchorMin; box.anchorMax = workspace.anchorMax; box.pivot = workspace.pivot;
            box.anchoredPosition = workspace.anchoredPosition; box.sizeDelta = workspace.sizeDelta;
            // VS Code Dark+: #1E1E1E page, #D4D4D4 text, a #858585 line-number gutter, white caret.
            var back = root.GetComponent<Image>();
            back.sprite = UITheme.Card; back.type = Image.Type.Sliced; back.color = Dark(0x1E1E1E);
            pythonInput = root.GetComponent<TMP_InputField>();
            pythonInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            pythonInput.richText = false;
            pythonInput.restoreOriginalTextOnEscape = false;
            pythonInput.resetOnDeActivation = false;
            pythonInput.caretWidth = 2; pythonInput.customCaretColor = true; pythonInput.caretColor = Color.white;
            pythonInput.selectionColor = Dark(0x264F78);
            var area = pythonInput.textViewport;
            area.offsetMin = new Vector2(8, 16); area.offsetMax = new Vector2(-22, -16);
            foreach (var text in new[] { pythonInput.textComponent, pythonInput.placeholder as TMP_Text })
            {
                if (!text) continue;
                text.font = pythonPreview.font; text.fontSize = 21; text.alignment = TextAlignmentOptions.TopLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap; text.lineSpacing = 18; text.color = Dark(0xD4D4D4);
                text.margin = new Vector4(Gutter, 0, 0, 0);
            }
            if (pythonInput.placeholder is TMP_Text hint) { hint.text = "여기에 파이썬 코드를 입력하세요. 두 글자를 치거나 Ctrl+Space를 누르면 자동 완성이 떠요."; hint.color = Dark(0x6A6A6A); hint.fontStyle = FontStyles.Normal; }
            pythonInput.onValueChanged.AddListener(_ => PythonEdited());
            pythonInput.onValidateInput = ValidatePython;
            // The field's own glyphs are clear (it still places the caret and selection by them); the coloured copy, the
            // current line's band, the line numbers and the underlines are its children, so they scroll with it.
            var field = pythonInput.textComponent;
            field.color = Color.clear;
            RectTransform Layer(GameObject layer)
            {
                var r = (RectTransform)layer.transform; r.SetParent(field.rectTransform, false);
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; r.pivot = field.rectTransform.pivot;
                return r;
            }
            currentLine = new GameObject("CurrentLine", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Layer(currentLine.gameObject); currentLine.color = Dark(0x2A2D2E); currentLine.raycastTarget = false;
            TMP_Text Copy(string name, TextAlignmentOptions alignment)
            {
                var copy = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                Layer(copy.gameObject);
                copy.font = field.font; copy.fontSize = field.fontSize; copy.lineSpacing = field.lineSpacing; copy.alignment = alignment;
                copy.textWrappingMode = TextWrappingModes.NoWrap; copy.richText = true; copy.raycastTarget = false; copy.margin = field.margin;
                return copy;
            }
            coloured = Copy("Coloured", TextAlignmentOptions.TopLeft); coloured.color = Dark(0xD4D4D4);
            lineNumbers = Copy("LineNumbers", TextAlignmentOptions.TopRight); lineNumbers.color = Dark(0x858585);
            lineNumbers.margin = Vector4.zero; lineNumbers.rectTransform.anchorMax = new Vector2(0, 1); lineNumbers.rectTransform.sizeDelta = new Vector2(Gutter - 14, 0); lineNumbers.rectTransform.pivot = new Vector2(0, lineNumbers.rectTransform.pivot.y);
            squiggles = new GameObject("Squiggles", typeof(RectTransform), typeof(CodeSquiggles)).GetComponent<CodeSquiggles>();
            Layer(squiggles.gameObject);
            squiggles.raycastTarget = false;
            root.SetActive(false);
        }
        const float Gutter = 58;
        Image currentLine;
        TMP_Text coloured, lineNumbers;
        readonly List<Button> gutterMarks = new List<Button>();
        static Color Dark(int rgb) => UITheme.Hex(rgb);
        // The coloured copy, line numbers, current line band and gutter ! marks for the text as it is now.
        void PaintPython()
        {
            if (!pythonInput) return;
            string text = pythonInput.text;
            coloured.text = PythonTreeParser.Colorize(text);
            int count = text.Count(c => c == '\n') + 1;
            lineNumbers.text = string.Join("\n", Enumerable.Range(1, count));
            PlaceCurrentLine();
            PlaceGutterMarks();
        }
        int paintedCaret = -1;
        void PlaceCurrentLine()
        {
            var field = pythonInput.textComponent;
            field.ForceMeshUpdate();
            var info = field.textInfo;
            int line = pythonInput.text.Substring(0, Mathf.Clamp(pythonInput.caretPosition, 0, pythonInput.text.Length)).Count(c => c == '\n');
            paintedCaret = pythonInput.caretPosition;
            currentLine.enabled = pythonInput.isFocused && line < info.lineCount;
            if (!currentLine.enabled) return;
            var row = info.lineInfo[line]; var r = field.rectTransform.rect;
            var band = currentLine.rectTransform; band.anchorMin = new Vector2(0, 1); band.anchorMax = Vector2.one;
            band.offsetMin = new Vector2(0, row.descender - r.yMax); band.offsetMax = new Vector2(0, row.ascender - r.yMax);
        }
        // A ! in the gutter on each line with a problem; clicked, it shows the message again.
        void PlaceGutterMarks()
        {
            var lines = Squiggles.Select(s => s.line).Distinct().ToList();
            var field = pythonInput.textComponent; var info = field.textInfo; var r = field.rectTransform.rect;
            for (int i = 0; i < Math.Max(lines.Count, gutterMarks.Count); i++)
            {
                if (i >= gutterMarks.Count)
                {
                    var mark = new GameObject("GutterMark", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                    var box = (RectTransform)mark.transform; box.SetParent(field.rectTransform, false);
                    box.anchorMin = box.anchorMax = new Vector2(0, 1); box.pivot = new Vector2(.5f, .5f); box.sizeDelta = new Vector2(20, 20);
                    mark.targetGraphic = mark.GetComponent<Image>(); ((Image)mark.targetGraphic).sprite = UITheme.Circle;
                    var label = new GameObject("Mark", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    label.rectTransform.SetParent(box, false); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                    if (UITheme.Bold) label.font = UITheme.Bold;
                    label.text = "!"; label.fontSize = 15; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                    gutterMarks.Add(mark);
                }
                var button = gutterMarks[i];
                bool shown = i < lines.Count && lines[i] - 1 < info.lineCount;
                button.gameObject.SetActive(shown);
                if (!shown) continue;
                int line = lines[i];
                var row = info.lineInfo[line - 1];
                bool error = Squiggles.Any(s => s.line == line && s.color == CommandBlockView.ErrorColor);
                button.targetGraphic.color = error ? CommandBlockView.ErrorColor : CommandBlockView.WarningColor;
                button.GetComponentInChildren<TMP_Text>().color = error ? Color.white : UITheme.Ink;
                ((RectTransform)button.transform).anchoredPosition = new Vector2(12, (row.ascender + row.descender) * .5f - r.yMax);
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => ReshowLine(line));
            }
        }
        public IReadOnlyList<Button> GutterMarks => gutterMarks.Where(b => b.gameObject.activeSelf).ToList();
        void ShowCommitText()
        {
            pythonLines = new Dictionary<CodeBlock, int>();
            ShowPython(PythonTreeCompiler.Print(History.commit, pythonLines), 0);
            SyntaxError = null;
        }
        void ShowPython(string text, int caret)
        {
            pythonInput.SetTextWithoutNotify(text);
            pythonInput.caretPosition = pythonInput.selectionAnchorPosition = Mathf.Clamp(caret, 0, text.Length);
            pythonDirty = false; PaintPython();
            RecordPython();
        }
        void RecordPython()
        {
            if (!pythonInput) return;
            History.pythonRecord = pythonInput.text; History.pythonCaret = pythonInput.caretPosition; History.pythonRecordCommit = History.CommitNumber;
        }
        void PythonEdited()
        {
            pythonDirty = true; pythonEdited = Time.unscaledTime;
            RecordPython();
            RefreshCompletion(false); PaintPython();
        }
        // Reads the text into blocks: committed when it parses (and differs), else the red underline on the bad line.
        public void ReadPython()
        {
            if (!pythonInput) return;
            pythonDirty = false;
            var lines = new Dictionary<CodeBlock, int>();
            try
            {
                var parsed = PythonTreeParser.Parse(pythonInput.text, kind => IsAvailable?.Invoke(kind) ?? true, lines);
                SyntaxError = null; pythonLines = lines;
                if (SameBlocks(parsed, History.commit)) { History.commit = parsed; Changed(); }
                else
                {
                    try { LoadSilently(parsed); }
                    catch (FormatException error) { throw new PythonSyntaxError(1, error.Message); }
                    History.commit = parsed; History.CommitNumber++;
                    Changed();
                }
            }
            catch (PythonSyntaxError error)
            {
                SyntaxError = error;
                ShowSquiggles(); PopNewProblem();
                ShowMessage("코드가 바뀌지 않음 · 로봇은 마지막 정상 코드로 움직여요 · " + Counts(1, 0) + error.Text);
                // A syntax error says nothing of meaning: the other underlines wait for the text to read again.
            }
            RecordPython();
        }
        void ShowSquiggles()
        {
            if (!squiggles) return;
            var marks = new List<(int line, Color color)>();
            if (SyntaxError != null) marks.Add((SyntaxError.Line, CommandBlockView.ErrorColor));
            else foreach (var problem in problems.OrderBy(p => p.error))
                    // 에러 in a bright yellow on the dark page.
                    if (problem.statement != null && pythonLines.TryGetValue(problem.statement, out int line)) marks.Add((line, problem.error ? CommandBlockView.ErrorColor : WarningLine));
            squiggles.Show(pythonInput.textComponent, marks); PlaceGutterMarks();
        }
        public static readonly Color WarningLine = new Color(1f, .8f, .15f);
        public IReadOnlyList<(int line, Color color)> Squiggles => squiggles ? squiggles.Marks : Array.Empty<(int, Color)>();
        // The message for a Python line (for the hover tip); null when the line is fine.
        public string LineProblem(int line)
        {
            if (SyntaxError != null) return SyntaxError.Line == line ? SyntaxError.Text : null;
            var found = problems.Where(p => p.statement != null && pythonLines.TryGetValue(p.statement, out int at) && at == line).OrderByDescending(p => p.error).ToList();
            return found.Count > 0 ? line + "번째 줄: " + found[0].message : null;
        }
        // For the checks: type text as a player would, and read it now instead of after the pause.
        public void TypePython(string text, int caret = -1)
        {
            pythonInput.text = text;
            pythonInput.caretPosition = pythonInput.selectionAnchorPosition = caret < 0 ? text.Length : caret;
            PythonEdited();
        }

        // Enter keeps the line's indent (one more after a colon), Tab puts four spaces, both take the completion when it is up;
        // Ctrl+Space opens it.
        char ValidatePython(string text, int index, char c)
        {
            if (c == ' ' && ControlHeld()) { RefreshCompletion(true); return '\0'; }
            if ((c == '\n' || c == '\t') && completion.Count > 0) { AcceptCompletion(); return '\0'; }
            if (c == '\t') { pendingInsert = "    "; return '\0'; }
            if (c == '\n')
            {
                int start = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
                if (index == 0) start = 0;
                string line = text.Substring(start, Math.Max(0, index - start));
                string indent = new string(' ', line.Length - line.TrimStart(' ').Length);
                if (line.TrimEnd().EndsWith(":")) indent += "    ";
                pendingInsert = indent;
            }
            return c;
        }
        string pendingInsert;
        static bool ControlHeld() => UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed;

        // ---- Completion list and parameter hint ----
        List<CodeCompletion.Item> completion = new List<CodeCompletion.Item>();
        int completionStart, completionIndex;
        RectTransform completionBox, hintBox;
        int savedCaret = -1;
        public IReadOnlyList<CodeCompletion.Item> CompletionItems => completion;
        public bool CompletionOpen => completion.Count > 0;
        // True on the frame Esc closed the list (the host's Esc then does not close the editor).
        public bool CompletionWasOpen { get; private set; }
        public string HintText { get; private set; }
        void RefreshCompletion(bool forced)
        {
            if (!pythonInput) return;
            string text = pythonInput.text; int caret = pythonInput.caretPosition;
            var items = CodeCompletion.Candidates(text, caret, IsAvailable, ActionWarning, out completionStart);
            bool member = completionStart > 0 && text[completionStart - 1] == '.';
            completion = forced || member || caret - completionStart >= 2 ? items.Take(8).ToList() : new List<CodeCompletion.Item>();
            completionIndex = 0;
            HintText = CodeCompletion.Hint(text, caret);
            DrawCompletion();
        }
        public void CloseCompletion() { completion.Clear(); HintText = null; DrawCompletion(); }
        public void AcceptCompletion()
        {
            if (completion.Count == 0) return;
            var item = completion[completionIndex];
            string text = pythonInput.text; int caret = pythonInput.caretPosition;
            string next = text.Substring(0, completionStart) + item.Text + text.Substring(caret);
            completion.Clear();
            pythonInput.text = next;
            pythonInput.caretPosition = pythonInput.selectionAnchorPosition = completionStart + item.Text.Length;
            savedCaret = pythonInput.caretPosition;
            PythonEdited();
            RefreshCompletion(false);
            if (item.Text.EndsWith(".")) RefreshCompletion(true);
        }
        public void MoveCompletion(int step) { if (completion.Count > 0) { completionIndex = (completionIndex + step + completion.Count) % completion.Count; DrawCompletion(); } }
        void DrawCompletion()
        {
            if (!pythonInput) return;
            if (!completionBox) completionBox = Popup("Completion");
            if (!hintBox) hintBox = Popup("ParameterHint");
            foreach (Transform row in completionBox) Destroy(row.gameObject);
            completionBox.gameObject.SetActive(completion.Count > 0);
            hintBox.gameObject.SetActive(completion.Count == 0 && HintText != null);
            // The list starts under the word being completed; the hint under the caret.
            Vector2 at = CaretPoint(completion.Count > 0 ? completionStart : pythonInput.caretPosition);
            for (int i = 0; i < completion.Count; i++)
            {
                var item = completion[i];
                bool chosen = i == completionIndex;
                var row = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                row.transform.SetParent(completionBox, false);
                row.GetComponent<Image>().color = chosen ? Dark(0x04395E) : Color.clear;
                row.GetComponent<LayoutElement>().preferredHeight = 34;
                // ! for ⚠: the code font has no warning sign.
                var label = PopupText(row.transform, (item.Warning != null ? "<color=#FFC81A>!</color> " : "") + item.Text + "   <size=80%><color=#9D9D9D>" +
                    item.Label + (item.Warning != null ? " · " + item.Warning : "") + "</color></size>");
                label.color = item.Warning != null ? Dark(0x808080) : Dark(0xD4D4D4);
            }
            completionBox.anchoredPosition = at;
            if (hintBox.gameObject.activeSelf)
            {
                foreach (Transform row in hintBox) Destroy(row.gameObject);
                var line = new GameObject("Hint", typeof(RectTransform), typeof(LayoutElement));
                line.transform.SetParent(hintBox, false); line.GetComponent<LayoutElement>().preferredHeight = 32;
                int words = HintText.IndexOf("  ", StringComparison.Ordinal);
                PopupText(line.transform, words < 0 ? HintText : HintText.Substring(0, words) + "<color=#9D9D9D>" + HintText.Substring(words) + "</color>").color = Dark(0xD4D4D4);
                hintBox.anchoredPosition = at;
            }
        }
        RectTransform Popup(string name)
        {
            var box = new GameObject(name, typeof(RectTransform), typeof(CommandProblemFrame), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            box.SetParent(pythonInput.transform, false);
            box.anchorMin = box.anchorMax = new Vector2(0, 1); box.pivot = new Vector2(0, 1);
            // VS Code's suggest widget: rounded, #252526 with a #454545 edge.
            var frame = box.GetComponent<CommandProblemFrame>(); frame.Width = 1; frame.Radius = 8; frame.color = Dark(0x454545); frame.Fill = Dark(0x252526); frame.raycastTarget = false;
            var layout = box.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(8, 8, 6, 6); layout.spacing = 2;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fit = box.GetComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            box.gameObject.SetActive(false);
            return box;
        }
        TMP_Text PopupText(Transform parent, string text)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10, 0); label.rectTransform.offsetMax = new Vector2(-10, 0);
            label.GetComponent<LayoutElement>().ignoreLayout = true;
            label.font = pythonPreview.font; label.fontSize = 19; label.richText = true; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.alignment = TextAlignmentOptions.MidlineLeft; label.text = text;
            parent.GetComponent<LayoutElement>().preferredWidth = Mathf.Ceil(label.GetPreferredValues(text).x + 24);
            return label;
        }
        // Just under the caret, in the input's top-left space.
        Vector2 CaretPoint(int index)
        {
            var text = pythonInput.textComponent;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            int caret = Mathf.Clamp(index, 0, Math.Max(0, info.characterCount));
            Vector3 local;
            if (info.characterCount == 0) local = new Vector3(text.rectTransform.rect.xMin, text.rectTransform.rect.yMax - text.fontSize * 1.3f);
            else if (caret > 0) { var ch = info.characterInfo[caret - 1]; local = ch.character == '\n' ? new Vector3(text.rectTransform.rect.xMin, ch.descender - info.lineInfo[0].lineHeight) : new Vector3(ch.xAdvance, ch.descender); }
            else { var ch = info.characterInfo[0]; local = new Vector3(ch.origin, ch.descender); }
            var box = (RectTransform)pythonInput.transform;
            Vector2 point = box.InverseTransformPoint(text.rectTransform.TransformPoint(local));
            return point - new Vector2(box.rect.xMin, box.rect.yMax) + new Vector2(-6, -4);
        }

        // ---- Hover messages (blocks and Python lines) ----
        readonly HashSet<CommandBlockView> hovered = new HashSet<CommandBlockView>();
        RectTransform tip;
        internal void HoverProblem(CommandBlockView view, bool enter)
        {
            if (enter) hovered.Add(view); else hovered.Remove(view);
            hovered.RemoveWhere(v => !v);
            // The innermost line with a problem under the pointer.
            var line = hovered.Where(v => v.Problem != null).OrderByDescending(v => v.transform.GetComponentsInParent<CommandBlockView>().Length).FirstOrDefault();
            if (line) { StopBubble(); ShowTip(line.Problem, BesideLine(line), line.ProblemIsError); }
            else if (bubble == null) ShowTip(null, default);
        }
        // Right of the line's header, at its middle height, so a taller bubble spreads evenly instead of over the next line.
        static Vector3 BesideLine(CommandBlockView line) => line.Rect.TransformPoint(new Vector3(line.Rect.rect.xMin + LineWidth(line) + 12, line.Rect.rect.yMax - line.HeaderHeight / 2));
        static float LineWidth(CommandBlockView line) => line.Body ? LayoutUtility.GetPreferredWidth((RectTransform)line.Rect.GetChild(0)) + 28 : line.Rect.rect.width;
        // Right of a Python line's last character.
        Vector3 BesideLine(int line)
        {
            var field = pythonInput.textComponent; field.ForceMeshUpdate();
            var info = field.textInfo;
            if (line < 1 || line > info.lineCount) return field.rectTransform.TransformPoint(field.rectTransform.rect.center);
            var row = info.lineInfo[line - 1];
            float x = row.lastVisibleCharacterIndex >= row.firstCharacterIndex && row.lastVisibleCharacterIndex < info.characterCount
                ? info.characterInfo[row.lastVisibleCharacterIndex].xAdvance : field.rectTransform.rect.xMin + Gutter;
            return field.rectTransform.TransformPoint(new Vector3(x + 18, (row.ascender + row.descender) / 2));
        }

        // ---- Message bubbles: a new 오류 or 에러 (and a click on its !) shows its message blinking, for 1.5 s, then fading ----
        Coroutine bubble;
        public string BubbleText { get; private set; }
        public int BubbleBlinks { get; private set; }
        HashSet<string> seenProblems = new HashSet<string>();
        bool quietProblems; // a robot's code just opened: its problems are not new
        void StopBubble()
        {
            if (bubble != null) StopCoroutine(bubble);
            bubble = null; BubbleText = null;
            if (tip && tip.TryGetComponent<CanvasGroup>(out var group)) group.alpha = 1;
        }
        void HideTip() { StopBubble(); ShowTip(null, default); }
        void Bubble(string text, Vector3 world, bool error)
        {
            StopBubble();
            ShowTip(text, world, error);
            if (!isActiveAndEnabled) return;
            BubbleText = text; BubbleBlinks = 0;
            bubble = StartCoroutine(Blink());
        }
        public const float BubbleHold = 1.5f, BubbleFade = .4f;
        System.Collections.IEnumerator Blink()
        {
            if (!tip.TryGetComponent<CanvasGroup>(out var group)) group = tip.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            for (int i = 0; i < 3; i++)
            {
                group.alpha = .15f; yield return new WaitForSecondsRealtime(.09f);
                group.alpha = 1; BubbleBlinks++; yield return new WaitForSecondsRealtime(.12f);
            }
            yield return new WaitForSecondsRealtime(BubbleHold);
            for (float start = Time.unscaledTime; Time.unscaledTime - start < BubbleFade;) { group.alpha = 1 - (Time.unscaledTime - start) / BubbleFade; yield return null; }
            group.alpha = 1; bubble = null; BubbleText = null;
            ShowTip(null, default);
        }
        // Lines by their place in the commit ("0", "2/1"): the same in every tab, so a tab switch shows nothing new.
        Dictionary<CodeBlock, string> Places()
        {
            var places = new Dictionary<CodeBlock, string>();
            void Walk(IReadOnlyList<CodeBlock> list, string prefix)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) { places[list[i]] = prefix + i; Walk(list[i].Body, prefix + i + "/"); }
            }
            Walk(History.commit, "");
            return places;
        }
        CommandBlockView ViewAt(string place)
        {
            IReadOnlyList<CommandBlockView> list = program.StartChain();
            CommandBlockView view = null;
            foreach (var step in place.Split('/'))
            {
                int i = int.Parse(step);
                if (list == null || i >= list.Count) return null;
                view = list[i]; list = view.Body ? view.Body.Blocks : null;
            }
            return view;
        }
        // After each read of the code: the first problem not on screen before pops up (오류 first).
        void PopNewProblem()
        {
            var places = Places();
            var current = new List<(string key, string text, bool error, Func<Vector3?> where)>();
            bool python = Mode == CodingMode.Python && pythonInput;
            if (python && SyntaxError != null)
            {
                var syntax = SyntaxError;
                current.Add(("syntax " + syntax.Line + " " + syntax.Message, syntax.Text, true, () => BesideLine(syntax.Line)));
            }
            else foreach (var problem in problems.OrderByDescending(p => p.error))
                {
                    if (problem.statement == null || !places.TryGetValue(problem.statement, out var place)) continue;
                    var statement = problem.statement;
                    current.Add((place + " " + problem.message, python && pythonLines.TryGetValue(statement, out int at) ? at + "번째 줄: " + problem.message : problem.message, problem.error,
                        () => python ? (pythonLines.TryGetValue(statement, out int line) ? BesideLine(line) : (Vector3?)null) : ViewAt(place) is CommandBlockView view ? BesideLine(view) : (Vector3?)null));
                }
            var fresh = current.FirstOrDefault(c => !seenProblems.Contains(c.key));
            seenProblems = new HashSet<string>(current.Select(c => c.key));
            if (fresh.key == null || quietProblems || loading || restoringHistory) return;
            Canvas.ForceUpdateCanvases();
            if (fresh.where() is Vector3 world) Bubble(fresh.text, world, fresh.error);
        }
        // The line block's ! clicked.
        public void ReshowProblem(CommandBlockView view) { if (view && view.Problem != null) Bubble(view.Problem, BesideLine(view), view.ProblemIsError); }
        // The Python gutter's ! clicked.
        public void ReshowLine(int line)
        {
            string message = LineProblem(line);
            if (message != null) Bubble(message, BesideLine(line), SyntaxError != null || problems.Any(p => p.error && message.EndsWith(p.message)));
        }
        string tipText;
        void ShowTip(string text, Vector3 world, bool error = true)
        {
            tipText = text;
            if (text == null) { if (tip) tip.gameObject.SetActive(false); return; }
            if (!tip)
            {
                var canvas = (RectTransform)definitionPanel.GetComponentInParent<Canvas>().rootCanvas.transform;
                tip = new GameObject("ProblemTip", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                tip.SetParent(canvas, false);
                tip.pivot = new Vector2(0, .5f);
                var image = tip.GetComponent<Image>(); image.sprite = UITheme.Card; image.type = Image.Type.Sliced; image.raycastTarget = false;
                var layout = tip.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(14, 14, 8, 15); // +7 at the bottom: ui_card ends in a 7 px shadow below its fill
                layout.childControlWidth = layout.childControlHeight = true; layout.childAlignment = TextAnchor.MiddleCenter;
                var fit = tip.GetComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(tip, false);
                if (UITheme.Bold) label.font = UITheme.Bold;
                label.fontSize = 18; label.raycastTarget = false;
                // Centred on the glyphs themselves: the line box's ascender/descender put Korean text low in the bubble.
                label.alignment = TextAlignmentOptions.CenterGeoAligned;
            }
            tip.GetComponent<Image>().color = error ? CommandBlockView.ErrorColor : CommandBlockView.WarningColor;
            var tipLabel = tip.GetComponentInChildren<TextMeshProUGUI>();
            tipLabel.text = text; tipLabel.color = error ? Color.white : UITheme.Ink;
            // As wide as its text up to the room left in the workspace (560 at most, 260 at least), then it wraps and grows taller.
            var canvasRect = (RectTransform)tip.parent;
            var corners = new Vector3[4]; (workspace ? workspace : definitionPanel).GetWorldCorners(corners);
            float right = canvasRect.InverseTransformPoint(corners[2]).x - 8;
            var at = canvasRect.InverseTransformPoint(world);
            tipLabel.textWrappingMode = TextWrappingModes.Normal;
            var element = tipLabel.GetComponent<LayoutElement>();
            element.preferredWidth = Mathf.Min(Mathf.Clamp(right - at.x - 28, 260, 560), tipLabel.GetPreferredValues(text).x + 2); element.preferredHeight = -1;
            tip.gameObject.SetActive(true); tip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tip);
            // Hug the drawn lines: wrapped lines are narrower than the cap, and TMP's line box pads one line but not several.
            tipLabel.ForceMeshUpdate();
            var drawn = tipLabel.GetRenderedValues(true);
            element.preferredWidth = Mathf.Min(element.preferredWidth, drawn.x + 4); element.preferredHeight = drawn.y + 12;
            LayoutRebuilder.ForceRebuildLayoutImmediate(tip);
            tip.localPosition = new Vector3(Mathf.Min(at.x, right - tip.rect.width), at.y, at.z);
        }
        public string TipText => tip && tip.gameObject.activeSelf ? tipText : null;

        string hoverMessage;
        void Update()
        {
            CompletionWasOpen = false;
            if (Mode != CodingMode.Python || !pythonInput || !pythonInput.gameObject.activeInHierarchy) return;
            if (pendingInsert != null)
            {
                string insert = pendingInsert; pendingInsert = null;
                int caret = pythonInput.caretPosition;
                pythonInput.text = pythonInput.text.Insert(caret, insert);
                pythonInput.caretPosition = pythonInput.selectionAnchorPosition = caret + insert.Length;
            }
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && completion.Count > 0)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { CloseCompletion(); CompletionWasOpen = true; }
                else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                {
                    MoveCompletion(keyboard.upArrowKey.wasPressedThisFrame ? -1 : 1);
                    // The field moved its caret a line up or down on the same key: put it back.
                    if (savedCaret >= 0) pythonInput.caretPosition = pythonInput.selectionAnchorPosition = savedCaret;
                }
            }
            if (pythonInput.isFocused && pythonInput.caretPosition != savedCaret && completion.Count == 0 && HintText != (CodeCompletion.Hint(pythonInput.text, pythonInput.caretPosition)))
            { HintText = CodeCompletion.Hint(pythonInput.text, pythonInput.caretPosition); DrawCompletion(); }
            savedCaret = pythonInput.caretPosition;
            if (paintedCaret != pythonInput.caretPosition || currentLine.enabled != pythonInput.isFocused) PlaceCurrentLine();
            if (pythonDirty && Time.unscaledTime - pythonEdited >= ReadDelay) ReadPython();
            // The message for the line under the pointer.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            var camera = pythonInput.GetComponentInParent<Canvas>().rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : pythonInput.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            Vector2 pointer = mouse.position.ReadValue();
            string message = null; Vector3 where = default;
            if (RectTransformUtility.RectangleContainsScreenPoint(pythonInput.textViewport, pointer, camera))
            {
                int line = TMP_TextUtilities.FindIntersectingLine(pythonInput.textComponent, pointer, camera);
                if (line >= 0 && (message = LineProblem(line + 1)) != null)
                {
                    var info = pythonInput.textComponent.textInfo.lineInfo[line];
                    where = pythonInput.textComponent.rectTransform.TransformPoint(new Vector3(pythonInput.textComponent.rectTransform.rect.xMin, info.descender - 6));
                }
            }
            if (message == hoverMessage) return;
            hoverMessage = message;
            if (message != null) { StopBubble(); ShowTip(message, where, SyntaxError != null || problems.Any(p => p.error && message.EndsWith(p.message))); }
            else if (bubble == null) ShowTip(null, default);
        }
    }

    public enum CodingMode { Natural, Block, Python }
}
