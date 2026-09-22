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
        [SerializeField] Button closeButton, automaticButton, recallButton;
        int editingId;
        DefenseSimulation editingSession;
        public bool IsOpen => editorRoot && editorRoot.activeSelf;
        public int EditingId => editingId;
        public CommandCodingPanel Panel => panel;
        void Start()
        {
            if (!battle || !panel || !editorRoot || !title || !inventory || !closeButton || !automaticButton || !recallButton)
                throw new InvalidOperationException("DefenseCodeEditor Inspector 참조를 연결하세요.");
            panel.ApplyToTarget = Apply;
            panel.IsAvailable = kind => editingSession == null || editingSession.Available(kind, editingId) > 0;
            panel.Remaining = kind => editingSession == null ? 0 : Math.Max(0, editingSession.Available(kind, editingId) - panel.Program.AllBlocks().Count(b => DefenseProgression.Canonical(b.Kind) == DefenseProgression.Canonical(kind)));
            battle.RobotSelected += Open;
            closeButton.onClick.AddListener(() => Close());
            automaticButton.onClick.AddListener(RestoreAutomatic);
            recallButton.onClick.AddListener(battle.RemoveSelected);
            editorRoot.SetActive(false);
        }
        public void OpenSelected() => Open(battle.SelectedId);
        public void Open(int id)
        {
            var sim = battle.Simulation;
            var robot = sim?.Robots.Find(r => r.Id == id);
            if (robot == null || !sim.CanEdit) return;
            if (IsOpen && editingId == id && editingSession == sim) return;
            Close(); editingSession = sim; editingId = id;
            title.text = battle.RobotName(id) + " #" + id;
            battle.BeginCodeView(robot);
            editorRoot.SetActive(true);
            panel.LoadProgram(robot.Program?.Name ?? "robot_" + id, robot.Program?.CopyBlocks() ?? Array.Empty<CodeBlock>());
            panel.ShowCategory(1); RefreshInventory();
            panel.ShowMessage(robot.Program == null || robot.Program.CopyBlocks().Count == 0 ? "블록을 넣으면 자동 적용됩니다." : "자동 적용됨");
        }
        void RefreshInventory()
        {
            inventory.text = "블록 인벤토리";
            panel.RefreshPalette();
        }
        string Apply(string name, IReadOnlyList<CodeBlock> blocks)
        {
            if (!IsOpen || editingSession != battle.Simulation) throw new FormatException("로봇을 다시 선택하세요.");
            editingSession.ApplyProgram(editingId, name, blocks); RefreshInventory();
            battle.NotifyCodeApplied(editingId, blocks.Count == 0);
            return blocks.Count == 0 ? "코드 없음 · 행동하지 않음" : "자동 적용됨";
        }
        public void Close(bool resume = true)
        {
            if (IsOpen) battle.EndCodeView();
            if (editorRoot) editorRoot.SetActive(false);
            editingId = 0; editingSession = null;
        }
        // Retains the serialized button connection; the player operation now clears code.
        public void RestoreAutomatic()
        {
            if (!IsOpen || editingSession != battle.Simulation) return;
            editingSession.ApplyProgram(editingId, "robot_" + editingId, Array.Empty<CodeBlock>());
            battle.NotifyCodeApplied(editingId, true);
            panel.LoadProgram("robot_" + editingId, Array.Empty<CodeBlock>());
            RefreshInventory(); panel.ShowMessage("코드를 비웠습니다. 블록 예약이 해제됩니다.");
        }
        void Update()
        {
            var sim = battle.Simulation;
            if (!IsOpen) return;
            if (editingSession != sim || !sim.CanEdit || !sim.Robots.Exists(r => r.Id == editingId)) { Close(false); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }
        void OnDestroy() { if (battle) battle.RobotSelected -= Open; if (panel) { panel.ApplyToTarget = null; panel.IsAvailable = null; panel.Remaining = null; } }
    }
}
