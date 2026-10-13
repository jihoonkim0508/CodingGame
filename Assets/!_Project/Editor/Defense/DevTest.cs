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

        // Stage 1 tutorial section titles per wave (1-5), read from the same JSON the game uses.
        public static string[] TutorialSections(int wave)
        {
            var path = AssetDatabase.FindAssets("stage1_" + wave + "_ t:TextAsset", new[] { "Assets/!_Project/Defense/Tutorial/Stage1" })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (path == null) return Array.Empty<string>();
            return StageOneContent.Read(AssetDatabase.LoadAssetAtPath<TextAsset>(path), wave).sections.Select(x => x.section_title).ToArray();
        }
        // Jumps to a Stage 1 tutorial section (0-based), starting 1-wave with the tutorial when needed.
        public static void JumpTutorial(int wave, int section)
        {
            var b = Battle; var stageOne = b && b.Tutorial ? b.Tutorial.EditorStageOne : null;
            if (b && b.Simulation != null && !b.Tutorial) throw new InvalidOperationException(NoTutorial);
            if (stageOne && stageOne.EditorWave == wave) { JumpNow(b, stageOne, section); return; }
            SessionState.SetString(RequestKey + ".jump", wave + "," + section);
            PlayDefense(1, wave, true, wave > 1);
        }
        static void JumpNow(DefenseBattle b, StageOneTutorial stageOne, int section)
        {
            if (stageOne.EditorNeedsRobot(section)) PlaceTeachingRobot(b, stageOne.EditorTeachingRole);
            stageOne.EditorJump(section);
        }
        // Lifts the tutorial input lock just for this placement; the best route-coverage cell is used.
        static void PlaceTeachingRobot(DefenseBattle b, RobotRole role)
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
            var parts = jump.Split(','); int wave = int.Parse(parts[0]), section = int.Parse(parts[1]);
            var b = Battle; var stageOne = b && b.Tutorial ? b.Tutorial.EditorStageOne : null;
            if (b && b.Simulation != null && !b.Tutorial) { SessionState.EraseString(RequestKey + ".jump"); Debug.LogWarning(NoTutorial); return; }
            if (!stageOne || stageOne.EditorWave != wave) return; // the tutorial enters the wave on its next Update
            SessionState.EraseString(RequestKey + ".jump");
            try { JumpNow(b, stageOne, section); } catch (Exception e) { Debug.LogException(e); }
        }
        static void Begin(DefenseBattle battle, int stage, int wave, bool previousStageRewards)
        {
            battle.EditorManualClock = false;
            battle.EditorStartSession(stage, wave, previousStageRewards, Options, pendingContent);
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
            CodeBlock command = new CodeBlock(DefenseProgression.ActionBlock(bot.Spec.NativeAction));
            if (sim.Available(BlockKind.While, bot.Id) > 0 && sim.Available(BlockKind.True, bot.Id) > 0)
            { var loop = new CodeBlock(BlockKind.While, "0", new CodeBlock(BlockKind.True)); loop.Body.Add(command); command = loop; }
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
                        if (sim.Phase == BattlePhase.Running) result = "실패: 웨이브 제한 시간 초과";
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
