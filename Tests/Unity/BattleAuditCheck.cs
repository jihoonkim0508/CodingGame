// Battle audit, bugs A1-A4 and A6: a won stage's record, real clears after the developer key, the code preview against the
// battle, the boss's entrance behind the Esc menu, the floor the deployment bounds cut off. (A3d and A5 change nothing a
// check can see.) Each check fails for its bug and passes once the bug is fixed.
// Run in Play mode in the Defense scene (DevTest.PlayDefense(2, 1), wait for DevTest.Ready), one run at a time (about 15 s):
//   run_script entry BattleAuditCheck.Begin   starts the run and returns at once
//   run_script entry BattleAuditCheck.Main    "RUNNING: <check>" until the run is over, then the result
// The result (also Temp/BattleAudit.txt): one line per failing check, then "AUDIT battle: <passed>/<total> passed".
// It starts its own battle sessions (2-1's lesson among them), opens the code editor and the Esc menu (3.5 s), puts
// StageProgress back as it found it and ends in a Stage 2 test session.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.StageMap;
using UnityEngine;
using Object = UnityEngine.Object;
using Point = System.Numerics.Vector2;

public static class BattleAuditCheck
{
    const string ResultPath = "Temp/BattleAudit.txt";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    // No lesson, every robot and block to hand.
    static DefenseTestOptions Sandbox => new DefenseTestOptions { tutorial = false, tutorialInput = false, inventoryLimit = false, unlockAll = true };

    public static string Begin()
    {
        var battle = Application.isPlaying ? Object.FindFirstObjectByType<DefenseBattle>() : null;
        if (!battle || battle.Simulation == null) return "Not started: enter Play mode in the Defense scene first (DevTest.PlayDefense(2, 1)).";
        File.WriteAllText(ResultPath, "RUNNING: starting");
        _ = Run(battle);
        return "Started. Ask entry BattleAuditCheck.Main for the result.";
    }
    public static string Main() => File.Exists(ResultPath) ? File.ReadAllText(ResultPath) : "Not started: run entry BattleAuditCheck.Begin in Play mode, in the Defense scene.";

    static async Task Run(DefenseBattle battle)
    {
        var checks = new (string name, Func<DefenseBattle, Task> run)[]
        {
            ("A1 a won stage is recorded when it is won", WonStageIsRecorded),
            ("A2 real clears and stars are kept after the developer key Z", RealClearAfterDeveloperKey),
            ("A3a the preview calls the code again whenever it ends", PreviewCallsCodeOnce),
            ("A3b new code starts the demo robot clean", NewCodeStartsDemoClean),
            ("A3c a preview held by a lesson keeps its actors in place", HeldPreviewKeepsPlaces),
            ("A4a the boss's entrance waits behind the Esc menu", BossEntranceWaitsForMenu),
            ("A4b every stage's boss gets the entrance", EveryBossGetsEntrance),
            ("A6 the deployment bounds cover the map's floor", BoundsCoverFloor),
            ("J1 the F1 jump to 1-1.E07 opens on the won wave's reward, and its claim goes on to 1-2", JumpToRewardLine),
            ("J2 the F1 jump to 2-3.D07 has the robots and code the lesson made", JumpMidLesson),
            ("J3 a jump's replay stands where the cached play stood", ReplayMatchesPlay),
        };
        var failures = new List<string>();
        bool background = Application.runInBackground;
        Application.runInBackground = true; // frames keep coming while the editor is not in front
        try
        {
            foreach (var (name, run) in checks)
            {
                File.WriteAllText(ResultPath, "RUNNING: " + name);
                // A check that throws has failed, and the others still run.
                try { await run(battle); }
                catch (Exception error)
                {
                    var cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                    failures.Add($"FAIL {name}: " + (cause is Failed ? cause.Message :
                        $"did not finish: {cause.GetType().Name}: {cause.Message} {cause.StackTrace?.Split('\n')[0].Trim()}").Replace('\n', ' '));
                }
            }
        }
        finally
        {
            Application.runInBackground = background;
            File.WriteAllText(ResultPath, string.Join("\n", failures.Append($"AUDIT battle: {checks.Length - failures.Count}/{checks.Length} passed")));
        }
    }

