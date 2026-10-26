using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class DefenseCodeEditor : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] CommandCodingPanel panel;
        [SerializeField] GameObject editorRoot;
        [SerializeField] TMP_Text title, inventory;
        [SerializeField] Button closeButton, automaticButton;
        // Optional: SetHint is a no-op without it.
        [SerializeField, Tooltip("'비우기' 오른쪽 힌트 버튼. 튜토리얼이 힌트를 줄 때만 보입니다.")] Button hintButton;
        [SerializeField] GameObject hintPopup;
        [SerializeField] TMP_Text hintText;
        int editingId;
        DefenseSimulation editingSession;
        DefenseSimulation draftSession;
        readonly Dictionary<int, CommandCodingPanel.EditHistory> drafts = new Dictionary<int, CommandCodingPanel.EditHistory>();
        public bool IsOpen => editorRoot && editorRoot.activeSelf;
        public bool HintOpen => IsOpen && hintPopup && hintPopup.activeSelf;
        public int EditingId => editingId;
        public CommandCodingPanel Panel => panel;
        bool HasCurrentSession => editingSession != null && editingSession == battle.Simulation;
        void Start()
        {
            if (!battle || !panel || !editorRoot || !title || !inventory || !closeButton || !automaticButton)
                throw new InvalidOperationException("DefenseCodeEditor Inspector 참조를 연결하세요.");
            panel.ApplyToTarget = Apply;
            panel.BlockDropped += OnBlockDropped;
            panel.IsAvailable = kind => editingSession == null || editingSession.Options.unlockAll || editingSession.Available(kind, editingId) > 0;
            panel.Remaining = kind => editingSession == null ? 0 : Math.Max(0, editingSession.Available(kind, editingId) - panel.Program.AllBlocks().Count(b => DefenseProgression.Canonical(b.Kind) == DefenseProgression.Canonical(kind)));
            closeButton.onClick.AddListener(() => Close());
            automaticButton.onClick.AddListener(RestoreAutomatic);
            if (hintButton) hintButton.onClick.AddListener(ToggleHint);
            SetHint(null);
            inventory.text = "블록 인벤토리";
            editorRoot.SetActive(false);
        }
        public void Open(int id)
        {
            var sim = battle.Simulation;
            var robot = sim?.FindRobot(id);
            if (robot == null || !sim.CanEdit) return;
            if (IsOpen && editingId == id && editingSession == sim) return;
            Close(false); editingSession = sim; editingId = id;
            if (draftSession != sim) { drafts.Clear(); draftSession = sim; }
            foreach (int removed in drafts.Keys.Where(key => !sim.Robots.Exists(r => r.Id == key)).ToArray()) drafts.Remove(removed);
            title.text = "미리보기 · " + battle.RobotName(id);
            battle.BeginCodeView(robot);
            editorRoot.SetActive(true);
            if (drafts.TryGetValue(id, out var history)) panel.LoadHistory(history);
            else panel.LoadProgram(robot.Program?.Name ?? "robot_" + id, robot.Program?.CopyBlocks() ?? Array.Empty<CodeBlock>());
            panel.ShowCategory(1); panel.RefreshPalette();
            if (panel.IsApplied) panel.ShowMessage("자동 적용됨 · Ctrl+Z 실행 취소 · Ctrl+Y 다시 실행");
        }
        string Apply(string name, IReadOnlyList<CodeBlock> blocks)
        {
            if (!IsOpen || !HasCurrentSession) throw new FormatException("로봇을 다시 선택하세요.");
            editingSession.ApplyProgram(editingId, name, blocks); panel.RefreshPalette();
            battle.NotifyCodeApplied(editingId, blocks.Count == 0);
            return blocks.Count == 0 ? "코드 없음 · 행동하지 않음" : "자동 적용됨";
        }
        void OnBlockDropped(BlockKind kind, RectTransform source, RectTransform destination)
            => battle.ReportTutorialDrop(source, destination, "BlockDropped:" + DefenseProgression.Canonical(kind));
        public void Close(bool resume = true)
        {
            if (resume && !battle.TutorialAllows("Editor.Close")) return;
            bool wasOpen = IsOpen;
            if (IsOpen && HasCurrentSession && editingSession.Robots.Exists(r => r.Id == editingId))
                drafts[editingId] = panel.History;
            if (IsOpen) battle.EndCodeView();
            if (editorRoot) editorRoot.SetActive(false);
            editingId = 0; editingSession = null;
            if (resume && wasOpen) battle.ReportTutorialAction("Editor.Close", "TargetClicked", "EditorClosed");
        }
        // Code shown by the hint button; null or empty hides the button.
        public void SetHint(string code)
        {
            if (!hintButton) return;
            hintButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(code));
            if (hintText) hintText.text = code ?? "";
            if (hintPopup) hintPopup.SetActive(false);
        }
        void ToggleHint()
        {
            if (!battle.TutorialAllows(TutorialKeys.Hint) || !hintPopup) return;
            hintPopup.SetActive(!hintPopup.activeSelf);
            battle.ReportTutorialAction(TutorialKeys.Hint, TutorialSignals.TargetClicked);
        }
        // Empties a robot's code (blocks go back to the inventory); through the editor when it is open on that robot.
        public void ClearRobotProgram(int id)
        {
            drafts.Remove(id);
            if (IsOpen && editingId == id) { panel.ClearProgram(); panel.RefreshPalette(); return; }
            var sim = battle.Simulation; var robot = sim?.FindRobot(id);
            if (robot != null && sim.CanEdit) sim.ApplyProgram(id, robot.Program?.Name ?? "robot_" + id, Array.Empty<CodeBlock>());
        }

        public void PrepareDistanceExample()
        {
            if (!IsOpen || !HasCurrentSession) return;
            panel.LoadProgram(panel.FunctionName, new[] {
                new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)),
                new CodeBlock(BlockKind.DeclareVariable, "enemy_distance", new CodeBlock(BlockKind.Distance, "0",
                    new CodeBlock(BlockKind.Variable, "enemy"), new CodeBlock(BlockKind.MyPos)))
            });
            if (!panel.IsApplied) throw new InvalidOperationException("조건문 기초 코드의 블록 지급량을 확인하세요.");
        }
        public void PrepareBaselineExample()
        {
            if (!IsOpen || !HasCurrentSession) return;
            panel.LoadProgram(panel.FunctionName, new[] {
                new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)),
                new CodeBlock(BlockKind.Slash, "0", new CodeBlock(BlockKind.Variable, "enemy"))
            });
            if (!panel.IsApplied) throw new InvalidOperationException("비교용 기본 베기 코드의 블록 지급량을 확인하세요.");
            RestartPreview();
        }
        public void RestartPreview() => battle.RestartCodePreview();
        // Python text of condition/value blocks lying in the workspace away from the start anchor (not part of the program).
        public IEnumerable<string> LooseExpressions() => !IsOpen ? Enumerable.Empty<string>() : panel.Program.LooseRoots()
            .Where(root => PythonTreeCompiler.OutputSlot(root.Kind) != BlockSlotKind.Statement)
            .Select(root => PythonTreeCompiler.ExpressionText(root.Read()));
        // Tutorial targets inside the editor (TutorialKeys.IsEditorArea): Editor.Code.Start | Loose | Line.N | After.N | Body.N | Arg|Name|Option.N.path (see Walk),
        // with N counted from 1 along the start chain, and Editor.Palette.<BlockKind>. False when not on screen.
        public bool TryGetTutorialCorners(string key, Vector3[] corners)
        {
            if (!IsOpen || key == null) return false;
            if (key.StartsWith(TutorialKeys.PalettePrefix)) return Corners(TutorialPaletteItem(key), null, corners);
            if (!key.StartsWith(TutorialKeys.CodePrefix)) return false;
            var program = panel.Program;
            var parts = key.Substring(TutorialKeys.CodePrefix.Length).Split('.');
            // The start slot, or the block once one sits there (the slot is narrower than e.g. enemy = get_nearest_enemy()).
            if (parts[0] == "Start")
            {
                var first = program.StartChain().FirstOrDefault();
                if (first) return Corners(first.Rect, null, corners);
                // ponytail: an empty start is boxed at the size of 1-1's finished first line (enemy = get_nearest_enemy(), 352x46),
                // the only step that points here; measure the expected block instead if another step needs it.
                var area = program.StartArea;
                return Corners(program.Rect, new Rect(area.xMin, area.yMax - 46, Mathf.Max(area.width, 352), 46), corners);
            }
            if (parts[0] == "Loose") { var loose = program.LooseRoots().FirstOrDefault(); return Corners(loose ? loose.Rect : null, null, corners); }
            var (block, slot) = Walk(parts);
            if (!block) return false;
            switch (parts[0])
            {
                case "Name":
                case "Option":
                    for (int i = 2; i < parts.Length; i++)
                    {
                        if (!int.TryParse(parts[i], out int arg) || arg < 0 || arg >= block.Arguments.Count || block.Arguments[arg].Blocks.Count == 0) return false;
                        block = block.Arguments[arg].Blocks[0];
                    }
                    return Corners(parts[0] == "Name" ? block.InputRect : block.OptionRect, null, corners);
                case "BodyArg":
                case "BodyName":
                    var bodySlot = BodyArgument(parts);
                    return Corners(parts[0] == "BodyArg" ? bodySlot?.Rect : bodySlot?.Blocks.FirstOrDefault()?.InputRect, null, corners);
                case "Line": return Corners(block.Rect, null, corners);
                // The slot where the next block snaps: one line high, right under line N; with a line already there (1-4 C04 puts
                // attack_range between enemy_distance and the if), only the seam between them, so the arrow does not land on the if.
                case "After":
                    var r = block.Rect.rect;
                    return Corners(block.Rect, int.Parse(parts[1]) < program.StartChain().Count ? new Rect(r.xMin, r.yMin - 6, r.width, 12) : new Rect(r.xMin, r.yMin - r.height, r.width, r.height), corners);
                case "Body": return Corners(block.Body ? block.Body.Rect : null, null, corners);
                case "Arg": return Corners(slot ? slot.Rect : null, null, corners);
                case "Name":
                case "Option":
                    var held = slot ? slot.Blocks.FirstOrDefault() : block;
                    return Corners(!held ? null : parts[0] == "Name" ? held.InputRect : held.OptionRect, null, corners);
                default: return false;
            }
        }
        // Follows an Editor.Code key from line N: a number steps into that argument slot of the block reached so far
        // (the next part looks at the block in it), bK into the K-th block of its body. Line.3.b0.0 = the slot inside if's slash.
        (CommandBlockView block, CommandDropZone slot) Walk(string[] parts)
        {
            var chain = panel.Program.StartChain();
            if (parts.Length < 2 || !int.TryParse(parts[1], out int line) || line < 1 || line > chain.Count) return default;
            CommandBlockView block = chain[line - 1]; CommandDropZone slot = null;
            for (int i = 2; i < parts.Length; i++)
            {
                if (slot) { block = slot.Blocks.FirstOrDefault(); slot = null; if (!block) return default; }
                if (parts[i].StartsWith("b"))
                {
                    if (!block.Body || !int.TryParse(parts[i].Substring(1), out int k) || k < 0 || k >= block.Body.Blocks.Count) return default;
                    block = block.Body.Blocks[k];
                }
                else if (int.TryParse(parts[i], out int arg) && arg >= 0 && arg < block.Arguments.Count) slot = block.Arguments[arg];
                else return default;
            }
            return (block, slot);
        }
        // The drop zone an Arg.… or Body.N key names, empty or not.
        CommandDropZone Zone(string key)
        {
            if (!IsOpen || key == null || !key.StartsWith(TutorialKeys.CodePrefix)) return null;
            var parts = key.Substring(TutorialKeys.CodePrefix.Length).Split('.');
            var (block, slot) = Walk(parts);
            return !block ? null : parts[0] == "Arg" ? slot : parts[0] == "Body" ? block.Body : null;
        }
        // Checked on the draft, before the line compiles: an Arg/Body slot holds a block, or line N exists
        // ("Editor.Code.Line.1:DeclareVariable" also names the block kind it must be).
        public bool SlotFilled(string key)
        {
            if (!IsOpen || key == null) return false;
            var kind = key.Split(':');
            if (kind[0].StartsWith(TutorialKeys.CodePrefix + "Line."))
            {
                var (line, _) = Walk(kind[0].Substring(TutorialKeys.CodePrefix.Length).Split('.'));
                return line && (kind.Length < 2 || line.BaseKind.ToString() == kind[1]);
            }
            var zone = Zone(key);
            return zone && zone.Blocks.Count > 0;
        }
        // The player clicked the slot (or already filled it).
        public bool SlotSelected(string key) => Zone(key) is CommandDropZone zone && zone && (panel.SelectedZone == zone || zone.Blocks.Count > 0);
        // "Editor.Code.Name.3.0.0|enemy_distance" / "Editor.Code.Option.3.0|<=": the name field or comparison picker holds the value.
        public bool DraftValueMatches(string expected)
        {
            var pair = (expected ?? "").Split('|');
            if (!IsOpen || pair.Length != 2 || !pair[0].StartsWith(TutorialKeys.CodePrefix)) return false;
            var (block, slot) = Walk(pair[0].Substring(TutorialKeys.CodePrefix.Length).Split('.'));
            var held = slot ? slot.Blocks.FirstOrDefault() : block;
            return held && held.DraftValue == pair[1];
        }
        // Gives a robot a ready-made program (1-3 starts from two given lines); through the editor when it is open on that robot.
        public void LoadTutorialCode(int id, string code)
        {
            var blocks = TutorialAnswers.Parse(code);
            drafts.Remove(id);
            if (IsOpen && editingId == id) { panel.LoadProgram(panel.FunctionName, blocks); panel.RefreshPalette(); return; }
            var sim = battle.Simulation; var robot = sim?.FindRobot(id);
            if (robot != null && sim.CanEdit) sim.ApplyProgram(id, robot.Program?.Name ?? "robot_" + id, blocks);
        }
        static bool Corners(RectTransform target, Rect? local, Vector3[] corners)
        {
            if (!target || !target.gameObject.activeInHierarchy) return false;
            var r = local ?? target.rect;
            corners[0] = target.TransformPoint(new Vector3(r.xMin, r.yMin)); corners[1] = target.TransformPoint(new Vector3(r.xMin, r.yMax));
            corners[2] = target.TransformPoint(new Vector3(r.xMax, r.yMax)); corners[3] = target.TransformPoint(new Vector3(r.xMax, r.yMin));
            return true;
        }
        // Switches the palette to the category holding an Editor.Palette.<BlockKind> target so its outline can show.
        public void RevealPalette(string key)
        {
            if (!IsOpen || !PaletteKind(key, out var kind)) return;
            int category = kind == BlockKind.Variable ? 2 : panel.Palette.Where(p => p.view.BaseKind == kind).Select(p => p.category).DefaultIfEmpty(-1).First();
            if (category >= 0) panel.ShowCategory(category);
        }
        // The palette block for an Editor.Palette.<BlockKind> key (the variable template for Variable); null when not on screen.
        public RectTransform TutorialPaletteItem(string key)
        {
            if (!IsOpen || !PaletteKind(key, out var kind)) return null;
            var item = kind == BlockKind.Variable ? panel.VariableTemplate : panel.Palette.FirstOrDefault(p => p.view.BaseKind == kind).view;
            return item && item.Rect.gameObject.activeInHierarchy ? item.Rect : null;
        }
        static bool PaletteKind(string key, out BlockKind kind)
        {
            kind = default;
            return key != null && key.StartsWith(TutorialKeys.PalettePrefix) && Enum.TryParse(key.Substring(TutorialKeys.PalettePrefix.Length), out kind);
        }
        public void RestoreAutomatic()
        {
            if (!battle.TutorialAllows(TutorialKeys.RestoreAutomatic)) return;
            if (!IsOpen || !HasCurrentSession) return;
            panel.ClearProgram();
            panel.RefreshPalette();
            if (!panel.IsApplied) return;
            panel.ShowMessage("코드를 비웠습니다. 블록 예약이 해제됩니다.");
            battle.ReportTutorialAction(TutorialKeys.RestoreAutomatic, TutorialSignals.TargetClicked);
        }
        void Update()
        {
            if (battle.HelpOpen) return;
            var sim = battle.Simulation;
            if (!IsOpen) return;
            // 재시작이나 도메인 리로드로 세션이 사라지면 이전 로봇 편집을 즉시 종료합니다.
            if (!HasCurrentSession || !sim.CanEdit || !sim.Robots.Exists(r => r.Id == editingId)) { Close(false); return; }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (battle.TutorialBlocksInput) return;
            // 텍스트 입력 중에는 입력창 단축키를 유지하고, 그 밖에서만 블록 실행 취소를 처리합니다.
            if (panel.TextInputFocused || keyboard.altKey.isPressed || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed) return;
            if (keyboard.ctrlKey.isPressed)
            {
                if (keyboard.zKey.wasPressedThisFrame) { if (keyboard.shiftKey.isPressed) panel.Redo(); else panel.Undo(); }
                else if (keyboard.yKey.wasPressedThisFrame) panel.Redo();
            }
        }
        void OnDestroy()
        {
            if (!panel) return;
            panel.BlockDropped -= OnBlockDropped;
            panel.ApplyToTarget = null; panel.IsAvailable = null; panel.Remaining = null;
        }
    }
}
