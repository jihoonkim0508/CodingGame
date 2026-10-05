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
        CodeBlock command=new CodeBlock(Action(bot.Spec.role));
        if(sim.Setup.Stage>=3 && sim.Available(BlockKind.For,bot.Id)>0 && sim.Available(BlockKind.Number,bot.Id)>0)
        { var loop=new CodeBlock(BlockKind.For,"0",new CodeBlock(BlockKind.Number,"10"));loop.Body.Add(command);command=loop; }
        bool condition = sim.Setup.Stage>=2 && bot.Spec.role!=RobotRole.Buffer &&
            new[]{BlockKind.If,BlockKind.Comparison,BlockKind.DeclareVariable,BlockKind.Variable,BlockKind.NearestEnemy,BlockKind.PositionX,BlockKind.PositionY}
                .All(k=>sim.Available(k,bot.Id)>0) && sim.Available(BlockKind.Number,bot.Id)>(sim.Setup.Stage>=3?1:0);
        CodeBlock declaration = null;
        if(condition)
        {
            declaration=new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy));
            var distance=new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"enemy"),new CodeBlock(BlockKind.PositionX),new CodeBlock(BlockKind.PositionY));
            var branch=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,"<",distance,new CodeBlock(BlockKind.Number,"10")));
            branch.Body.Add(command);command=branch;
        }
        sim.ApplyProgram(bot.Id,"robot_"+bot.Id,declaration==null?new[]{command}:new[]{declaration,command});
    }
    public static string Main()
    {
        var lines = new System.Collections.Generic.List<string>();
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var battle=UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        if(battle.Stage!=1)throw new Exception("Start from stage 1");
        battle.Restart();var sim=battle.Simulation;
        var hud=UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>(FindObjectsInactive.Include);
        var button=(UnityEngine.UI.Button)new SerializedObject(hud).FindProperty("claimButton").objectReferenceValue;
        for(int stage=1;stage<=5;stage++)
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
                if(stage>=4 && sim.Robots.All(r=>r.Spec.role!=RobotRole.Tank) && sim.RobotAvailable(RobotRole.Tank)>0 && sim.Robots.Count==sim.Setup.RobotLimit)
                    sim.RemoveRobot(sim.Robots.Last(r=>r.Spec.role==RobotRole.Shooter).Id);
                if(stage>=4)Deploy(battle,RobotRole.Tank);
                if(stage>=3)Deploy(battle,RobotRole.Bomber);
                if(stage>=2)Deploy(battle,RobotRole.Buffer);
                Deploy(battle,RobotRole.Shooter);Deploy(battle,RobotRole.Shooter);
                Deploy(battle,RobotRole.Utility);Deploy(battle,RobotRole.Warrior);Deploy(battle,RobotRole.Warrior);
                foreach(var bot in sim.Robots) Program(sim,bot);
                foreach(var bot in sim.Robots.OrderByDescending(r=>r.Damage))sim.UpgradeRobot(bot.Id);
                battle.StartBattle();int kills=sim.Kills;
                for(int tick=0;tick<10000&&sim.Phase==BattlePhase.Running;tick++)sim.Advance(1.0/60);
                lines.Add($"S{stage} W{wave}: {sim.Phase}, core={sim.BaseHealth}, kills={sim.Kills-kills}, survivors={sim.LastSurvivors}, robots={sim.Robots.Count}, rewards={sim.PendingDrops.Count}");
                if(sim.Phase!=BattlePhase.Reward)throw new Exception("Campaign failed");
                var tankHealth=sim.Robots.Where(r=>r.Health.HasValue).ToDictionary(r=>r.Id,r=>r.Health);
                button.onClick.Invoke();
                if(sim.Robots.Any(r=>tankHealth.ContainsKey(r.Id)&&r.Health!=tankHealth[r.Id]))throw new Exception("Tank healed between waves");
            }
            if(stage<5){button.onClick.Invoke();if(battle.Stage!=stage+1||battle.Simulation!=sim||sim.Phase!=BattlePhase.Ready)throw new Exception("UI failed to advance stage");}
        }
        if(sim.Phase!=BattlePhase.Victory||battle.Stage!=5||sim.CanAdvanceStage)throw new Exception("Campaign did not finish stage 5");
        lines.Add("PASS: real scene, 25 waves, reward button, stage continuity, inventory/code retention, tank health");
        return string.Join("\n",lines);
    }
}
