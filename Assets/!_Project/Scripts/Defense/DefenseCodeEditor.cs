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
        // ponytail: optional until the scene wires the hint button (W3); SetHint is a no-op without it.
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
        public void RestoreAutomatic()
        {
            if (!battle.TutorialAllows("Editor.RestoreAutomatic")) return;
            if (!IsOpen || !HasCurrentSession) return;
            panel.ClearProgram();
            panel.RefreshPalette();
            if (!panel.IsApplied) return;
            panel.ShowMessage("코드를 비웠습니다. 블록 예약이 해제됩니다.");
            battle.ReportTutorialAction("Editor.RestoreAutomatic", "TargetClicked");
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
