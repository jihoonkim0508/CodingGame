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

        // The sword hits who is inside the range ring both when the swing starts and when it lands (the user, 2026-11-20: a
        // blind swing at an empty ring cut enemies that walked in during it). A runs into the ring during the swing, B runs
        // out of it, C (the other way) is inside at both moments: only C is hit.
        sim = Make(new[] { line, back }, new SpawnGroup { Enemy = new EnemySpec { health = 10000, speed = 4 }, Count = 2, Interval = 1.15f },
            new SpawnGroup { Enemy = new EnemySpec { health = 10000, speed = 4 }, Count = 1, Delay = .5f, Interval = 1, RouteIndex = 1 });
        bot = Warrior(sim, 0, 2f);
        sim.ApplyProgram(bot.Id, "idle", new[] { new CodeBlock(BlockKind.Wait, "0", N("100")) });
        sim.Start();
        while (sim.Enemies.Count < 3 || sim.Enemies[0].Position.X < 2) sim.Advance(1.0 / 60);
        EnemyState leaving = sim.Enemies[0], staying = sim.Enemies[1], coming = sim.Enemies[2];
        float Dist(EnemyState e) => Vector2.Distance(e.Position, bot.Position);
        Assert(Dist(leaving) <= bot.Range && Dist(staying) <= bot.Range && Dist(coming) > bot.Range,
            $"setup: B and C inside, A outside at the swing {Dist(leaving):0.00} {Dist(staying):0.00} {Dist(coming):0.00}");
        Assert(sim.RequestAction(bot.Id, RobotAction.Slash) == ActionResult.Executed, "the swing starts");
        double lands = sim.Time + .5 * .4;
        while (sim.Time + 1e-8 < lands) sim.Advance(1.0 / 60);
        Assert(Dist(leaving) > bot.Range && Dist(staying) <= bot.Range && Dist(coming) <= bot.Range,
            $"setup: B outside, A and C inside when the sword lands {Dist(leaving):0.00} {Dist(staying):0.00} {Dist(coming):0.00}");
        Assert(staying.Health < 10000 && coming.Health == 10000 && leaving.Health == 10000,
            $"the strike hits only the enemy inside the ring at both moments: A={coming.Health} B={leaving.Health} C={staying.Health}");

        // 2-3's preview, as DefenseAttackPreview builds it (TimeMeleeDemo): `while True: slash()` swings at air as the enemy
        // walks in (that strike lands on it and misses) and lets it through with one strike (2-3 B01 '허공을 베고 쉬는 사이
        // 지나가 버렸어'), the if-guarded slash fells it (D07 '들어오자마자 벴어').
        foreach (bool guarded in new[] { false, true })
        foreach (float waveSpeed in new[] { 1.1f, 1.6f, 2.4f }) // the wave's own pace no longer matters
        {
            var demo = new SpawnGroup { Enemy = new EnemySpec { health = 10.7f, speed = waveSpeed }, Count = 1, Interval = 1.4f };
            DefenseSimulation.TimeMeleeDemo(demo, 3.6f, 1.8f, 7, 1.6f, 1.6f * .4f, 7);
            sim = new DefenseSimulation(new BattleSetup {
                Progression = FixtureProgression.Create(), Min = new Vector2(-50, -50), Max = new Vector2(50, 50), BaseHealth = 100,
                ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r, damage = 7, interval = 1.6f, range = 3.6f }).ToArray(),
                Routes = new[] { new Route(new[] { new Vector2(7, 0), new Vector2(-20, 0) }, .75f) }, Waves = new[] { new[] { demo } } });
            bot = sim.Place(new RobotSpec { role = RobotRole.Warrior, range = 3.6f, damage = 7, interval = 1.6f }, (int)RobotRole.Warrior, new Vector2(0, 1.8f), out var error);
            Assert(bot != null, "the demo warrior stands 1.8 off the road: " + error);
            sim.ApplyProgram(bot.Id, "demo", CodingGame.Defense.Editor.TutorialAnswers.Parse(guarded
                ? "while True:\n    enemy = get_nearest_enemy()\n    if enemy.distance(get_my_pos()) <= my_attack_range():\n        slash()"
                : "while True:\n    slash()"));
            sim.Start();
            int hits = 0, misses = 0, seen = 0; float last = demo.Enemy.health; // strikes it survived (a fallen enemy leaves the list)
            var landings = new System.Collections.Generic.List<double>();
            for (int i = 0; i < 60 * 12; i++)
            {
                sim.Invoke(bot.Id); sim.Advance(1.0 / 60);
                for (; seen < sim.Events.Count; seen++) if (sim.Events[seen].Kind == "slash") landings.Add(sim.Time + sim.Events[seen].Seconds - 1e-6);
                var walker = sim.Enemies.FirstOrDefault();
                bool hurt = walker != null && walker.Health < last;
                if (hurt) { hits++; last = walker.Health; }
                // A strike that landed this tick on an enemy inside the ring without hurting it: the visible miss.
                if (landings.RemoveAll(t => t <= sim.Time) > 0 && !hurt && walker != null && walker.Active && Vector2.Distance(walker.Position, bot.Position) <= bot.Range) misses++;
            }
            Assert(guarded ? sim.Kills == 1 && hits == 1 && misses == 0 : sim.Kills == 0 && hits == 1 && misses >= 1,
                $"2-3 demo at the wave's speed {waveSpeed}: {(guarded ? "the if fells the enemy with two strikes" : "the loop's air swing lands on the enemy that walked in and misses, it strikes once and the enemy walks through")} (kills={sim.Kills} hits={hits} misses={misses})");
        }

        Console.WriteLine($"Melee checks passed: {checks}");
    }
}
