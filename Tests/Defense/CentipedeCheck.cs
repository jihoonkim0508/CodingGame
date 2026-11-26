using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// Queue and Stack centipedes: one spawn group is one centipede, its enemies the segments. Only a queue's front (first in) or a
// stack's top (last in) takes damage; a stack is a pile of books on one spot (its bottom walks) and pushes a book onto its top every few seconds up to its cap; e.front(),
// e.top() and e.size; every segment that reaches the core is a leak.
static class CentipedeCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception("CentipedeCheck: " + name); checks++; }

    // The snippet the lesson asks for, in the shooter's while True.
    public const string Snippet =
        "while True:\n" +
        "    for e in get_enemies():\n" +
        "        if e.type == Enemy.Stack:\n" +
        "            shoot(e.top())\n" +
        "        elif e.type == Enemy.Queue:\n" +
        "            shoot(e.front())\n";
    const string Nearest = "while True:\n    e = get_nearest_enemy()\n    shoot(e)\n";

    static SpawnGroup C(EnemyType type, int segments, float delay = 0, float health = 30) =>
        new SpawnGroup { Enemy = new EnemySpec { type = type, health = health, speed = 1, abilityInterval = 2, segmentCap = 5 }, Count = segments, Interval = 1, Delay = delay };
    static DefenseSimulation Make(params SpawnGroup[] wave) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) }, Waves = new[] { wave } });
    static RobotState Robot(DefenseSimulation sim, string code, Vector2 at, RobotRole role = RobotRole.Ranged, float damage = 30, float range = 30) =>
        Robot(sim, TutorialAnswers.Parse(code), at, role, damage, range);
    static RobotState Robot(DefenseSimulation sim, CodeBlock[] code, Vector2 at, RobotRole role, float damage, float range = 30)
    {
        var bot = sim.Place(new RobotSpec { role = role, range = range, damage = damage, interval = 1, projectileSpeed = 1000, effectRadius = 30 }, (int)role, at, out _);
        Assert(bot != null, role + " placed");
        sim.ApplyProgram(bot.Id, "robot", code);
        if (sim.Phase == BattlePhase.Running) sim.Invoke(bot.Id);
        return bot;
    }
    static void Seconds(DefenseSimulation sim, double s) { for (int i = 0; i < (int)Math.Round(s * 60); i++) sim.Advance(1.0 / 60); }
    static bool Untouched(EnemyState e) => e.Health == e.Spec.health;

    public static void RunAll()
    {
        Assert(PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse(Snippet)) == "def robot():\n" + string.Concat(Snippet.Split('\n').Where(l => l.Length > 0).Select(l => "    " + l + "\n")),
            "the snippet compiles back to the same Python");
        Assert(PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse("e = get_nearest_enemy()\nn = e.size\nshoot(e.front())")).Contains("n = e.size"), "e.size compiles");

        // Queue: five segments one behind another, the head first in line.
        var sim = Make(C(EnemyType.Queue, 5));
        sim.Start(); Seconds(sim, 4.5);
        var segs = sim.Enemies.ToList(); var chain = segs[0].Chain;
        Assert(segs.Count == 5 && segs.All(s => s.Chain == chain && s.Spec.type == EnemyType.Queue) && !chain.Stack, "one queue centipede of five");
        Assert(chain.Front == segs[0] && chain.Hittable == segs[0], "the queue's open end is its head, first in");
        Assert(segs.Zip(segs.Skip(1), (a, b) => sim.RemainingPath(b) - sim.RemainingPath(a)).All(d => d >= DefenseSimulation.SegmentGap - .01f), "segments walk in line, head in front");
        // A nearest-enemy shooter at the road's start: its nearest is the tail, which takes nothing.
        Robot(sim, Nearest, new Vector2(-15, 2));
        sim.Events.Clear(); Seconds(sim, 3);
        Assert(segs.All(Untouched) && sim.Events.Any(e => e.Kind == "blocked") && !sim.Events.Any(e => e.Kind.EndsWith("hit") && e.Amount > 0),
            "shots at a queue's back segments are blocked");

        // The snippet hits the front; when it dies the next segment is the front (dequeue), and the queue is cleared.
        sim = Make(C(EnemyType.Queue, 5));
        sim.Start(); Seconds(sim, 4.5);
        segs = sim.Enemies.ToList(); chain = segs[0].Chain;
        var bot = Robot(sim, Snippet, new Vector2(0, 2));
        Seconds(sim, .2);
        Assert(bot.Program.Fault == null && !segs[0].Active && chain.Front == segs[1] && segs.Skip(1).All(Untouched), "the front dies first, then the next one is the front: " + bot.Program.Fault);
        Seconds(sim, 6);
        Assert(segs.All(s => !s.Active) && sim.Leaks == 0 && chain.Segments.Count == 0, "the snippet clears the queue");

        // Area attacks also hit only the open end: a blast over the whole queue.
        sim = Make(C(EnemyType.Queue, 5, health: 100));
        sim.Start(); Seconds(sim, 4.5);
        segs = sim.Enemies.ToList();
        var boom = new CodeBlock(BlockKind.While); boom.Body.Add(new CodeBlock(BlockKind.Boom, "0", new CodeBlock(BlockKind.NearestEnemy)));
        Robot(sim, new[] { boom }, new Vector2(0, 2), RobotRole.Ranged, 5);
        Seconds(sim, .5);
        Assert(segs[0].Health < 100 && segs.Skip(1).All(Untouched), "boom's blast damages only the queue's front");

        // e.size: shots only while the queue has five segments. The second shoot() was reached while the first shot flew (size
        // still 5) and fires after its cooldown, so two die; then no more.
        sim = Make(C(EnemyType.Queue, 5));
        sim.Start(); Seconds(sim, 4.5);
        Robot(sim, "while True:\n    for e in get_enemies():\n        if e.size == 5:\n            shoot(e.front())", new Vector2(0, 2));
        Seconds(sim, 3); int sized = sim.Kills; Seconds(sim, 3);
        Assert(sized == 2 && sim.Kills == 2 && sim.Enemies.Count(e => e.Active) == 3, "e.size counts the segments (" + sim.Kills + " kills)");

        // Stack: three segments, the head pushes one onto the tail every 2 s up to five. Spawns at 0, 1, 2; pushes at 2 and 4.
        sim = Make(C(EnemyType.Stack, 3));
        sim.Start(); Seconds(sim, 2.5);
        chain = sim.Enemies[0].Chain;
        Assert(chain.Stack && chain.Segments.Count == 4 && chain.Top == sim.Enemies.OrderBy(e => e.Id).Last() && chain.Hittable == chain.Top, "a stack pushes its new top onto the tail");
        Seconds(sim, 2);
        Assert(chain.Segments.Count == 5, "and again 2 s later");
        Seconds(sim, 4);
        Assert(chain.Segments.Count == 5 && sim.Enemies.Count == 5, "not past its cap");
        segs = chain.Segments.ToList();
        Assert(segs.All(s => s.Position == segs[0].Position) && sim.RemainingPath(segs[0]) < 30 - 7.5, "a stack is one pile of books on one spot, walking");
        // A nearest-enemy shooter by the road's end: its nearest is the bottom book (first in), which takes nothing.
        Robot(sim, Nearest, new Vector2(15, 2));
        sim.Events.Clear(); Seconds(sim, 2);
        Assert(segs.All(Untouched) && sim.Events.Where(e => e.Kind == "blocked").Select(e => e.Target).Distinct().SequenceEqual(new[] { segs[0].Id }),
            "get_nearest_enemy() on a stack is its bottom book, and its shots are blocked");
        Assert(segs.All(s => s.Position == segs[0].Position), "the pile stays together as it walks");

        // The snippet hits the top; when it dies the one below is the top (pop); kills outpace pushes, so it is cleared.
        sim = Make(C(EnemyType.Stack, 3));
        sim.Start(); Seconds(sim, 8.5);
        chain = sim.Enemies[0].Chain; segs = chain.Segments.ToList();
        bot = Robot(sim, Snippet, new Vector2(0, 2));
        Seconds(sim, .2);
        Assert(bot.Program.Fault == null && !segs[4].Active && chain.Top == segs[3] && segs.Take(4).All(Untouched), "the top dies first, then the one below is the top");
        Seconds(sim, 15);
        Assert(sim.Enemies.Count(e => e.Active) == 0 && sim.Leaks == 0 && sim.Phase == BattlePhase.Victory, "the snippet clears the stack");

        // Both on one road, one shooter in the middle with a shooter's range of 7: the snippet clears them, the nearest enemy
        // does not (it shoots the stack's head, first into range, while the top walks by behind).
        sim = Make(C(EnemyType.Queue, 5), C(EnemyType.Stack, 3, 6));
        Robot(sim, Snippet, new Vector2(0, 2), range: 7);
        sim.Start(); Seconds(sim, 45);
        Assert(sim.Phase == BattlePhase.Victory && sim.Leaks == 0, $"the snippet clears both ({sim.Phase}, leaks {sim.Leaks})");
        sim = Make(C(EnemyType.Queue, 5), C(EnemyType.Stack, 3, 6));
        Robot(sim, Nearest, new Vector2(0, 2), range: 7);
        sim.Start(); Seconds(sim, 45);
        int nearestLeaks = sim.Leaks;
        Assert(nearestLeaks > 0, $"get_nearest_enemy() does not ({nearestLeaks} leaks, {sim.Kills} kills)");

        // Any other enemy is a centipede of one: front() and top() are the enemy itself.
        sim = Make(new SpawnGroup { Enemy = new EnemySpec { type = EnemyType.Normal, health = 30, speed = 1 }, Count = 1, Interval = 1 });
        sim.Start(); Seconds(sim, 1);
        Robot(sim, "while True:\n    for e in get_enemies():\n        shoot(e.top())", new Vector2(0, 2));
        Seconds(sim, .2);
        Assert(sim.Kills == 1, "top() of a normal enemy is that enemy");

        // Leaks: every segment that reaches the core counts, one core health each.
        sim = Make(C(EnemyType.Queue, 5));
        sim.Start(); Seconds(sim, 40);
        Assert(sim.Leaks == 5 && sim.BaseHealth == 12 - 5, $"five segments through, five leaks ({sim.Leaks}, core {sim.BaseHealth})");
        // and every book of a stack's pile, all at once as its bottom book gets there.
        sim = Make(C(EnemyType.Stack, 3));
        sim.Start(); Seconds(sim, 40);
        Assert(sim.Leaks == 5 && sim.BaseHealth == 12 - 5, $"a pile of five books through, five leaks ({sim.Leaks}, core {sim.BaseHealth})");

        Console.WriteLine($"Centipede checks passed: {checks} (nearest-enemy shooter on both: {nearestLeaks} leaks)");
    }
}
