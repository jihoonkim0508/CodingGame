using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class Check
{
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static CodeBlock[] Program(string op, string target = "enemy", string distance = "enemy_distance")
    {
        var condition = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, op,
            new CodeBlock(BlockKind.Variable, distance), new CodeBlock(BlockKind.Number, "2.5")));
        condition.Body.Add(new CodeBlock(BlockKind.Slash, "0", new CodeBlock(BlockKind.Variable, target)));
        return new[] {
            new CodeBlock(BlockKind.DeclareVariable, target, new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.DeclareVariable, distance, new CodeBlock(BlockKind.Distance, "0",
                new CodeBlock(BlockKind.Variable, target), new CodeBlock(BlockKind.MyPos))), condition
        };
    }
    static int Attacks(string op)
    {
        var spec = new RobotSpec { role = RobotRole.Warrior, range = 6, interval = .1f, damage = 5 };
        var sim = new DefenseSimulation(new BattleSetup {
            Progression = new DefenseProgression {
                lessons = Array.Empty<WaveLesson>(), robots = new[] { new RobotStock { role = RobotRole.Warrior } },
                initial = DefenseProgression.ItemKinds.Select(k => new BlockStock { kind = k, count = 20 }).ToArray()
            },
            Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>()
                .Select(role => new RobotSpec { role = role, range = 6, interval = .1f, damage = 5 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = 1000, speed = .01f }, Count = 1, Interval = 1 } } }
        });
        var robot = sim.Place(spec, 1, new Vector2(-9, 2), out var error);
        Require(robot != null, error);
        sim.ApplyProgram(robot.Id, "condition_example", Program(op)); sim.Start();
        for (int i = 0; i < 180; i++) sim.Advance(1.0 / 60);
        Require(robot.Program.Fault == null, robot.Program.Fault);
        return robot.AttacksThisWave;
    }
    static int FirstSlashHits(bool guarded, float start = 8, float speed = 1.65f, float interval = .5f, float y = 2.2f, int count = 2)
    {
        var spec = new RobotSpec { role = RobotRole.Warrior, range = 3.2f, interval = .8f, damage = 14 };
        var sim = new DefenseSimulation(new BattleSetup {
            Min = new Vector2(-12, -4), Max = new Vector2(12, 4), FunctionRepeatLimit = 5,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>()
                .Select(role => new RobotSpec { role = role, range = 3.2f, interval = .8f, damage = 14 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(start, 0), new Vector2(-10, 0) }, speed) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = 55, speed = speed }, Count = count, Interval = interval } } }
        });
        var robot = sim.Place(spec, 1, new Vector2(0, y), out var error);
        Require(robot != null, error);
        var guided = Program("<=");
        var blocks = guarded ? guided : new[] { guided[0], guided[1],
            new CodeBlock(BlockKind.Slash, "0", new CodeBlock(BlockKind.Variable, "enemy")) };
        sim.ApplyProgram(robot.Id, "timing", blocks); sim.Start();
        bool started = false;
        for (int i = 0; i < 900; i++)
        {
            sim.Advance(1.0 / 60);
            started |= sim.Events.Any(e => e.Kind == "slash" && e.Source == robot.Id);
            int hits = sim.Events.Count(e => e.Kind == "hit" && e.Source == robot.Id && e.Amount > 0);
            sim.Events.Clear();
            if (started && hits > 0) return hits;
        }
        throw new Exception("The first slash did not connect");
    }
    static void IntroLeak()
    {
        var spec = new RobotSpec { role = RobotRole.Warrior, range = 3.2f, interval = .8f, damage = 14 };
        var setup = new BattleSetup {
            Min = new Vector2(-12, -4), Max = new Vector2(12, 4), BaseHealth = 12, FunctionRepeatLimit = 5,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(role =>
                new RobotSpec { role = role, range = 3.2f, interval = .8f, damage = 14 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(8, 0), new Vector2(-10, 0) }, 1.65f) },
            Waves = new[] { new[] {
                new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1.4f, leakDamage = 1 }, Count = 2, Interval = 2 },
                new SpawnGroup { Enemy = new EnemySpec { health = 500, speed = 1.6f, leakDamage = 1 }, Count = 1, Delay = 12, Interval = 2 }
            } }
        };
        var sim = new DefenseSimulation(setup);
        var robot = sim.Place(spec, 1, new Vector2(0, 2.2f), out var error);
        Require(robot != null, error);
        sim.ApplyProgram(robot.Id, "intro", new[] {
            new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Slash, "0", new CodeBlock(BlockKind.Variable, "enemy"))
        });
        sim.Start();
        bool paused = false;
        for (int i = 0; i < 10000 && sim.Phase == BattlePhase.Running; i++)
        {
            sim.Advance(1.0 / 60);
            if (!paused && sim.Kills > 0)
            {
                paused = true;
                sim.TogglePause(); double frozenAt = sim.Time;
                sim.Advance(2);
                Require(sim.Time == frozenAt && sim.Phase == BattlePhase.Paused, "Pause must freeze battle time");
                sim.TogglePause();
            }
        }
        Require(paused && sim.Kills == 2 && sim.Leaks == 1 && sim.BaseHealth == 11,
            $"Intro must kill two basic enemies and leak one strong enemy: kills={sim.Kills}, leaks={sim.Leaks}, core={sim.BaseHealth}");
    }
    static void Main()
    {
        const string answer = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= 2.5:\n    slash(enemy)";
        Require(Attacks("<=") > 0, "True distance condition must execute the attack inside if");
        Require(Attacks(">") == 0, "False condition must skip the attack even when the enemy is in attack range");
        Require(TutorialWatcher.Matches(new DefenseProgram("renamed", Program("<=", "target", "distance")), answer), "Consistent custom variable names should be accepted");
        Require(!TutorialWatcher.Matches(new DefenseProgram("wrong", Program(">")), answer), "Wrong comparison must not finish the guided lesson");
        Require(FirstSlashHits(false) == 1, "Without the timing condition, the first slash should hit only the lead enemy");
        Require(FirstSlashHits(true) == 2, "Waiting until distance 2.5 should let the first slash hit both enemies");
        Require(FirstSlashHits(false, 7, 1.2f, .5f, 1.8f, 2) == 1, "The preview's first unguarded slash should hit one enemy");
        Require(FirstSlashHits(true, 7, 1.2f, .5f, 1.8f, 2) == 2, "The preview's first guarded slash should hit both enemies");
        IntroLeak();
        Console.WriteLine("PASS: 1-1 strong enemy leaks once after two kills; pause freezes battle; 1-3 conditional timing and comparison checks pass.");
    }
}
