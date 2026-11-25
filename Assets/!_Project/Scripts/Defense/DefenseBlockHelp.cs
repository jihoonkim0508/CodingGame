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
        [SerializeField] TMP_Text title, description, example;
        [SerializeField] ScrollRect list;
        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        Entry selected;
        public bool IsOpen => window.activeSelf;

        void Start()
        {
            if (!battle || !window || infoButtons.Length == 0 || infoButtons.Any(b => !b) || !closeButton || !title || !description || !example || !list ||
                entries.Length == 0 || entries.Any(e => !e.button || !e.label))
                throw new InvalidOperationException("DefenseBlockHelp Inspector 참조를 연결하세요.");
            foreach (var button in infoButtons) button.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            foreach (var entry in entries) entry.button.onClick.AddListener(() => Select(entry));
            example.richText = false;
            window.SetActive(false);
        }
        bool Owned(Entry entry) => battle.Simulation.Owned(entry.kind) > 0;
        public void Open()
        {
            if (battle.Simulation == null) return;
            foreach (var entry in entries)
            {
                entry.button.gameObject.SetActive(Owned(entry));
                entry.label.text = entry.title;
            }
            window.SetActive(true);
            battle.HelpOpen = true;
            Select(selected != null && Owned(selected) ? selected : entries.FirstOrDefault(Owned));
            Canvas.ForceUpdateCanvases();
            list.verticalNormalizedPosition = 1;
        }
        void Select(Entry entry)
        {
            selected = entry;
            title.text = entry?.title ?? "블록 도감";
            description.text = entry?.description ?? "보유한 블록이 없습니다. 웨이브 보상을 수령하면 사용법을 확인할 수 있습니다.";
            example.text = entry?.example ?? "";
            foreach (var item in entries)
            {
                var slot=PythonTreeCompiler.OutputSlot(item.kind);
                Color category=PythonTreeCompiler.HasBody(item.kind)||item.kind==BlockKind.Break||item.kind==BlockKind.Continue?UITheme.BlockCategories[0]:
                    UITheme.BlockCategories[slot==BlockSlotKind.Condition?3:slot==BlockSlotKind.Value?2:1];
                item.button.targetGraphic.color=item==entry?category:Color.Lerp(category,new Color(.1f,.14f,.2f),.6f);
            }
        }
        public void Close() { window.SetActive(false); battle.HelpOpen = false; }
        void Update() { if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close(); }
        void OnDisable() { if (battle) battle.HelpOpen = false; if (window) window.SetActive(false); }
    }
}
