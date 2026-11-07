using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    // Play feedback 3 (Docs/MDA_고도화.md 7차, items 71 to 84) measured on screen: follows 1-1's tutorial like ScreenTour, then a
    // 2-1 sandbox for the placement marker, and writes one PASS/FAIL line per item to TestResults/Editor/feedback3.log with
    // screenshots (TestResults/Editor/tour/s81_*.png). Needs the Game view (windowed Editor), at 1920x1080.
    public static class PlayFeedbackChecks
    {
        const string LogPath = "TestResults/Editor/feedback3.log", Shots = "TestResults/Editor/tour/";
        static readonly Stack<IEnumerator> run = new Stack<IEnumerator>();
        static int failures;

        [MenuItem("Tools/Develop/Verify Play Feedback 3")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); Directory.CreateDirectory(Shots);
            File.WriteAllText(LogPath, "START play feedback 3\n");
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "ScreenTour");
            failures = 0; run.Clear(); run.Push(Steps());
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            try
            {
                if (run.Peek().MoveNext()) { if (run.Peek().Current is IEnumerator inner) run.Push(inner); }
                else run.Pop();
            }
            catch (System.Exception error) { Check(false, "run", error.ToString()); run.Clear(); }
            if (run.Count > 0) return;
            EditorApplication.update -= Tick;
            string result = failures == 0 ? "PASS all" : $"FAIL {failures}";
            File.AppendAllText(LogPath, result + "\n");
            if (failures == 0) Debug.Log("[feedback3] " + result); else Debug.LogError("[feedback3] " + result + " → " + LogPath);
            EditorApplication.isPlaying = false;
        }
        static void Check(bool ok, string item, string detail)
        {
            if (!ok) failures++;
            File.AppendAllText(LogPath, (ok ? "PASS " : "FAIL ") + item + ": " + detail + "\n");
        }
        static IEnumerator Seconds(double count) { for (double end = EditorApplication.timeSinceStartup + count; EditorApplication.timeSinceStartup < end;) yield return null; }
        static IEnumerator Until(System.Func<bool> done, double seconds = 60)
        {
            for (double end = EditorApplication.timeSinceStartup + seconds; !done() && EditorApplication.timeSinceStartup < end;) yield return null;
        }
        static IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Shots + "s81_" + name + ".png"));
            yield return Seconds(.3);
        }
        // A rect's screen rectangle (pixels, y up); every canvas here is screen-space overlay.
        static Rect OnScreen(RectTransform rect, Rect? local = null)
        {
            var r = local ?? rect.rect;
            Vector3 a = rect.TransformPoint(new Vector3(r.xMin, r.yMin)), b = rect.TransformPoint(new Vector3(r.xMax, r.yMax));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        static Rect Corners(System.Func<Vector3[], bool> get)
        {
            var c = new Vector3[4];
            if (!get(c)) return default;
            return Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
        }
        static string R(Rect r) => $"{r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0}";
        static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(owner);

        static IEnumerator Steps()
        {
            // 71: the path arrows ease in and out, so the pack is fastest in the middle of its run.
            float total = 1200, step = .01f;
            float Speed(float t) => (TutorialPointer.PathLead(t + step, total) - TutorialPointer.PathLead(t, total)) / step;
            float lap = TutorialPointer.PathRunSeconds(total);
            float early = Speed(lap * .03f), middle = Speed(lap * .5f);
            Check(middle > early * 2 && TutorialPointer.PathLead(0, total) <= 1, "71 path arrow curve", $"speed at 3% {early:0}, at 50% {middle:0} px/s");

            DevTest.PlayDefense(1, 1, true);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(1.5);
            var b = DevTest.Battle;
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            var steps = DevTest.TutorialSections(1);
            int Index(string id) => System.Array.FindIndex(steps, s => s.Split(' ')[0] == id);
            CodeBlock Loop(string count, params CodeBlock[] body)
            {
                var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, count)); loop.Body.AddRange(body); return loop;
            }
            // 1-1 up to C05 (slash() at the start); the loop's checks (80, 84) follow in 1-3, where the loop is taught now.
            for (int i = 0; i <= Index("C05"); i++)
            {
                string id = steps[i].Split(' ')[0];
                // B03 points at the robot B02 placed, which the battle registers as a target a frame after the jump places it.
                string failed = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try { DevTest.JumpTutorial(1, i); failed = null; }
                    catch (System.InvalidOperationException error) { failed = error.Message; }
                    yield return Seconds(.3);
                    if (failed == null) break;
                }
                if (failed != null) { if (id == "B03" || id == "C05") Check(false, "jump " + id, failed); continue; }
                bool coding = id.StartsWith("C") && id != "C01";
                if (coding && !b.CodeEditor.IsOpen && b.Simulation.Robots.Count > 0) b.CodeEditor.Open(b.Simulation.Robots.OrderBy(r => r.Id).First().Id);
                else if (!coding && b.CodeEditor.IsOpen) b.CodeEditor.Close(false);
                if (id != "B03" && id != "C05") continue;
                yield return Seconds(1.7);
                var talk = Object.FindFirstObjectByType<TutorialDialogueView>(); if (talk) talk.FinishTyping();
                yield return Seconds(.1);
                yield return Shot("1-1_" + id);
                var bridge = director.EditorBridge;
                if (id == "B03")
                {
                    // 73: the robot's focus box leaves room round its '코드 없음' tag.
                    var tag = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Status tag" && t.gameObject.activeInHierarchy);
                    var spotlight = Object.FindFirstObjectByType<TutorialSpotlight>();
                    if (!tag || !bridge.TryGetArea(TutorialKeys.Unit(0), out var area)) { Check(false, "73 code tag focus", "no tag or no focus"); continue; }
                    float pad = director.EditorStep.highlightPadding;
                    var focus = OnScreen(spotlight.rectTransform, Rect.MinMaxRect(area.xMin - pad, area.yMin - pad, area.xMax + pad, area.yMax + pad));
                    var label = OnScreen(tag);
                    float top = focus.yMax - label.yMax, side = Mathf.Min(label.xMin - focus.xMin, focus.xMax - label.xMax);
                    Check(top >= 10 && side >= 10, "73 code tag focus", $"tag {R(label)} in focus {R(focus)}: {top:0} px above, {side:0} px aside");
                }
                if (id == "C05")
                {
                    // 79: the empty start is boxed at the size of the block about to go there (slash() since play feedback 4).
                    var start = Corners(c => b.CodeEditor.TryGetTutorialCorners("Editor.Code.Start", c));
                    var item = b.CodeEditor.TutorialPaletteItem("Editor.Palette.Slash");
                    var view = item.GetComponent<CommandBlockView>();
                    var placed = OnScreen(item, new Rect(0, 0, view.preferredWidth, view.preferredHeight));
                    Check(Mathf.Abs(start.width - placed.width) <= 16 && Mathf.Abs(start.height - placed.height) <= 8, "79 start box",
                        $"box {R(start)}, slash block {placed.width:0}x{placed.height:0}");
                }
            }
            {
                // 1-3 C08: the jump skips the drags, so the empty loop is put in place the way C05 leaves it.
                var lesson = DevTest.TutorialSections(3);
                DevTest.JumpTutorial(3, System.Array.FindIndex(lesson, s => s.Split(' ')[0] == "C08"));
                yield return Until(() => DevTest.Ready && DevTest.Sim.WaveIndex == 2 && Object.FindFirstObjectByType<TutorialDirector>().EditorStep?.stepId == "C08", 20);
                b = DevTest.Battle; director = Object.FindFirstObjectByType<TutorialDirector>();
                var bridge = director.EditorBridge;
                if (!b.CodeEditor.IsOpen) b.CodeEditor.Open(b.Simulation.Robots.OrderBy(r => r.Id).First().Id);
                b.CodeEditor.Panel.LoadProgram(b.CodeEditor.Panel.FunctionName, new[] { Loop("10") });
                yield return Seconds(1.7);
                var talk = Object.FindFirstObjectByType<TutorialDialogueView>(); if (talk) talk.FinishTyping();
                yield return Seconds(.1);
                yield return Shot("1-3_C08");
                {
                    // 80: the loop's empty mouth is boxed where slash() goes, at its size.
                    var body = Corners(c => b.CodeEditor.TryGetTutorialCorners("Editor.Code.Body.1", c));
                    var item = b.CodeEditor.TutorialPaletteItem("Editor.Palette.Slash");
                    var view = item.GetComponent<CommandBlockView>();
                    var size = OnScreen(item, new Rect(0, 0, view.preferredWidth, view.preferredHeight));
                    var loopBlock = b.CodeEditor.Line("1");
                    var mouth = OnScreen(loopBlock.Body.Rect);
                    Check(Mathf.Abs(body.width - size.width) <= 12 && Mathf.Abs(body.height - size.height) <= 8 && mouth.Contains(body.center),
                        "80 body box", $"box {R(body)}, slash {size.width:0}x{size.height:0}, mouth {R(mouth)}");

                    // 84: slash() in a loop of 100 still finishes the step.
                    b.CodeEditor.Panel.LoadProgram(b.CodeEditor.Panel.FunctionName, new[] { Loop("100", new CodeBlock(BlockKind.Slash)) });
                    yield return Until(() => director.EditorStep?.stepId != "C08", 3);
                    Check(director.EditorStep?.stepId != "C08", "84 loop of 100", "step after slash() in range(100): " + director.EditorStep?.stepId);
                    yield return Seconds(.5);
                    if (talk) talk.FinishTyping();
                    yield return Shot("1-3_C08_filled");
                    yield return EditorBlocks(b);
                    yield return EditorLayout(b);
                }
            }

            // 72: the placement marker is the robot's body, and the rules use the same size.
            DevTest.PlayDefense(2, 1, true, new DefenseTestOptions { tutorial = false, tutorialInput = false });
            yield return Until(() => DevTest.Ready);
            yield return Seconds(1);
            b = DevTest.Battle;
            var marker = Field<Transform>(b, "placementMarker");
            foreach (var role in new[] { RobotRole.Warrior, RobotRole.Shooter, RobotRole.Tank })
            {
                int index = Enumerable.Range(0, 6).First(i => b.DefinitionRole(i) == role);
                if (b.Simulation.RobotAvailable(role) < 1) b.Simulation.GrantRobot(role, 1);
                b.ChooseRobot(index);
                float radius = b.RobotDefinition(index).stats.radius;
                var model = b.RobotDefinition(index).prefab ? b.RobotDefinition(index).prefab.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.name.Contains("Mesh") || r.name == "Body").Select(r => Mathf.Min(r.bounds.size.x, r.bounds.size.z)).DefaultIfEmpty(0).Max() : 0;
                Check(Mathf.Abs(marker.localScale.x - radius * 2) < .01f && radius * 2 >= model * .9f, "72 placement marker " + role,
                    $"marker {marker.localScale.x:0.00}, rule diameter {radius * 2:0.00}, body {model:0.00}");
            }
        }

        // 81, 82, 83: the code blocks on screen in C08's loop of 100.
        static IEnumerator EditorBlocks(DefenseBattle b)
        {
            var panel = b.CodeEditor.Panel;
            var loop = b.CodeEditor.Line("1"); var swing = b.CodeEditor.Line("1/1"); var count = loop.Arguments[0].Blocks[0];
            // 83: the swing sits clear of the loop's arm, header and foot (outlines are 3 px outside both).
            var inner = loop.Rect.InverseTransformPoint(swing.Rect.TransformPoint(swing.Rect.rect.min));
            var innerMax = loop.Rect.InverseTransformPoint(swing.Rect.TransformPoint(swing.Rect.rect.max));
            float left = inner.x - loop.Rect.rect.xMin - 18, top = loop.Rect.rect.yMax - innerMax.y - 46, bottom = inner.y - (loop.Rect.rect.yMin + 18);
            Check(left >= 9.5f && top >= 9.5f && bottom >= 9.5f, "83 block overlap", $"slash() is {left:0} from the arm, {top:0} under the header, {bottom:0} over the foot");
            // 81: the count is a small pill inside the header, as wide as its digits.
            var text = count.GetComponentInChildren<TMP_InputField>().textComponent;
            float digits = text.GetPreferredValues("100").x;
            Check(count.Rect.rect.height <= 28 && count.Rect.rect.width <= digits + 28, "81 value block", $"number {count.Rect.rect.width:0}x{count.Rect.rect.height:0}, digits {digits:0}");
            // 82: typed text sizes its field: the count, the def name.
            var field = count.GetComponentInChildren<TMP_InputField>();
            field.text = "1"; Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(panel.Program.Rect); yield return null;
            float one = count.Rect.rect.width;
            field.text = "12345"; Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(panel.Program.Rect); yield return null;
            float five = count.Rect.rect.width;
            var name = panel.GetComponentsInChildren<TMP_InputField>(true).First(f => f.name == "FunctionName");
            string original = panel.FunctionName;
            panel.FunctionName = "a"; yield return null; float shortName = ((RectTransform)name.transform).rect.width;
            panel.FunctionName = "robot_with_a_long_name"; yield return null; float longName = ((RectTransform)name.transform).rect.width;
            yield return Shot("editor_long_name");
            panel.FunctionName = original; field.text = "100";
            Check(five > one + 20 && longName > shortName + 100, "82 input sizes", $"count 1 → {one:0}, 12345 → {five:0}; def name a → {shortName:0}, long → {longName:0}");
        }

        // 74 to 78: the editor's frame, buttons and panels.
        static IEnumerator EditorLayout(DefenseBattle b)
        {
            var editor = b.CodeEditor;
            Button close = Field<Button>(editor, "closeButton"), clear = Field<Button>(editor, "automaticButton"), hint = Field<Button>(editor, "hintButton");
            editor.SetHint("while True:\n    slash()"); yield return null;
            yield return Shot("editor_hint");
            Rect c = OnScreen((RectTransform)close.transform), e = OnScreen((RectTransform)clear.transform), h = OnScreen((RectTransform)hint.transform);
            float gap = c.xMin - e.xMax, hintGap = e.xMin - h.xMax;
            Check(gap >= 0 && gap <= 16 && Mathf.Abs(c.center.y - e.center.y) < 4, "77 clear and close", $"비우기 {R(e)}, 닫기 {R(c)}: {gap:0} px apart");
            Check(hintGap >= 0 && hintGap <= 16 && Mathf.Abs(h.center.y - e.center.y) < 4, "75 hint button", $"i {R(h)}, 비우기 {R(e)}: {hintGap:0} px apart");
            editor.SetHint(null);
            // 75: the block guide's i follows the inventory's title.
            var info = editor.Panel.GetComponentsInChildren<Button>(true).First(x => x.name == "Info" && x.transform.parent == editor.Panel.transform);
            var heading = Field<TMP_Text>(editor, "inventory"); heading.ForceMeshUpdate();
            Rect words = OnScreen(heading.rectTransform, new Rect(heading.textBounds.min, heading.textBounds.size)), i = OnScreen((RectTransform)info.transform);
            Check(i.xMin - words.xMax >= 0 && i.xMin - words.xMax <= 24 && Mathf.Abs(i.center.y - words.center.y) < 8, "75 block guide button",
                $"i {R(i)}, '블록 인벤토리' {R(words)}: {i.xMin - words.xMax:0} px after it");
            var label = clear.GetComponentInChildren<TMP_Text>();
            Check(label.font == UITheme.Display || (label.fontStyle & FontStyles.Bold) != 0, "78 clear bold", label.font.name + " " + label.fontStyle);

            var preview = Object.FindFirstObjectByType<DefenseAttackPreview>();
            var frame = (RectTransform)Field<GameObject>(preview, "previewPanel").transform;
            var title = Field<TMP_Text>(editor, "title");
            title.ForceMeshUpdate();
            var letters = OnScreen(title.rectTransform, new Rect(title.textBounds.min, title.textBounds.size));
            var box = OnScreen(frame);
            Check(letters.yMin - box.yMax >= 8, "74 title gap", $"title letters end {letters.yMin:0}, preview frame top {box.yMax:0}");
            var stats = Field<TMP_Text>(preview, "stats"); var card = (RectTransform)stats.transform.parent;
            var cardRect = OnScreen(card); var statsRect = OnScreen(stats.rectTransform);
            Check(cardRect.xMin >= 8 && cardRect.yMin >= 8 && statsRect.xMin - cardRect.xMin >= 20, "74 stats padding",
                $"card {R(cardRect)} (screen margin {cardRect.xMin:0} left, {cardRect.yMin:0} below), text {statsRect.xMin - cardRect.xMin:0} px in");
            var fill = frame.GetComponent<Image>();
            Color.RGBToHSV(fill.color, out _, out _, out float value);
            Check(value > .4f || fill.sprite == UITheme.Card, "76 preview frame", $"{fill.sprite?.name} #{ColorUtility.ToHtmlStringRGB(fill.color)}");
        }
    }
}
