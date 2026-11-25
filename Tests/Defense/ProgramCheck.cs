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
        return new DefenseSimulation(new BattleSetup { Progression = FixtureProgression.Create(),
            Min=new Vector2(-20,-10),Max=new Vector2(20,10),
            ActionProfiles=Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r=>new RobotSpec{role=r,damage=20,interval=.1f,range=6}).ToArray(),
            Routes=new[]{new Route(new[]{new Vector2(-10,0),new Vector2(15,0)},1)},
            Waves=new[]{new[]{new SpawnGroup{Enemy=new EnemySpec{health=10000,speed=.01f},Count=1,Interval=1}}}
        });
    }
    static RobotState Bot(DefenseSimulation sim, RobotRole role=RobotRole.Melee,float x=-9)
        => sim.Place(new RobotSpec{role=role,range=6,damage=20,interval=.1f},(int)role,new Vector2(x,2),out _);
    static void Tick(DefenseSimulation sim,int frames) { for(int i=0;i<frames;i++)sim.Advance(1.0/60); }
    // The attack preview fights the wave's own enemies: their type and numbers (damage too), every group shown, no boss wait.
    static void CheckPreviewWave()
    {
        SpawnGroup G(EnemyType type, int definition, int count, float delay, bool boss = false) => new SpawnGroup { Enemy = new EnemySpec { type = type, health = 30 + definition, damage = 9 },
            DefinitionIndex = definition, RouteIndex = 1, Count = count, Delay = delay, Interval = .5f, Boss = boss };
        var wave = new[] { G(EnemyType.Tank, 2, 1, 0, true), G(EnemyType.Tank, 2, 2, 6), G(EnemyType.Normal, 0, 6, 0) };
        var sample = DefenseSimulation.PreviewWave(wave, 3);
        Assert(string.Join(",", sample.Select(g => g.DefinitionIndex)) == "0,2,2" && sample.All(g => g.Count == 1 && g.RouteIndex == 0 && !g.Boss),
            "preview: ordinary groups by delay, then the boss, one enemy each");
        Assert(sample[2].Enemy.type == EnemyType.Tank && sample[2].Enemy.health == 32 && sample[2].Enemy.damage == 9 && sample[2].Enemy != wave[0].Enemy,
            "preview: the wave's own numbers, copied");
        Assert(sample.Select(g => g.Delay).SequenceEqual(new[] { 0f, 1.4f, 2.8f }), "preview: 1.4 s apart");
        Assert(DefenseSimulation.PreviewWave(new[] { G(EnemyType.Fast, 1, 5, 0), G(EnemyType.Normal, 0, 4, 5) }, 1).Single().DefinitionIndex == 1, "preview: a one-enemy lesson takes the first");
        Assert(DefenseSimulation.PreviewWave(new[] { G(EnemyType.Normal, 0, 8, 0) }, 3).Length == 3, "preview: one group fills the sample");
    }
    public static void Run()
    {
        CheckPreviewWave();
        // wait() before an action runs on simulation time and stands still while paused.
        var waitSetup = new BattleSetup { Progression = FixtureProgression.Create(),
            Min = new Vector2(-50,-50), Max = new Vector2(50,50),
            ActionProfiles = Make().Setup.ActionProfiles,
            Routes = new[]{new Route(new[]{new Vector2(2,0),new Vector2(-20,0)},1.2f)},
            Waves = new[]{new[]{new SpawnGroup{Enemy=new EnemySpec{health=100,speed=1.5f,damage=0},Count=3,Interval=.7f}}}
        };
        var delayed = new DefenseSimulation(waitSetup);
        var delayedBot = delayed.Place(new RobotSpec{role=RobotRole.Melee,range=2.3f,interval=3,damage=2,duration=1.1f},1,new Vector2(0,2.5f),out _);
        delayed.ApplyProgram(delayedBot.Id,"delayed",new[]{new CodeBlock(BlockKind.Wait,"0",N("1")),new CodeBlock(BlockKind.Block,"0",new CodeBlock(BlockKind.NearestEnemy))});
        delayed.Start();Tick(delayed,30);
        Assert(delayedBot.Executions==0,"block after wait does not run before execution reaches it");
        Assert(delayedBot.Program.WaitRemaining(delayed.Time)>.5&&delayedBot.Program.WaitRemaining(delayed.Time)<.6,"preview wait duration uses simulation time");
        delayed.TogglePause(); var waitRemaining = delayedBot.Program.WaitRemaining(delayed.Time); delayed.Advance(2);
        Assert(delayedBot.Program.WaitRemaining(delayed.Time)==waitRemaining,"pause preserves displayed wait duration");
        delayed.TogglePause();Tick(delayed,32);
        Assert(delayedBot.Program.WaitRemaining(delayed.Time)==0,"wait completion clears wait display");
        Assert(!delayedBot.Program.IsEmpty&&new DefenseProgram("empty",Array.Empty<CodeBlock>()).IsEmpty,"empty program detection needs no block copies");
        var sim=Make();var robot=Bot(sim,RobotRole.Support);robot.AutoExecute=false;sim.Start();Tick(sim,6);
        var target=sim.Enemies.Single();sim.RequestAction(robot.Id,RobotAction.Slash);Tick(sim,30); // the sword gets there after the wind-up
        Assert(target.Health==9995,"the support robot's slash is 25 percent of its damage");
        Tick(sim,6);sim.RequestAction(robot.Id,RobotAction.Slow);
        Assert(Math.Abs(target.Speed(sim.Time)/target.Spec.speed-.5)<.00001,"the support robot's own slow is full strength");
        var slowing=Make();var melee=Bot(slowing,RobotRole.Melee);melee.AutoExecute=false;slowing.Start();Tick(slowing,6);
        var slowed=slowing.Enemies.Single();slowing.RequestAction(melee.Id,RobotAction.Slow);
        Assert(Math.Abs(slowed.Speed(slowing.Time)/slowed.Spec.speed-.875)<.00001,"an attack robot's slow is 25 percent reduction strength, not 25 percent remaining speed");

        sim=Make();robot=Bot(sim,RobotRole.Support); var other=Bot(sim,RobotRole.Ranged,-7);other.AutoExecute=false;
        var attack=new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy));
        sim.ApplyProgram(robot.Id,"cross",new[]{attack});attack.Kind=BlockKind.Buff;
        Assert(!robot.AutoExecute&&robot.Program.Source.Contains("shoot(get_nearest_enemy())"),"apply freezes source and disables automatic actions");
        sim.Start();Tick(sim,6);
        // The shot is in flight: it carries the support robot's 25 percent damage and lands on arrival.
        Assert(robot.Executions==1&&sim.Projectiles.Single().Damage==5&&sim.Enemies.Single().Health==10000,"code uses the same 25 percent combat path without duplicate native attack");
        Assert(other.Program==null&&other.Executions==0,"apply only changes selected robot");
        var copied=robot.Program.CopyBlocks();copied[0].Kind=BlockKind.Buff;
        Assert(robot.Program.CopyBlocks()[0].Kind==BlockKind.Shot,"editing copy does not mutate applied program");
        Assert(robot.Program.BlockCount(BlockKind.Shot)==1&&robot.Program.BlockCount(BlockKind.Attack)==1&&robot.Program.BlockCount(BlockKind.Buff)==0,
            "cached inventory counts remain canonical and isolated from source and editable copies");
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
        var condition=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.False));condition.Body.Add(new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy)));
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
        var distance=new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"enemy"),new CodeBlock(BlockKind.MyPos));
        condition=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,"<",distance,N("6")));
        condition.Body.Add(new CodeBlock(BlockKind.Attack,"0",new CodeBlock(BlockKind.Variable,"enemy")));
        sim.ApplyProgram(robot.Id,"targeted",new[]{declaration,condition,new CodeBlock(BlockKind.Wait,"0",N("100"))});sim.Start();Tick(sim,10);
        Assert(robot.Executions==1&&robot.Program.Fault==null,"enemy variables, distances, coordinates and targeted attack execute");
        // 본문과 매개변수를 모두 세되 distance()의 수신자는 변수 아이템 하나로 취급합니다.
        var used = DefenseProgression.Used(robot.Program.CopyBlocks()).ToArray();
        Assert(Enum.GetValues(typeof(BlockKind)).Cast<BlockKind>().All(kind => robot.Program.BlockCount(kind)==used.Count(k=>k==DefenseProgression.Canonical(kind))),
            "cached counts match nested bodies, parameters and canonical aliases");
        Assert(robot.Program.BlockCount(BlockKind.Variable)==2&&robot.Program.BlockCount(BlockKind.Number)==2,
            "method receiver is not charged twice and nested parameters are counted");

        var hpSim=Make();var hpBot=Bot(hpSim);
        var healthy=new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,">",new CodeBlock(BlockKind.Hp,"0",new CodeBlock(BlockKind.Variable,"enemy")),N("9999")));
        healthy.Body.Add(new CodeBlock(BlockKind.Slash));
        hpSim.ApplyProgram(hpBot.Id,"hp",new[]{new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy)),healthy,new CodeBlock(BlockKind.Wait,"0",N("100"))});hpSim.Start();Tick(hpSim,10);
        Assert(hpBot.Executions==1&&hpBot.Program.Fault==null&&hpBot.Program.Source.Contains("enemy.hp > 9999"),"enemy.hp reads the live target health");
        var rangeSim=Make();var rangeBot=Bot(rangeSim);
        // Equal to the robot's own range: true, so the attack runs. One above: false.
        CodeBlock InRange(string op)=>new CodeBlock(BlockKind.If,"0",new CodeBlock(BlockKind.Comparison,op,new CodeBlock(BlockKind.MyAttackRange),N(rangeBot.Range.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        var inRange=InRange("==");inRange.Body.Add(new CodeBlock(BlockKind.Slash));
        rangeSim.ApplyProgram(rangeBot.Id,"range",new[]{inRange,new CodeBlock(BlockKind.Wait,"0",N("100"))});rangeSim.Start();Tick(rangeSim,10);
        Assert(rangeBot.Executions==1&&rangeBot.Program.Fault==null&&rangeBot.Program.Source.Contains("my_attack_range() == ")&&rangeSim.Enemies.Single().Health<10000,"my_attack_range() reads the robot's range");
        // != is the opposite: equal to its range, so the attack is skipped.
        var notSim=Make();var notBot=Bot(notSim);var notInRange=InRange("!=");notInRange.Body.Add(new CodeBlock(BlockKind.Slash));
        notSim.ApplyProgram(notBot.Id,"not_range",new[]{notInRange,new CodeBlock(BlockKind.Wait,"0",N("100"))});notSim.Start();Tick(notSim,10);
        Assert(notBot.Executions==0&&notBot.Program.Fault==null&&notBot.Program.Source.Contains("my_attack_range() != ")&&notSim.Enemies.Single().Health==10000,"!= is false for equal values, so the slash never runs");
        sim.ApplyProgram(robot.Id,"invalid_wait",new[]{new CodeBlock(BlockKind.Wait,"0",N("-1"))});sim.Invoke(robot.Id);Tick(sim,1);
        Assert(robot.Program.Fault!=null&&sim.Phase==BattlePhase.Running,"runtime diagnostic stops only offending program");
        sim.RestoreAutomatic(robot.Id);Assert(robot.Program==null&&robot.AutoExecute&&robot.Action==RobotAction.Slash,"restore native automatic mode");
        var before=robot.Executions;Tick(sim,10);Assert(robot.Executions>before,"native automatic mode resumes");
        sim.RemoveRobot(robot.Id);Assert(sim.Robots.Count==0,"removing robot removes its program owner");
        Console.WriteLine($"Defense program checks passed: {checks}");
    }
}
