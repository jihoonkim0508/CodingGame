using System;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    [DefaultExecutionOrder(100)]
    public sealed class DefenseBlockHelp : MonoBehaviour
    {
        [Serializable] public sealed class Entry
        {
            public BlockKind kind;
            public Button button;
            public TMP_Text label;
            public string title;
            [TextArea(3, 8)] public string description, example;
        }
        [SerializeField] DefenseBattle battle;
        [SerializeField] GameObject window;
        [SerializeField] Button[] infoButtons = Array.Empty<Button>();
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text title, description, example, stock;
        [SerializeField] ScrollRect list;
        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        Entry selected;
        public bool IsOpen => window.activeSelf;

        void Start()
        {
            if (!battle || !window || infoButtons.Length == 0 || infoButtons.Any(b => !b) || !closeButton || !title || !description || !example || !stock || !list ||
                entries.Length == 0 || entries.Any(e => !e.button || !e.label))
                throw new InvalidOperationException("DefenseBlockHelp Inspector 참조를 연결하세요.");
            foreach (var button in infoButtons) button.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            foreach (var entry in entries) entry.button.onClick.AddListener(() => Select(entry));
            example.richText = false;
            window.SetActive(false);
        }
        int Owned(Entry entry) => battle.Simulation.Inventory.TryGetValue(DefenseProgression.Canonical(entry.kind), out int count) ? count : 0;
        public void Open()
        {
            if (battle.Simulation == null) return;
            foreach (var entry in entries)
            {
                int count = Owned(entry);
                entry.button.gameObject.SetActive(count > 0);
                entry.label.text = entry.title + "   × " + count;
            }
            window.SetActive(true);
            battle.HelpOpen = true;
            Select(selected != null && Owned(selected) > 0 ? selected : entries.FirstOrDefault(e => Owned(e) > 0));
            Canvas.ForceUpdateCanvases();
            list.verticalNormalizedPosition = 1;
        }
        void Select(Entry entry)
        {
            selected = entry;
            title.text = entry?.title ?? "블록 도감";
            description.text = entry?.description ?? "보유한 블록이 없습니다. 웨이브 보상을 수령하면 사용법을 확인할 수 있습니다.";
            example.text = entry?.example ?? "";
            stock.text = entry == null ? "" : "보유 " + Owned(entry) + "개 · 로봇에 배치한 블록 포함";
            foreach (var item in entries)
                item.button.targetGraphic.color = item == entry ? new Color(.16f, .39f, .46f) : new Color(.10f, .15f, .20f);
        }
        public void Close() { window.SetActive(false); battle.HelpOpen = false; }
        void Update() { if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close(); }
        void OnDisable() { if (battle) battle.HelpOpen = false; if (window) window.SetActive(false); }
    }
}
