using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        // Python text of condition/value blocks lying in the workspace away from the start anchor (not part of the program).
        public IEnumerable<string> LooseExpressions() => !IsOpen ? Enumerable.Empty<string>() : panel.Program.LooseRoots()
            .Where(root => PythonTreeCompiler.OutputSlot(root.Kind) != BlockSlotKind.Statement)
            .Select(root => PythonTreeCompiler.ExpressionText(root.Read()));
        // Tutorial targets inside the editor (TutorialKeys.IsEditorArea): Editor.Code.Start | Loose | Line.N | After.N | Body.N | Arg.N.i[.j…],
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
                var slot = program.StartArea;
                return Corners(program.Rect, new Rect(slot.xMin, slot.yMax - 46, Mathf.Max(slot.width, 352), 46), corners);
            }
            if (parts[0] == "Loose") { var loose = program.LooseRoots().FirstOrDefault(); return Corners(loose ? loose.Rect : null, null, corners); }
            var chain = program.StartChain();
            if (parts.Length < 2 || !int.TryParse(parts[1], out int line) || line < 1 || line > chain.Count) return false;
            var block = chain[line - 1];
            switch (parts[0])
            {
                case "Line": return Corners(block.Rect, null, corners);
                // The slot where the next block snaps: one line high, right under line N.
                case "After": var r = block.Rect.rect; return Corners(block.Rect, new Rect(r.xMin, r.yMin - r.height, r.width, r.height), corners);
                case "Body": return Corners(block.Body ? block.Body.Rect : null, null, corners);
                case "Arg":
                    CommandDropZone slot = null;
                    for (int i = 2; i < parts.Length; i++)
                    {
                        if (slot) { if (slot.Blocks.Count == 0) return false; block = slot.Blocks[0]; }
                        if (!int.TryParse(parts[i], out int index) || index < 0 || index >= block.Arguments.Count) return false;
                        slot = block.Arguments[index];
                    }
                    return Corners(slot ? slot.Rect : null, null, corners);
                default: return false;
            }
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
