using System;
using System.Linq;
using System.Reflection;
using CodingGame.Defense;
using TMPro;
using UnityEngine;

public static class StageOneFlowCheck
{
    public static string Failure()
    {
        var battle=GameObject.Find("DefenseBattle").GetComponent<DefenseBattle>();
        var tutorial=GameObject.Find("TutorialSystem").GetComponent<StageOneTutorial>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var update=typeof(StageOneTutorial).GetMethod("Update",flags);
        object Get(string key)=>typeof(StageOneTutorial).GetField(key,flags).GetValue(tutorial);
        update.Invoke(tutorial,null);typeof(StageOneTutorial).GetField("bootUntil",flags).SetValue(tutorial,0f);
        for(int i=0;i<80;i++)
        {
            update.Invoke(tutorial,null);
            int section=(int)Get("section");bool waiting=(bool)Get("waiting");
            if(waiting && section==1) battle.PlaceRobot(4,new Vector3(-2,0,4));
            if(waiting && section==3) { ((TMP_InputField)Get("codeInput")).text="damage=20";tutorial.Submit(); }
            if(waiting && section==4) break;
            battle.Tutorial.EditorView.FinishTyping();tutorial.Next();
        }
        var sim=battle.Simulation;
        foreach(var group in sim.UpcomingWave) group.Enemy.speed=1000;
        battle.StartBattle();for(int i=0;i<600 && sim.Phase==BattlePhase.Running;i++)sim.Advance(1.0/60);
        update.Invoke(tutorial,null);
        if(sim.Phase!=BattlePhase.Reward || sim.Leaks==0 || !(bool)Get("waiting"))throw new Exception("Leak must not release the clear gate");
        battle.ClaimRewards();if(sim.WaveIndex!=0)throw new Exception("Reward button skipped failed teaching wave");
        battle.Restart();update.Invoke(tutorial,null);
        if(battle.Simulation==sim || battle.Simulation.Robots.Count!=0 || !tutorial.Active || (int)Get("section")!=0)
            throw new Exception("Restart must reset gameplay and teaching progress together");
        return "PASS: actual leak blocks clear/reward progression; restart resets robots and tutorial";
    }
    // Run via Unity MCP run_script in a fresh Defense Play Mode session.
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        var battle = GameObject.Find("DefenseBattle").GetComponent<DefenseBattle>();
        var tutorial = GameObject.Find("TutorialSystem").GetComponent<StageOneTutorial>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(StageOneTutorial).GetField(name, flags).GetValue(tutorial);
        void Tick() => typeof(StageOneTutorial).GetMethod("Update", flags).Invoke(tutorial, null);
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
        string Answer(int wave, int section) => wave == 1 ? "damage=20" : wave == 2 ? section == 2 ? "range=8" : "damage=15\nrange=7" :
            wave == 3 ? "if enemyDistance<=10:\n    Attack()" : wave == 4 ? "if enemyDistance<=8:\n    Attack()" :
            "attackDistance=15\nif enemyDistance<=attackDistance:\n    Attack()";
        Assert(battle.Simulation.Robots.Count == 0, "Run in a fresh Play session");
        Tick();
        typeof(StageOneTutorial).GetField("bootUntil",flags).SetValue(tutorial,0f); Tick();
        int submitted = 0, cleared = 0;
        for (int guard=0; guard<500 && battle.Stage==1; guard++)
        {
            Tick();
            var sim=battle.Simulation;
            if (!tutorial.Active)
            {
                if(sim.Phase==BattlePhase.Reward)
                {
                    Assert(sim.LastSurvivors==0 && sim.Leaks==0, "A leak or timeout cannot count as tutorial clear");
                    battle.ClaimRewards(); cleared++; Tick();
                }
                else if(sim.Phase==BattlePhase.Victory) battle.ContinueCampaign();
                continue;
            }
            int wave=(int)Get("wave"), section=(int)Get("section");
            if((bool)Get("waiting"))
            {
                if(wave==1 && section==1)
                {
                    Assert(!battle.PlaceRobot(4,new Vector3(-6,0,4)), "Path placement must fail");
                    Assert(battle.PlaceRobot(4,new Vector3(-2,0,4)), "Allowed placement must succeed"); Tick();
                }
                else if((bool)typeof(StageOneTutorial).GetProperty("IsCodeSection",flags).GetValue(tutorial))
                {
                    var input=(TMP_InputField)Get("codeInput");
                    input.text="invalid()"; tutorial.Submit();
                    Assert((bool)Get("waiting"), "Invalid input must not advance");
                    if(wave==3)
                    {
                        battle.SelectRobot(sim.Robots[0].Id); battle.DevelopSelected(); Tick();
                        Assert(battle.EditorCode.IsOpen,"Existing block editor must be reachable");
                        battle.EditorCode.Panel.LoadProgram("tutorial",StageOneCode.Parse(Answer(wave,section)));
                        battle.EditorCode.Panel.Apply(); Tick(); battle.EditorCode.Close(false);
                    }
                    else { input.text=Answer(wave,section); tutorial.Submit(); }
                    Assert(!(bool)Get("waiting"), $"Valid input rejected at {wave}/{section}: {((TMP_Text)Get("feedback")).text}");
                    submitted++;
                }
                else
                {
                    battle.StartBattle(); Assert(sim.Phase==BattlePhase.Running, "Battle must start after teaching input");
                    for(int i=0;i<9000 && sim.Phase==BattlePhase.Running;i++) sim.Advance(1.0/60);
                    Tick();
                    Assert(sim.Phase==BattlePhase.Reward && sim.LastSurvivors==0 && sim.Leaks==0,
                        $"Wave {wave}: phase={sim.Phase}, kills={sim.Kills}, leaks={sim.Leaks}, survivors={sim.LastSurvivors}");
                    Assert(!(bool)Get("waiting"), "Actual wave clear must release teaching gate");
                }
            }
            else { battle.Tutorial.EditorView.FinishTyping(); tutorial.Next(); }
        }
        Assert(cleared==5 && submitted==8 && battle.Stage==2, $"Flow incomplete: clears={cleared}, inputs={submitted}, stage={battle.Stage}");
        Assert(battle.Simulation.LoopsUnlocked && !battle.Simulation.Setup.StageOneLearning, "Stage 2 unlocks loops and ends Stage 1 rules");
        return "PASS: 28 sections, 8 valid/invalid inputs, path/allowed placement, 5 actual waves, boss and Stage 2 loop unlock";
    }
}
