using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    public sealed class CommandBlockView : MonoBehaviour, ILayoutElement, ILayoutGroup,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler
    {
        [SerializeField] BlockKind kind;
        [SerializeField] RectTransform rect;
        [SerializeField] RectTransform header;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] CommandBlockShape shape;
        [SerializeField] TMP_InputField input;
        [SerializeField] TMP_Dropdown dropdown;
        [SerializeField] GameObject methodArguments;
        [SerializeField] CommandDropZone[] arguments = Array.Empty<CommandDropZone>();
        [SerializeField] CommandDropZone body;
        [SerializeField] TMP_Text stockLabel;
        [SerializeField] RectTransform stockColumn;
        [SerializeField] string variableName = "enemy";
        public CommandCodingPanel Panel { get; private set; }
        public CommandDropZone Owner { get; private set; }
        public bool IsPalette { get; private set; }
        public BlockKind Kind => kind == BlockKind.Variable && dropdown.value == 1 ? BlockKind.Distance : kind;
        public BlockKind BaseKind => kind;
        public CommandDropZone Body => body;
        public IReadOnlyList<CommandDropZone> Arguments => arguments;
        public RectTransform Rect => rect;
        public string VariableName => kind == BlockKind.DeclareVariable ? input.text : variableName;
        public string RetainedName { get; private set; }
        public bool TextInputFocused => input && input.isFocused;
        bool initialized;
        // Expressions share a fixed height even when nested; only their width grows.
        float headHeight => PythonTreeCompiler.OutputSlot(Kind) == BlockSlotKind.Statement ? 46 : 32;
        public float minWidth => preferredWidth;
        public float preferredWidth => Mathf.Max(100, Mathf.Max(LayoutUtility.GetPreferredWidth(header) + 28, body ? body.preferredWidth + 32 : 0));
        public float flexibleWidth => 0;
        public float minHeight => preferredHeight;
        public float preferredHeight => headHeight + (body ? body.preferredHeight + 18 : 0);
        public float flexibleHeight => 0;
        public int layoutPriority => 1;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }

        public void Bind(CommandCodingPanel panel, CommandDropZone owner, bool palette = false)
        {
            if (!rect || !header || !canvasGroup || !shape ||
                ((kind == BlockKind.Number || kind == BlockKind.DeclareVariable) && !input) ||
                ((kind == BlockKind.Variable || kind == BlockKind.Comparison) && !dropdown) ||
                (PythonTreeCompiler.HasBody(kind) && !body))
                throw new InvalidOperationException(name + ": Inspector 참조가 누락되었습니다.");
            Panel = panel;
            if (stockLabel && !stockColumn) throw new InvalidOperationException(name + ": 수량 열 참조를 연결하세요.");
            Owner = owner;
            IsPalette = palette;
            if (stockLabel) stockLabel.gameObject.SetActive(palette && panel.Remaining != null);
            // Disable the handlers too so palette inputs forward drag/click events to the block.
            if (input) { input.interactable = !palette; input.enabled = !palette; }
            if (dropdown) { dropdown.interactable = !palette; dropdown.enabled = !palette; }
            if (!initialized)
            {
                initialized = true;
                if (input) input.onValueChanged.AddListener(InputChanged);
                if (dropdown) dropdown.onValueChanged.AddListener(SelectionChanged);
            }
            foreach (var argument in arguments) argument.Bind(panel, this);
            if (body) body.Bind(panel, this);
            if (kind == BlockKind.Variable) RefreshVariableOptions();
            if (kind == BlockKind.DeclareVariable)
            {
                try { RetainedName = PythonTreeCompiler.NormalizeIdentifier(input.text); }
                catch (FormatException) { }
            }
            if (methodArguments) methodArguments.SetActive(dropdown.value == 1);
            shape.Configure(PythonTreeCompiler.OutputSlot(Kind), body);
            SetDragAppearance(false);
        }

        void InputChanged(string value)
        {
            if (kind == BlockKind.DeclareVariable)
            {
                try
                {
                    string current = PythonTreeCompiler.NormalizeIdentifier(value);
                    if (!IsPalette && current != RetainedName) Panel.RenameVariable(RetainedName, current);
                    RetainedName = current;
                }
                catch (FormatException) { }
            }
            if (!IsPalette) Panel.Changed();
        }
        void SelectionChanged(int value)
        {
            if (methodArguments) methodArguments.SetActive(value == 1);
            if (!IsPalette) Panel.Changed();
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        public void SetVariable(string value)
        {
            variableName = value;
            if (kind == BlockKind.Variable) RefreshVariableOptions();
        }
        public void RefreshVariableOptions()
        {
            int selection = dropdown.value;
            var options = new List<string> { variableName };
            if (selection == 1 || (Panel && Panel.IsEnemyVariable(variableName))) options.Add(variableName + ".get_distance(");
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(Mathf.Min(selection, options.Count - 1));
            dropdown.RefreshShownValue();
            if (methodArguments) methodArguments.SetActive(dropdown.value == 1);
        }
        public void SetText(string value) { input.text = value; }
        public void SetStock(int count) { if (stockLabel) { stockLabel.text = count.ToString(); stockLabel.gameObject.SetActive(IsPalette && Panel.Remaining != null); } }
        void LateUpdate()
        {
            if (!IsPalette || !stockLabel || !stockLabel.gameObject.activeSelf) return;
            // Keep differently sized block silhouettes while sharing one right-aligned count column.
            var edge = rect.InverseTransformPoint(stockColumn.TransformPoint(new Vector3(stockColumn.rect.xMax - 8, 0, 0)));
            var label = stockLabel.rectTransform;
            label.anchoredPosition = new Vector2(edge.x - rect.rect.xMax, -3);
        }
        public void SelectOption(int value) { dropdown.value = value; }
        internal void LoadData(CodeBlock data)
        {
            if (kind == BlockKind.Number || kind == BlockKind.DeclareVariable) input.SetTextWithoutNotify(data.Value);
            if (kind == BlockKind.DeclareVariable) RetainedName = data.Value;
            if (kind == BlockKind.Comparison) dropdown.SetValueWithoutNotify(Array.IndexOf(PythonBlockCompiler.Comparisons, data.Value));
            int skip = 0;
            if (kind == BlockKind.Variable)
            {
                variableName = data.Kind == BlockKind.Distance ? data.Arguments[0].Value : data.Value;
                dropdown.ClearOptions(); dropdown.AddOptions(new List<string> { variableName, variableName + ".get_distance(" });
                dropdown.SetValueWithoutNotify(data.Kind == BlockKind.Distance ? 1 : 0);
                methodArguments.SetActive(data.Kind == BlockKind.Distance);
                if (data.Kind == BlockKind.Variable) return;
                skip = 1;
            }
            for (int i = 0; i < data.Arguments.Count - skip; i++)
                arguments[i].Load(data.Arguments[i + skip] == null ? Array.Empty<CodeBlock>() : new[] { data.Arguments[i + skip] });
            if (body) body.Load(data.Body);
        }

        public CodeBlock Read()
        {
            var block = new CodeBlock(Kind);
            if (kind == BlockKind.Number || kind == BlockKind.DeclareVariable) block.Value = input.text;
            if (kind == BlockKind.Comparison) block.Value = PythonBlockCompiler.Comparisons[dropdown.value];
            if (kind == BlockKind.Variable)
            {
                if (Kind == BlockKind.Variable) { block.Value = variableName; return block; }
                block.Arguments.Add(new CodeBlock(BlockKind.Variable, variableName));
            }
            foreach (var argument in arguments) block.Arguments.Add(argument.ReadArgument());
            if (body) block.Body.AddRange(body.Read());
            return block;
        }
        public IEnumerable<CommandBlockView> Descendants()
        {
            yield return this;
            foreach (var zone in arguments)
                foreach (var child in zone.AllBlocks()) yield return child;
            if (body) foreach (var child in body.AllBlocks()) yield return child;
        }
        public CommandBlockView Copy(Transform parent)
        {
            var copy = Instantiate(this, parent);
            copy.Bind(Panel, null);
            copy.gameObject.SetActive(true);
            // LayoutGroup measurements are not copied by Instantiate. Measure the
            // header and nested slots before placement or the drag ghost reads them.
            LayoutRebuilder.ForceRebuildLayoutImmediate(copy.Rect);
            return copy;
        }
        public void SetDragAppearance(bool dragged)
        {
            canvasGroup.alpha = dragged ? 0.55f : 1;
            canvasGroup.blocksRaycasts = !dragged;
        }
        public void JoinNext(bool joined) { shape.JoinNext = joined; shape.SetVerticesDirty(); }
        public void SetLayoutHorizontal()
        {
            Place(header, 14, 6, rect.rect.width - 28, headHeight - 12);
            if (body) Place(body.Rect, 24, headHeight, rect.rect.width - 32, body.preferredHeight);
        }
        public void SetLayoutVertical()
        {
            SetLayoutHorizontal();
            shape.HeaderHeight = headHeight;
            shape.SetVerticesDirty();
        }
        public static void Place(RectTransform child, float x, float y, float width, float height)
        {
            child.anchorMin = child.anchorMax = child.pivot = new Vector2(0, 1);
            child.anchoredPosition = new Vector2(x, -y);
            child.sizeDelta = new Vector2(width, height);
        }
        public void BringToFront()
        {
            if (IsPalette) return;
            var outer = this;
            while (outer.Owner && outer.Owner.ParentBlock) outer = outer.Owner.ParentBlock;
            outer.Owner?.BringToFront(outer);
        }
        public void OnPointerDown(PointerEventData e) => BringToFront();
        public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { BringToFront(); Panel.BeginDrag(this, e); } }
        public void OnDrag(PointerEventData e) => Panel.MoveDrag(e);
        public void OnEndDrag(PointerEventData e)
        {
            if (Panel.Dragged == this) Panel.FinishDrag(e);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && IsPalette) Panel.AddFromPalette(this);
            else if (e.button == PointerEventData.InputButton.Right && !IsPalette) Panel.DeleteOne(this);
            e.Use();
        }
    }
}
