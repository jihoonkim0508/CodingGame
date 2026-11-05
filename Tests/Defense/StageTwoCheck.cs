using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class StageTwoCheck
{
    static int checks;
    static void Require(bool value, string message) { if (!value) throw new Exception("Stage 2: " + message); checks++; }
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, range = 5, damage = 14, interval = .2f };
    static BattleSetup Setup(int wave = 0) => new BattleSetup {
        Stage = 2, InitialWave = wave, PlayerFlow = true, WaveSeconds = .2f,
        Min = new Vector2(-20, -10), Max = new Vector2(20, 10), BaseHealth = 12,
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Waves = Enumerable.Range(0, 5).Select(i => new[] { new SpawnGroup {
            Enemy = new EnemySpec { health = 10000, speed = .05f }, Count = 1, Interval = 1, Boss = i == 4 } }).ToArray()
    };
    static RobotState Place(DefenseSimulation sim, RobotRole role, float x = -9)
    {
        var bot = sim.Place(Spec(role), (int)role, new Vector2(x, 2), out var error);
        Require(bot != null, "place " + role + ": " + error); return bot;
    }
    static void Advance(DefenseSimulation sim, double seconds)
    { for (int i = 0; i < seconds * 60; i++) sim.Advance(1d / 60); }
    public static void Run()
    {
        var warrior = StageTwoLearning.Loop(RobotRole.Warrior, 3, "target");
        Require(StageTwoLearning.Matches(new[] { warrior }, RobotRole.Warrior, 3), "renamed enemy accepted");
        Require(!StageTwoLearning.Matches(new[] { warrior }, RobotRole.Shooter), "wrong role rejected");
        Require(!StageTwoLearning.Matches(warrior.Body, RobotRole.Warrior), "loose/outer statements do not replace loop");
        var reversed = StageTwoLearning.Loop(RobotRole.Warrior); reversed.Body.Reverse();
        Require(!StageTwoLearning.Matches(new[] { reversed }, RobotRole.Warrior), "reversed order rejected");
        var mismatch = StageTwoLearning.Loop(RobotRole.Shooter); mismatch.Body[1].Arguments[0].Value = "other";
        Require(!StageTwoLearning.Matches(new[] { mismatch }, RobotRole.Shooter), "different variable rejected");
        foreach (string number in new[] { "0", "-1", "1.5", "NaN", "Infinity", "1000000001" })
        {
            var loop = StageTwoLearning.Loop(RobotRole.Warrior); loop.Arguments[0].Value = number;
            Require(!StageTwoLearning.Matches(new[] { loop }, RobotRole.Warrior), "invalid count " + number);
        }
        Require(StageTwoLearning.Matches(new[] { StageTwoLearning.Loop(RobotRole.Shooter, 7, "적") }, RobotRole.Shooter), "free count and Unicode variable accepted");
        Require(!StageTwoLearning.Matches(new[] { StageTwoLearning.Loop(RobotRole.Warrior, 7) }, RobotRole.Warrior, 3), "intro requires three");

        var sim = new DefenseSimulation(Setup());
        Require(sim.FunctionRepeatLimit == 1, "for unlock means one function invocation");
        Require(sim.RobotAvailable(RobotRole.Warrior) >= 1 && sim.Owned(BlockKind.For) >= 1, "intro supply");
        Require(sim.StartError() != null, "no learner program rejected");
        var bot = Place(sim, RobotRole.Warrior);
        sim.ApplyProgram(bot.Id, "robot", new[] { StageTwoLearning.Loop(RobotRole.Warrior, 7) });
        Require(sim.StartError() != null, "wrong intro count blocks battle");
        sim.ApplyProgram(bot.Id, "robot", new[] { warrior });
        Require(sim.StartError() == null, "valid intro starts");
        int stock = sim.Owned(BlockKind.For), coins = sim.Coins;
        sim.Start(); Advance(sim, 1);
        var retry = sim.RetryPreparation();
        Require(retry != null && retry.Phase == BattlePhase.Ready && retry.WaveIndex == 0, "same-wave retry restores ready state");
        Require(retry.BaseHealth == 12 && retry.Coins == coins && retry.Owned(BlockKind.For) == stock, "retry restores economy without extra supplies");
        Require(retry.Projectiles.Count == 0 && retry.Enemies.Count == 0 && retry.Robots[0].Program.ObservedCommands == 0, "retry removes combat and resets programs");
        Require(!ReferenceEquals(retry.Robots[0].Program, sim.Robots[0].Program), "retry programs are independent");

        var practice = new DefenseSimulation(Setup(1));
        var old = Place(practice, RobotRole.Warrior); old.PlacedWave = 0;
        practice.ApplyProgram(old.Id, "old", new[] { warrior });
        Require(practice.StartError() != null, "old warrior cannot replace fresh practice");
        var learner = Place(practice, RobotRole.Warrior, -5);
        practice.ApplyProgram(learner.Id, "fresh", new[] { warrior });
        Require(practice.StartError() == null, "fresh warrior practice accepted");

        var observeSetup = Setup(); observeSetup.Stage = 0; observeSetup.PlayerFlow = false; observeSetup.WaveSeconds = 120;
        var observe = new DefenseSimulation(observeSetup); var observer = Place(observe, RobotRole.Warrior);
        observe.ApplyProgram(observer.Id, "observed", new[] { warrior });
        var program = observer.Program; program.ObserveSingleInvocation = true;
        observe.Start(); Advance(observe, 1);
        Require(program.ObservedCommands == 0 && !program.ObservationFinished, "observation waits for player");
        for (int i = 1; i <= 6; i++)
        {
            program.AdvanceObservation(); Advance(observe, 1);
            Require(program.ObservedCommands == i, "one click executes exactly one command " + i);
        }
        Require(program.ObservationFinished && program.FinishedIterations == 3 && program.IterationTotal == 3, "actual three iterations finish once");
        Advance(observe, 5); Require(program.ObservedCommands == 6 && observe.Kills == 0, "no automatic fourth loop; iterations differ from kills");

        var boss = new DefenseSimulation(Setup(4)); var melee = Place(boss, RobotRole.Warrior); var ranged = Place(boss, RobotRole.Shooter, -5);
        boss.ApplyProgram(melee.Id, "melee", new[] { warrior }); boss.ApplyProgram(ranged.Id, "ranged", new[] { StageTwoLearning.Loop(RobotRole.Shooter) });
        boss.Start(); Advance(boss, 1);
        Require(boss.Phase == BattlePhase.Defeat && !boss.BossKilled && boss.PendingDrops.Count == 0, "unkilled boss cannot clear at timeout");
        Require(boss.RetryPreparation() == null, "final failure offers full stage restart, not wave retry");
        var victorySetup = Setup(4); victorySetup.WaveSeconds = 120; victorySetup.Waves[4][0].Enemy.health = 1;
        var victory = new DefenseSimulation(victorySetup); melee = Place(victory, RobotRole.Warrior); ranged = Place(victory, RobotRole.Shooter, -5);
        victory.ApplyProgram(melee.Id, "melee", new[] { warrior }); victory.ApplyProgram(ranged.Id, "ranged", new[] { StageTwoLearning.Loop(RobotRole.Shooter) });
        victory.Start(); Advance(victory, 5);
        Require(victory.Phase == BattlePhase.Reward && victory.BossKilled && victory.BaseHealth > 0, "actual boss kill clears");
        Require(victory.ClaimRewards() && victory.Phase == BattlePhase.Victory, "final claim completes stage");
        Console.WriteLine("Stage 2 checks passed: " + checks);
    }
}
