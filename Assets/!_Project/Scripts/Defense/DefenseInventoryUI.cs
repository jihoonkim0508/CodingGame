using System;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class DefenseInventoryUI : MonoBehaviour
    {
        [Serializable] public struct BlockSlot { public BlockKind kind; public TMP_Text label; }
        [SerializeField] DefenseBattle battle;
        [SerializeField] GameObject inventoryRoot;
        [SerializeField] Button closeButton;
        [SerializeField] Button[] robotButtons = Array.Empty<Button>();
        [SerializeField] TMP_Text[] robotLabels = Array.Empty<TMP_Text>();
        [SerializeField] BlockSlot[] blockSlots = Array.Empty<BlockSlot>();
        readonly BlockKind[] kinds = DefenseProgression.ItemKinds;
        public bool IsOpen => inventoryRoot.activeSelf;
        void Start()
        {
            if (!battle || !inventoryRoot || !closeButton || robotButtons.Length != 6 || robotLabels.Length != 6 ||
                robotButtons.Any(b => !b) || robotLabels.Any(t => !t) || blockSlots.Length != kinds.Length || blockSlots.Any(s => !s.label))
                throw new InvalidOperationException("인벤토리 Inspector 참조를 연결하세요.");
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => { battle.ChooseRobot(index); Close(); }); }
            Close();
        }
        public void Close() { if (inventoryRoot) inventoryRoot.SetActive(false); }
        public void Toggle() => inventoryRoot.SetActive(!inventoryRoot.activeSelf);
        void LateUpdate()
        {
            var sim = battle.Simulation;
            if (sim == null || !inventoryRoot.activeSelf) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            DefensePlayerHUD.RefreshRobotButtons(battle, robotButtons, robotLabels, "   x");
            foreach (var slot in blockSlots)
            {
                var kind = DefenseProgression.Canonical(slot.kind);
                slot.label.text = DefenseProgression.Label(kind);
                slot.label.color = sim.Available(kind) > 0 ? new Color(.81f,.87f,.91f) : new Color(.49f,.58f,.64f);
            }
        }
    }
}
