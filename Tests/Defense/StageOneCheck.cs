using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class StageOneCheck
{
    public static void Run()
    {
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
        void Reject(string code, int wave, int section)
        { try { StageOneCode.Validate(code, wave, section); } catch (FormatException) { return; } throw new Exception("Accepted invalid goal: " + code); }
        StageOneCode.Validate("damage=20.0", 1, 3);
        StageOneCode.Validate("range = 8", 2, 2);
        StageOneCode.Validate("damage=17\nrange=6", 2, 3);
        StageOneCode.Validate("if enemyDistance<=10.0:\n  Attack()", 3, 3);
        StageOneCode.Validate("if enemyDistance<=9:\n  Attack()", 4, 2);
        StageOneCode.Validate("attackDistance=15\nif enemyDistance<=attackDistance:\n  Attack()", 5, 4);
        Reject("damage=10", 1, 3); Reject("range=8", 2, 3);
        Reject("if enemyDistance<=10:\nAttack()", 3, 3);
        Reject("Attack()\nif enemyDistance<=10:\n  Attack()", 3, 3);
        Reject("if enemyDistance<=attackDistance:\n  Attack()", 5, 4);
        Reject("damage=101", 1, 3); Reject("damage=0", 1, 3);
        var profiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role=r, range=16, damage=10, interval=.1f }).ToArray();
        var sim = new DefenseSimulation(new BattleSetup { StageOneLearning=true, FunctionRepeatLimit=1000,
            Min=new Vector2(-30,-30), Max=new Vector2(30,30), ActionProfiles=profiles,
            Routes=new[]{new Route(new[]{new Vector2(-20,0),new Vector2(20,0)},1)},
            Waves=new[]{new[]{new SpawnGroup { Enemy=new EnemySpec { health=1000 },Count=1,Delay=100,Interval=1 }}} });
        var robot=sim.Place(profiles[(int)RobotRole.Shooter],4,new Vector2(0,3),out _);
        sim.ApplyProgram(robot.Id,"tutorial",StageOneCode.Parse("damage=20\nrange=16\nattackDistance=10\nif enemyDistance<=attackDistance:\n  Attack()"));
        sim.Start();
        var enemy=new EnemyState { Id=999, Active=true, Spec=new EnemySpec { health=1000 },Health=1000,Position=new Vector2(15,3) };
        sim.Enemies.Add(enemy);
        robot.Program.Tick(sim,robot);
        Assert(enemy.Health==1000 && robot.Program.Fault==null,"False condition waits for distant enemy");
        enemy.Position=new Vector2(10,3); robot.NextAction=0;
        robot.Program.Tick(sim,robot);
        Assert(enemy.Health==980 && robot.Range==16,"Inclusive distance and damage variable affect actual hit");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CodingGame-StageOne-generated.py"),robot.Program.Source);
        Assert(robot.Program.Source.Contains("enemy.get_distance("),"Python uses the project's actual distance API");
        robot.Program.Tick(sim,robot);
        enemy.Position=new Vector2(9,3); robot.NextAction=100;
        robot.Program.Tick(sim,robot);
        enemy.Position=new Vector2(11,3); robot.NextAction=0;
        robot.Program.Tick(sim,robot);
        Assert(enemy.Health==980,"Enemy leaving threshold during cooldown must not receive a stale-condition hit");
        var previous=robot.Program;
        try
        {
            sim.ApplyProgram(robot.Id,"invalid",new[]{new CodeBlock(BlockKind.DeclareVariable,"damage",new CodeBlock(BlockKind.Number,"50")),
                new CodeBlock(BlockKind.DeclareVariable,"range",new CodeBlock(BlockKind.Number,"1000"))});
            throw new Exception("Invalid learning stats accepted");
        }
        catch(FormatException) { }
        Assert(robot.Program==previous && robot.DamageSetting==20 && robot.RangeSetting==16,"Rejected stats preserve code and both stats atomically");
        Console.WriteLine("Stage 1 code checks passed: goals, syntax, equivalent inputs, variable stats, false/true and inclusive boundary");
    }
}
