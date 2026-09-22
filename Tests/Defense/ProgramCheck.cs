using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class ProgramCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static CodeBlock N(string n) => new CodeBlock(BlockKind.Number,n);
    static DefenseSimulation Make()
    {
        return new DefenseSimulation(new BattleSetup {
            Min=new Vector2(-20,-10),Max=new Vector2(20,10),
            ActionProfiles=Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r=>new RobotSpec{role=r,damage=20,interval=.1f,range=6}).ToArray(),
            Routes=new[]{new Route(new[]{new Vector2(-10,0),new Vector2(15,0)},1)},
            Waves=new[]{new[]{new SpawnGroup{Enemy=new EnemySpec{health=10000,speed=.01f},Count=1,Interval=1}}}
        });
    }
    static RobotState Bot(DefenseSimulation sim, RobotRole role=RobotRole.Warrior,float x=-9)
        => sim.Place(new RobotSpec{role=role,range=6,damage=role==RobotRole.Buffer?0:20,interval=.1f},(int)role,new Vector2(x,role==RobotRole.Tank?0:2),out _);
    static void Tick(DefenseSimulation sim,int frames) { for(int i=0;i<frames;i++)sim.Advance(1.0/60); }
    public static void Run()
    {
        foreach(RobotRole role in Enum.GetValues(typeof(RobotRole)))
            foreach(RobotAction action in Enum.GetValues(typeof(RobotAction)))
                if(action!=RobotAction.Block&&action!=RobotAction.Buff)
                    Assert(Rules.Compatibility(role,action)==(Rules.NativeAction(role)==action?ActionCompatibility.Native:ActionCompatibility.Reduced),$"compatibility {role}/{action}");
        var sim=Make();var robot=Bot(sim,RobotRole.Buffer);robot.AutoExecute=false;sim.Start();Tick(sim,6);
        var target=sim.Enemies.Single();sim.RequestAction(robot.Id,RobotAction.Slash);
        Assert(target.Health==9998,"buffer with zero own damage uses 10 percent specialist slash damage");
        Tick(sim,6);sim.RequestAction(robot.Id,RobotAction.Slow);
        Assert(Math.Abs(target.Speed(sim.Time)/target.Spec.speed-.95)<.00001,"cross-role slow is 10 percent reduction strength, not 10 percent remaining speed");
        Assert(Rules.Compatibility(RobotRole.Warrior,RobotAction.Buff)==ActionCompatibility.Undecided,"buff exception preserved");
        Assert(Rules.Compatibility(RobotRole.Shooter,RobotAction.Block)==ActionCompatibility.Ineffective,"block prohibition preserved");

        sim=Make();robot=Bot(sim); var other=Bot(sim,RobotRole.Shooter,-7);other.AutoExecute=false;
        var attack=new CodeBlock(BlockKind.Shot);
        sim.ApplyProgram(robot.Id,"cross",new[]{attack});attack.Kind=BlockKind.Buff;
        Assert(!robot.AutoExecute&&robot.Program.Source.Contains("Attack()"),"apply freezes source and disables automatic actions");
        sim.Start();Tick(sim,6);
        Assert(robot.Executions==1&&sim.Enemies.Single().Health==9998,"code uses same ten percent combat path without duplicate native attack");
        Assert(other.Program==null&&other.Executions==0,"apply only changes selected robot");
        var copied=robot.Program.CopyBlocks();copied[0].Kind=BlockKind.Buff;
        Assert(robot.Program.CopyBlocks()[0].Kind==BlockKind.Shot,"editing copy does not mutate applied program");
        var previous=robot.Program; bool rejected=false;
        try{sim.ApplyProgram(robot.Id,"bad",new[]{new CodeBlock(BlockKind.Break)});}catch(FormatException){rejected=true;}
        Assert(rejected&&robot.Program==previous,"invalid replacement keeps previous applied code");

        sim=Make();robot=Bot(sim);
        sim.ApplyProgram(robot.Id,"wait_then_fire",new[]{new CodeBlock(BlockKind.Wait,"0",N("0.5")),new CodeBlock(BlockKind.Slash)});
        sim.Start();Tick(sim,20);Assert(robot.Executions==0,"wait blocks subsequent action");
        sim.TogglePause();var time=sim.Time;sim.Advance(10);Assert(sim.Time==time,"program wait freezes during pause");sim.TogglePause();Tick(sim,13);
        Assert(robot.Executions==1,"wait resumes on battle time");

        sim=Make();robot=Bot(sim);
        var loop=new CodeBlock(BlockKind.For,"0",N("3"));loop.Body.Add(new CodeBlock(BlockKind.Slash));
        sim.ApplyProgram(robot.Id,"loop",new[]{loop,new CodeBlock(BlockKind.Wait,"0",N("100"))});sim.Start();Tick(sim,60);
        Assert(robot.Executions==3,"for executes exactly three attacks and respects cooldown");

        sim=Make();robot=Bot(sim);
        var endless=new CodeBlock(BlockKind.While);
        sim.ApplyProgram(robot.Id,"empty_loop",new[]{endless});sim.Start();Tick(sim,2);
        Assert(robot.Program.StepsLastTick==DefenseProgram.InstructionBudget&&robot.Program.Fault==null&&sim.Time>0,"empty infinite loop bounded per tick");

        sim=Make();robot=Bot(sim);
        var condition=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.False));condition.Body.Add(new CodeBlock(BlockKind.Shot));
        var otherwise=new CodeBlock(BlockKind.Else);otherwise.Body.Add(new CodeBlock(BlockKind.Slash));
        sim.ApplyProgram(robot.Id,"branch",new[]{condition,otherwise,new CodeBlock(BlockKind.Wait,"0",N("100"))});sim.Start();Tick(sim,10);
        Assert(robot.Executions==1&&sim.Enemies.Single().Health==9980,"else branch selected");

        sim=Make();robot=Bot(sim);
        loop=new CodeBlock(BlockKind.For,"0",N("5"));var skip=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,"<",new CodeBlock(BlockKind.Variable,"i"),N("2")));skip.Body.Add(new CodeBlock(BlockKind.Continue));
        loop.Body.Add(skip);loop.Body.Add(new CodeBlock(BlockKind.Slash));loop.Body.Add(new CodeBlock(BlockKind.Break));
        sim.ApplyProgram(robot.Id,"loop_control",new[]{loop,new CodeBlock(BlockKind.Wait,"0",N("100"))});sim.Start();Tick(sim,10);
        Assert(robot.Executions==1,"continue and break preserve loop control");

        sim=Make();robot=Bot(sim);
        var declaration=new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy));
        var distance=new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"enemy"),new CodeBlock(BlockKind.PositionX),new CodeBlock(BlockKind.PositionY));
        condition=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,"<",distance,N("6")));
        condition.Body.Add(new CodeBlock(BlockKind.Attack,"0",new CodeBlock(BlockKind.Variable,"enemy")));
        sim.ApplyProgram(robot.Id,"targeted",new[]{declaration,condition,new CodeBlock(BlockKind.Wait,"0",N("100"))});sim.Start();Tick(sim,10);
        Assert(robot.Executions==1&&robot.Program.Fault==null,"enemy variables, distances, coordinates and targeted attack execute");

        sim.ApplyProgram(robot.Id,"invalid_wait",new[]{new CodeBlock(BlockKind.Wait,"0",N("-1"))});Tick(sim,1);
        Assert(robot.Program.Fault!=null&&sim.Phase==BattlePhase.Running,"runtime diagnostic stops only offending program");
        sim.RestoreAutomatic(robot.Id);Assert(robot.Program==null&&robot.AutoExecute&&robot.Action==RobotAction.Slash,"restore native automatic mode");
        var before=robot.Executions;Tick(sim,10);Assert(robot.Executions>before,"native automatic mode resumes");
        sim.RemoveRobot(robot.Id);Assert(sim.Robots.Count==0,"removing robot removes its program owner");
        Console.WriteLine($"Defense program checks passed: {checks}");
    }
}
