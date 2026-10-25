using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    // Integration check: uses the live editor, inventory, tutorial predicates and battle entry points.
    // Native pointer/keyboard interaction is checked separately; this is not a replacement for that check.
    [InitializeOnLoad]
    public static class BeginnerLessonChecks
    {
        const string Running = "CodingGame.BeginnerLessons.Check", LogPath = "TestResults/Editor/beginner-lessons.log";
        const string BodyPreview = "CodingGame.BeginnerLessons.BodyPreview";
        static double bodyReadyAt;
        static string last;
        static double since, next;
        static bool practiced, previewLogged;
        static int coreAtThirdWave;
        static BeginnerLessonChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Running, false)) End("INTERRUPTED: left Play mode");
            };
        }

        [MenuItem("Tools/Develop/Preview Beginner Lesson 1-3")]
        public static void PreviewCondition()
        {
            SessionState.SetBool(Running, false);
            if (!EditorApplication.isPlaying) TutorialSequenceBuilder.ApplyFirstThree();
            DevTest.JumpTutorial(3, 0);
        }

        [MenuItem("Tools/Develop/Preview 1-3 Attack Slot")]
        public static void PreviewAttackSlot()
        {
            SessionState.SetBool(Running, false);
            SessionState.SetBool(BodyPreview, true);
            bodyReadyAt = 0;
            DevTest.JumpTutorial(3, 0);
        }


        [MenuItem("Tools/Develop/Verify Beginner Lessons 1-1 to 1-3")]
        public static void Run()
        {
            TutorialSequenceBuilder.ApplyFirstThree();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lessons integration check\n");
            last = null; practiced = false; previewLogged = false; coreAtThirdWave = -1; next = 0;
            SessionState.SetBool(Running, true);
            DevTest.PlayDefense(1, 1, true, false);
        }

        [MenuItem("Tools/Develop/Verify Beginner Lesson 1-3 Only")]
        public static void RunConditionOnly()
        {
            TutorialSequenceBuilder.ApplyFirstThree();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lesson 1-3 integration check\n");
            last = null; practiced = false; previewLogged = false; coreAtThirdWave = -1; next = 0;
            SessionState.SetBool(Running, true);
            DevTest.PlayDefense(1, 3, true, true);
        }

        static void Log(string value) => File.AppendAllText(LogPath, value + "\n");
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        static CommandDropZone Slot(DefenseCodeEditor editor, string key) => (CommandDropZone)typeof(DefenseCodeEditor)
            .GetMethod("TutorialSlot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { key });
        static void Select(CommandCodingPanel panel, CommandDropZone slot) => typeof(CommandCodingPanel)
            .GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { slot });
        static void Add(CommandCodingPanel panel, BlockKind kind) => panel.AddFromPalette(kind == BlockKind.Variable
            ? panel.VariableTemplate : panel.Palette.First(p => p.view.BaseKind == kind).view);

        static void Tick()
        {
            if (SessionState.GetBool(BodyPreview, false) && EditorApplication.isPlaying && DevTest.Ready &&
                DevTest.Sim.WaveIndex == 2 && DevTest.Battle.Tutorial.EditorSynced && DevTest.Battle.Tutorial.EditorStep != null)
            {
                // DevTest's asynchronous jump and the director may finish on adjacent editor ticks.
                if (bodyReadyAt == 0) { bodyReadyAt = EditorApplication.timeSinceStartup + 1; return; }
                if (EditorApplication.timeSinceStartup < bodyReadyAt) return;
                SessionState.SetBool(BodyPreview, false);
                var battle = DevTest.Battle; var director = battle.Tutorial;
                var sequence = director.Sequences.Single(s => s.sequenceId == "1-3");
                director.EditorStop();
                battle.SelectRobot(TutorialWatcher.TeachingRobot(DevTest.Sim).Id); battle.DevelopSelected();
                battle.CodeEditor.Panel.LoadProgram("condition_example", TutorialAnswers.Parse(
                    "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= 2.5:"));
                director.EditorRun(sequence, Array.FindIndex(sequence.steps, s => s.stepId == "D01"));
            }
            if (!SessionState.GetBool(Running, false)) return;
            try { Advance(); }
            catch (Exception error) { End("FAIL " + error); }
        }
        static void End(string result)
        {
            Log(result); SessionState.SetBool(Running, false); Time.timeScale = 1;
            if (result.StartsWith("FAIL")) Debug.LogError(result); else Debug.Log(result);
        }

        static void Advance()
        {
            if (!EditorApplication.isPlaying || !DevTest.Ready) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < next) return;
            next = now + .4;
            var battle = DevTest.Battle; var sim = DevTest.Sim; var director = battle.Tutorial;
            if (!director.EditorSynced) return;
            if (sim.WaveIndex >= 3) { End("PASS waves 1-1, 1-2, 1-3 completed; reached 1-4 without changing it"); return; }
            var step = director.EditorStep;
            if (step == null) return;
            string key = (sim.WaveIndex + 1) + "/" + step.stepId;
            if (key != last)
            {
                last = key; since = now;
                if (key == "1/A01")
                {
                    var groups = sim.UpcomingWave.ToArray();
                    Require(groups.Length == 2 && groups[0].DefinitionIndex == groups[1].DefinitionIndex &&
                        groups[0].Count == 2 && groups[1].Count == 1 &&
                        groups[1].Enemy.health > groups[0].Enemy.health * 10 &&
                        groups[1].Enemy.speed > groups[0].Enemy.speed,
                        "1-1 must use basic enemy visuals for all three spawns and strengthen only the last one");
                }
                if (key == "3/A01") coreAtThirdWave = sim.BaseHealth;
                Log("STEP " + key + " " + step.completionSignal + " " + step.trigger);
                return; // Wait for layout and target registration.
            }
            Require(now - since < 85, "Stalled at " + key);
            if (key == "3/A05" && now - since > 5 && !previewLogged)
            {
                previewLogged = true;
                var preview = Field<DefenseAttackPreview>(battle, "attackPreview");
                Log("PREVIEW A05 firstHits=" + Field<int>(preview, "firstHitCount") +
                    " signals=" + string.Join(",", Field<System.Collections.Generic.HashSet<string>>(director, "signals")));
            }
            if (sim.WaveIndex == 1 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practiced)
            {
                FreePlay(battle); practiced = true; return;
            }
            if (!director.IsActive) { Time.timeScale = 3; return; }
            var view = Field<TutorialDialogueView>(director, "view");
            if (!view.IsVisible) return; // System banner.
            if (key == "1/E04" || key == "1/E04a")
                Require(sim.Phase == BattlePhase.Running && Math.Abs(director.BattlePace - .2f) < .001f,
                    "1-1 pause introduction must show a moving slow-motion battle at " + key);
            if (key == "1/E04b")
                Require(sim.Phase == BattlePhase.Paused, "Pause button did not fully stop the battle");
            if (key == "1/E04c")
                Require(sim.Phase == BattlePhase.Running && Math.Abs(director.BattlePace - 1f) < .001f,
                    "Battle did not return to normal speed after Continue");
            director.EditorFinishTyping(); Canvas.ForceUpdateCanvases();
            foreach (var text in view.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Dialogue"))
            { text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "Dialogue overflow at " + key); }
            foreach (var target in step.texts.SelectMany(t => t.focusKeys).Where(TutorialKeys.IsEditorArea))
                Require(director.EditorBridge.TryGetArea(target, out _), "Missing focus " + target + " at " + key);
            if (step.kind != TutorialStepKind.ActionGuide) { director.EditorNext(); return; }
            var editor = battle.CodeEditor; var panel = editor.Panel;
            if (step.targetKeys.Contains("Battle.ClaimRewards"))
            {
                Log("CLEAR wave=" + (sim.WaveIndex + 1) + " core=" + sim.BaseHealth + " kills=" + sim.Kills +
                    " leaks=" + sim.Leaks + " survivors=" + sim.LastSurvivors + " timedOut=" + sim.LastWaveTimedOut);
                if (sim.WaveIndex == 0) Require(sim.BaseHealth == 11 && sim.Kills == 2 && sim.Leaks == 1,
                    "1-1 did not teach exactly one strong-enemy leak; core=" + sim.BaseHealth + " kills=" + sim.Kills + " leaks=" + sim.Leaks);
                if (sim.WaveIndex == 2) Require(sim.BaseHealth == coreAtThirdWave && sim.Kills >= 2,
                    "1-3's condition did not defend both enemies; core=" + sim.BaseHealth + " kills=" + sim.Kills);
                battle.ClaimRewards(); return;
            }
            if (step.targetKeys.Contains(TutorialKeys.Pause))
            {
                Require(battle.TutorialAllows(TutorialKeys.Pause), "Pause control blocked at " + key);
                Field<Button>(Field<DefensePlayerHUD>(battle, "playerHUD"), "pauseButton").onClick.Invoke();
                Require(sim.Phase == (step.stepId == "E04a" ? BattlePhase.Paused : BattlePhase.Running), "Pause/resume failed at " + key);
                return;
            }
            if (step.targetKeys.Contains(TutorialKeys.Speed))
            {
                Require(battle.TutorialAllows(TutorialKeys.Speed), "Speed control blocked at " + key);
                Field<Button>(Field<DefensePlayerHUD>(battle, "playerHUD"), "speedButton").onClick.Invoke();
                Require(battle.BattleSpeed == 2, "Speed did not change to 2x at " + key);
                return;
            }
            if (step.targetKeys.Contains(TutorialKeys.RobotButton(1))) { battle.ChooseRobot(1); return; }
            if (step.completionSignal == TutorialSignals.RobotPlaced) { PlaceBest(battle, 1); return; }
            if (step.targetKeys.Contains(TutorialKeys.Unit(0))) { battle.SelectRobot(TutorialWatcher.TeachingRobot(sim).Id); return; }
            if (step.completionSignal == TutorialSignals.EditorOpened) { battle.DevelopSelected(); return; }
            if (step.completionSignal == TutorialSignals.BattleStarted) { battle.StartBattle(); return; }
            if (step.completionSignal == TutorialSignals.PreviewHit ||
                step.completionSignal == TutorialSignals.PreviewSingleHit || step.completionSignal == TutorialSignals.PreviewMultiHit)
            { Require(Field<DefenseAttackPreview>(battle, "attackPreview").IsShowing, "Preview is closed at " + key); return; }
            Require(editor.IsOpen && battle.TutorialAllows(TutorialKeys.Palette), "Editor action blocked at " + key);
            if (step.completionSignal == TutorialSignals.SlotSelected) { Select(panel, Slot(editor, step.expectedCode)); return; }
            if (step.completionSignal == TutorialSignals.CodeLinePlaced)
            { Add(panel, (BlockKind)Enum.Parse(typeof(BlockKind), step.expectedCode.Split(':')[1])); return; }
            if (step.completionSignal == TutorialSignals.DraftValueMatches)
            {
                var pair = step.expectedCode.Split('|'); var path = pair[0].Split('.').Select(int.Parse).ToArray();
                var block = panel.Program.StartChain()[path[0] - 1];
                foreach (int arg in path.Skip(1)) block = block.Arguments[arg].Blocks[0];
                if (block.BaseKind == BlockKind.Comparison) block.SelectOption(Array.IndexOf(PythonTreeCompiler.Comparisons, pair[1]));
                else block.SetText(pair[1]);
                return;
            }
            if (step.completionSignal == TutorialSignals.SlotFilled || step.completionSignal == TutorialSignals.ConditionBuilt)
            { Add(panel, (BlockKind)Enum.Parse(typeof(BlockKind), step.pointerKey.Substring(TutorialKeys.PalettePrefix.Length))); return; }
            if (step.completionSignal == TutorialSignals.ProgramMatches)
            {
                if (sim.WaveIndex == 0 && step.stepId == "C08") { Add(panel, BlockKind.NearestEnemy); return; }
                var variable = panel.Program.AllBlocks().Last(b => b.BaseKind == BlockKind.Variable && b.Kind == BlockKind.Variable);
                variable.SetText("enemy");
                return;
            }
            throw new InvalidOperationException("Unknown action at " + key);
        }

        static void FreePlay(DefenseBattle battle)
        {
            var sim = battle.Simulation; var first = TutorialWatcher.TeachingRobot(sim);
            var goal = Field<TMP_Text>(Field<DefensePlayerHUD>(battle, "playerHUD"), "guide");
            Require(goal.transform.parent.gameObject.activeSelf && goal.text.Contains("변수를 사용해 적을 찾고 공격"),
                "1-2 free-play goal is not visible at the top of the HUD");
            string preserved = first.Program.Source;
            Require(battle.TutorialAllows(TutorialKeys.Start) && battle.TutorialAllows(TutorialKeys.Close), "Free play still restricts input");
            battle.ChooseRobot(1); PlaceBest(battle, 1);
            var second = sim.Robots.OrderBy(r => r.Id).Last();
            Require(second.Id != first.Id, "Practice warrior was not granted");
            battle.SelectRobot(second.Id); battle.DevelopSelected();
            var panel = battle.CodeEditor.Panel;
            panel.LoadProgram("practice", TutorialAnswers.Parse("target = get_nearest_enemy()\nslash(target)"));
            Require(panel.IsApplied, "Custom variable name did not compile");
            var hint = Field<Button>(battle.CodeEditor, "hintButton");
            Require(hint.gameObject.activeSelf, "Optional hint unavailable"); hint.onClick.Invoke();
            Require(battle.CodeEditor.HintOpen, "Hint did not open"); hint.onClick.Invoke();
            panel.LoadProgram("practice", new[] { new CodeBlock(BlockKind.Slash) });
            Require(!panel.IsApplied, "Incomplete draft unexpectedly applied");
            battle.CodeEditor.Close(); Require(!battle.CodeEditor.IsOpen, "Incomplete draft cannot close in free play");
            battle.DevelopSelected();
            Require(!panel.IsApplied, "Incomplete draft not preserved on reopen");
            panel.LoadProgram("practice", TutorialAnswers.Parse("target = get_nearest_enemy()\nslash(target)"));
            Require(panel.IsApplied && first.Program.Source == preserved, "Free practice changed the original robot");
            battle.CodeEditor.Close(); battle.StartBattle();
            Require(sim.Phase == BattlePhase.Running, "Free play cannot start battle");
            Log("PASS free play: custom names, extra warrior, optional hint, close/reopen unfinished draft, original code retained, battle start");
        }

        static void PlaceBest(DefenseBattle battle, int robot)
        {
            var sim = battle.Simulation; var spec = battle.RobotDefinition(robot).stats;
            var samples = sim.Setup.Routes.SelectMany(r => r.Points).ToArray();
            var candidates = new System.Collections.Generic.List<System.Numerics.Vector2>();
            for (float x = sim.Setup.Min.X + .5f; x < sim.Setup.Max.X; x += 1)
                for (float z = sim.Setup.Min.Y + .5f; z < sim.Setup.Max.Y; z += 1)
                {
                    var p = new System.Numerics.Vector2(x, z);
                    if (sim.PlacementError(spec, p) == null) candidates.Add(p);
                }
            var best = candidates.OrderByDescending(p => samples.Count(q => System.Numerics.Vector2.Distance(p, q) <= spec.range)).First();
            Require(battle.PlaceRobot(robot, new Vector3(best.X, 0, best.Y)), "Could not place warrior");
        }
    }
}
