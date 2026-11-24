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
        PlayerFlow = true, Stage = 2, InitialWave = initialWave, Min = new Vector2(-20, -10), Max = new Vector2(20, 10), BaseHealth = 7,
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1.1f) },
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Waves = Enumerable.Range(0, 3).Select(i => new[] { new SpawnGroup { Count = 2 + i, Interval = .2f, Enemy = new EnemySpec { health = 30, speed = 1 } } }).ToArray() };
    static void RunWave(DefenseSimulation sim) { for (int i = 0; i < 6000 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60); }

    public static void Run()
    {
        var sim = new DefenseSimulation(Setup());
        // Out of range of the road, so both enemies of wave 1 reach the core; nothing carries into wave 2.
        var shooter = sim.Place(Spec(RobotRole.Shooter), (int)RobotRole.Shooter, new Vector2(8, 9.5f), out var error);
        Assert(shooter != null, "shooter placed: " + error);
        sim.Start(); RunWave(sim); sim.ClaimRewards();
        Assert(sim.WaveIndex == 1 && sim.UpcomingWave.Sum(g => g.Count) == 3 && sim.BaseHealth == 5, "wave 1's two enemies leaked; wave 2 has only its own 3");
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
        Assert(retry.UpcomingWave.Sum(g => g.Count) == 3 && retry.RobotAvailable(RobotRole.Shooter) == 2, "the wave's enemies and robot stock restored");
        Assert(retry.StartError() == null, "restored wave can start");
        retry.RemoveRobot(bot.Id);
        Assert(retry.Coins == 120, "recall refunds the restored upgrade");
        Throws(() => new DefenseSimulation(Setup(1)).Restore(new WaveCheckpoint { Stage = 2, Wave = 0, Groups = checkpoint.Groups, Robots = checkpoint.Robots }), "checkpoint of another wave rejected");
        var twice = new DefenseSimulation(Setup(1)); twice.Restore(checkpoint);
        Throws(() => twice.Restore(checkpoint), "restore only onto an empty field");
        CheckNoTimeLimit();
        CheckBossEditMidWave();
        Console.WriteLine($"Retry checks passed: {checks}");
    }

    // F2 mid-wave: the boss (not out yet) takes an edit at once, a minion group already spawning keeps its numbers.
    static void CheckBossEditMidWave()
    {
        SpawnGroup[] Groups(int minions, int boss) => new[] {
            new SpawnGroup { Count = minions, Interval = .2f, Enemy = new EnemySpec { health = 30, speed = 1 } },
            new SpawnGroup { Count = boss, Interval = .2f, Boss = true, Enemy = new EnemySpec { health = 90, speed = 1 } } };
        var setup = Setup(); setup.Waves[0] = Groups(2, 1);
        var sim = new DefenseSimulation(setup);
        sim.Place(Spec(RobotRole.Shooter), (int)RobotRole.Shooter, new Vector2(8, 9.5f), out _);
        sim.Start(); sim.Advance(.1);
        Assert(sim.Phase == BattlePhase.Running && sim.Spawned == 1, "one minion out, boss waiting");
        Assert(sim.SetWaveGroups(2, 0, Groups(2, 3)) && sim.CurrentWave[1].Count == 3, "boss edit lands mid-wave");
        Assert(!sim.SetWaveGroups(2, 0, Groups(5, 3)) && sim.CurrentWave[0].Count == 2, "spawning minion group keeps its count");
        Assert(sim.Checkpoint.Groups[1].Count == 1, "checkpoint keeps the wave as it began");
    }

    // No wave time limit (the user, 2026-11-19): a wave ends only when every enemy has been killed or reached the core.
    static DefenseSimulation LastWave(int stage, int core)
    {
        var setup = Setup(2); setup.Stage = stage; setup.BaseHealth = core;
        var sim = new DefenseSimulation(setup);
        // Out of range, so all 4 enemies of the last wave walk the whole 20-unit road (about 20 s).
        sim.Place(Spec(RobotRole.Shooter), (int)RobotRole.Shooter, new Vector2(8, 9.5f), out _);
        sim.Start();
        // Past this fixture's old 1.1 s limit the wave still runs with its enemies on the road.
        for (int i = 0; i < 180; i++) sim.Advance(1.0 / 60);
        Assert(sim.Phase == BattlePhase.Running && sim.RemainingEnemies == 4 && sim.Leaks == 0, "a wave with enemies alive keeps running past the old time limit");
        RunWave(sim);
        return sim;
    }
    static void CheckNoTimeLimit()
    {
        var leaked = LastWave(2, 7);
        Assert(leaked.Phase == BattlePhase.Reward && leaked.WaveLeaks == 4 && leaked.BaseHealth == 3 && leaked.Enemies.Count == 0 && leaked.Time > 15,
            "the wave ended when the last enemy reached the core");
        leaked.ClaimRewards();
        Assert(leaked.Phase == BattlePhase.Victory && leaked.StageStars == 2, "stage still won, with the stars the core earns");
        var lost = LastWave(3, 3);
        Assert(lost.Phase == BattlePhase.Defeat && lost.BaseHealth == 0 && lost.Checkpoint != null, "leaks that empty the core lose, and the wave can be retried");
    }
}
