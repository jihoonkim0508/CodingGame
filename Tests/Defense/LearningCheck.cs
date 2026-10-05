using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class LearningCheck
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var progression = new DefenseProgression();
        Require(progression.initial.Length == 0 && progression.robots.Length == 0, "No blanket starting stock");
        Require(progression.lessons.Length == 25, "25 explicit lessons");
        var profiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role=r }).ToArray();
        var setup = new BattleSetup { Stage=1, PlayerFlow=true, Progression=progression, WaveSeconds=.1f,
            Min=new Vector2(-20,-20), Max=new Vector2(20,20), ActionProfiles=profiles,
            Routes=new[]{new Route(new[]{new Vector2(-10,0),new Vector2(10,0)},1)},
            Waves=Enumerable.Range(0,5).Select(_=>new[]{new SpawnGroup { Enemy=new EnemySpec(), Count=1, Interval=1 }}).ToArray() };
        var sim = new DefenseSimulation(setup);
        Require(sim.RobotAvailable(RobotRole.Shooter)==2 && sim.Inventory[BlockKind.Shot]==2 && !sim.Inventory.ContainsKey(BlockKind.For), "Only lesson 1-1 supplies");
        var bot=sim.Place(profiles[(int)RobotRole.Shooter],4,new Vector2(0,3),out _);
        Require(bot!=null,"Starting supplies playable");
        Require(Math.Abs(sim.ActionDamage(bot,RobotAction.Attack,2)-2*sim.ActionDamage(bot,RobotAction.Attack,4))<.001f,"Distance double, damage half");
        Require(sim.ActionDamage(bot,RobotAction.Attack,0)==sim.ActionDamage(bot,RobotAction.Attack,1),"Near-distance damage capped");
        sim.ApplyProgram(bot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot)});
        RobotState tank=null;
        for(int stage=1;stage<=5;stage++)
        {
            Require(sim.Setup.Stage==stage,"Stage advances sequentially");
            if(stage==4) { tank=sim.Place(profiles[(int)RobotRole.Tank],2,new Vector2(0,0),out _); tank.Health=37; }
            for(int wave=1;wave<=5;wave++)
            {
                Require(sim.WaveIndex==wave-1 && sim.Lesson.stage==stage && sim.Lesson.wave==wave,"Correct lesson");
                var expected=sim.Lesson.rewards.SelectMany(s=>Enumerable.Repeat(s.kind,s.count)).ToArray();
                sim.Start();for(int i=0;i<20 && sim.Phase==BattlePhase.Running;i++)sim.Advance(.05);
                Require(sim.Phase==BattlePhase.Reward && sim.PendingDrops.SequenceEqual(expected),"Fixed rewards independent of kills");
                Require(sim.ClaimRewards() && !sim.ClaimRewards(),"Claim exactly once");
                if(tank!=null)Require(tank.Health==37,"Tank health survives transitions");
            }
            Require(sim.Phase==BattlePhase.Victory,"Stage clear");
            int stock=sim.Inventory[BlockKind.Shot];
            Require(sim.AdvanceStage()==(stage<5),"Campaign ends at 5");
            Require(sim.Inventory[BlockKind.Shot]>=stock && sim.Robots.Contains(bot) && bot.Program!=null,"Inventory, placement and code persist");
        }
        setup.Stage=0; setup.PlayerFlow=false;
        var combat=new DefenseSimulation(setup);
        var ranged=combat.Place(profiles[(int)RobotRole.Shooter],4,new Vector2(0,2),out _);
        combat.Start();ranged.NextAction=0;
        var target=new EnemyState { Id=999, Spec=new EnemySpec { health=1000 }, Health=1000, Position=new Vector2(0,0) };
        combat.Enemies.Add(target);
        Require(combat.RequestAction(ranged.Id,RobotAction.Attack)==ActionResult.Executed,"Near hit executes");
        float near=1000-target.Health;
        target.Health=1000;target.Position=new Vector2(0,-2);ranged.NextAction=0;
        Require(combat.RequestAction(ranged.Id,RobotAction.Attack)==ActionResult.Executed,"Far hit executes");
        Require(Math.Abs(near-2*(1000-target.Health))<.001f,"Actual hit damage halves at double distance");
        Console.WriteLine("Learning checks passed: 25-wave flow, supplies, fixed rewards, retained tank health, distance damage");
    }
}
