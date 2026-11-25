using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// for e in get_enemies() (front of the road first) with break, the Healer's heal, the Shielder's shield and the shooter's
// break_shield(). Code is read from Python text the way a tutorial answer is.
static class ForEachCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception("ForEachCheck: " + name); checks++; }

    // The loop the user pasted, inside the shooter's program so target is declared on every path.
    public const string PickHealer =
        "while True:\n" +
        "    target = get_nearest_enemy()\n" +
        "    for e in get_enemies():\n" +
        "        if e.type == Enemy.Healer:\n" +
        "            target = e\n" +
        "            break\n" +
        "    shoot(target)\n";
    const string BreakThenShoot =
        "while True:\n" +
        "    e = get_nearest_enemy()\n" +
        "    if e.shield > 0:\n" +
        "        break_shield(e)\n" +
        "    else:\n" +
        "        shoot(e)\n";

    static SpawnGroup G(EnemyType type, float delay, float health = 100000) =>
        new SpawnGroup { Enemy = new EnemySpec { type = type, health = health, speed = 1, abilityInterval = 2, abilityRadius = 3, abilityAmount = 12 }, Count = 1, Interval = 1, Delay = delay };
    static DefenseSimulation Make(params SpawnGroup[] wave) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) }, Waves = new[] { wave } });
    static RobotState Shooter(DefenseSimulation sim, string code)
    {
        // Stands by the road's front: the first enemy is always the nearest one.
        var bot = sim.Place(new RobotSpec { role = RobotRole.Shooter, range = 30, damage = 20, interval = 1, projectileSpeed = 1000 }, (int)RobotRole.Shooter, new Vector2(5, 2), out _);
        Assert(bot != null, "shooter placed");
        sim.ApplyProgram(bot.Id, "robot", TutorialAnswers.Parse(code));
        if (sim.Phase == BattlePhase.Running) sim.Invoke(bot.Id); // placed mid-battle: start its function as Play does
        return bot;
    }
    static void Seconds(DefenseSimulation sim, double s) { for (int i = 0; i < (int)(s * 60); i++) sim.Advance(1.0 / 60); }

    public static void RunAll()
    {
        var parsed = TutorialAnswers.Parse(PickHealer);
        Assert(PythonTreeCompiler.Compile("robot", parsed) == "def robot():\n" + string.Concat(PickHealer.Split('\n').Where(l => l.Length > 0).Select(l => "    " + l + "\n")),
            "the pasted loop compiles back to the same Python");
        Assert(PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse(BreakThenShoot)).Contains("break_shield(e)") &&
            PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse("for e in get_enemies():\n    x = e.shield")).Contains("x = e.shield"), "shield and break_shield() compile");
        Assert(!PythonTreeCompiler.HasStaleEnemyLoop(parsed), "a for-each's variable is a new enemy every turn: not a stale aim");
        bool Rejects(string code) { try { PythonTreeCompiler.Compile("r", TutorialAnswers.Parse(code)); return false; } catch (FormatException) { return true; } }
        Assert(Rejects("for e in get_enemies():\n    x = 1\nshoot(e)"), "the loop variable is not definite after the loop");
        Assert(Rejects("for e in get_nearest_enemy():\n    shoot(e)"), "for-each takes get_enemies() only");

        // A normal enemy in front (the shooter's nearest), two healers behind it: the code picks the front healer and breaks
        // there (without the break the last one, the back healer, would be the target).
        var sim = Make(G(EnemyType.Normal, 0), G(EnemyType.Healer, 1.5f), G(EnemyType.Healer, 3));
        var bot = Shooter(sim, PickHealer);
        sim.Start(); Seconds(sim, 3.2);
        var normal = sim.Enemies[0]; var front = sim.Enemies[1]; var back = sim.Enemies[2];
        Assert(front.Spec.type == EnemyType.Healer && back.Spec.type == EnemyType.Healer && sim.RemainingPath(front) < sim.RemainingPath(back), "healers spawned in order");
        sim.Events.Clear(); Seconds(sim, 1.1);
        var shots = sim.Events.Where(e => e.Kind == "shot").Select(e => e.Target).ToList();
        Assert(bot.Program.Fault == null && shots.Count > 0, "the loop runs and shoots: " + bot.Program.Fault);
        Assert(Rules.Nearest(sim.Enemies.Where(e => e.Active), bot.Position) == normal, "the healer is not the nearest enemy");
        Assert(shots.All(t => t == front.Id), "the shot goes at the front healer, so break left the loop on it");

        // Healer: heals the others in its radius up to their health, not itself.
        sim = Make(G(EnemyType.Normal, 0, 100), G(EnemyType.Healer, 1, 100));
        sim.Start(); Seconds(sim, 1.05);
        normal = sim.Enemies[0]; var healer = sim.Enemies[1];
        normal.Health = 50; healer.Health = 50;
        Seconds(sim, 2.1);
        Assert(normal.Health == 62 && healer.Health == 50, $"healer heals 12 on others, not itself ({normal.Health}, {healer.Health})");
        normal.Health = 95; Seconds(sim, 2);
        Assert(normal.Health == 100, "a heal stops at full health");

        // Shielder: the shield takes damage first; break_shield() removes it and the shot follows on its own cooldown.
        sim = Make(G(EnemyType.Normal, 0), G(EnemyType.Shielder, 1));
        sim.Start(); Seconds(sim, 2.1);
        normal = sim.Enemies[0];
        Assert(normal.Shield == 12 && sim.Enemies[1].Shield == 0, "shielder shields the others, not itself");
        bot = Shooter(sim, "while True:\n    e = get_nearest_enemy()\n    shoot(e)");
        float before = normal.Health;
        sim.Events.Clear(); Seconds(sim, .1);
        Assert(normal.Shield == 0 && before - normal.Health == 8, $"a 20 shot on a 12 shield takes 8 health ({before - normal.Health})");

        sim = Make(G(EnemyType.Normal, 0), G(EnemyType.Shielder, 1));
        sim.Start(); Seconds(sim, 2.1);
        normal = sim.Enemies[0]; before = normal.Health;
        bot = Shooter(sim, BreakThenShoot);
        sim.Events.Clear(); Seconds(sim, .2);
        var broke = sim.Events.FirstOrDefault(e => e.Kind == "shield-break");
        Assert(broke != null && broke.Target == normal.Id, "break_shield() hits the shielded enemy");
        Assert(sim.Events.Any(e => e.Kind == "shot" && e.Target == normal.Id), "the shot follows right away (its own cooldown)");
        Assert(normal.Shield == 0 && before - normal.Health == 2 + 20, $"shield gone, 2 from break_shield and the full 20 from the shot ({before - normal.Health})");
        // The shielder shields again at 4 s; the next break_shield() waits for its own 3 s cooldown, not the shot's 1 s.
        double firstBreak = sim.Time - .2, secondBreak = -1; sim.Events.Clear();
        for (int i = 0; i < 60 * 4 && secondBreak < 0; i++) { sim.Advance(1.0 / 60); if (sim.Events.Any(e => e.Kind == "shield-break")) secondBreak = sim.Time; }
        Assert(bot.Program.Fault == null && secondBreak - firstBreak >= 2.9, $"break_shield cools down on its own 3 s ({secondBreak - firstBreak:0.00} s)");
        var stranger = sim.Place(new RobotSpec { role = RobotRole.Warrior, range = 30 }, (int)RobotRole.Warrior, new Vector2(-5, 2), out _);
        Assert(sim.RequestAction(stranger.Id, RobotAction.BreakShield, normal.Id) == ActionResult.Ineffective, "only the shooter has break_shield()");

        Console.WriteLine("ForEach/healer/shield checks passed: " + checks);
    }
}
