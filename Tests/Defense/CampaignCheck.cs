using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class CampaignCheck
{
    static int checks;
    static void Assert(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static BattleSetup Setup() => new BattleSetup { Progression = FixtureProgression.Create(), TestOptions = new DefenseTestOptions(),
        PlayerFlow = true, Min = new Vector2(-20,-10), Max = new Vector2(20,10), BaseHealth = 20,
        Routes = new[]{new Route(new[]{new Vector2(-10,0),new Vector2(10,0)},1.1f)},
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Waves = Enumerable.Range(0,5).Select(i => new[]{new SpawnGroup{Count=2+i,Interval=.2f,Enemy=new EnemySpec{health=10,speed=1}}}).ToArray()
    };
    // Shots land within one .1 s interval, so five program runs kill five enemies (real shooters fire every 1.2 s).
    static RobotSpec Spec(RobotRole role) => new RobotSpec{role=role,damage=100,range=8,interval=.1f,projectileSpeed=200};
    static RobotState Place(DefenseSimulation sim, RobotRole role, float x=-8) => sim.Place(Spec(role),(int)role,new Vector2(x,role==RobotRole.Tank?0:2),out _);
    // A player who presses each robot's Play whenever its function has ended (play feedback 4).
    static void RunWave(DefenseSimulation sim) { for(int i=0;i<6000&&sim.Phase==BattlePhase.Running;i++){foreach(var r in sim.Robots)sim.Invoke(r.Id);sim.Advance(1.0/60);} }
    static void Reject(Action action,string label) { bool rejected=false;try{action();}catch(FormatException){rejected=true;}Assert(rejected,label); }
    static void CheckHealthTrail()
    {
        var trail=new DefenseHealthTrail();
        Assert(trail.Step(1,.1f)==1&&!trail.JustHit,"trail starts at current health");
        Assert(trail.Step(.5f,.1f)==1&&trail.JustHit,"hit keeps the old value and flags the hit");
        Assert(trail.Step(.5f,DefenseHealthTrail.HoldSeconds)==1&&!trail.JustHit,"trail holds after the hit");
        float sliding=trail.Step(.5f,.1f);Assert(sliding<1&&sliding>.5f,"trail then slides down");
        for(int i=0;i<20;i++)trail.Step(.5f,.1f);Assert(trail.Value==.5f,"trail settles on current health");
        Assert(trail.Step(.8f,.1f)==.8f,"healing snaps the trail up");
    }
    static void CheckTutorialWatcher()
    {
        const string answer="enemy = get_nearest_enemy()\nslash()";
        var code=new[]{new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash)};
        var program=new DefenseProgram("robot",code);
        Assert(TutorialWatcher.Matches(program,answer)&&TutorialWatcher.Matches(program,string.Join("", "  enemy = get_nearest_enemy()", Environment.NewLine, Environment.NewLine, "    slash()  ")),"program match ignores header, indent and blank lines");
        Assert(!TutorialWatcher.Matches(program,"enemy = get_nearest_enemy()")&&!TutorialWatcher.Matches(null,answer),"partial or missing program does not match");
        var renamed=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"target",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash)});
        Assert(TutorialWatcher.Matches(renamed,answer),"any variable name holding the enemy matches");
        var korean=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"적",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash)});
        Assert(TutorialWatcher.Matches(korean,answer),"a Korean variable name matches too");
        var later=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"e",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash),
            new CodeBlock(BlockKind.DeclareVariable,"d",new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"e"),new CodeBlock(BlockKind.MyPos)))});
        Assert(TutorialWatcher.Matches(later,"enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash()"),"distance line set below the attack still matches");
        const string taught="enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash()";
        Assert(TutorialWatcher.ConditionMatches("d <= my_attack_range()",later,"enemy_distance <= my_attack_range()",taught)&&
            !TutorialWatcher.ConditionMatches("e <= my_attack_range()",later,"enemy_distance <= my_attack_range()",taught)&&
            !TutorialWatcher.ConditionMatches("d < my_attack_range()",later,"enemy_distance <= my_attack_range()",taught),
            "loose condition matches with the player's own distance variable, not another variable or operator");
        var wrong=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"e",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash),
            new CodeBlock(BlockKind.DeclareVariable,"d",new CodeBlock(BlockKind.Hp,"0",new CodeBlock(BlockKind.Variable,"e")))});
        Assert(!TutorialWatcher.Matches(wrong,"enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash()"),"a different value still does not match");

        var sim=new DefenseSimulation(Setup());var teacher=Place(sim,RobotRole.Warrior);var second=Place(sim,RobotRole.Warrior,-5);
        Assert(TutorialWatcher.TeachingRobot(sim)==teacher&&TutorialWatcher.Robot(sim,1)==second&&TutorialWatcher.Robot(sim,2)==null,"robot index counts warriors in placement order");
        Assert(new TutorialWatcher().Poll(sim,answer,1).Count==0,"robotIndex picks whose code is judged");
        var watcher=new TutorialWatcher();
        Assert(watcher.Poll(sim,answer).Count==0,"no signals before the code matches or the wave runs");
        teacher.Program=program;
        Assert(watcher.Poll(sim,answer).SequenceEqual(new[]{TutorialSignals.ProgramMatches}),"matching code raises ProgramMatches");
        sim.Start();RunWave(sim);
        var signals=watcher.Poll(sim);
        Assert(signals.Contains(TutorialSignals.FirstKill)&&signals.Contains(TutorialSignals.FirstAttack)&&signals.Contains(TutorialSignals.WaveCleared)&&!signals.Contains(TutorialSignals.Leak),"cleared wave raises FirstKill, FirstAttack and WaveCleared once");
        Assert(watcher.Poll(sim).Count==0,"signals fire once per wave");

        var idle=new DefenseSimulation(Setup());Place(idle,RobotRole.Warrior);var idleWatcher=new TutorialWatcher();idleWatcher.Poll(idle);idle.Start();RunWave(idle);
        var leaked=idleWatcher.Poll(idle);
        Assert(leaked.Contains(TutorialSignals.Leak)&&!leaked.Contains(TutorialSignals.FirstKill)&&!leaked.Contains(TutorialSignals.FirstAttack),"robot without code lets enemies leak (watcher polled from wave start)");
        Assert(TutorialKeys.IsRuntime(TutorialKeys.Unit(0))&&TutorialKeys.IsRuntime(TutorialKeys.Range(1))&&TutorialKeys.IsRuntime(TutorialKeys.Route)&&
            TutorialKeys.IsEditorArea("Editor.Code.Arg.4.0.1")&&TutorialKeys.IsEditorArea("Editor.Palette.Slash")&&!TutorialKeys.IsEditorArea(TutorialKeys.Hint)&&!TutorialKeys.IsRuntime(null),"tutorial key classes");
    }
    static void CheckUpgrades()
    {
        foreach (RobotRole role in Enum.GetValues(typeof(RobotRole)))
        {
            var sim = new DefenseSimulation(Setup()); var bot = Place(sim,role);
            float damage = bot.Damage;
            Assert(sim.Coins==100&&bot.Level==1&&!bot.IsMaxLevel,"initial coins and level "+role);
            Assert(sim.UpgradeRobot(bot.Id)&&sim.Coins==50&&bot.Level==2&&bot.IsMaxLevel,"single paid upgrade "+role);
            Assert(Math.Abs(bot.Damage-damage*1.2f)<.001f,"upgrade damage "+role);
            Assert(!sim.UpgradeRobot(bot.Id)&&sim.Coins==50,"max level cannot charge again "+role);
            Assert(bot.Program==null&&!bot.AutoExecute&&!bot.BlockingEnabled,"upgrade grants no automatic abilities "+role);
            Assert(bot.Health.HasValue==(role==RobotRole.Tank),"upgrade does not give non-tanks health "+role);
            sim.RemoveRobot(bot.Id);
            Assert(sim.Coins==100&&Place(sim,role).Level==1,"recall refunds upgrade and returns level one item "+role);
        }
        // The campaign allows level 3: each level costs more (50, then 100) and recall refunds both.
        var tallSetup=Setup();tallSetup.RulesConfig.maxLevel=3;
        var tall=new DefenseSimulation(tallSetup);tall.SetCoins(140);var grown=Place(tall,RobotRole.Warrior);
        Assert(tall.UpgradeCostFor(grown)==50&&tall.UpgradeRobot(grown.Id)&&tall.UpgradeCostFor(grown)==100&&tall.Coins==90,"second level costs double");
        Assert(tall.UpgradeError(grown.Id)=="코인이 부족합니다."&&!tall.UpgradeRobot(grown.Id)&&tall.Coins==90,"cannot afford the dearer level");
        tall.SetCoins(100);Assert(tall.UpgradeRobot(grown.Id)&&grown.Level==3&&grown.IsMaxLevel&&tall.Coins==0,"level 3 bought");
        Assert(Math.Abs(grown.LevelMultiplier-1.44f)<.001f,"level 3 multiplier");
        tall.RemoveRobot(grown.Id);Assert(tall.Coins==150,"recall refunds every level paid");
        var early=Setup();early.Stage=1;var first=new DefenseSimulation(early);var firstBot=Place(first,RobotRole.Warrior);
        Assert(!first.UpgradeUnlocked&&first.UpgradeError(firstBot.Id)!=null&&!first.UpgradeRobot(firstBot.Id)&&first.Coins==100,"level-up locked in 1-1");
        early=Setup();early.Stage=1;early.InitialWave=1;first=new DefenseSimulation(early);firstBot=Place(first,RobotRole.Warrior);
        Assert(first.UpgradeUnlocked&&first.UpgradeRobot(firstBot.Id),"level-up opens in 1-2");
        foreach (int wave in new[]{0,1,2})
        {
            early=Setup();early.Stage=1;early.InitialWave=wave;first=new DefenseSimulation(early);firstBot=Place(first,RobotRole.Warrior);
            Assert(first.RecallUnlocked==(wave>=1)&&first.RemoveRobot(firstBot.Id)==(wave>=1),"recall locked in 1-1, opens in 1-2 (wave "+wave+")");
        }
        var setup=Setup();setup.Progression.initialCoins=49;
        var poor=new DefenseSimulation(setup);var poorBot=Place(poor,RobotRole.Warrior);
        Assert(!poor.UpgradeRobot(poorBot.Id)&&poor.Coins==49&&poorBot.Level==1,"insufficient coins leave robot unchanged");
        Assert(!poor.UpgradeRobot(-1)&&poor.Coins==49,"invalid robot leaves coins unchanged");

        var battle=new DefenseSimulation(Setup());var tank=Place(battle,RobotRole.Tank);
        tank.Health=50; battle.UpgradeRobot(tank.Id);
        Assert(Math.Abs(tank.MaxHealth-120)<.001f&&Math.Abs(tank.Health.Value-60)<.001f,"tank upgrade preserves health ratio");
        battle.ApplyProgram(tank.Id,"guard",new[]{new CodeBlock(BlockKind.Block,"0",new CodeBlock(BlockKind.NearestEnemy))});
        var shooter=Place(battle,RobotRole.Shooter,-5);
        battle.ApplyProgram(shooter.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});
        battle.Start();
        Assert(!battle.UpgradeRobot(shooter.Id)&&battle.Coins==50,"combat upgrade locked");
        battle.TogglePause();Assert(!battle.UpgradeRobot(shooter.Id),"paused combat upgrade locked");battle.TogglePause();
        RunWave(battle);
        Assert(battle.Coins==60&&battle.Kills==2,"each defeated enemy awards coins once");
        Assert(!battle.UpgradeRobot(shooter.Id),"reward phase upgrade locked");
        battle.ClaimRewards();
        Assert(tank.Level==2&&Math.Abs(tank.Health.Value-60)<.001f&&battle.Coins==60,"upgrade and coins survive wave transition");
        Assert(!battle.ClaimRewards()&&battle.Coins==60,"reward claim does not duplicate coins");
        battle.Start();battle.DamageRobot(tank.Id,10000);
        Assert(battle.Coins==60&&!battle.RemoveRobot(tank.Id),"destroyed tank has no upgrade refund");

        var buffs=new DefenseSimulation(Setup());var buffer=Place(buffs,RobotRole.Buffer);
        var ally=Place(buffs,RobotRole.Warrior,-5);buffs.UpgradeRobot(buffer.Id);buffs.Start();buffs.Advance(.2);
        buffs.RequestAction(buffer.Id,RobotAction.Buff);
        Assert(Math.Abs(ally.Buffs.Single().Damage-1.3f)<.001f&&Math.Abs(ally.Buffs.Single().Range-1.18f)<.001f&&Math.Abs(ally.Buffs.Single().Interval-.76f)<.001f,
            "buffer upgrade strengthens bonus portions by twenty percent");

        var leakSetup=Setup();leakSetup.Waves=new[]{new[]{new SpawnGroup{Count=1,Interval=1,Enemy=new EnemySpec{speed=500}}}};
        var leak=new DefenseSimulation(leakSetup);Place(leak,RobotRole.Warrior);leak.Start();RunWave(leak);
        Assert(leak.Leaks==1&&leak.Coins==100,"leaked enemies award no coins");
        var overflowSetup=Setup();overflowSetup.Progression.initialCoins=int.MaxValue;
        var overflow=new DefenseSimulation(overflowSetup);var armed=Place(overflow,RobotRole.Shooter);
        overflow.ApplyProgram(armed.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});overflow.Start();RunWave(overflow);
        Assert(overflow.Coins==int.MaxValue,"coin rewards saturate safely at storage limit");
        Assert(new DefenseSimulation(Setup()).Coins==100,"new run resets coins");
    }
    public static void Run()
    {
        LearningCheck.Run();
        CheckUpgrades();
        CheckTutorialWatcher();
        CheckHealthTrail();
        var scaledSetup=Setup();scaledSetup.Stage=2;
        var scaled=new DefenseSimulation(scaledSetup);
        Assert(Math.Abs(scaled.UpcomingWave[0].Enemy.health-11.3625f)<.001f&&
            Math.Abs(scaled.UpcomingWave[0].Enemy.speed-1.02375f)<.001f,"stage and first-wave enemy scaling");
        Assert(scaledSetup.Waves[0][0].Enemy.health==10&&scaledSetup.Waves[0][0].Enemy.speed==1,"base enemy stats remain unchanged");
        scaledSetup.Waves[0][0].Enemy.type=EnemyType.Fast;
        var runner=new DefenseSimulation(scaledSetup);
        Assert(Math.Abs(runner.UpcomingWave[0].Enemy.health-8.521875f)<.001f&&
            Math.Abs(runner.UpcomingWave[0].Enemy.speed-1.330875f)<.001f,"fast enemy type multipliers");
        scaledSetup.Waves[0][0].Enemy.type=EnemyType.Tank;
        var heavy=new DefenseSimulation(scaledSetup);
        Assert(Math.Abs(heavy.UpcomingWave[0].Enemy.health-17.04375f)<.001f&&
            Math.Abs(heavy.UpcomingWave[0].Enemy.speed-.7678125f)<.001f,"tank enemy type multipliers");
        scaledSetup.Waves[0][0].Enemy.type=EnemyType.Normal;
        Place(scaled,RobotRole.Tank);scaled.Start();RunWave(scaled);scaled.ClaimRewards();
        Assert(scaled.UpcomingWave.Sum(g=>g.Count)==3&&Math.Abs(scaled.UpcomingWave.Single().Enemy.health-13.24f)<.001f,
            "next wave uses next-wave scaling and carries nothing over");
        var cappedSetup=Setup();cappedSetup.Stage=4;
        var capped=new DefenseSimulation(cappedSetup);Place(capped,RobotRole.Tank);
        for(int wave=0;wave<4;wave++) { capped.Start();RunWave(capped);capped.ClaimRewards(); }
        Assert(Math.Abs(capped.UpcomingWave[0].Enemy.health-23.965f)<.001f&&
            Math.Abs(capped.UpcomingWave[0].Enemy.speed-1.6555f)<.001f,
            "final wave uses tuned stage progress formula");
        var neededSetup=Setup();neededSetup.Waves[0][0].Count=5;
        var needed=new DefenseSimulation(neededSetup);var neededBot=Place(needed,RobotRole.Shooter);
        needed.ApplyProgram(neededBot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});needed.Start();RunWave(needed);
        Assert(needed.PendingDrops.Take(4).SequenceEqual(new[]{BlockKind.Boom,BlockKind.Buff,BlockKind.Block,BlockKind.Boom}),
            "drops fill missing native actions before surplus blocks");
        // Play (play feedback 4): battle start calls each function once; Play calls it again, never while it runs.
        var runs=new DefenseSimulation(Setup());var caller=Place(runs,RobotRole.Warrior);
        runs.ApplyProgram(caller.Id,"swing",new[]{new CodeBlock(BlockKind.Slash)});
        Assert(!runs.CanInvoke(caller.Id),"Play waits for the battle");
        runs.Start();Assert(caller.Program.Running&&!runs.Invoke(caller.Id),"battle start calls the function; Play is locked while it runs");
        for(int i=0;i<60&&caller.Program.Running;i++)runs.Advance(1.0/60);
        Assert(!caller.Program.Running&&caller.Program.CompletedRuns==1&&runs.Invoke(caller.Id)&&caller.Program.Running,"after it ends Play calls it again");
        var flow=Setup();
        var sim=new DefenseSimulation(flow); sim.Start();Assert(sim.Phase==BattlePhase.Ready,"no robots start rejected");
        var bot=Place(sim,RobotRole.Shooter);
        Assert(bot.Program==null&&!bot.AutoExecute&&!bot.BlockingEnabled,"placed robot is empty");
        Assert(sim.StartError()==null,"empty robot does not block start");
        sim.ApplyProgram(bot.Id,"empty",Array.Empty<CodeBlock>());Assert(sim.StartError()==null,"empty applied code does not block start");
        sim.ApplyProgram(bot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});
        Assert(sim.Available(BlockKind.Shot)==int.MaxValue,"unlocked blocks are unlimited");
        var saved=bot.Program;
        var many=Enumerable.Range(0,10).Select(i=>new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))).ToArray();
        sim.ApplyProgram(bot.Id,"many",many);
        var second=Place(sim,RobotRole.Warrior,-5);sim.ApplyProgram(second.Id,"many",many);
        Assert(bot.Program.BlockCount(BlockKind.Shot)==10&&second.Program.BlockCount(BlockKind.Shot)==10&&sim.Available(BlockKind.Shot)==int.MaxValue,"placing blocks never uses them up");
        sim.RemoveRobot(second.Id);var applied=bot.Program;
        Reject(()=>sim.ApplyProgram(bot.Id,"locked",new[]{new CodeBlock(BlockKind.While)}),"a locked block is rejected");
        Assert(bot.Program==applied,"failed apply atomic");
        sim.ApplyProgram(bot.Id,"fire",saved.CopyBlocks());
        // This fixture tests rewards and reservation, without randomly unlocking loop execution rules.
        sim.Setup.Progression.drops = sim.Setup.Progression.drops.Where(d => d.kind != BlockKind.For && d.kind != BlockKind.While).ToArray();
        for(int wave=0;wave<5;wave++)
        {
            int before=sim.Inventory.Values.Sum();sim.Start();Assert(sim.Phase==BattlePhase.Running,"manual start wave "+wave);
            Reject(()=>sim.ApplyProgram(bot.Id,"midfight",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))}),"battle code lock");
            Assert(Place(sim,RobotRole.Warrior,-5)==null&&!sim.RemoveRobot(bot.Id),"battle placement removal lock");
            RunWave(sim);Assert(sim.Phase==BattlePhase.Reward&&sim.WaveIndex==wave,"reward stops progression");
            Assert(sim.PendingDrops.Count==2+wave&&sim.Inventory.Values.Sum()==before,$"drops pending until claim: wave={wave} drops={sim.PendingDrops.Count} kills={sim.Kills} leaks={sim.Leaks} stock={sim.Inventory.Values.Sum()} before={before}");
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
        items.Inventory.Remove(BlockKind.Number);var previous=armed.Program;
        Reject(()=>items.ApplyProgram(armed.Id,"wait",new[]{new CodeBlock(BlockKind.Wait,"0",new CodeBlock(BlockKind.Number,"2"))}),"a locked parameter block blocks apply");
        Assert(armed.Program==previous,"a locked parameter preserves old code");
        items.Inventory[BlockKind.Number]=12;
        items.GrantBlock(BlockKind.Attack,2);Assert(items.Inventory[BlockKind.Shot]==5,"legacy grant canonicalizes attack");
        var method=new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"enemy"),new CodeBlock(BlockKind.MyPos));
        Assert(DefenseProgression.Used(new[]{method}).SequenceEqual(new[]{BlockKind.Variable,BlockKind.MyPos}),"method receiver not charged twice");
        Assert(DefenseProgression.Used(new[]{new CodeBlock(BlockKind.IsDead,"0",new CodeBlock(BlockKind.Variable,"enemy"))}).SequenceEqual(new[]{BlockKind.IsDead}),"is_dead() receiver not charged as a variable (the editor's one block)");
        itemTank=Place(items,RobotRole.Tank);items.ApplyProgram(itemTank.Id,"tank",new[]{new CodeBlock(BlockKind.Block,"0",new CodeBlock(BlockKind.NearestEnemy))});items.Start();
        stock=items.RobotAvailable(RobotRole.Tank);items.DamageRobot(itemTank.Id,10000);
        Assert(items.RobotAvailable(RobotRole.Tank)==stock&&items.Available(BlockKind.Block)==int.MaxValue,"destroyed robot consumed");
        Assert(DefenseProgression.ItemKinds.All(DefenseProgression.Consumes),"all block types are inventory items");
        items.GrantBlock(BlockKind.Shot,10000); Assert(items.Inventory[BlockKind.Shot]==10005,"block grants have no gameplay cap");
        // No wave time limit: a wave with enemies alive (or still to come) runs until each one is killed or reaches the core.
        var untimedSetup=Setup();
        untimedSetup.Waves[0][0].Count=3;untimedSetup.Waves[0][0].Interval=30;
        var untimed=new DefenseSimulation(untimedSetup);Place(untimed,RobotRole.Shooter);
        Assert(untimed.RemainingEnemies==3,"ready counts queued enemies");
        untimed.Start();for(int i=0;i<55*60;i++)untimed.Advance(1.0/60);
        Assert(untimed.Phase==BattlePhase.Running&&untimed.RemainingEnemies==1&&untimed.Leaks==2,"past the old 35-50 s the wave still runs with its last enemy on the road");
        RunWave(untimed);
        Assert(untimed.Phase==BattlePhase.Reward&&untimed.Leaks==3&&untimed.Kills==0&&untimed.Enemies.Count==0&&untimed.Time>75,"the wave ends when its last enemy reaches the core");
        untimed.ClaimRewards();Assert(untimed.RemainingEnemies==3,"the next wave brings only its own enemies");
        var fast=new DefenseSimulation(Setup());var fastBot=Place(fast,RobotRole.Shooter);
        fast.ApplyProgram(fastBot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});fast.Start();RunWave(fast);
        Assert(fast.Phase==BattlePhase.Reward&&fast.Kills==2&&fast.Leaks==0,"all dead ends the wave");
        Console.WriteLine($"Campaign checks passed: {checks}");
    }
}
