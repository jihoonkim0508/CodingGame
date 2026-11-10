using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class Check
{
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    // The conditional shooter (the old 1-3, moving to 2-3): if the enemy is within 3, attack it.
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
    // while True: the function is called once at battle start (play feedback 4), so a shooter that keeps watching loops forever.
    static CodeBlock Loop(params CodeBlock[] body)
    {
        var loop = new CodeBlock(BlockKind.While);
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
    // A straight road: four enemies (64 hp, 1.65/s, 6 s apart) pass a shooter y off the road's centre.
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
    // 1-1 (play feedback 4): `slash()` runs when called. Battle start calls it once (likely at nobody); the player presses Play
    // when an enemy is in range, except while the pace, pause and speed lines (about 5 battle seconds from the first kill)
    // hold the input. A basic enemy falls to one swing; the tank-bodied one does not.
    static void IntroLeak()
    {
        var spec = new RobotSpec { role = RobotRole.Warrior, range = 3.6f, interval = .8f, damage = 14 };
        var basic = new EnemySpec { health = 12.6f, speed = 1.26f, leakDamage = 1 };
        var setup = new BattleSetup {
            Min = new Vector2(-12, -4), Max = new Vector2(12, 4), BaseHealth = 12,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(role =>
                new RobotSpec { role = role, range = 3.6f, interval = .8f, damage = 14 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(8, 0), new Vector2(-10, 0) }, 1.65f) },
            Waves = new[] { new[] {
                new SpawnGroup { Enemy = basic, Count = 2, Interval = 2 },
                new SpawnGroup { Enemy = basic, Count = 4, Delay = 4, Interval = .6f },
                new SpawnGroup { Enemy = new EnemySpec { health = 500, speed = 1.08f, leakDamage = 1 }, Count = 1, Delay = 12, Interval = 2 }
            } }
        };
        var sim = new DefenseSimulation(setup);
        var robot = sim.Place(spec, 1, new Vector2(0, 2.6f), out var error);
        Require(robot != null, error);
        sim.ApplyProgram(robot.Id, "intro", new[] { new CodeBlock(BlockKind.Slash) });
        sim.Start();
        Require(robot.Program.Running && !sim.Invoke(robot.Id), "Battle start calls the function; Play is locked while it runs");
        bool paused = false; double firstKill = -1, emptyAt = -1;
        for (int i = 0; i < 10000 && sim.Phase == BattlePhase.Running; i++)
        {
            bool lines = firstKill >= 0 && sim.Time < firstKill + .8 + 5;
            if (!lines && sim.Enemies.Any(e => e.Active && Vector2.Distance(e.Position, robot.Position) <= robot.Range)) sim.Invoke(robot.Id);
            sim.Advance(1.0 / 60);
            // The pace and pause lines need enemies to watch.
            if (lines && emptyAt < 0 && !sim.Enemies.Any(e => e.Active)) emptyAt = sim.Time - firstKill;
            if (!paused && sim.Kills > 0)
            {
                paused = true; firstKill = sim.Time;
                sim.TogglePause(); double frozenAt = sim.Time;
                sim.Advance(2);
                Require(sim.Time == frozenAt && sim.Phase == BattlePhase.Paused, "Pause must freeze battle time");
                sim.TogglePause();
            }
        }
        Require(paused && sim.Kills >= 5 && sim.Leaks >= 1 && sim.BaseHealth >= 10,
            $"Play at each enemy must fell the basic ones and let only the tank-bodied one through: kills={sim.Kills}, leaks={sim.Leaks}, core={sim.BaseHealth}");
        Require(emptyAt < 0, $"1-1's pace and pause lines must have enemies to watch: the road emptied {emptyAt:0.0}s after the first kill");
    }
    static void Main()
    {
        const string answer = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= 3:\n    attack(enemy)";
        Require(Attacks("<=") > 0, "True distance condition must execute the attack inside if");
        Require(Attacks(">") == 0, "False condition must skip the attack even when the enemy is in attack range");
        Require(TutorialWatcher.Matches(new DefenseProgram("renamed", Program("<=", "target", "distance")), answer), "Consistent custom variable names should be accepted");
        Require(!TutorialWatcher.Matches(new DefenseProgram("wrong", Program(">")), answer), "Wrong comparison must not finish the guided lesson");
        // 1-3 C08 (12차 121: no for for the player): while True with slash() inside finishes the loop lesson; an empty loop does not.
        const string swing = "while True:\n    slash()";
        Require(TutorialWatcher.Matches(new DefenseProgram("swing", new[] { Loop(new CodeBlock(BlockKind.Slash)) }), swing), "while True: slash() must finish 1-3's loop lesson");
        Require(!TutorialWatcher.Matches(new DefenseProgram("empty", new[] { Loop() }), swing), "A loop without slash() must not finish 1-3's loop lesson");
        // The conditional shooter (2-3 to come): 1.8 off the road like the preview's, 2.6 like a battle shooter.
        foreach (float y in new[] { 1.8f, 2.6f })
        {
            var baseline = Approach(false, y); var guarded = Approach(true, y);
            Require(baseline.distance > 6.5f && !baseline.closeLater,
                $"y={y}: the unconditional shooter must fire first at its range's edge (got {baseline.distance:0.0}) and not shoot that enemy again within 3");
            Require(guarded.distance <= 3 && guarded.damage >= 60,
                $"y={y}: the condition must hold the first shot until within 3 and fell the enemy with it (got {guarded.distance:0.0}m, {guarded.damage:0})");
            Require(guarded.kills == 4 && baseline.kills < guarded.kills,
                $"y={y}: the condition must defend all four enemies and the unconditional code fewer (got {guarded.kills} and {baseline.kills})");
            if (y == 1.8f) Console.WriteLine($"Straight road: unconditional first shot {baseline.distance:0.0}m {baseline.damage:0}, kills {baseline.kills}; " +
                $"condition first shot {guarded.distance:0.0}m {guarded.damage:0}, kills {guarded.kills}");
        }
        IntroLeak();
        Console.WriteLine("PASS: 1-1 Play at each enemy fells the basic ones, enemies walking through the pace and pause lines; pause freezes battle; a conditional shooter waits until within 3 and fells each enemy with one shot.");
    }
}
