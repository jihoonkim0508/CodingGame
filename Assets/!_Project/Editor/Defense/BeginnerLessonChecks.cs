using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
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
        static bool practiced, practicedFour, practicedFive, practicedTwoTwo, bossVisualSeen;
        static int coreAtThirdWave, killsAtFifthWave;
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

        [MenuItem("Tools/Develop/Preview 2-1 Attack Slot")]
        public static void PreviewAttackSlot()
        {
            SessionState.SetBool(Running, false);
            SessionState.SetBool(BodyPreview, true);
            bodyReadyAt = 0;
            DevTest.JumpTutorial(2, 1, 0);
        }


        [MenuItem("Tools/Develop/Verify Beginner Lessons 1-1 to 1-3")]
        public static void Run()
        {
            TutorialSequenceBuilder.ApplyFirstThree();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lessons integration check\n");
            Start(Mode.FirstThree); DevTest.PlayDefense(1, 1, true, false);
        }

        [MenuItem("Tools/Develop/Verify Beginner Lesson 1-3 Only")]
        public static void RunConditionOnly()
        {
            TutorialSequenceBuilder.ApplyFirstThree();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lesson 1-3 integration check\n");
            Start(Mode.FirstThree); DevTest.PlayDefense(1, 3, true, true);
        }

        [MenuItem("Tools/Develop/Verify Beginner Lessons 1-1 to 1-5")]
        public static void RunAllFive()
        {
            TutorialSequenceBuilder.ApplyAllFive();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lessons 1-1 to 1-5 integration check\n");
            Start(Mode.AllFive); DevTest.PlayDefense(1, 1, true, false);
        }

        [MenuItem("Tools/Develop/Verify Beginner Lessons 1-4 and 1-5")]
        public static void RunLastTwo()
        {
            TutorialSequenceBuilder.ApplyAllFive();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lessons 1-4 and 1-5 integration check\n");
            Start(Mode.AllFive); DevTest.JumpTutorial(4, 0);
        }

        [MenuItem("Tools/Develop/Verify Lessons 2-1 and 2-2")]
        public static void RunStageTwo()
        {
            TutorialSequenceBuilder.ApplyAllFive();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START lessons 2-1 and 2-2 integration check\n");
            Start(Mode.StageTwo); DevTest.PlayDefense(2, 1, true, true);
        }

        enum Mode { FirstThree, AllFive, StageTwo }
        const string ModeKey = "CodingGame.BeginnerLessons.Mode";
        static void Start(Mode mode)
        {
            last = null; practiced = practicedFour = practicedFive = practicedTwoTwo = bossVisualSeen = false;
            coreAtThirdWave = -1; next = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetInt(ModeKey, (int)mode);
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
                DevTest.Battle.Stage == 2 && DevTest.Sim.WaveIndex == 0 && DevTest.Battle.Tutorial.EditorSynced && DevTest.Battle.Tutorial.EditorStep != null)
            {
                // DevTest's asynchronous jump and the director may finish on adjacent editor ticks.
                if (bodyReadyAt == 0) { bodyReadyAt = EditorApplication.timeSinceStartup + 1; return; }
                if (EditorApplication.timeSinceStartup < bodyReadyAt) return;
                SessionState.SetBool(BodyPreview, false);
                var battle = DevTest.Battle; var director = battle.Tutorial;
                var sequence = director.Sequences.Single(s => s.sequenceId == "2-1");
                director.EditorStop();
                if (DevTest.Sim.Robots.Count == 0) DevTest.PlaceTeachingRobot(battle, RobotRole.Shooter);
                battle.SelectRobot(TutorialWatcher.TeachingRobot(DevTest.Sim).Id); battle.DevelopSelected();
                battle.CodeEditor.Panel.LoadProgram("condition_example", TutorialAnswers.Parse("while True:\n    enemy = get_nearest_enemy()\n" +
                    "    enemy_distance = enemy.distance(get_my_pos())\n    if enemy_distance <= 3:"));
                director.EditorRun(sequence, Array.FindIndex(sequence.steps, s => s.stepId == "E01"));
            }
            if (!SessionState.GetBool(Running, false)) return;
            try { Advance(); }
            catch (Exception error) { End("FAIL " + error); }
        }
        static void End(string result)
        {
            Log(result); SessionState.SetBool(Running, false); Time.timeScale = 1;
            if (result.StartsWith("FAIL")) Debug.LogError(result); else Debug.Log(result);
            // Run from the command line (-batchmode -executeMethod ...): quit with the result.
            if (Application.isBatchMode) EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 1);
        }

        static void Advance()
        {
            if (!EditorApplication.isPlaying || !DevTest.Ready) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < next) return;
            next = now + .4;
            var battle = DevTest.Battle; var sim = DevTest.Sim; var director = battle.Tutorial;
            if (!director.EditorSynced) return;
            var mode = (Mode)SessionState.GetInt(ModeKey, 0);
            bool allFive = mode == Mode.AllFive;
            if (mode == Mode.FirstThree && sim.WaveIndex >= 3) { End("PASS waves 1-1, 1-2, 1-3 completed; reached 1-4 without changing it"); return; }
            if (mode == Mode.StageTwo && battle.Stage == 2 && sim.WaveIndex >= 2) { End("PASS waves 2-1, 2-2 completed; reached 2-3"); return; }
            if (allFive && sim.Phase == BattlePhase.Victory)
            {
                Require(bossVisualSeen && Field<bool>(battle, "bossCameraRestored"), "Boss entrance or camera return was not completed");
                End("PASS waves 1-1 to 1-5 completed, minions defeated before animated boss entrance"); return;
            }
            if (allFive && sim.WaveIndex == 4 && sim.Phase == BattlePhase.Running && !bossVisualSeen)
            {
                var boss = sim.Enemies.FirstOrDefault(e => e.Boss);
                if (boss != null)
                {
                    var views = Field<System.Collections.Generic.Dictionary<int, DefenseActorView>>(battle, "enemyViews");
                    if (views.TryGetValue(boss.Id, out var enemyView) &&
                        Time.unscaledTime - Field<float>(enemyView.GetComponent<DefenseBossAura>(), "appearedAt") >= 1.2f)
                    {
                        Require(sim.Kills >= killsAtFifthWave + 3 && sim.Enemies.All(enemy => enemy.Boss),
                            "Boss appeared before every minion died");
                        Require(enemyView.GetComponent<DefenseBossAura>() && enemyView.transform.localScale.x > 1.5f &&
                            enemyView.GetComponentInChildren<ParticleSystem>().isPlaying &&
                            enemyView.GetComponentInChildren<Light>().intensity > 1 && Field<bool>(battle, "bossEntranceStarted"),
                            "Boss must use the basic enemy model with visible enlarged aura");
                        CaptureBoss(battle);
                        bossVisualSeen = true;
                    }
                }
            }
            var step = director.EditorStep;
            if (step == null) return;
            string key = battle.Stage + "-" + (sim.WaveIndex + 1) + "/" + step.stepId;
            if (key != last)
            {
                last = key; since = now;
                if (key == "1-1/A01")
                {
                    var groups = sim.UpcomingWave.ToArray();
                    Require(groups.Length == 2 && groups[0].DefinitionIndex == groups[1].DefinitionIndex &&
                        groups[0].Count == 2 && groups[1].Count == 1 &&
                        groups[1].Enemy.health > groups[0].Enemy.health * 10 &&
                        groups[1].Enemy.speed > groups[0].Enemy.speed,
                        "1-1 must use basic enemy visuals for all three spawns and strengthen only the last one");
                }
                if (key == "1-3/A01") coreAtThirdWave = sim.BaseHealth;
                if (key == "1-5/A01") killsAtFifthWave = sim.Kills;
                Log("STEP " + key + " " + step.completionSignal + " " + step.trigger);
                return; // Wait for layout and target registration.
            }
            Require(now - since < 85, "Stalled at " + key + " phase=" + sim.Phase + " time=" + sim.Time.ToString("0.0") +
                " enemies=" + string.Join(",", sim.Enemies.Select(e => (e.Boss ? "boss" : "minion") + ":" + e.Health.ToString("0.0"))) +
                " runs=" + string.Join(",", sim.Robots.Select(r => r.Program?.CompletedRuns.ToString() ?? "none")));
            if (key == "1-3/B03" || key == "2-1/B03")
            {
                var newest = sim.Robots.OrderBy(robot => robot.Id).Last();
                Require(newest.Spec.role == RobotRole.Shooter && TutorialWatcher.TeachingRobot(sim)?.Id == newest.Id,
                    key + " must teach the shooter just placed");
                var views = Field<System.Collections.Generic.Dictionary<int, DefenseActorView>>(battle, "robotViews");
                Require(Field<TutorialInputBridge>(director, "bridge").CanPerformAction(TutorialKeys.Unit(0), views[newest.Id].gameObject),
                    key + " tutorial arrow points at a different robot");
            }
            if (key == "1-1/B05")
            {
                var bridge = Field<TutorialInputBridge>(director, "bridge");
                Require(Field<System.Collections.Generic.List<Rect>>(bridge, "roundAreas").Count > 0 &&
                    Field<System.Collections.Generic.List<Rect>>(bridge, "outlinedAreas").Count == 0,
                    "The tutorial draws a duplicate outline over the robot's range ring");
            }
            if (battle.Stage == 1 && sim.WaveIndex == 1 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practiced)
            {
                FreePlay(battle); practiced = true; return;
            }
            if (battle.Stage == 2 && sim.WaveIndex == 1 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practicedTwoTwo)
            { FreePlayTwoTwo(battle); practicedTwoTwo = true; return; }
            if (allFive && battle.Stage == 1 && sim.WaveIndex == 3 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practicedFour)
            { FreePlayFour(battle); practicedFour = true; return; }
            if (allFive && battle.Stage == 1 && sim.WaveIndex == 4 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practicedFive)
            { FreePlayFive(battle); practicedFive = true; return; }
            if (!director.IsActive) { Time.timeScale = 3; return; }
            var view = Field<TutorialDialogueView>(director, "view");
            if (!view.IsVisible) return; // System banner.
            if (key.StartsWith("1-1/C", StringComparison.Ordinal) && key != "1-1/C01" || key.StartsWith("1-1/D", StringComparison.Ordinal) && key != "1-1/D09")
                Require(!battle.TutorialBlocksPreview && Field<DefenseAttackPreview>(battle, "attackPreview").IsShowing,
                    "1-1 preview stopped while placing blocks at " + key);
            if (!string.IsNullOrWhiteSpace(step.dragDemo))
                Require(Field<TutorialPointer>(director, "pointer").IsDragVisible,
                    "Block placement drag preview is missing at " + key);
            if (key == "1-1/E04" || key == "1-1/E04a")
                Require(sim.Phase == BattlePhase.Running && Math.Abs(director.BattlePace - .2f) < .001f,
                    "1-1 pause introduction must show a moving slow-motion battle at " + key);
            if (key == "1-1/E04" || key == "1-1/E04a" || key == "1-1/E04b")
            {
                var speed = Field<Button>(Field<DefensePlayerHUD>(battle, "playerHUD"), "speedButton");
                Require(!battle.SpeedUnlocked && !speed.interactable && battle.BattleSpeed == 1,
                    "Speed is available before its 1-1 introduction at " + key);
                battle.ToggleSpeed();
                Require(battle.BattleSpeed == 1, "Speed changed while locked at " + key);
            }
            if (key == "1-1/E04b")
                Require(sim.Phase == BattlePhase.Paused, "Pause button did not fully stop the battle");
            if (key == "1-1/E04c")
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
                if (battle.Stage == 1 && sim.WaveIndex == 0) Require(sim.BaseHealth == 11 && sim.Kills == 2 && sim.Leaks == 1,
                    "1-1 did not teach exactly one strong-enemy leak; core=" + sim.BaseHealth + " kills=" + sim.Kills + " leaks=" + sim.Leaks);
                if (battle.Stage == 1 && sim.WaveIndex == 2) Require(sim.BaseHealth == coreAtThirdWave && sim.Kills >= 2,
                    "1-3's shooter and warriors did not defend both enemies; core=" + sim.BaseHealth + " kills=" + sim.Kills);
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
                Require(battle.SpeedUnlocked && battle.TutorialAllows(TutorialKeys.Speed) &&
                    Field<Button>(Field<DefensePlayerHUD>(battle, "playerHUD"), "speedButton").interactable,
                    "Speed control did not unlock at " + key);
                var speed = Field<Button>(Field<DefensePlayerHUD>(battle, "playerHUD"), "speedButton");
                speed.onClick.Invoke();
                Require(battle.BattleSpeed == 2, "Speed did not change to 2x at " + key);
                Require(director.CanPerformAction(TutorialKeys.Speed) && speed.interactable,
                    "Speed became locked again immediately after the lesson");
                for (int x = 3; x <= DefenseBattle.MaxSpeed; x++)
                { speed.onClick.Invoke(); Require(battle.BattleSpeed == x, $"Speed cannot reach {x}x after the tutorial introduction"); }
                speed.onClick.Invoke();
                Require(battle.BattleSpeed == 1, $"Speed cannot return to 1x after {DefenseBattle.MaxSpeed}x");
                return;
            }
            var robotButton = step.targetKeys.FirstOrDefault(k => k.StartsWith(TutorialKeys.RobotButtonPrefix, StringComparison.Ordinal));
            if (robotButton != null) { battle.ChooseRobot(int.Parse(robotButton.Substring(TutorialKeys.RobotButtonPrefix.Length))); return; }
            if (step.completionSignal == TutorialSignals.RobotPlaced)
            {
                if (key == "1-1/B02")
                {
                    var button = Field<Button[]>(Field<DefensePlayerHUD>(battle, "playerHUD"), "robotButtons")[1];
                    battle.BeginRobotDrag(1);
                    battle.EndRobotDrag(RectTransformUtility.WorldToScreenPoint(null, button.transform.position));
                    Require(Field<int>(battle, "palette") == 1 && sim.Robots.Count == 0,
                        "Releasing a slight drag over the robot button cleared the selected robot");
                }
                PlaceBest(battle, Field<int>(battle, "palette")); return;
            }
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
                var pair = step.expectedCode.Split('|'); var path = pair[0].Split('.');
                var block = Line(panel, path[0]);
                foreach (int arg in path.Skip(1).Select(int.Parse)) block = block.Arguments[arg].Blocks[0];
                if (block.BaseKind == BlockKind.Comparison) block.SelectOption(Array.IndexOf(PythonTreeCompiler.Comparisons, pair[1]));
                else block.SetText(pair[1]);
                return;
            }
            if (step.completionSignal == TutorialSignals.SlotFilled || step.completionSignal == TutorialSignals.ConditionBuilt)
            { Add(panel, (BlockKind)Enum.Parse(typeof(BlockKind), step.pointerKey.Substring(TutorialKeys.PalettePrefix.Length))); return; }
            if (step.completionSignal == TutorialSignals.ProgramMatches)
            {
                // The last block comes from the palette (1-1 D05: slash() into the loop), or the last variable gets its name.
                if (step.pointerKey?.StartsWith(TutorialKeys.PalettePrefix, StringComparison.Ordinal) == true)
                { Add(panel, (BlockKind)Enum.Parse(typeof(BlockKind), step.pointerKey.Substring(TutorialKeys.PalettePrefix.Length))); return; }
                var variable = panel.Program.AllBlocks().Last(b => b.BaseKind == BlockKind.Variable && b.Kind == BlockKind.Variable);
                variable.SetText("enemy");
                return;
            }
            throw new InvalidOperationException("Unknown action at " + key);
        }

        // "3" is line 3 of the start chain, "1>2" the second line inside line 1's body (the tutorial key form).
        static CommandBlockView Line(CommandCodingPanel panel, string path)
        {
            System.Collections.Generic.IReadOnlyList<CommandBlockView> lines = panel.Program.StartChain();
            CommandBlockView block = null;
            foreach (var step in path.Split('>')) { if (block) lines = block.Body.Blocks; block = lines[int.Parse(step) - 1]; }
            return block;
        }

        static void FreePlay(DefenseBattle battle)
        {
            var sim = battle.Simulation; var first = TutorialWatcher.TeachingRobot(sim);
            var goal = Field<TMP_Text>(Field<DefensePlayerHUD>(battle, "playerHUD"), "guide");
            Require(goal.transform.parent.gameObject.activeSelf && goal.text.Contains("새 전사를 배치하고 while True 반복"),
                "1-2 free-play goal is not visible at the top of the HUD");
            Require(sim.Robots.Count(r => r.Spec.role == RobotRole.Warrior) == 2 && sim.StartError()?.Contains("코드") == true,
                "1-2 free play must start with the new warrior placed on its marked cell and not yet coded");
            battle.StartBattle();
            Require(sim.Phase == BattlePhase.Ready, "1-2 auto-started with an uncoded new warrior");
            string preserved = first.Program.Source;
            Require(battle.TutorialAllows(TutorialKeys.Start) && battle.TutorialAllows(TutorialKeys.Close), "Free play still restricts input");
            var second = sim.Robots.OrderBy(r => r.Id).Last();
            Require(second.Id != first.Id, "Practice warrior was not granted");
            battle.SelectRobot(second.Id); battle.DevelopSelected();
            var panel = battle.CodeEditor.Panel;
            panel.LoadProgram("practice", TutorialAnswers.Parse("while True:\n    slash()"));
            Require(panel.IsApplied, "The practice loop did not compile");
            var hint = Field<Button>(battle.CodeEditor, "hintButton");
            Require(hint.gameObject.activeSelf, "Optional hint unavailable"); hint.onClick.Invoke();
            Require(battle.CodeEditor.HintOpen, "Hint did not open"); hint.onClick.Invoke();
            panel.LoadProgram("practice", new[] { new CodeBlock(BlockKind.For) });
            Require(!panel.IsApplied, "Incomplete draft unexpectedly applied");
            battle.CodeEditor.Close(); Require(!battle.CodeEditor.IsOpen, "Incomplete draft cannot close in free play");
            battle.DevelopSelected();
            Require(!panel.IsApplied, "Incomplete draft not preserved on reopen");
            panel.LoadProgram("practice", TutorialAnswers.Parse("while True:\n    slash()"));
            Require(panel.IsApplied && first.Program.Source == preserved, "Free practice changed the original robot");
            battle.CodeEditor.Close(); battle.StartBattle();
            Require(sim.Phase == BattlePhase.Running, "Free play cannot start battle");
            Log("PASS free play: own loop, extra warrior, optional hint, close/reopen unfinished draft, original code retained, battle start");
        }

        static void FreePlayFour(DefenseBattle battle)
        {
            var sim = battle.Simulation;
            Require(sim.Robots.Count(r => r.Spec.role == RobotRole.Shooter) == 2, "1-4's new shooter was not placed on its marked cell");
            var learner = sim.Robots.Where(r => r.Spec.role == RobotRole.Shooter).OrderBy(r => r.Id).Last();
            Require(sim.StartError()?.Contains("변수") == true, "1-4 allows battle with an uncoded new shooter");
            battle.SelectRobot(learner.Id); battle.DevelopSelected();
            battle.CodeEditor.Panel.LoadProgram("variable_practice", TutorialAnswers.Parse(
                "while True:\n    target = get_nearest_enemy()\n    attack(target)"));
            Require(battle.CodeEditor.Panel.IsApplied, "1-4 variable code was not applied: " +
                Field<TMP_Text>(battle.CodeEditor.Panel, "status").text);
            battle.CodeEditor.Close();
            Require(sim.StartError() == null, "1-4 remains blocked after the new shooter uses a variable: " + sim.StartError());
            battle.StartBattle();
            Require(sim.Phase == BattlePhase.Running, "1-4 battle did not start");
            Log("PASS 1-4: start blocked until the new shooter attacks a stored enemy");
        }

        static void FreePlayFive(DefenseBattle battle)
        {
            var sim = battle.Simulation;
            var groups = sim.UpcomingWave.ToArray();
            Require(groups.Length == 2 && !groups[0].Boss && groups[1].Boss && groups[0].Count == 3 &&
                groups[0].DefinitionIndex == groups[1].DefinitionIndex &&
                groups[1].Enemy.health >= 80 && groups[1].Enemy.health > groups[0].Enemy.health * 3,
                "1-5 boss must strengthen the existing basic enemy only");
            foreach (var kind in new[] { BlockKind.DeclareVariable, BlockKind.Variable, BlockKind.NearestEnemy, BlockKind.While })
                Require(sim.Available(kind) > 0, "1-5 has no spare " + kind + " block");
            var shooter = sim.Robots.Where(r => r.Spec.role == RobotRole.Shooter).OrderBy(r => r.Id).First();
            var original = shooter.Program.CopyBlocks();
            sim.ApplyProgram(shooter.Id, "boss_shots", TutorialAnswers.Parse("while True:\n    boss = get_nearest_enemy()\n    attack(boss)"));
            Require(shooter.Program.Source.Contains("attack(boss)"), "1-5 could not rewrite a shooter's loop");
            sim.ApplyProgram(shooter.Id, "variable_practice", original);
            battle.StartBattle();
            Require(battle.Simulation.Phase == BattlePhase.Running, "1-5 battle did not start");
        }

        static void FreePlayTwoTwo(DefenseBattle battle)
        {
            var sim = battle.Simulation;
            Require(sim.StartError()?.Contains("2-2") == true, "2-2 allows battle with one conditional shooter");
            Require(sim.Robots.Count(r => r.Spec.role == RobotRole.Shooter) == 2, "2-2's new shooter was not placed on its marked cell");
            var learner = sim.Robots.Where(r => r.Spec.role == RobotRole.Shooter).OrderBy(r => r.Id).Last();
            battle.SelectRobot(learner.Id); battle.DevelopSelected();
            battle.CodeEditor.Panel.LoadProgram("condition_practice", TutorialAnswers.Parse("while True:\n    e = get_nearest_enemy()\n" +
                "    d = e.distance(get_my_pos())\n    if d <= 3:\n        attack(e)"));
            Require(battle.CodeEditor.Panel.IsApplied, "2-2 conditional code was not applied: " +
                Field<TMP_Text>(battle.CodeEditor.Panel, "status").text);
            battle.CodeEditor.Close();
            Require(sim.StartError() == null, "2-2 remains blocked after both shooters use a distance condition: " + sim.StartError());
            battle.StartBattle();
            Require(sim.Phase == BattlePhase.Running, "2-2 battle did not start");
            Log("PASS 2-2: start blocked until two shooters attack inside a distance if");
        }

        static void CaptureBoss(DefenseBattle battle)
        {
            var camera = Field<Camera>(battle, "battleCamera");
            var priorTarget = camera.targetTexture;
            var priorActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(960, 540, 24);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes("TestResults/Editor/boss-entrance.png", image.EncodeToPNG());
            }
            catch (Exception error) { Log("Boss screenshot unavailable: " + error.Message); }
            finally
            {
                camera.targetTexture = priorTarget;
                RenderTexture.active = priorActive;
                if (image) UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
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
            if (battle.Tutorial && battle.Tutorial.EditorStep?.placeSpot == true)
            {
                var marked = battle.TutorialSpot;
                Require(marked.HasValue, "No marked placement cell at " + battle.Tutorial.EditorStep.stepId);
                best = new System.Numerics.Vector2(marked.Value.x, marked.Value.z);
            }
            if (battle.Stage == 1 && sim.WaveIndex == 0 && battle.Tutorial && battle.Tutorial.EditorStep?.stepId == "B02")
            {
                var point = new Vector3(best.X, 0, best.Y);
                var camera = Field<Camera>(battle, "battleCamera");
                var eventSystem = Field<EventSystem>(battle, "eventSystem");
                var hits = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = camera.WorldToScreenPoint(point) }, hits);
                Require(hits.Count == 0, "Robot placement point is blocked by UI: " + string.Join(",", hits.Select(h => h.gameObject.name)));
                Require(battle.Tutorial.AllowsWorldPoint(point), "Robot placement point is outside the tutorial click area");
            }
            Require(battle.PlaceRobot(robot, new Vector3(best.X, 0, best.Y)), "Could not place " + spec.role);
        }
    }
}
