using System;
using System.Collections;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense.Editor
{
    // The coding panel's three tabs in the real editor (Docs/코딩_모드_3탭.md 검사): the 1-6 example, every block through the
    // Python and 자연어 tabs, errors and 에러 on blocks and lines, completion, and typed code running like built blocks.
    // Screenshots go to Report/. Batchmode: -executeMethod CodingGame.Defense.Editor.CodingModeChecks.Run (no -quit).
    [InitializeOnLoad]
    public static class CodingModeChecks
    {
        const string Running = "CodingGame.CodingModes.Check", LogPath = "TestResults/Editor/coding-modes.log";
        static readonly System.Collections.Generic.Stack<IEnumerator> run = new System.Collections.Generic.Stack<IEnumerator>();
        static CodingModeChecks()
        {
            EditorApplication.update += Tick;
        }
        [MenuItem("Tools/Develop/Verify Coding Tabs (자연어·블록·파이썬)")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START coding tabs check\n");
            SessionState.SetBool(Running, true);
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "CodingModes");
            DevTest.PlayDefense(2, 1, false, new DefenseTestOptions { tutorial = false, tutorialInput = false, deploymentLimit = false, inventoryLimit = false, unlockAll = true });
        }
        static void Log(string line) { File.AppendAllText(LogPath, line + "\n"); Debug.Log("[CodingModes] " + line); }
        static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || !DevTest.Ready) return;
            try
            {
                if (run.Count == 0) run.Push(Steps());
                var top = run.Peek();
                if (top.MoveNext()) { if (top.Current is IEnumerator inner) run.Push(inner); }
                else { run.Pop(); if (run.Count == 0) End("PASS coding tabs: 1-6 example, round trips, errors, 에러, bubbles, completion, typed code"); }
            }
            catch (Exception error) { End("FAIL " + error); }
        }
        static void End(string result)
        {
            Log(result); SessionState.SetBool(Running, false); run.Clear();
            if (!Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                FileUtil.DeleteFileOrDirectory("Temp/__Backupscenes");
                EditorApplication.delayCall += () => EditorApplication.Exit(result.StartsWith("FAIL") ? 1 : 0);
            };
            EditorApplication.isPlaying = false;
        }
        static void Require(bool value, string what) { if (!value) throw new Exception(what); Log("ok " + what); }
        static IEnumerator Wait(double seconds) { for (double end = EditorApplication.timeSinceStartup + seconds; EditorApplication.timeSinceStartup < end;) yield return null; }

        static DefenseBattle battle; static DefenseSimulation sim; static DefenseCodeEditor editor; static CommandCodingPanel panel;
        static CodeBlock B(BlockKind kind, params CodeBlock[] args) => new CodeBlock(kind, "0", args);
        static CodeBlock V(string name) => new CodeBlock(BlockKind.Variable, name);
        static CodeBlock Body(CodeBlock block, params CodeBlock[] children) { block.Body.AddRange(children); return block; }
        static string Chain() => PythonTreeCompiler.Print(panel.Program.StartChain().Select(b => b.Read()).ToList());
        static string RobotCode(int id) => PythonTreeCompiler.Print(sim.FindRobot(id).Program?.CopyBlocks() ?? Array.Empty<CodeBlock>());
        static int Place(RobotRole role)
        {
            int definition = Enumerable.Range(0, battle.RobotDefinitions.Count).First(i => battle.DefinitionRole(i) == role);
            var spec = battle.RobotDefinition(definition).stats;
            for (float x = sim.Setup.Min.X + 1; x < sim.Setup.Max.X - 1; x++)
                for (float y = sim.Setup.Min.Y + 1; y < sim.Setup.Max.Y - 1; y++)
                    if (sim.PlacementError(spec, new Point(x, y)) == null && battle.PlaceRobot(definition, new Vector3(x, 0, y))) return sim.Robots.Last().Id;
            throw new Exception("no legal placement for " + role);
        }
        static void Open(int id) { editor.Close(false); editor.Open(id); if (!editor.IsOpen) throw new Exception("editor did not open"); }
        static CommandBlockView Palette(BlockKind kind) => panel.Palette.First(p => p.view.BaseKind == kind).view;

        static IEnumerator Steps()
        {
            battle = DevTest.Battle; sim = battle.Simulation; editor = battle.CodeEditor; panel = editor.Panel;
            battle.EditorGrantAll();
            int shooter = Place(RobotRole.Shooter), tank = Place(RobotRole.Tank);
            Open(shooter);
            yield return Wait(.5);
            Require(panel.Mode == CodingMode.Block, "the editor opens on the 블록 tab");
            var tabs = panel.transform.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Tab")).ToList();
            Require(tabs.Count == 3 && tabs.All(t => t.gameObject.activeInHierarchy), "three tabs in the title bar");

            // Example table 1-6.
            string start = "enemy = get_nearest_enemy()\nwhile True:\n    shoot(enemy)\n";
            panel.LoadProgram("robot_1", PythonTreeParser.Parse(start).ToArray());
            panel.Program.PlaceFree(Palette(BlockKind.Slash), new Vector2(380, -330));
            panel.Program.PlaceFree(Palette(BlockKind.Wait), new Vector2(380, -440));
            yield return Wait(.3);
            Require(panel.Program.LooseRoots().Count() == 2, "1: two loose chains in the 블록 tab");
            int five = panel.History.CommitNumber;
            panel.SetMode(CodingMode.Python);
            yield return Wait(.3);
            Require(panel.PythonText == start, "1: the Python text is the start chain only");
            string robotBefore = RobotCode(shooter);
            string typo = start.Replace("while True:", "whlie True:");
            panel.TypePython(typo); panel.ReadPython();
            Require(panel.SyntaxError != null && panel.SyntaxError.Line == 2 && panel.Squiggles.Any(s => s.line == 2 && s.color == CommandBlockView.ErrorColor),
                "2: whlie gets a red underline on line 2 (" + panel.SyntaxError?.Text + ")");
            Require(panel.History.CommitNumber == five && RobotCode(shooter) == robotBefore, "2: the commit and the robot's code stay");
            yield return Wait(.6);
            yield return Shot("2026-11-25_파이썬 탭 문법 오류 빨간 물결 밑줄");
            yield return Wait(.2);
            panel.SetMode(CodingMode.Block);
            yield return Wait(.3);
            Require(panel.Program.LooseRoots().Count() == 2 && Chain() == start, "3: the 블록 tab comes back with its loose chains");
            Require(panel.StatusLine.GetComponent<TMP_Text>().text.Contains("파이썬 탭에 문법 오류가 있어"), "3: the status line says the Python tab has a syntax error");
            panel.SetMode(CodingMode.Python);
            yield return Wait(.2);
            Require(panel.PythonText == typo, "4: the Python tab keeps the text with the typo");
            string fixedText = start + "    wait(1)\n";
            panel.TypePython(fixedText); panel.ReadPython();
            Require(panel.SyntaxError == null && panel.History.CommitNumber == five + 1, "5: fixed, it commits (" + five + " → " + panel.History.CommitNumber + ")");
            Require(RobotCode(shooter) == fixedText, "5: the robot runs the typed code");
            panel.SetMode(CodingMode.Block);
            yield return Wait(.3);
            Require(!panel.Program.LooseRoots().Any() && Chain() == fixedText, "6: the 블록 tab is rebuilt from the commit, loose chains gone");

            // Every block kind through the Python and 자연어 tabs.
            string every = "enemy = get_nearest_enemy()\nx = get_pos_x()\ny = get_pos_y()\nr = my_attack_range()\nd = (x + 2) * (y - 1)\nwhile True:\n" +
                "    if enemy.is_dead():\n        enemy = get_nearest_enemy()\n        continue\n" +
                "    elif enemy.distance(get_my_pos()) <= r:\n        shoot(enemy)\n        slash()\n" +
                "    elif enemy.type == Enemy.Tank:\n        block(enemy)\n        boom(enemy)\n" +
                "    else:\n        slow(enemy)\n        buff()\n" +
                "    for i in range(3):\n        if enemy.hp > 10:\n            break\n        wait(0.5)\n    if True:\n        pass\n    if False:\n        wait(1.5)\n";
            panel.LoadProgram("robot_1", PythonTreeParser.Parse(every).ToArray());
            yield return Wait(.3);
            Require(Chain() == every, "every block kind loads as blocks");
            panel.SetMode(CodingMode.Python); yield return Wait(.2);
            Require(panel.PythonText == every, "블록 → 파이썬: same text");
            panel.SetMode(CodingMode.Block); yield return Wait(.2);
            Require(Chain() == every, "파이썬 → 블록: same tree");
            panel.SetMode(CodingMode.Natural); yield return Wait(.4);
            Require(Chain() == every, "블록 → 자연어: same tree");
            var loop = panel.Program.StartChain().First(b => b.Kind == BlockKind.While);
            Require(loop.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "계속 반복하기" && t.gameObject.activeInHierarchy), "자연어 words on the loop");
            panel.SetMode(CodingMode.Block); yield return Wait(.2);
            Require(Chain() == every && !loop.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "계속 반복하기"), "자연어 → 블록: same tree, Python words back");

            // Screens of the three tabs on a readable program.
            string shown = "enemy = get_nearest_enemy()\nwhile True:\n    if enemy.is_dead():\n        enemy = get_nearest_enemy()\n" +
                "    if enemy.distance(get_my_pos()) <= my_attack_range():\n        shoot(enemy)\n    wait(0.5)\n";
            panel.LoadProgram("robot_1", PythonTreeParser.Parse(shown).ToArray());
            panel.SetMode(CodingMode.Natural); yield return Wait(.5);
            yield return Shot("2026-11-25_자연어 탭");
            yield return Wait(.2);
            panel.SetMode(CodingMode.Block); yield return Wait(.5);
            yield return Shot("2026-11-25_블록 탭");
            yield return Wait(.2);
            panel.SetMode(CodingMode.Python); yield return Wait(.5);
            yield return Shot("2026-11-25_파이썬 탭 다크 테마");
            yield return Wait(.2);

            // Completion and the parameter hint.
            panel.TypePython(shown + "    e = get");
            Require(panel.CompletionItems.Count >= 3 && panel.CompletionItems.All(i => i.Text.StartsWith("get")), "get offers the get_ functions");
            yield return Wait(.3);
            yield return Shot("2026-11-25_파이썬 탭 자동 완성 둥근 목록");
            yield return Wait(.2);
            panel.TypePython(shown + "    e = get_n");
            Require(panel.CompletionItems.Count > 0 && panel.CompletionItems[0].Text == "get_nearest_enemy()", "get_n offers get_nearest_enemy() first");
            panel.AcceptCompletion();
            Require(panel.PythonText == shown + "    e = get_nearest_enemy()", "Tab puts it in");
            panel.TypePython(shown + "    wait(");
            Require(panel.HintText != null && panel.HintText.StartsWith("wait(초)"), "wait( shows its hint: " + panel.HintText);
            panel.CloseCompletion();

            // 에러 and 오류: slash() on the shooter, buff() on the tank, an empty slot.
            panel.TypePython("enemy = get_nearest_enemy()\nwhile True:\n    slash()\n    shoot(enemy)\n"); panel.ReadPython();
            Require(panel.Problems.Any(p => !p.error && p.message.Contains("slash()")) && panel.IsApplied, "slash() on the shooter: 에러, still runs");
            Require(panel.Squiggles.Any(s => s.line == 3 && s.color == CommandCodingPanel.WarningLine), "yellow underline on line 3");
            Require(panel.StatusLine.GetComponent<TMP_Text>().text.Contains("오류 0 · 에러 2"), "status counts: " + panel.StatusLine.GetComponent<TMP_Text>().text);
            yield return Wait(.3);
            yield return Shot("2026-11-25_파이썬 탭 에러 노란 밑줄");
            yield return Wait(.2);
            panel.TypePython("enemy = get_nearest_enemy()\nwhile True:\n    shoot(___)\n"); panel.ReadPython();
            string why = panel.Problems.FirstOrDefault(p => p.error).message;
            Require(why != null && !panel.IsApplied && panel.Squiggles.Any(s => s.line == 3 && s.color == CommandBlockView.ErrorColor), "an empty slot: 오류, the robot stops (" + why + ")");
            panel.SetMode(CodingMode.Block); yield return Wait(.3);
            Require(panel.Problems.FirstOrDefault(p => p.error).message == why, "the same 오류 in the 블록 tab");
            var red = panel.Program.AllBlocks().FirstOrDefault(b => b.Problem != null);
            Require(red && red.ProblemIsError && red.Kind == BlockKind.Shot, "red border on the shoot line");

            // Border screen: a red line (empty slot) and a yellow one (slash() on the shooter).
            panel.LoadProgram("robot_1", new[] { new CodeBlock(BlockKind.DeclareVariable, "enemy", B(BlockKind.NearestEnemy)), Body(B(BlockKind.While), B(BlockKind.Slash), new CodeBlock(BlockKind.Shot, "0", new CodeBlock[] { null })) });
            yield return Wait(.4);
            var lines = panel.Program.AllBlocks().Where(b => b.Problem != null).ToList();
            Require(lines.Any(b => b.ProblemIsError && b.Kind == BlockKind.Shot) && lines.Any(b => !b.ProblemIsError && b.Kind == BlockKind.Slash), "red on shoot(___) and yellow on slash() together");
            Require(panel.StatusLine.GetComponent<TMP_Text>().text.Contains("오류 1 · 에러 1"), "status counts both: " + panel.StatusLine.GetComponent<TMP_Text>().text);
            var slash = panel.Program.AllBlocks().First(b => b.Kind == BlockKind.Slash);
            slash.OnPointerEnter(null);
            Require(panel.TipText != null && panel.TipText.Contains("slash()"), "hovering the yellow line shows its message: " + panel.TipText);
            yield return Wait(.3);
            yield return Shot("2026-11-25_블록 탭 오류·에러 테두리");
            yield return Wait(.2);
            slash.OnPointerExit(null);
            Require(panel.TipText == null, "the message goes when the pointer leaves");
            panel.Program.AllBlocks().First(b => b.Kind == BlockKind.Shot).Arguments[0].Insert(panel.VariableTemplate, 0);
            panel.Program.AllBlocks().First(b => b.Kind == BlockKind.Variable).SetText("enemy");
            yield return Wait(.3);
            Require(slash.Problem != null && !slash.ProblemIsError && panel.IsApplied && !panel.Program.AllBlocks().Any(b => b.ProblemIsError && b.Problem != null),
                "the slot filled: red gone, yellow stays on slash(), the robot runs");
            // Both on one line: red wins.
            panel.LoadProgram("robot_1", PythonTreeParser.Parse("while True:\n    block(___)\n").ToArray());
            var both = panel.Program.AllBlocks().First(b => b.Kind == BlockKind.Block);
            Require(both.Problem != null && both.ProblemIsError && panel.Problems.Count(p => p.statement != null) == 2, "block(___) on the shooter: 오류 and 에러, the border is red");

            // Message bubbles: a new 오류 blinks in, stays 1.5 s and fades; nothing pops again on an unchanged line; ! shows it again.
            Require(panel.BubbleText == null, "an opened program's problems do not pop up");
            panel.LoadProgram("robot_1", PythonTreeParser.Parse("while True:\n    enemy = get_nearest_enemy()\n    shoot(enemy)\n").ToArray());
            yield return Wait(.3);
            panel.Program.StartChain()[0].Body.Insert(Palette(BlockKind.Shot), 2);
            string popped = panel.BubbleText;
            Require(popped != null && popped.Contains("공격 대상"), "a new 오류 pops its message: " + popped);
            yield return Wait(.15);
            yield return Shot("2026-11-25_오류 메시지 깜박임 등장");
            float shownAt = Time.realtimeSinceStartup;
            for (double end = EditorApplication.timeSinceStartup + 20; panel.BubbleText != null && EditorApplication.timeSinceStartup < end;) yield return null;
            float lasted = Time.realtimeSinceStartup - shownAt;
            Require(panel.BubbleText == null && panel.TipText == null && panel.BubbleBlinks == 3 && lasted >= CommandCodingPanel.BubbleHold,
                $"it blinked {panel.BubbleBlinks} times, then went after {lasted:0.0} s");
            var shot = panel.Program.AllBlocks().First(b => b.Kind == BlockKind.Shot && b.Problem != null);
            Require(shot.ProblemIsError && shot.GetComponentsInChildren<Button>().Any(b => b.name == "Badge"), "the red border and ! stay");
            panel.FunctionName = "robot_9"; panel.FunctionName = "robot_1";
            Require(panel.BubbleText == null, "an edit that leaves the 오류 as it was does not pop it again");
            shot.GetComponentsInChildren<Button>().First(b => b.name == "Badge").onClick.Invoke();
            Require(panel.BubbleText == popped, "clicking ! shows the message again");
            yield return Wait(.15);
            yield return Shot("2026-11-25_느낌표 눌러 다시 보기");
            panel.SetMode(CodingMode.Python); yield return Wait(.3);
            Require(panel.BubbleText == popped || panel.BubbleText == null, "a tab switch is no new 오류");
            var gutter = panel.GutterMarks;
            Require(gutter.Count == 1, "one ! in the Python gutter (" + gutter.Count + ")");
            gutter[0].onClick.Invoke();
            Require(panel.BubbleText != null && panel.BubbleText.StartsWith("4번째 줄: ") && panel.BubbleText.Contains("공격 대상"), "the gutter's ! shows the line's message: " + panel.BubbleText);
            panel.SetMode(CodingMode.Block);

            // Bubble layout: padding round centred text, as wide as the text up to the workspace, then more lines.
            panel.LoadProgram("robot_1", PythonTreeParser.Parse("enemy = get_nearest_enemy()\nwhile True:\n    wait(___)\n    shoot(enemy)\n").ToArray());
            yield return Wait(.3);
            panel.ReshowProblem(panel.Program.AllBlocks().First(b => b.Problem != null && b.ProblemIsError));
            yield return Wait(.8);
            BubbleFits("short 오류 in the 블록 tab", 1, true);
            yield return Shot("2026-11-25_오류 말풍선 짧은 글");
            panel.SetMode(CodingMode.Natural);
            panel.LoadProgram("robot_1", PythonTreeParser.Parse("enemy = get_nearest_enemy()\nwhile True:\n    shoot(___)\n    wait(1)\n").ToArray());
            yield return Wait(.4);
            panel.ReshowProblem(panel.Program.AllBlocks().First(b => b.Problem != null && b.ProblemIsError));
            yield return Wait(.8);
            BubbleFits("long 오류 in the 자연어 tab", 2, true);
            yield return Shot("2026-11-25_오류 말풍선 긴 글 여러 줄");
            panel.SetMode(CodingMode.Python); yield return Wait(.3);
            panel.TypePython("enemy = get_nearest_enemy()\nwhile True:\n    slash()\n    shoot(enemy)\n"); panel.ReadPython();
            yield return Wait(.2);
            panel.GutterMarks[0].onClick.Invoke();
            yield return Wait(.8);
            BubbleFits("에러 in the 파이썬 tab", 0, false);
            yield return Shot("2026-11-25_에러 말풍선 파이썬 탭");
            panel.SetMode(CodingMode.Block);

            // The tank: buff() does nothing there.
            Open(tank);
            yield return Wait(.3);
            panel.LoadProgram("robot_" + tank, PythonTreeParser.Parse("while True:\n    buff()\n").ToArray());
            Require(panel.Problems.Any(p => !p.error && p.message.Contains("효과가 없어요")) && panel.IsApplied, "buff() on the tank: 에러, still runs");

            // Typed code runs like the same code built from blocks.
            Open(shooter); yield return Wait(.2);
            panel.SetMode(CodingMode.Block);
            panel.LoadProgram("robot_1", PythonTreeParser.Parse(shown).ToArray());
            string built = panel.AppliedSource;
            panel.ClearProgram();
            panel.SetMode(CodingMode.Python); yield return Wait(.2);
            panel.TypePython(shown);
            // The panel's frame, run by hand: batchmode plays few frames, so the 0.5 s pause is set back on the clock.
            var frame = typeof(CommandCodingPanel).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var edited = typeof(CommandCodingPanel).GetField("pythonEdited", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            frame.Invoke(panel, null);
            Require(panel.AppliedSource != built, "nothing applied before the 0.5 s pause");
            edited.SetValue(panel, Time.unscaledTime - .6f);
            frame.Invoke(panel, null);
            Require(panel.AppliedSource == built && RobotCode(shooter) == shown, "after the pause the typed Python applies the same code as the blocks" +
                $" [applied={panel.AppliedSource}] [built={built}] [robot={RobotCode(shooter)}] [status={panel.StatusLine.GetComponent<TMP_Text>().text}]");
            // The tab is kept when the editor closes and opens again.
            editor.Close(false); editor.Open(shooter); yield return Wait(.2);
            Require(panel.Mode == CodingMode.Python && panel.PythonText == shown, "the Python tab and its text are kept on reopening");
            panel.SetMode(CodingMode.Block);
        }

        // lines: 1 exactly one line, 2 at least two, 0 any. The text's drawn bounds sit inside the bubble with even margins.
        static void BubbleFits(string what, int lines, bool error)
        {
            var tip = (RectTransform)GameObject.Find("ProblemTip").transform;
            var label = tip.GetComponentInChildren<TextMeshProUGUI>(); label.ForceMeshUpdate();
            var pad = tip.GetComponent<VerticalLayoutGroup>().padding;
            // The glyphs' own bounds (what the eye sees), not TMP's line box.
            Vector3 lo = new Vector3(float.MaxValue, float.MaxValue), hi = new Vector3(float.MinValue, float.MinValue);
            for (int i = 0; i < label.textInfo.characterCount; i++)
            {
                var c = label.textInfo.characterInfo[i]; if (!c.isVisible) continue;
                lo = Vector3.Min(lo, c.bottomLeft); hi = Vector3.Max(hi, c.topRight);
            }
            Vector3 min = tip.InverseTransformPoint(label.transform.TransformPoint(lo)), max = tip.InverseTransformPoint(label.transform.TransformPoint(hi));
            Rect r = tip.rect;
            float left = min.x - r.xMin, right = r.xMax - max.x, top = r.yMax - max.y, bottom = min.y - r.yMin - 7; // the card's shadow is no fill
            var corners = new Vector3[4]; tip.GetWorldCorners(corners);
            var area = new Vector3[4]; ((RectTransform)panel.Program.GetComponentInParent<ScrollRect>(true).transform).GetWorldCorners(area);
            int count = label.textInfo.lineCount;
            Require(tip.GetComponent<Image>().color == (error ? CommandBlockView.ErrorColor : CommandBlockView.WarningColor) &&
                pad.left >= 12 && pad.right >= 12 && pad.top >= 6 && pad.bottom >= 13 && label.alignment == TextAlignmentOptions.CenterGeoAligned &&
                left >= 10 && right >= 10 && top >= 4 && bottom >= 4 && Mathf.Abs(left - right) <= 4 && Mathf.Abs(top - bottom) <= 3 &&
                (lines == 0 || (lines == 1 ? count == 1 : count >= lines)) && corners[2].x <= area[2].x + .5f,
                $"{what}: {r.width:0}x{r.height:0}, {count} line(s), margins L{left:0} R{right:0} T{top:0} B{bottom:0}, inside the workspace ({label.text})");
        }

        // The Game view's own frame at 1920x1080 (ScreenCapture, as ScreenTour takes its shots), so the robot preview keeps the
        // aspect it has in play; a camera drawing the canvas into a picture stretched it (batchmode's screen is not 16:9).
        static IEnumerator Shot(string name)
        {
            bool saved = false;
            Directory.CreateDirectory("Report");
            var host = new GameObject("CodingModesShot").AddComponent<ShotHost>();
            host.path = Path.Combine("Report", name + ".png"); host.done = () => saved = true;
            for (double end = EditorApplication.timeSinceStartup + 30; !saved && EditorApplication.timeSinceStartup < end;) yield return null;
            if (host) UnityEngine.Object.DestroyImmediate(host.gameObject);
            if (!saved) throw new Exception("no frame captured for " + name);
            Log("shot Report/" + name + ".png at " + Screen.width + "x" + Screen.height);
        }
        sealed class ShotHost : MonoBehaviour
        {
            public string path; public Action done;
            IEnumerator Start()
            {
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Destroy(image); done();
            }
        }
    }
}

