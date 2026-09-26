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
    static void CheckTankLifecycle()
    {
        DefenseSimulation Scenario(int count) => new DefenseSimulation(new BattleSetup {
            Min = new Vector2(-50,-50), Max = new Vector2(50,50),
            ActionProfiles = Make().Setup.ActionProfiles,
            Routes = new[]{new Route(new[]{new Vector2(2,0),new Vector2(-20,0)},1.2f)},
            Waves = new[]{new[]{new SpawnGroup{Enemy=new EnemySpec{health=100,speed=120,damage=0},Count=count,Interval=.1f}}}
        });
        RobotState Tank(DefenseSimulation simulation, float x, int capacity)
        {
            var bot = simulation.Place(new RobotSpec{role=RobotRole.Tank,interval=3,blockRadius=1.25f,blockCapacity=capacity},2,new Vector2(x,0),out _);
            simulation.ApplyProgram(bot.Id,"guard",new[]{new CodeBlock(BlockKind.Block)});
            return bot;
        }

        var sim = Scenario(4); var tank = Tank(sim,0,3);
        sim.Start(); Tick(sim,30);
        Assert(sim.Enemies.Count(e=>e.BlockedBy==tank.Id)==3&&sim.Leaks==1,"capacity holds three and lets overflow pass even at high speed");
        Assert(sim.Enemies.All(e=>Math.Abs(e.Position.X-1.25f)<.001f),"swept interception stops fast enemies at front edge");
        sim.ApplyProgram(tank.Id,"empty",Array.Empty<CodeBlock>());
        Assert(!tank.BlockingEnabled&&sim.Enemies.All(e=>e.BlockedBy==0),"clearing code immediately releases held enemies");
        Tick(sim,1);
        Assert(sim.Enemies.All(e=>e.Position.X<0),"released enemies resume moving");
        sim.ApplyProgram(tank.Id,"guard",new[]{new CodeBlock(BlockKind.Block)}); Tick(sim,1);
        Assert(sim.Enemies.All(e=>e.BlockedBy==tank.Id),"reaching block again catches enemies still inside radius");
        Assert(sim.DamageRobot(tank.Id,tank.Spec.health)&&sim.Robots.Count==0&&sim.Enemies.All(e=>e.BlockedBy==0),"destroying tank releases all occupied slots");
        Tick(sim,15);
        Assert(sim.Leaks==4&&sim.Phase==BattlePhase.Victory,"released enemies reach base without stale tank references");

        sim = Scenario(3);
        // Reverse placement order: physical entry distance, not robot list order, decides interception.
        var back = Tank(sim,-4,1); var front = Tank(sim,0,1);
        sim.Start(); Tick(sim,30);
        Assert(sim.Enemies.Single(e=>e.BlockedBy==front.Id).Id<sim.Enemies.Single(e=>e.BlockedBy==back.Id).Id,
            "first contact takes nearest tank and overflow reaches second tank");
        Assert(sim.Leaks==1&&sim.Enemies.Count==2,"multiple tanks enforce separate capacities");

        sim = Scenario(1); tank = Tank(sim,0,3);
        var never = new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.False));
        never.Body.Add(new CodeBlock(BlockKind.Block));
        sim.ApplyProgram(tank.Id,"conditional",new[]{never}); sim.Start(); Tick(sim,15);
        Assert(!tank.BlockingEnabled&&sim.Leaks==1,"block inside false branch never enables stance");
    }
    public static void Run()
    {
        CheckTankLifecycle();
        // Same close spawn and cooldown as the authored tank preview.
        var tankSetup = new BattleSetup {
            Min = new Vector2(-50,-50), Max = new Vector2(50,50),
            ActionProfiles = Make().Setup.ActionProfiles,
            Routes = new[]{new Route(new[]{new Vector2(2,0),new Vector2(-20,0)},1.2f)},
            Waves = new[]{new[]{new SpawnGroup{Enemy=new EnemySpec{health=100,speed=1.5f,damage=0},Count=3,Interval=.7f}}}
        };
        var blocking = new DefenseSimulation(tankSetup);
        var tank = blocking.Place(new RobotSpec{role=RobotRole.Tank,range=2.3f,interval=3,damage=2,duration=1.1f,blockRadius=1.25f,blockCapacity=3},2,Vector2.Zero,out _);
        blocking.ApplyProgram(tank.Id,"guard",new[]{new CodeBlock(BlockKind.Block)});
        Assert(!tank.BlockingEnabled,"adding code alone does not activate blocking");
        blocking.Start(); Tick(blocking,1);
        Assert(tank.BlockingEnabled&&tank.Executions==0,"block stance starts before first attack cooldown");
        Tick(blocking,149);
        Assert(blocking.Enemies.Count==3&&blocking.Enemies.All(e=>e.BlockedBy==tank.Id&&e.Position.X>0),"all preview enemies stop in front of tank");
        Assert(blocking.Enemies.All(e=>e.Health==100&&!e.Stunned(blocking.Time)),"stance does not bypass stun or damage cooldown");
        Tick(blocking,30);
        Assert(tank.Executions==1&&blocking.Enemies.All(e=>e.Stunned(blocking.Time)),"stun executes after original cooldown");
        Tick(blocking,75);
        Assert(blocking.Enemies.All(e=>e.BlockedBy==tank.Id&&!e.Stunned(blocking.Time)),"body blocking persists after stun expires");
        var delayed = new DefenseSimulation(tankSetup);
        var delayedTank = delayed.Place(tank.Spec.Copy(),2,Vector2.Zero,out _);
        delayed.ApplyProgram(delayedTank.Id,"delayed",new[]{new CodeBlock(BlockKind.Wait,"0",N("1")),new CodeBlock(BlockKind.Block)});
        delayed.Start();Tick(delayed,30);
        Assert(!delayedTank.BlockingEnabled,"block after wait does not activate before execution reaches it");
        Assert(delayedTank.Program.WaitRemaining(delayed.Time)>.5&&delayedTank.Program.WaitRemaining(delayed.Time)<.6,"preview wait duration uses simulation time");
        delayed.TogglePause(); var waitRemaining = delayedTank.Program.WaitRemaining(delayed.Time); delayed.Advance(2);
        Assert(delayedTank.Program.WaitRemaining(delayed.Time)==waitRemaining,"pause preserves displayed wait duration");
        delayed.TogglePause();Tick(delayed,32);
        Assert(delayedTank.Program.WaitRemaining(delayed.Time)==0&&delayedTank.BlockingEnabled,"wait completion enables blocking and clears wait display");
        Assert(!delayedTank.Program.IsEmpty&&new DefenseProgram("empty",Array.Empty<CodeBlock>()).IsEmpty,"empty program detection needs no block copies");
        var empty = new DefenseSimulation(tankSetup);
        var emptyTank = empty.Place(tank.Spec.Copy(),2,Vector2.Zero,out _);
        empty.Start();Tick(empty,120);
        Assert(!emptyTank.BlockingEnabled&&empty.Enemies.Any(e=>e.Position.X<0),"empty tank still cannot block");
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
