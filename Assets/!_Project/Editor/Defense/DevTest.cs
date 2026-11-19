using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense.Editor
{
    // Play always starts from Title. The dev editor and AI test scripts start Defense directly through this class.
    // AI usage (Unity MCP Unity_RunCommand), one call per step because entering Play Mode is asynchronous:
    //   DevTest.PlayDefense(2, 1);        then poll DevTest.Ready
    //   DevTest.State();                  JSON snapshot of the battle
    //   DevTest.AutoDeploy(); DevTest.RunWave();  or  DevTest.RunCampaign(5);
    //   DevTest.Battle / DevTest.Sim      direct access for anything else
    // Reports are written to TestResults/Editor/last.json.
    [InitializeOnLoad]
    public static class DevTest
    {
        const string ScenesRoot = "Assets/!_Project/Scenes/", RequestKey = "CodingGame.DevTest.Request";
        const string NoTutorial = "Defense 씬에 튜토리얼이 연결되어 있지 않습니다 (DefenseBattle.tutorial 비어 있음).";
        public const string ReportPath = "TestResults/Editor/last.json";

        [Serializable] sealed class Request { public int stage = 1, wave = 1; public bool previousStageRewards = true; public DefenseTestOptions options; }
        [Serializable] public sealed class LogEntry { public string origin, kind, reason; public int stage, wave, source, target; public double time; public float amount, appliedAmount; }
        [Serializable] public sealed class Metric { public string origin, entity; public int id, kills, leaks; public float damageDealt, damageTaken; }
        [Serializable] sealed class RobotInfo { public int id, level; public string role, lastResult, fault, code; public float health, x, y; public int attacks; }
        [Serializable] sealed class Snapshot
        {
            public string scene, phase, status, tutorialStep;
            public bool ready;
            public int stage, wave, baseHealth, coins, kills, leaks, remainingEnemies, activeEnemies;
            public double time;
            public float timeScale;
            public string[] robotStock, blockStock;
            public RobotInfo[] robots;
        }
        [Serializable] sealed class Report
        {
            public string result;
            public string[] waves;
            public DefenseTestOptions options;
            public Metric[] metrics;
            public LogEntry[] events;
            public Snapshot final;
        }

        public static DefenseTestOptions Options = new DefenseTestOptions();
        public static readonly List<LogEntry> Events = new List<LogEntry>();
        public static readonly Dictionary<string, Metric> Metrics = new Dictionary<string, Metric>();
        static DefenseBattle subscribed;
        static DefenseCampaignDefinition pendingContent;

        static DevTest()
        {
            // A domain reload also happens while entering Play Mode; resetting then would drop a PlayDefense request.
            if (!EditorApplication.isPlayingOrWillChangePlaymode) StartFrom(GameFlow.TitleScene);
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) StartFrom(GameFlow.TitleScene); };
            EditorApplication.update += ApplyRequest;
        }
        static void StartFrom(string scene) =>
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenesRoot + scene + ".unity");

        public static DefenseBattle Battle => EditorApplication.isPlaying ? Object.FindFirstObjectByType<DefenseBattle>() : null;
        public static DefenseSimulation Sim => Battle ? Battle.Simulation : null;
        public static bool Ready => Sim != null && !SessionState.GetBool(RequestKey + ".pending", false);

        public static void PlayTitle()
        {
            if (EditorApplication.isPlaying) { GameFlow.LoadScene(GameFlow.TitleScene); return; }
            StartFrom(GameFlow.TitleScene); EditorApplication.isPlaying = true;
        }
        public static void PlayDefense(int stage = 1, int wave = 1, bool tutorial = false, bool previousStageRewards = true) =>
            PlayDefense(stage, wave, previousStageRewards, new DefenseTestOptions { tutorial = tutorial, tutorialInput = tutorial });
        // Starts (or restarts) a Defense test session; content overrides the campaign asset with edited Play Mode copies.
        public static void PlayDefense(int stage, int wave, bool previousStageRewards, DefenseTestOptions options, DefenseCampaignDefinition content = null)
        {
            Options = options.Copy(); pendingContent = content;
            var battle = Battle;
            if (battle && battle.Simulation != null) { SessionState.SetBool(RequestKey + ".pending", false); Begin(battle, stage, wave, previousStageRewards); return; }
            SessionState.SetString(RequestKey, JsonUtility.ToJson(new Request { stage = stage, wave = wave, previousStageRewards = previousStageRewards, options = Options }));
            SessionState.SetBool(RequestKey + ".pending", true);
            if (EditorApplication.isPlaying) { SceneManager.LoadScene(GameFlow.DefenseScene); return; }
            StartFrom(GameFlow.DefenseScene); EditorApplication.isPlaying = true;
        }
        public static void PlayStageMap()
        {
            if (EditorApplication.isPlaying) { GameFlow.LoadScene(GameFlow.StageScene); return; }
            StartFrom(GameFlow.StageScene); EditorApplication.isPlaying = true;
        }
        // Every project scene (test scenes included), third-party demos excluded.
        public static string[] ScenePaths => AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.Contains("/ThirdParty/")).OrderBy(path => path).ToArray();
        // Plays any scene, build list or not: starts Play there, or switches to it while playing. The next plain Play still starts at Title.
        public static void PlayScene(string path)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) throw new ArgumentException("씬을 찾을 수 없습니다: " + path);
            if (EditorApplication.isPlaying) { EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)); return; }
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path); EditorApplication.isPlaying = true;
        }
        // Opens a scene for editing (asks to save the current one first).
        public static void OpenScene(string path)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 중에는 편집용으로 열 수 없습니다. Play 종료 후 다시 시도하세요.");
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
        public static void Stop() => EditorApplication.isPlaying = false;
        // Back to a fresh install: every saved value lives in PlayerPrefs (cleared stages), so all of it goes.
        public static void ResetSaveData()
        {
            PlayerPrefs.DeleteAll(); PlayerPrefs.Save();
            StageMap.StageProgress.SelectedStage = 0;
            if (EditorApplication.isPlaying) GameFlow.LoadScene(GameFlow.TitleScene);
        }

        // Every enabled lesson (skips the disabled SequenceTest sample); Sequence(stage, wave) is null for a free-play wave.
        public static TutorialSequence[] Lessons => AssetDatabase.FindAssets("t:TutorialSequence")
            .Select(guid => AssetDatabase.LoadAssetAtPath<TutorialSequence>(AssetDatabase.GUIDToAssetPath(guid))).Where(s => s && s.enabled).ToArray();
        public static TutorialSequence Sequence(int stage, int wave) => Lessons.FirstOrDefault(s => s.stage == stage && s.wave == wave);
        // A lesson's steps ("stepId first line"); empty for a wave without one.
        public static string[] TutorialSections(int stage, int wave)
        {
            var sequence = Sequence(stage, wave);
            return sequence ? sequence.steps.Select(s => s.stepId + " " + (s.texts.Length > 0 ? s.texts[0].text : "")).ToArray() : Array.Empty<string>();
        }
        public static string[] TutorialSections(int wave) => TutorialSections(1, wave);

        // Which state the last jump or test Play started from (the dev editor shows it); null: a plain start.
        public static string LastJump { get; private set; }
        // Where the play cache is read: null for Resources/Dev, or a folder with PlayCache_<stage>.json (an empty one: no cache).
        public static string CacheFolder;
        public static bool JumpPending => SessionState.GetString(RequestKey + ".jump", "") != "";
        // The F1 panel's jump (DefenseBattle.DeveloperJump "2-3.A01", or DeveloperJumpWave "2-4" for a wave without a lesson):
        // the recorded play's state from the play cache; without a fresh recording the wave starts over with the robots and
        // code the stage's earlier lessons left. step: 0-based. Starts the Defense scene first when there is no battle.
        public static void JumpTutorial(int stage, int wave, int step)
        {
            var b = Battle;
            if (b && b.Simulation != null && !b.Tutorial) throw new InvalidOperationException(NoTutorial);
            if (b && b.Simulation != null) { Jump(b, stage, wave, step); return; }
            SessionState.SetString(RequestKey + ".jump", stage + "," + wave + "," + step);
            if (EditorApplication.isPlaying) { SceneManager.LoadScene(GameFlow.DefenseScene); return; }
            StartFrom(GameFlow.DefenseScene); EditorApplication.isPlaying = true;
        }
        static void Jump(DefenseBattle b, int stage, int wave, int step)
        {
            var sequence = Sequence(stage, wave);
            if (sequence && (step < 0 || step >= sequence.steps.Length)) throw new ArgumentOutOfRangeException(nameof(step), $"{stage}-{wave}에 {step + 1}번째 단계가 없습니다.");
            if (!Options.tutorial) { Options = Options.Copy(); Options.tutorial = Options.tutorialInput = true; }
            b.EditorSetOptions(Options);
            var session = b.Simulation;
            string note = sequence ? b.DeveloperJump(sequence.sequenceId + "." + sequence.steps[step].stepId, CacheFolder) : b.DeveloperJumpWave(stage + "-" + wave, CacheFolder);
            Events.Clear(); Metrics.Clear(); Subscribe(b);
            if (note == null || note.StartsWith("캐시 재생")) { LastJump = note ?? "캐시로 이동"; return; } // a replay that diverged stays as replayed
            if (b.Simulation == session) // not started over (a stage past the F1 panel's): the wave's start as a test Play gives it
            {
                b.EditorStartSession(stage, wave, true, Options);
                if (sequence) b.Tutorial.JumpWhenBegun(sequence.sequenceId, step);
            }
            RestoreTutorialWork(b, stage, wave, step);
            LastJump = Describe(note);
        }
        static string Describe(string note) => (note.StartsWith("캐시 오래됨") ? "오래된 캐시" : note.StartsWith("캐시") ? "캐시 없음" : note) + ": 웨이브 시작 상태 (수업 로봇·정답 코드 복원)";

        // Stage 1 jump inside the running session, synchronous (the lesson checks and screen tours step through 1-x with it);
        // elsewhere the F1 jump above.
        public static void JumpTutorial(int wave, int step)
        {
            var b = Battle;
            if (b && b.Simulation != null && !b.Tutorial) throw new InvalidOperationException(NoTutorial);
            if (!(b && b.Simulation != null && b.Stage == 1 && b.Simulation.WaveIndex + 1 == wave && b.Tutorial.EditorSynced)) { JumpTutorial(1, wave, step); return; }
            var sequence = Sequence(1, wave);
            if (!sequence) throw new InvalidOperationException($"1-{wave} 튜토리얼 시퀀스가 없습니다.");
            RestoreTutorialWork(b, 1, wave, step);
            b.Tutorial.EditorRun(sequence, step);
        }
        // A player who followed a stage's lessons up to (wave, step) has the robots they placed (by the lesson's own entry,
        // placeShooter / placeWarrior, or by the player at a RobotPlaced step: the dock button an earlier step pointed at) and
        // the code each answer step taught on its robot (robotIndex: installation order, negative from the newest). Rebuilt in
        // lesson order on top of the session; robots already there count, and code on a robot that had code is left alone.
        // Each stage starts on its own (from the stage map); the stock comes from the session (Award.md rewards).
        static void RestoreTutorialWork(DefenseBattle b, int stage, int wave, int step = 0)
        {
            var current = Sequence(stage, wave);
            var done = Enumerable.Range(1, wave - 1).Select(w => Sequence(stage, w)).Where(s => s).SelectMany(s => s.steps)
                .Concat(current ? current.steps.Take(step) : Enumerable.Empty<TutorialStep>());
            var sim = b.Simulation;
            var coded = new HashSet<int>(sim.Robots.Where(r => r.Program != null).Select(r => r.Id));
            var roles = new List<RobotRole>(); var dock = RobotRole.Warrior;
            foreach (var s in done)
            {
                var button = (s.targetKeys ?? Array.Empty<string>()).LastOrDefault(k => k.StartsWith(TutorialKeys.RobotButtonPrefix));
                if (button != null && int.TryParse(button.Substring(TutorialKeys.RobotButtonPrefix.Length), out int i) && i >= 0 && i < sim.Setup.ActionProfiles.Length) dock = b.DefinitionRole(i);
                if (s.placeShooter) Place(b, roles, RobotRole.Shooter, false, true);
                if (s.placeWarrior) Place(b, roles, RobotRole.Warrior, s.robotIndex < 0, true);
                if (s.completionSignal == TutorialSignals.RobotPlaced) Place(b, roles, dock, true, false);
                if (s.completionSignal != TutorialSignals.ProgramMatches) continue;
                var bot = sim.Robots.OrderBy(r => r.Id).ElementAtOrDefault(s.robotIndex < 0 ? roles.Count + s.robotIndex : s.robotIndex);
                if (bot == null || coded.Contains(bot.Id)) continue;
                try { sim.ApplyProgram(bot.Id, "robot_" + bot.Id, TutorialAnswers.Parse(s.expectedCode)); }
                catch (FormatException e) { Debug.LogWarning($"튜토리얼 코드 복원 실패 ({stage}-{wave} 로봇 {bot.Id}): {e.Message}"); }
            }
        }
        // The lesson's robot of the role, unless it stood already (another: one more); by the road as the lesson puts it, or
        // at the best route coverage for one the player placed.
        static void Place(DefenseBattle b, List<RobotRole> roles, RobotRole role, bool another, bool byRoad)
        {
            if (!another && roles.Contains(role)) return;
            roles.Add(role);
            var sim = b.Simulation;
            if (sim.Robots.Count >= roles.Count) return;
            if (byRoad) { if (sim.RobotAvailable(role) < 1) sim.GrantRobot(role, 1); b.PlaceLessonRobot(role, true); }
            if (sim.Robots.Count < roles.Count) PlaceTeachingRobot(b, role);
        }
        // Lifts the tutorial input lock just for this placement; the best route-coverage cell is used.
        internal static void PlaceTeachingRobot(DefenseBattle b, RobotRole role)
        {
            var sim = b.Simulation; int index = Enumerable.Range(0, sim.Setup.ActionProfiles.Length).First(i => b.DefinitionRole(i) == role);
            var spec = b.RobotDefinition(index).stats;
            if (sim.RobotAvailable(role) < 1) sim.GrantRobot(role, 1);
            var cells = new List<Point>();
            for (float x = sim.Setup.Min.X + .5f; x < sim.Setup.Max.X; x += 1)
                for (float y = sim.Setup.Min.Y + .5f; y < sim.Setup.Max.Y; y += 1)
                { var p = new Point(x, y); if (sim.PlacementError(spec, p) == null) cells.Add(p); }
            if (cells.Count == 0) throw new InvalidOperationException("교육용 로봇을 놓을 자리가 없습니다.");
            var best = cells.OrderByDescending(p => Coverage(sim, spec, p)).First();
            bool limited = sim.Options.tutorialInput; sim.Options.tutorialInput = false;
            try { if (!b.PlaceRobot(index, new Vector3(best.X, 0, best.Y))) throw new InvalidOperationException("교육용 로봇 배치 실패: " + b.StatusMessage); }
            finally { sim.Options.tutorialInput = limited; }
        }

        static void ApplyRequest()
        {
            ApplyJump();
            if (!SessionState.GetBool(RequestKey + ".pending", false)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(RequestKey + ".pending", false); return; }
            var battle = Battle; if (!battle || battle.Simulation == null) return;
            SessionState.SetBool(RequestKey + ".pending", false);
            var request = JsonUtility.FromJson<Request>(SessionState.GetString(RequestKey, "{}"));
            Options = request.options ?? new DefenseTestOptions();
            try { Begin(battle, request.stage, request.wave, request.previousStageRewards); }
            catch (Exception e) { Debug.LogException(e); }
        }
        static void ApplyJump()
        {
            var jump = SessionState.GetString(RequestKey + ".jump", "");
            if (jump == "") return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.EraseString(RequestKey + ".jump"); return; }
            var parts = jump.Split(',');
            if (parts.Length != 3) { SessionState.EraseString(RequestKey + ".jump"); return; }
            var b = Battle;
            if (b && b.Simulation != null && !b.Tutorial) { SessionState.EraseString(RequestKey + ".jump"); Debug.LogWarning(NoTutorial); return; }
            if (!b || b.Simulation == null || !b.Tutorial.EditorSynced) return; // the session starts, and the tutorial resets for it, on a later Update
            SessionState.EraseString(RequestKey + ".jump");
            try { Jump(b, int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2])); } catch (Exception e) { Debug.LogException(e); }
        }
        static void Begin(DefenseBattle battle, int stage, int wave, bool previousStageRewards)
        {
            battle.EditorStartSession(stage, wave, previousStageRewards, Options, pendingContent);
            LastJump = null;
            // Starting past 1-1 with the earlier rewards: the state a player would have there, the recorded play's wave start
            // (F1 "2-4") when the play cache has it, else the wave's start with the robots and code the stage's lessons left.
            if (previousStageRewards && (stage > 1 || wave > 1))
            {
                var note = battle.DeveloperJumpWave(stage + "-" + wave, CacheFolder);
                if (note == null) { battle.EditorSetOptions(Options); LastJump = "캐시로 이동"; }
                else
                {
                    battle.EditorStartSession(stage, wave, previousStageRewards, Options, pendingContent); // the window's content and options again
                    RestoreTutorialWork(battle, stage, wave);
                    LastJump = Describe(note);
                }
            }
            Events.Clear(); Metrics.Clear(); Subscribe(battle);
        }

        static DefenseBattle Require()
        {
            var battle = Battle;
            if (!battle || battle.Simulation == null) throw new InvalidOperationException("Defense 전투가 실행 중이 아닙니다. DevTest.PlayDefense()로 시작하세요.");
            Subscribe(battle); return battle;
        }

        public static void SetOptions(DefenseTestOptions options) { Options = options.Copy(); var b = Battle; if (b) b.EditorSetOptions(Options); }
        public static void Speed(float scale) => Time.timeScale = Mathf.Clamp(scale, 0, 20);
        public static void Coins(int value) => Require().EditorSetCoins(value);
        public static void GrantAll() => Require().EditorGrantAll();
        public static void Continue() => Require().ContinueCampaign();
        public static void StartWave() { var b = Require(); b.StartBattle(); if (b.Simulation.Phase != BattlePhase.Running) throw new Exception(b.Simulation.StartError() ?? "전투 시작 차단 (튜토리얼 입력 제한일 수 있음)"); }

        // Places one robot of each available type at the best route coverage and gives every robot a looping native action.
        // ponytail: a progression smoke test, not an optimal player strategy.
        public static void AutoDeploy()
        {
            var b = Require(); var sim = b.Simulation;
            if (sim.Phase != BattlePhase.Ready) throw new InvalidOperationException("준비 단계에서만 배치할 수 있습니다: " + sim.Phase);
            for (int index = 0; index < sim.Setup.ActionProfiles.Length; index++)
            {
                var def = b.RobotDefinition(index); var kind = DefenseProgression.ActionBlock(def.stats.NativeAction);
                if (sim.RobotAvailable(def.stats.role) < 1 || sim.Available(kind) < 1) continue;
                var candidates = new List<Point>();
                for (float x = sim.Setup.Min.X + .5f; x < sim.Setup.Max.X; x += 1)
                    for (float y = sim.Setup.Min.Y + .5f; y < sim.Setup.Max.Y; y += 1)
                    { var p = new Point(x, y); if (sim.PlacementError(def.stats, p) == null) candidates.Add(p); }
                if (candidates.Count == 0) continue;
                var best = candidates.OrderByDescending(p => Coverage(sim, def.stats, p)).First();
                b.PlaceRobot(index, new Vector3(best.X, 0, best.Y));
            }
            foreach (var robot in sim.Robots) { Program(sim, robot); sim.UpgradeRobot(robot.Id); }
        }
        static void Program(DefenseSimulation sim, RobotState bot)
        {
            sim.ApplyProgram(bot.Id, "robot_" + bot.Id, Array.Empty<CodeBlock>());
            var kind = DefenseProgression.ActionBlock(bot.Spec.NativeAction);
            CodeBlock command = PythonTreeCompiler.HasEnemyArgument(kind) ? new CodeBlock(kind, "0", new CodeBlock(BlockKind.NearestEnemy)) : new CodeBlock(kind);
            if (sim.Available(BlockKind.While, bot.Id) > 0)
            { var loop = new CodeBlock(BlockKind.While); loop.Body.Add(command); command = loop; } // no condition slot: while True
            else if (sim.Available(BlockKind.For, bot.Id) > 0 && sim.Available(BlockKind.Number, bot.Id) > 0)
            { var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, "100")); loop.Body.Add(command); command = loop; }
            sim.ApplyProgram(bot.Id, "robot_" + bot.Id, new[] { command });
        }
        static float Coverage(DefenseSimulation sim, RobotSpec spec, Point p)
        {
            float score = 0;
            foreach (var route in sim.Setup.Routes)
                for (int i = 1; i < route.Points.Length; i++)
                    for (int s = 0; s <= 20; s++)
                        if (Point.Distance(p, Point.Lerp(route.Points[i - 1], route.Points[i], s / 20f)) <= spec.range)
                            score += Point.Distance(route.Points[i - 1], route.Points[i]) / 21;
            return spec.role == RobotRole.Buffer ? sim.Robots.Count(r => Point.Distance(r.Position, p) <= spec.range) * 100 + score : score;
        }

        // Starts the wave if needed and simulates it to the end in one call. Views catch up on the next frame.
        public static string RunWave(double maxSeconds = 600)
        {
            var b = Require(); var sim = b.Simulation;
            if (sim.Phase == BattlePhase.Ready) StartWave();
            if (sim.Phase == BattlePhase.Paused) sim.TogglePause();
            for (double t = 0; t < maxSeconds && sim.Phase == BattlePhase.Running; t += DefenseSimulation.TickDuration) sim.Advance(DefenseSimulation.TickDuration);
            return State();
        }

        // Plays stage 1-1 to the end of toStage with AutoDeploy, tutorial off. Returns a summary and writes ReportPath.
        public static string RunCampaign(int toStage = 5, double maxSecondsPerWave = 600)
        {
            var b = Require();
            if (b.Stage != 1 || b.Simulation.WaveIndex != 0 || b.Simulation.Phase != BattlePhase.Ready || b.Simulation.Robots.Count > 0)
            { Options = new DefenseTestOptions { tutorial = false, tutorialInput = false, detailedLog = true }; Begin(b, 1, 1, true); }
            var waves = new List<string>(); string result = null;
            for (int guard = 0; guard < 200 && result == null; guard++)
            {
                var sim = b.Simulation;
                switch (sim.Phase)
                {
                    case BattlePhase.Ready:
                        AutoDeploy(); int kills = sim.Kills, leaks = sim.Leaks; RunWave(maxSecondsPerWave);
                        waves.Add($"S{b.Stage} W{sim.WaveIndex + 1}: {sim.Phase} core={sim.BaseHealth} kills={sim.Kills - kills} leaks={sim.Leaks - leaks} robots={sim.Robots.Count}");
                        if (sim.Phase == BattlePhase.Running) result = $"실패: 웨이브가 {maxSecondsPerWave}초 안에 끝나지 않음";
                        break;
                    case BattlePhase.Reward: b.ContinueCampaign(); break;
                    case BattlePhase.Victory:
                        if (sim.CanAdvanceStage && b.Stage < toStage) b.ContinueCampaign();
                        else result = $"통과: 스테이지 {b.Stage}까지 진행 (자동 배치, UI 별도)";
                        break;
                    case BattlePhase.Defeat: result = $"실패: 기지 파괴 S{b.Stage} W{sim.WaveIndex + 1}"; break;
                    default: result = "중단: 예상하지 못한 단계 " + sim.Phase; break;
                }
            }
            result ??= "중단: 반복 한도 초과";
            SaveReport(result, waves);
            return result + "\n" + string.Join("\n", waves);
        }

        public static string State()
        {
            var b = Battle; var sim = b ? b.Simulation : null;
            var s = new Snapshot { scene = SceneManager.GetActiveScene().name, ready = Ready, timeScale = Time.timeScale };
            if (sim != null)
            {
                s.phase = sim.Phase.ToString(); s.status = b.StatusMessage; s.tutorialStep = b.Tutorial ? b.Tutorial.EditorStep?.stepId : null;
                s.stage = sim.Setup.Stage; s.wave = sim.WaveIndex + 1; s.baseHealth = sim.BaseHealth; s.coins = sim.Coins;
                s.kills = sim.Kills; s.leaks = sim.Leaks; s.remainingEnemies = sim.RemainingEnemies; s.activeEnemies = sim.Enemies.Count(e => e.Active); s.time = sim.Time;
                s.robotStock = sim.RobotInventory.Select(p => $"{p.Key}:{p.Value}").ToArray();
                s.blockStock = sim.Inventory.Select(p => $"{p.Key}:{p.Value}").ToArray();
                s.robots = sim.Robots.Select(r => new RobotInfo { id = r.Id, level = r.Level, role = r.Spec.role.ToString(), health = r.Health ?? -1, x = r.Position.X, y = r.Position.Y,
                    attacks = r.AttacksThisWave, lastResult = r.LastResult.ToString(), fault = r.Program?.Fault, code = r.Program?.Source }).ToArray();
            }
            return JsonUtility.ToJson(s, true);
        }

        public static string SaveReport(string result, IEnumerable<string> waves = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            var json = JsonUtility.ToJson(new Report { result = result, waves = waves?.ToArray() ?? Array.Empty<string>(), options = Options,
                metrics = Metrics.Values.ToArray(), events = Events.ToArray(), final = JsonUtility.FromJson<Snapshot>(State()) }, true);
            File.WriteAllText(ReportPath, json);
            return ReportPath;
        }

        static void Subscribe(DefenseBattle battle)
        {
            if (subscribed == battle) return;
            if (subscribed) subscribed.EditorCombatEvent -= Combat;
            subscribed = battle;
            if (subscribed) subscribed.EditorCombatEvent += Combat;
        }
        static void Combat(string origin, DefenseSimulation sim, CombatEvent e)
        {
            Metric Get(string entity, int id)
            {
                var key = origin + "/" + entity + "/" + id;
                if (!Metrics.TryGetValue(key, out var m)) Metrics.Add(key, m = new Metric { origin = origin, entity = entity, id = id });
                return m;
            }
            if (e.Kind == "hit" || e.Kind == "projectile-hit") { Get("robot", e.Source).damageDealt += e.AppliedAmount; Get("enemy", e.Target).damageTaken += e.AppliedAmount; }
            if (e.Kind == "robot-damage") { Get("enemy", e.Source).damageDealt += e.AppliedAmount; Get("robot", e.Target).damageTaken += e.AppliedAmount; }
            if (e.Kind == "killed") Get("robot", e.Source).kills++;
            if (e.Kind == "leaked") Get("enemy", e.Source).leaks++;
            if (!Options.detailedLog) return;
            if (Events.Count >= 1000) Events.RemoveAt(0);
            Events.Add(new LogEntry { origin = origin, stage = sim.Setup.Stage, wave = sim.WaveIndex + 1, time = sim.Time, kind = e.Kind, reason = e.Reason,
                source = e.Source, target = e.Target, amount = e.Amount, appliedAmount = e.AppliedAmount });
        }
    }
}
