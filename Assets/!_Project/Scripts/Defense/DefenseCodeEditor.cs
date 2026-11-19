using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class DefenseCodeEditor : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] CommandCodingPanel panel;
        [SerializeField] GameObject editorRoot;
        [SerializeField] TMP_Text title, inventory;
        [SerializeField] Button closeButton, automaticButton;
        // Optional: SetHint is a no-op without it.
        [SerializeField, Tooltip("'비우기' 오른쪽 힌트 버튼. 튜토리얼이 힌트를 줄 때만 보입니다.")] Button hintButton;
        [SerializeField] GameObject hintPopup;
        [SerializeField] TMP_Text hintText;
        int editingId;
        DefenseSimulation editingSession;
        DefenseSimulation draftSession;
        readonly Dictionary<int, CommandCodingPanel.EditHistory> drafts = new Dictionary<int, CommandCodingPanel.EditHistory>();
        public bool IsOpen => editorRoot && editorRoot.activeSelf;
        public bool HintOpen => IsOpen && hintPopup && hintPopup.activeSelf;
        public int EditingId => editingId;
        public CommandCodingPanel Panel => panel;
        bool HasCurrentSession => editingSession != null && editingSession == battle.Simulation;
        void Start()
        {
            if (!battle || !panel || !editorRoot || !title || !inventory || !closeButton || !automaticButton)
                throw new InvalidOperationException("DefenseCodeEditor Inspector 참조를 연결하세요.");
            panel.ApplyToTarget = Apply;
            panel.BlockDropped += OnBlockDropped;
            panel.BlockDropped += (kind, source, destination) => UISound.Play("block-drop");
            panel.Feedback += kind => UISound.Play(kind);
            panel.CanCopy = () => battle.Stage >= 2; // the right-click menu offers 복사 and 붙여넣기 from Stage 2 (삭제 only in Stage 1)
            panel.Copied += () => battle.ReportTutorialAction(TutorialKeys.DropZone, TutorialSignals.CodeCopied);
            panel.CategoryChosen += category => battle.ReportTutorialAction(TutorialKeys.PaletteTab(category), TutorialSignals.TargetClicked);
            // A kind the player owns stays on the palette when other robots use all of it (it vanished, as if not unlocked,
            // while one this robot used up stayed as ×0): the count beside it says none is left.
            panel.IsAvailable = kind => editingSession == null || editingSession.Options.unlockAll || editingSession.Owned(kind) > 0 || editingSession.Available(kind, editingId) > 0;
            panel.Remaining = kind => editingSession == null ? 0 : Math.Max(0, editingSession.Available(kind, editingId) - panel.Program.AllBlocks().Count(b => DefenseProgression.Canonical(b.Kind) == DefenseProgression.Canonical(kind)));
            // A click on the block a tutorial step's demo drags puts it where the demo drops it (1-3 C08: slash() inside the while).
            panel.PaletteClicking = source =>
            {
                foreach (var ends in (TutorialDemo ?? "").Split('|').Select(pair => pair.Split('>')).Where(ends => ends.Length == 2))
                    if (TutorialPaletteItem(ends[0].Trim()) == source.Rect) { SelectTutorialSlot(ends[1].Trim()); return; }
            };
            closeButton.onClick.AddListener(() => Close());
            automaticButton.onClick.AddListener(RestoreAutomatic);
            if (hintButton) hintButton.onClick.AddListener(ToggleHint);
            SetHint(null);
            if (!baked) Bake();
            editorRoot.SetActive(false);
        }
        // The top row's buttons together at the right (play feedback 3: 비우기 sat 130 px from 닫기): 닫기, then 비우기, then the
        // tutorial's hint, 10 px apart, 비우기 in Jalnan like 닫기. The block guide's i (DefenseBlockHelp) follows the inventory's
        // title instead of floating alone over the card's corner.
        // Built once in the editor and saved with the scene or prefab (13차: the editor showed the old look until Play); at run
        // time only in one that was never baked.
        [SerializeField, HideInInspector] bool baked;
        public bool Baked => baked;
        public void Bake()
        {
            if (baked) return;
            inventory.text = "블록 인벤토리";
            Arrange();
            baked = true;
        }
        void Arrange()
        {
            var clear = (RectTransform)automaticButton.transform;
            LeftOf(clear, (RectTransform)closeButton.transform, 10);
            if (hintButton) LeftOf((RectTransform)hintButton.transform, clear, 10);
            var label = automaticButton.GetComponentInChildren<TMP_Text>();
            if (label && UITheme.Display) { label.font = UITheme.Display; label.fontStyle &= ~FontStyles.Bold; }
            if (hintButton) Like(hintButton, automaticButton);
            var info = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Info" && b.transform.parent == panel.transform);
            if (!info) return;
            var rect = (RectTransform)info.transform; var heading = inventory.rectTransform; var size = rect.rect.size;
            rect.anchorMin = rect.anchorMax = heading.anchorMin; rect.pivot = new Vector2(0, .5f); rect.sizeDelta = size;
            rect.anchoredPosition = heading.anchoredPosition + new Vector2(-heading.pivot.x * heading.rect.width + inventory.preferredWidth + 10, (.5f - heading.pivot.y) * heading.rect.height);
        }
        // The hint button in 비우기's look with its word centred (play feedback 4: a dark box with the word at its right edge).
        static void Like(Button button, Button model)
        {
            if (button.targetGraphic is Image image && model.targetGraphic is Image look)
            { image.sprite = look.sprite; image.type = look.type; image.pixelsPerUnitMultiplier = look.pixelsPerUnitMultiplier; image.color = look.color; }
            button.transition = model.transition; button.colors = model.colors; button.spriteState = model.spriteState;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true), font = model.GetComponentInChildren<TMP_Text>(true);
            if (!text || !font) return;
            text.font = font.font; text.fontSharedMaterial = font.fontSharedMaterial; text.fontSize = font.fontSize;
            text.fontStyle = font.fontStyle; text.color = font.color; text.alignment = TextAlignmentOptions.Center;
            var box = text.rectTransform; box.anchorMin = Vector2.zero; box.anchorMax = Vector2.one; box.offsetMin = box.offsetMax = Vector2.zero;
        }
        // rect's right edge gap left of anchor's left edge, on the same row; both under one parent, anchor anchored at a point.
        static void LeftOf(RectTransform rect, RectTransform anchor, float gap)
        {
            var size = rect.rect.size;
            rect.anchorMin = rect.anchorMax = anchor.anchorMin; rect.pivot = new Vector2(1, .5f); rect.sizeDelta = size;
            rect.anchoredPosition = anchor.anchoredPosition + new Vector2(-anchor.pivot.x * anchor.rect.width - gap, (.5f - anchor.pivot.y) * anchor.rect.height);
        }
        public void Open(int id)
        {
            var sim = battle.Simulation;
            var robot = sim?.FindRobot(id);
            if (robot == null || !sim.CanEdit) return;
            if (IsOpen && editingId == id && editingSession == sim) return;
            Close(false); editingSession = sim; editingId = id;
            if (draftSession != sim) { drafts.Clear(); draftSession = sim; }
            foreach (int removed in drafts.Keys.Where(key => !sim.Robots.Exists(r => r.Id == key)).ToArray()) drafts.Remove(removed);
            title.text = "미리보기 · " + battle.RobotName(id);
            battle.BeginCodeView(robot);
            editorRoot.SetActive(true); UISound.Play("open");
            if (drafts.TryGetValue(id, out var history)) panel.LoadHistory(history);
            else panel.LoadProgram(robot.Program?.Name ?? "robot_" + id, robot.Program?.CopyBlocks() ?? Array.Empty<CodeBlock>());
            panel.ShowCategory(1); panel.RefreshPalette();
            ShowHint(); // this robot's own hint
            if (panel.IsApplied) panel.ShowMessage("자동 적용됨 · Ctrl+Z 실행 취소 · Ctrl+Y 다시 실행");
        }
        string Apply(string name, IReadOnlyList<CodeBlock> blocks)
        {
            if (!IsOpen || !HasCurrentSession) throw new FormatException("로봇을 다시 선택하세요.");
            // Emptying the robot does not depend on the name field: under a name Python rejects the last code kept running
            // while the editor said the robot does nothing.
            if (blocks.Count == 0) try { PythonTreeCompiler.NormalizeIdentifier(name); } catch (FormatException) { name = "robot_" + editingId; }
            editingSession.ApplyProgram(editingId, name, blocks);
            // The blocks in the editor stay this robot's even when the code is unfinished and nothing was applied, except a
            // kind the stock cannot cover (an undo after another robot took the blocks): that kind holds none, or the shared
            // stock went below zero and the other robot's applied code no longer fit.
            editingSession.SetDraft(editingId, panel.Program.AllBlocks().Select(b => DefenseProgression.Canonical(b.Kind)).GroupBy(k => k)
                .Where(g => g.Count() <= editingSession.Available(g.Key, editingId)).SelectMany(g => g));
            panel.RefreshPalette();
            battle.NotifyCodeApplied(editingId, blocks.Count == 0);
            return blocks.Count == 0 ? "코드 없음 · 행동하지 않음" : "자동 적용됨";
        }
        void OnBlockDropped(BlockKind kind, RectTransform source, RectTransform destination)
            => battle.ReportTutorialDrop(source, destination, "BlockDropped:" + DefenseProgression.Canonical(kind));
        public void Close(bool resume = true)
        {
            if (resume && !battle.TutorialAllows("Editor.Close")) return;
            bool wasOpen = IsOpen;
            if (IsOpen && HasCurrentSession && editingSession.Robots.Exists(r => r.Id == editingId))
                drafts[editingId] = panel.History;
            if (IsOpen) { battle.EndCodeView(); UISound.Play("close"); }
            if (editorRoot) editorRoot.SetActive(false);
            editingId = 0; editingSession = null;
            if (resume && wasOpen) battle.ReportTutorialAction("Editor.Close", "TargetClicked", "EditorClosed");
        }
        // Code shown by the hint button; null or empty hides the button. earlier: the hint codes before it in play order. The
        // robot being edited gets the last of them that uses its own action (play feedback: in 2-7 the warrior was shown the
        // shooter's shoot(enemy) code), as a picture of its blocks when TutorialHintPictures has made one (text code read hard).
        string[] hints = Array.Empty<string>();
        public void SetHint(string code, IEnumerable<string> earlier = null)
        {
            if (!hintButton) return;
            bool any = !string.IsNullOrWhiteSpace(code);
            hintButton.gameObject.SetActive(any);
            hints = any ? (earlier ?? Enumerable.Empty<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Append(code).ToArray() : Array.Empty<string>();
            ShowHint();
            if (hintPopup) hintPopup.SetActive(false);
        }
        void ToggleHint()
        {
            if (!battle.TutorialAllows(TutorialKeys.Hint) || !hintPopup) return;
            ShowHint();
            hintPopup.SetActive(!hintPopup.activeSelf);
            battle.ReportTutorialAction(TutorialKeys.Hint, TutorialSignals.TargetClicked);
        }
        public string HintCode
        {
            get
            {
                var robot = IsOpen && HasCurrentSession ? editingSession.FindRobot(editingId) : null;
                var action = robot == null ? (BlockKind?)null : DefenseProgression.ActionBlock(robot.Spec.NativeAction);
                string call = action == null ? null : (action == BlockKind.Shot ? "shoot" : action.ToString().ToLowerInvariant()) + "(";
                return hints.LastOrDefault(c => call != null && c.Contains(call)) ?? hints.LastOrDefault() ?? "";
            }
        }
        // The picture's Resources path for a hint code (TutorialHintPictures saves it there): named by the code itself, so an
        // edited hint shows as text until the pictures are made again rather than as an old picture.
        public static string HintPicture(string code)
        {
            uint hash = 2166136261;
            foreach (char c in (code ?? "").Replace("\r", "").Trim()) hash = (hash ^ c) * 16777619;
            return "TutorialHints/hint_" + hash.ToString("x8");
        }
        Image hintPicture; TextMeshProUGUI hintTitle; Outline hintFrame; Vector2 hintTextSize;
        const float HintPad = 12, HintTitle = 38; static readonly Vector2 HintMax = new Vector2(760, 640);
        void ShowHint()
        {
            string code = HintCode;
            if (hintText) hintText.text = code;
            if (!hintPopup) return;
            var sprite = code.Length > 0 ? Resources.Load<Sprite>(HintPicture(code)) : null;
            var popup = (RectTransform)hintPopup.transform;
            if (hintTextSize == default) hintTextSize = popup.sizeDelta;
            if (sprite && !hintPicture)
            {
                hintPicture = new GameObject("HintPicture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var rect = hintPicture.rectTransform; rect.SetParent(popup, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * HintPad; rect.offsetMax = new Vector2(-HintPad, -HintPad - HintTitle);
                hintPicture.preserveAspect = true; hintPicture.raycastTarget = false;
                // A title and a sky frame, so the picture's blocks do not read as the player's own code under it.
                hintTitle = new GameObject("HintTitle", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                var title = hintTitle.rectTransform; title.SetParent(popup, false);
                title.anchorMin = new Vector2(0, 1); title.anchorMax = Vector2.one; title.pivot = new Vector2(.5f, 1);
                title.offsetMin = new Vector2(HintPad + 4, -HintPad - HintTitle); title.offsetMax = new Vector2(-HintPad, -HintPad);
                if (UITheme.Display) hintTitle.font = UITheme.Display;
                hintTitle.fontSize = 22; hintTitle.color = UITheme.Sky; hintTitle.alignment = TextAlignmentOptions.MidlineLeft; hintTitle.raycastTarget = false;
                if (hintPopup.TryGetComponent<Graphic>(out var back)) { hintFrame = back.gameObject.AddComponent<Outline>(); hintFrame.effectColor = UITheme.Sky; hintFrame.effectDistance = new Vector2(3, -3); }
            }
            if (hintPicture) { hintPicture.sprite = sprite; hintPicture.gameObject.SetActive(sprite); hintTitle.gameObject.SetActive(sprite); }
            if (hintFrame) hintFrame.enabled = sprite;
            if (hintText) hintText.gameObject.SetActive(!sprite);
            if (!sprite) { popup.sizeDelta = hintTextSize; return; }
            hintTitle.text = IsOpen && HasCurrentSession ? "예제 힌트 · " + battle.RobotName(editingId) : "예제 힌트";
            // Drawn at the editor's own scale (captured at 1080p) or smaller to fit.
            var size = sprite.rect.size;
            popup.sizeDelta = size * Mathf.Min(1, HintMax.x / size.x, HintMax.y / size.y) + new Vector2(2 * HintPad, 2 * HintPad + HintTitle);
        }
        // Empties a robot's code (blocks go back to the inventory); through the editor when it is open on that robot.
        public void ClearRobotProgram(int id)
        {
            drafts.Remove(id);
            if (IsOpen && editingId == id) { panel.ClearProgram(); panel.RefreshPalette(); return; }
            var sim = battle.Simulation; var robot = sim?.FindRobot(id);
            if (robot != null && sim.CanEdit) { sim.ApplyProgram(id, robot.Program?.Name ?? "robot_" + id, Array.Empty<CodeBlock>()); sim.SetDraft(id, Array.Empty<BlockKind>()); }
        }

        public void PrepareDistanceExample()
        {
            if (!IsOpen || !HasCurrentSession) return;
            // 2-3 B02: the warrior's loop finds the nearest enemy; the player adds the if on its distance, and the slash.
            LoadLesson(new[] { Loop(new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy))) }, "2-3 조건문 기초 코드");
        }
        public void PrepareBaselineExample()
        {
            if (!IsOpen || !HasCurrentSession) return;
            // 2-3 A05: the warrior slashing on and on, at air or not (each swing rests it 1.6 s), to compare against.
            LoadLesson(new[] { Loop(new CodeBlock(BlockKind.Slash)) }, "2-3 비교용 기본 베기 코드");
            RestartPreview();
        }
        // 2-5: the enemy found once, before the loop; 2-6: a loop in the loop that never lets its enemy go; 2-8: 2-7's hint
        // (break once the enemy is down or out of range).
        public void PrepareExample(string id)
        {
            if (!IsOpen || !HasCurrentSession) return;
            CodeBlock Find() => new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy));
            CodeBlock Shoot() => new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, "enemy"));
            CodeBlock Leave(CodeBlock condition) { var check = new CodeBlock(BlockKind.If, "0", condition); check.Body.Add(new CodeBlock(BlockKind.Break)); return check; }
            var dead = new CodeBlock(BlockKind.IsDead, "0", new CodeBlock(BlockKind.Variable, "enemy"));
            var away = new CodeBlock(BlockKind.Comparison, ">", new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, "enemy"),
                new CodeBlock(BlockKind.MyPos)), new CodeBlock(BlockKind.MyAttackRange));
            var code = id == "2-5" ? new[] { Find(), Loop(Shoot()) } :
                new[] { Loop(Find(), id == "2-8" ? Loop(Leave(dead), Leave(away), Shoot()) : Loop(Shoot())) };
            LoadLesson(code, id + " 준비 코드");
        }
        // The lesson's prepared code takes its blocks from the stock and grants none (GameData/Award.md gives the blocks, on
        // wave clears only): with a block missing it stays unapplied in the editor and the lesson warns (no crash).
        void LoadLesson(CodeBlock[] code, string what)
        {
            panel.LoadProgram(panel.FunctionName, code);
            if (panel.IsApplied) return;
            var missing = DefenseProgression.Used(code).GroupBy(DefenseProgression.Canonical).Where(g => g.Count() > editingSession.Available(g.Key, editingId))
                .Select(g => DefenseProgression.Label(g.Key) + " " + (g.Count() - Math.Max(0, editingSession.Available(g.Key, editingId))) + "개");
            Debug.LogWarning(what + ": 블록이 모자라 적용되지 않음 (" + string.Join(", ", missing) + ")");
        }
        static CodeBlock Loop(params CodeBlock[] body)
        {
            var loop = new CodeBlock(BlockKind.While); // while True (no for for the player, 12차 121)
            loop.Body.AddRange(body);
            return loop;
        }
        public void RestartPreview(float hold = 0) => battle.RestartCodePreview(hold);
        // Python text of condition/value blocks lying in the workspace away from the start anchor (not part of the program).
        public IEnumerable<string> LooseExpressions() => !IsOpen ? Enumerable.Empty<string>() : panel.Program.LooseRoots()
            .Where(root => PythonTreeCompiler.OutputSlot(root.Kind) != BlockSlotKind.Statement)
            .Select(root => PythonTreeCompiler.ExpressionText(root.Read()));
        // Tutorial targets inside the editor (TutorialKeys.IsEditorArea): Editor.Code.Start | Loose | Line.N | After.N | Body.N | Arg.N.i[.j…],
        // with N counted from 1 along the start chain (N/k for the k-th line inside its body, see Line), and Editor.Palette.<BlockKind>.
        // False when not on screen.
        public bool TryGetTutorialCorners(string key, Vector3[] corners)
        {
            if (!IsOpen || key == null) return false;
            if (key.StartsWith(TutorialKeys.PaletteTabPrefix))
                return int.TryParse(key.Substring(TutorialKeys.PaletteTabPrefix.Length), out int tab) && Corners(panel.CategoryTab(tab), null, corners);
            if (key.StartsWith(TutorialKeys.PalettePrefix)) return Corners(TutorialPaletteItem(key), null, corners);
            if (!key.StartsWith(TutorialKeys.CodePrefix)) return false;
            if (key == TutorialKeys.Status) return Corners(panel.StatusLine, null, corners);
            var program = panel.Program;
            var parts = key.Substring(TutorialKeys.CodePrefix.Length).Split('.');
            // The start slot, or the block once one sits there (the slot is narrower than e.g. enemy = get_nearest_enemy()). Empty,
            // it is boxed at the size of the block the step's demo drops there (play feedback 3: the for went past a one-line box).
            if (parts[0] == "Start")
            {
                var first = program.StartChain().FirstOrDefault();
                if (first) return Corners(first.Rect, null, corners);
                var slot = program.StartArea;
                var size = Incoming(key, program.Rect) ?? new Vector2(Mathf.Max(slot.width, 352), 46);
                return Corners(program.Rect, new Rect(slot.xMin, slot.yMax - size.y, size.x, size.y), corners);
            }
            if (parts[0] == "Loose") { var loose = program.LooseRoots().FirstOrDefault(); return Corners(loose ? loose.Rect : null, null, corners); }
            var block = parts.Length < 2 ? null : Line(parts[1]);
            if (!block) return false;
            switch (parts[0])
            {
                case "Name":
                case "Option":
                    for (int i = 2; i < parts.Length; i++)
                    {
                        if (!int.TryParse(parts[i], out int arg) || arg < 0 || arg >= block.Arguments.Count || block.Arguments[arg].Blocks.Count == 0) return false;
                        block = block.Arguments[arg].Blocks[0];
                    }
                    return Corners(parts[0] == "Name" ? block.InputRect : block.OptionRect, null, corners);
                case "BodyArg":
                case "BodyName":
                    var bodySlot = BodyArgument(parts);
                    return Corners(parts[0] == "BodyArg" ? bodySlot?.Rect : bodySlot?.Blocks.FirstOrDefault()?.InputRect, null, corners);
                case "Line": return Corners(block.Rect, null, corners);
                // Line N's own row without its body (the right-click 복사 on a long loop: the whole block left the line no
                // place clear of it, 2-8 F01).
                case "Head": var head = block.Rect.rect; return Corners(block.Rect, new Rect(head.xMin, head.yMax - block.HeaderHeight, head.width, block.HeaderHeight), corners);
                // The slot where the next block snaps: one line high, right under line N.
                case "After": var r = block.Rect.rect; return Corners(block.Rect, new Rect(r.xMin, r.yMin - r.height, r.width, r.height), corners);
                // The upper half of line N: a block dropped there goes in front of it (2-5's if above shoot(enemy)).
                case "Before": var b = block.Rect.rect; return Corners(block.Rect, new Rect(b.xMin, b.center.y, b.width, b.height / 2), corners);
                case "Body":
                    if (!block.Body) return false;
                    if (block.Body.Blocks.Count > 0) return Corners(block.Body.Rect, null, corners);
                    // An empty body: the place its first line goes, at the size of the block the step's demo drops there (one
                    // line otherwise: the closed body is an 8 px strip, and a box that thin hung under the header like a broken slot).
                    var bodyRect = block.Body.Rect.rect;
                    var fit = Incoming(key, block.Body.Rect) ?? new Vector2(bodyRect.width - CommandDropZone.Inset.x, 46);
                    return Corners(block.Body.Rect, new Rect(bodyRect.xMin + CommandDropZone.Inset.x, bodyRect.yMax - CommandDropZone.Inset.y - fit.y, fit.x, fit.y), corners);
                case "Arg":
                    CommandDropZone slot = null;
                    for (int i = 2; i < parts.Length; i++)
                    {
                        if (slot) { if (slot.Blocks.Count == 0) return false; block = slot.Blocks[0]; }
                        if (!int.TryParse(parts[i], out int index) || index < 0 || index >= block.Arguments.Count) return false;
                        slot = block.Arguments[index];
                    }
                    return Corners(slot ? slot.Rect : null, null, corners);
                default: return false;
            }
        }
        // The tutorial step's drag demo ("Editor.Palette.X>destination|…", TutorialStep.dragDemo), set by the director.
        public string TutorialDemo { get; set; }
        // The size, in target's units, of the palette block the demo drops on destination; null when none does. Kept from when
        // that block was last on screen: another palette tab hides it (2-1 B02: a click on 제어 shrank the slot's box to a sliver).
        readonly Dictionary<string, Vector2> incomingSeen = new Dictionary<string, Vector2>();
        Vector2? Incoming(string destination, RectTransform target)
        {
            foreach (var pair in (TutorialDemo ?? "").Split('|'))
            {
                var ends = pair.Split('>');
                if (ends.Length != 2 || ends[1].Trim() != destination) continue;
                string source = ends[0].Trim();
                if (TutorialPaletteItem(source) is RectTransform item)
                {
                    var view = item.GetComponent<CommandBlockView>();
                    return incomingSeen[source] = new Vector2(view.preferredWidth, view.preferredHeight) * (item.lossyScale.x / target.lossyScale.x);
                }
                if (incomingSeen.TryGetValue(source, out var seen)) return seen;
            }
            return null;
        }
        // A demo whose value slot holds a block already is done (2-6 C03b: get_my_pos() kept gliding into the filled brackets).
        public bool DemoDone(string destination) => TutorialSlot(destination) is CommandDropZone slot && slot.Slot != BlockSlotKind.Statement && slot.Blocks.Count > 0;
        // An Editor.Code.Arg.N.i[.j…] slot that holds a block (the slot exists, empty, as soon as its line does).
        public bool SlotFilled(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            // Optional ':BlockKind' makes teaching steps check what was placed, not just occupancy.
            var parts = key.Split(':');
            var slot = TutorialSlot(parts[0]);
            if (slot == null || slot.Blocks.Count == 0) return false;
            return parts.Length == 1 || parts.Length == 2 &&
                Enum.TryParse(parts[1], out BlockKind kind) && (slot.Blocks[0].BaseKind == kind || slot.Blocks[0].Kind == kind);
        }

        public bool SlotSelected(string key)
            => TutorialSlot(key) is CommandDropZone slot && (panel.SelectedZone == slot || slot.Blocks.Count > 0);

        public bool SelectTutorialSlot(string key)
        {
            // Editor.Code.Before.N / After.N: a click on the palette puts its block right there (2-5, 2-6, 2-8 C01).
            var place = IsOpen && key != null && key.StartsWith(TutorialKeys.CodePrefix) ? key.Substring(TutorialKeys.CodePrefix.Length).Split('.') : new string[0];
            // Editor.Code.Start (1-1 C05, 1-3 C05, on an empty program): the program itself, where a click starts the code.
            if (place.Length == 1 && place[0] == "Start") { panel.Select(panel.Program); return true; }
            if (place.Length == 2 && (place[0] == "Before" || place[0] == "After"))
            {
                var line = Line(place[1]);
                if (!line || !line.Owner || line.Owner.FreePlacement) return false;
                panel.Select(line.Owner, line.Owner.Blocks.ToList().IndexOf(line) + (place[0] == "After" ? 1 : 0));
                return true;
            }
            var slot = TutorialSlot(key);
            if (slot == null) return false;
            // A player may have filled the slot before this guide started. Keep their work;
            // the tutorial's normal completion checks decide whether it is correct. A body (a statement slot) takes more
            // lines at its end, so it is chosen even when it holds some (1-3 C01: the if goes under the loop's two lines).
            if (slot.Blocks.Count > 0 && slot.Slot != BlockSlotKind.Statement) return true;
            panel.Select(slot);
            return true;
        }

        // A draft block's input/option can be checked before its surrounding if is complete.
        // Expected: "3.0|<=" or "3.0.0|enemy_distance" (line, then argument indexes).
        public bool DraftValueMatches(string expected)
        {
            if (!IsOpen || string.IsNullOrEmpty(expected)) return false;
            var pair = expected.Split('|');
            if (pair.Length != 2) return false;
            var path = pair[0].Split('.');
            var block = Line(path[0]);
            if (!block) return false;
            for (int i = 1; i < path.Length; i++)
            {
                if (!int.TryParse(path[i], out int arg) || arg < 0 || arg >= block.Arguments.Count) return false;
                block = block.Arguments[arg].Blocks.FirstOrDefault();
                if (!block) return false;
            }
            return block.Read().Value == pair[1];
        }

        public bool ConditionMatches(string expected)
        {
            if (!IsOpen) return false;
            foreach (var block in panel.Program.AllBlocks().Where(b => PythonTreeCompiler.OutputSlot(b.Kind) == BlockSlotKind.Condition))
            {
                try { if (PythonTreeCompiler.ExpressionText(block.Read()) == expected) return true; }
                catch (FormatException) { } // An unfinished comparison is not a match yet.
            }
            return false;
        }

        // Expected form: "1:DeclareVariable". Checks the connected draft, not the last compiled program.
        public bool CodeLinePlaced(string expected)
        {
            if (!IsOpen || string.IsNullOrEmpty(expected)) return false;
            var parts = expected.Split(':');
            var block = parts.Length == 2 ? Line(parts[0]) : null;
            return block && Enum.TryParse(parts[1], out BlockKind kind) && block.BaseKind == kind;
        }

        // A line of the program: "N" is the N-th block on the start chain (from 1), "N/k" the k-th block inside its body,
        // "N/k/j" one level further (1-3's if sits inside the for: "1/3").
        public CommandBlockView Line(string token)
        {
            var steps = token.Split('/');
            var chain = panel.Program.StartChain();
            if (!int.TryParse(steps[0], out int line) || line < 1 || line > chain.Count) return null;
            var block = chain[line - 1];
            for (int i = 1; i < steps.Length; i++)
            {
                if (!block.Body || !int.TryParse(steps[i], out int index) || index < 1 || index > block.Body.Blocks.Count) return null;
                block = block.Body.Blocks[index - 1];
            }
            return block;
        }

        // Finish editing before the next dialogue can receive stray keystrokes from the completed name field.
        public void FinishTutorialInput()
        {
            var current = EventSystem.current;
            var selected = current ? current.currentSelectedGameObject : null;
            if (IsOpen && selected && selected.transform.IsChildOf(panel.transform) && panel.TextInputFocused)
                current.SetSelectedGameObject(null);
        }

        CommandDropZone TutorialSlot(string key)
        {
            if (!IsOpen || key == null || !key.StartsWith(TutorialKeys.CodePrefix)) return null;
            var parts = key.Substring(TutorialKeys.CodePrefix.Length).Split('.');
            if (parts[0] == "BodyArg") return BodyArgument(parts);
            var block = parts.Length < 2 ? null : Line(parts[1]);
            if (!block) return null;
            if (parts[0] == "Body") return block.Body;
            if (parts[0] != "Arg" || parts.Length < 3) return null;
            CommandDropZone slot = null;
            for (int i = 2; i < parts.Length; i++)
            {
                if (slot) { if (slot.Blocks.Count == 0) return null; block = slot.Blocks[0]; }
                if (!int.TryParse(parts[i], out int index) || index < 0 || index >= block.Arguments.Count) return null;
                slot = block.Arguments[index];
            }
            return slot;
        }

        CommandDropZone BodyArgument(string[] parts)
        {
            var chain = panel.Program.StartChain();
            if (parts.Length < 4 || !int.TryParse(parts[1], out int line) || line < 1 || line > chain.Count) return null;
            var body = chain[line - 1].Body;
            if (!body || !int.TryParse(parts[2], out int index) || index < 0 || index >= body.Blocks.Count) return null;
            var block = body.Blocks[index];
            CommandDropZone slot = null;
            for (int i = 3; i < parts.Length; i++)
            {
                if (slot) { block = slot.Blocks.FirstOrDefault(); if (!block) return null; }
                if (!int.TryParse(parts[i], out int arg) || arg < 0 || arg >= block.Arguments.Count) return null;
                slot = block.Arguments[arg];
            }
            return slot;
        }
        static bool Corners(RectTransform target, Rect? local, Vector3[] corners)
        {
            if (!target || !target.gameObject.activeInHierarchy) return false;
            var r = local ?? target.rect;
            corners[0] = target.TransformPoint(new Vector3(r.xMin, r.yMin)); corners[1] = target.TransformPoint(new Vector3(r.xMin, r.yMax));
            corners[2] = target.TransformPoint(new Vector3(r.xMax, r.yMax)); corners[3] = target.TransformPoint(new Vector3(r.xMax, r.yMin));
            return true;
        }
        // Switches the palette to the category holding an Editor.Palette.<BlockKind> target so its outline can show.
        public void RevealPalette(string key)
        {
            if (!IsOpen || !PaletteKind(key, out var kind)) return;
            int category = kind == BlockKind.Variable ? 2 : panel.Palette.Where(p => p.view.BaseKind == kind).Select(p => p.category).DefaultIfEmpty(-1).First();
            if (category >= 0) panel.ShowCategory(category);
        }
        // The palette block for an Editor.Palette.<BlockKind> key (the variable template for Variable); null when not on screen.
        public RectTransform TutorialPaletteItem(string key)
        {
            if (!IsOpen || !PaletteKind(key, out var kind)) return null;
            var item = kind == BlockKind.Variable ? panel.VariableTemplate : panel.Palette.FirstOrDefault(p => p.view.BaseKind == kind).view;
            return item && item.Rect.gameObject.activeInHierarchy ? item.Rect : null;
        }
        static bool PaletteKind(string key, out BlockKind kind)
        {
            kind = default;
            return key != null && key.StartsWith(TutorialKeys.PalettePrefix) && Enum.TryParse(key.Substring(TutorialKeys.PalettePrefix.Length), out kind);
        }
        public void RestoreAutomatic()
        {
            if (!battle.TutorialAllows(TutorialKeys.RestoreAutomatic)) return;
            if (!IsOpen || !HasCurrentSession) return;
            panel.ClearProgram();
            panel.RefreshPalette();
            if (!panel.IsApplied) return;
            panel.ShowMessage("코드를 비웠습니다. 블록 예약이 해제됩니다.");
            battle.ReportTutorialAction(TutorialKeys.RestoreAutomatic, TutorialSignals.TargetClicked);
        }
        void Update()
        {
            if (battle.HelpOpen) return;
            var sim = battle.Simulation;
            if (!IsOpen) return;
            // 재시작이나 도메인 리로드로 세션이 사라지면 이전 로봇 편집을 즉시 종료합니다.
            if (!HasCurrentSession || !sim.CanEdit || !sim.Robots.Exists(r => r.Id == editingId)) { Close(false); return; }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            // In a lesson Esc opens the menu over the editor; closing the editor there would leave the step behind.
            if (keyboard.escapeKey.wasPressedThisFrame) { if (battle.InTutorial) battle.ToggleMenu(); else Close(); return; }
            if (battle.TutorialBlocksInput) return;
            // 텍스트 입력 중에는 입력창 단축키를 유지하고, 그 밖에서만 블록 실행 취소를 처리합니다.
            if (panel.TextInputFocused || keyboard.altKey.isPressed || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed) return;
            if (keyboard.ctrlKey.isPressed)
            {
                if (keyboard.zKey.wasPressedThisFrame) { if (keyboard.shiftKey.isPressed) panel.Redo(); else panel.Undo(); }
                else if (keyboard.yKey.wasPressedThisFrame) panel.Redo();
            }
        }
        void OnDestroy()
        {
            if (!panel) return;
            panel.BlockDropped -= OnBlockDropped;
            panel.ApplyToTarget = null; panel.IsAvailable = null; panel.Remaining = null; panel.PaletteClicking = null;
        }
    }
}
