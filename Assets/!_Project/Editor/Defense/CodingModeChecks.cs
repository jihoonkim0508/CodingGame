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
        static IEnumerator steps;
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
            DevTest.PlayDefense(2, 1, false, new DefenseTestOptions { tutorial = false, tutorialInput = false, deploymentLimit = false, inventoryLimit = false, unlockAll = true });
        }
        static void Log(string line) { File.AppendAllText(LogPath, line + "\n"); Debug.Log("[CodingModes] " + line); }
        static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || !DevTest.Ready) return;
            try
            {
                steps ??= Steps();
                if (!steps.MoveNext()) End("PASS coding tabs: 1-6 example, round trips, errors, 에러, completion, typed code");
            }
            catch (Exception error) { End("FAIL " + error); }
        }
        static void End(string result)
        {
            Log(result); SessionState.SetBool(Running, false); steps = null;
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
            Shot("2026-11-25_파이썬 탭 문법 오류 빨간 물결 밑줄");
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
            Shot("2026-11-25_자연어 탭");
            yield return Wait(.2);
            panel.SetMode(CodingMode.Block); yield return Wait(.5);
            Shot("2026-11-25_블록 탭");
            yield return Wait(.2);
            panel.SetMode(CodingMode.Python); yield return Wait(.5);
            Shot("2026-11-25_파이썬 탭");
            yield return Wait(.2);

            // Completion and the parameter hint.
            panel.TypePython(shown + "    e = get");
            Require(panel.CompletionItems.Count >= 3 && panel.CompletionItems.All(i => i.Text.StartsWith("get")), "get offers the get_ functions");
            yield return Wait(.3);
            Shot("2026-11-25_파이썬 탭 자동 완성 목록");
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
            Shot("2026-11-25_파이썬 탭 에러 노란 밑줄");
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
            Shot("2026-11-25_블록 탭 오류·에러 테두리", () => { slash.OnPointerExit(null); slash.OnPointerEnter(null); });
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

        // The editor's canvas drawn through a camera into a 1920x1080 picture (batchmode has no game view to capture).
        static void Shot(string name, Action laidOut = null)
        {
            var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
            var camera = new GameObject("ShotCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << canvas.gameObject.layer; camera.orthographic = true;
            // Far from the scene: its props on the UI layer stay out of the picture.
            camera.transform.position = new Vector3(0, -50000, 0); camera.nearClipPlane = .1f; camera.farClipPlane = 50;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var mode = canvas.renderMode; var plane = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            if (laidOut != null) { laidOut(); Canvas.ForceUpdateCanvases(); } // placed after the picture's layout (a hover tip)
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var picture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); picture.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Report");
            File.WriteAllBytes(Path.Combine("Report", name + ".png"), picture.EncodeToPNG());
            canvas.renderMode = mode; canvas.planeDistance = plane; canvas.worldCamera = null;
            UnityEngine.Object.DestroyImmediate(picture); camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Log("shot Report/" + name + ".png");
        }
    }
}
