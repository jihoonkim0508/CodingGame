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
    static void RunWave(DefenseSimulation sim) { for(int i=0;i<6000&&sim.Phase==BattlePhase.Running;i++)sim.Advance(1.0/60); }
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
        const string answer="enemy = get_nearest_enemy()\nslash(enemy)";
        var code=new[]{new CodeBlock(BlockKind.DeclareVariable,"enemy",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash,"0",new CodeBlock(BlockKind.Variable,"enemy"))};
        var program=new DefenseProgram("robot",code);
        Assert(TutorialWatcher.Matches(program,answer)&&TutorialWatcher.Matches(program,string.Join("", "  enemy = get_nearest_enemy()", Environment.NewLine, Environment.NewLine, "    slash(enemy)  ")),"program match ignores header, indent and blank lines");
        Assert(!TutorialWatcher.Matches(program,"enemy = get_nearest_enemy()")&&!TutorialWatcher.Matches(null,answer),"partial or missing program does not match");
        var renamed=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"target",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash,"0",new CodeBlock(BlockKind.Variable,"target"))});
        Assert(TutorialWatcher.Matches(renamed,answer),"any variable name holding the enemy matches");
        var korean=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"적",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash,"0",new CodeBlock(BlockKind.Variable,"적"))});
        Assert(TutorialWatcher.Matches(korean,answer),"a Korean variable name matches too");
        var later=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"e",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash,"0",new CodeBlock(BlockKind.Variable,"e")),
            new CodeBlock(BlockKind.DeclareVariable,"d",new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"e"),new CodeBlock(BlockKind.MyPos)))});
        Assert(TutorialWatcher.Matches(later,"enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash(enemy)"),"distance line set below the attack still matches");
        const string taught="enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash(enemy)";
        Assert(TutorialWatcher.ConditionMatches("d <= my_attack_range()",later,"enemy_distance <= my_attack_range()",taught)&&
            !TutorialWatcher.ConditionMatches("e <= my_attack_range()",later,"enemy_distance <= my_attack_range()",taught)&&
            !TutorialWatcher.ConditionMatches("d < my_attack_range()",later,"enemy_distance <= my_attack_range()",taught),
            "loose condition matches with the player's own distance variable, not another variable or operator");
        var wrong=new DefenseProgram("robot",new[]{new CodeBlock(BlockKind.DeclareVariable,"e",new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash,"0",new CodeBlock(BlockKind.Variable,"e")),
            new CodeBlock(BlockKind.DeclareVariable,"d",new CodeBlock(BlockKind.Hp,"0",new CodeBlock(BlockKind.Variable,"e")))});
        Assert(!TutorialWatcher.Matches(wrong,"enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash(enemy)"),"a different value still does not match");

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
        var early=Setup();early.Stage=1;var first=new DefenseSimulation(early);var firstBot=Place(first,RobotRole.Warrior);
        Assert(!first.UpgradeUnlocked&&first.UpgradeError(firstBot.Id)!=null&&!first.UpgradeRobot(firstBot.Id)&&first.Coins==100,"level-up locked in 1-1");
        early=Setup();early.Stage=1;early.InitialWave=1;first=new DefenseSimulation(early);firstBot=Place(first,RobotRole.Warrior);
        Assert(first.UpgradeUnlocked&&first.UpgradeRobot(firstBot.Id),"level-up opens in 1-2");
        foreach (int wave in new[]{0,1,2})
        {
            early=Setup();early.Stage=1;early.InitialWave=wave;first=new DefenseSimulation(early);firstBot=Place(first,RobotRole.Warrior);
            Assert(first.RecallUnlocked==(wave>=2)&&first.RemoveRobot(firstBot.Id)==(wave>=2),"recall locked through 1-2, opens in 1-3 (wave "+wave+")");
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

        var timeoutSetup=Setup();timeoutSetup.WaveSeconds=.2f;
        var timeout=new DefenseSimulation(timeoutSetup);Place(timeout,RobotRole.Tank);timeout.Start();RunWave(timeout);
        Assert(timeout.Kills==0&&timeout.Coins==100,"timeout cleanup awards no coins");
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
        var scaledSetup=Setup();scaledSetup.Stage=2;scaledSetup.WaveSeconds=.2f;
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
        Assert(scaled.UpcomingWave.Sum(g=>g.Count)==5&&
            Math.Abs(scaled.UpcomingWave.Single(g=>g.Count==2).Enemy.health-11.3625f)<.001f&&
            Math.Abs(scaled.UpcomingWave.Single(g=>g.Count==3).Enemy.health-13.24f)<.001f,
            "carried enemies keep prior stats while new enemies use next-wave scaling");
        var cappedSetup=Setup();cappedSetup.Stage=4;cappedSetup.WaveSeconds=.1f;
        var capped=new DefenseSimulation(cappedSetup);Place(capped,RobotRole.Tank);
        for(int wave=0;wave<4;wave++) { capped.Start();RunWave(capped);capped.ClaimRewards(); }
        Assert(Math.Abs(capped.UpcomingWave[0].Enemy.health-23.965f)<.001f&&
            Math.Abs(capped.UpcomingWave[0].Enemy.speed-1.6555f)<.001f,
            "final wave uses tuned stage progress formula");
        var carriedSetup=Setup();carriedSetup.Stage=2;carriedSetup.WaveSeconds=1.1f;
        var carried=new DefenseSimulation(carriedSetup);Place(carried,RobotRole.Tank);
        for(int wave=0;wave<2;wave++) { carried.Start();RunWave(carried);carried.ClaimRewards(); }
        Assert(carried.UpcomingWave.Any(g=>g.Count==2&&Math.Abs(g.Enemy.health-11.3625f)<.001f)&&
            carried.UpcomingWave.Any(g=>g.Count==3&&Math.Abs(g.Enemy.health-13.24f)<.001f),
            "carried enemies from separate waves retain their own stats");
        var neededSetup=Setup();neededSetup.Waves[0][0].Count=5;
        var needed=new DefenseSimulation(neededSetup);var neededBot=Place(needed,RobotRole.Shooter);
        needed.ApplyProgram(neededBot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});needed.Start();RunWave(needed);
        Assert(needed.PendingDrops.Take(4).SequenceEqual(new[]{BlockKind.Boom,BlockKind.Buff,BlockKind.Block,BlockKind.Boom}),
            "drops fill missing native actions before surplus blocks");
        var runs=new DefenseSimulation(Setup());
        Assert(runs.FunctionRepeatLimit==5,"function runs 5 times per wave before loops");
        runs.GrantBlock(BlockKind.For,1);Assert(runs.FunctionRepeatLimit==1,"function runs once after loops unlock");
        // The reward flow below needs every wave cleared, not the per-wave run limit.
        var flow=Setup();flow.FunctionRepeatLimit=int.MaxValue;
        var sim=new DefenseSimulation(flow); sim.Start();Assert(sim.Phase==BattlePhase.Ready,"no robots start rejected");
        var bot=Place(sim,RobotRole.Shooter);
        Assert(bot.Program==null&&!bot.AutoExecute&&!bot.BlockingEnabled,"placed robot is empty");
        Assert(sim.StartError()==null,"empty robot does not block start");
        sim.ApplyProgram(bot.Id,"empty",Array.Empty<CodeBlock>());Assert(sim.StartError()==null,"empty applied code does not block start");
        sim.ApplyProgram(bot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});
        Assert(sim.Available(BlockKind.Shot)==2&&sim.Available(BlockKind.Shot,bot.Id)==3,"inventory reserves per robot");
        var saved=bot.Program;
        Reject(()=>sim.ApplyProgram(bot.Id,"too_many",Enumerable.Range(0,4).Select(i=>new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))).ToArray()),"overcommit rejected");
        Assert(bot.Program==saved&&sim.Available(BlockKind.Shot)==2,"failed apply atomic");
        var second=Place(sim,RobotRole.Warrior,-5);sim.ApplyProgram(second.Id,"copy",new[]{new CodeBlock(BlockKind.Attack,"0",new CodeBlock(BlockKind.NearestEnemy))});
        Assert(sim.Available(BlockKind.Shot)==1,"legacy attack shares inventory");
        sim.RemoveRobot(second.Id);Assert(sim.Available(BlockKind.Shot)==2,"remove releases reservation");
        // A syntax error empties the applied code, but the blocks left in that robot's editor are still its own.
        sim.ApplyProgram(bot.Id,"broken",Array.Empty<CodeBlock>());sim.SetDraft(bot.Id,new[]{BlockKind.Shot,BlockKind.NearestEnemy});
        Assert(sim.Available(BlockKind.Shot)==2&&sim.Available(BlockKind.Shot,bot.Id)==3,"unfinished editor blocks stay reserved");
        Reject(()=>sim.ApplyProgram(Place(sim,RobotRole.Warrior,-5).Id,"take",Enumerable.Range(0,3).Select(i=>new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))).ToArray()),"another robot cannot take a draft's blocks");
        sim.SetDraft(bot.Id,Array.Empty<BlockKind>());sim.RemoveRobot(sim.Robots.Last().Id);sim.ApplyProgram(bot.Id,"fire",saved.CopyBlocks());
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
        Assert(items.Available(BlockKind.Wait)==5&&items.Available(BlockKind.Number)==11,"parameters consume inventory too");
        items.Inventory[BlockKind.Number]=0;var previous=armed.Program;
        Reject(()=>items.ApplyProgram(armed.Id,"wait",new[]{new CodeBlock(BlockKind.Wait,"0",new CodeBlock(BlockKind.Number,"2"))}),"missing parameter item blocks apply");
        Assert(armed.Program==previous,"parameter shortage preserves old code");
        items.Inventory[BlockKind.Number]=12;
        items.GrantBlock(BlockKind.Attack,2);Assert(items.Inventory[BlockKind.Shot]==5,"legacy grant canonicalizes attack");
        var method=new CodeBlock(BlockKind.Distance,"0",new CodeBlock(BlockKind.Variable,"enemy"),new CodeBlock(BlockKind.MyPos));
        Assert(DefenseProgression.Used(new[]{method}).SequenceEqual(new[]{BlockKind.Variable,BlockKind.MyPos}),"method receiver not charged twice");
        itemTank=Place(items,RobotRole.Tank);items.ApplyProgram(itemTank.Id,"tank",new[]{new CodeBlock(BlockKind.Block,"0",new CodeBlock(BlockKind.NearestEnemy))});items.Start();
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
        fast.ApplyProgram(fastBot.Id,"fire",new[]{new CodeBlock(BlockKind.Shot,"0",new CodeBlock(BlockKind.NearestEnemy))});fast.Start();RunWave(fast);
        Assert(fast.Time<20&&!fast.LastWaveTimedOut&&fast.LastSurvivors==0,"all dead ends before deadline");
        Console.WriteLine($"Campaign checks passed: {checks}");
    }
}
