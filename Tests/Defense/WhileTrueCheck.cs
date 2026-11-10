using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// The planner's always-on robots (play feedback 4): `while True:` keeps one target until it is dead, then takes the nearest;
// the melee robot swings every turn, the ranged one shoots only inside 0.3 of its range. An enemy name not assigned yet is
// no enemy, and no enemy is dead, so the first turn picks a target.
static class WhileTrueCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static void Tick(DefenseSimulation sim, double seconds) { for (int i = 0; i < (int)(seconds * 60); i++) sim.Advance(1.0 / 60); }
    static CodeBlock B(BlockKind kind, string value = "0", params CodeBlock[] arguments) => new CodeBlock(kind, value, arguments);
    static CodeBlock Enemy => B(BlockKind.Variable, "enemy");
    static CodeBlock With(CodeBlock block, params CodeBlock[] body) { block.Body.AddRange(body); return block; }
    // while True:
    //     if enemy.is_dead():
    //         enemy = get_nearest_enemy()
    //     <then>
    static CodeBlock[] Forever(CodeBlock then) => new[] { With(B(BlockKind.While),
        With(B(BlockKind.If, "0", B(BlockKind.IsDead, "0", Enemy)), B(BlockKind.DeclareVariable, "enemy", B(BlockKind.NearestEnemy))),
        then) };
    static DefenseSimulation Make(params SpawnGroup[] wave) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r, damage = 20, interval = .5f, range = 3 }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) }, Waves = new[] { wave } });

    public static void Run()
    {
        var melee = Forever(B(BlockKind.Slash));
        Assert(PythonTreeCompiler.Compile("robot_1", melee) ==
            "def robot_1():\n    while True:\n        if enemy.is_dead():\n            enemy = get_nearest_enemy()\n        slash()\n",
            "the melee loop compiles to the planner's Python");
        var near = B(BlockKind.Comparison, "<", B(BlockKind.Distance, "0", Enemy, B(BlockKind.MyPos)),
            B(BlockKind.Arithmetic, "*", B(BlockKind.MyAttackRange), B(BlockKind.Number, "0.3")));
        var ranged = Forever(With(B(BlockKind.If, "0", near), B(BlockKind.Shot, "0", Enemy)));
        Assert(PythonTreeCompiler.Compile("robot_2", ranged).Contains(
            "        if enemy.distance(get_my_pos()) < my_attack_range() * 0.3:\n            attack(enemy)\n"),
            "after `if enemy.is_dead(): enemy = ...` the name is an enemy, so distance and attack compile");

        // Still refused: an enemy read before any assignment other than through is_dead(), and is_dead() on a number.
        Assert(Throws(() => PythonTreeCompiler.Compile("r", new[] { With(B(BlockKind.While), With(B(BlockKind.If, "0", near), B(BlockKind.Shot, "0", Enemy))) })),
            "enemy.distance() before enemy is assigned does not compile");
        Assert(Throws(() => PythonTreeCompiler.Compile("r", new[] { B(BlockKind.DeclareVariable, "n", B(BlockKind.Number, "3")),
            With(B(BlockKind.If, "0", B(BlockKind.IsDead, "0", B(BlockKind.Variable, "n")))) })), "is_dead() on a number does not compile");
        Assert(PythonTreeCompiler.Compile("r", new[] { B(BlockKind.DeclareVariable, "x", B(BlockKind.Arithmetic, "*",
            B(BlockKind.Arithmetic, "+", B(BlockKind.Number, "1"), B(BlockKind.Number, "2")), B(BlockKind.Number, "3"))) }).Contains("x = (1 + 2) * 3"),
            "a calculation inside a calculation keeps its brackets");

        // Melee: three enemies five seconds apart all fall to the one robot that never stops.
        var sim = Make(new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1 }, Count = 3, Interval = 5 });
        var bot = sim.Place(new RobotSpec { role = RobotRole.Warrior, range = 3, damage = 20, interval = .5f }, (int)RobotRole.Warrior, new Vector2(0, 2), out _);
        sim.ApplyProgram(bot.Id, "melee", melee);
        sim.Start(); Tick(sim, 40);
        Assert(sim.Kills == 3 && sim.Leaks == 0 && bot.Program.Fault == null, $"while True + slash() keeps cutting: kills={sim.Kills} leaks={sim.Leaks} fault={bot.Program.Fault}");

        // Ranged, range 10: it holds fire until the enemy is inside 3 (0.3 of the range).
        sim = Make(new SpawnGroup { Enemy = new EnemySpec { health = 1000, speed = 1 }, Count = 1, Interval = 1 });
        bot = sim.Place(new RobotSpec { role = RobotRole.Shooter, range = 10, damage = 20, interval = .5f, projectileSpeed = 100 }, (int)RobotRole.Shooter, new Vector2(0, 2), out _);
        sim.ApplyProgram(bot.Id, "ranged", ranged);
        sim.Start();
        float first = -1;
        for (int i = 0; i < 60 * 30 && first < 0; i++)
        {
            sim.Advance(1.0 / 60);
            if (bot.Executions > 0) first = Vector2.Distance(sim.Enemies.First().Position, bot.Position);
        }
        Assert(first > 0 && first < 3.05f && bot.Program.Fault == null, $"the ranged loop first shoots inside 0.3 of its range (first shot at {first:0.00})");

        // Why 0.3: a shot does the most (3x) from a third of the range in, 1x at the edge.
        Assert(Rules.RangedDamageFactor(9, 1) == 3 && Rules.RangedDamageFactor(9, 3) == 3 && Rules.RangedDamageFactor(9, 6) == 1.5f &&
            Rules.RangedDamageFactor(9, 9) == 1, "ranged damage peaks inside a third of the range");

        // Dividing by zero stops that robot's code with a message instead of breaking the battle.
        sim = Make(new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1 }, Count = 1, Interval = 1 });
        bot = sim.Place(new RobotSpec { role = RobotRole.Warrior, range = 3, damage = 20, interval = .5f }, (int)RobotRole.Warrior, new Vector2(0, 2), out _);
        sim.ApplyProgram(bot.Id, "zero", new[] { B(BlockKind.DeclareVariable, "x", B(BlockKind.Arithmetic, "/", B(BlockKind.Number, "1"), B(BlockKind.Number, "0"))), B(BlockKind.Slash) });
        sim.Start(); Tick(sim, 20);
        Assert(bot.Program.Fault != null && bot.Program.Fault.Contains("0으로"), "x = 1 / 0 faults with a message: " + bot.Program.Fault);

        Console.WriteLine($"While-true checks passed: {checks}");
    }
    static bool Throws(Action action) { try { action(); return false; } catch (FormatException) { return true; } }
}
