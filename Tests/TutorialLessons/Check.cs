using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class Check
{
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    // 1-3's code, for the shooter: if the enemy is within 3, attack it.
    static CodeBlock[] Program(string op, string target = "enemy", string distance = "enemy_distance")
    {
        var condition = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, op,
            new CodeBlock(BlockKind.Variable, distance), new CodeBlock(BlockKind.Number, "3")));
        condition.Body.Add(new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, target)));
        return new[] {
            new CodeBlock(BlockKind.DeclareVariable, target, new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.DeclareVariable, distance, new CodeBlock(BlockKind.Distance, "0",
                new CodeBlock(BlockKind.Variable, target), new CodeBlock(BlockKind.MyPos))), condition
        };
    }
    static CodeBlock Loop(params CodeBlock[] body)
    {
        var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, "10"));
        loop.Body.AddRange(body); return loop;
    }
    // The shooter as in Shooter.asset.
    static RobotSpec Shooter(float interval = 6) => new RobotSpec { role = RobotRole.Shooter, range = 7, interval = interval, damage = 32 };
    static RobotSpec[] Profiles() => Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>()
        .Select(role => role == RobotRole.Shooter ? Shooter() : new RobotSpec { role = role, range = 3.6f, interval = .8f, damage = 14 }).ToArray();
    static int Attacks(string op)
    {
        var spec = Shooter(.1f);
        var sim = new DefenseSimulation(new BattleSetup {
            Progression = new DefenseProgression {
                lessons = Array.Empty<WaveLesson>(), robots = new[] { new RobotStock { role = RobotRole.Shooter } },
                initial = DefenseProgression.ItemKinds.Select(k => new BlockStock { kind = k, count = 20 }).ToArray()
            },
            Min = new Vector2(-20, -10), Max = new Vector2(20, 10), ActionProfiles = Profiles(),
            Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = 1000, speed = .01f }, Count = 1, Interval = 1 } } }
        });
        var robot = sim.Place(spec, 4, new Vector2(-9, 2), out var error); // 2.2 from the enemy: within 3
        Require(robot != null, error);
        sim.ApplyProgram(robot.Id, "condition_example", Program(op)); sim.Start();
        for (int i = 0; i < 180; i++) sim.Advance(1.0 / 60);
        Require(robot.Program.Fault == null, robot.Program.Fault);
        return robot.AttacksThisWave;
    }
    // 1-3 on a straight road: the wave's four enemies (64 hp, 1.65/s, 6 s apart) pass a shooter y off the road's centre.
    // Returns the first shot's distance and damage, whether its target was ever shot again within 3, and the kills.
    static (float distance, float damage, bool closeLater, int kills) Approach(bool guarded, float y, float health = 64, float interval = 6)
    {
        var sim = new DefenseSimulation(new BattleSetup {
            Min = new Vector2(-30, -6), Max = new Vector2(30, 6), ActionProfiles = Profiles(),
            Routes = new[] { new Route(new[] { new Vector2(12, 0), new Vector2(-25, 0) }, .75f) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = health, speed = 1.65f }, Count = 4, Interval = interval } } }
        });
        var robot = sim.Place(Shooter(), 4, new Vector2(0, y), out var error);
        Require(robot != null, error);
        var code = guarded ? new[] { Loop(Program("<=")) } :
            new[] { Loop(new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.NearestEnemy))) };
        sim.ApplyProgram(robot.Id, guarded ? "guarded" : "baseline", code); sim.Start();
        float firstDistance = -1, firstDamage = 0; int firstTarget = 0; bool closeLater = false;
        for (int i = 0; i < 60 * 60 && sim.Phase == BattlePhase.Running; i++)
        {
            sim.Advance(1.0 / 60);
            foreach (var shot in sim.Events.Where(e => e.Kind == "shot" && e.Source == robot.Id))
            {
                float d = Vector2.Distance(robot.Position, shot.Position);
                if (firstTarget == 0) { firstTarget = shot.Target; firstDistance = d; firstDamage = sim.ActionDamage(robot, RobotAction.Attack, d); }
                else if (shot.Target == firstTarget && d <= 3) closeLater = true;
            }
            sim.Events.Clear();
        }
        Require(robot.Program.Fault == null, robot.Program.Fault);
        return (firstDistance, firstDamage, closeLater, sim.Kills);
    }
    static void IntroLeak()
    {
        var spec = new RobotSpec { role = RobotRole.Warrior, range = 3.6f, interval = .8f, damage = 14 };
        var setup = new BattleSetup {
            Min = new Vector2(-12, -4), Max = new Vector2(12, 4), BaseHealth = 12, FunctionRepeatLimit = 1,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(role =>
                new RobotSpec { role = role, range = 3.6f, interval = .8f, damage = 14 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(8, 0), new Vector2(-10, 0) }, 1.65f) },
            Waves = new[] { new[] {
                new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1.4f, leakDamage = 1 }, Count = 2, Interval = 2 },
                new SpawnGroup { Enemy = new EnemySpec { health = 500, speed = 1.6f, leakDamage = 1 }, Count = 1, Delay = 12, Interval = 2 }
            } }
        };
        var sim = new DefenseSimulation(setup);
        var robot = sim.Place(spec, 1, new Vector2(0, 2.6f), out var error);
        Require(robot != null, error);
        // 1-1's answer: the function runs once a wave, the loop swings five times.
        var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, "5"));
        loop.Body.Add(new CodeBlock(BlockKind.Slash));
        sim.ApplyProgram(robot.Id, "intro", new[] { loop });
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
    static void TeachingShooter()
    {
        var warrior = new RobotSpec { role = RobotRole.Warrior, range = 3.6f, interval = .8f, damage = 14 };
        var group = new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1 }, RouteIndex = 0, Count = 1, Interval = 1 };
        var sim = new DefenseSimulation(new BattleSetup {
            Stage = 1, PlayerFlow = true, InitialWave = 2,
            Progression = new DefenseProgression {
                lessons = Array.Empty<WaveLesson>(),
                robots = new[] { new RobotStock { role = RobotRole.Warrior, count = 2 }, new RobotStock { role = RobotRole.Shooter, count = 1 } },
                initial = DefenseProgression.ItemKinds.Select(kind => new BlockStock { kind = kind, count = 20 }).ToArray()
            },
            Min = new Vector2(-12, -5), Max = new Vector2(12, 5), ActionProfiles = Profiles(),
            Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
            Waves = new[] { new[] { group }, new[] { group }, new[] { group } }
        });
        var nearEntrance = sim.Place(warrior, 1, new Vector2(-8, 2), out var firstError);
        Require(nearEntrance != null, firstError);
        var shooter = sim.Place(Shooter(), 4, new Vector2(0, 2), out var secondError);
        Require(shooter != null, secondError);
        Require(TutorialWatcher.TeachingRobot(sim)?.Id == shooter.Id && TutorialWatcher.Robot(sim, 1)?.Id == nearEntrance.Id,
            "1-3 must teach its shooter, even with a warrior nearer the enemy entrance");
    }
    static void Main()
    {
        const string answer = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= 3:\n    attack(enemy)";
        Require(Attacks("<=") > 0, "True distance condition must execute the attack inside if");
        Require(Attacks(">") == 0, "False condition must skip the attack even when the enemy is in attack range");
        Require(TutorialWatcher.Matches(new DefenseProgram("renamed", Program("<=", "target", "distance")), answer), "Consistent custom variable names should be accepted");
        Require(!TutorialWatcher.Matches(new DefenseProgram("wrong", Program(">")), answer), "Wrong comparison must not finish the guided lesson");
        // 1-1 C08 (play feedback 3, 17): a player who typed another count into the new loop (range(100)) has still built the
        // lesson's loop, and must not be stuck on it.
        const string swing = "for i in range(10):\n    slash()";
        var hundred = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, "100")); hundred.Body.Add(new CodeBlock(BlockKind.Slash));
        Require(TutorialWatcher.Matches(new DefenseProgram("hundred", new[] { hundred }), swing), "A loop count other than 10 must still finish 1-1's loop lesson");
        Require(!TutorialWatcher.Matches(new DefenseProgram("empty", new[] { Loop() }), swing), "A loop without slash() must not finish 1-1's loop lesson");
        // The preview's shooter stands 1.8 off the road; 1-3's battle shooter 2.0 to 2.8 (DefenseBattle.PlaceLessonShooter).
        foreach (float y in new[] { 1.8f, 2.6f })
        {
            var baseline = Approach(false, y); var guarded = Approach(true, y);
            Require(baseline.distance > 6.5f && !baseline.closeLater,
                $"y={y}: the unconditional shooter must fire first at its range's edge (got {baseline.distance:0.0}) and not shoot that enemy again within 3");
            Require(guarded.distance <= 3 && guarded.damage >= 60,
                $"y={y}: the condition must hold the first shot until within 3 and fell the enemy with it (got {guarded.distance:0.0}m, {guarded.damage:0})");
            Require(guarded.kills == 4 && baseline.kills < guarded.kills,
                $"y={y}: the condition must defend all four enemies and the unconditional code fewer (got {guarded.kills} and {baseline.kills})");
            if (y == 1.8f) Console.WriteLine($"1-3 straight road: unconditional first shot {baseline.distance:0.0}m {baseline.damage:0}, kills {baseline.kills}; " +
                $"condition first shot {guarded.distance:0.0}m {guarded.damage:0}, kills {guarded.kills}");
        }
        IntroLeak();
        TeachingShooter();
        Console.WriteLine("PASS: 1-1 strong enemy leaks once after two kills; pause freezes battle; 1-3 shooter waits until within 3 and fells each enemy with one shot.");
    }
}
