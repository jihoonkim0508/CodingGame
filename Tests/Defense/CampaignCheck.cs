using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class CampaignCheck
{
    static int checks;
    static void Assert(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static BattleSetup Setup() => new BattleSetup {
        PlayerFlow = true, DropSeed = 42, Min = new Vector2(-20,-10), Max = new Vector2(20,10), BaseHealth = 20,
        Routes = new[]{new Route(new[]{new Vector2(-10,0),new Vector2(10,0)},1.1f)},
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Waves = Enumerable.Range(0,5).Select(i => new[]{new SpawnGroup{Count=2+i,Interval=.2f,Enemy=new EnemySpec{health=10,speed=1}}}).ToArray()
    };
    static RobotSpec Spec(RobotRole role) => new RobotSpec{role=role,damage=100,range=8,interval=.1f};
    static RobotState Place(DefenseSimulation sim, RobotRole role, float x=-8) => sim.Place(Spec(role),(int)role,new Vector2(x,role==RobotRole.Tank?0:2),out _);
    static void RunWave(DefenseSimulation sim) { for(int i=0;i<6000&&sim.Phase==BattlePhase.Running;i++)sim.Advance(1.0/60); }
    static void Reject(Action action,string label) { bool rejected=false;try{action();}catch(FormatException){rejected=true;}Assert(rejected,label); }
    public static void Run()
    {
        var sim=new DefenseSimulation(Setup()); sim.Start();Assert(sim.Phase==BattlePhase.Ready,"no robots start rejected");
        var bot=Place(sim,RobotRole.Shooter);
        Assert(bot.Program==null&&!bot.AutoExecute&&!bot.BlockingEnabled,"placed robot is empty");
        Assert(sim.StartError()==null,"empty robot does not block start");
        sim.ApplyProgram(bot.Id,"empty",Array.Empty<CodeBlock>());Assert(sim.StartError()==null,"empty applied code does not block start");
        sim.ApplyProgram(bot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot)});
        Assert(sim.Available(BlockKind.Shot)==2&&sim.Available(BlockKind.Shot,bot.Id)==3,"inventory reserves per robot");
        var saved=bot.Program;
        Reject(()=>sim.ApplyProgram(bot.Id,"too_many",Enumerable.Range(0,4).Select(i=>new CodeBlock(BlockKind.Shot)).ToArray()),"overcommit rejected");
        Assert(bot.Program==saved&&sim.Available(BlockKind.Shot)==2,"failed apply atomic");
        var second=Place(sim,RobotRole.Warrior,-5);sim.ApplyProgram(second.Id,"copy",new[]{new CodeBlock(BlockKind.Attack,"0",new CodeBlock(BlockKind.NearestEnemy))});
        Assert(sim.Available(BlockKind.Shot)==1,"legacy attack shares inventory");
        sim.RemoveRobot(second.Id);Assert(sim.Available(BlockKind.Shot)==2,"remove releases reservation");
        for(int wave=0;wave<5;wave++)
        {
            int before=sim.Inventory.Values.Sum();sim.Start();Assert(sim.Phase==BattlePhase.Running,"manual start wave "+wave);
            Reject(()=>sim.ApplyProgram(bot.Id,"midfight",new[]{new CodeBlock(BlockKind.Shot)}),"battle code lock");
            Assert(Place(sim,RobotRole.Warrior,-5)==null&&!sim.RemoveRobot(bot.Id),"battle placement removal lock");
            RunWave(sim);Assert(sim.Phase==BattlePhase.Reward&&sim.WaveIndex==wave,"reward stops progression");
            Assert(sim.PendingDrops.Count==2+wave&&sim.Inventory.Values.Sum()==before,"drops pending until claim");
            double time=sim.Time;sim.Advance(200);Assert(sim.Time==time,"reward freezes clock");
            Assert(sim.ClaimRewards()&&sim.Inventory.Values.Sum()==before+2+wave&&sim.PendingDrops.Count==0,"claim transfers exactly once");
            Assert(!sim.ClaimRewards(),"duplicate claim rejected");
            Assert(sim.Phase==(wave==4?BattlePhase.Victory:BattlePhase.Ready),"claim phase transition");
        }
        Assert(sim.Kills==20&&sim.BaseHealth==20,"five wave win");
        var emptySetup=Setup();
        var emptySim=new DefenseSimulation(emptySetup);var tank=Place(emptySim,RobotRole.Tank);emptySim.Start();RunWave(emptySim);
        Assert(tank.Executions==0&&!tank.BlockingEnabled&&emptySim.Kills==0,"empty tank never auto blocks or attacks");
        var reset=new DefenseSimulation(Setup());Assert(reset.Inventory.Values.Sum()==Setup().Progression.initial.Sum(s=>s.count)&&reset.PendingDrops.Count==0,"new run resets stock");
        var leakSetup=Setup();leakSetup.Waves=new[]{new[]{new SpawnGroup{Count=1,Interval=1,Enemy=new EnemySpec{speed=500,health=10}}}};
        var leaks=new DefenseSimulation(leakSetup);var idle=Place(leaks,RobotRole.Shooter);
        leaks.ApplyProgram(idle.Id,"idle",new[]{new CodeBlock(BlockKind.Wait,"0",new CodeBlock(BlockKind.Number,"100"))});leaks.Start();RunWave(leaks);
        Assert(leaks.Phase==BattlePhase.Reward&&leaks.PendingDrops.Count==0&&leaks.Leaks==1,"leak gives no drop but reaches rewards");
        leaks.ClaimRewards();Assert(leaks.Phase==BattlePhase.Victory,"zero drop rewards can finish");
        var items=new DefenseSimulation(Setup());
        int stock=items.RobotAvailable(RobotRole.Tank);var itemTank=Place(items,RobotRole.Tank);
        Assert(items.RobotAvailable(RobotRole.Tank)==stock-1,"placement spends robot item");
        Assert(items.Place(Spec(RobotRole.Tank),2,itemTank.Position,out _)==null&&items.RobotAvailable(RobotRole.Tank)==stock-1,"failed placement preserves stock");
        items.RemoveRobot(itemTank.Id);Assert(items.RobotAvailable(RobotRole.Tank)==stock,"recall returns robot item");
        items.RobotInventory[RobotRole.Shooter]=0;Assert(Place(items,RobotRole.Shooter)==null,"empty inventory prevents placement");
        items.GrantRobot(RobotRole.Shooter,2);var armed=Place(items,RobotRole.Shooter);
        Assert(armed!=null&&items.RobotAvailable(RobotRole.Shooter)==1,"granted robot deploys");
        items.ApplyProgram(armed.Id,"wait",new[]{new CodeBlock(BlockKind.Wait,"0",new CodeBlock(BlockKind.Number,"1"))});
        Assert(items.Available(BlockKind.Wait)==5&&items.Available(BlockKind.Number)==11,"parameters consume inventory too");
        items.Inventory[BlockKind.Number]=0;var previous=armed.Program;
        Reject(()=>items.ApplyProgram(armed.Id,"wait",new[]{new CodeBlock(BlockKind.Wait,"0",new CodeBlock(BlockKind.Number,"2"))}),"missing parameter item blocks apply");
        Assert(armed.Program==previous,"parameter shortage preserves old code");
        items.Inventory[BlockKind.Number]=12;
        items.GrantBlock(BlockKind.Attack,2);Assert(items.Inventory[BlockKind.Shot]==5,"legacy grant canonicalizes attack");
        var method=new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"enemy"),new CodeBlock(BlockKind.PositionX),new CodeBlock(BlockKind.PositionY));
        Assert(DefenseProgression.Used(new[]{method}).SequenceEqual(new[]{BlockKind.Variable,BlockKind.PositionX,BlockKind.PositionY}),"method receiver not charged twice");
        itemTank=Place(items,RobotRole.Tank);items.ApplyProgram(itemTank.Id,"tank",new[]{new CodeBlock(BlockKind.Block)});items.Start();
        stock=items.RobotAvailable(RobotRole.Tank);items.DamageRobot(itemTank.Id,10000);
        Assert(items.RobotAvailable(RobotRole.Tank)==stock&&items.Available(BlockKind.Block)==1,"destroyed robot consumed, blocks returned");
        Assert(DefenseProgression.ItemKinds.All(DefenseProgression.Consumes),"all block types are inventory items");
        items.GrantBlock(BlockKind.Shot,10000); Assert(items.Inventory[BlockKind.Shot]==10005,"block grants have no gameplay cap");
        var timedSetup=Setup();timedSetup.WaveSeconds=20;
        timedSetup.Waves[0][0].Count=3;timedSetup.Waves[0][0].Interval=30;timedSetup.Waves[0][0].Enemy.speed=.1f;
        var timed=new DefenseSimulation(timedSetup);Place(timed,RobotRole.Shooter);
        Assert(timed.RemainingEnemies==3&&timed.WaveRemaining==20,"ready counts queued enemies and full timer");
        timed.Start();timed.Advance(5);timed.TogglePause();double frozen=timed.WaveRemaining;timed.Advance(100);
        Assert(timed.WaveRemaining==frozen,"pause freezes deadline");timed.TogglePause();RunWave(timed);
        Assert(timed.Phase==BattlePhase.Reward&&Math.Abs(timed.Time-20)<.001,"twenty second deadline");
        Assert(timed.LastSurvivors==3&&timed.WaveSurvivors.SequenceEqual(new[]{3})&&timed.Enemies.Count==0,"alive and queued enemies recorded and removed");
        Assert(timed.PendingDrops.Count==0&&timed.Kills==0&&timed.Leaks==0,"timeout removal gives neither kills drops nor leaks");
        Assert(timed.UpcomingWave.Sum(g=>g.Count)==6,"next wave preview includes carry");
        timed.ClaimRewards();Assert(timed.RemainingEnemies==6&&timed.WaveRemaining==20,"carry applied exactly once with fresh timer");
        timed.Start();RunWave(timed);Assert(timed.WaveSurvivors.Count==2,"survivor history retained");
        var displaySetup=Setup();displaySetup.WaveSeconds=35;displaySetup.Waves[0][0].Enemy.speed=.1f;
        var display=new DefenseSimulation(displaySetup);Place(display,RobotRole.Shooter);display.Start();RunWave(display);
        Assert(display.Phase==BattlePhase.Reward&&display.LastWaveTimedOut&&display.WaveRemaining==0&&Math.Ceiling(display.WaveRemaining)==0,"35 second timeout displays zero despite fractional tick drift");
        display.ClaimRewards();Assert(display.WaveRemaining==35,"next ready phase restores full 35 seconds");
        var fastSetup=Setup();fastSetup.WaveSeconds=20;var fast=new DefenseSimulation(fastSetup);var fastBot=Place(fast,RobotRole.Shooter);
        fast.ApplyProgram(fastBot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot)});fast.Start();RunWave(fast);
        Assert(fast.Time<20&&!fast.LastWaveTimedOut&&fast.LastSurvivors==0,"all dead ends before deadline");
        Console.WriteLine($"Campaign checks passed: {checks}");
    }
}
