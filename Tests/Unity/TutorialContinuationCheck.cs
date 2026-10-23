using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using UnityEngine;

// run_script entry TutorialContinuationCheck.Run, after DevTest.PlayDefense(1,5,true,true).
// Uses real UI entry points and inventory validation. Does not grant stock or bypass tutorial locks.
public static class TutorialContinuationCheck
{
    public static string Start()
    {
        Application.runInBackground = true;
        UnityEditor.EditorApplication.delayCall += async () => {
            string result = await Run();
            System.IO.Directory.CreateDirectory("TestResults");
            System.IO.File.WriteAllText("TestResults/tutorial-continuation.txt", result);
            Debug.Log(result);
        };
        return "Scheduled; result: TestResults/tutorial-continuation.txt";
    }
    static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static void Next(TutorialDirector d) => d.GetType().GetMethod("NextText", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null);
    public static async Task<string> Run()
    {
        var log = new List<string>();
        var seen = new HashSet<string>();
        string last = "";
        float changed = Time.realtimeSinceStartup;
        try
        {
            for (int tick = 0; tick < 6000; tick++)
            {
                await Task.Delay(50);
                if (!UnityEditor.EditorApplication.isPlaying) return "CANCELLED: Play Mode stopped";
                var b = DevTest.Battle;
                if (!b || b.Simulation == null) continue;
                var sim = b.Simulation; var d = b.Tutorial;
                Time.timeScale = sim.Phase == BattlePhase.Running ? 20 : 1;
                if (sim.Phase == BattlePhase.Defeat) throw new Exception("Defeat");
                if (sim.Phase == BattlePhase.Victory)
                {
                    log.Add($"Stage {b.Stage}: health={sim.BaseHealth}, kills={sim.Kills}, leaks={sim.Leaks}");
                    if (b.Stage == 2) return "PASS " + seen.Count + " steps\n" + string.Join("\n", log);
                    // The normal stage-map route opens a fresh Defense session, not the dev-only in-place AdvanceStage.
                    DevTest.PlayDefense(2, 1, true, true); continue;
                }
                var seq = Field<TutorialSequence>(d, "active");
                if (!d.IsActive || !seq) continue;
                int index = Field<int>(d, "progressStep");
                if (index >= seq.steps.Length) continue;
                var step = seq.steps[index]; string key = seq.sequenceId + "." + step.stepId;
                if (key != last) { last = key; changed = Time.realtimeSinceStartup; if (seen.Add(key)) log.Add(key); }
                if (Time.realtimeSinceStartup - changed > 20) throw new Exception("Stuck: " + key + " / " + b.StatusMessage);
                var view = Field<TutorialDialogueView>(d, "view");
                if (!view.IsVisible) continue;
                d.EditorFinishTyping();
                if (step.kind == TutorialStepKind.Dialogue) { Next(d); continue; }
                if (Field<HashSet<string>>(d, "completedStep").Contains(step.stepId)) { Next(d); continue; }
                string target = step.targetKeys.FirstOrDefault() ?? "";
                switch (step.completionSignal)
                {
                    case TutorialSignals.ProgramMatches:
                        var panel = b.CodeEditor.Panel;
                        panel.LoadProgram(panel.FunctionName, TutorialAnswers.Parse(step.expectedCode));
                        if (!panel.IsApplied) throw new Exception("Answer rejected: " + key);
                        break;
                    case TutorialSignals.EditorOpened: b.DevelopSelected(); break;
                    case TutorialSignals.EditorClosed: b.CodeEditor.Close(); break;
                    case TutorialSignals.BattleStarted: b.StartBattle(); break;
                    case TutorialSignals.RobotPlaced: Place(b); break;
                    default:
                        if (target.StartsWith(TutorialKeys.RobotButtonPrefix)) b.ChooseRobot(1);
                        else if (target.StartsWith("Robot.Unit.")) b.SelectRobot(sim.Robots.OrderBy(r => r.Id).ElementAt(int.Parse(target.Substring(11))).Id);
                        else if (target == "Battle.ClaimRewards") { log.Add($"Wave {seq.sequenceId}: hp={sim.BaseHealth}, survivors={sim.LastSurvivors}"); b.ClaimRewards(); }
                        break;
                }
            }
            throw new Exception("Timeout: " + last);
        }
        catch (Exception e) { return "FAIL " + e + "\n" + string.Join("\n", log); }
        finally { Time.timeScale = 1; }
    }
    static void Place(DefenseBattle b)
    {
        var sim = b.Simulation; var spec = b.RobotDefinition(1).stats;
        var samples = new List<System.Numerics.Vector2>();
        foreach (var route in sim.Setup.Routes)
            for (int i = 1; i < route.Points.Length; i++)
                for (float t = 0; t < 1; t += .05f) samples.Add(System.Numerics.Vector2.Lerp(route.Points[i-1], route.Points[i], t));
        var cells = new List<System.Numerics.Vector2>();
        for (float x = sim.Setup.Min.X + .5f; x < sim.Setup.Max.X; x++)
            for (float y = sim.Setup.Min.Y + .5f; y < sim.Setup.Max.Y; y++)
            { var p = new System.Numerics.Vector2(x,y); if (sim.PlacementError(spec,p) == null) cells.Add(p); }
        var best = cells.OrderByDescending(p => samples.Count(q => System.Numerics.Vector2.Distance(p,q) <= spec.range)).First();
        if (!b.PlaceRobot(1, new Vector3(best.X,0,best.Y))) throw new Exception("Placement rejected");
    }
}
