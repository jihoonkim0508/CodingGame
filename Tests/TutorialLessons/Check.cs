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
            new CodeBlock(BlockKind.Variable, distance), new CodeBlock(BlockKind.MyAttackRange)));
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
    static void Main()
    {
        const string answer = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= my_attack_range():\n    slash(enemy)";
        Require(Attacks("<=") > 0, "True distance condition must execute the attack inside if");
        Require(Attacks(">") == 0, "False condition must skip the attack even when the enemy is in attack range");
        Require(TutorialWatcher.Matches(new DefenseProgram("renamed", Program("<=", "target", "distance")), answer), "Consistent custom variable names should be accepted");
        Require(!TutorialWatcher.Matches(new DefenseProgram("wrong", Program(">")), answer), "Wrong comparison must not finish the guided lesson");
        Console.WriteLine("PASS: condition true/false changes actual attacks; custom names accepted; wrong comparison rejected.");
    }
}
