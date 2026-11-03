using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// Retrying a lost wave: a fresh simulation at that wave gets back the robots, code, levels, coins and the wave as fought.
static class RetryCheck
{
    static int checks;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Throws(Action action, string message) { bool thrown = false; try { action(); } catch (ArgumentException) { thrown = true; } Assert(thrown, message); }
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, damage = 4, range = 8, interval = .1f, projectileSpeed = 200 };
    static BattleSetup Setup(int initialWave = 0) => new BattleSetup { Progression = FixtureProgression.Create(), TestOptions = new DefenseTestOptions(),
        PlayerFlow = true, Stage = 2, InitialWave = initialWave, WaveSeconds = 1.1f, Min = new Vector2(-20, -10), Max = new Vector2(20, 10), BaseHealth = 7,
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1.1f) },
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Waves = Enumerable.Range(0, 3).Select(i => new[] { new SpawnGroup { Count = 2 + i, Interval = .2f, Enemy = new EnemySpec { health = 30, speed = 1 } } }).ToArray() };
    static void RunWave(DefenseSimulation sim) { for (int i = 0; i < 6000 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60); }

    public static void Run()
    {
        var sim = new DefenseSimulation(Setup());
        // Out of range of the road, so wave 1 times out and both enemies carry into wave 2.
        var shooter = sim.Place(Spec(RobotRole.Shooter), (int)RobotRole.Shooter, new Vector2(8, 9.5f), out var error);
        Assert(shooter != null, "shooter placed: " + error);
        sim.Start(); RunWave(sim); sim.ClaimRewards();
        Assert(sim.WaveIndex == 1 && sim.UpcomingWave.Sum(g => g.Count) == 5, "wave 2 has its own 3 enemies and 2 carried ones");
        sim.SetCoins(120); Assert(sim.UpgradeRobot(shooter.Id), "upgrade before the wave");
        sim.ApplyProgram(shooter.Id, "fire", new[] { new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.NearestEnemy)) });
        sim.Start();
        var checkpoint = sim.Checkpoint;
        Assert(checkpoint != null && checkpoint.Wave == 1 && checkpoint.Coins == 70 && checkpoint.Robots.Length == 1, "checkpoint taken at wave start");
        RunWave(sim);

        var retry = new DefenseSimulation(Setup(1));
        retry.Restore(checkpoint);
        var bot = retry.Robots.Single();
        Assert(retry.Phase == BattlePhase.Ready && bot.Position == shooter.Position && bot.Level == 2 && bot.DefinitionIndex == shooter.DefinitionIndex, "robot back where it stood, same level");
        Assert(bot.Program != null && bot.Program.Source == shooter.Program.Source, "robot keeps its code");
        Assert(retry.Coins == 70 && retry.BaseHealth == checkpoint.BaseHealth && retry.Kills == checkpoint.Kills && retry.WaveLeaks == 0, "coins, core and counts as the wave began");
        Assert(retry.UpcomingWave.Sum(g => g.Count) == 5 && retry.RobotAvailable(RobotRole.Shooter) == 2, "carried enemies and robot stock restored");
        Assert(retry.StartError() == null, "restored wave can start");
        retry.RemoveRobot(bot.Id);
        Assert(retry.Coins == 120, "recall refunds the restored upgrade");
        Throws(() => new DefenseSimulation(Setup(1)).Restore(new WaveCheckpoint { Stage = 2, Wave = 0, Groups = checkpoint.Groups, Robots = checkpoint.Robots }), "checkpoint of another wave rejected");
        var twice = new DefenseSimulation(Setup(1)); twice.Restore(checkpoint);
        Throws(() => twice.Restore(checkpoint), "restore only onto an empty field");
        Console.WriteLine($"Retry checks passed: {checks}");
    }
}
