using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// The Reflector: normal for reflectOff, reflecting for reflectOn, from its spawn. A hit made while it reflects does it no
// harm and lands on the robot that made it; a robot at 0 HP is gone for the wave. e.reflecting, `and` and my_hp().
static class ReflectorCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception("ReflectorCheck: " + name); checks++; }
    public const string Snippet =
        "while True:\n" +
        "    for e in get_enemies():\n" +
        "        if e.type == Enemy.Reflector and e.reflecting:\n" +
        "            continue\n" +
        "        shoot(e)\n" +
        "        break\n";
    const string Plain = "while True:\n    e = get_nearest_enemy()\n    shoot(e)\n";
    static SpawnGroup Reflectors(int count, float health, float speed) => new SpawnGroup {
        Enemy = new EnemySpec { type = EnemyType.Reflector, health = health, speed = speed, reflectOn = 2, reflectOff = 3 }, Count = count, Interval = .7f };
    static DefenseSimulation Make(SpawnGroup group) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) }, Waves = new[] { new[] { group } } });
    static RobotState Robot(DefenseSimulation sim, RobotRole role = RobotRole.Ranged, float health = 100, string code = null)
    {
        var bot = sim.Place(new RobotSpec { role = role, range = 40, damage = 7, interval = 1.6f, health = health, projectileSpeed = 1000 }, (int)role, new Vector2(0, 3), out var error);
        Assert(bot != null, "robot placed: " + error);
        if (code != null) sim.ApplyProgram(bot.Id, "robot", TutorialAnswers.Parse(code));
        return bot;
    }
    static void Seconds(DefenseSimulation sim, double s) { for (int i = 0; i < (int)Math.Round(s * 60); i++) sim.Advance(1.0 / 60); }

    public static void Run()
    {
        // The code: `a and b`, e.reflecting and my_hp() read back as the Python the player sees.
        Assert(PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse(Snippet)).Contains("if e.type == Enemy.Reflector and e.reflecting:"), "the snippet compiles");
        Assert(PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse("while True:\n    if my_hp() < 50:\n        break")).Contains("if my_hp() < 50:"), "my_hp() compiles");
        bool Rejects(string code) { try { PythonTreeCompiler.Compile("r", TutorialAnswers.Parse(code)); return false; } catch (FormatException) { return true; } }
        Assert(Rejects("x = 3\nif x.reflecting:\n    break"), "reflecting needs an Enemy");

        // The visible timer: 3 s normal, then 2 s reflecting, again and again.
        var timer = Make(Reflectors(1, 1000, .01f)); var watcher = Robot(timer); timer.Start();
        Seconds(timer, .5); var r = timer.Enemies.Single();
        Assert(!r.Reflecting && Math.Abs(r.PhaseEnds - 3 - r.SpawnedAt) < 1e-6, "normal first, flips 3 s after the spawn");
        Seconds(timer, 3); Assert(r.Reflecting && timer.Events.Any(e => e.Kind == "reflect-on"), "reflecting after 3 s");
        Seconds(timer, 2); Assert(!r.Reflecting, "normal again after 2 s");

        // Hits while normal hurt it; a shot fired while it reflects comes back to the robot, which loses that much HP.
        var reflect = Make(Reflectors(1, 1000, .01f)); var shooter = Robot(reflect); reflect.Start(); Seconds(reflect, .5);
        var mirror = reflect.Enemies.Single();
        Assert(reflect.RequestAction(shooter.Id, RobotAction.Attack, mirror.Id) == ActionResult.Executed, "shot while normal");
        Seconds(reflect, .2); Assert(mirror.Health == 993 && shooter.Health == 100, "a normal reflector takes the hit");
        Seconds(reflect, 3); Assert(mirror.Reflecting, "now reflecting");
        shooter.NextAction = 0; reflect.Events.Clear();
        Assert(reflect.RequestAction(shooter.Id, RobotAction.Attack, mirror.Id) == ActionResult.Executed, "shot while reflecting");
        Seconds(reflect, .2);
        Assert(mirror.Health == 993 && shooter.Health == 93 && reflect.Events.Any(e => e.Kind == "reflect" && e.Target == shooter.Id && e.Amount == 7), "it takes nothing, the robot takes 7");
        // Melee too: a slash that lands while it reflects.
        var cut = Make(Reflectors(1, 1000, .01f)); var sword = Robot(cut, RobotRole.Melee); cut.Start(); Seconds(cut, 3.6);
        var cutMirror = cut.Enemies.Single(); Assert(cutMirror.Reflecting, "reflecting before the slash");
        cut.RequestAction(sword.Id, RobotAction.Slash); Seconds(cut, 1);
        Assert(cutMirror.Health == 1000 && sword.Health == 93, "a slash comes back to the melee robot");
        // A robot at 0 HP is disabled: removed from the battle, with an event for its view and sound.
        var weak = Make(Reflectors(1, 1000, .01f)); var frail = Robot(weak, health: 10); weak.Start(); Seconds(weak, 3.6);
        weak.RequestAction(frail.Id, RobotAction.Attack); Seconds(weak, .1); Assert(frail.Health == 3 && weak.Robots.Count == 1, "10 → 3");
        frail.NextAction = 0; weak.RequestAction(frail.Id, RobotAction.Attack); Seconds(weak, .1);
        Assert(weak.Robots.Count == 0 && weak.Events.Any(e => e.Kind == "robot-destroyed" && e.Target == frail.Id), "at 0 HP the robot is gone");

        // my_hp(): a robot that only shoots while above half health does nothing at 40.
        var careful = Make(Reflectors(1, 1000, .01f)); var hurt = Robot(careful, code: "while True:\n    e = get_nearest_enemy()\n    if my_hp() > 50:\n        shoot(e)\n");
        hurt.Health = 40; careful.Start(); Seconds(careful, 2);
        Assert(hurt.Executions == 0, "my_hp() reads the robot's own health");

        // The lesson: the snippet clears a wave of reflectors without a scratch; shooting the nearest one hurts the robot.
        var smart = Make(Reflectors(3, 28, .25f)); var skipper = Robot(smart, code: Snippet); smart.Start(); Seconds(smart, 70);
        var blunt = Make(Reflectors(3, 28, .25f)); var naive = Robot(blunt, code: Plain); blunt.Start(); Seconds(blunt, 70);
        Assert(smart.Kills == 3 && smart.Leaks == 0 && skipper.Health == 100,$"the snippet beats the reflectors untouched (kills {smart.Kills}, hp {skipper.Health})");
        Assert(naive.Health < 100 || blunt.Robots.Count == 0, $"plain shoot(e) is hit back (hp {naive.Health})");
        Console.WriteLine($"Reflector checks passed: {checks} (snippet robot {skipper.Health} HP, {smart.Kills} kills; plain shoot robot {naive.Health:0} HP, {blunt.Kills} kills)");
    }
}
