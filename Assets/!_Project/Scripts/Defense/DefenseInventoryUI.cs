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
        [Serializable] public struct BlockSlot { public BlockKind kind; public TMP_Text label, count; }
        [SerializeField] DefenseBattle battle;
        [SerializeField] GameObject inventoryRoot;
        [SerializeField] Button closeButton;
        [SerializeField] Button[] robotButtons = Array.Empty<Button>();
        [SerializeField] TMP_Text[] robotLabels = Array.Empty<TMP_Text>();
        [SerializeField] BlockSlot[] blockSlots = Array.Empty<BlockSlot>();
        [Header("Developer item grant")]
        [SerializeField] Button categoryButton, previousButton, nextButton, grantButton;
        [SerializeField] TMP_Text categoryLabel, itemLabel, feedback;
        [SerializeField] TMP_InputField quantity;
        readonly BlockKind[] kinds = DefenseProgression.ItemKinds;
        bool blocks;
        int item;
        public bool IsOpen => inventoryRoot.activeSelf;
        void Start()
        {
            if (!battle || !inventoryRoot || !closeButton || robotButtons.Length != 6 || robotLabels.Length != 6 ||
                robotButtons.Any(b => !b) || robotLabels.Any(t => !t) || blockSlots.Length != kinds.Length || blockSlots.Any(s => !s.label || !s.count) ||
                !categoryButton || !previousButton || !nextButton || !grantButton || !categoryLabel || !itemLabel || !feedback || !quantity)
                throw new InvalidOperationException("인벤토리 Inspector 참조를 연결하세요.");
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => { battle.ChooseRobot(index); Close(); }); }
            categoryButton.onClick.AddListener(() => { blocks = !blocks; item = 0; RefreshGrant(); });
            previousButton.onClick.AddListener(() => Change(-1)); nextButton.onClick.AddListener(() => Change(1));
            grantButton.onClick.AddListener(Grant); RefreshGrant(); Close();
        }
        public void Close() { if (inventoryRoot) inventoryRoot.SetActive(false); }
        public void Toggle() => inventoryRoot.SetActive(!inventoryRoot.activeSelf);
        void Change(int delta) { int count = blocks ? kinds.Length : 6; item = (item + delta + count) % count; RefreshGrant(); }
        public void SelectRobotItem(int index) { blocks = false; item = index; RefreshGrant(); }
        public void SelectBlockItem(BlockKind kind) { blocks = true; item = Array.IndexOf(kinds, kind); RefreshGrant(); }
        void RefreshGrant() { categoryLabel.text = blocks ? "블록" : "로봇"; itemLabel.text = blocks ? DefenseProgression.Label(kinds[item]) : battle.DefinitionName(item); }
        public void Grant()
        {
            if (!battle.DeveloperMode) return;
            if (!int.TryParse(quantity.text, out int count) || count < 1) { feedback.text = "1 이상의 수량을 입력하세요."; return; }
            try { if (blocks) battle.Simulation.GrantBlock(kinds[item], count); else battle.Simulation.GrantRobot(battle.DefinitionRole(item), count); }
            catch (OverflowException) { feedback.text = "저장 가능한 정수 범위를 넘었습니다."; return; }
            feedback.text = itemLabel.text + " +" + count;
        }
        void LateUpdate()
        {
            var sim = battle.Simulation;
            if (sim == null || !inventoryRoot.activeSelf) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            for (int i = 0; i < robotButtons.Length; i++)
            {
                int count = sim.RobotAvailable(battle.DefinitionRole(i));
                robotLabels[i].text = battle.DefinitionName(i) + "   × " + count;
                robotButtons[i].interactable = sim.CanPrepare && count > 0;
            }
            foreach (var slot in blockSlots)
            {
                var kind = DefenseProgression.Canonical(slot.kind); sim.Inventory.TryGetValue(kind, out int owned);
                slot.label.text = DefenseProgression.Label(kind);
                slot.count.text = sim.Available(kind).ToString();
                slot.label.color = owned > 0 ? new Color(.9f,.95f,1) : new Color(.42f,.48f,.54f);
                slot.count.color = slot.label.color;
            }
        }
    }
}
