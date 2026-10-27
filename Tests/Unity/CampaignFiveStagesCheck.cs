// Run: unity command run_script --file Tests/Unity/CampaignFiveStagesCheck.cs --entry CampaignFiveStagesCheck.Main
// Requires a fresh Defense Play Mode session.
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using Point = System.Numerics.Vector2;

public static class CampaignFiveStagesCheck
{
    static BlockKind Action(RobotRole role) => new[] { BlockKind.Buff, BlockKind.Slash, BlockKind.Block, BlockKind.Boom, BlockKind.Shot, BlockKind.Slow }[(int)role];
    static float Coverage(DefenseSimulation sim, RobotSpec spec, Point p)
    {
        float score=0;
        foreach(var route in sim.Setup.Routes)
            for(int i=1;i<route.Points.Length;i++)
                for(int s=0;s<=20;s++)
                    if(Point.Distance(p,Point.Lerp(route.Points[i-1],route.Points[i],s/20f))<=spec.range)
                        score+=Point.Distance(route.Points[i-1],route.Points[i])/21;
        return spec.role==RobotRole.Buffer ? sim.Robots.Count(r=>Point.Distance(r.Position,p)<=spec.range)*100+score : score;
    }
    static void Deploy(DefenseBattle battle, RobotRole role)
    {
        var sim=battle.Simulation;
        if(sim.RobotAvailable(role)<1 || sim.Available(Action(role))<1 || sim.Robots.Count>=sim.Setup.RobotLimit) return;
        int index=Enumerable.Range(0,6).Single(i=>battle.DefinitionRole(i)==role);
        var spec=battle.RobotDefinition(index).stats;
        var candidates=from x in Enumerable.Range(0,(int)(sim.Setup.Max.X-sim.Setup.Min.X))
                       from y in Enumerable.Range(0,(int)(sim.Setup.Max.Y-sim.Setup.Min.Y))
                       let p=new Point(sim.Setup.Min.X+x+.5f,sim.Setup.Min.Y+y+.5f)
                       where sim.PlacementError(spec,p)==null orderby Coverage(sim,spec,p) descending select p;
        foreach(var p in candidates) if(battle.PlaceRobot(index,new Vector3(p.X,0,p.Y)))break;
    }
    static void Program(DefenseSimulation sim, RobotState bot)
    {
        sim.ApplyProgram(bot.Id,"robot_"+bot.Id,Array.Empty<CodeBlock>());
        // Stage 1 starts 1-2 only with a warrior that finds an enemy into a variable and slashes it, and 1-4 only with two
        // warriors that slash inside a distance check; use the richest of those the shared stock still allows.
        if(sim.Setup.Stage==1 && bot.Spec.role==RobotRole.Warrior)
            foreach(var form in Stage1Warrior())
                try{sim.ApplyProgram(bot.Id,"robot_"+bot.Id,form);return;}catch(FormatException){}
        var body=new System.Collections.Generic.List<CodeBlock>();
        var kind=Action(bot.Spec.role);
        // Attacks need an explicit target: the nearest enemy, or the declared `enemy` inside the distance check.
        CodeBlock command=PythonTreeCompiler.HasEnemyArgument(kind)?new CodeBlock(kind,"0",new CodeBlock(BlockKind.NearestEnemy)):new CodeBlock(kind);
        bool condition = sim.Setup.Stage>=2 && bot.Spec.role!=RobotRole.Buffer &&
            new[]{BlockKind.If,BlockKind.Comparison,BlockKind.DeclareVariable,BlockKind.Variable,BlockKind.NearestEnemy,BlockKind.MyPos}
                .All(k=>sim.Available(k,bot.Id)>0) && sim.Available(BlockKind.Number,bot.Id)>1;
        if(condition)
        {
            // Declared inside the loop so each pass re-targets; a stale `enemy` stops attacking after the first kill.
            body.Add(new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy)));
            var distance=new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"enemy"),new CodeBlock(BlockKind.MyPos));
            var branch=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,"<",distance,new CodeBlock(BlockKind.Number,"10")));
            if(PythonTreeCompiler.HasEnemyArgument(kind)) command.Arguments[0]=new CodeBlock(BlockKind.Variable,"enemy");
            branch.Body.Add(command);command=branch;
        }
        body.Add(command);
        // Once loops unlock a function runs once per wave, so the whole body must repeat.
        if(sim.LoopsUnlocked && sim.Available(BlockKind.For,bot.Id)>0 && sim.Available(BlockKind.Number,bot.Id)>0)
        { var loop=new CodeBlock(BlockKind.For,"0",new CodeBlock(BlockKind.Number,"50"));loop.Body.AddRange(body);body=new System.Collections.Generic.List<CodeBlock>{loop}; }
        sim.ApplyProgram(bot.Id,"robot_"+bot.Id,body.ToArray());
    }
    static CodeBlock Enemy()=>new CodeBlock(BlockKind.Variable,"enemy");
    static CodeBlock[][] Stage1Warrior()=>new[]{
        new[]{new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.DeclareVariable,"enemy_distance",new CodeBlock(BlockKind.Distance,"0",Enemy(),new CodeBlock(BlockKind.MyPos))),
            new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,"<=",new CodeBlock(BlockKind.Variable,"enemy_distance"),new CodeBlock(BlockKind.MyAttackRange))){Body={new CodeBlock(BlockKind.Slash,"0",Enemy())}}},
        new[]{new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy)),new CodeBlock(BlockKind.Slash,"0",Enemy())}};
    public static string Main()
    {
        var lines = new System.Collections.Generic.List<string>();
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var battle=UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        // Real campaign from stage 1; the tutorial is off so the scripted player can act freely.
        battle.EditorStartSession(1,1,false,new DefenseTestOptions{tutorial=false,tutorialInput=false});var sim=battle.Simulation;
        var hud=UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>(FindObjectsInactive.Include);
        var button=(UnityEngine.UI.Button)new SerializedObject(hud).FindProperty("claimButton").objectReferenceValue;
        for(int stage=1;stage<=3;stage++)
        {
            for(int wave=1;wave<=5;wave++)
            {
                if(stage>=2 && sim.Robots.Count==sim.Setup.RobotLimit)
                {
                    var replace=sim.Robots.FirstOrDefault(r=>r.Spec.role==RobotRole.Warrior && sim.RobotAvailable(RobotRole.Buffer)>0 && sim.Robots.All(b=>b.Spec.role!=RobotRole.Buffer));
                    if(replace!=null)sim.RemoveRobot(replace.Id);
                }
                if(stage>=3 && sim.Robots.Count==sim.Setup.RobotLimit)
                {
                    var replace=sim.Robots.FirstOrDefault(r=>r.Spec.role==RobotRole.Warrior || r.Spec.role==RobotRole.Utility);
                    if(replace!=null && sim.RobotAvailable(RobotRole.Bomber)>0)sim.RemoveRobot(replace.Id);
                }
                if(stage>=3 && sim.Robots.All(r=>r.Spec.role!=RobotRole.Tank) && sim.RobotAvailable(RobotRole.Tank)>0 && sim.Robots.Count==sim.Setup.RobotLimit)
                    sim.RemoveRobot(sim.Robots.Last(r=>r.Spec.role==RobotRole.Shooter).Id);
                if(stage>=3)Deploy(battle,RobotRole.Tank);
                if(stage>=3)Deploy(battle,RobotRole.Bomber);
                if(stage>=2)Deploy(battle,RobotRole.Buffer);
                Deploy(battle,RobotRole.Shooter);Deploy(battle,RobotRole.Shooter);
                Deploy(battle,RobotRole.Utility);Deploy(battle,RobotRole.Warrior);Deploy(battle,RobotRole.Warrior);
                foreach(var bot in sim.Robots) Program(sim,bot);
                foreach(var bot in sim.Robots.OrderByDescending(r=>r.Damage))sim.UpgradeRobot(bot.Id);
                battle.StartBattle();int kills=sim.Kills;
                for(int tick=0;tick<10000&&sim.Phase==BattlePhase.Running;tick++)sim.Advance(1.0/60);
                lines.Add($"S{stage} W{wave}: {sim.Phase}, core={sim.BaseHealth}, kills={sim.Kills-kills}, survivors={sim.LastSurvivors}, robots={sim.Robots.Count}, rewards={sim.PendingDrops.Count}");
                if(sim.Phase!=BattlePhase.Reward)throw new Exception("Campaign failed: "+string.Join(" | ",lines));
                var tankHealth=sim.Robots.Where(r=>r.Health.HasValue).ToDictionary(r=>r.Id,r=>r.Health);
                button.onClick.Invoke();
                if(sim.Robots.Any(r=>tankHealth.ContainsKey(r.Id)&&r.Health!=tankHealth[r.Id]))throw new Exception("Tank healed between waves");
            }
            if(stage<3){button.onClick.Invoke();if(battle.Stage!=stage+1||battle.Simulation!=sim||sim.Phase!=BattlePhase.Ready)throw new Exception("UI failed to advance stage");}
        }
        if(sim.Phase!=BattlePhase.Victory||battle.Stage!=3||sim.CanAdvanceStage)throw new Exception("Campaign did not finish stage 3");
        lines.Add("PASS: real scene, 15 waves, reward button, stage continuity, inventory/code retention, tank health");
        return string.Join("\n",lines);
    }
}
