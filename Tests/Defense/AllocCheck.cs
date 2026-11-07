using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// 13차 129/130: garbage made per simulation tick in a busy battle (4 robots running while True, 12 enemies on the road).
// Ticks run at 60 Hz, 180 per second at 3x speed, so per-tick garbage becomes the frame's GC pressure.
static class AllocCheck
{
    // ponytail: budgets, not zero. The simulation's own share (robots acting without code: 58.6 KB per tick with code before
    // 129, 3.2 KB of it the simulation's after); the code interpreter's share is 130's.
    public const long MaxSimBytesPerTick = 4096;
    static CodeBlock B(BlockKind kind, string value = "0", params CodeBlock[] arguments) => new CodeBlock(kind, value, arguments);
    static CodeBlock With(CodeBlock block, params CodeBlock[] body) { block.Body.AddRange(body); return block; }

    public static long BytesPerTick(bool withCode = true)
    {
        var sim = new DefenseSimulation(new BattleSetup {
            Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r, damage = 4, interval = .5f, range = 3 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = 400, speed = .4f }, Count = 12, Interval = .4f } } } });
        var enemy = B(BlockKind.Variable, "enemy");
        // while True: enemy = get_nearest_enemy(); d = enemy.distance(get_my_pos()); if d < my_attack_range() * 0.9: attack(enemy) / slash()
        CodeBlock[] Code(bool melee) => new[] { With(B(BlockKind.While),
            B(BlockKind.DeclareVariable, "enemy", B(BlockKind.NearestEnemy)),
            B(BlockKind.DeclareVariable, "d", B(BlockKind.Distance, "0", enemy, B(BlockKind.MyPos))),
            With(B(BlockKind.If, "0", B(BlockKind.Comparison, "<", B(BlockKind.Variable, "d"),
                B(BlockKind.Arithmetic, "*", B(BlockKind.MyAttackRange), B(BlockKind.Number, "0.9")))),
                melee ? B(BlockKind.Slash) : B(BlockKind.Shot, "0", enemy))) };
        for (int i = 0; i < 4; i++)
        {
            bool melee = i % 2 == 0;
            var spec = new RobotSpec { role = melee ? RobotRole.Warrior : RobotRole.Shooter, range = melee ? 3 : 7, damage = 4, interval = .5f, projectileSpeed = 12 };
            var bot = sim.Place(spec, (int)spec.role, new Vector2(-9 + i * 5, 2.2f), out _);
            if (withCode) sim.ApplyProgram(bot.Id, "bot" + i, Code(melee)); else bot.AutoExecute = true;
        }
        sim.Start();
        for (int i = 0; i < 600; i++) sim.Advance(1.0 / 60);   // warm up: every enemy on the road, every robot busy
        const int ticks = 1200;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < ticks; i++) sim.Advance(1.0 / 60);
        long perTick = (GC.GetAllocatedBytesForCurrentThread() - before) / ticks;
        if (sim.Enemies.Count() == 0) throw new Exception("AllocCheck: the road emptied; the measurement is not of a busy battle");
        return perTick;
    }

    public static void Run()
    {
        long simOnly = BytesPerTick(false), withCode = BytesPerTick();
        Console.WriteLine($"Simulation garbage per tick: {simOnly} bytes without code, {withCode} with while True code ({withCode * 180 / 1024} KB/s at 3x)");
        if (simOnly > MaxSimBytesPerTick) throw new Exception($"AllocCheck: the simulation makes {simOnly} bytes per tick, budget {MaxSimBytesPerTick}");
    }
}
