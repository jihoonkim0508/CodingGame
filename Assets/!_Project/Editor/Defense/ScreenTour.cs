using System.Collections;
using System.IO;
using System.Linq;
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
        [MenuItem("Tools/Develop/보스 경고 시안 찍기")]
        public static void RunBossWarnings() => Start("BossWarning", BossWarnings());

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

        // DevTest.AutoDeploy gives a shooter attack() with no target, which the compiler now rejects; the tour fills in
        // attack(enemy) for any robot left without a program.
        static void Deploy()
        {
            try { DevTest.AutoDeploy(); } catch (System.FormatException) { }
            var sim = DevTest.Sim;
            foreach (var bot in sim.Robots.Where(r => r.Program == null || r.Program.IsEmpty).ToArray())
                try
                {
                    sim.ApplyProgram(bot.Id, "robot_" + bot.Id, TutorialAnswers.Parse(bot.Spec.NativeAction == RobotAction.Attack ?
                        "while True:\n    enemy = get_nearest_enemy()\n    attack(enemy)" : "while True:\n    slash()"));
                }
                catch (System.Exception error) { Debug.LogWarning("[ScreenTour] " + bot.Spec.role + ": " + error.Message); }
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
                    // The coding lessons (C02 to D09) happen in the code editor, which a jump does not open. Opened and closed
                    // directly, the way Playtest does, since the tutorial locks the 개발 button while a line is showing.
                    string id = steps[i].Split(' ')[0];
                    var lesson = DevTest.Battle;
                    bool coding = id.StartsWith("C") && id != "C01" || id.StartsWith("D") && id != "D10";
                    if (coding && !lesson.CodeEditor.IsOpen && lesson.Simulation.Robots.Count > 0)
                        lesson.CodeEditor.Open(lesson.Simulation.Robots.OrderBy(r => r.Id).First().Id);
                    else if (!coding && lesson.CodeEditor.IsOpen) lesson.CodeEditor.Close(false);
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
