using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Play Mode smoke run of the Defense scene, also in batchmode: a battle with live robot tags, the result screen
    // (debrief, stars, name tags), a lost last wave and its retry, and the Esc menu. Fails on any error, exception or
    // assert logged on the way. No -quit: it exits by itself with 0 (clean) or 1.
    //   Unity.exe -batchmode -nographics -projectPath . -executeMethod CodingGame.Defense.Editor.PlaySmoke.Run -logFile smoke.log
    // The steps and any errors are written to TestResults/Editor/smoke.txt.
    [InitializeOnLoad]
    public static class PlaySmoke
    {
        const string Key = "CodingGame.PlaySmoke.", ReportPath = "TestResults/Editor/smoke.txt";
        const double StepSeconds = 90;
        static int Step { get => SessionState.GetInt(Key + "step", 0); set { SessionState.SetInt(Key + "step", value); SessionState.SetFloat(Key + "since", (float)EditorApplication.timeSinceStartup); wait = 0; } }
        static int wait;
        static readonly DefenseTestOptions Sandbox = new DefenseTestOptions { tutorial = false, tutorialInput = false };

        // Entering Play Mode reloads the scripts; pick the run back up afterwards.
        static PlaySmoke() { if (Step > 0) Hook(); }
        static void Hook() { Application.logMessageReceived += Record; EditorApplication.update += Tick; }

        [MenuItem("Tools/Develop/Play 모드 스모크 검사")]
        public static void Run()
        {
            SessionState.SetString(Key + "log", ""); Step = 1; Hook();
            DevTest.PlayDefense(2, 5, true, Sandbox);
        }
        static void Record(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Note("ERROR " + message + "\n" + stack);
        }
        // A state the run relies on; a wrong one fails the run like a logged error.
        static void Expect(bool ok, string what) { if (!ok) Note("ERROR expected: " + what); }
        static void Note(string line) => SessionState.SetString(Key + "log", SessionState.GetString(Key + "log", "") + line + "\n");
        // Frames for the HUD to draw what the last step changed.
        static bool Waited(int count) => ++wait > count;

        // A step that throws ends the run there instead of throwing again every frame until the timeout.
        static void Tick()
        {
            try { Advance(); }
            catch (System.Exception error) { Finish($"exception at step {Step}: {error}"); }
        }
        static void Advance()
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "since", 0) > StepSeconds) { Finish("timeout at step " + Step); return; }
            if (!EditorApplication.isPlaying || !DevTest.Ready) return;
            var battle = DevTest.Battle; var sim = battle.Simulation;
            var hud = Object.FindFirstObjectByType<DefensePlayerHUD>();
            switch (Step)
            {
                case 1: // 2-5 (last wave): place, start, let the robot tags draw for a moment
                    DevTest.AutoDeploy(); battle.StartBattle();
                    Note($"2-5 started: robots {sim.Robots.Count}, phase {sim.Phase}, status '{battle.StatusMessage}'");
                    Step = 2; break;
                case 2:
                    if (!Waited(30)) return;
                    Note($"2-5 running: tags {Tags()}");
                    DevTest.RunWave(); Step = 3; break;
                case 3:
                    if (!Waited(20)) return;
                    Note($"2-5 ended: {sim.Phase}, core {sim.BaseHealth}, rush {sim.LastRush}, debrief shown {Shown("Debrief")}, tags {Tags()}");
                    Expect(sim.Phase != BattlePhase.Running && Shown("Debrief"), "2-5 ends with the debrief shown");
                    if (sim.Phase == BattlePhase.Reward) battle.ContinueCampaign();
                    Step = 4; break;
                case 4:
                    if (!Waited(20)) return;
                    Note($"2-5 after claim: {sim.Phase}, stars {sim.StageStars}");
                    // 3-5 with one robot: the rush should take the core, then retry the wave.
                    DevTest.PlayDefense(3, 5, true, Sandbox); Step = 5; break;
                case 5:
                    DevTest.AutoDeploy();
                    foreach (var extra in sim.Robots.Skip(1).ToArray()) sim.RemoveRobot(extra.Id);
                    battle.StartBattle(); DevTest.RunWave(); Step = 6; break;
                case 6:
                    if (!Waited(20)) return;
                    Note($"3-5 one robot ended: {sim.Phase}, core {sim.BaseHealth}, rush {sim.LastRush}, can retry {battle.CanRetryWave}, debrief shown {Shown("Debrief")}, restart button {Shown("RestartStage")}");
                    Expect(sim.Phase == BattlePhase.Defeat && battle.CanRetryWave && Shown("RestartStage"), "3-5 lost with retry and restart offered");
                    if (battle.CanRetryWave) battle.RetryWave();
                    Step = 7; break;
                case 7:
                    if (!Waited(10)) return;
                    Note($"after retry: {sim.Phase}, wave {sim.WaveIndex + 1}, robots {sim.Robots.Count}, core {sim.BaseHealth}, status '{battle.StatusMessage}'");
                    Expect(sim.Phase == BattlePhase.Ready && sim.WaveIndex == 4 && sim.Robots.Count == 1 && sim.BaseHealth == sim.Setup.BaseHealth, "retry restores wave 5, its robot and the core");
                    battle.StartBattle(); hud.ToggleMenu(); Step = 8; break;
                case 8:
                    if (!Waited(10)) return;
                    Note($"menu open: {hud.MenuOpen}, phase {sim.Phase}");
                    Expect(hud.MenuOpen && sim.Phase == BattlePhase.Paused, "first Esc opens the menu and pauses");
                    hud.ToggleMenu(); Step = 9; break;
                case 9:
                    if (!Waited(10)) return;
                    Note($"menu closed: {!hud.MenuOpen}, phase {sim.Phase}");
                    Expect(!hud.MenuOpen && sim.Phase == BattlePhase.Running, "closing the menu resumes the wave");
                    Finish(null); break;
            }
        }
        static bool Shown(string name) { var found = GameObject.Find(name); return found && found.activeInHierarchy; }
        static string Tags() => string.Join(" | ", Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None)
            .Where(t => t.name == "Status" && t.gameObject.activeInHierarchy).Select(t => t.GetParsedText()));

        static void Finish(string failure)
        {
            if (failure != null) Note("ERROR " + failure);
            string log = SessionState.GetString(Key + "log", "");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, log);
            SessionState.EraseInt(Key + "step"); EditorApplication.update -= Tick; Application.logMessageReceived -= Record;
            int code = log.Contains("ERROR ") ? 1 : 0;
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else { EditorApplication.isPlaying = false; Debug.Log("[PlaySmoke] " + (code == 0 ? "통과" : "실패") + " · " + ReportPath); }
        }
    }
}
