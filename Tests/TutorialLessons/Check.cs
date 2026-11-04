using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// The lesson answers against the Warrior, Shooter, TutorialError and Enemy asset numbers, scaled like the campaign:
// 1-1 loops (slash()), 1-3 variables (attack(enemy)), 2-1 conditions (shoot only when close).
static class Check
{
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static readonly RobotSpec Warrior = new RobotSpec { role = RobotRole.Warrior, range = 3.2f, interval = .8f, damage = 14, radius = .45f, projectileSpeed = 4 };
    static readonly RobotSpec Shooter = new RobotSpec { role = RobotRole.Shooter, range = 5.5f, interval = 1.2f, damage = 22, radius = .45f, projectileSpeed = 15 };
    static CodeBlock N(string n) => new CodeBlock(BlockKind.Number, n);
    static CodeBlock V(string name) => new CodeBlock(BlockKind.Variable, name);
    // Stages 1 and 2 loop with while True: the robot keeps acting until the wave ends.
    static CodeBlock Loop(params CodeBlock[] body) { var loop = new CodeBlock(BlockKind.While); loop.Body.AddRange(body); return loop; }
    static CodeBlock FindEnemy(string name = "enemy") => new CodeBlock(BlockKind.DeclareVariable, name, new CodeBlock(BlockKind.NearestEnemy));
    static CodeBlock Shoot(string name = "enemy") => new CodeBlock(BlockKind.Shot, "0", V(name));
    // 2-1's answer: while True / enemy / enemy_distance / if enemy_distance <= threshold: attack(enemy)
    static CodeBlock[] Guarded(string threshold = "3", string target = "enemy", string distance = "enemy_distance")
    {
        var near = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, "<=", V(distance), N(threshold)));
        near.Body.Add(Shoot(target));
        return new[] { Loop(FindEnemy(target), new CodeBlock(BlockKind.DeclareVariable, distance,
            new CodeBlock(BlockKind.Distance, "0", V(target), new CodeBlock(BlockKind.MyPos))), near) };
    }
    // One robot beside a straight road (half width 1), the closest a player can place it.
    static DefenseSimulation Battle(int stage, RobotSpec robot, float y, params SpawnGroup[] groups)
    {
        var sim = new DefenseSimulation(new BattleSetup {
            Stage = stage, Min = new Vector2(-12, -6), Max = new Vector2(12, 6), BaseHealth = 12,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(role => role == RobotRole.Warrior ? Warrior.Copy() :
                role == RobotRole.Shooter ? Shooter.Copy() : new RobotSpec { role = role, range = 3, interval = 1, damage = 1 }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(8, 0), new Vector2(-10, 0) }, 1) },
            Waves = new[] { groups }
        });
        Require(sim.Place(robot.Copy(), (int)robot.role, new Vector2(0, y), out var error) != null, error);
        return sim;
    }
    static void Run(DefenseSimulation sim, Action<DefenseSimulation> each = null)
    {
        for (int i = 0; i < 60 * 120 && sim.Phase == BattlePhase.Running; i++) { sim.Advance(1.0 / 60); each?.Invoke(sim); }
    }

    // 1-1: one slash() swings once a wave; while True keeps slashing, two slashes kill each basic enemy and the strong one leaks (core 12 to 11).
    static void Loops()
    {
        SpawnGroup[] Intro() => new[] {
            new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1.4f, leakDamage = 1 }, Count = 2, Interval = 2 },
            new SpawnGroup { Enemy = new EnemySpec { health = 500, speed = 1.6f, leakDamage = 1 }, Count = 1, Delay = 12, Interval = 2 } };
        var once = Battle(1, Warrior, 1.6f, Intro());
        var bot = once.Robots.Single();
        once.ApplyProgram(bot.Id, "once", new[] { new CodeBlock(BlockKind.Slash) }); once.Start(); Run(once);
        Require(bot.Executions == 1 && once.Kills == 0, $"1-1 C07: a lone slash() swings once and stops: swings={bot.Executions} kills={once.Kills}");
        var looped = Battle(1, Warrior, 1.6f, Intro());
        bot = looped.Robots.Single();
        looped.ApplyProgram(bot.Id, "loop", new[] { Loop(new CodeBlock(BlockKind.Slash)) }); looped.Start();
        var hitsToKill = new Dictionary<int, int>();
        bool paused = false;
        Run(looped, sim => {
            foreach (var e in sim.Events.Where(e => e.Kind == "hit" && e.Amount > 0)) hitsToKill[e.Target] = hitsToKill.TryGetValue(e.Target, out int n) ? n + 1 : 1;
            sim.Events.Clear();
            if (paused || sim.Kills == 0) return;
            paused = true; sim.TogglePause(); double frozenAt = sim.Time; sim.Advance(2);
            Require(sim.Time == frozenAt && sim.Phase == BattlePhase.Paused, "Pause must freeze battle time");
            sim.TogglePause();
        });
        Require(paused && bot.Executions > 4 && looped.Kills == 2 && looped.Leaks == 1 && looped.BaseHealth == 11,
            $"1-1 while True must kill both basic enemies and leak the strong one: swings={bot.Executions} kills={looped.Kills} leaks={looped.Leaks} core={looped.BaseHealth}");
        // The preview demo uses the same wave enemy, so C08's "two slashes each" holds there too.
        Require(hitsToKill.Values.OrderBy(n => n).Take(2).All(n => n == 2), "1-1's basic enemies fall to exactly two slashes: " + string.Join(",", hitsToKill.Values));
        Require(TutorialWatcher.Matches(looped.Robots.Single().Program, "while True:\n    slash()") &&
            !TutorialWatcher.Matches(once.Robots.Single().Program, "while True:\n    slash()"), "1-1's answer is the loop, not a lone slash()");
    }

    // 1-3: attack(enemy) needs a stored enemy; found anew each turn, so the loop moves on to the next enemy.
    static void Variables()
    {
        var sim = Battle(1, Shooter, 2.5f, new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1.4f, leakDamage = 1 }, Count = 2, Interval = 2 });
        var bot = sim.Robots.Single();
        sim.ApplyProgram(bot.Id, "aim", new[] { Loop(FindEnemy(), Shoot()) }); sim.Start(); Run(sim);
        Require(sim.Kills == 2 && sim.Leaks == 0, $"1-3's answer must shoot both enemies: kills={sim.Kills} leaks={sim.Leaks} shots={bot.Executions}");
        const string answer = "while True:\n    enemy = get_nearest_enemy()\n    attack(enemy)";
        Require(TutorialWatcher.Matches(new DefenseProgram("renamed", new[] { Loop(FindEnemy("target"), Shoot("target")) }), answer),
            "1-3 accepts any variable name used consistently");
        // Matches ignores indentation, so the guided steps (attack placed as line 1>2) keep the enemy line inside the loop;
        // a player's own loop that keeps the first enemy is flagged by the editor.
        Require(PythonTreeCompiler.HasStaleEnemyLoop(new[] { FindEnemy(), Loop(Shoot()) }) &&
            !PythonTreeCompiler.HasStaleEnemyLoop(new[] { Loop(FindEnemy(), Shoot()) }), "an enemy found outside the loop is flagged");
    }

    // 2-1: the shotgun hits harder up close, so the guarded loop's shots land for more damage than shots fired on entering range.
    static float[] ShotDamage(CodeBlock[] code, out DefenseSimulation sim)
    {
        sim = Battle(2, Shooter, 1.6f, new SpawnGroup { Enemy = new EnemySpec { health = 55, speed = 1.65f, leakDamage = 1 }, Count = 5, Interval = 2 });
        var bot = sim.Robots.Single(); sim.ApplyProgram(bot.Id, "shot", code); sim.Start();
        var hits = new List<float>();
        Run(sim, s => { hits.AddRange(s.Events.Where(e => e.Kind == "projectile-hit" && e.Amount > 0).Select(e => e.Amount)); s.Events.Clear(); });
        return hits.ToArray();
    }
    static void Conditions()
    {
        var plain = ShotDamage(new[] { Loop(FindEnemy(), Shoot()) }, out var plainSim);
        var guarded = ShotDamage(Guarded(), out var guardedSim);
        Require(plain.Length > 0 && guarded.Length > 0 && guarded[0] >= 1.5f * plain[0],
            $"2-1: the close shot must hit clearly harder: near={guarded.FirstOrDefault():0.#} far={plain.FirstOrDefault():0.#}");
        Require(guardedSim.Kills > 0, $"2-1's guarded loop kills: shots={guarded.Length} kills={guardedSim.Kills}");
        Console.WriteLine($"2-1 wave (5 scaled enemies, one shooter): no condition kills {plainSim.Kills}, first hit {plain[0]:0.#}; " +
            $"distance <= 3 kills {guardedSim.Kills}, first hit {guarded[0]:0.#}");
        const string answer = "while True:\n    enemy = get_nearest_enemy()\n    enemy_distance = enemy.distance(get_my_pos())\n    if enemy_distance <= 3:\n        attack(enemy)";
        Require(TutorialWatcher.Matches(new DefenseProgram("renamed", Guarded("3", "target", "gap")), answer), "2-1 accepts consistent custom names");
        Require(!TutorialWatcher.Matches(new DefenseProgram("other", Guarded("5")), answer), "2-1's guided threshold is 3");
    }

    // 1-3 and 2-1 place a new robot beside robots already on the field; the newest is the teaching robot (Robot.Unit.0).
    static void NewestTeachingRobot()
    {
        foreach (var (stage, wave, newestFirst) in new[] { (1, 2, true), (2, 0, true), (1, 1, false) })
        {
            var group = new SpawnGroup { Enemy = new EnemySpec { health = 20, speed = 1 }, Count = 1, Interval = 1 };
            var sim = new DefenseSimulation(new BattleSetup {
                Stage = stage, PlayerFlow = true, InitialWave = wave,
                Progression = new DefenseProgression {
                    lessons = Array.Empty<WaveLesson>(),
                    robots = new[] { new RobotStock { role = RobotRole.Warrior }, new RobotStock { role = RobotRole.Shooter } },
                    initial = DefenseProgression.ItemKinds.Select(kind => new BlockStock { kind = kind, count = 20 }).ToArray()
                },
                Min = new Vector2(-12, -5), Max = new Vector2(12, 5),
                ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(role => new RobotSpec { role = role, range = 3, interval = 1, damage = 1 }).ToArray(),
                Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
                Waves = new[] { new[] { group }, new[] { group }, new[] { group } }
            });
            var first = sim.Place(Warrior.Copy(), 1, new Vector2(0, 2), out var firstError);
            var newest = sim.Place(Shooter.Copy(), 4, new Vector2(-6, 2), out var secondError);
            Require(first != null && newest != null, firstError ?? secondError);
            var teacher = newestFirst ? newest : first;
            Require(TutorialWatcher.TeachingRobot(sim)?.Id == teacher.Id && TutorialWatcher.RobotIndex(sim, teacher.Id) == 0,
                $"{stage}-{wave + 1}: Robot.Unit.0 must be the " + (newestFirst ? "robot just placed" : "first robot placed"));
        }
    }

    static void Main()
    {
        Loops();
        Variables();
        Conditions();
        NewestTeachingRobot();
        Console.WriteLine("PASS: 1-1 lone slash() swings once, while True kills two in two slashes each and the strong enemy leaks (core 11), pause freezes; " +
            "1-3 loop with a stored enemy; 2-1 close shots hit harder; the newest robot teaches in 1-3 and 2-1.");
    }
}
