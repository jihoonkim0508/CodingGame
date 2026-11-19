using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// Audit regressions (S1-S5): each named check fails today for the reason stated and passes once that bug is fixed.
// Failures are collected, not thrown, so one run lists every red check.
static class AuditCheck
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static CodeBlock B(BlockKind kind, string value = "0", params CodeBlock[] arguments) => new CodeBlock(kind, value, arguments);
    static CodeBlock With(CodeBlock block, params CodeBlock[] body) { block.Body.AddRange(body); return block; }
    static void Tick(DefenseSimulation sim, int frames) { for (int i = 0; i < frames; i++) sim.Advance(1.0 / 60); }
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, damage = 4, range = 8, interval = .1f, projectileSpeed = 200 };
    static BattleSetup Setup(int stage, int initialWave, params SpawnGroup[][] waves) => new BattleSetup { Progression = FixtureProgression.Create(), TestOptions = new DefenseTestOptions(),
        PlayerFlow = true, Stage = stage, InitialWave = initialWave, Min = new Vector2(-20, -10), Max = new Vector2(20, 10), BaseHealth = 100,
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1.1f) },
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(), Waves = waves };
    static SpawnGroup Group(int count, float speed, float delay = 0, bool boss = false) =>
        new SpawnGroup { Count = count, Interval = .2f, Delay = delay, Boss = boss, Enemy = new EnemySpec { health = 30, speed = speed } };
    static RobotState Place(DefenseSimulation sim, RobotRole role, float x, float z)
    {
        var bot = sim.Place(Spec(role), (int)role, new Vector2(x, z), out var error);
        if (bot == null) throw new Exception("place: " + error);
        return bot;
    }

    // S1: a minion leaking in any boss wave (not only 1-5) fails the objective at once instead of idling until the timer.
    static void BossWaveLeak()
    {
        var sim = new DefenseSimulation(Setup(2, 0, new[] { Group(1, 200), Group(1, 1, 0, true) }));
        Place(sim, RobotRole.Shooter, 8, 9.5f);
        sim.Start(); Tick(sim, 120);
        Assert(sim.Leaks == 1, "the minion leaked");
        Assert(sim.Phase == BattlePhase.Defeat && sim.ObjectiveFailed, $"2 s after the leak: phase {sim.Phase}, ObjectiveFailed {sim.ObjectiveFailed}");
    }

    // S2: == / != with an infinite distance (no enemy) compare instead of faulting.
    static void InfinityComparison()
    {
        foreach (var op in new[] { "==", "!=" })
        {
            var sim = new DefenseSimulation(Setup(2, 0, new[] { Group(1, 1, 100) }));
            var bot = Place(sim, RobotRole.Warrior, -9, 2);
            var distance = B(BlockKind.Distance, "0", B(BlockKind.Variable, "enemy"), B(BlockKind.MyPos));
            sim.ApplyProgram(bot.Id, "cmp", new[] { B(BlockKind.DeclareVariable, "enemy", B(BlockKind.NearestEnemy)),
                With(B(BlockKind.If, "0", B(BlockKind.Comparison, op, distance, B(BlockKind.Number, "3"))), B(BlockKind.Slash)) });
            sim.Start(); Tick(sim, 30);
            Assert(bot.Program.Fault == null, $"infinity {op} 3 faulted: {bot.Program.Fault}");
            Assert((bot.Executions > 0) == (op == "!="), $"infinity {op} 3 took the wrong branch (executions {bot.Executions})");
        }
    }

    // S3: "쐈다!" (FirstAttack) follows the robot the lesson teaches (robot 0), also in 2-3, not any robot's first attack.
    static void FirstAttackIsTeachingRobot()
    {
        var sim = new DefenseSimulation(Setup(2, 2, new[] { Group(2, 1) }, new[] { Group(2, 1) }, new[] { Group(4, 1) }));
        foreach (var g in sim.CurrentWave) g.Enemy.health = 1000; // outlives the other robot's fire
        var teacher = Place(sim, RobotRole.Shooter, -9, 5);
        var other = Place(sim, RobotRole.Shooter, -8, 3); other.AutoExecute = true;
        var watcher = new TutorialWatcher();
        sim.Start();
        var early = new List<string>();
        for (int i = 0; i < 120; i++) { sim.Advance(1.0 / 60); early.AddRange(watcher.Poll(sim)); }
        Assert(other.AttacksThisWave > 0 && teacher.AttacksThisWave == 0, "setup: only the other robot attacked");
        Assert(!early.Contains(TutorialSignals.FirstAttack), "FirstAttack fired for another robot's attack");
        var result = sim.RequestAction(teacher.Id, RobotAction.Attack);
        Assert(result == ActionResult.Executed, "setup: the teaching robot attacks: " + result);
        Assert(watcher.Poll(sim).Contains(TutorialSignals.FirstAttack), "FirstAttack missing after the teaching robot attacked");
    }

    // S4: a tank destroyed in an earlier wave stays gone: a retry of a later wave does not give it back (a normal wave
    // transition keeps it gone: ClaimRewards only adds the lesson's supply, nothing refunds a destroyed robot).
    static void RetryKeepsLostTank()
    {
        BattleSetup S(int wave) => Setup(2, wave, new[] { Group(2, 1) }, new[] { Group(2, 1) }, new[] { Group(2, 1) });
        var sim = new DefenseSimulation(S(0));
        Place(sim, RobotRole.Shooter, 8, 9.5f); var tank = Place(sim, RobotRole.Tank, 0, 0);
        sim.Start(); sim.DamageRobot(tank.Id, 1e6f);
        Assert(sim.Robots.All(r => r.Spec.role != RobotRole.Tank), "setup: the tank is destroyed");
        for (int i = 0; i < 6000 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60);
        sim.ClaimRewards(); sim.Start();
        Assert(sim.WaveIndex == 1 && sim.Checkpoint != null, "setup: wave 2 started");
        int left = sim.RobotAvailable(RobotRole.Tank);
        var retry = new DefenseSimulation(S(1)); retry.Restore(sim.Checkpoint);
        Assert(retry.RobotAvailable(RobotRole.Tank) == left, $"tanks in stock: {left} when the wave began, {retry.RobotAvailable(RobotRole.Tank)} after the retry");
    }

    // S5: a while loop that goes around counts as loop turns exactly like for.
    static void WhileCountsLoopTurns()
    {
        var setup = Setup(2, 0, new[] { Group(1, 1, 100) });
        setup.Progression.initial = setup.Progression.initial.Append(new BlockStock { kind = BlockKind.While }).ToArray(); // the fixture has no while block
        var sim = new DefenseSimulation(setup);
        var bot = Place(sim, RobotRole.Warrior, -9, 2);
        sim.ApplyProgram(bot.Id, "forever", new[] { With(B(BlockKind.While), B(BlockKind.Wait, "0", B(BlockKind.Number, "0.1"))) });
        sim.Start(); Tick(sim, 60);
        Assert(bot.Program.Fault == null && bot.Program.Running, "setup: the loop keeps running");
        Assert(bot.LoopTurnsThisWave >= 5, $"LoopTurnsThisWave {bot.LoopTurnsThisWave} after about 9 turns of while");
    }

    public static void Run()
    {
        var checks = new (string Name, Action Body)[] { ("S1 boss-wave leak fails the objective", BossWaveLeak), ("S2 == / != with infinity", InfinityComparison),
            ("S3 FirstAttack follows the teaching robot", FirstAttackIsTeachingRobot), ("S4 retry keeps a lost tank lost", RetryKeepsLostTank),
            ("S5 while counts loop turns", WhileCountsLoopTurns) };
        int passed = 0;
        foreach (var (name, body) in checks)
            try { body(); passed++; } catch (Exception e) { Console.WriteLine($"AUDIT FAIL {name}: {e.Message}"); }
        Console.WriteLine($"AUDIT sim: {passed}/{checks.Length} passed");
        if (passed < checks.Length) Environment.ExitCode = 1;
    }
}
