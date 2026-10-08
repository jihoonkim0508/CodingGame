using System;
using System.Linq;
using System.Reflection;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using UnityEngine;

// Run via Unity MCP run_script in a fresh Defense Play Mode session.
public static class StageOneFlowCheck
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static string Answer(int wave, int section) => wave == 1 ? "damage=20\nAttack()" : wave == 2 ? section == 2 ? "damage=20\nrange=8\nAttack()" : "damage=15\nrange=7\nAttack()" :
        wave == 3 ? "damage=20\nrange=16\nif enemyDistance<=10:\n    Attack()" : wave == 4 ? section == 2 ? "damage=20\nrange=16\nif enemyDistance<=6:\n    Attack()" : "damage=20\nrange=16\nif enemyDistance<=8:\n    Attack()" :
        section == 4 ? "damage=20\nrange=16\nattackDistance=15\nif enemyDistance<=attackDistance:\n    Attack()" : "damage=20\nrange=16\nattackDistance=12\nif enemyDistance<=attackDistance:\n    Attack()";

    public static string Failure()
    {
        var battle = GameObject.Find("DefenseBattle").GetComponent<DefenseBattle>();
        var tutorial = GameObject.Find("TutorialSystem").GetComponent<StageOneTutorial>();
        object Get(string key) => typeof(StageOneTutorial).GetField(key, Flags).GetValue(tutorial);
        void Tick() => typeof(StageOneTutorial).GetMethod("Update", Flags).Invoke(tutorial, null);
        int warrior = Enumerable.Range(0, 6).Single(i => battle.DefinitionRole(i) == RobotRole.Warrior);
        Tick(); typeof(StageOneTutorial).GetField("bootUntil", Flags).SetValue(tutorial, 0f);
        for (int i = 0; i < 80; i++)
        {
            Tick();
            int section = (int)Get("section"); bool waiting = (bool)Get("waiting");
            if (waiting && section == 1) battle.PlaceRobot(warrior, new Vector3(-4, 0, 1));
            if (waiting && section == 3) { battle.EditorCode.Panel.LoadProgram("t", StageOneCode.Parse(Answer(1, 3), false, BlockKind.Slash)); battle.EditorCode.Panel.Apply(); }
            if (waiting && section == 4) break;
            battle.Tutorial.EditorView.FinishTyping(); tutorial.Next();
        }
        var sim = battle.Simulation;
        foreach (var group in sim.UpcomingWave) group.Enemy.speed = 1000;
        battle.StartBattle(); for (int i = 0; i < 600 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60);
        Tick();
        if (sim.Phase != BattlePhase.Reward || sim.Leaks == 0 || !(bool)Get("waiting")) throw new Exception("Leak must not release the clear gate");
        battle.ClaimRewards(); if (sim.WaveIndex != 0) throw new Exception("Reward button skipped failed teaching wave");
        battle.Restart(); Tick();
        if (battle.Simulation == sim || battle.Simulation.Robots.Count != 0 || !tutorial.Active || (int)Get("section") != 0)
            throw new Exception("Restart must reset gameplay and teaching progress together");
        return "PASS: actual leak blocks clear/reward progression; restart resets robots and tutorial";
    }

    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        var battle = GameObject.Find("DefenseBattle").GetComponent<DefenseBattle>();
        var tutorial = GameObject.Find("TutorialSystem").GetComponent<StageOneTutorial>();
        var bridge = UnityEngine.Object.FindFirstObjectByType<TutorialInputBridge>();
        object Get(string name) => typeof(StageOneTutorial).GetField(name, Flags).GetValue(tutorial);
        void Tick() => typeof(StageOneTutorial).GetMethod("Update", Flags).Invoke(tutorial, null);
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
        var editor = battle.EditorCode;
        int warrior = Enumerable.Range(0, 6).Single(i => battle.DefinitionRole(i) == RobotRole.Warrior);
        Assert(battle.Simulation.Robots.Count == 0, "Run in a fresh Play session");
        Assert(battle.Simulation.RobotUnlocked(RobotRole.Warrior) && !battle.Simulation.RobotUnlocked(RobotRole.Shooter), "Stage 1 starts with the warrior only");
        Tick();
        typeof(StageOneTutorial).GetField("bootUntil", Flags).SetValue(tutorial, 0f); Tick();
        int submitted = 0, cleared = 0, guided = 0;
        for (int guard = 0; guard < 500 && battle.Stage == 1; guard++)
        {
            Tick();
            var sim = battle.Simulation;
            if (!tutorial.Active)
            {
                if (sim.Phase == BattlePhase.Reward)
                {
                    Assert(sim.LastSurvivors == 0 && sim.Leaks == 0, "A leak or timeout cannot count as tutorial clear");
                    battle.ClaimRewards(); cleared++; Tick();
                }
                else if (sim.Phase == BattlePhase.Victory) battle.ContinueCampaign();
                continue;
            }
            int wave = (int)Get("wave"), section = (int)Get("section");
            var step = (TutorialStep)Get("step");
            if ((bool)Get("waiting"))
            {
                Assert(step != null && step.blockInput && step.highlightKeys.Length == 1, $"Locked input and one outlined target while waiting at {wave}/{section}");
                guided++;
                if (wave == 1 && section == 1)
                {
                    Assert(step.highlightKeys[0] == "Battle.Robot." + warrior, "Placement first points at the warrior button");
                    battle.ChooseRobot(warrior); Tick();
                    Assert(step.highlightKeys[0] == "Battle.DeploymentArea", "Then at the field");
                    Assert(!battle.PlaceRobot(warrior, new Vector3(-6, 0, 4)), "Path placement must fail");
                    Assert(battle.PlaceRobot(warrior, new Vector3(-4, 0, 1)), "Allowed placement must succeed"); Tick();
                }
                else if ((bool)typeof(StageOneTutorial).GetProperty("IsCodeSection", Flags).GetValue(tutorial))
                {
                    Assert(editor.IsOpen && editor.EditingId == sim.Robots[0].Id, $"Code lesson {wave}/{section} happens in the warrior's block editor");
                    Assert(battle.Tutorial.AllowPreview, "Preview runs while editing");
                    Assert(sim.Robots[0].Program != null && !sim.Robots[0].Program.IsEmpty, "Starter blocks are loaded");
                    editor.Panel.LoadProgram("warrior", StageOneCode.Parse("damage=3\nAttack()", false, BlockKind.Slash)); editor.Panel.Apply(); Tick();
                    Assert((bool)Get("waiting"), $"Off-goal blocks must not advance {wave}/{section}");
                    editor.Panel.LoadProgram("warrior", StageOneCode.Parse(Answer(wave, section), false, BlockKind.Slash)); editor.Panel.Apply(); Tick();
                    Assert(!(bool)Get("waiting"), $"Goal blocks rejected at {wave}/{section}");
                    submitted++;
                }
                else
                {
                    Assert(!editor.IsOpen, "Waves are fought on the map");
                    Assert(step.highlightKeys[0] == "Battle.Start", "Battle step points at start");
                    battle.StartBattle(); Assert(sim.Phase == BattlePhase.Running, "Battle must start after teaching input"); Tick();
                    var running = (TutorialStep)Get("step");
                    Assert(running != null && running.blockInput && !running.outline && running.targetKeys.Contains("Battle.Start"), "Running wave: input still limited, no boxes over the map");
                    for (int i = 0; i < 9000 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60);
                    Tick();
                    Assert(sim.Phase == BattlePhase.Reward && sim.LastSurvivors == 0 && sim.Leaks == 0,
                        $"Wave {wave}: phase={sim.Phase}, kills={sim.Kills}, leaks={sim.Leaks}, survivors={sim.LastSurvivors}");
                    Assert(!(bool)Get("waiting"), "Actual wave clear must release teaching gate");
                }
            }
            else
            {
                Assert(step != null && step.blockInput && step.targetKeys.Length == 0, $"Dialogue {wave}/{section}: game input locked, only the dialogue answers");
                Assert(!battle.TutorialAllows("Battle.Start") && !battle.TutorialAllows("Battle.Develop"), "Dialogue sections refuse game actions");
                battle.Tutorial.EditorView.FinishTyping(); tutorial.Next();
            }
        }
        Assert(cleared == 5 && submitted == 8 && battle.Stage == 2, $"Flow incomplete: clears={cleared}, inputs={submitted}, stage={battle.Stage}");
        Assert(battle.Simulation.LoopsUnlocked && !battle.Simulation.Setup.StageOneLearning, "Stage 2 unlocks loops and ends Stage 1 rules");
        Assert(battle.Simulation.RobotUnlocked(RobotRole.Shooter), "Shooters arrive in Stage 2");
        return $"PASS: 28 sections, warrior placement, 8 block-editor lessons (off-goal rejected), {guided} single-target guides, 5 real waves, boss, Stage 2 loops and shooters";
    }
}
