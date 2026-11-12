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
        static bool practiced, practicedFour, practicedFive, bossVisualSeen;
        static int coreAtThirdWave, killsAtThirdWave, leaksAtThirdWave, killsAtFifthWave;
        static BeginnerLessonChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Running, false)) End("INTERRUPTED: left Play mode");
            };
        }

        public static void PreviewCondition()
        {
            SessionState.SetBool(Running, false);
            if (!EditorApplication.isPlaying) TutorialSequenceBuilder.ApplyFirstThree();
            DevTest.JumpTutorial(3, 0);
        }

        public static void PreviewAttackSlot()
        {
            SessionState.SetBool(Running, false);
            SessionState.SetBool(BodyPreview, true);
            bodyReadyAt = 0;
            DevTest.JumpTutorial(3, 0);
        }


        public static void Run()
        {
            TutorialSequenceBuilder.ApplyFirstThree();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lessons integration check\n");
            last = null; practiced = false; coreAtThirdWave = -1; next = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetBool("CodingGame.BeginnerLessons.AllFive", false);
            DevTest.PlayDefense(1, 1, true, false);
        }

        public static void RunConditionOnly()
        {
            TutorialSequenceBuilder.ApplyFirstThree();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lesson 1-3 integration check\n");
            last = null; practiced = false; coreAtThirdWave = -1; next = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetBool("CodingGame.BeginnerLessons.AllFive", false);
            DevTest.PlayDefense(1, 3, true, true);
        }

        [MenuItem("Tools/Develop/Verify Beginner Lessons 1-1 to 1-5")]
        public static void RunAllFive()
        {
            TutorialSequenceBuilder.ApplyAllFive();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lessons 1-1 to 1-5 integration check\n");
            last = null; practiced = practicedFour = practicedFive = bossVisualSeen = false;
            coreAtThirdWave = -1; next = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetBool("CodingGame.BeginnerLessons.AllFive", true);
            DevTest.PlayDefense(1, 1, true, false);
        }

        // Stage 2's lessons (play feedback 4): 2-1 variables, 2-2 free, 2-3 conditions; ends when 2-4 begins.
        [MenuItem("Tools/Develop/Verify Stage 2 Lessons 2-1 to 2-3")]
        public static void RunStageTwo()
        {
            TutorialSequenceBuilder.ApplyAllFive();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START stage 2 lessons 2-1 to 2-3 integration check\n");
            last = null; practiced = practicedFour = practicedFive = bossVisualSeen = false;
            coreAtThirdWave = -1; next = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetBool("CodingGame.BeginnerLessons.AllFive", false);
            DevTest.PlayDefense(2, 1, true, true);
        }

        // 3-1 (13차 179): the tank's introduction and placement on the road; ends when 3-2 begins.
        [MenuItem("Tools/Develop/Verify Stage 3 Lesson 3-1")]
        public static void RunStageThree()
        {
            TutorialSequenceBuilder.ApplyAllFive();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START stage 3 lesson 3-1 integration check\n");
            last = null; practiced = practicedFour = practicedFive = bossVisualSeen = false;
            next = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetBool("CodingGame.BeginnerLessons.AllFive", false);
            DevTest.PlayDefense(3, 1, true, true);
        }

        public static void RunLastTwo()
        {
            TutorialSequenceBuilder.ApplyAllFive();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "START beginner lessons 1-4 and 1-5 integration check\n");
            last = null; practiced = practicedFour = practicedFive = bossVisualSeen = false;
            next = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetBool("CodingGame.BeginnerLessons.AllFive", true);
            DevTest.JumpTutorial(4, 0);
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
                    "while True:\n    enemy = get_nearest_enemy()\n    enemy_distance = enemy.distance(get_my_pos())\n    if enemy_distance <= 3:"));
                director.EditorRun(sequence, Array.FindIndex(sequence.steps, s => s.stepId == "D02"));
            }
            if (!SessionState.GetBool(Running, false)) return;
            try { Advance(); }
            catch (Exception error) { End("FAIL " + error); }
        }
        static void End(string result)
        {
            Log(result); SessionState.SetBool(Running, false); Time.timeScale = 1;
            if (result.StartsWith("FAIL")) Debug.LogError(result); else Debug.Log(result);
            // A batchmode run (a worktree without the open editor) ends with the result.
            if (Application.isBatchMode) EditorApplication.Exit(result.StartsWith("FAIL") ? 1 : 0);
        }

        static void Advance()
        {
            if (!EditorApplication.isPlaying || !DevTest.Ready) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < next) return;
            next = now + .4;
            var battle = DevTest.Battle; var sim = DevTest.Sim; var director = battle.Tutorial;
            if (!director.EditorSynced) return;
            if (sim.Phase == BattlePhase.Running && !Field<TutorialDialogueView>(director, "view").IsVisible)
                foreach (var robot in sim.Robots.ToArray())
                    if (robot.Program != null && !robot.Program.Running &&
                        sim.Enemies.Any(e => e.Active && System.Numerics.Vector2.Distance(e.Position, robot.Position) <= robot.Range))
                        battle.PlayRobot(robot.Id);
            bool allFive = SessionState.GetBool("CodingGame.BeginnerLessons.AllFive", false);
            if (battle.Stage == 2)
            {
                if (sim.WaveIndex >= 3) { End("PASS 2-1 to 2-3 completed: variables lesson, free 2-2, conditions lesson; core=" + sim.BaseHealth); return; }
                if (sim.Phase == BattlePhase.Defeat) { End("FAIL stage 2 wave " + (sim.WaveIndex + 1) + " lost; kills=" + sim.Kills + " leaks=" + sim.Leaks); return; }
                // 2-2 has no lesson to claim its reward, so the checker does.
                if (sim.Phase == BattlePhase.Reward && !director.IsActive)
                {
                    Log("CLEAR wave=" + (sim.WaveIndex + 1) + " core=" + sim.BaseHealth + " kills=" + sim.Kills +
                        " leaks=" + sim.Leaks + " survivors=" + sim.LastSurvivors + " timedOut=" + sim.LastWaveTimedOut);
                    battle.ClaimRewards(); return;
                }
                // Free time (after 2-1's lesson, all of 2-2): a second shooter with 2-1's code, then the battle.
                if (sim.Phase == BattlePhase.Ready && !director.IsActive && (sim.WaveIndex == 1 || director.EditorStep?.stepId == "E01"))
                { StageTwoFree(battle); battle.StartBattle(); Log("START " + (sim.WaveIndex + 1) + " robots=" + sim.Robots.Count); return; }
            }
            if (battle.Stage == 3)
            {
                if (sim.WaveIndex >= 1)
                {
                    Require(sim.Kills >= 4 && sim.BaseHealth >= 6, "3-1's wave was not held; kills=" + sim.Kills + " core=" + sim.BaseHealth);
                    End("PASS 3-1 completed: the tank introduced, stood on the road, the wave held; kills=" + sim.Kills + " core=" + sim.BaseHealth); return;
                }
                if (sim.Phase == BattlePhase.Defeat) { End("FAIL 3-1 lost; kills=" + sim.Kills + " leaks=" + sim.Leaks); return; }
                // The lesson waits for the wave's end (E01): the tank stands on the road, then every robot is placed with a loop.
                if (sim.Phase == BattlePhase.Ready && director.EditorStep?.stepId == "E01")
                {
                    var tank = sim.Robots.SingleOrDefault(r => r.Spec.role == RobotRole.Tank);
                    Require(tank != null, "3-1 did not place the tank");
                    try { DevTest.AutoDeploy(); } catch (FormatException) { } // it places them, then stops at a target-less attack()
                    Loops(sim);
                    battle.StartBattle();
                    Log("START 3-1 robots=" + sim.Robots.Count + " tank at " + tank.Position);
                    return;
                }
            }
            if (!allFive && sim.WaveIndex >= 3) { End("PASS waves 1-1, 1-2, 1-3 completed; reached 1-4 without changing it"); return; }
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
            string key = (battle.Stage > 1 ? battle.Stage + ":" : "") + (sim.WaveIndex + 1) + "/" + step.stepId;
            if (key != last)
            {
                last = key; since = now;
                if (key == "1/A01")
                {
                    var groups = sim.UpcomingWave.ToArray();
                    // Two basic enemies, one more that walks in while pace and pause are explained (a crowd of four until 13차 175),
                    // and one strong enemy in the tank's body (play feedback 4).
                    Require(groups.Length == 3 && groups[0].DefinitionIndex == groups[1].DefinitionIndex &&
                        groups[0].Count == 2 && groups[1].Count == 1 && groups[2].Count == 1 &&
                        groups[2].DefinitionIndex != groups[0].DefinitionIndex && groups[2].Enemy.type == EnemyType.Tank &&
                        groups[2].Enemy.health > groups[0].Enemy.health * 10,
                        "1-1 must send two basic enemies, then one more, and one tank-bodied strong enemy");
                }
                if (key == "3/A01") { coreAtThirdWave = sim.BaseHealth; killsAtThirdWave = sim.Kills; leaksAtThirdWave = sim.Leaks; }
                if (key == "5/A01") killsAtFifthWave = sim.Kills;
                Log("STEP " + key + " " + step.completionSignal + " " + step.trigger);
                return; // Wait for layout and target registration.
            }
            Require(now - since < 85, "Stalled at " + key + " phase=" + sim.Phase + " time=" + sim.Time.ToString("0.0") +
                " enemies=" + string.Join(",", sim.Enemies.Select(e => (e.Boss ? "boss" : "minion") + ":" + e.Health.ToString("0.0"))) +
                " runs=" + string.Join(",", sim.Robots.Select(r => r.Program?.CompletedRuns.ToString() ?? "none")));
            if (key == "1/B05")
            {
                var bridge = Field<TutorialInputBridge>(director, "bridge");
                // The 3D range ring, tinted inside, is the highlight: no screen hole or outline that would not line up with it.
                Require(Field<System.Collections.Generic.List<Rect>>(bridge, "roundAreas").Count == 0 &&
                    Field<System.Collections.Generic.List<Rect>>(bridge, "outlinedAreas").Count == 0 && director.Highlighted(TutorialKeys.Range(0)),
                    "1-1 B05 must highlight the range ring itself, without a spotlight hole or outline over it");
            }
            if (battle.Stage == 1 && sim.WaveIndex == 1 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practiced)
            {
                FreePlay(battle); practiced = true; return;
            }
            if (allFive && sim.WaveIndex == 3 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practicedFour)
            { FreePlayFour(battle); practicedFour = true; return; }
            if (allFive && sim.WaveIndex == 4 && !director.IsActive && sim.Phase == BattlePhase.Ready && !practicedFive)
            { FreePlayFive(battle); practicedFive = true; return; }
            // 1-3 leaves the battle start to the player after the loop lesson.
            if (battle.Stage == 1 && sim.WaveIndex == 2 && !director.IsActive && sim.Phase == BattlePhase.Ready) { battle.StartBattle(); return; }
            if (!director.IsActive) { Time.timeScale = 3; return; }
            var view = Field<TutorialDialogueView>(director, "view");
            if (!view.IsVisible) return; // System banner.
            if (key == "1/C05" || key == "1/C08")
                Require(!battle.TutorialBlocksPreview && Field<DefenseAttackPreview>(battle, "attackPreview").IsShowing,
                    "1-1 preview stopped while placing blocks at " + key);
            if (!string.IsNullOrWhiteSpace(step.dragDemo))
                Require(Field<TutorialPointer>(director, "pointer").IsDragVisible,
                    "Block placement drag preview is missing at " + key);
            if (key == "1/E04" || key == "1/E04a")
                Require(sim.Phase == BattlePhase.Running && Math.Abs(director.BattlePace - .2f) < .001f,
                    "1-1 pause introduction must show a moving slow-motion battle at " + key);
            if (key == "1/E04" || key == "1/E04a" || key == "1/E04b")
            {
                var speed = Field<Button>(Field<DefensePlayerHUD>(battle, "playerHUD"), "speedButton");
                Require(!battle.SpeedUnlocked && !speed.interactable && battle.BattleSpeed == 1,
                    "Speed is available before its 1-1 introduction at " + key);
                battle.ToggleSpeed();
                Require(battle.BattleSpeed == 1, "Speed changed while locked at " + key);
            }
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
                // 1-1: the Play presses fell the three basic enemies (two swings each); the tank-bodied one still gets through.
                if (battle.Stage == 1 && sim.WaveIndex == 0) Require(sim.Kills >= 3 && sim.Leaks >= 1 && sim.BaseHealth >= 8,
                    "1-1's Play lesson did not hold the road; core=" + sim.BaseHealth + " kills=" + sim.Kills + " leaks=" + sim.Leaks);
                // 1-3: two warriors' ten-swing loops (and Play between calls) hold eight enemies.
                if (battle.Stage == 1 && sim.WaveIndex == 2) Require(sim.Leaks - leaksAtThirdWave <= 1 && sim.Kills - killsAtThirdWave >= 7,
                    "1-3's loops did not hold the road; leaks=" + (sim.Leaks - leaksAtThirdWave) + " kills=" + (sim.Kills - killsAtThirdWave));
                battle.ClaimRewards(); return;
            }
            if (step.targetKeys.Contains(TutorialKeys.Play(0)))
            {
                var teacher = TutorialWatcher.TeachingRobot(sim);
                Require(battle.TutorialAllows(TutorialKeys.Play(0)) && director.EditorBridge.TryGetArea(TutorialKeys.Play(0), out _),
                    "The robot's Play button is not offered at " + key);
                battle.PlayRobot(teacher.Id);
                Require(teacher.Program.Running, "Play did not call the robot's function at " + key);
                return;
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
                speed.onClick.Invoke();
                Require(battle.BattleSpeed == 3, "Speed cannot reach 3x after the tutorial introduction");
                speed.onClick.Invoke();
                if (GameFlow.Developer) { Require(battle.BattleSpeed == 5, "Developer rights do not add 5x after 3x"); speed.onClick.Invoke(); }
                Require(battle.BattleSpeed == 1, "Speed cannot return to 1x after 3x");
                return;
            }
            var robotButton = step.targetKeys.FirstOrDefault(k => k.StartsWith(TutorialKeys.RobotButtonPrefix, StringComparison.Ordinal));
            if (robotButton != null) { battle.ChooseRobot(int.Parse(robotButton.Substring(TutorialKeys.RobotButtonPrefix.Length))); return; }
            if (step.completionSignal == TutorialSignals.RobotPlaced)
            {
                if (key == "1/B02")
                {
                    var button = Field<Button[]>(Field<DefensePlayerHUD>(battle, "playerHUD"), "robotButtons")[1];
                    battle.BeginRobotDrag(1);
                    battle.EndRobotDrag(RectTransformUtility.WorldToScreenPoint(null, button.transform.position));
                    Require(Field<int>(battle, "palette") == 1 && sim.Robots.Count == 0,
                        "Releasing a slight drag over the robot button cleared the selected robot");
                }
                if (key == "1/B02")
                {
                    // 1-1 takes only the lit cell: a click beside it is turned away, one inside it stands on the spot.
                    Require(!battle.PlaceRobot(Field<int>(battle, "palette"), DefenseBattle.FirstWarriorPosition + new Vector3(3, 0, 0)) && sim.Robots.Count == 0,
                        "1-1 placed the warrior off the lit cell");
                    Require(battle.PlaceRobot(Field<int>(battle, "palette"), DefenseBattle.FirstWarriorPosition + new Vector3(.3f, 0, -.3f)) &&
                        System.Numerics.Vector2.Distance(sim.Robots[0].Position, new System.Numerics.Vector2(DefenseBattle.FirstWarriorPosition.x, DefenseBattle.FirstWarriorPosition.z)) < .01f,
                        "1-1 did not stand the warrior on its spot");
                    return;
                }
                PlaceBest(battle, Field<int>(battle, "palette")); return;
            }
            if (step.targetKeys.Contains(TutorialKeys.Unit(0))) { battle.SelectRobot(TutorialWatcher.TeachingRobot(sim).Id); return; }
            if (step.completionSignal == TutorialSignals.EditorOpened) { battle.DevelopSelected(); return; }
            if (step.completionSignal == TutorialSignals.BattleStarted) { battle.StartBattle(); return; }
            if (step.completionSignal == TutorialSignals.PreviewHit ||
                step.completionSignal == TutorialSignals.PreviewSingleHit || step.completionSignal == TutorialSignals.PreviewMultiHit)
            { Require(Field<DefenseAttackPreview>(battle, "attackPreview").IsShowing, "Preview is closed at " + key); return; }
            var tab = step.targetKeys.FirstOrDefault(k => k.StartsWith(TutorialKeys.PaletteTabPrefix, StringComparison.Ordinal));
            if (tab != null)
            {
                Require(director.EditorBridge.TryGetArea(tab, out _), "Palette tab " + tab + " is not offered at " + key);
                panel.CategoryTab(int.Parse(tab.Substring(TutorialKeys.PaletteTabPrefix.Length))).GetComponent<Button>().onClick.Invoke();
                return;
            }
            Require(editor.IsOpen && battle.TutorialAllows(TutorialKeys.Palette), "Editor action blocked at " + key);
            if (step.completionSignal == TutorialSignals.SlotSelected) { Select(panel, Slot(editor, step.expectedCode)); return; }
            if (step.completionSignal == TutorialSignals.CodeLinePlaced)
            { Add(panel, (BlockKind)Enum.Parse(typeof(BlockKind), step.expectedCode.Split(':')[1])); return; }
            if (step.completionSignal == TutorialSignals.DraftValueMatches)
            {
                var pair = step.expectedCode.Split('|'); var path = pair[0].Split('.');
                var block = editor.Line(path[0]); // "1/3" is the third line inside line 1
                foreach (int arg in path.Skip(1).Select(int.Parse)) block = block.Arguments[arg].Blocks[0];
                if (block.BaseKind == BlockKind.Comparison) block.SelectOption(Array.IndexOf(PythonTreeCompiler.Comparisons, pair[1]));
                else block.SetText(pair[1]);
                return;
            }
            if (step.completionSignal == TutorialSignals.SlotFilled || step.completionSignal == TutorialSignals.ConditionBuilt)
            { Add(panel, (BlockKind)Enum.Parse(typeof(BlockKind), step.pointerKey.Substring(TutorialKeys.PalettePrefix.Length))); return; }
            if (step.completionSignal == TutorialSignals.ProgramMatches)
            {
                // A step that points at a palette block finishes by adding it (1-1 C08 and 1-3 D02: slash()); otherwise
                // the last variable still needs its name.
                if (step.pointerKey != null && step.pointerKey.StartsWith(TutorialKeys.PalettePrefix))
                { Add(panel, (BlockKind)Enum.Parse(typeof(BlockKind), step.pointerKey.Substring(TutorialKeys.PalettePrefix.Length))); return; }
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
            Require(goal.transform.parent.gameObject.activeSelf && goal.text.Contains("새 전사를 배치하고 '실행'으로 베기"),
                "1-2 free-play goal is not visible at the top of the HUD");
            Require(sim.StartError() == null, "1-2 free play must start with only the original robot: " + sim.StartError());
            string preserved = first.Program.Source;
            Require(battle.TutorialAllows(TutorialKeys.Start) && battle.TutorialAllows(TutorialKeys.Close), "Free play still restricts input");
            battle.ChooseRobot(1); PlaceBest(battle, 1);
            var second = sim.Robots.OrderBy(r => r.Id).Last();
            Require(second.Id != first.Id, "Practice warrior was not granted");
            Require(sim.StartError() == null, "1-2 free play must start with an uncoded new robot: " + sim.StartError());
            battle.SelectRobot(second.Id); battle.DevelopSelected();
            var panel = battle.CodeEditor.Panel;
            panel.LoadProgram("practice", TutorialAnswers.Parse("slash()\nslash()"));
            Require(panel.IsApplied, "Two swings a call did not compile");
            var hint = Field<Button>(battle.CodeEditor, "hintButton");
            Require(hint.gameObject.activeSelf, "Optional hint unavailable"); hint.onClick.Invoke();
            Require(battle.CodeEditor.HintOpen, "Hint did not open"); hint.onClick.Invoke();
            panel.LoadProgram("practice", new[] { new CodeBlock(BlockKind.Shot) }); // no target
            Require(!panel.IsApplied, "Incomplete draft unexpectedly applied");
            battle.CodeEditor.Close(); Require(!battle.CodeEditor.IsOpen, "Incomplete draft cannot close in free play");
            battle.DevelopSelected();
            Require(!panel.IsApplied, "Incomplete draft not preserved on reopen");
            panel.LoadProgram("practice", TutorialAnswers.Parse("slash()\nslash()"));
            Require(panel.IsApplied && first.Program.Source == preserved, "Free practice changed the original robot");
            battle.CodeEditor.Close(); battle.StartBattle();
            Require(sim.Phase == BattlePhase.Running, "Free play cannot start battle");
            Log("PASS free play: two swings a call, extra warrior, optional hint, close/reopen unfinished draft, original code retained, battle start");
        }

        static void StageTwoFree(DefenseBattle battle)
        {
            var sim = battle.Simulation;
            int shooter = Enumerable.Range(0, sim.Setup.ActionProfiles.Length).First(i => battle.DefinitionRole(i) == RobotRole.Shooter);
            // Two shooters: 2-1 gives the variable blocks for two copies of its code.
            if (sim.RobotAvailable(RobotRole.Shooter) > 0 && sim.Robots.Count(r => r.Spec.role == RobotRole.Shooter) < 2) { battle.ChooseRobot(shooter); PlaceBest(battle, shooter); }
            foreach (var bot in sim.Robots.Where(r => r.Spec.role == RobotRole.Shooter && (r.Program == null || r.Program.IsEmpty)).ToArray())
                sim.ApplyProgram(bot.Id, "free_" + bot.Id, TutorialAnswers.Parse("while True:\n    enemy = get_nearest_enemy()\n    attack(enemy)"));
        }

        static void FreePlayFour(DefenseBattle battle)
        {
            var sim = battle.Simulation;
            // 1-4 brings a third warrior (the shooter waits for 2-1 since 13차 179).
            Require(sim.RobotAvailable(RobotRole.Warrior) >= 1 && sim.RobotAvailable(RobotRole.Shooter) == 0, "1-4 must bring a new warrior and no shooter");
            Require(sim.StartError() == null, "1-4 free play must start without new code: " + sim.StartError());
            int warrior = Enumerable.Range(0, sim.Setup.ActionProfiles.Length).First(i => battle.DefinitionRole(i) == RobotRole.Warrior);
            battle.ChooseRobot(warrior); PlaceBest(battle, warrior);
            var learner = sim.Robots.OrderBy(r => r.Id).Last();
            Require(learner.Spec.role == RobotRole.Warrior, "1-4's new robot is not a warrior");
            battle.SelectRobot(learner.Id); battle.DevelopSelected();
            battle.CodeEditor.Panel.LoadProgram("loop_practice", TutorialAnswers.Parse("while True:\n    slash()"));
            Require(battle.CodeEditor.Panel.IsApplied, "1-4 warrior code was not applied: " +
                Field<TMP_Text>(battle.CodeEditor.Panel, "status").text);
            battle.CodeEditor.Close();
            battle.StartBattle();
            Require(sim.Phase == BattlePhase.Running, "1-4 battle did not start");
            Log("PASS 1-4: free play starts without new code, the new warrior loops slash()");
        }

        // Every robot without code gets its own action in a loop, on a found enemy where the action needs one (block, attack,
        // slow, boom); the stock is not counted (test only, as the screen tour does).
        public static void Loops(DefenseSimulation sim)
        {
            var setup = sim.Setup; var options = setup.TestOptions;
            setup.TestOptions = options.Copy(); setup.TestOptions.inventoryLimit = false;
            try
            {
                foreach (var bot in sim.Robots.Where(r => r.Program == null || r.Program.IsEmpty).ToArray())
                {
                    var kind = DefenseProgression.ActionBlock(bot.Spec.NativeAction);
                    var loop = new CodeBlock(BlockKind.While);
                    if (PythonTreeCompiler.HasEnemyArgument(kind))
                    {
                        loop.Body.Add(new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)));
                        loop.Body.Add(new CodeBlock(kind, "0", new CodeBlock(BlockKind.Variable, "enemy")));
                    }
                    else loop.Body.Add(new CodeBlock(kind));
                    sim.ApplyProgram(bot.Id, "loop_" + bot.Id, new[] { loop });
                }
            }
            finally { setup.TestOptions = options; }
        }

        static void FreePlayFive(DefenseBattle battle)
        {
            var sim = battle.Simulation;
            var groups = sim.UpcomingWave.ToArray();
            Require(groups.Length == 2 && !groups[0].Boss && groups[1].Boss && groups[0].Count == 3 &&
                groups[0].DefinitionIndex == groups[1].DefinitionIndex &&
                groups[1].Enemy.health >= 80 && groups[1].Enemy.health > groups[0].Enemy.health * 3,
                "1-5 boss must strengthen the existing basic enemy only");
            foreach (var kind in new[] { BlockKind.While, BlockKind.Slash })
                Require(sim.Available(kind) > 0, "1-5 has no spare " + kind + " block");
            Require(sim.Available(BlockKind.DeclareVariable) == 0 && sim.Available(BlockKind.If) == 0, "Variables and conditions open in Stage 2, not 1-5");
            battle.StartBattle();
            Require(battle.Simulation.Phase == BattlePhase.Running, "1-5 battle did not start");
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
            // Points every half unit along the roads: the corners alone are just the two ends on Stage 1's straight road.
            var samples = sim.Setup.Routes.SelectMany(r => r.Points.Zip(r.Points.Skip(1), (a, b) => (a, b)))
                .SelectMany(s => Enumerable.Range(0, Mathf.CeilToInt(System.Numerics.Vector2.Distance(s.a, s.b) * 2) + 1)
                    .Select(i => System.Numerics.Vector2.Lerp(s.a, s.b, Mathf.Clamp01(i / Mathf.Max(1, System.Numerics.Vector2.Distance(s.a, s.b) * 2)))))
                .ToArray();
            var candidates = new System.Collections.Generic.List<System.Numerics.Vector2>();
            for (float x = sim.Setup.Min.X + .5f; x < sim.Setup.Max.X; x += 1)
                for (float z = sim.Setup.Min.Y + .5f; z < sim.Setup.Max.Y; z += 1)
                {
                    var p = new System.Numerics.Vector2(x, z);
                    if (sim.PlacementError(spec, p) == null) candidates.Add(p);
                }
            var best = candidates.OrderByDescending(p => samples.Count(q => System.Numerics.Vector2.Distance(p, q) <= spec.range)).First();
            if (sim.WaveIndex == 0 && battle.Tutorial && battle.Tutorial.EditorStep?.stepId == "B02")
            {
                var point = new Vector3(best.X, 0, best.Y);
                var camera = Field<Camera>(battle, "battleCamera");
                var eventSystem = Field<EventSystem>(battle, "eventSystem");
                var hits = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = camera.WorldToScreenPoint(point) }, hits);
                Require(hits.Count == 0, "Robot placement point is blocked by UI: " + string.Join(",", hits.Select(h => h.gameObject.name)));
                Require(battle.Tutorial.AllowsWorldPoint(point), "Robot placement point is outside the tutorial click area");
            }
            Require(battle.PlaceRobot(robot, new Vector3(best.X, 0, best.Y)), "Could not place warrior");
        }
    }
}
