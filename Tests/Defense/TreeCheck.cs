using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// Tree legion (Enemy.Tree): one spawn is a whole binary tree of random shape and depth (at most 4), each node with its own HP.
// Only a node without living children takes damage; only the root is in get_enemies()/get_nearest_enemy(), the others are
// reached through e.left / e.right; when the tree reaches the core every living node is a leak.
static class TreeCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception("TreeCheck: " + name); checks++; }

    // The lesson's snippet in the shooter's while True: walk down to a leaf, shoot it.
    public const string Snippet =
        "while True:\n" +
        "    n = get_nearest_enemy()\n" +
        "    while n.left or n.right:\n" +
        "        if n.left:\n" +
        "            n = n.left\n" +
        "        else:\n" +
        "            n = n.right\n" +
        "    shoot(n)\n";
    const string Nearest = "while True:\n    e = get_nearest_enemy()\n    shoot(e)\n";

    // A seed whose tree has this depth (BattleSetup.TreeSeed; the first tree of a wave at stage 0 is rolled from it alone).
    static int SeedOfDepth(int depth) => Enumerable.Range(0, 1000).First(s => { TreeNode.Roll((uint)s, out int d); return d == depth; });
    static DefenseSimulation Make(int seed, int trees = 1, float health = 30) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10), TreeSeed = seed,
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) },
        Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { type = EnemyType.Tree, health = health, speed = 1 }, Count = trees, Interval = 4 } } } });
    static RobotState Robot(DefenseSimulation sim, string code, Vector2 at, float damage = 30, float range = 30)
    {
        var bot = sim.Place(new RobotSpec { role = RobotRole.Shooter, range = range, damage = damage, interval = 1, projectileSpeed = 1000 }, (int)RobotRole.Shooter, at, out _);
        Assert(bot != null, "shooter placed");
        sim.ApplyProgram(bot.Id, "robot", TutorialAnswers.Parse(code));
        if (sim.Phase == BattlePhase.Running) sim.Invoke(bot.Id);
        return bot;
    }
    static void Seconds(DefenseSimulation sim, double s) { for (int i = 0; i < (int)Math.Round(s * 60); i++) sim.Advance(1.0 / 60); }
    static int DepthOf(DefenseSimulation sim) => sim.Enemies.Where(e => e.Node != null).Max(e => e.Node.Depth);

    public static void RunAll()
    {
        // The snippet reads as blocks and compiles back to the same Python.
        Assert(PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse(Snippet)) == "def robot():\n" + string.Concat(Snippet.Split('\n').Where(l => l.Length > 0).Select(l => "    " + l + "\n")),
            "the snippet compiles back to the same Python");

        // Shapes: never deeper than 4, always as deep as rolled (one path reaches it), parents before children, every depth seen.
        var seen = new int[5];
        for (uint seed = 0; seed < 5000; seed++)
        {
            var shape = TreeNode.Roll(seed, out int depth);
            Assert(depth >= 1 && depth <= TreeNode.MaxDepth && shape.Max(n => n.depth) == depth, $"seed {seed}: rolled depth {depth} reached, at most 4");
            Assert(shape[0] == (1, 0) && shape.Skip(1).All(n => shape.IndexOf((n.depth - 1, n.slot >> 1)) is int p && p >= 0 && p < shape.IndexOf(n)) &&
                shape.Distinct().Count() == shape.Count, $"seed {seed}: a tree, parents first");
            Assert(depth < 2 || shape.Count(n => n.depth == 2) >= 1, $"seed {seed}: the root has a child");
            seen[depth]++;
        }
        Assert(seen.Skip(1).All(n => n > 800), "every depth 1-4 comes up: " + string.Join(", ", seen.Skip(1)));
        Assert(Enumerable.Range(0, 50).All(s => TreeNode.Roll((uint)s, out _).SequenceEqual(TreeNode.Roll((uint)s, out _))), "the same seed grows the same tree");
        Assert(Enumerable.Range(0, 50).Select(s => string.Join(";", TreeNode.Roll((uint)s, out _))).Distinct().Count() > 10, "different seeds grow different trees");

        // A depth-4 tree on the road: one root, the rest ride on it; two simulations with the same seed spawn the same tree.
        int deep = SeedOfDepth(4);
        var sim = Make(deep);
        sim.Start(); Seconds(sim, 2);
        var nodes = sim.Enemies.ToList(); var root = nodes[0];
        string Shape(DefenseSimulation s) => string.Join(";", s.Enemies.Select(e => $"{e.Node.Depth}/{e.Node.Slot}"));
        var twin = Make(deep); twin.Start(); Seconds(twin, 2);
        Assert(nodes.All(e => e.Spec.type == EnemyType.Tree && e.Node?.Root == root) && root.Node.Parent == null && DepthOf(sim) == 4 &&
            nodes.Count == TreeNode.Roll((uint)deep, out _).Count && Shape(sim) == Shape(twin), "one depth-4 tree, the same in a second run: " + Shape(sim));
        Assert(nodes.All(e => e.Position == root.Position) && root.Position.X > -15, "the whole tree walks as one");
        Assert(PythonTreeCompiler.EnemyTypes[(int)EnemyType.Tree] == "Tree", "Enemy.Tree names the kind");

        // Only the root is listed: get_enemies() gives one enemy, and get_nearest_enemy() is the root.
        var bot = Robot(sim, "while True:\n    for e in get_enemies():\n        if e.type == Enemy.Tree:\n            shoot(e)", new Vector2(0, 2));
        sim.Events.Clear(); Seconds(sim, 2);
        Assert(bot.Program.Fault == null && sim.Events.Where(e => e.Kind == "shot").All(e => e.Target == root.Id) && sim.Events.Any(e => e.Kind == "shot"),
            "for e in get_enemies() sees only the root");
        Assert(nodes.All(e => e.Health == e.Spec.health) && sim.Events.Any(e => e.Kind == "blocked"), "a root with children takes nothing (blocked)");
        sim.RemoveRobot(bot.Id);

        // shoot(get_nearest_enemy()) does no damage to a tree with children.
        sim = Make(deep); sim.Start(); Seconds(sim, 2);
        nodes = sim.Enemies.ToList();
        Robot(sim, Nearest, new Vector2(0, 2));
        Seconds(sim, 6);
        Assert(nodes.All(e => e.Active && e.Health == e.Spec.health), "get_nearest_enemy() does no damage to a tree with children");

        // Leaves first: a shot at a node with children is blocked, a leaf takes it; once both children die the parent is open.
        sim = Make(deep, health: 100); sim.Start(); Seconds(sim, 1);
        nodes = sim.Enemies.ToList(); root = nodes[0];
        var inner = nodes.First(e => e.Node.Depth == 3 && !e.Node.Open);
        Assert(nodes.Where(e => e.Node.Open).All(e => e.Node.Depth == 4 || e.Node.Left == null && e.Node.Right == null), "the open nodes are the leaves");
        string Walk(EnemyState n) { var path = ""; for (var p = n; p.Node.Parent != null; p = p.Node.Parent) path = ((p.Node.Slot & 1) == 0 ? ".left" : ".right") + path; return path; }
        // Shoot that inner node by its path from the root: nothing; then each of its children, then it.
        RobotState Aim(string path) => Robot(sim, "while True:\n    n = get_nearest_enemy()\n" +
            string.Concat(path.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(side => "    n = n." + side + "\n")) + "    shoot(n)", new Vector2(0, 2), 100);
        bot = Aim(Walk(inner)); Seconds(sim, 1.5);
        Assert(inner.Health == 100 && bot.Program.Fault == null, "a node with living children is blocked: " + Walk(inner));
        sim.RemoveRobot(bot.Id);
        foreach (var child in new[] { inner.Node.Left, inner.Node.Right }.Where(c => c != null))
        {
            bot = Aim(Walk(child)); Seconds(sim, 1.2); sim.RemoveRobot(bot.Id);
            Assert(!child.Active, "the leaf " + Walk(child) + " dies");
        }
        Assert(inner.Node.Open && inner.Active, "its parent is open once its children are dead");
        bot = Aim(Walk(inner)); Seconds(sim, 1.2); sim.RemoveRobot(bot.Id);
        Assert(!inner.Active, "and then it takes damage");
        // e.left / e.right of a dead child, of a leaf and of a non-tree enemy read as None: `if n.left:` is false.
        Assert(PythonTreeCompiler.Compile("robot", TutorialAnswers.Parse("n = get_nearest_enemy()\nif n.left or n.right:\n    shoot(n.left)")).Contains("if n.left or n.right:"), "or and e.left compile");

        // The snippet kills the whole tree, root last, nothing leaks.
        foreach (int depth in new[] { 1, 2, 3, 4 })
        {
            sim = Make(SeedOfDepth(depth)); sim.Start(); Seconds(sim, 1);
            nodes = sim.Enemies.ToList(); root = nodes[0];
            bot = Robot(sim, Snippet, new Vector2(0, 2));
            int rootDiedAt = -1, killed = 0;
            for (int t = 0; t < 60 * 30 && sim.Phase == BattlePhase.Running; t++)
            {
                sim.Advance(1.0 / 60);
                foreach (var evt in sim.Events.Where(e => e.Kind == "killed")) { killed++; if (evt.Target == root.Id) rootDiedAt = killed; }
                sim.Events.Clear();
            }
            Assert(bot.Program.Fault == null && sim.Phase == BattlePhase.Victory && sim.Leaks == 0 && killed == nodes.Count && rootDiedAt == nodes.Count,
                $"depth {depth}: the snippet kills all {nodes.Count} nodes, the root last ({sim.Phase}, leaks {sim.Leaks}, kills {killed}): {bot.Program.Fault}");
        }

        // Leaks: the tree reaches the core with some leaves cut; every living node is one leak.
        sim = Make(deep); sim.Start(); Seconds(sim, 1);
        nodes = sim.Enemies.ToList();
        bot = Robot(sim, Snippet, new Vector2(-14, 2), range: 3);
        Seconds(sim, 3); sim.RemoveRobot(bot.Id);
        int living = nodes.Count(e => e.Active);
        Assert(living > 0 && living < nodes.Count, $"some leaves cut ({nodes.Count - living} of {nodes.Count})");
        Seconds(sim, 40);
        Assert(sim.Leaks == living && sim.BaseHealth == Math.Max(0, 12 - living), $"{living} living nodes through, {sim.Leaks} leaks (core {sim.BaseHealth})");

        Console.WriteLine($"Tree checks passed: {checks} (depths over 5000 seeds: {string.Join(", ", seen.Skip(1))})");
    }
}
