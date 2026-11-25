using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    public sealed class CommandBlockView : MonoBehaviour, ILayoutElement, ILayoutGroup,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
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
        static readonly string[] Methods = { "distance(", "hp", "is_dead()", "type" };
        static readonly BlockKind[] MemberKinds = { BlockKind.Distance, BlockKind.Hp, BlockKind.IsDead, BlockKind.EnemyTypeOf };
        bool HasDot => kind == BlockKind.Variable && input && input.text.EndsWith(".", StringComparison.Ordinal);
        // Dropdown after `name.`: distance(position), the read-only hp and type properties, is_dead() (a condition: the block
        // turns into a logic block; there is no is_dead() block of its own in the palette). Read by the shown name, since the
        // members not handed out yet are left out of the list; in the 자연어 tab the name is its words (ShownMethod).
        string Member => HasDot && dropdown.value > 0 && dropdown.value < dropdown.options.Count ? MethodOf(dropdown.options[dropdown.value].text) : null;
        static string MethodOf(string shown) { int i = Array.FindIndex(MemberKinds, k => NaturalMember(k) == shown); return i >= 0 ? Methods[i] : shown; }
        string ShownMethod(string method) => naturalShown ? NaturalMember(MemberKinds[Array.IndexOf(Methods, method)]) : method;
        // The member's words between the variable and its argument: "의 체력", "와(과)".
        static string NaturalMember(BlockKind member) => NaturalParts(PythonTreeCompiler.Natural(member))[1];
        static string[] NaturalParts(string template) => template == null ? new string[0] :
            System.Text.RegularExpressions.Regex.Split(template, @"\{[^}]*\}").Select(part => part.Trim()).ToArray();
        public BlockKind Kind { get { int member = Array.IndexOf(Methods, Member); return member < 0 ? kind : MemberKinds[member]; } }
        public BlockKind BaseKind => kind;
        public CommandDropZone Body => body;
        public IReadOnlyList<CommandDropZone> Arguments => arguments;
        public RectTransform Rect => rect;
        // Tutorial targets: the name field and the comparison picker, and what they hold before the line compiles.
        public RectTransform InputRect => input ? input.transform as RectTransform : null;
        public RectTransform OptionRect => dropdown ? dropdown.transform as RectTransform : null;
        public string DraftValue => kind == BlockKind.Comparison ? PythonTreeCompiler.Comparisons[dropdown.value] :
            kind == BlockKind.Arithmetic ? PythonTreeCompiler.Operators[dropdown.value] :
            kind == BlockKind.EnemyTypeName ? PythonTreeCompiler.EnemyTypes[dropdown.value] : input ? input.text : null;
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
                ((kind == BlockKind.Variable || kind == BlockKind.Comparison || kind == BlockKind.Arithmetic || kind == BlockKind.EnemyTypeName) && !dropdown) ||
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
                // After `enemy.` the picker chooses the member: typing on (".distance") made a name no variable has and skipped
                // the lesson's step (the user, 2026-11-19). The name before the dot can still be edited, and backspace takes the dot.
                if (input && kind == BlockKind.Variable) input.onValidateInput = (text, index, c) => text.IndexOf('.') is int dot && dot >= 0 && (c == '.' || index > dot) ? '\0' : c;
                if (input && !IsStatement && input.TryGetComponent<LayoutElement>(out var field))
                {
                    // A field in a value block fits its 28 px pill; its text area has no vertical inset, so 17 px text is not clipped.
                    field.minHeight = field.preferredHeight = FieldHeight;
                    if (input.textViewport) input.textViewport.sizeDelta = new Vector2(kind == BlockKind.Number ? -4 : -12, 0);
                }
                if (dropdown) dropdown.onValueChanged.AddListener(SelectionChanged);
                // The calculation block is a copy of the comparison's (two holes and a picker) with the operators in its picker;
                // both pickers list the compiler's own signs, so a sign added there shows up in every block.
                // `Enemy.` picks a kind the same way, Tank first chosen (the kind a ranged robot hunts).
                if (kind == BlockKind.Arithmetic || kind == BlockKind.Comparison || kind == BlockKind.EnemyTypeName)
                {
                    // A copy is bound as a new block: it keeps the sign it was copied with (the copy under the hand showed == or +).
                    int sign = dropdown.value;
                    dropdown.ClearOptions(); dropdown.AddOptions(new List<string>(kind == BlockKind.Arithmetic ? PythonTreeCompiler.Operators :
                        kind == BlockKind.Comparison ? PythonTreeCompiler.Comparisons : PythonTreeCompiler.EnemyTypes));
                    dropdown.SetValueWithoutNotify(kind == BlockKind.EnemyTypeName && palette ? Array.IndexOf(PythonTreeCompiler.EnemyTypes, "Tank") : sign); dropdown.RefreshShownValue();
                }
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
            ShowNatural(panel.Mode == CodingMode.Natural);
        }

        // 자연어 tab (Docs/코딩_모드_3탭.md): the same block in Korean words. The Python labels and bracket glyphs of the header
        // hide and word labels show between the slots and fields (PythonTreeCompiler.Natural); pickers show their words. Kept
        // in serialized fields so a copy (Instantiate) keeps them.
        [SerializeField, HideInInspector] bool naturalShown, naturalBuilt;
        [SerializeField, HideInInspector] List<GameObject> pythonLabels = new List<GameObject>(), naturalLabels = new List<GameObject>();
        public void ShowNatural(bool natural)
        {
            if (natural && !naturalBuilt) BuildNatural();
            bool changed = natural != naturalShown;
            naturalShown = natural;
            foreach (var label in pythonLabels) if (label) label.SetActive(!natural);
            foreach (var label in naturalLabels) if (label) label.SetActive(natural);
            if (dropdown && kind != BlockKind.Variable && (kind == BlockKind.Comparison || kind == BlockKind.EnemyTypeName))
            {
                int value = dropdown.value;
                dropdown.ClearOptions();
                dropdown.AddOptions(new List<string>(kind == BlockKind.Comparison ? natural ? PythonTreeCompiler.NaturalComparisons : PythonTreeCompiler.Comparisons
                    : natural ? PythonTreeCompiler.NaturalEnemyTypes : PythonTreeCompiler.EnemyTypes));
                dropdown.SetValueWithoutNotify(value); dropdown.RefreshShownValue(); FitDropdown(dropdown);
            }
            if (kind == BlockKind.Variable && changed) RefreshVariableOptions();
            foreach (var zone in arguments) if (zone) zone.ShowNatural(natural);
            if (changed) LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        void BuildNatural()
        {
            naturalBuilt = true;
            var template = PythonTreeCompiler.Natural(kind == BlockKind.Attack ? BlockKind.Shot : kind);
            if (template != null) Words(header, NaturalParts(template), SlotNames(template));
            // enemy.distance(pos): "와(과)" is the picker's; the words after the position go in the argument row.
            var distance = PythonTreeCompiler.Natural(BlockKind.Distance);
            if (methodArguments) Words((RectTransform)methodArguments.transform, new[] { "", NaturalParts(distance)[2] }, new[] { SlotNames(distance)[1] });
        }
        static string[] SlotNames(string template) => System.Text.RegularExpressions.Regex.Matches(template, @"\{([^}]*)\}")
            .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToArray();
        // parts[i] goes before the i-th slot or field of row, the last part after the last one; an empty slot shows names[i].
        void Words(RectTransform row, string[] parts, string[] names)
        {
            var style = row.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.transform.parent == row);
            var fields = new List<Transform>();
            foreach (Transform child in row)
            {
                if (child.GetComponent<CommandDropZone>() || child.GetComponent<TMP_InputField>() || child.GetComponent<TMP_Dropdown>() || child.gameObject == methodArguments) fields.Add(child);
                else if (child.gameObject.activeSelf && (child.GetComponent<TMP_Text>() || child.GetComponent<CommandParenthesis>())) pythonLabels.Add(child.gameObject);
            }
            for (int i = 0; i < Math.Min(fields.Count, names.Length); i++)
                if (fields[i].TryGetComponent<CommandDropZone>(out var slot)) slot.SetNaturalHint(names[i]);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                var label = style ? Instantiate(style, row) : new GameObject("Word", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                label.name = "Natural"; label.transform.SetParent(row, false);
                // A copied bracket label keeps its fixed width: the words would run over the next piece.
                if (label.TryGetComponent<LayoutElement>(out var fixedWidth)) fixedWidth.enabled = false;
                label.text = parts[i]; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                if (!style && UITheme.Bold) { label.font = UITheme.Bold; label.fontSize = 17; label.color = UITheme.Ink; }
                label.transform.SetSiblingIndex(i < fields.Count ? fields[i].GetSiblingIndex() : row.childCount - 1);
                naturalLabels.Add(label.gameObject);
            }
        }

        // 오류 (red) or 에러 (yellow) on this line: a thick frame round its header and a ! badge at its left edge. Neither takes
        // pointer hits, so dragging and slots work as before; the panel shows the message on hover.
        [SerializeField, HideInInspector] CommandProblemFrame problemFrame;
        [SerializeField, HideInInspector] Image problemBadge;
        public string Problem { get; private set; }
        public bool ProblemIsError { get; private set; }
        public void SetProblem(string message, bool error)
        {
            Problem = message; ProblemIsError = error;
            if (message == null) { if (problemFrame) { problemFrame.gameObject.SetActive(false); problemBadge.gameObject.SetActive(false); } return; }
            if (!problemFrame)
            {
                problemFrame = new GameObject("Problem", typeof(RectTransform), typeof(CommandProblemFrame), typeof(LayoutElement)).GetComponent<CommandProblemFrame>();
                problemFrame.GetComponent<LayoutElement>().ignoreLayout = true;
                problemFrame.rectTransform.SetParent(rect, false);
                problemFrame.raycastTarget = false;
                problemBadge = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
                problemBadge.GetComponent<LayoutElement>().ignoreLayout = true;
                problemBadge.rectTransform.SetParent(rect, false);
                problemBadge.sprite = UITheme.Circle; problemBadge.raycastTarget = false;
                var mark = new GameObject("Mark", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                mark.rectTransform.SetParent(problemBadge.rectTransform, false);
                mark.rectTransform.anchorMin = Vector2.zero; mark.rectTransform.anchorMax = Vector2.one; mark.rectTransform.offsetMin = mark.rectTransform.offsetMax = Vector2.zero;
                if (UITheme.Bold) mark.font = UITheme.Bold;
                mark.text = "!"; mark.fontSize = 17; mark.alignment = TextAlignmentOptions.Center; mark.raycastTarget = false;
            }
            var color = error ? ErrorColor : WarningColor;
            problemFrame.color = color; problemBadge.color = color;
            problemBadge.GetComponentInChildren<TMP_Text>().color = error ? Color.white : UITheme.Ink;
            problemFrame.gameObject.SetActive(true); problemBadge.gameObject.SetActive(true);
            problemFrame.transform.SetAsLastSibling(); problemBadge.transform.SetAsLastSibling();
            PlaceProblem();
        }
        public static readonly Color ErrorColor = new Color(.93f, .2f, .22f), WarningColor = new Color(1f, .78f, .1f);
        void PlaceProblem()
        {
            if (!problemFrame || !problemFrame.gameObject.activeSelf) return;
            const float Out = 4;
            var frame = problemFrame.rectTransform;
            frame.anchorMin = new Vector2(0, 1); frame.anchorMax = Vector2.one; frame.pivot = new Vector2(.5f, 1);
            frame.offsetMin = new Vector2(-Out, -headHeight - Out); frame.offsetMax = new Vector2(Out, Out);
            var badge = problemBadge.rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(0, 1); badge.pivot = new Vector2(.5f, .5f);
            badge.sizeDelta = new Vector2(22, 22); badge.anchoredPosition = new Vector2(-Out, -headHeight * .5f);
        }
        public void OnPointerEnter(PointerEventData e) { if (Panel && !IsPalette && !readOnlyPreview) Panel.HoverProblem(this, true); }
        public void OnPointerExit(PointerEventData e) { if (Panel && !IsPalette && !readOnlyPreview) Panel.HoverProblem(this, false); }

        void InputChanged(string value)
        {
            FitInput(input, MinField);
            if (kind == BlockKind.Variable) RefreshVariableOptions();
            if (kind == BlockKind.DeclareVariable)
            {
                try
                {
                    string current = PythonTreeCompiler.NormalizeIdentifier(value);
                    // While the name is another variable's, the uses keep the last one they were given.
                    if (IsPalette || current == RetainedName || Panel.RenameVariable(this, RetainedName, current)) RetainedName = current;
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
            string chosen = Member;
            var options = new List<string> { "메서드 선택" };
            // is_dead() once it is handed out (an item, like the other blocks), type once Enemy.Tank is (nothing to compare it
            // with before), or when this block already is one.
            bool Offered(int i) => Methods[i] == chosen || MemberKinds[i] != BlockKind.IsDead && MemberKinds[i] != BlockKind.EnemyTypeOf ||
                (Panel?.IsAvailable?.Invoke(MemberKinds[i] == BlockKind.IsDead ? BlockKind.IsDead : BlockKind.EnemyTypeName) ?? true);
            if (chosen != null || (Panel && Panel.IsEnemyVariable(VariableName)))
                options.AddRange(Methods.Where((_, i) => Offered(i)).Select(ShownMethod));
            else options[0] = "사용 가능한 메서드 없음";
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(HasDot && chosen != null ? Mathf.Max(0, options.IndexOf(ShownMethod(chosen))) : 0);
            dropdown.RefreshShownValue();
            dropdown.gameObject.SetActive(HasDot);
            // 자연어: "enemy 의 체력", the dot of `enemy.` not drawn.
            input.textComponent.maxVisibleCharacters = naturalShown && HasDot ? input.text.Length - 1 : 99999;
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
            shape.color=UITheme.BlockCategories[body?0:slot==BlockSlotKind.Condition?3:slot==BlockSlotKind.Value?2:1];
        }
        public void SetText(string value) { input.text = value; }

        void PrepareHeader()
        {
            // Blocks with a body widen with what is inside: their header stays at the left, by the body's spine.
            header.GetComponent<HorizontalLayoutGroup>().childAlignment = kind is BlockKind.For or BlockKind.While or BlockKind.If or BlockKind.Elif or BlockKind.Else
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
            outOfStock = counted && count <= 0;
            SetFaded(outOfStock || dragged);
        }
        // Faded while either holds: a drag's end must not brighten a palette block its own drop just used up.
        bool outOfStock, dragged;
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
            if (kind == BlockKind.EnemyTypeName) { dropdown.SetValueWithoutNotify(Array.IndexOf(PythonTreeCompiler.EnemyTypes, data.Value)); FitDropdown(dropdown); }
            int skip = 0;
            if (kind == BlockKind.Variable)
            {
                bool member = Array.IndexOf(MemberKinds, data.Kind) >= 0;
                variableName = member ? data.Arguments[0].Value : data.Value;
                input.SetTextWithoutNotify(variableName + (member ? "." : "")); FitInput(input, MinField);
                dropdown.ClearOptions(); dropdown.AddOptions(new List<string> { "메서드 선택" }); dropdown.AddOptions(new List<string>(Methods));
                dropdown.SetValueWithoutNotify(Array.IndexOf(MemberKinds, data.Kind) + 1);
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
            if (kind == BlockKind.EnemyTypeName) block.Value = PythonTreeCompiler.EnemyTypes[dropdown.value];
            if (kind == BlockKind.Variable)
            {
                if (Kind == BlockKind.Variable) { block.Value = input.text; return block; }
                block.Arguments.Add(new CodeBlock(BlockKind.Variable, VariableName));
                if (Kind == BlockKind.Hp || Kind == BlockKind.IsDead || Kind == BlockKind.EnemyTypeOf) return block;
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
            this.dragged = dragged;
            SetFaded(dragged || outOfStock);
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
        // The snap preview's copy where a dragged block will land: see-through and flat (draft 1, the ghost block).
        public void SetLandingAppearance() { canvasGroup.alpha = .4f; canvasGroup.blocksRaycasts = false; }
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
            PlaceProblem();
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
            else if (e.button == PointerEventData.InputButton.Right && !IsPalette) Panel.RightClick(this, e);
            e.Use();
        }
    }
}
