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
        bool readOnlyPreview;
        bool previewBody;
        float bodyReveal;
        CanvasGroup bodyVisibility;
        public bool BodyExpanded => body && !IsPalette && (previewBody || body.Blocks.Count > 0);
        public float HeaderHeight => headHeight;
        internal float FooterOverlap => CommandBlockShape.ControlFooterHeight * BodyReveal;
        float BodyReveal => !body || IsPalette ? 0 : body.Blocks.Count > 0 ? 1 : bodyReveal;
        // Value and condition blocks are as tall as what they hold plus a margin, at least ExpressionHeight (PR #19's 38), and as
        // wide as their contents (play feedback 3); a line is 52 high (PR #19) unless it holds a taller one. A value block holding
        // another keeps NestPad above and below it, so the two do not run together (play feedback 4).
        public const float ExpressionHeight = 38, ExpressionPad = 6, NestPad = 7, FieldHeight = ExpressionHeight - 2 * ExpressionPad;
        bool IsStatement => PythonTreeCompiler.OutputSlot(Kind) == BlockSlotKind.Statement;
        float headHeight => IsStatement ? Mathf.Max(52, LayoutUtility.GetPreferredHeight(header) + 12) : ExpressionHead(LayoutUtility.GetPreferredHeight(header));
        static float ExpressionHead(float content) => Mathf.Max(ExpressionHeight, content + 2 * (content > FieldHeight + .5f ? NestPad : ExpressionPad));
        // Room left and right of the header: a number's digits sit close in their pill; a condition clears its pointed ends.
        float Margin => IsStatement ? 14 : kind == BlockKind.Number ? 8 : PythonTreeCompiler.OutputSlot(Kind) == BlockSlotKind.Condition ? 14 : 10;
        public float minWidth => preferredWidth;
        public float preferredWidth => Mathf.Max(IsStatement ? 100 : 36, Mathf.Max(LayoutUtility.GetPreferredWidth(header) + 2 * Margin, BodyReveal > 0 ? body.preferredWidth + CommandBlockShape.ControlSpineWidth : 0));
        public float flexibleWidth => 0;
        public float minHeight => preferredHeight;
        public float preferredHeight => headHeight + (body ? body.preferredHeight * BodyReveal + FooterOverlap : 0);
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
            previewBody = false; bodyReveal = 0;
            EnsureBodyVisibility();
            if (stockLabel) stockLabel.gameObject.SetActive(palette && panel.Remaining != null);
            // Disable the handlers too so palette inputs forward drag/click events to the block.
            if (input) { input.interactable = !palette; input.enabled = !palette; }
            if (dropdown) { dropdown.interactable = !palette; dropdown.enabled = !palette; }
            if (!initialized)
            {
                initialized = true;
                PrepareHeader();
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
            var slot=PythonTreeCompiler.OutputSlot(Kind);
            shape.color=body?new Color(.27f,.72f,.9f):slot==BlockSlotKind.Condition?new Color(.73f,.53f,.94f):
                slot==BlockSlotKind.Value?new Color(1f,.76f,.3f):new Color(.97f,.43f,.57f);
        }
        public void SetText(string value) { input.text = value; }

        void PrepareHeader()
        {
            header.GetComponent<HorizontalLayoutGroup>().childAlignment = kind is BlockKind.For or BlockKind.While
                ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            foreach (var label in header.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.transform.parent != header || !label.gameObject.activeSelf) continue;
                label.alignment = TextAlignmentOptions.Center;
                CommandParenthesis.ReplaceIn(label);
            }
            if (!input) return;
            input.textComponent.alignment = TextAlignmentOptions.Center;
            input.textComponent.margin = Vector4.zero;
            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.alignment = TextAlignmentOptions.Center;
                placeholder.margin = Vector4.zero;
            }
            FitInput(input, MinField);
        }

        // Reward/tutorial previews share the editor's geometry and layout, without editing callbacks.
        public void PreparePreview()
        {
            readOnlyPreview=true; IsPalette=false; Panel=null; Owner=null;
            PrepareHeader();
            if(stockLabel)stockLabel.gameObject.SetActive(false);
            if(input){input.interactable=false;input.enabled=false;}
            if(dropdown){dropdown.interactable=false;dropdown.enabled=false;}
            canvasGroup.alpha=1;canvasGroup.interactable=false;canvasGroup.blocksRaycasts=false;
            previewBody=false;bodyReveal=0;
            EnsureBodyVisibility();
            RefreshShape();shape.HeaderHeight=headHeight;
        }
        internal void PreviewBody(bool value)
        {
            if (!body || IsPalette || readOnlyPreview || previewBody == value) return;
            previewBody = value;
            // Make the target available on the first drag event, before its opening animation finishes.
            if(value)bodyReveal=Mathf.Max(.01f,bodyReveal);
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        void EnsureBodyVisibility()
        {
            if(!body || bodyVisibility)return;
            bodyVisibility=body.GetComponent<CanvasGroup>();
            if(!bodyVisibility)bodyVisibility=body.gameObject.AddComponent<CanvasGroup>();
        }
        public void SetStock(int count)
        {
            if (!stockLabel) return;
            bool counted = IsPalette && Panel.Remaining != null;
            stockLabel.text = count >= int.MaxValue / 2 ? "∞" : "×" + count; // unlimited stock minus blocks in use is still unlimited
            stockLabel.gameObject.SetActive(counted);
            // Out-of-stock palette blocks stay visible (so players learn they exist) but read as unavailable.
            if (counted) SetFaded(count <= 0);
        }
        void LateUpdate()
        {
            if(body){
                float target=BodyExpanded?1:0;
                float reveal=Mathf.MoveTowards(bodyReveal,target,Time.unscaledDeltaTime/ .13f);
                if(!Mathf.Approximately(reveal,bodyReveal)){bodyReveal=reveal;LayoutRebuilder.MarkLayoutForRebuild(rect);}
            }
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
            if (body) {
                float reveal=BodyReveal;
                // The collapsed strip remains a tutorial/click target at the bottom of the header.
                float bodyInset = reveal > 0 ? CommandBlockShape.ControlSpineWidth : 24;
                Place(body.Rect, bodyInset, reveal > 0 ? headHeight : headHeight - 8,
                    rect.rect.width - bodyInset - (reveal > 0 ? 0 : 8), reveal > 0 ? body.preferredHeight * reveal : 8);
                if(bodyVisibility){bodyVisibility.alpha=reveal;bodyVisibility.blocksRaycasts=!IsPalette&&!readOnlyPreview;}
                shape.Configure(PythonTreeCompiler.OutputSlot(Kind),reveal>0,true);
            }
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
        public void OnBeginDrag(PointerEventData e) { if (!readOnlyPreview && e.button == PointerEventData.InputButton.Left) { BringToFront(); Panel.BeginDrag(this, e); } }
        public void OnDrag(PointerEventData e) { if(!readOnlyPreview)Panel.MoveDrag(e); }
        public void OnEndDrag(PointerEventData e)
        {
            if (!readOnlyPreview && Panel.Dragged == this) Panel.FinishDrag(e);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if(readOnlyPreview)return;
            if (e.button == PointerEventData.InputButton.Left && IsPalette) Panel.AddFromPalette(this);
            else if (e.button == PointerEventData.InputButton.Right && !IsPalette) Panel.RightClick(this);
            e.Use();
        }
    }
}
