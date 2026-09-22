using System;
using System.Linq;
using System.Numerics;
using CodingGame.Defense;

static class Check
{
    static int checks;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static BattleSetup Setup(float health = 100, float speed = 1, int count = 1, int baseHealth = 10)
        => new BattleSetup { Min = new Vector2(-12, -8), Max = new Vector2(12, 8), BaseHealth = baseHealth,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1.1f) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = health, speed = speed }, Count = count, Interval = .1f } } } };
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, range = 5, interval = .1f, damage = 5 };
    static RobotState Place(DefenseSimulation sim, RobotRole role, float x, float z)
    {
        var bot = sim.Place(Spec(role), (int)role, new Vector2(x, z), out var error);
        if (bot == null) throw new Exception(error);
        bot.AutoExecute = true; bot.BlockingEnabled = true; return bot;
    }
    static void Advance(DefenseSimulation sim, double seconds) { for (int i = 0; i < (int)(seconds * 60); i++) sim.Advance(1.0 / 60); }
    static void Main()
    {
        var sim = new DefenseSimulation(Setup());
        Assert(sim.Place(Spec(RobotRole.Warrior), 1, new Vector2(-8, 0), out _) == null, "ordinary robot cannot occupy path");
        var warrior = Place(sim, RobotRole.Warrior, -8.123f, 2.123f);
        Assert(warrior.Position.X == -8.123f && warrior.Position.Y == 2.123f, "free position is not snapped");
        Assert(warrior.Health == null, "ordinary robot has no health");
        Assert(sim.Place(Spec(RobotRole.Warrior), 1, warrior.Position, out _) == null, "overlap rejected");
        Assert(sim.Place(Spec(RobotRole.Tank), 2, new Vector2(0, 2), out _) == null, "tank sample policy is path-only");
        Place(sim, RobotRole.Buffer, 0, 3);
        Assert(sim.Place(Spec(RobotRole.Buffer), 0, new Vector2(5, 3), out _) == null, "one buffer limit");
        var tank = Place(sim, RobotRole.Tank, -7, 0); tank.AutoExecute = false;
        sim.Start();
        Assert(!sim.DamageRobot(warrior.Id, 999), "no damage to ordinary robot");
        Assert(sim.RequestAction(tank.Id, RobotAction.Buff) == ActionResult.Ineffective, "tank buff ineffective");
        Assert(sim.RequestAction(warrior.Id, RobotAction.Buff) == ActionResult.Undecided, "undefined buff action not implicitly permitted");
        Assert(Rules.Compatibility(RobotRole.Bomber, RobotAction.Block) == ActionCompatibility.Ineffective, "bomber block ineffective");
        Assert(Rules.Compatibility(RobotRole.Shooter, RobotAction.Block) == ActionCompatibility.Ineffective, "shooter block ineffective");
        var before = tank.NextAction;
        sim.RequestAction(tank.Id, RobotAction.Buff);
        Assert(tank.NextAction == before, "ineffective action consumes no cooldown");
        sim.TogglePause(); double time = sim.Time; sim.Advance(50);
        Assert(sim.Time == time && sim.Spawned == 0, "pause freezes time/spawns");
        sim.TogglePause();

        var blockSim = new DefenseSimulation(Setup(speed: 300));
        var blocker = Place(blockSim, RobotRole.Tank, -5, 0); blocker.AutoExecute = false;
        blockSim.Start(); blockSim.Advance(1.0 / 60);
        Assert(blockSim.Enemies[0].BlockedBy == blocker.Id, "swept path cannot skip tank at high speed");
        var enemy = blockSim.Enemies[0]; var stopped = enemy.Position;
        enemy.Effects.Add(new MovementEffect { Source = blocker.Id, StunUntil = blockSim.Time + 1, SlowUntil = blockSim.Time + 3, Multiplier = .5f });
        blockSim.RemoveRobot(blocker.Id); blockSim.Advance(1.0 / 60);
        Assert(enemy.BlockedBy == 0 && enemy.Position == stopped, "tank removal preserves independent stun");
        Assert(enemy.Speed(blockSim.Time) == enemy.Spec.speed * .5f, "slow remains after blocker removal");

        var bodySim = new DefenseSimulation(Setup(speed: 4));
        var body = Place(bodySim, RobotRole.Tank, -8, 0); body.AutoExecute = false;
        bodySim.Start(); Advance(bodySim, .5);
        var held = bodySim.Enemies.Single();
        Assert(held.BlockedBy == body.Id, "enabled blocking holds enemies");
        held.Effects.Add(new MovementEffect { StunUntil = bodySim.Time + .1 });
        Advance(bodySim, .2);
        Assert(held.BlockedBy == body.Id && !held.Stunned(bodySim.Time), "stun expiry does not release body block");
        bodySim.DamageRobot(body.Id, 10000);
        Assert(bodySim.Robots.Count == 0 && held.BlockedBy == 0, "tank death clears references");

        var shooterSim = new DefenseSimulation(Setup());
        var shooter = Place(shooterSim, RobotRole.Shooter, -8, 3);
        float damage = shooter.Damage;
        shooterSim.SetShooterRange(shooter.Id, 7);
        Assert(shooter.Damage < damage, "shooter damage inversely follows range");

        var blastSim = new DefenseSimulation(Setup(health: 20, count: 3));
        var bomb = Place(blastSim, RobotRole.Bomber, -8, 3); bomb.AutoExecute = false;
        blastSim.Start(); Advance(blastSim, .3);
        Assert(blastSim.RequestAction(bomb.Id, RobotAction.Boom) == ActionResult.Executed, "bomb action succeeds");
        Assert(blastSim.Projectiles.Count == 1 && blastSim.Enemies.All(e => e.Health == 20), "bomb launch has travel time and no immediate damage");
        Assert(blastSim.RequestAction(bomb.Id, RobotAction.Boom) == ActionResult.CoolingDown, "repeated calls cannot bypass interval");
        var aim = blastSim.Projectiles[0].Destination;
        blastSim.TogglePause();time=blastSim.Time;blastSim.Advance(5);
        Assert(blastSim.Time==time&&blastSim.Projectiles.Count==1,"pause freezes projectile flight");blastSim.TogglePause();
        Advance(blastSim, 1);
        Assert(blastSim.Projectiles.Count==0&&blastSim.Enemies.Count==3&&blastSim.Enemies.All(e=>e.Health==15),"one damage application per target at impact");
        Assert(blastSim.Events.Single(e=>e.Kind=="blast").Position==aim,"impact uses launch position rather than tracking target");
        foreach(var speed in new[]{1.5f,2.8f,1.15f})
        {
            var dodge=new DefenseSimulation(Setup(speed:speed));var thrower=Place(dodge,RobotRole.Bomber,-8,3);thrower.AutoExecute=false;
            dodge.Start();Advance(dodge,.2);dodge.RequestAction(thrower.Id,RobotAction.Boom);Advance(dodge,1);
            Assert(dodge.Enemies[0].Health==(speed==2.8f?100:95),"speed determines whether enemy leaves blast radius: "+speed);
        }
        foreach(var role in new[]{RobotRole.Shooter,RobotRole.Warrior})
        {
            var direct=new DefenseSimulation(Setup(speed:8));var attacker=Place(direct,role,-8,3);attacker.AutoExecute=false;
            direct.Start();Advance(direct,.2);direct.RequestAction(attacker.Id,attacker.Spec.NativeAction);
            Assert(direct.Projectiles.Count==0&&direct.Enemies[0].Health==95,"direct action hits fast in-range target immediately: "+role);
        }
        var crossBomb=new DefenseSimulation(Setup());var crossThrower=Place(crossBomb,RobotRole.Warrior,-8,3);crossThrower.AutoExecute=false;
        crossBomb.Start();Advance(crossBomb,.2);crossBomb.RequestAction(crossThrower.Id,RobotAction.Boom);crossBomb.RemoveRobot(crossThrower.Id);Advance(crossBomb,1);
        Assert(crossBomb.Enemies[0].Health==99.5f,"cross-role projectile retains ten percent damage after source removal");
        var deadlineSetup=Setup();deadlineSetup.PlayerFlow=true;deadlineSetup.WaveSeconds=.5f;
        var deadline=new DefenseSimulation(deadlineSetup);var late=Place(deadline,RobotRole.Bomber,-8,3);late.AutoExecute=false;
        deadline.Start();Advance(deadline,.2);deadline.RequestAction(late.Id,RobotAction.Boom);Advance(deadline,1);
        Assert(deadline.Phase==BattlePhase.Reward&&deadline.Projectiles.Count==0&&deadline.PendingDrops.Count==0&&deadline.Kills==0,"timeout cancels in-flight bombs without drops");

        var buffSim = new DefenseSimulation(Setup());
        var buffer = Place(buffSim, RobotRole.Buffer, -5, 3); buffer.AutoExecute = false;
        var recipient = Place(buffSim, RobotRole.Warrior, -7, 3); recipient.AutoExecute = false;
        buffSim.Start(); Advance(buffSim, .2); buffSim.RequestAction(buffer.Id, RobotAction.Buff);
        Assert(recipient.Range > recipient.Spec.range && recipient.Damage > recipient.Spec.damage && recipient.Interval < recipient.Spec.interval, "buff strengthens all 3 fields");
        Advance(buffSim, .2); buffSim.RequestAction(buffer.Id, RobotAction.Buff);
        Assert(recipient.Buffs.Count == 1, "same source refreshes instead of stacking");
        buffSim.RemoveRobot(buffer.Id);
        Assert(recipient.Range == recipient.Spec.range && recipient.Damage == recipient.Spec.damage, "buff cleanup restores original values");

        var win = new DefenseSimulation(Setup(health: 1));
        var winner = Place(win, RobotRole.Shooter, -8, 2); win.Start(); Advance(win, .3);
        Assert(win.Phase == BattlePhase.Victory && win.Kills == 1 && win.Leaks == 0, "kill-only final wave victory");
        int events = win.Events.Count; Advance(win, 2);
        Assert(events == win.Events.Count, "result generates no extra events");
        var loss = new DefenseSimulation(Setup(speed: 200, baseHealth: 1)); loss.Start(); Advance(loss, 1);
        Assert(loss.Phase == BattlePhase.Defeat && loss.Leaks == 1 && loss.Kills == 0, "final leak prioritizes defeat");

        var normal = new DefenseSimulation(Setup(count: 5)); var fast = new DefenseSimulation(Setup(count: 5));
        normal.Start(); fast.Start();
        for (int i = 0; i < 120; i++) normal.Advance(1.0 / 60);
        for (int i = 0; i < 60; i++) fast.Advance(2.0 / 60);
        Assert(normal.Time == fast.Time && normal.Spawned == fast.Spawned && normal.Enemies.Zip(fast.Enemies, (a,b) => a.Position == b.Position).All(v => v), "1x/2x deterministic equal battle time");
        var clean = new DefenseSimulation(normal.Setup);
        Assert(clean.Time == 0 && clean.Robots.Count == 0 && clean.Enemies.Count == 0 && clean.Kills == 0 && clean.Phase == BattlePhase.Ready, "new session clears runtime state");

        var setup = Setup();
        var reduced = new DefenseSimulation(setup); var trainee = Place(reduced, RobotRole.Warrior, -8, 2); trainee.AutoExecute = false;
        reduced.Start(); Advance(reduced, .2); reduced.RequestAction(trainee.Id, RobotAction.Attack);
        Assert(reduced.Enemies[0].Health == 99.5f, "cross-role action uses 10 percent of specialist damage");
        bool rejected = false;
        setup.Permissions = new[] { new ActionPermission { role = RobotRole.Tank, action = RobotAction.Buff, strength = .5f } };
        try { new DefenseSimulation(setup); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "confirmed forbidden combinations cannot be overridden");
        Assert(Rules.SegmentDistance(new Vector2(0,2), new Vector2(-1,0), new Vector2(1,0)) == 2, "continuous segment distance");

        var invalidBounds = Setup(); invalidBounds.Min.X = float.NaN;
        rejected = false;
        try { new DefenseSimulation(invalidBounds); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "non-finite placement bounds rejected");
        var obstructed = Setup(); obstructed.Obstacles = new[] { new PlacementObstacle { Min = new Vector2(-4,2), Max = new Vector2(-2,4) } };
        var obstacleSim = new DefenseSimulation(obstructed);
        Assert(obstacleSim.Place(Spec(RobotRole.Warrior), 1, new Vector2(-4.2f,3), out _) == null, "robot footprint cannot overlap obstacle");
        Assert(obstacleSim.Place(Spec(RobotRole.Warrior), 1, new Vector2(-5,3), out _) != null, "free space next to obstacle is allowed");

        var boundarySim = new DefenseSimulation(Setup(health: 1000));
        var boundaryBot = Place(boundarySim, RobotRole.Shooter, -8, 5); boundaryBot.AutoExecute = false;
        boundarySim.Start(); Advance(boundarySim, .2);
        var boundaryEnemy = boundarySim.Enemies.Single(); boundaryEnemy.Position = new Vector2(-8,0);
        Assert(boundarySim.RequestAction(boundaryBot.Id, RobotAction.Attack, boundaryEnemy.Id) == ActionResult.Executed, "exact range boundary included");
        boundaryBot.NextAction = 0; boundaryEnemy.Position = new Vector2(-8,-.001f);
        Assert(boundarySim.RequestAction(boundaryBot.Id, RobotAction.Attack, boundaryEnemy.Id) == ActionResult.NoTarget, "just outside range excluded");

        var stunSim = new DefenseSimulation(Setup()); var stunBot = Place(stunSim, RobotRole.Tank, -8, 0); stunBot.AutoExecute = false;
        stunSim.Start(); Advance(stunSim, .2);
        var stunEnemy = stunSim.Enemies.Single(); stunSim.RequestAction(stunBot.Id, RobotAction.Block);
        Assert(stunEnemy.Stunned(stunSim.Time) && stunEnemy.Health == 95, "sample block combines area stun with single-target low damage");

        var multi = Setup(health: 1); multi.Waves = new[] { multi.Waves[0], multi.Waves[0], multi.Waves[0] };
        var multiSim = new DefenseSimulation(multi); Place(multiSim, RobotRole.Shooter,-8,2); multiSim.Start(); Advance(multiSim, 1);
        Assert(multiSim.Phase == BattlePhase.Victory && multiSim.Spawned == 3 && multiSim.Kills == 3 && multiSim.WaveIndex == 2, "three waves finish without duplicate spawns");

        var slowSim = new DefenseSimulation(Setup(health: 1000)); var utility = Place(slowSim,RobotRole.Utility,-8,2); utility.AutoExecute = false;
        slowSim.Start(); Advance(slowSim,.2); slowSim.RequestAction(utility.Id,RobotAction.Slow);
        var slowed = slowSim.Enemies.Single();
        Assert(slowed.Health == 995 && slowed.Speed(slowSim.Time) == .5f, "slow command deals sample damage and reduces speed");
        Advance(slowSim,2.1);
        Assert(slowed.Speed(slowSim.Time) == 1 && slowed.Effects.Count == 0, "slow expires and restores original speed");

        var expirySim = new DefenseSimulation(Setup(health: 1000)); var expiryBuffer = Place(expirySim,RobotRole.Buffer,-5,3); expiryBuffer.AutoExecute = false;
        var expiryRecipient = Place(expirySim,RobotRole.Warrior,-7,3); expiryRecipient.AutoExecute = false;
        expirySim.Start(); Advance(expirySim,.2); expirySim.RequestAction(expiryBuffer.Id,RobotAction.Buff); Advance(expirySim,2.1);
        Assert(expiryRecipient.Buffs.Count == 0 && expiryRecipient.Range == expiryRecipient.Spec.range && expiryRecipient.Interval == expiryRecipient.Spec.interval, "buff expiration restores baseline without removing source");

        var capacitySim = new DefenseSimulation(Setup(health: 1000,speed:10,count:5)); var capacityTank = Place(capacitySim,RobotRole.Tank,-8,0); capacityTank.AutoExecute = false;
        capacitySim.Start(); Advance(capacitySim,1);
        Assert(capacitySim.Enemies.Count(e=>e.BlockedBy==capacityTank.Id)==3 && capacitySim.Enemies.Count(e=>e.Position.X>-8)==2, "capacity overflow passes while three enemies remain blocked");
        Console.WriteLine($"Defense checks passed: {checks}");
        ProgramCheck.Run();
        CampaignCheck.Run();
    }
}
