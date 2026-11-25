using System.Collections;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Screenshots of the first session the way a player meets it, for reviewing layout and text by eye: the title, the stage
    // map, every Stage 1-1 tutorial step, then a 2-1 robot's code editor, its battle and the wave result. Needs the Game view,
    // so it runs in the windowed Editor, not batchmode; started from the command line it quits by itself when done:
    //   Unity.exe -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.Run -logFile tour.log
    // Writes TestResults/Editor/tour/NN_name.png at 1920x1080.
    public static class ScreenTour
    {
        const string Folder = "TestResults/Editor/tour";
        static readonly DefenseTestOptions Sandbox = new DefenseTestOptions { tutorial = false, tutorialInput = false };
        // Steps yield nested waits; the top of the stack is the one running.
        static readonly System.Collections.Generic.Stack<IEnumerator> tour = new System.Collections.Generic.Stack<IEnumerator>();
        static int shot;

        static string folder = Folder;

        [MenuItem("Tools/Develop/화면 캡처 투어")]
        public static void Run() => Start(Folder, Steps());

        // 12차 120: 1-5 to the boss's entrance, then the entrance replayed with each warning draft, photographed into BossWarning/.
        public static void RunBossWarnings() => Start("BossWarning", BossWarnings());

        // Raw footage for the demo video: real play recorded clip by clip (TestResults/Demo/footage/cNN_name.mp4, 1920x1080,
        // 30 fps), cut, titled and scored afterwards. Batchmode like the tour:
        //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.RunDemo -logFile demo.log
        [MenuItem("Tools/Develop/시연 영상 촬영")]
        public static void RunDemo() => Start("TestResults/Demo/footage", Demo());

        // The tank monster (Heavy) walking in 2-1, its first wave: TestResults/Editor/tanker/01_tanker.png, and 4 s of it walking
        // (tanker_walk.mp4, 1920x1080) to check its feet stay planted.
        public static void RunTanker() => Start("TestResults/Editor/tanker", Tanker());
        static IEnumerator Tanker()
        {
            DevTest.PlayDefense(2, 1, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle();
            yield return Until(() => DevTest.Sim.Enemies.Any(e => e.Active && e.Spec.type == EnemyType.Tank), 60);
            yield return Seconds(1);
            yield return Record("tanker_walk", 4);
            yield return Shot("tanker");
        }

        // The loading screen (Assets/ThisIsCodingLoading): the stage map loads under black only, then the battle load from it
        // shows the screen for at least 1 s, photographed half a second in: TestResults/Editor/loading/01_loading.png.
        public static void RunLoading() => Start("TestResults/Editor/loading", Loading());
        static IEnumerator Loading()
        {
            DevTest.PlayStageMap();
            GameObject LoadingUp() => GameObject.Find("GameFlow/LoadingScreen(Clone)");
            bool shown = false;
            for (double end = EditorApplication.timeSinceStartup + 8; EditorApplication.timeSinceStartup < end; ) { shown |= LoadingUp() != null; yield return null; }
            Debug.Log($"[ScreenTour] stage map load showed the loading screen: {shown}");
            GameFlow.LoadScene(GameFlow.DefenseScene);
            yield return Until(() => LoadingUp(), 30);
            double shownAt = EditorApplication.timeSinceStartup;
            yield return Seconds(.5);
            yield return Shot("loading");
            yield return Until(() => !LoadingUp(), 30);
            Debug.Log($"[ScreenTour] battle loading screen up {EditorApplication.timeSinceStartup - shownAt:0.00} s");
        }

        // 3-4 and 2-4 with the support enemies: the shooter's for-each healer loop in the code editor, a healer pulsing among the
        // tanks, a shielded enemy's bubble: TestResults/Editor/support/NN_*.png. Batchmode without -nographics, like RunDemo.
        public static void RunSupportEnemies() => Start("TestResults/Editor/support", SupportEnemies());
        static IEnumerator SupportEnemies()
        {
            const string pick = "while True:\n    target = get_nearest_enemy()\n    for e in get_enemies():\n        if e.type == Enemy.Healer:\n" +
                "            target = e\n            break\n    shoot(target)";
            DevTest.PlayDefense(3, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            var battle = DevTest.Battle; var sim = battle.Simulation;
            var shooter = sim.Robots.FirstOrDefault(r => r.Spec.role == RobotRole.Ranged) ?? sim.Robots[0];
            battle.SelectRobot(shooter.Id); battle.DevelopSelected();
            yield return Seconds(.6);
            Program(TutorialAnswers.Parse(pick));
            yield return Seconds(1);
            Debug.Log("[ScreenTour] editor code: " + battle.CodeEditor.Panel.FunctionName + " applied " + battle.CodeEditor.Panel.IsApplied);
            yield return Shot("foreach_editor");
            battle.CodeEditor.Close();
            Relaxed(() => { foreach (var bot in sim.Robots.Where(r => r.Spec.role == RobotRole.Ranged)) sim.ApplyProgram(bot.Id, "pick_healer", TutorialAnswers.Parse(pick)); });
            yield return Seconds(.3);
            battle.StartBattle(); DevTest.Speed(2);
            yield return Until(() => sim.Enemies.Any(e => e.Active && e.Spec.type == EnemyType.Healer && sim.Time - e.LastAbility < .05), 60);
            DevTest.Speed(1);
            yield return Seconds(.1);
            Debug.Log("[ScreenTour] 3-4 road: " + string.Join(", ", sim.Enemies.Where(e => e.Active).Select(e => $"{e.Spec.type} hp {e.Health:0}/{e.Spec.health:0}")));
            yield return Shot("healer"); DevTest.Speed(3);
            yield return Outcome(sim);
            Debug.Log($"[ScreenTour] 3-4 with the healer loop: {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}");

            DevTest.PlayDefense(2, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            sim = DevTest.Sim;
            DevTest.Battle.StartBattle(); DevTest.Speed(2);
            yield return Until(() => sim.Enemies.Count(e => e.Active && e.Shield > 0) >= 1 && sim.Time - sim.Enemies.Where(e => e.Spec.type == EnemyType.Shielder).Select(e => e.LastAbility).DefaultIfEmpty(-9).Max() > .4, 90);
            DevTest.Speed(1);
            Debug.Log("[ScreenTour] 2-4 road: " + string.Join(", ", sim.Enemies.Where(e => e.Active).Select(e => $"{e.Spec.type} hp {e.Health:0} shield {e.Shield:0}")));
            yield return Shot("shield"); DevTest.Speed(3);
            yield return Outcome(sim);
            Debug.Log($"[ScreenTour] 2-4 with shoot(nearest): {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}");

            // 3-4 again with every shooter on the nearest enemy (the healer ignored), for comparison.
            DevTest.PlayDefense(3, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            sim = DevTest.Sim;
            DevTest.Battle.StartBattle(); DevTest.Speed(3);
            yield return Outcome(sim);
            Debug.Log($"[ScreenTour] 3-4 with shoot(nearest): {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}");

            // And 3-4 as it was before the healers and their tanks, the same robots: the baseline.
            DevTest.PlayDefense(3, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            sim = DevTest.Sim;
            sim.SetWaveGroups(3, 3, sim.Setup.Waves[3].Where(g => g.Boss || g.Enemy.type != EnemyType.Healer && g.Enemy.type != EnemyType.Tank).ToArray());
            Deploy();
            DevTest.Battle.StartBattle(); DevTest.Speed(3);
            yield return Outcome(sim);
            Debug.Log($"[ScreenTour] 3-4 without healers and tanks (before): {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}");
        }

        // 3-4 with the Queue and Stack centipedes: the shooter's front()/top() loop in the code editor, then each centipede on the
        // road with its open end ringed (under the nearest-enemy code, so the chains stay long), and the wave played out three
        // ways for comparison: TestResults/Editor/centipede/NN_*.png. Batchmode without -nographics, like RunSupportEnemies.
        public static void RunCentipedes() => Start("TestResults/Editor/centipede", Centipedes());
        const string StackQueue = "while True:\n    for e in get_enemies():\n        if e.type == Enemy.Stack:\n            shoot(e.top())\n" +
            "        elif e.type == Enemy.Queue:\n            shoot(e.front())";
        static IEnumerator Centipedes()
        {
            // Every shooter on the snippet plus `else: shoot(e)`, so the other enemies are shot too.
            string withElse = StackQueue + "\n        else:\n            shoot(e)";
            foreach (var (code, name) in new[] { (withElse, "snippet + else: shoot(e)"), (StackQueue, "snippet only"), (null, "shoot(get_nearest_enemy())") })
            {
                DevTest.PlayDefense(3, 4, true, Sandbox);
                yield return Until(() => DevTest.Ready);
                yield return Seconds(.5);
                Deploy();
                var battle = DevTest.Battle; var sim = battle.Simulation;
                if (code == withElse)
                {
                    var shooter = sim.Robots.FirstOrDefault(r => r.Spec.role == RobotRole.Ranged) ?? sim.Robots[0];
                    battle.SelectRobot(shooter.Id); battle.DevelopSelected();
                    yield return Seconds(.6);
                    Program(TutorialAnswers.Parse(StackQueue));
                    yield return Seconds(1);
                    Debug.Log("[ScreenTour] editor code: " + battle.CodeEditor.Panel.FunctionName + " applied " + battle.CodeEditor.Panel.IsApplied);
                    yield return Shot("centipede_editor");
                    battle.CodeEditor.Close();
                }
                if (code != null) Relaxed(() => { foreach (var bot in sim.Robots.Where(r => r.Spec.role == RobotRole.Ranged)) sim.ApplyProgram(bot.Id, "stack_queue", TutorialAnswers.Parse(code)); });
                yield return Seconds(.3);
                battle.StartBattle(); DevTest.Speed(3);
                if (code == null)
                {
                    foreach (var type in new[] { EnemyType.Queue, EnemyType.Stack })
                    {
                        yield return Until(() => sim.Enemies.Count(e => e.Active && e.Spec.type == type) >= 4, 90);
                        DevTest.Speed(1);
                        yield return Seconds(.6);
                        Debug.Log($"[ScreenTour] 3-4 {type}: " + string.Join(", ", sim.Enemies.Where(e => e.Active && e.Chain != null).Select(e => $"{e.Spec.type}#{e.Id}{(e.Chain.Hittable == e ? "*" : "")} hp {e.Health:0}/{e.Spec.health:0}")));
                        yield return Shot(type == EnemyType.Queue ? "queue" : "stack"); DevTest.Speed(3);
                    }
                }
                int blocked = 0, pushed = 0;
                var kinds = new System.Collections.Generic.Dictionary<int, EnemyType>();
                for (double end = EditorApplication.timeSinceStartup + 150; sim.Phase == BattlePhase.Running && EditorApplication.timeSinceStartup < end;)
                {
                    foreach (var e in sim.Enemies) kinds[e.Id] = e.Spec.type;
                    foreach (var evt in sim.Events)
                    {
                        if (evt.Kind == "blocked") blocked++;
                        if (evt.Kind == "push") pushed++;
                        if (evt.Kind == "leaked") Debug.Log($"[ScreenTour] leaked {(kinds.TryGetValue(evt.Source, out var k) ? k.ToString() : "?")} at {sim.Time:0.0} s");
                    }
                    yield return null;
                }
                Debug.Log($"[ScreenTour] 3-4 with {name}: {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}, kills {sim.Kills}, blocked hits {blocked}, stack pushes {pushed} (stack segments seen {kinds.Count(k => k.Value == EnemyType.Stack)}), {sim.Time:0.0} s");
            }
        }

        // 3-3 with the Reflectors (2026-11-25): the three-robot dock before anything is placed, then a reflector in its reflect
        // state hitting a shooter's shot back (plain shoot(nearest)), and the wave played out with plain code and with the
        // e.reflecting snippet: TestResults/Editor/reflector/NN_*.png. Batchmode without -nographics, like RunSupportEnemies.
        public static void RunReflectors() => Start("TestResults/Editor/reflector", Reflectors());
        static IEnumerator Reflectors()
        {
            const string plain = "while True:\n    e = get_nearest_enemy()\n    shoot(e)";
            foreach (var (code, name) in new[] { (plain, "shoot(get_nearest_enemy())"), (ReflectorSnippet, "e.reflecting snippet") })
            {
                DevTest.PlayDefense(3, 3, true, Sandbox);
                yield return Until(() => DevTest.Ready);
                yield return Seconds(1);
                var battle = DevTest.Battle; var sim = battle.Simulation;
                Debug.Log("[ScreenTour] 3-3 stock: " + string.Join(", ", System.Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => $"{r} {sim.RobotAvailable(r)}")));
                Deploy();
                yield return Seconds(3);
                if (code == plain) yield return Shot("robot_dock");
                Relaxed(() => { foreach (var bot in sim.Robots.Where(r => r.Spec.role == RobotRole.Ranged)) sim.ApplyProgram(bot.Id, "aim", TutorialAnswers.Parse(code)); });
                yield return Seconds(.3);
                battle.StartBattle(); DevTest.Speed(2);
                if (code == plain)
                {
                    yield return Until(() => sim.Robots.Any(r => r.Spec.role == RobotRole.Ranged && sim.Time - r.LastReflected < .03), 90);
                    DevTest.Speed(1);
                    Debug.Log("[ScreenTour] 3-3 reflect: " + string.Join(", ", sim.Enemies.Where(e => e.Active && e.Spec.type == EnemyType.Reflector).Select(e => $"#{e.Id} {(e.Reflecting ? "reflecting" : "normal")} hp {e.Health:0}")) +
                        " · robots " + string.Join(", ", sim.Robots.Select(r => $"{r.Spec.role} {r.Health:0}/{r.MaxHealth:0}")));
                    yield return Shot("reflect_hit_back"); DevTest.Speed(3);
                }
                for (double end = EditorApplication.timeSinceStartup + 150; sim.Phase == BattlePhase.Running && EditorApplication.timeSinceStartup < end;) yield return null;
                Debug.Log($"[ScreenTour] 3-3 with {name}: {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}, kills {sim.Kills}, robots " +
                    string.Join(", ", sim.Robots.Select(r => $"{r.Spec.role} {r.Health:0}/{r.MaxHealth:0}")) + $", {sim.Time:0.0} s");
            }
        }
        const string ReflectorSnippet = "while True:\n    for e in get_enemies():\n        if e.type == Enemy.Reflector and e.reflecting:\n            continue\n        shoot(e)\n        break";
        // 3-3 with the tree legion: the leaf-finding snippet in the shooter's code editor, a depth-4 tree on the road with its
        // leaves glowing (close up, before it walks into the shooter's range), then the same tree after the snippet cut some
        // leaves: TestResults/Editor/tree/NN_*.png. Batchmode without -nographics, like RunCentipedes.
        public static void RunTrees() => Start("TestResults/Editor/tree", Trees());
        const string Leaf = "while True:\n    n = get_nearest_enemy()\n    while n.left or n.right:\n        if n.left:\n            n = n.left\n" +
            "        else:\n            n = n.right\n    shoot(n)";
        static IEnumerator Trees()
        {
            DevTest.PlayDefense(3, 3, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            var battle = DevTest.Battle; var sim = battle.Simulation;
            var shooter = sim.Robots.FirstOrDefault(r => r.Spec.role == RobotRole.Ranged) ?? sim.Robots[0];
            battle.SelectRobot(shooter.Id); battle.DevelopSelected();
            yield return Seconds(.6);
            Program(TutorialAnswers.Parse(Leaf));
            yield return Seconds(1);
            Debug.Log("[ScreenTour] editor code: " + battle.CodeEditor.Panel.FunctionName + " applied " + battle.CodeEditor.Panel.IsApplied);
            yield return Shot("tree_code");
            battle.CodeEditor.Close();
            yield return Seconds(.3);
            battle.StartBattle(); DevTest.Speed(3);
            EnemyState Deep() => sim.Enemies.FirstOrDefault(e => e.Active && e.Node != null && e.Node.Parent == null &&
                sim.Enemies.Any(c => c.Active && c.Node?.Root == e && c.Node.Depth == 4));
            yield return Until(() => Deep() != null, 120);
            var tree = Deep();
            yield return Seconds(1.5); // a few steps onto the road
            if (tree == null) { Debug.LogWarning("[ScreenTour] no depth-4 tree"); yield break; }
            string Nodes() => string.Join(", ", sim.Enemies.Where(e => e.Node?.Root == tree).Select(e => $"{e.Node.Depth}/{e.Node.Slot}{(e.Active ? e.Node.Open ? "*" : "" : " dead")}"));
            var camera = Object.FindAnyObjectByType<DefenseSelectionCamera>();
            DevTest.Speed(1);
            camera.FocusBoss(new Vector3(tree.Position.X, 1.4f, tree.Position.Y));
            yield return Seconds(.8);
            camera.FocusBoss(new Vector3(tree.Position.X, 1.4f, tree.Position.Y)); // again: the battle's start may have turned it back
            yield return Seconds(1);
            Debug.Log("[ScreenTour] depth-4 tree: " + Nodes());
            yield return Shot("tree_depth4");
            // The shooter runs the snippet from the editor: leaves go first once the tree walks into its range.
            int nodes = sim.Enemies.Count(e => e.Node?.Root == tree);
            DevTest.Speed(3);
            yield return Until(() => !tree.Active || sim.Enemies.Count(e => e.Node?.Root == tree && e.Active) <= nodes - 3, 90);
            DevTest.Speed(1);
            camera.FocusBoss(new Vector3(tree.Position.X, 1.4f, tree.Position.Y));
            yield return Seconds(.9);
            Debug.Log("[ScreenTour] after the snippet: " + Nodes());
            yield return Shot("tree_cut");
            camera.Restore(); DevTest.Speed(3);
            for (double end = EditorApplication.timeSinceStartup + 150; sim.Phase == BattlePhase.Running && EditorApplication.timeSinceStartup < end;) yield return null;
            Debug.Log($"[ScreenTour] 3-3 with the snippet: {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}, kills {sim.Kills}, {sim.Time:0.0} s");
        }

        // The wave to its end, logging which kinds leaked.
        static IEnumerator Outcome(DefenseSimulation sim)
        {
            var kinds = new System.Collections.Generic.Dictionary<int, EnemyType>(); var logged = new System.Collections.Generic.HashSet<int>();
            for (double end = EditorApplication.timeSinceStartup + 120; sim.Phase == BattlePhase.Running && EditorApplication.timeSinceStartup < end;)
            {
                foreach (var e in sim.Enemies) kinds[e.Id] = e.Spec.type;
                foreach (var evt in sim.Events) if (evt.Kind == "leaked" && logged.Add(evt.Source)) Debug.Log($"[ScreenTour] leaked {(kinds.TryGetValue(evt.Source, out var k) ? k.ToString() : "?")} at {sim.Time:0.0} s");
                yield return null;
            }
        }
        static void Start(string into, IEnumerator steps)
        {
            folder = into;
            if (Directory.Exists(folder)) foreach (var old in Directory.GetFiles(folder, "*.png")) File.Delete(old);
            Directory.CreateDirectory(folder);
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "ScreenTour");
            shot = 0; tour.Clear(); tour.Push(steps);
            EditorApplication.update += Tick;
        }

        static IEnumerator BossWarnings()
        {
            DevTest.PlayDefense(1, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle(); DevTest.Speed(4);
            yield return Until(() => DevTest.Battle.EditorBossEntered, 120);
            DevTest.Speed(1);
            yield return Seconds(DefenseBossWarning.Seconds + .3);
            for (int i = 0; i < DefenseBossWarning.Drafts.Length; i++)
            {
                DevTest.Battle.EditorReplayBossEntrance(i);
                yield return Seconds(1.0); yield return Shot(DefenseBossWarning.Drafts[i]);
                yield return Seconds(DefenseBossWarning.Seconds);
            }
        }
        static void Tick()
        {
            try
            {
                if (tour.Peek().MoveNext()) { if (tour.Peek().Current is IEnumerator inner) tour.Push(inner); }
                else tour.Pop();
            }
            catch (System.Exception error) { Debug.LogError("[ScreenTour] " + error); tour.Clear(); }
            if (tour.Count > 0) return;
            EditorApplication.update -= Tick;
            Debug.Log($"[ScreenTour] {shot} shots → {folder}");
            // Quitting from inside Play Mode crashed the Editor on the way out: quit once it is back in Edit Mode. The play-mode
            // scene backup goes first, or a crash on exit leaves it and the next Editor launch stops on "Recovering Scene Backups".
            if (System.Environment.GetCommandLineArgs().Contains("-executeMethod"))
                EditorApplication.playModeStateChanged += state =>
                {
                    if (state != PlayModeStateChange.EnteredEditMode) return;
                    FileUtil.DeleteFileOrDirectory("Temp/__Backupscenes");
                    EditorApplication.delayCall += () => EditorApplication.Exit(0);
                };
            EditorApplication.isPlaying = false;
        }

        // Real seconds: the Editor runs at a few hundred frames a second, so frame counts end animations early.
        static IEnumerator Seconds(double count) { for (double end = EditorApplication.timeSinceStartup + count; EditorApplication.timeSinceStartup < end;) yield return null; }
        static IEnumerator Until(System.Func<bool> done, double seconds = 60)
        {
            for (double end = EditorApplication.timeSinceStartup + seconds; !done() && EditorApplication.timeSinceStartup < end;) yield return null;
        }
        static IEnumerator Shot(string name)
        {
            bool saved = false;
            var host = new GameObject("ScreenTour").AddComponent<Capture>();
            host.path = Path.Combine(folder, $"{++shot:00}_{name}.png"); host.done = () => saved = true;
            yield return Until(() => saved, 10);
            if (!saved) Debug.LogWarning("[ScreenTour] no frame captured for " + host.path); // the game view did not render (screen off?)
            if (host) Object.DestroyImmediate(host.gameObject);
        }
        sealed class Capture : MonoBehaviour
        {
            public string path; public System.Action done;
            IEnumerator Start()
            {
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Destroy(image); done();
            }
        }

        static void Begin(string name) => PlayRecorder.Start(Path.Combine(folder, name + ".mp4"), 1920, 30, 14);
        static void End() => Debug.Log("[ScreenTour] " + PlayRecorder.Stop());
        static IEnumerator Record(string name, double seconds) { Begin(name); yield return Seconds(seconds); End(); }

        // The demo video's scenario, one clip per shot. Every battle clip has each robot on a loop (Loop below) and
        // logs which robots are still running their code when it ends, since a robot that ran out of code stands still.
        static IEnumerator Demo()
        {
            DevTest.PlayTitle();
            yield return Until(() => EditorApplication.isPlaying);
            yield return Record("c01_title", 9); // the intro, then the menu
            DevTest.PlayStageMap();
            yield return Seconds(.6);
            yield return Record("c02_map", 4);

            // 1-1 as a new player meets it: the system alert and 디버; the road, the range, slash() going into the slot.
            DevTest.PlayDefense(1, 1, true);
            yield return Until(() => DevTest.Ready);
            yield return Record("c03_alert", 8);
            var steps = DevTest.TutorialSections(1);
            int Step(string[] list, string id) => System.Array.FindIndex(list, s => s.Split(' ')[0] == id);
            foreach (var (id, clip) in new[] { ("A04a", "c04_road"), ("B04", "c05_range") })
            {
                DevTest.JumpTutorial(1, Step(steps, id));
                yield return Seconds(.3);
                yield return Record(clip, 4);
            }
            // The editor opens first: C05 points at the slash() block in its palette.
            LessonEditor("C05");
            yield return Seconds(.3);
            DevTest.JumpTutorial(1, Step(steps, "C05"));
            yield return Seconds(.3);
            Begin("c06_slash");
            yield return Seconds(2);
            Program(new CodeBlock(BlockKind.Slash)); // where the drag would put it; 디버 moves on to C09
            yield return Seconds(3.5);
            End();

            // 1-3, the loop lesson: while True goes in, then slash() inside it; then that wave with every robot looping.
            var loopSteps = DevTest.TutorialSections(3);
            // 1-3 is loaded on its 개발 line (A04), so its editor can be open before the jump to C05.
            DevTest.JumpTutorial(3, Step(loopSteps, "A04"));
            yield return Until(() => DevTest.Ready && DevTest.Sim.WaveIndex == 2, 30);
            yield return Seconds(.5);
            LessonEditor("C05");
            yield return Seconds(.3);
            DevTest.JumpTutorial(3, Step(loopSteps, "C05"));
            yield return Seconds(.3);
            Begin("c07_loop");
            yield return Seconds(1.8);
            Program(new CodeBlock(BlockKind.While));
            yield return Seconds(2.2);
            DevTest.JumpTutorial(3, Step(loopSteps, "C08"));
            yield return Seconds(.3);
            LessonEditor("C08");
            yield return Seconds(1.2);
            var loop = new CodeBlock(BlockKind.While); loop.Body.Add(new CodeBlock(BlockKind.Slash));
            Program(loop);
            yield return Seconds(3.5);
            End();
            // Its wave without the tutorial, whose last line holds 전투 시작 until it is clicked through.
            DevTest.PlayDefense(1, 3, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            DevTest.Battle.StartBattle();
            yield return Engage(1); // of the two warriors only one stands where the enemies pass
            yield return Battle("c08_first_battle", 8);

            // 2-1: a shooter's program built block by block (the preview runs it), the block help, then the wave.
            DevTest.PlayDefense(2, 1, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            var battle = DevTest.Battle;
            var shooter = battle.Simulation.Robots.FirstOrDefault(r => r.Spec.NativeAction == RobotAction.Attack) ?? battle.Simulation.Robots[0];
            battle.SelectRobot(shooter.Id); battle.DevelopSelected();
            yield return Seconds(.6);
            Begin("c09_code");
            yield return Seconds(1);
            // One block per beat; the if goes in with its attack, since an empty if does not compile.
            string[] lines = { "while True:", "    enemy = get_nearest_enemy()", "    d = enemy.distance(get_my_pos())",
                "    r = my_attack_range()", "    if d <= r:\n        shoot(enemy)" };
            for (int n = 1; n <= lines.Length; n++)
            {
                Program(TutorialAnswers.Parse(string.Join("\n", lines.Take(n))));
                yield return Seconds(1);
            }
            yield return Seconds(2.5);
            End();
            var help = Object.FindFirstObjectByType<DefenseBlockHelp>(FindObjectsInactive.Include);
            Begin("c10_help");
            yield return Seconds(.5);
            if (help) help.Open();
            yield return Seconds(3.5);
            if (help) help.Close();
            End();
            battle.CodeEditor.Close();
            yield return Seconds(.3);
            Begin("c11_start"); // the WAVE 1 banner
            battle.StartBattle();
            yield return Seconds(3);
            End();
            yield return Engage(2);
            yield return Battle("c11_battle", 10);

            // 3-5 with every robot out: six roles, spiders, flying runners and tanks.
            DevTest.PlayDefense(3, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle();
            yield return Engage(4);
            yield return Battle("c12_battle_big", 12);

            // 3-2: the bombers and tanks Stage 3 brings in.
            DevTest.PlayDefense(3, 2, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle();
            yield return Engage(3);
            yield return Battle("c12b_stage3", 10);

            // 3-5 with one robot: enemies reach the core, then the defeat panel.
            DevTest.PlayDefense(3, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            var sim = DevTest.Sim;
            var lone = sim.Robots.FirstOrDefault(r => r.Spec.NativeAction == RobotAction.Attack) ?? sim.Robots[0]; // one that fights back
            foreach (var extra in sim.Robots.Where(r => r != lone).ToArray()) sim.RemoveRobot(extra.Id);
            DevTest.Battle.StartBattle(); DevTest.Speed(4);
            int full = sim.BaseHealth;
            yield return Until(() => sim.BaseHealth < full, 60);
            DevTest.Speed(1);
            yield return Battle("c13_leak", 5);
            DevTest.RunWave();
            yield return Record("c14_defeat", 6);

            // 2-1 won: the clear banner and 디버's debrief, scrolled down to its cards.
            DevTest.PlayDefense(2, 1, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle(); DevTest.RunWave();
            Begin("c15_result");
            yield return Seconds(5);
            var scroll = GameObject.Find("Debrief") ? GameObject.Find("Debrief").GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            for (double t0 = EditorApplication.timeSinceStartup, t = 0; scroll && t < 1; t = (EditorApplication.timeSinceStartup - t0) / 1.5)
            { scroll.verticalNormalizedPosition = 1 - (float)t; yield return null; }
            if (scroll) scroll.verticalNormalizedPosition = 0;
            yield return Seconds(2);
            End();

            // 1-5: the boss's entrance with the game's warning, then the fight to the wave's end.
            DevTest.PlayDefense(1, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle(); DevTest.Speed(4);
            yield return Until(() => DevTest.Battle.EditorBossEntered, 120);
            DevTest.Speed(1);
            yield return Seconds(DefenseBossWarning.Seconds + .5);
            Begin("c16_boss");
            DevTest.Battle.EditorReplayBossEntrance(DefenseBossWarning.Style);
            yield return Seconds(DefenseBossWarning.Seconds + 2.5);
            End();
            yield return Battle("c17_boss_fight", 12);

            // 2-5 won: the last wave's reward, then the stage clear with its stars.
            DevTest.PlayDefense(2, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle(); DevTest.RunWave();
            Begin("c18_clear");
            yield return Seconds(4.5);
            DevTest.Battle.ContinueCampaign();
            yield return Seconds(5.5);
            End();
        }

        // Code put in the open editor the way a drag would leave it (applied to the robot, checked by the tutorial).
        static void Program(params CodeBlock[] blocks)
        {
            var editor = DevTest.Battle.CodeEditor;
            Relaxed(() => editor.Panel.LoadProgram(editor.Panel.FunctionName, blocks));
        }
        // Block counts and unlocks off for a moment: the footage's code may use blocks the stage has not handed out yet.
        static void Relaxed(System.Action action)
        {
            var setup = DevTest.Sim.Setup; var options = setup.TestOptions;
            setup.TestOptions = options.Copy(); setup.TestOptions.unlockAll = true; setup.TestOptions.inventoryLimit = false;
            try { action(); } finally { setup.TestOptions = options; }
        }
        // Every robot on a loop. AutoDeploy gives Stage 1 robots one command (no loop blocks there yet), and a robot that has
        // run its code once stands still for the rest of the clip; tutorial code (slash() alone) the same.
        static void Loop()
        {
            var sim = DevTest.Sim;
            Relaxed(() =>
            {
                foreach (var bot in sim.Robots.Where(r => r.Program != null && !r.Program.IsEmpty &&
                    r.Program.BlockCount(BlockKind.While) + r.Program.BlockCount(BlockKind.For) == 0).ToArray())
                {
                    var loop = new CodeBlock(BlockKind.While); loop.Body.AddRange(bot.Program.CopyBlocks());
                    sim.ApplyProgram(bot.Id, bot.Program.Name, new[] { loop });
                }
            });
            foreach (var bot in sim.Robots) Debug.Log($"[ScreenTour] {bot.Spec.role}: {bot.Program?.Source?.Replace("\n", " / ")}");
        }
        // Enemies walk a while before they reach the robots: the wave runs at three times speed until that many robots have
        // acted (hit something, buffed someone), so the clip opens on the fight instead of robots waiting.
        static IEnumerator Engage(int robots, double seconds = 40)
        {
            var sim = DevTest.Sim;
            DevTest.Speed(3);
            yield return Until(() => sim.Robots.Count(r => r.ActionsThisWave > 0) >= Mathf.Min(robots, sim.Robots.Count), seconds);
            DevTest.Speed(1);
        }
        // A battle clip, then which robots still run their code and what each did during it: an idle robot shows in the log.
        static IEnumerator Battle(string name, double seconds)
        {
            var sim = DevTest.Sim;
            var before = sim.Robots.ToDictionary(r => r.Id, r => r.ActionsThisWave);
            yield return Record(name, seconds);
            Debug.Log($"[ScreenTour] {name}: " + string.Join(", ", sim.Robots.Select(r => $"{r.Spec.role} " +
                $"{(r.Program != null && r.Program.Running ? "running" : "IDLE")} {r.ActionsThisWave - (before.TryGetValue(r.Id, out int n) ? n : 0)} actions")));
        }

        // The coding lessons (C02 to D09) happen in the code editor, which a tutorial jump does not open. Opened and closed
        // directly, the way Playtest does, since the tutorial locks the 개발 button while a line is showing.
        static void LessonEditor(string id)
        {
            var lesson = DevTest.Battle;
            bool coding = id.StartsWith("C") && id != "C01" || id.StartsWith("D") && id != "D10";
            if (coding && !lesson.CodeEditor.IsOpen && lesson.Simulation.Robots.Count > 0)
                lesson.CodeEditor.Open(lesson.Simulation.Robots.OrderBy(r => r.Id).First().Id);
            else if (!coding && lesson.CodeEditor.IsOpen) lesson.CodeEditor.Close(false);
        }

        // DevTest.AutoDeploy gives a shooter shoot() with no target, which the compiler now rejects; the tour fills in
        // shoot(enemy) for any robot left without a program.
        static void Deploy()
        {
            // AutoDeploy places every robot first, then runs out of blocks part-way through the programs (a later robot was
            // left with none, or with a stand-in slash() it cannot use): each robot gets its own ability in a loop instead.
            try { DevTest.AutoDeploy(); } catch (System.FormatException) { }
            var sim = DevTest.Sim;
            Relaxed(() =>
            {
                foreach (var bot in sim.Robots)
                {
                    if (bot.Spec.NativeAction == RobotAction.Slash) // only with an enemy in reach, or it swings at nothing
                    {
                        sim.ApplyProgram(bot.Id, "robot_" + bot.Id, TutorialAnswers.Parse("while True:\n    enemy = get_nearest_enemy()\n" +
                            "    d = enemy.distance(get_my_pos())\n    r = my_attack_range()\n    if d <= r:\n        slash()"));
                        continue;
                    }
                    var kind = DefenseProgression.ActionBlock(bot.Spec.NativeAction);
                    var loop = new CodeBlock(BlockKind.While);
                    loop.Body.Add(PythonTreeCompiler.HasEnemyArgument(kind) ? new CodeBlock(kind, "0", new CodeBlock(BlockKind.NearestEnemy)) : new CodeBlock(kind));
                    sim.ApplyProgram(bot.Id, "robot_" + bot.Id, new[] { loop });
                }
            });
            Loop();
        }

        static IEnumerator Steps()
        {
            // -tourBattleOnly skips the title, the map and the 1-1 steps.
            if (!System.Environment.GetCommandLineArgs().Contains("-tourBattleOnly"))
            {
                DevTest.PlayTitle();
                yield return Until(() => EditorApplication.isPlaying);
                yield return Seconds(9); yield return Shot("title"); // after the intro, with the menu
                DevTest.PlayStageMap();
                yield return Seconds(2.5); yield return Shot("map");

                DevTest.PlayDefense(1, 1, true);
                yield return Until(() => DevTest.Ready);
                yield return Seconds(1.5);
                var steps = DevTest.TutorialSections(1);
                for (int i = 0; i < steps.Length; i++)
                {
                    try { DevTest.JumpTutorial(1, i); }
                    catch (System.InvalidOperationException error) { Debug.LogWarning($"[ScreenTour] 1-1 {steps[i]}: {error.Message}"); continue; }
                    yield return Seconds(.3); // the jump applies on the director's next update
                    LessonEditor(steps[i].Split(' ')[0]);
                    yield return Seconds(1.7);
                    // The letters keep pace with 디버's voice, so a long line is still typing: show it whole.
                    var talk = Object.FindFirstObjectByType<TutorialDialogueView>(); if (talk) talk.FinishTyping();
                    yield return Seconds(.1);
                    yield return Shot("1-1_" + steps[i].Split(' ')[0]);
                }
            }

            // Stage 2's opening with the tutorial on: the system banner, then 디버's first line.
            DevTest.PlayDefense(2, 1, true, new DefenseTestOptions());
            yield return Until(() => DevTest.Ready);
            yield return Seconds(2); yield return Shot("2-1_intro_banner");
            yield return Seconds(4);
            var intro = Object.FindFirstObjectByType<TutorialDialogueView>(); if (intro) intro.FinishTyping();
            yield return Seconds(.1); yield return Shot("2-1_intro");

            DevTest.PlayDefense(2, 1, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            yield return Seconds(.5); yield return Shot("2-1_ready");
            var battle = DevTest.Battle;
            battle.SelectRobot(battle.Simulation.Robots[0].Id); battle.DevelopSelected();
            yield return Seconds(1); yield return Shot("2-1_code");
            var palette = Object.FindFirstObjectByType<CodingGame.BlockCoding.CommandCodingPanel>();
            if (palette) { palette.ShowCategory(0); yield return Seconds(.4); yield return Shot("2-1_code_control"); }
            var help = Object.FindFirstObjectByType<DefenseBlockHelp>(FindObjectsInactive.Include);
            if (help) { help.Open(); yield return Seconds(.4); yield return Shot("2-1_block_help"); help.Close(); }
            battle.CodeEditor.Close();
            battle.StartBattle();
            yield return Seconds(.4); yield return Shot("2-1_start"); // the moment the wave begins
            var hud = Object.FindFirstObjectByType<DefensePlayerHUD>();
            hud.ToggleMenu(); yield return Seconds(.4); yield return Shot("2-1_menu"); hud.ToggleMenu();
            // Just after the first hit, with its damage number up.
            yield return Until(() => battle.Simulation.Robots.Any(bot => bot.DamageThisWave > 0), 30);
            yield return Seconds(.15); yield return Shot("2-1_battle");
            DevTest.RunWave();
            yield return Seconds(.5); yield return Shot("2-1_banner"); // the clear banner over the map
            yield return Seconds(3.5); yield return Shot("2-1_result"); // past the banner, on the reward panel
            var scroll = GameObject.Find("Debrief") ? GameObject.Find("Debrief").GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (scroll) { scroll.verticalNormalizedPosition = 0; yield return Seconds(.3); yield return Shot("2-1_result_cards"); }

            // 3-5 with one robot, sped up: the moment an enemy reaches the core.
            DevTest.PlayDefense(3, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            var sim = DevTest.Sim;
            foreach (var extra in sim.Robots.Skip(1).ToArray()) sim.RemoveRobot(extra.Id);
            DevTest.Battle.StartBattle(); DevTest.Speed(4);
            int full = sim.BaseHealth;
            yield return Until(() => sim.BaseHealth < full, 60);
            yield return Seconds(.12); yield return Shot("3-5_leak");
            DevTest.Speed(1);
            DevTest.RunWave();
            yield return Seconds(4); yield return Shot("3-5_defeat");

            // 2-5 won: the last wave's reward, then the stage clear.
            DevTest.PlayDefense(2, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); DevTest.Battle.StartBattle(); DevTest.RunWave();
            yield return Seconds(4); yield return Shot("2-5_reward");
            DevTest.Battle.ContinueCampaign();
            yield return Seconds(4); yield return Shot("2-5_victory");
        }
    }
}
