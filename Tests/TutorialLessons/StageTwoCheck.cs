using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class StageTwoCheck
{
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static CodeBlock[] Repeat(int count, RobotRole role = RobotRole.Warrior, string name = "enemy")
    {
        var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, count.ToString()));
        loop.Body.Add(new CodeBlock(BlockKind.DeclareVariable, name, new CodeBlock(BlockKind.NearestEnemy)));
        loop.Body.Add(new CodeBlock(role == RobotRole.Warrior ? BlockKind.Slash : BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, name)));
        return new[] { loop };
    }
    static CodeBlock[] ConditionalShots()
    {
        var loop = Repeat(5, RobotRole.Shooter)[0]; var shot = loop.Body[1]; loop.Body.RemoveAt(1);
        loop.Body.Add(new CodeBlock(BlockKind.DeclareVariable, "distance", new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, "enemy"), new CodeBlock(BlockKind.MyPos))));
        var condition = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, "<=", new CodeBlock(BlockKind.Variable, "distance"), new CodeBlock(BlockKind.Number, "3")));
        condition.Body.Add(shot); loop.Body.Add(condition); return new[] { loop };
    }
    static BattleSetup Setup(int wave = 0, bool boss = false) => new BattleSetup {
        Stage = 2, PlayerFlow = true, InitialWave = wave, PreviousStageRewards = false, WaveSeconds = 20,
        Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        Progression = new DefenseProgression { lessons = Array.Empty<WaveLesson>(),
            robots = new[] { new RobotStock { role = RobotRole.Warrior, count = 4 }, new RobotStock { role = RobotRole.Shooter, count = 4 } },
            initial = DefenseProgression.ItemKinds.Select(kind => new BlockStock { kind = kind, count = 50 }).ToArray() },
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(role => new RobotSpec { role = role, range = 4, interval = .4f, damage = 10 }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(0, 0), new Vector2(-15, 0) }, .01f) },
        Waves = Enumerable.Range(0, 5).Select(_ => new[] { new SpawnGroup { Enemy = new EnemySpec { health = 1000, speed = .01f }, Count = 1, Interval = 1, Boss = boss } }).ToArray()
    };
    public static void Run()
    {
        foreach (int count in new[] { 1, 3 })
        {
            var sim = new DefenseSimulation(Setup());
            var bot = sim.Place(sim.Setup.ActionProfiles[(int)RobotRole.Warrior], 1, new Vector2(0, 2), out var error);
            Require(bot != null, error);
            var blocks = Repeat(count);
            sim.ApplyProgram(bot.Id, "finite", count == 1 ? blocks[0].Body.ToArray() : blocks);
            sim.Start();
            for (int i = 0; i < 300; i++)
            {
                sim.Advance(1.0 / 60);
                if (bot.Program.Completed(sim) && !sim.HasPendingActions(bot.Id)) break;
            }
            Require(sim.Events.Count(e => e.Kind == "hit" && e.Source == bot.Id) == count, "Observe all finite strikes before pausing");
            Require(bot.AttacksThisWave == count && bot.Program.Completed(sim), "Finite program attacks exactly " + count + " times");
            Require(sim.Enemies.Any(e => e.Active && Vector2.Distance(e.Position, bot.Position) < bot.Range), "Comparison enemy survives in range");
            if (count == 3) Require(bot.Program.LoopIteration == 3 && bot.Program.LoopCount == 3, "Loop status counts body entries");
        }
        var renamed = new DefenseProgram("renamed", Repeat(4, name:"foe"));
        Require(TutorialWatcher.HasRepeatAttack(renamed, RobotRole.Warrior), "Positive different count and variable accepted");
        Require(!TutorialWatcher.HasRepeatAttack(new DefenseProgram("zero", Repeat(0)), RobotRole.Warrior), "Zero rejected");
        var loop = Repeat(3)[0]; var finder = loop.Body[0]; loop.Body.RemoveAt(0);
        var outside = new DefenseProgram("outside", new[] { finder, loop });
        Require(!TutorialWatcher.HasRepeatAttack(outside, RobotRole.Warrior), "Search outside loop rejected");
        Require(!TutorialWatcher.Matches(outside, "for i in range(3):\n    enemy = get_nearest_enemy()\n    slash(enemy)"), "Guided matcher preserves scope");
        var goalSetup = Setup(); goalSetup.WaveSeconds = .1f;
        var goal = new DefenseSimulation(goalSetup);
        var old = goal.Place(goalSetup.ActionProfiles[(int)RobotRole.Warrior], 1, new Vector2(0, 2), out _);
        goal.ApplyProgram(old.Id, "old", Repeat(5)); goal.Start(); for(int i=0;i<20;i++)goal.Advance(.05); goal.ClaimRewards();
        Require(goal.WaveIndex == 1 && !goal.IsNewRobotThisWave(old) && goal.StartError() != null, "Warrior code cannot satisfy ranged practice goal");
        var fresh = goal.Place(goalSetup.ActionProfiles[(int)RobotRole.Shooter], 4, new Vector2(5, 2), out _);
        Require(goal.StartError() != null, "New placement without code rejected");
        goal.ApplyProgram(fresh.Id, "fresh", Repeat(4,RobotRole.Shooter,name:"target")); Require(goal.StartError() == null, "New robot variable/count freedom");
        var togetherSetup = Setup(4, true);
        togetherSetup.Waves[4] = togetherSetup.Waves[4].Concat(new[] { new SpawnGroup { Enemy = new EnemySpec { health = 1000, speed = .01f }, Count = 3, Interval = 1 } }).ToArray();
        var together = new DefenseSimulation(togetherSetup); together.Place(togetherSetup.ActionProfiles[1], 1, new Vector2(0, 2), out _);
        var ranged = together.Place(togetherSetup.ActionProfiles[4], 4, new Vector2(3, 2), out _); together.ApplyProgram(ranged.Id,"conditional",ConditionalShots());
        together.Start(); together.Advance(.1);
        Require(together.Enemies.Any(e => e.Boss) && together.Enemies.Any(e => !e.Boss), "Stage 2 boss spawns with minions before their defeat");
        var bossSetup = Setup(4, true); bossSetup.WaveSeconds = .1f;
        var bossSim = new DefenseSimulation(bossSetup); bossSim.Place(bossSetup.ActionProfiles[1], 1, new Vector2(0, 2), out _);
        var bossRanged = bossSim.Place(bossSetup.ActionProfiles[4], 4, new Vector2(3, 2), out _); bossSim.ApplyProgram(bossRanged.Id,"conditional",ConditionalShots());
        bossSim.Start(); for(int i=0;i<20;i++)bossSim.Advance(.05);
        Require(bossSim.Phase == BattlePhase.Defeat && bossSim.ObjectiveFailed, "Living boss timeout cannot clear");
        Console.WriteLine("Stage 2 checks passed: finite 1/3 runs, telemetry, scope, new-robot gate, variable/count freedom, boss timeout");
    }
}
