using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// The melee robot's slash(): no target, it cuts all the way around the robot; with nobody in range it still swings (and
// spends its cooldown), so `for i in range(n): slash()` is n swings right away (play feedback 4: Play calls it again).
static class MeleeCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static CodeBlock N(string n) => new CodeBlock(BlockKind.Number, n);
    static void Tick(DefenseSimulation sim, double seconds) { for (int i = 0; i < (int)(seconds * 60); i++) sim.Advance(1.0 / 60); }
    static DefenseSimulation Make(Route[] routes, params SpawnGroup[] wave) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r, damage = 20, interval = .5f, range = 3 }).ToArray(),
        Routes = routes, Waves = new[] { wave } });
    static RobotState Warrior(DefenseSimulation sim, float x, float y)
        => sim.Place(new RobotSpec { role = RobotRole.Warrior, range = 3, damage = 20, interval = .5f }, (int)RobotRole.Warrior, new Vector2(x, y), out _);
    static CodeBlock Slash() => new CodeBlock(BlockKind.Slash);
    static CodeBlock Loop(int times, params CodeBlock[] body) { var loop = new CodeBlock(BlockKind.For, "0", N(times.ToString())); loop.Body.AddRange(body); return loop; }

    public static void Run()
    {
        Assert(PythonTreeCompiler.Compile("robot_1", new[] { Slash() }).Contains("slash()"), "slash() needs no target and compiles to slash()");

        // Two enemies walking at each other on two routes meet under the robot: one swing cuts both, even the one behind it.
        var line = new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1);
        var back = new Route(new[] { new Vector2(10, 0), new Vector2(-10, 0) }, 1);
        var sim = Make(new[] { line, back },
            new SpawnGroup { Enemy = new EnemySpec { health = 10000, speed = 1 }, Count = 1, Interval = 1, RouteIndex = 0 },
            new SpawnGroup { Enemy = new EnemySpec { health = 10000, speed = 1 }, Count = 1, Interval = 1, RouteIndex = 1 });
        var bot = Warrior(sim, 0, 2f);
        sim.ApplyProgram(bot.Id, "once", new[] { new CodeBlock(BlockKind.Wait, "0", N("8.5")), Slash(), new CodeBlock(BlockKind.Wait, "0", N("100")) });
        sim.Start(); Tick(sim, 9);
        Assert(bot.Executions == 1 && sim.Enemies.Count(e => e.Health < 10000) == 2, $"one slash() hits every enemy in range all the way around the robot: exec={bot.Executions} hurt={sim.Enemies.Count(e => e.Health < 10000)} pos={string.Join(",", sim.Enemies.Select(e => e.Position.X.ToString("0.0")))}");

        // Enemies far away: both swings go at once into the air, and Play with an enemy near lands them.
        sim = Make(new[] { line }, new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1 }, Count = 2, Interval = 5 });
        bot = Warrior(sim, 0, 2f);
        sim.ApplyProgram(bot.Id, "twice", new[] { Loop(2, Slash()) });
        sim.Start(); Tick(sim, 1);
        Assert(bot.Executions == 2 && sim.Kills == 0 && !bot.Program.Running, "slash() swings at nobody instead of waiting; the function then ends");
        Tick(sim, 8);
        Assert(sim.Invoke(bot.Id), "Play with the first enemy near");
        Tick(sim, 1.5);
        Assert(sim.Kills == 1, "Play's swings cut the enemy in range");

        // The same with the swing behind an if: a turn whose condition is false waits instead of using up the loop.
        sim = Make(new[] { line }, new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1 }, Count = 2, Interval = 5 });
        bot = Warrior(sim, 0, 2f);
        var find = new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy));
        var near = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, "<=",
            new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, "enemy"), new CodeBlock(BlockKind.MyPos)), new CodeBlock(BlockKind.MyAttackRange)));
        near.Body.Add(Slash());
        sim.ApplyProgram(bot.Id, "guarded", new[] { Loop(2, find, near) });
        sim.Start(); Tick(sim, 1);
        Assert(bot.Executions == 0 && bot.LoopTurnsThisWave == 2 && !bot.Program.Running && bot.Program.Fault == null,
            "a loop turn whose if is false is spent (synchronous: nothing waits for the condition)");

        Console.WriteLine($"Melee checks passed: {checks}");
    }
}
