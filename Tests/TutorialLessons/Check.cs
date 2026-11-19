using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

static class Check
{
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    // 2-3's conditional warrior: slash only once the nearest enemy is within the attack range.
    static CodeBlock[] Program(string op, string target = "enemy")
    {
        var condition = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, op,
            new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, target), new CodeBlock(BlockKind.MyPos)),
            new CodeBlock(BlockKind.MyAttackRange)));
        condition.Body.Add(new CodeBlock(BlockKind.Slash));
        return new[] { new CodeBlock(BlockKind.DeclareVariable, target, new CodeBlock(BlockKind.NearestEnemy)), condition };
    }
    // while True: the function is called once at battle start (play feedback 4), so a shooter that keeps watching loops forever.
    static CodeBlock Loop(params CodeBlock[] body)
    {
        var loop = new CodeBlock(BlockKind.While);
        loop.Body.AddRange(body); return loop;
    }
    // The warrior as in Warrior.asset.
    static RobotSpec Warrior() => new RobotSpec { role = RobotRole.Warrior, range = 3.6f, interval = 1.6f, damage = 7 };
    static RobotSpec[] Profiles() => Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>()
        .Select(role => role == RobotRole.Warrior ? Warrior() : new RobotSpec { role = role, range = 3.6f, interval = .8f, damage = 14 }).ToArray();
    static int Attacks(string op)
    {
        var sim = new DefenseSimulation(new BattleSetup {
            Progression = new DefenseProgression {
                lessons = Array.Empty<WaveLesson>(), robots = new[] { new RobotStock { role = RobotRole.Warrior } },
                initial = DefenseProgression.ItemKinds.Select(k => new BlockStock { kind = k, count = 20 }).ToArray()
            },
            Min = new Vector2(-20, -10), Max = new Vector2(20, 10), ActionProfiles = Profiles(),
            Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = 1000, speed = .01f }, Count = 1, Interval = 1 } } }
        });
        var robot = sim.Place(Warrior(), (int)RobotRole.Warrior, new Vector2(-9, 2), out var error); // 2.2 from the enemy: within range
        Require(robot != null, error);
        sim.ApplyProgram(robot.Id, "condition_example", Program(op)); sim.Start();
        for (int i = 0; i < 180; i++) sim.Advance(1.0 / 60);
        Require(robot.Program.Fault == null, robot.Program.Fault);
        return robot.AttacksThisWave;
    }
    // 2-3's road: enemies of 13 health (two swings), 2.7/s, 3 s apart, pass a warrior 2.6 off the road; the first one enters
    // `lag` seconds later, so the swings at air fall at a different moment. Returns the kills.
    static int Approach(bool guarded, float lag)
    {
        var sim = new DefenseSimulation(new BattleSetup {
            Min = new Vector2(-30, -6), Max = new Vector2(30, 6), ActionProfiles = Profiles(),
            Routes = new[] { new Route(new[] { new Vector2(12, 0), new Vector2(-25, 0) }, .75f) },
            Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = 13, speed = 2.7f }, Count = 5, Delay = lag, Interval = 3 } } }
        });
        var robot = sim.Place(Warrior(), (int)RobotRole.Warrior, new Vector2(0, 2.6f), out var error);
        Require(robot != null, error);
        var code = guarded ? new[] { Loop(Program("<=")) } : new[] { Loop(new CodeBlock(BlockKind.Slash)) };
        sim.ApplyProgram(robot.Id, guarded ? "guarded" : "baseline", code); sim.Start();
        for (int i = 0; i < 60 * 60 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60);
        Require(robot.Program.Fault == null, robot.Program.Fault);
        return sim.Kills;
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
    // 1-1 E05a/E05: a basic enemy at the core is a Leak only, the tank-bodied one a Leak and a LeakTank, in either order.
    static void LeakKinds(bool tankFirst)
    {
        var basic = new EnemySpec { health = 12.6f, speed = 3, leakDamage = 1 };
        var tank = new EnemySpec { type = EnemyType.Tank, health = 500, speed = 3, leakDamage = 1 };
        var setup = new BattleSetup {
            Min = new Vector2(-12, -4), Max = new Vector2(12, 4), BaseHealth = 12,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(role => new RobotSpec { role = role }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(8, 0), new Vector2(-10, 0) }, 1.65f) },
            Waves = new[] { new[] {
                new SpawnGroup { Enemy = tankFirst ? tank : basic, Count = 1, Interval = 2 },
                new SpawnGroup { Enemy = tankFirst ? basic : tank, Count = 1, Delay = 3, Interval = 2 } } }
        };
        var sim = new DefenseSimulation(setup); var watcher = new TutorialWatcher(); var seen = new System.Collections.Generic.List<string>();
        sim.Start();
        for (int i = 0; i < 60 * 60 && sim.Phase == BattlePhase.Running; i++)
        { sim.Advance(1.0 / 60); seen.AddRange(watcher.Poll(sim).Where(s => s == TutorialSignals.Leak || s == TutorialSignals.LeakTank)); }
        string wanted = tankFirst ? "Leak,LeakTank,Leak" : "Leak,Leak,LeakTank";
        Require(string.Join(",", seen) == wanted, $"Leak signals ({(tankFirst ? "tank" : "basic")} first): {string.Join(",", seen)}, wanted {wanted}");
    }
    static void Main()
    {
        LeakKinds(false); LeakKinds(true);
        const string answer = "enemy = get_nearest_enemy()\nif enemy.distance(get_my_pos()) <= my_attack_range():\n    slash()";
        Require(Attacks("<=") > 0, "True distance condition must execute the slash inside if");
        Require(Attacks(">") == 0, "False condition must skip the slash even when the enemy is in attack range");
        Require(TutorialWatcher.Matches(new DefenseProgram("renamed", Program("<=", "target")), answer), "Consistent custom variable names should be accepted");
        Require(!TutorialWatcher.Matches(new DefenseProgram("wrong", Program(">")), answer), "Wrong comparison must not finish the guided lesson");
        // 1-3 C08 (12차 121: no for for the player): while True with slash() inside finishes the loop lesson; an empty loop does not.
        const string swing = "while True:\n    slash()";
        Require(TutorialWatcher.Matches(new DefenseProgram("swing", new[] { Loop(new CodeBlock(BlockKind.Slash)) }), swing), "while True: slash() must finish 1-3's loop lesson");
        Require(!TutorialWatcher.Matches(new DefenseProgram("empty", new[] { Loop() }), swing), "A loop without slash() must not finish 1-3's loop lesson");
        // 2-3: slashing at air rests the warrior 1.6 s, so an enemy walking in is often swung at once before it is past; the
        // condition swings the moment one is in range. Never worse, better over the entry moments.
        int plain = 0, guardedKills = 0;
        foreach (float lag in new[] { 0f, .4f, .8f, 1.2f })
        {
            int a = Approach(false, lag), b = Approach(true, lag);
            Require(b >= a, $"lag={lag}: the conditional warrior must fell at least as many as slash() alone ({b} vs {a})");
            plain += a; guardedKills += b;
        }
        Require(guardedKills > plain, $"The conditional warrior must fell more than slash() alone over the entry moments ({guardedKills} vs {plain})");
        Console.WriteLine($"2-3 road (5 enemies x 4 entry moments): slash() alone fells {plain}, the condition {guardedKills}");
        IntroLeak();
        Console.WriteLine("PASS: 1-1 Play at each enemy fells the basic ones, enemies walking through the pace and pause lines; pause freezes battle; 2-3's conditional warrior fells more than slash() alone.");
    }
}
