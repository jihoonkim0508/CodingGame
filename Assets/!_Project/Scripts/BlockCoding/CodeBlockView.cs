using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CodingGame.BlockCoding
{
    public sealed class CodeBlockView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] BlockKind kind;
        [SerializeField] bool palette;
        [SerializeField] CodeBlockView palettePrefab;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] TMP_InputField numberInput;
        [SerializeField] TMP_Dropdown comparisonInput;
        [SerializeField] TMP_InputField variableInput;
        [SerializeField] TMP_Text variableLabel;
        [SerializeField] string variableName = "val";
        [SerializeField] BlockCodingPanel palettePanel;
        [SerializeField] BlockDropZone[] arguments = Array.Empty<BlockDropZone>();
        public static CodeBlockView Dragged { get; private set; }
        public BlockKind Kind => kind;
        public bool IsPalette => palette;
        public CodeBlockView PalettePrefab => palettePrefab;
        public BlockDropZone Owner { get; private set; }
        public string VariableName => kind == BlockKind.DeclareVariable ? variableInput.text : variableName;
        public string RetainedVariableName => lastValidName;
        public bool DropAccepted { get; private set; }
        string lastValidName;
        BlockCodingPanel dragPanel;

        void Awake()
        {
            if (!canvasGroup) throw new InvalidOperationException($"{name}: CanvasGroup 참조가 필요합니다.");
            if (palette && !palettePrefab) throw new InvalidOperationException($"{name}: Palette Prefab 참조가 필요합니다.");
            if (arguments.Length != PythonBlockCompiler.ArgumentCount(kind))
                throw new InvalidOperationException($"{name}: {kind}의 인자 슬롯 개수가 올바르지 않습니다.");
            if (kind == BlockKind.Number)
            {
                if (!numberInput) throw new InvalidOperationException($"{name}: Number Input 참조가 필요합니다.");
                numberInput.contentType = TMP_InputField.ContentType.Standard;
                numberInput.lineType = TMP_InputField.LineType.SingleLine;
                numberInput.characterLimit = 0;
                if (string.IsNullOrEmpty(numberInput.text)) numberInput.SetTextWithoutNotify("0");
                numberInput.onValidateInput += ValidateNumber;
                numberInput.onValueChanged.AddListener(OnNumberChanged);
            }
            if (kind == BlockKind.Comparison)
            {
                if (!comparisonInput) throw new InvalidOperationException($"{name}: Comparison Dropdown 참조가 필요합니다.");
                int selected = comparisonInput.value;
                comparisonInput.ClearOptions();
                comparisonInput.AddOptions(new List<string>(PythonBlockCompiler.Comparisons));
                comparisonInput.SetValueWithoutNotify(selected);
                comparisonInput.onValueChanged.AddListener(OnComparisonChanged);
            }
            if (kind == BlockKind.DeclareVariable)
            {
                if (!variableInput) throw new InvalidOperationException($"{name}: Variable Input 참조가 필요합니다.");
                try { lastValidName = PythonBlockCompiler.NormalizeIdentifier(variableInput.text); }
                catch (FormatException) { lastValidName = ""; }
                variableInput.onValueChanged.AddListener(OnVariableChanged);
            }
            if (kind == BlockKind.Variable)
            {
                if (!variableLabel) throw new InvalidOperationException($"{name}: Variable Label 참조가 필요합니다.");
                variableLabel.text = variableName;
            }
        }

        public void Bind(BlockDropZone owner)
        {
            Owner = owner;
            palette = false;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1;
            foreach (var argument in arguments)
            {
                if (!argument) throw new InvalidOperationException($"{name}: 인자 슬롯 참조가 비어 있습니다.");
                argument.Bind(owner.Panel);
            }
        }

        public CodeBlock Read(bool allowIncomplete = false)
        {
            var block = new CodeBlock(kind);
            if (kind == BlockKind.Number) block.Value = numberInput.text;
            if (kind == BlockKind.Comparison) block.Value = PythonBlockCompiler.Comparisons[comparisonInput.value];
            if (kind == BlockKind.DeclareVariable || kind == BlockKind.Variable) block.Value = VariableName;
            foreach (var argument in arguments) block.Arguments.Add(argument.ReadArgument(allowIncomplete));
            return block;
        }

        static char ValidateNumber(string text, int index, char value)
        {
            // Permit intermediate edits ("-", ".", "1e-"); Apply validates the completed literal.
            return (value >= '0' && value <= '9') || value == '-' || value == '+' ||
                   value == '.' || value == 'e' || value == 'E' ? value : '\0';
        }

        void OnNumberChanged(string value)
        {
            // TMP paste can bypass per-character validation on some platforms.
            string filtered = string.Concat(Array.FindAll(value.ToCharArray(), c => ValidateNumber("", 0, c) != '\0'));
            if (filtered != value) numberInput.SetTextWithoutNotify(filtered);
            Owner?.Panel.MarkDirty();
        }
        void OnComparisonChanged(int value) => Owner?.Panel.MarkDirty();

        void OnVariableChanged(string value)
        {
            try
            {
                string normalized = PythonBlockCompiler.NormalizeIdentifier(value);
                if (normalized != lastValidName) Owner?.Panel.RenameVariable(lastValidName, normalized);
                lastValidName = normalized;
            }
            catch (FormatException) { }
            Owner?.Panel.MarkDirty();
        }

        public void SetVariableName(string value)
        {
            variableName = value;
            if (variableLabel) variableLabel.text = value;
        }

        public void ConfigurePalette(BlockCodingPanel panel, CodeBlockView prefab, string value)
        {
            palette = true;
            palettePanel = panel;
            palettePrefab = prefab;
            SetVariableName(value);
        }

        public IEnumerable<CodeBlockView> EnumerateBlocks()
        {
            yield return this;
            foreach (var argument in arguments)
                foreach (var block in argument.EnumerateBlocks()) yield return block;
        }

        public void AcceptDrop() => DropAccepted = true;

        public CodeBlockView CopyTo(Transform parent)
        {
            var copy = Instantiate(palette ? palettePrefab : this, parent);
            if (kind == BlockKind.Variable) copy.SetVariableName(variableName);
            return copy;
        }

        public void SetDragAppearance(bool preview)
        {
            canvasGroup.blocksRaycasts = !preview;
            canvasGroup.interactable = !preview;
            canvasGroup.alpha = preview ? 0.92f : 1;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!palette && Owner == null) return;
            if (Dragged != null) return;
            dragPanel = Owner != null ? Owner.Panel : palettePanel;
            if (!dragPanel) throw new InvalidOperationException($"{name}: Palette Panel 참조가 필요합니다.");
            Dragged = this;
            DropAccepted = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.55f;
            dragPanel.BeginBlockDrag(this, eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (Dragged == this) dragPanel.MoveBlockDrag(eventData);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (Dragged != this) return;
            Dragged = null;
            bool delete = !palette && !DropAccepted && !dragPanel.ContainsPointer(eventData);
            dragPanel.EndBlockDrag();
            dragPanel = null;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1;
            if (delete) RemoveFromProgram();
        }
        // Consume block clicks so only the row background changes indentation.
        public void OnPointerClick(PointerEventData eventData) => eventData.Use();

        public void RemoveFromProgram(bool notify = true)
        {
            if (palette || Owner == null) return;
            var panel = Owner.Panel;
            Owner.Remove(this, false);
            gameObject.SetActive(false);
            Destroy(gameObject);
            if (notify) panel.MarkDirty();
        }

        void OnDisable()
        {
            if (Dragged == this)
            {
                Dragged = null;
                if (dragPanel) dragPanel.EndBlockDrag();
                dragPanel = null;
            }
            if (canvasGroup) { canvasGroup.blocksRaycasts = true; canvasGroup.alpha = 1; }
        }
    }
}
