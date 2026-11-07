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
        [SerializeField] string variableName = "variable";
        public CommandCodingPanel Panel { get; private set; }
        public CommandDropZone Owner { get; private set; }
        public bool IsPalette { get; private set; }
        static readonly string[] Methods = { "distance(", "hp" };
        bool HasDot => kind == BlockKind.Variable && input && input.text.EndsWith(".", StringComparison.Ordinal);
        // Dropdown after `name.`: 1 = distance(position), 2 = the read-only hp property.
        public BlockKind Kind => !HasDot ? kind : dropdown.value == 1 ? BlockKind.Distance : dropdown.value == 2 ? BlockKind.Hp : kind;
        public BlockKind BaseKind => kind;
        public CommandDropZone Body => body;
        public IReadOnlyList<CommandDropZone> Arguments => arguments;
        public RectTransform Rect => rect;
        // Tutorial targets: the name field and the comparison picker, and what they hold before the line compiles.
        public RectTransform InputRect => input ? input.transform as RectTransform : null;
        public RectTransform OptionRect => dropdown ? dropdown.transform as RectTransform : null;
        public string DraftValue => kind == BlockKind.Comparison ? PythonTreeCompiler.Comparisons[dropdown.value] :
            kind == BlockKind.Arithmetic ? PythonTreeCompiler.Operators[dropdown.value] : input ? input.text : null;
        public string VariableName => kind == BlockKind.DeclareVariable ? input.text :
            kind == BlockKind.Variable ? (HasDot ? input.text.Substring(0, input.text.Length - 1) : input.text) : variableName;
        public string RetainedName { get; private set; }
        public bool TextInputFocused => input && input.isFocused;
        bool initialized;
        // Value and condition blocks are as tall as what they hold plus a small margin, at least ExpressionHeight, and as wide as
        // their contents (play feedback 3: a 122x32 number filled a line's header); a line is 46 high unless it holds a taller one.
        // A value block holding another keeps NestPad above and below it, so the two outlines do not run together (play feedback 4:
        // get_my_pos() inside distance( touched its holder's edge).
        public const float ExpressionHeight = 28, ExpressionPad = 3, NestPad = 6, FieldHeight = ExpressionHeight - 2 * ExpressionPad;
        bool IsStatement => PythonTreeCompiler.OutputSlot(Kind) == BlockSlotKind.Statement;
        float headHeight => IsStatement ? Mathf.Max(46, LayoutUtility.GetPreferredHeight(header) + 12) : ExpressionHead(LayoutUtility.GetPreferredHeight(header));
        static float ExpressionHead(float content) => Mathf.Max(ExpressionHeight, content + 2 * (content > FieldHeight + .5f ? NestPad : ExpressionPad));
        // Room left and right of the header: a number's digits sit close in their pill; a condition clears its pointed ends.
        float Margin => IsStatement ? 14 : kind == BlockKind.Number ? 8 : PythonTreeCompiler.OutputSlot(Kind) == BlockSlotKind.Condition ? 14 : 10;
        public float minWidth => preferredWidth;
        public float preferredWidth => Mathf.Max(IsStatement ? 100 : 36, Mathf.Max(LayoutUtility.GetPreferredWidth(header) + 2 * Margin, body ? body.preferredWidth + 32 : 0));
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
                ((kind == BlockKind.Number || kind == BlockKind.DeclareVariable || kind == BlockKind.Variable) && !input) ||
                ((kind == BlockKind.Variable || kind == BlockKind.Comparison || kind == BlockKind.Arithmetic) && !dropdown) ||
                (PythonTreeCompiler.HasBody(kind) && !body) || (stockLabel && !stockColumn))
                throw new InvalidOperationException(name + ": Inspector 참조가 누락되었습니다.");
            Panel = panel;
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
                // Only a Python name can be typed (a name like "123" left the code unapplied without a word).
                if (input && kind == BlockKind.DeclareVariable) input.onValidateInput = (text, index, c) => c == '_' || char.IsLetter(c) || (char.IsDigit(c) && index > 0) ? c : '\0';
                if (input && !IsStatement && input.TryGetComponent<LayoutElement>(out var field))
                {
                    // A field in a value block fits its 28 px pill; its text area has no vertical inset, so 17 px text is not clipped.
                    field.minHeight = field.preferredHeight = FieldHeight;
                    if (input.textViewport) input.textViewport.sizeDelta = new Vector2(kind == BlockKind.Number ? -4 : -12, 0);
                }
                if (dropdown) dropdown.onValueChanged.AddListener(SelectionChanged);
                // The calculation block is a copy of the comparison's (two holes and a picker) with the operators in its picker.
                if (kind == BlockKind.Arithmetic)
                { dropdown.ClearOptions(); dropdown.AddOptions(new List<string>(PythonTreeCompiler.Operators)); dropdown.SetValueWithoutNotify(0); dropdown.RefreshShownValue(); }
                // Pieces of a value block (enemy., distance(, a held block) 8 apart, not touching (play feedback 4).
                if (!IsStatement && header.TryGetComponent<HorizontalLayoutGroup>(out var row)) row.spacing = 8;
            }
            foreach (var argument in arguments) argument.Bind(panel, this);
            if (body) body.Bind(panel, this);
            if (kind == BlockKind.DeclareVariable)
            {
                try { RetainedName = PythonTreeCompiler.NormalizeIdentifier(input.text); }
                catch (FormatException) { }
            }
            if (kind == BlockKind.Variable) RefreshVariableOptions(); else RefreshShape();
            SetDragAppearance(false);
        }

        void InputChanged(string value)
        {
            FitInput(input, MinField);
            if (kind == BlockKind.Variable) RefreshVariableOptions();
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
            if (kind == BlockKind.Variable) RefreshVariableOptions();
            FitDropdown(dropdown);
            if (!IsPalette) Panel.Changed();
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        public void SetVariable(string value)
        {
            variableName = value;
            if (kind == BlockKind.Variable)
            {
                input.SetTextWithoutNotify(value + (HasDot ? "." : "")); FitInput(input, MinField);
                RefreshVariableOptions();
            }
        }
        public void RefreshVariableOptions()
        {
            int selection = dropdown.value;
            var options = new List<string> { "메서드 선택" };
            if (selection > 0 || (Panel && Panel.IsEnemyVariable(VariableName))) options.AddRange(Methods);
            else options[0] = "사용 가능한 메서드 없음";
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(HasDot ? Mathf.Min(selection, options.Count - 1) : 0);
            dropdown.RefreshShownValue();
            dropdown.gameObject.SetActive(HasDot);
            RefreshShape();
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        float MinField => kind == BlockKind.Number ? 16 : 40;
        // A typed field as wide as its text (or its placeholder) plus its text area's insets and the caret, at least min (play
        // feedback 3: fields kept their authored widths, a lone 0 in a 94 px box and long names cut off).
        public static void FitInput(TMP_InputField field, float min)
        {
            if (!field || !field.textComponent || !field.TryGetComponent<LayoutElement>(out var layout)) return;
            string shown = field.text.Length > 0 ? field.text : field.placeholder is TMP_Text hint ? hint.text : "";
            float inset = field.textViewport ? -field.textViewport.sizeDelta.x : 0;
            float width = Mathf.Ceil(Mathf.Max(min, field.textComponent.GetPreferredValues(shown).x + inset + 4));
            if (Mathf.Approximately(layout.preferredWidth, width)) return;
            layout.minWidth = layout.preferredWidth = width;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)field.transform);
        }
        // A picker (<=, distance() as wide as its choice plus its arrow, like the typed fields.
        static void FitDropdown(TMP_Dropdown list)
        {
            if (!list || !list.captionText || !list.TryGetComponent<LayoutElement>(out var layout)) return;
            float width = Mathf.Ceil(list.captionText.GetPreferredValues(list.captionText.text).x - list.captionText.rectTransform.sizeDelta.x + 4);
            if (Mathf.Approximately(layout.preferredWidth, width)) return;
            layout.minWidth = layout.preferredWidth = width;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)list.transform);
        }
        void RefreshShape()
        {
            FitInput(input, MinField); FitDropdown(dropdown);
            if (methodArguments) methodArguments.SetActive(Kind == BlockKind.Distance);
            shape.Configure(PythonTreeCompiler.OutputSlot(Kind), body);
        }
        public void SetText(string value) { input.text = value; }
        public void SetStock(int count)
        {
            if (!stockLabel) return;
            bool counted = IsPalette && Panel.Remaining != null;
            stockLabel.text = count >= int.MaxValue / 2 ? "∞" : "×" + count; // unlimited stock minus blocks in use is still unlimited
            stockLabel.gameObject.SetActive(counted);
            // Out-of-stock palette blocks stay visible (so players learn they exist) but read as unavailable.
            if (counted) SetFaded(count <= 0);
        }
        // The look of the block's palette tab (UITheme.BlockCategories): the face in its colour with white Pretendard Bold labels, and
        // value holes, inputs and the loop's mouth in paper with ink text, so they read as places to fill (play feedback: the dark
        // holes looked disabled). Placed blocks are copies of palette blocks and keep it.
        internal void Style(Color face)
        {
            // A number is a literal: a paper pill with its digits in ink, not an orange block round a white field.
            bool literal = kind == BlockKind.Number;
            shape.color = literal ? UITheme.Paper : face;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                var list = graphic.GetComponentInParent<ScrollRect>(true);
                if (graphic == shape || graphic == stockLabel || graphic.GetComponentInParent<CommandBlockView>(true) != this ||
                    list && list.transform.IsChildOf(transform)) continue; // a dropdown's option list keeps its own look
                if (graphic is TMP_Text text)
                {
                    if (UITheme.Bold) text.font = UITheme.Bold;
                    if (text.name == "Hint") text.color = UITheme.Muted;
                    else if (text.name == "Arrow") text.color = UITheme.Ink;
                }
                // The loop's mouth is see-through, so the C reads as one shape round the code it holds (play feedback: its paper
                // panel and outline looked broken, with a white bar over the foot).
                else if (graphic.name == "Body") graphic.color = Color.clear;
                else if (graphic is CommandBlockShape) graphic.color = UITheme.Paper;
            }
            // The fields' colour tints multiply the paper; their dark tints made the inputs look like holes. Rounded (7 px) so two fields
            // side by side (enemy. and distance( in a condition) read as two places to fill, not one white bar; a palette block's
            // fields are not interactable, and the disabled tint turned them grey.
            foreach (var field in new Selectable[] { input, dropdown })
            {
                if (!field) continue;
                if (field.targetGraphic is Image box && UITheme.Fill)
                { box.sprite = UITheme.Fill; box.type = Image.Type.Sliced; box.pixelsPerUnitMultiplier = 16f / 7; }
                if (field.targetGraphic) field.targetGraphic.color = UITheme.Paper;
                field.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(.96f, .96f, .96f), pressedColor = new Color(.86f, .86f, .86f),
                    selectedColor = Color.white, disabledColor = Color.white, colorMultiplier = 1, fadeDuration = .08f };
            }
            if (literal && input && input.targetGraphic) input.targetGraphic.color = Color.clear;
            if (input && input.textComponent)
            {
                input.textComponent.color = UITheme.Ink;
                if (literal) input.textComponent.alignment = TextAlignmentOptions.Center;
            }
            if (stockLabel)
            {
                if (UITheme.Bold) stockLabel.font = UITheme.Bold;
                stockLabel.fontStyle &= ~FontStyles.Bold; stockLabel.alignment = TextAlignmentOptions.Center; stockLabel.color = UITheme.Ink;
                stockLabel.rectTransform.pivot = new Vector2(.5f, .5f); stockLabel.rectTransform.sizeDelta = new Vector2(40, 28);
            }
            if (input && input.placeholder is Graphic placeholder) placeholder.color = UITheme.Muted;
            if (dropdown && dropdown.captionText) dropdown.captionText.color = UITheme.Ink;
        }
        void LateUpdate()
        {
            if (!IsPalette || !stockLabel || !stockLabel.gameObject.activeSelf) return;
            // One count column at the palette's right edge for every block, each count centred in it and on its block's first line.
            var edge = rect.InverseTransformPoint(stockColumn.TransformPoint(new Vector3(stockColumn.rect.xMax - 8, 0, 0)));
            var label = stockLabel.rectTransform;
            label.anchoredPosition = new Vector2(edge.x - rect.rect.xMax - label.rect.width * .5f, -Mathf.Min(rect.rect.height, headHeight) * .5f);
        }
        public void SelectOption(int value) { dropdown.value = value; }
        internal void LoadData(CodeBlock data)
        {
            if (kind == BlockKind.Number || kind == BlockKind.DeclareVariable) { input.SetTextWithoutNotify(data.Value); FitInput(input, MinField); }
            if (kind == BlockKind.DeclareVariable) RetainedName = data.Value;
            if (kind == BlockKind.Comparison) { dropdown.SetValueWithoutNotify(Array.IndexOf(PythonTreeCompiler.Comparisons, data.Value)); FitDropdown(dropdown); }
            if (kind == BlockKind.Arithmetic) { dropdown.SetValueWithoutNotify(Array.IndexOf(PythonTreeCompiler.Operators, data.Value)); FitDropdown(dropdown); }
            int skip = 0;
            if (kind == BlockKind.Variable)
            {
                bool member = data.Kind == BlockKind.Distance || data.Kind == BlockKind.Hp;
                variableName = member ? data.Arguments[0].Value : data.Value;
                input.SetTextWithoutNotify(variableName + (member ? "." : "")); FitInput(input, MinField);
                dropdown.ClearOptions(); dropdown.AddOptions(new List<string> { "메서드 선택" }); dropdown.AddOptions(new List<string>(Methods));
                dropdown.SetValueWithoutNotify(data.Kind == BlockKind.Distance ? 1 : data.Kind == BlockKind.Hp ? 2 : 0);
                RefreshVariableOptions();
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
            if (kind == BlockKind.Comparison) block.Value = PythonTreeCompiler.Comparisons[dropdown.value];
            if (kind == BlockKind.Arithmetic) block.Value = PythonTreeCompiler.Operators[dropdown.value];
            if (kind == BlockKind.Variable)
            {
                if (Kind == BlockKind.Variable) { block.Value = input.text; return block; }
                block.Arguments.Add(new CodeBlock(BlockKind.Variable, VariableName));
                if (Kind == BlockKind.Hp) return block;
            }
            foreach (var argument in arguments) block.Arguments.Add(argument.ReadArgument());
            if (body) block.Body.AddRange(body.Read());
            return block;
        }
        public IEnumerable<CommandBlockView> Descendants()
        {
            yield return this;
            if (kind == BlockKind.Variable && Kind != BlockKind.Distance) yield break;
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
        // The block left behind while dragged fades; the copy under the cursor is the block lifted, a little larger on a
        // deeper shadow (비주얼_레퍼런스.md 4).
        // A copy in a robot's code tooltip (DefenseActorView, PR #19): read-only, no stock count, no panel or palette.
        public void PreparePreview()
        {
            IsPalette = false; Panel = null; Owner = null;
            if (stockLabel) stockLabel.gameObject.SetActive(false);
            if (input) { input.interactable = false; input.enabled = false; }
            if (dropdown) { dropdown.interactable = false; dropdown.enabled = false; }
            canvasGroup.alpha = 1; canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false;
        }
        public void SetDragAppearance(bool dragged)
        {
            SetFaded(dragged);
            canvasGroup.blocksRaycasts = !dragged;
        }
        // Shapes mix towards the paper (CommandBlockShape.Fade, opaque); texts and fields dim through their renderers.
        void SetFaded(bool faded)
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.GetComponentInParent<CommandBlockView>(true) != this) continue;
                if (graphic is CommandBlockShape part) { part.Fade = faded ? .65f : 0; part.SetVerticesDirty(); }
                else graphic.canvasRenderer.SetAlpha(faded ? .45f : 1);
            }
        }
        public void SetGhostAppearance()
        {
            canvasGroup.alpha = 1; canvasGroup.blocksRaycasts = false;
            rect.localScale = Vector3.one * 1.05f;
            shape.Lifted = true; shape.SetVerticesDirty();
        }
        public void JoinNext(bool joined) { shape.JoinNext = joined; shape.SetVerticesDirty(); }
        public void SetLayoutHorizontal()
        {
            float inset = IsStatement ? 6 : ExpressionPad;
            Place(header, Margin, inset, rect.rect.width - 2 * Margin, headHeight - 2 * inset);
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
