using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    public sealed class BlockCodingPanel : MonoBehaviour
    {
        [SerializeField] TMP_InputField functionName;
        [SerializeField] TMP_Text pythonPreview;
        [SerializeField] TMP_Text status;
        [SerializeField] Button applyButton;
        [SerializeField] Button addLineButton;
        [SerializeField] RectTransform lineContainer;
        [SerializeField] CodeLineView linePrefab;
        [SerializeField] RectTransform editArea;
        [SerializeField] RectTransform dragLayer;
        [SerializeField] RectTransform variablePalette;
        [SerializeField] CodeBlockView variablePrefab;
        [SerializeField] GameObject variableEmptyHint;
        [SerializeField] List<CodeLineView> lines = new List<CodeLineView>();
        [SerializeField] UnityEvent<string> onApplied = new UnityEvent<string>();
        static int nextFunctionNumber;
        public string AppliedSource { get; private set; }
        public bool IsApplied { get; private set; }
        readonly Dictionary<string, CodeBlockView> variableEntries = new Dictionary<string, CodeBlockView>();
        public IEnumerable<string> AvailableVariables => variableEntries.Keys;
        CodeBlockView dragPreview;
        bool refreshing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCounter() => nextFunctionNumber = 0;

        void Awake()
        {
            if (!functionName || !pythonPreview || !status || !applyButton || !addLineButton || !lineContainer || !linePrefab ||
                !editArea || !dragLayer || !variablePalette || !variablePrefab || !variableEmptyHint)
                throw new InvalidOperationException($"{name}: BlockCodingPanel의 Inspector 참조를 모두 연결해 주세요.");
            functionName.SetTextWithoutNotify("f" + nextFunctionNumber++);
            pythonPreview.richText = false;
            functionName.onValueChanged.AddListener(OnNameChanged);
            applyButton.onClick.AddListener(Apply);
            addLineButton.onClick.AddListener(AddLine);
            foreach (var line in lines)
            {
                if (!line) throw new InvalidOperationException($"{name}: 초기 줄 참조가 비어 있습니다.");
                line.Bind(this);
            }
            Renumber();
            MarkDirty();
        }

        public void AddLine()
        {
            var line = Instantiate(linePrefab, lineContainer);
            lines.Add(line);
            line.Bind(this);
            Renumber();
            MarkDirty();
        }
        public void RemoveLine(CodeLineView line)
        {
            if (!lines.Remove(line)) return;
            line.gameObject.SetActive(false);
            Destroy(line.gameObject);
            Renumber();
            MarkDirty();
        }
        void Renumber()
        {
            for (int i = 0; i < lines.Count; i++) lines[i].SetNumber(i + 1);
        }
        void OnNameChanged(string value) => MarkDirty();
        string Compile()
        {
            var program = new List<CodeLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                try { program.Add(lines[i].Read()); }
                catch (FormatException error) { throw new FormatException($"{i + 1}줄: {error.Message}"); }
            }
            return PythonBlockCompiler.Compile(functionName.text, program);
        }
        public void MarkDirty()
        {
            if (refreshing) return;
            refreshing = true;
            IsApplied = false;
            try
            {
                SyncVariables();
                pythonPreview.text = Compile();
                status.text = "Ready to apply";
            }
            catch (FormatException error) { pythonPreview.text = ""; status.text = error.Message; }
            finally { refreshing = false; }
        }

        IEnumerable<CodeBlockView> EnumerateBlocks() => lines.SelectMany(line => line.EnumerateBlocks());

        public void RenameVariable(string previous, string current)
        {
            if (string.IsNullOrEmpty(previous)) return;
            // Other assignments to the old name still belong to that variable.
            if (EnumerateBlocks().Any(b => b.Kind == BlockKind.DeclareVariable && b.VariableName == previous)) return;
            foreach (var block in EnumerateBlocks())
                if (block.Kind == BlockKind.Variable && block.VariableName == previous) block.SetVariableName(current);
        }

        void SyncVariables()
        {
            var retained = new HashSet<string>();
            foreach (var block in EnumerateBlocks().Where(b => b.Kind == BlockKind.DeclareVariable))
            {
                try { retained.Add(PythonBlockCompiler.NormalizeIdentifier(block.VariableName)); }
                catch (FormatException)
                {
                    // Keep placed references during intermediate edits such as selecting and replacing a name.
                    if (!string.IsNullOrEmpty(block.RetainedVariableName)) retained.Add(block.RetainedVariableName);
                }
            }
            foreach (var block in EnumerateBlocks().ToArray())
                if (block.Kind == BlockKind.Variable && !retained.Contains(block.VariableName)) block.RemoveFromProgram(false);
            var variables = PythonBlockCompiler.DeclaredVariables(lines.Select(line => line.Read(true)).ToArray());
            foreach (var key in variableEntries.Keys.ToArray())
            {
                if (variables.ContainsKey(key)) continue;
                var entry = variableEntries[key];
                variableEntries.Remove(key);
                entry.gameObject.SetActive(false);
                Destroy(entry.gameObject);
            }
            foreach (var key in variables.Keys)
            {
                if (variableEntries.ContainsKey(key)) continue;
                var entry = Instantiate(variablePrefab, variablePalette);
                entry.ConfigurePalette(this, variablePrefab, key);
                variableEntries.Add(key, entry);
            }
            variableEmptyHint.SetActive(variableEntries.Count == 0);
        }

        public bool ContainsPointer(PointerEventData pointer) =>
            RectTransformUtility.RectangleContainsScreenPoint(editArea, pointer.position, pointer.pressEventCamera);

        public void BeginBlockDrag(CodeBlockView source, PointerEventData pointer)
        {
            EndBlockDrag();
            dragPreview = source.CopyTo(dragLayer);
            dragPreview.name = "Dragged Block Preview";
            dragPreview.SetDragAppearance(true);
            var rect = (RectTransform)dragPreview.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 1);
            rect.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            rect.sizeDelta = new Vector2(Math.Max(60, LayoutUtility.GetPreferredWidth(rect)), Math.Max(40, LayoutUtility.GetPreferredHeight(rect)));
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            MoveBlockDrag(pointer);
        }

        public void MoveBlockDrag(PointerEventData pointer)
        {
            if (!dragPreview) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, pointer.position, pointer.pressEventCamera, out var position))
                ((RectTransform)dragPreview.transform).anchoredPosition = position + new Vector2(14, -14);
        }

        public void EndBlockDrag()
        {
            if (!dragPreview) return;
            dragPreview.gameObject.SetActive(false);
            Destroy(dragPreview.gameObject);
            dragPreview = null;
        }

        void OnDisable() => EndBlockDrag();
        public void Apply()
        {
            try
            {
                string source = Compile();
                AppliedSource = source;
                IsApplied = true;
                pythonPreview.text = source;
                status.text = "Applied";
                Debug.Log(source, this);
                onApplied.Invoke(source);
            }
            catch (FormatException error)
            {
                IsApplied = false;
                status.text = error.Message;
                Debug.LogWarning(error.Message, this);
            }
        }
    }
}
