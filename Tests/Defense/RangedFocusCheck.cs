using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// The ranged robot's focus code (Docs/원거리_집중공격_설계.md): it skips every enemy that is not a tank, then keeps shooting the
// one tank until it is dead or out of range. Read from the Python text the way a tutorial answer is.
static class RangedFocusCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static CodeBlock B(BlockKind kind, string value = "0", params CodeBlock[] arguments) => new CodeBlock(kind, value, arguments);
    static bool Throws(Action action) { try { action(); return false; } catch (FormatException) { return true; } }

    public const string Focus =
        "while True:\n" +
        "    enemy = get_nearest_enemy()\n" +
        "    if enemy.type != Enemy.Tank:\n" +
        "        continue\n" +
        "    while True:\n" +
        "        if enemy.is_dead():\n" +
        "            break\n" +
        "        if enemy.distance(get_my_pos()) > my_attack_range():\n" +
        "            break\n" +
        "        shoot(enemy)\n";

    // Two normal enemies walk past first, a tank behind them; the robot stands 2 off the road with range 10.
    public sealed class Run
    {
        public readonly List<float> ShotDistances = new List<float>();
        public float TankDamage, NormalDamage;
        public string Fault;
    }
    public static Run Simulate(string code)
    {
        var sim = new DefenseSimulation(new BattleSetup {
            Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r, damage = 20, interval = .5f, range = 10 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) },
            Waves = new[] { new[] {
                new SpawnGroup { Enemy = new EnemySpec { type = EnemyType.Normal, health = 50, speed = 1 }, Count = 2, Interval = 2 },
                new SpawnGroup { Enemy = new EnemySpec { type = EnemyType.Tank, health = 100000, speed = 1 }, Count = 1, Interval = 1, Delay = 4 } } } });
        var bot = sim.Place(new RobotSpec { role = RobotRole.Shooter, range = 10, damage = 20, interval = .5f, projectileSpeed = 100 }, (int)RobotRole.Shooter, new Vector2(0, 2), out _);
        sim.ApplyProgram(bot.Id, "ranged", TutorialAnswers.Parse(code));
        sim.Start();
        var run = new Run();
        var full = new Dictionary<int, float>();
        int shots = 0;
        for (int i = 0; i < 60 * 30; i++) // the tank leaves the range at 24 s, before it reaches the core
        {
            sim.Advance(1.0 / 60);
            foreach (var e in sim.Enemies.Where(e => e.Active))
            {
                if (!full.ContainsKey(e.Id)) full[e.Id] = e.Health;
                float lost = full[e.Id] - e.Health;
                if (e.Spec.type == EnemyType.Tank) run.TankDamage = lost; else run.NormalDamage = Math.Max(run.NormalDamage, lost);
            }
            if (bot.Executions > shots)
            {
                shots = bot.Executions;
                var tank = sim.Enemies.FirstOrDefault(e => e.Active && e.Spec.type == EnemyType.Tank);
                run.ShotDistances.Add(tank == null ? -1 : Vector2.Distance(tank.Position, bot.Position));
            }
        }
        run.Fault = bot.Program.Fault;
        return run;
    }

    public static void RunAll()
    {
        Assert(PythonTreeCompiler.EnemyTypes.SequenceEqual(Enum.GetNames(typeof(EnemyType))), "Enemy.<name> lists the game's enemy kinds in their order");

        var focus = TutorialAnswers.Parse(Focus);
        Assert(PythonTreeCompiler.Compile("robot", focus) == "def robot():\n" + string.Concat(Focus.Split('\n').Where(l => l.Length > 0).Select(l => "    " + l + "\n")),
            "the focus code compiles back to the same Python");
        Assert(!PythonTreeCompiler.HasStaleEnemyLoop(focus), "a loop that breaks on enemy.is_dead() is not warned as aiming at a gone target");
        Assert(PythonTreeCompiler.HasStaleEnemyLoop(TutorialAnswers.Parse("enemy = get_nearest_enemy()\nwhile True:\n    shoot(enemy)")),
            "a loop that never looks at its target again is still warned");

        var enemy = B(BlockKind.DeclareVariable, "enemy", B(BlockKind.NearestEnemy));
        CodeBlock[] If(CodeBlock test) { var branch = B(BlockKind.If, "0", test); branch.Body.Add(B(BlockKind.Shot, "0", B(BlockKind.Variable, "enemy"))); return new[] { enemy, branch }; }
        var type = B(BlockKind.EnemyTypeOf, "0", B(BlockKind.Variable, "enemy"));
        Assert(Throws(() => PythonTreeCompiler.Compile("r", If(B(BlockKind.Comparison, "!=", type, B(BlockKind.Number, "2"))))), "enemy.type != 2 does not compile");
        Assert(Throws(() => PythonTreeCompiler.Compile("r", If(B(BlockKind.Comparison, ">", type, B(BlockKind.EnemyTypeName, "Fast"))))), "kinds have no order");
        Assert(Throws(() => PythonTreeCompiler.Compile("r", If(B(BlockKind.Comparison, "==", type, B(BlockKind.EnemyTypeName, "Boss"))))), "Enemy.Boss is no kind");
        Assert(Throws(() => PythonTreeCompiler.Compile("r", new[] { B(BlockKind.DeclareVariable, "n", B(BlockKind.Number, "3")),
            B(BlockKind.DeclareVariable, "k", B(BlockKind.EnemyTypeOf, "0", B(BlockKind.Variable, "n"))) })), "type on a number does not compile");
        Assert(DefenseProgression.Canonical(BlockKind.EnemyTypeOf) == BlockKind.Variable && DefenseProgression.ItemKinds.Contains(BlockKind.EnemyTypeName),
            "enemy.type is the variable's member (no item of its own); Enemy.Tank is an item");

        var run = Simulate(Focus);
        Assert(run.Fault == null, "the focus code runs: " + run.Fault);
        Assert(run.NormalDamage == 0, $"normal enemies are skipped (lost {run.NormalDamage})");
        Assert(run.ShotDistances.Count > 0 && run.ShotDistances.All(d => d > 0 && d <= 10.01f) && run.TankDamage > 0,
            $"the tank is shot inside the range ({run.ShotDistances.Count} shots, {run.TankDamage} damage)");

        Console.WriteLine($"Ranged focus checks passed: {checks}");
    }
}
