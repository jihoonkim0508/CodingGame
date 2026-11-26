using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// 탱커 로봇 (2026-11-26): it stands beside the road like any robot and holds the road within blockRadius; the ones behind the
// held enemy queue; held enemies wear its guard (a tank-type faster, a stunned one not at all) until it is knocked back, they
// walk on, and it stands up full; a boss is slowed, not held. The buff is gone.
static class TankCheck
{
    static int checks;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static RobotSpec Tank() => new RobotSpec { role = RobotRole.Tank, range = 2.6f, damage = 2, interval = 2.5f, radius = .75f, blockRadius = 2.2f, guard = 10, knockback = 3, duration = 1.2f };
    static RobotSpec Other(RobotRole role) => new RobotSpec { role = role, range = 5, damage = 5, interval = 1 };
    static BattleSetup Setup(int count, EnemyType type = EnemyType.Normal, bool boss = false) => new BattleSetup {
        Min = new Vector2(-14, -6), Max = new Vector2(14, 6), BaseHealth = 20,
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => r == RobotRole.Tank ? Tank() : Other(r)).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-12, 0), new Vector2(12, 0) }, .8f) },
        Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { type = type, health = 1000, speed = 2 }, Count = count, Interval = .5f, Boss = boss } } } };
    static void Advance(DefenseSimulation sim, double seconds) { for (int i = 0; i < (int)Math.Round(seconds * 60); i++) sim.Advance(1.0 / 60); }
    static RobotState Place(DefenseSimulation sim, Vector2 at) => sim.Place(Tank(), (int)RobotRole.Tank, at, out var error) ?? throw new Exception(error);

    public static void Run()
    {
        Assert((int)RobotRole.Utility == 0 && (int)RobotRole.Tank == 3 && Rules.RoleCount == 4, "four roles, Utility keeps 0, Tank appended at 3");
        Assert(Rules.NativeAction(RobotRole.Tank) == RobotAction.Block && Rules.NativeAction(RobotRole.Utility) == RobotAction.Slow &&
            Rules.AttackClass(RobotRole.Tank) && Rules.RoleName(RobotRole.Tank) == "탱커 로봇" && Rules.RoleName(RobotRole.Utility) == "유틸 로봇", "tank bashes (공격류), utility slows");
        // Placed like any robot: not on the road; just beside it.
        var held = new DefenseSimulation(Setup(3));
        Assert(held.PlacementError(Tank(), new Vector2(0, .5f)) != null, "the tank is not placed on the road");
        var tank = Place(held, new Vector2(0, 1.6f));
        Assert(tank.Guard == 10 && tank.Holding(0), "placed with a full guard, holding");
        held.Start(); Advance(held, 5.5); // the road enters the zone 10.5 from the spawn, 5.25 s at speed 2
        var front = held.Enemies.OrderBy(e => held.RemainingPath(e)).First();
        float edge = -(float)Math.Sqrt(2.2 * 2.2 - 1.6 * 1.6);
        Assert(front.BlockedBy == tank.Id && !front.Queued && Math.Abs(front.Position.X - edge) < .05f, $"the first enemy stops where the road enters the hold zone (x {front.Position.X:0.00}, edge {edge:0.00})");
        Assert(held.Events.Any(e => e.Kind == "hold" && e.Source == tank.Id) && tank.HeldThisWave == 1, "a hold event and the count for the debrief");
        Advance(held, 1);
        var line = held.Enemies.OrderBy(e => held.RemainingPath(e)).ToArray();
        Assert(line.Length == 3 && line.All(e => e.BlockedBy == tank.Id) && line.Skip(1).All(e => e.Queued), "the two behind queue, held by the same tank");
        Assert(line.Zip(line.Skip(1), (a, b) => held.RemainingPath(b) - held.RemainingPath(a)).All(gap => gap >= DefenseSimulation.QueueGap - .05f && gap < DefenseSimulation.QueueGap + .05f),
            "the line waits QueueGap apart");
        // Three held wear 3 a second: the guard breaks about 10/3 s after the line formed, the three walk on.
        float before = tank.Guard; Advance(held, .5);
        Assert(Math.Abs(before - tank.Guard - 1.5f) < .1f, $"three held wear 3 a second ({before - tank.Guard:0.00} in .5 s)");
        double broke = -1;
        for (int i = 0; i < 600 && broke < 0; i++) { held.Advance(1.0 / 60); if (!tank.Holding(held.Time)) broke = held.Time; }
        Assert(broke > 0 && held.Events.Any(e => e.Kind == "guard-break") && tank.KnockedThisWave == 1, "the guard runs out: knocked back");
        Assert(held.Enemies.All(e => e.BlockedBy == 0) && Math.Abs(tank.KnockedUntil - broke - 3) < .02, "knocked back 3 s, the held ones let go");
        float x = held.Enemies.Max(e => e.Position.X); Advance(held, 1);
        Assert(held.Enemies.Max(e => e.Position.X) > x + 1.5f && tank.Guard > 2 && tank.Guard < 5, $"they walk past while the guard fills again ({tank.Guard:0.0})");
        Advance(held, 2.1);
        Assert(tank.Holding(held.Time) && Math.Abs(tank.Guard - tank.MaxGuard) < .01f, "it stands up with its guard full");

        // A tank-type enemy wears 2.5 a second; a stunned one (block()) nothing.
        var heavy = new DefenseSimulation(Setup(1, EnemyType.Tank)); var wall = Place(heavy, new Vector2(0, 1.6f));
        heavy.Start(); Advance(heavy, 6); before = wall.Guard; Advance(heavy, .4);
        Assert(heavy.Enemies.Single().BlockedBy == wall.Id && Math.Abs(before - wall.Guard - 1) < .05f, $"a tank-type enemy wears 2.5 a second ({(before - wall.Guard) / .4f:0.00})");
        heavy.Enemies.Single().Shield = 12;
        Assert(heavy.RequestAction(wall.Id, RobotAction.Block) == ActionResult.Executed && heavy.Enemies.Single().Stunned(heavy.Time) && heavy.Enemies.Single().Shield == 0,
            "block() bashes the held enemy: stunned, its shield knocked off");
        before = wall.Guard; Advance(heavy, 1);
        Assert(Math.Abs(wall.Guard - before) < 1e-4f, "a stunned enemy neither wears the guard nor lets it recover");

        // A boss is not held, only slowed while at the shield.
        var boss = new DefenseSimulation(Setup(1, boss: true)); var stand = Place(boss, new Vector2(0, 1.6f));
        boss.Start(); Advance(boss, 5.5);
        var big = boss.Enemies.Single();
        Assert(big.BlockedBy == 0 && Vector2.Distance(big.Position, stand.Position) <= 2.2f && Math.Abs(big.Speed(boss.Time) - 1) < .01f && stand.Guard < 10,
            $"the boss walks on at half speed through the zone, wearing the guard (speed {big.Speed(boss.Time):0.00}, guard {stand.Guard:0.0})");

        // Two tanks at most (DefenseBalanceRules.roleLimits).
        var many = new DefenseSimulation(Setup(1)); Place(many, new Vector2(-6, 1.6f)); Place(many, new Vector2(6, 1.6f));
        Assert(many.Place(Tank(), (int)RobotRole.Tank, new Vector2(0, -1.6f), out var limit) == null && limit.Contains("탱커 로봇"), "a third tank is refused: " + limit);

        // The buff is gone: no action, no word, no block.
        var plain = new DefenseSimulation(Setup(1)); var utility = plain.Place(Other(RobotRole.Utility), 0, new Vector2(0, 3), out _); plain.Start();
        Assert(plain.RequestAction(utility.Id, RobotAction.Buff) == ActionResult.Unavailable, "RequestAction(Buff) does nothing");
        bool refused = false; try { PythonTreeCompiler.Compile("r", new[] { new CodeBlock(BlockKind.Buff) }); } catch (FormatException) { refused = true; }
        Assert(refused && !DefenseProgression.ItemKinds.Contains(BlockKind.Buff) && !DefenseProgression.ItemKinds.Contains(BlockKind.BuffRobot), "a buff block does not compile and is no item");
        Console.WriteLine($"Tank checks passed: {checks} (guard broke {broke:0.0} s into the wave)");
    }
}