    sealed class Failed : Exception { public Failed(string message) : base(message) { } }
    // "setup: ..." failures mean the check could not reach the point it tests.
    static void Require(bool ok, string failure) { if (!ok) throw new Failed(failure); }
    // Frames and real time, not game time: the Esc menu stops the game's clock.
    static async Task Until(Func<bool> met, float seconds, string failure)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!met()) { Require(Time.realtimeSinceStartup < end, failure); await Task.Delay(15); }
    }
    static Task Frames(int count) { int at = Time.frameCount + count; return Until(() => Time.frameCount >= at, 10, "setup: no frames ran (is Play mode paused?)"); }
    static async Task Seconds(float seconds) { int frame = Time.frameCount; await Task.Delay((int)(seconds * 1000)); await Frames(frame + 2 - Time.frameCount); }
    static T Field<T>(object owner, string name) => Field<T>(owner.GetType(), owner, name);
    static T Field<T>(Type type, object owner, string name)
    {
        var field = type.GetField(name, Any);
        Require(field != null, $"setup: {type.Name}.{name} is gone");
        return (T)field.GetValue(owner);
    }
    // The first spot the simulation takes this robot at: a grid over the map, then points along the roads (a tank's place).
    static RobotState Place(DefenseBattle battle, RobotRole role)
    {
        var sim = battle.Simulation;
        int index = Enumerable.Range(0, battle.RobotDefinitions.Count).First(i => battle.DefinitionRole(i) == role);
        var spec = battle.RobotDefinition(index).stats;
        var spots = new List<Point>();
        for (float x = sim.Setup.Min.X + .5f; x < sim.Setup.Max.X; x++)
            for (float y = sim.Setup.Min.Y + .5f; y < sim.Setup.Max.Y; y++) spots.Add(new Point(x, y));
        foreach (var road in sim.Setup.Routes)
            for (int i = 1; i < road.Points.Length; i++)
                for (int k = 0; k <= 20; k++) spots.Add(Point.Lerp(road.Points[i - 1], road.Points[i], k / 20f));
        foreach (var spot in spots)
            if (sim.PlacementError(spec, spot) == null && battle.PlaceRobot(index, new Vector3(spot.X, 0, spot.Y))) return sim.Robots[sim.Robots.Count - 1];
        throw new Failed($"setup: no place for a {role} ({sim.PlacementError(spec, spots[0])})");
    }

    // StageProgress emptied for a check and put back as it was (it has no save file: these statics are all of it).
    sealed class BorrowedProgress : IDisposable
    {
        readonly HashSet<int> cleared = Field<HashSet<int>>(typeof(StageProgress), null, "cleared"), previews = Field<HashSet<int>>(typeof(StageProgress), null, "previewClears");
        readonly Dictionary<int, int> stars = Field<Dictionary<int, int>>(typeof(StageProgress), null, "stars");
        readonly int[] wasCleared, wasPreviews;
        readonly KeyValuePair<int, int>[] wasStars;
        readonly int selected = StageProgress.SelectedStage, unlock = StageProgress.PendingUnlock, purification = StageProgress.PendingPurification;
        public BorrowedProgress()
        {
            wasCleared = cleared.ToArray(); wasPreviews = previews.ToArray(); wasStars = stars.ToArray();
            StageProgress.ResetAll(0);
        }
        // A real clear, not only one shown by a preview clear.
        public bool Cleared(int stage) => cleared.Contains(stage);
        public void Dispose()
        {
            StageProgress.ResetAll(0);
            cleared.UnionWith(wasCleared); previews.UnionWith(wasPreviews);
            foreach (var pair in wasStars) stars[pair.Key] = pair.Value;
            StageProgress.SelectedStage = selected;
            typeof(StageProgress).GetProperty("PendingUnlock").SetValue(null, unlock);
            typeof(StageProgress).GetProperty("PendingPurification").SetValue(null, purification);
        }
    }

    // A1. A stage entered from the stage map counts as cleared, with its stars, once it is won: the Esc menu's 스테이지 선택 and
    // 타이틀로 leave the result screen without the result button. (Only that button's ContinueCampaign recorded them.)
    static async Task WonStageIsRecorded(DefenseBattle battle)
    {
        const int stage = 2;
        // Wins the stage: no enemies and a core that takes no damage, so the last wave ends on its timer with the core whole.
        async Task Win()
        {
            StageProgress.SelectedStage = stage;
            var options = Sandbox; options.enemySpawn = false; options.baseDamage = false;
            battle.EditorStartSession(stage, battle.EditorCampaign.stages[stage - 1].waves.Length, true, options);
            var sim = battle.Simulation;
            Place(battle, RobotRole.Warrior); battle.StartBattle();
            for (int i = 0; i < 600 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1);
            Require(sim.Phase == BattlePhase.Reward, "setup: the last wave did not end in its reward (" + sim.Phase + ")");
            await Frames(2); // the reward screen is up, as when the player clicks
            battle.ContinueCampaign(); // the result button claims the last reward: the stage is won
            Require(sim.Phase == BattlePhase.Victory && sim.StageStars == 3 && battle.FromStageMap, "setup: no three-star victory screen (" + sim.Phase + ")");
            await Frames(3);
        }
        using (new BorrowedProgress())
        {
            await Win();
            Require(StageProgress.GetState(stage) == StageState.Cleared && StageProgress.BestStars(stage) == 3,
                $"stage {stage} is won with 3 stars, but StageProgress holds {StageProgress.GetState(stage)} and {StageProgress.BestStars(stage)} stars until the result button is pressed again");
            // Won again over an earlier one-star clear: the result screen still calls it a new record.
            StageProgress.ResetAll(0); StageProgress.RecordStars(stage, 1); StageProgress.MarkCleared(stage);
            await Win();
            string card = Field<TMPro.TMP_Text>(Object.FindFirstObjectByType<DefensePlayerHUD>(), "rewardItems").text;
            Require(StageProgress.BestStars(stage) == 3 && card.Contains("새 기록"), $"three stars over an earlier one-star clear: {StageProgress.BestStars(stage)} stars kept, result card says '{card.Split('\n')[0]}'");
        }
    }

    // A2. The stage map's developer key Z (a preview clear) is a display-only overlay: clears and stars really earned after it
    // are still kept. (After one Z every later clear went to the preview list and its stars were dropped.)
    static Task RealClearAfterDeveloperKey(DefenseBattle battle)
    {
        using (var progress = new BorrowedProgress())
        {
            StageProgress.MarkClearedForPreview(1);                        // Z on stage 1
            StageProgress.RecordStars(2, 3); StageProgress.MarkCleared(2); // then stage 2 really won
            Require(StageProgress.GetState(1) == StageState.Cleared && StageProgress.BestStars(1) == 0 && !progress.Cleared(1),
                "a preview clear is no longer display-only");
            Require(StageProgress.BestStars(2) == 3 && progress.Cleared(2),
                $"after Z on stage 1, stage 2 won with 3 stars keeps {StageProgress.BestStars(2)} stars and " + (progress.Cleared(2) ? "its clear" : "only a preview clear"));
        }
        return Task.CompletedTask;
    }

    // A robot of this role with this code, its code editor open and the preview's demo started over.
    static DefenseAttackPreview OpenPreview(DefenseBattle battle, RobotRole role, params CodeBlock[] code)
    {
        battle.EditorStartSession(2, 1, true, Sandbox);
        var bot = Place(battle, role);
        battle.Simulation.ApplyProgram(bot.Id, "robot_" + bot.Id, code);
        battle.SelectRobot(bot.Id); battle.DevelopSelected();
        var preview = Object.FindFirstObjectByType<DefenseAttackPreview>();
        Require(battle.CodeEditor.IsOpen && preview.IsShowing, "setup: the code editor's preview did not open");
        preview.Restart();
        return preview;
    }
    static readonly MethodInfo AdvancePreview = typeof(DefenseAttackPreview).GetMethod("AdvancePreview", Any);
    // The preview's own step, one tick.
    static void Step(DefenseAttackPreview preview)
    {
        Require(AdvancePreview != null, "setup: DefenseAttackPreview.AdvancePreview is gone");
        AdvancePreview.Invoke(preview, new object[] { 1f / 60 });
    }
    // Plays this demo on to `seconds` on its clock, or until `until` holds or the preview replaces it.
    static void Play(DefenseAttackPreview preview, DefenseSimulation demo, double seconds, Func<bool> until = null)
    {
        for (int i = 0; i < seconds * 60 + 60 && preview.PreviewSimulation == demo && demo.Time < seconds && (until == null || !until()); i++) Step(preview);
    }

    // A3a. The preview calls the function again whenever it ends (the user's choice, 2026-11-19), so slash() with no loop keeps
    // swinging instead of standing still; when the enemies are past, the demo starts over. A hit still tells the tutorial.
    static async Task PreviewCallsCodeOnce(DefenseBattle battle)
    {
        var preview = OpenPreview(battle, RobotRole.Warrior, new CodeBlock(BlockKind.Slash));
        var signals = Field<HashSet<string>>(battle.Tutorial, "signals"); signals.Remove(TutorialSignals.PreviewHit);
        var demo = preview.PreviewSimulation; var bot = demo.Robots[0];
        Play(preview, demo, 4);
        Require(bot.Program.CompletedRuns > 1 && bot.Executions > 1,
            $"slash() with no loop ran {bot.Program.CompletedRuns} times ({bot.Executions} swings) in 4 s of one demo; the preview should call it again");
        await Frames(3); // the preview's Update reads the demo's events
        Require(signals.Contains(TutorialSignals.PreviewHit), "the swing hit an enemy but the tutorial got no PreviewHit");
        Play(preview, demo, 20);
        Require(preview.PreviewSimulation != demo, "the demo did not start over once the enemies were past");
    }

    // A3b. New code starts the demo robot clean, as applying code does in the battle: a tank that was blocking under
    // block(enemy) stops when its code is emptied. (The program was swapped in place and the old code's state stayed.)
    static Task NewCodeStartsDemoClean(DefenseBattle battle)
    {
        var preview = OpenPreview(battle, RobotRole.Tank, new CodeBlock(BlockKind.Block, "0", new CodeBlock(BlockKind.NearestEnemy)));
        var demo = preview.PreviewSimulation; var tank = demo.Robots[0];
        Play(preview, demo, 6, () => tank.BlockingEnabled && demo.Enemies.Any(e => e.BlockedBy == tank.Id));
        Require(preview.PreviewSimulation == demo && tank.BlockingEnabled && demo.Enemies.Any(e => e.BlockedBy == tank.Id), "setup: the demo tank never held an enemy");
        battle.Simulation.ApplyProgram(battle.SelectedId, "robot_" + battle.SelectedId, Array.Empty<CodeBlock>());
        Step(preview);
        var now = preview.PreviewSimulation; var bot = now.Robots[0];
        int held = now.Enemies.Count(e => e.BlockedBy == bot.Id);
        Require((bot.Program == null || bot.Program.IsEmpty) && !bot.BlockingEnabled && held == 0,
            $"with its code emptied the demo tank still blocks ({held} enemies held): the old code's state outlives the change");
        return Task.CompletedTask;
    }

    // A3c. While a lesson holds the preview (the block-placing steps of 2-1 and 2-3) the demo may stand still, but its robot
    // and enemies are drawn where they stand. (Update returned before placing them: a new view starts at the arena's origin,
    // the middle of the road, and stayed there. The check moves the views there itself, as a reused view starts where it was.)
    static async Task HeldPreviewKeepsPlaces(DefenseBattle battle)
    {
        battle.EditorStartSession(2, 1, true, new DefenseTestOptions()); // 2-1 with its lesson
        var director = battle.Tutorial;
        // The director takes the new session on its next Update and begins the lesson; stopped before that, it begins it over ours.
        await Until(() => director.EditorSynced && director.EditorStep != null, 10, "setup: lesson 2-1 did not begin");
        var lesson = director.Sequences.FirstOrDefault(s => s && s.sequenceId == "2-1");
        int step = lesson ? Array.FindIndex(lesson.steps, s => s.kind == TutorialStepKind.ActionGuide && !s.allowPreview && s.targetKeys.Contains(TutorialKeys.DropZone)) : -1;
        Require(step >= 0, "setup: lesson 2-1 has no block-placing step that holds the preview");
        try
        {
            director.EditorStop();
            battle.PlaceLessonShooter(); // as the lesson's step A03 does (nothing if it already has)
            battle.SelectRobot(battle.Simulation.Robots.First(r => r.Spec.role == RobotRole.Shooter).Id); battle.DevelopSelected();
            var preview = Object.FindFirstObjectByType<DefenseAttackPreview>();
            Require(battle.CodeEditor.IsOpen && preview.IsShowing, "setup: the code editor's preview did not open");
            preview.Restart(); Play(preview, preview.PreviewSimulation, 1.5); // an enemy on the road
            await Frames(2);
            director.EditorRun(lesson, step);
            Require(battle.TutorialBlocksPreview, $"setup: step {lesson.steps[step].stepId} does not hold the preview");
            var arena = Field<Transform>(preview, "arena"); var views = Field<Dictionary<int, DefenseActorView>>(preview, "views");
            foreach (var view in views.Values) view.transform.position = arena.position;
            await Frames(3);
            var demo = preview.PreviewSimulation;
            Require(battle.TutorialBlocksPreview && demo.Enemies.Count > 0, "setup: the lesson let go of the preview, or its demo has no enemy");
            float Off(int id, Point at) => views.TryGetValue(id, out var drawn) ? Vector3.Distance(drawn.transform.position, arena.position + new Vector3(at.X, 0, at.Y)) : 99;
            float worst = demo.Robots.Select(r => Off(r.Id, r.Position)).Concat(demo.Enemies.Select(e => Off(e.Id, e.Position))).Max();
            Require(worst < .05f, $"lesson 2-1 step {lesson.steps[step].stepId} holds the preview and its robot or enemy stands {worst:0.0} from its place (left at the arena's origin, on the road)");
        }
        finally { director.EditorStop(); battle.CodeEditor.Close(false); }
    }

    // Runs the wave to the boss's arrival: it comes once every ordinary enemy is out and gone, so each is taken off as it appears.
    static void BringBoss(DefenseSimulation sim)
    {
        for (int tick = 0; tick < 3600 && sim.Phase == BattlePhase.Running && !sim.Enemies.Any(e => e.Boss); tick++)
        {
            sim.Advance(DefenseSimulation.TickDuration);
            foreach (var enemy in sim.Enemies) if (!enemy.Boss) enemy.Active = false;
        }
        Require(sim.Enemies.Any(e => e.Boss), "setup: the boss never came (" + sim.Phase + ")");
    }

    // A4a. The boss's entrance (the camera on the boss, the battle held 2.9 s) waits while the Esc menu has the game paused.
    // (Its timers ran on unscaled time: behind the menu the camera went back and the hold ran out.)
    static async Task BossEntranceWaitsForMenu(DefenseBattle battle)
    {
        battle.EditorStartSession(1, 5, true, Sandbox);
        var sim = battle.Simulation; var camera = battle.BattleCamera.transform; var hud = Object.FindFirstObjectByType<DefensePlayerHUD>();
        Vector3 home = camera.position;
        Place(battle, RobotRole.Warrior); battle.StartBattle(); BringBoss(sim);
        await Until(() => battle.EditorBossEntered, 3, "setup: the boss of 1-5 came without an entrance");
        await Frames(2);
        double held = sim.Time;
        await Seconds(.3f);
        float focusing = Vector3.Distance(camera.position, home);
        Require(sim.Time == held && focusing > .05f, "setup: the entrance does not hold the battle and move the camera");
        battle.ToggleMenu();
        try
        {
            await Seconds(3.5f); // longer than the whole entrance
            float behindMenu = Vector3.Distance(camera.position, home);
            battle.ToggleMenu();
            await Seconds(.5f);
            Require(behindMenu > focusing / 2 && sim.Time - held < .05,
                $"the entrance ran on behind the Esc menu (open 3.5 s): the camera is {behindMenu:0.0} from home (it was {focusing:0.0} when the menu opened), and in the 0.5 s after the menu closed the battle ran {sim.Time - held:0.00} s");
            await Until(() => sim.Time > held && Vector3.Distance(camera.position, home) < .05f, 6, "after the menu closed the entrance never ended (the battle still held, or the camera away)");
        }
        finally { if (hud.MenuOpen) battle.ToggleMenu(); }
    }

    // A4b. Every stage's boss gets the entrance, not only 1-5's. (It was tied to stage 1, wave 5.)
    static async Task EveryBossGetsEntrance(DefenseBattle battle)
    {
        var missed = new List<string>();
        for (int stage = 2; stage <= battle.EditorCampaign.stages.Length; stage++)
        {
            int wave = Array.FindIndex(battle.EditorCampaign.ReadWaves(stage), groups => groups.Any(g => g.Boss)) + 1;
            if (wave == 0) continue;
            battle.EditorStartSession(stage, wave, true, Sandbox);
            Place(battle, RobotRole.Warrior); battle.StartBattle(); BringBoss(battle.Simulation);
            await Seconds(.5f);
            if (!battle.EditorBossEntered) missed.Add(stage + "-" + wave);
        }
        Require(missed.Count == 0, "the boss walks in without an entrance in " + string.Join(", ", missed));
    }

    // A6. The bounds the simulation places robots in cover every floor cell the map draws. (The scene's DeploymentArea was
    // sized for the workshop map and cuts the Stage 1 map's last column off.)
    static Task BoundsCoverFloor(DefenseBattle battle)
    {
        var cut = new List<string>();
        foreach (int stage in new[] { 1, 2 })
        {
            battle.EditorStartSession(stage, 1, true, Sandbox);
            var sim = battle.Simulation;
            var spec = battle.RobotDefinitions.First(robot => robot.stats.role == RobotRole.Warrior).stats;
            Require(sim.Setup.Floor.Length > 0, $"setup: stage {stage} has no painted floor");
            // What the simulation answers for a spot outside its bounds, whatever the wording.
            string outside = sim.PlacementError(spec, sim.Setup.Max + new Point(100, 100));
            var cells = sim.Setup.Floor.Select(tile => (tile.Min + tile.Max) / 2).Where(centre => sim.PlacementError(spec, centre) == outside).ToArray();
            if (cells.Length > 0) cut.Add($"stage {stage}: {cells.Length} cells answer '{outside}', e.g. ({cells[0].X:0.0}, {cells[0].Y:0.0}) against bounds x {sim.Setup.Min.X:0.0}..{sim.Setup.Max.X:0.0}, z {sim.Setup.Min.Y:0.0}..{sim.Setup.Max.Y:0.0}");
        }
        Require(cut.Count == 0, "drawn floor outside the deployment bounds — " + string.Join("; ", cut));
        return Task.CompletedTask;
    }

    // The F1 panel's dialogue jump, at the step it names, as the lesson left it (the lesson's session is the jump's own).
    static async Task<string> Jump(DefenseBattle battle, string id)
    {
        battle.EditorStartSession(1, 1, true, new DefenseTestOptions()); // a normal play's options, not the sandbox's
        string message = battle.DeveloperJump(id);
        var director = battle.Tutorial;
        await Until(() => director.EditorSynced && director.EditorStep != null, 10, $"setup: the lesson of {id} did not begin ({message})");
        await Frames(5);
        return message;
    }
    static string Active(TutorialDirector director) => Field<TutorialSequence>(director, "active")?.sequenceId;

    // J1. 1-1.E07 comes after the wave is won: the jump opens on its reward screen, and E09's 보상 받기 claims it and goes on to
    // 1-2. (The jump only started the wave over: no robot, no fought wave, no reward, so E09 could never be done.)
    static async Task JumpToRewardLine(DefenseBattle battle)
    {
        string message = await Jump(battle, "1-1.E07");
        var director = battle.Tutorial; var sim = battle.Simulation;
        Require(sim.Phase == BattlePhase.Reward && director.EditorStep.stepId == "E07",
            $"the jump to 1-1.E07 shows step {director.EditorStep.stepId} in phase {sim.Phase} (wave {sim.WaveIndex + 1}, {sim.Robots.Count} robots, {sim.Kills} kills), not the won wave's reward" + (message != null ? $" ({message})" : ""));
        Require(Field<GameObject>(Object.FindFirstObjectByType<DefensePlayerHUD>(), "rewardPanel").activeInHierarchy, "1-1.E07: the reward panel is not shown");
        for (int i = 0; i < 20 && director.EditorStep?.stepId != "E09"; i++) { director.EditorFinishTyping(); director.EditorNext(); await Frames(2); }
        Require(director.EditorStep?.stepId == "E09", $"next from 1-1.E07 does not reach E09 (at {director.EditorStep?.stepId})");
        await Frames(3);
        Require(battle.TutorialAllows("Battle.ClaimRewards") && director.EditorBridge.TryGetArea("Battle.ClaimRewards", out _), "1-1.E09: 보상 받기 is not offered");
        battle.ClaimRewards();
        await Until(() => Active(director) == "1-2", 10, $"claiming 1-1's reward did not go on to 1-2 (wave {sim.WaveIndex + 1}, {sim.Phase}, lesson {Active(director)})");
        Require(battle.Simulation == sim && sim.WaveIndex == 1 && sim.Phase == BattlePhase.Ready, $"after the claim: wave {sim.WaveIndex + 1} in {sim.Phase}");
    }

    static StageCache Cache(int stage)
    {
        var text = Resources.Load<TextAsset>(DefenseBattle.StageCachePath(stage));
        Require(text, $"no stage {stage} cache (Tools/Develop/스테이지 캐시)");
        return JsonUtility.FromJson<StageCache>(text.text);
    }
    static PlayMarker Mark(StageCache cache, string id)
    {
        var mark = cache.markers.FirstOrDefault(m => m.id == id);
        Require(mark != null, $"the stage {cache.stage} cache has no mark for {id}");
        return mark;
    }

    // J2. 2-3.D07 comes once the warrior's if is written: the jump has every robot the play had placed by then, each with the
    // code last applied to it in the play (read off the cache's inputs), and the warrior's editor open as it was.
    static async Task JumpMidLesson(DefenseBattle battle)
    {
        var mark = Mark(Cache(2), "2-3.D07");
        string message = await Jump(battle, "2-3.D07");
        var sim = battle.Simulation; var inputs = Cache(2).inputs.Take(mark.input).ToArray();
        Require(!inputs.Any(i => i.kind == "remove"), "setup: the play recalled a robot before 2-3.D07");
        string Robot(int definition, Point at) => $"{definition}@{at.X:0.000},{at.Y:0.000}";
        var placed = inputs.Where(i => i.kind == "place").Select(i => Robot(i.index, new Point(i.x, i.y))).OrderBy(s => s).ToArray();
        var standing = sim.Robots.Select(r => Robot(r.DefinitionIndex, r.Position)).OrderBy(s => s).ToArray();
        Require(standing.SequenceEqual(placed), $"2-3.D07 has robots [{string.Join(" ", standing)}], the play had placed [{string.Join(" ", placed)}]" + (message != null ? $" ({message})" : ""));
        foreach (var bot in sim.Robots)
        {
            var last = inputs.LastOrDefault(i => i.kind == "program" && i.id == bot.Id);
            string expected = last == null ? null : new DefenseProgram(last.name, PlayLog.Unflatten(last.code)).Source;
            Require((bot.Program?.Source) == expected, $"robot {bot.Id} at 2-3.D07 runs\n{bot.Program?.Source}\nbut the play had\n{expected}");
        }
        Require(mark.editing != 0 && battle.CodeEditor.IsOpen && battle.CodeEditor.EditingId == mark.editing,
            $"2-3.D07: the play had robot {mark.editing}'s editor open, the jump has {(battle.CodeEditor.IsOpen ? "robot " + battle.CodeEditor.EditingId + "'s" : "none")}");
        Require(battle.Tutorial.EditorStep?.stepId == "D07" && message == null, $"2-3.D07: at {battle.Tutorial.EditorStep?.stepId} ({message})");
    }

    // J3. Replayed to a mark, the session agrees with the state the cache run recorded there (robots, code, coins, kills, core,
    // wave, phase, stock): the middle and the last mark of each stage.
    static async Task ReplayMatchesPlay(DefenseBattle battle)
    {
        var wrong = new List<string>();
        for (int stage = 1; stage <= DefenseBattle.PrototypeLastStage; stage++)
        {
            var cache = Cache(stage);
            foreach (var mark in new[] { cache.markers[cache.markers.Length / 2], cache.markers[cache.markers.Length - 1] })
            {
                // The jump checks the state right after its replay (before the step's entry effects) and says when it differs;
                // a second replay on a session of its own, the same way, is compared here.
                string message = await Jump(battle, mark.id);
                var setup = battle.EditorReadSetup(); setup.PreviousStageRewards = true; setup.TestOptions = cache.options;
                var sim = new DefenseSimulation(setup);
                string diverged = PlayLog.Replay(sim, cache.inputs, mark.input, mark.tick), now = PlayLog.Digest(sim);
                if (now != mark.digest || message != null || diverged != null) wrong.Add($"{mark.id}: {message ?? diverged}\n  replayed {now}\n  recorded {mark.digest}");
            }
        }
        Require(wrong.Count == 0, "replay differs from the play at " + string.Join("; ", wrong));
    }
}
