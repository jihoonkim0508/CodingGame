using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// The team (stage 4): lists and dicts in the interpreter, the team board's names shared by reference across robots,
// get_robots()/r.hp/r.role, e.weak_point() per enemy kind, and the two snippets the stage is built on, as blocks.
static class TeamCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception("TeamCheck: " + name); checks++; }
    public const string Board = "jobs = []\n";
    public const string Support =
        "while True:\n" +
        "    for e in get_enemies():\n" +
        "        if e.type == Enemy.Healer and e not in jobs:\n" +
        "            jobs.append(e)\n" +
        "    e = get_nearest_enemy()\n" +
        "    slow(e)\n";
    // As asked but for `shoot(get_nearest_enemy().weak_point())`: a member goes after a variable in the block editor
    // (`name.` then the member), so the nearest enemy is named first.
    public const string Ranged =
        "while True:\n" +
        "    if len(jobs) > 0:\n" +
        "        target = jobs.pop(0)\n" +
        "        while not target.is_dead():\n" +
        "            shoot(target.weak_point())\n" +
        "    else:\n" +
        "        e = get_nearest_enemy()\n" +
        "        shoot(e.weak_point())\n";
    public const string Priority =
        "priority = {Enemy.Healer: 0, Enemy.Shielder: 1, Enemy.Normal: 2}\n" +
        "best = get_nearest_enemy()\n" +
        "for e in get_enemies():\n" +
        "    if priority[e.type] < priority[best.type]:\n" +
        "        best = e\n" +
        "shoot(best.weak_point())\n";
    const string Nearest = "while True:\n    e = get_nearest_enemy()\n    shoot(e)\n";

    static string Python(string code) => string.Concat(code.Split('\n').Where(l => l.Length > 0).Select(l => "    " + l + "\n"));
    static DefenseSimulation Make(params SpawnGroup[] groups) => Make(0, groups);
    static DefenseSimulation Make(int stage, params SpawnGroup[] groups) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10), Stage = stage,
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-18, 0), new Vector2(18, 0) }, 1) }, Waves = new[] { groups } });
    static SpawnGroup Group(EnemyType type, int count, float health = 1000, float speed = .01f, float interval = .5f, float delay = 0) =>
        new SpawnGroup { Enemy = new EnemySpec { type = type, health = health, speed = speed }, Count = count, Interval = interval, Delay = delay };
    static RobotState Robot(DefenseSimulation sim, RobotRole role, Vector2 at, string code = null, RobotSpec spec = null)
    {
        var bot = sim.Place(spec ?? new RobotSpec { role = role, range = 40, damage = 7, interval = 1.6f, health = 100, projectileSpeed = 1000 }, (int)role, at, out var error);
        Assert(bot != null, "robot placed: " + error);
        if (code != null) sim.ApplyProgram(bot.Id, "robot_" + bot.Id, TutorialAnswers.Parse(code));
        return bot;
    }
    static void Seconds(DefenseSimulation sim, double s) { for (int i = 0; i < (int)Math.Round(s * 60); i++) sim.Advance(1.0 / 60); }
    static string Fault(Action run) { try { run(); return null; } catch (FormatException e) { return e.Message; } }

    public static void Run()
    {
        // As blocks: each snippet reads into blocks and compiles back to the same Python (robots against the board's names).
        var shared = Shared(Board);
        Assert(PythonTreeCompiler.CompileBoard(TutorialAnswers.Parse(Board), out _) == Board, "the board compiles to module-level Python");
        Assert(PythonTreeCompiler.Compile("support", TutorialAnswers.Parse(Support), shared) == "def support():\n" + Python(Support), "the support snippet compiles back");
        Assert(PythonTreeCompiler.Compile("ranged", TutorialAnswers.Parse(Ranged), shared) == "def ranged():\n" + Python(Ranged), "the ranged snippet compiles back");
        Assert(PythonTreeCompiler.Compile("ranged", TutorialAnswers.Parse(Priority)) == "def ranged():\n" + Python(Priority), "the priority snippet compiles back");
        Assert(Fault(() => PythonTreeCompiler.Compile("r", TutorialAnswers.Parse(Ranged))) is string missing && missing.Contains("jobs"), "without the board, jobs is not declared");
        Assert(Fault(() => PythonTreeCompiler.CompileBoard(TutorialAnswers.Parse("e = get_nearest_enemy()"), out _)) != null, "the board takes no robot values");
        Assert(Fault(() => PythonTreeCompiler.Compile("r", TutorialAnswers.Parse("x = 3\nx.append(1)"))) != null, "append() needs a list");
        Assert(Fault(() => PythonTreeCompiler.Compile("r", TutorialAnswers.Parse("for r in get_robots():\n    shoot(r)"))) != null, "shoot() takes no robot");
        Assert(PythonTreeCompiler.Compile("r", TutorialAnswers.Parse("for r in get_robots():\n    if r.role == Robot.Utility:\n        break")).Contains("if r.role == Robot.Utility:"), "r.role and Robot.Utility");
        Assert(PythonTreeCompiler.Compile("r", TutorialAnswers.Parse("d = {Enemy.Tank: 1}")) .Contains("d = {Enemy.Tank: 1}"), "a dict with one pair: the empty pairs are left out");

        Lists(); Dicts(); SharedBoard(); WeakPoints(); Robots(); PrioritySnippet(); Wave41();
        Console.WriteLine($"Team checks passed: {checks} ({wave41})");
    }
    static PythonTreeCompiler.Shared Shared(string board) { PythonTreeCompiler.CompileBoard(TutorialAnswers.Parse(board), out var shared); return shared; }

    // A list: append keeps order, pop(0) takes the first (FIFO), len counts, in/not in look, for walks a copy, an empty pop faults.
    static void Lists()
    {
        var sim = Make(Group(EnemyType.Normal, 3, interval: .001f));
        var bot = Robot(sim, RobotRole.Ranged, new Vector2(0, 3),
            "q = []\nfor e in get_enemies():\n    q.append(e)\nn = len(q)\nfirst = q.pop(0)\nm = len(q)\nfor e in q:\n    shoot(e)\nwhile True:\n    x = q.pop(0)\n");
        sim.Start(); Seconds(sim, 2);
        Assert(bot.Program.Fault != null && bot.Program.Fault.Contains("IndexError"), "pop(0) on an empty list faults (IndexError): " + bot.Program.Fault);
        var front = sim.Enemies.OrderBy(e => sim.RemainingPath(e)).ThenBy(e => e.Id).ToList();
        Assert(bot.Job == front[2].Id, "the last pop(0) took the third enemy (FIFO), the robot's job is it");
        // The same, read step by step through a second robot that only reads.
        var check = Make(Group(EnemyType.Normal, 3, interval: .001f)); var reader = Robot(check, RobotRole.Ranged, new Vector2(0, 3),
            "q = []\nfor e in get_enemies():\n    q.append(e)\nfirst = q.pop(0)\nif len(q) == 2 and first not in q:\n    if q.pop(0) in get_enemies():\n        shoot(first)\n");
        check.Start(); Seconds(check, 1.5);
        Assert(reader.Program.Fault == null && reader.Executions == 1, "len after pop is 2, the popped one is not in the list any more, an item is in get_enemies()");
    }
    static void Dicts()
    {
        var sim = Make(Group(EnemyType.Healer, 1), Group(EnemyType.Normal, 1, delay: .2f));
        var bot = Robot(sim, RobotRole.Ranged, new Vector2(0, 3),
            "p = {Enemy.Healer: 0, Enemy.Normal: 2}\nfor e in get_enemies():\n    if p[e.type] == 0:\n        shoot(e)\nx = p[Enemy.Tank]\n");
        sim.Start(); Seconds(sim, 1);
        var healer = sim.Enemies.Single(e => e.Spec.type == EnemyType.Healer);
        Assert(healer.Health < 1000, "d[k] reads the value for the key");
        Assert(bot.Program.Fault != null && bot.Program.Fault.Contains("KeyError"), "a key the dict lacks faults (KeyError): " + bot.Program.Fault);
    }
    // One object: the board's jobs is the same list in every robot (reference semantics), and it is new every wave.
    static void SharedBoard()
    {
        var sim = Make(Group(EnemyType.Healer, 2, interval: .001f), Group(EnemyType.Normal, 2, delay: .1f));
        sim.ApplyBoard(TutorialAnswers.Parse(Board));
        var scout = Robot(sim, RobotRole.Utility, new Vector2(-3, 3), "for e in get_enemies():\n    if e.type == Enemy.Healer and e not in jobs:\n        jobs.append(e)\nfor e in get_enemies():\n    if e.type == Enemy.Healer and e not in jobs:\n        jobs.append(e)\n");
        var taker = Robot(sim, RobotRole.Ranged, new Vector2(3, 3), "wait(1)\nif len(jobs) == 2:\n    target = jobs.pop(0)\n    shoot(target)\n");
        sim.Start(); Seconds(sim, .5);
        var jobs = sim.Board.Variables["jobs"].Items;
        Assert(jobs.Count == 2 && jobs.All(v => sim.Enemies.Single(e => e.Id == v.Enemy).Spec.type == EnemyType.Healer), "the scout's appends are in the board's list, no healer twice (not in)");
        Seconds(sim, 1);
        Assert(jobs.Count == 1 && taker.Executions == 1 && taker.Job != 0 && sim.Board.Notes.Count(n => n.Kind == "pop" && n.Robot == taker.Id) == 1,
            "the other robot saw 2, popped one: the scout's list lost it too (one object)");
        Assert(sim.Board.Notes.Count(n => n.Kind == "append" && n.Robot == scout.Id) == 2 && sim.Board.NameOf(sim.Board.Notes[0].List) == "jobs", "notes name the board's list");
        Assert(sim.Board.IndexOf(jobs[0].Enemy, out var list) == 0 && list == "jobs", "the reticle's number: place in the list");
        // A robot's own name is its own: two robots with `mine = []` do not share.
        var apart = Make(Group(EnemyType.Normal, 1));
        var a = Robot(apart, RobotRole.Ranged, new Vector2(-3, 3), "mine = []\nmine.append(1)\nmine.append(2)\nwait(5)\n");
        var b = Robot(apart, RobotRole.Ranged, new Vector2(3, 3), "mine = []\nmine.append(1)\nwait(1)\nif len(mine) == 1:\n    shoot(get_nearest_enemy())\n");
        apart.Start(); Seconds(apart, 2);
        Assert(b.Executions == 1, "names not on the board stay each robot's own");
        // A board name the robot assigns is the board's: one robot's `jobs = [...]` is what the other reads.
        var swap = Make(Group(EnemyType.Normal, 1)); swap.ApplyBoard(TutorialAnswers.Parse("jobs = []\nlimit = 3\n"));
        Robot(swap, RobotRole.Ranged, new Vector2(-3, 3), "limit = 1\n");
        var r2 = Robot(swap, RobotRole.Ranged, new Vector2(3, 3), "wait(.5)\nif limit == 1:\n    shoot(get_nearest_enemy())\n");
        swap.Start(); Seconds(swap, 1);
        Assert(r2.Executions == 1, "assigning a board name writes the board's");
    }
    // e.weak_point(): queue front, stack top, a tree's leftmost living leaf, any other enemy itself.
    static void WeakPoints()
    {
        const string Code = "e = get_nearest_enemy()\nw = e.weak_point()\nshoot(w)\n";
        foreach (var type in new[] { EnemyType.Normal, EnemyType.Healer, EnemyType.Reflector, EnemyType.Shielder, EnemyType.Fast, EnemyType.Tank })
        {
            var sim = Make(Group(type, 1)); var bot = Robot(sim, RobotRole.Ranged, new Vector2(0, 3), Code); sim.Start(); Seconds(sim, .6);
            var e = sim.Enemies.Single();
            Assert(DefenseSimulation.WeakPoint(e) == e && (e.Health < 1000 || e.Reflecting) && bot.Program.Fault == null, type + ": weak_point() is itself");
        }
        foreach (var stack in new[] { false, true })
        {
            var sim = Make(Group(stack ? EnemyType.Stack : EnemyType.Queue, 4, speed: 1, interval: .9f));
            sim.Start(); Seconds(sim, 4);
            var chain = sim.Enemies.First().Chain;
            foreach (var segment in chain.Segments) Assert(DefenseSimulation.WeakPoint(segment) == (stack ? chain.Top : chain.Front), (stack ? "stack: top" : "queue: front") + " from any segment");
        }
        // A tree of depth 3 at least: the weak point is the leaf reached going left where it can.
        int seed = Enumerable.Range(0, 2000).First(s => { TreeNode.Roll((uint)s, out int d); return d >= 3; });
        var tree = new DefenseSimulation(new BattleSetup { Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10), TreeSeed = seed,
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(-18, 0), new Vector2(18, 0) }, 1) },
            Waves = new[] { new[] { Group(EnemyType.Tree, 1, health: 5, speed: .01f) } } });
        var cutter = Robot(tree, RobotRole.Ranged, new Vector2(0, 3), "while True:\n    e = get_nearest_enemy()\n    shoot(e.weak_point())\n", new RobotSpec { role = RobotRole.Ranged, range = 40, damage = 30, interval = .5f, health = 100, projectileSpeed = 1000 });
        tree.Start(); Seconds(tree, .05);
        var root = tree.Enemies.Single(e => e.Node != null && e.Node.Parent == null);
        EnemyState Leftmost(EnemyState n) { while (true) { var l = n.Node.Left; var r = n.Node.Right; if (l != null && l.Active) n = l; else if (r != null && r.Active) n = r; else return n; } }
        var leaf = DefenseSimulation.WeakPoint(root);
        Assert(leaf == Leftmost(root) && leaf.Node.Open && leaf != root, "tree: the leftmost living leaf");
        int nodes = tree.Enemies.Count(e => e.Node?.Root == root);
        Seconds(tree, nodes * .5 + 3);
        Assert(!root.Active && tree.Events.All(e => e.Kind != "blocked"), $"shoot(e.weak_point()) fells the whole tree ({nodes} nodes), never hitting a node with living children");
    }
    // get_robots(), r.hp, r.role: the utility looks over the team and slows the nearest enemy only while a robot is hurt.
    static void Robots()
    {
        bool Slowed(bool hurt)
        {
            var sim = Make(Group(EnemyType.Normal, 1));
            var far = Robot(sim, RobotRole.Ranged, new Vector2(15, 8)); Robot(sim, RobotRole.Melee, new Vector2(-1, 3));
            Robot(sim, RobotRole.Utility, new Vector2(-15, -8), "for r in get_robots():\n    if r.hp < 30 and r.role != Robot.Utility:\n        e = get_nearest_enemy()\n        slow(e)\n");
            if (hurt) far.Health = 20;
            sim.Start(); Seconds(sim, .5);
            var e = sim.Enemies.Single(); return e.Speed(sim.Time) < e.Spec.speed;
        }
        Assert(Slowed(true) && !Slowed(false), "get_robots() finds the hurt robot by r.hp and r.role anywhere on the map");
    }
    // The second snippet as written: one pass that shoots the most urgent kind's weak point, the nearer normal aside.
    static void PrioritySnippet()
    {
        var sim = Make(Group(EnemyType.Healer, 1), Group(EnemyType.Normal, 1, delay: 0));
        var bot = Robot(sim, RobotRole.Ranged, new Vector2(0, 3), Priority);
        sim.Start(); Seconds(sim, .3);
        var healer = sim.Enemies.Single(e => e.Spec.type == EnemyType.Healer); var normal = sim.Enemies.Single(e => e.Spec.type == EnemyType.Normal);
        Assert(bot.Program.Fault == null && healer.Health < 1000 && normal.Health == 1000 && bot.Program.CompletedRuns == 1, "priority[e.type] min-search shoots the healer once");
        // With the dict on the team board and the search in while True, it keeps to the rule.
        var team = Make(Group(EnemyType.Normal, 2, interval: .001f), Group(EnemyType.Shielder, 1));
        team.ApplyBoard(TutorialAnswers.Parse("priority = {Enemy.Healer: 0, Enemy.Shielder: 1, Enemy.Normal: 2}\n"));
        Robot(team, RobotRole.Ranged, new Vector2(0, 3), "while True:\n" + Python(Priority.Substring(Priority.IndexOf('\n') + 1)));
        team.Start(); Seconds(team, 1);
        Assert(team.Enemies.Single(e => e.Spec.type == EnemyType.Shielder).Health < 1000 && team.Enemies.Where(e => e.Spec.type == EnemyType.Normal).All(e => e.Health == 1000), "the board's priority dict steers every turn");
    }

    // 4-1 as GameData has it (Balance.md rows, the enemy and robot assets, stage 4 scaling): the support scouting healers
    // into jobs and the ranged robot popping them wins; the same ranged robot shooting the nearest alone loses the core.
    static string wave41;
    static void Wave41()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var data = GameDataTables.Parse(File.ReadAllText(Path.Combine(root, "GameData/Award.md")), File.ReadAllText(Path.Combine(root, "GameData/Balance.md")), 1.2f);
        var rows = data.Groups.Where(g => g.Stage == 4 && g.Wave == 1).ToList();
        Assert(rows.Count > 0 && rows.Any(g => g.Enemy == "Healer"), "4-1 has healers in Balance.md");
        SpawnGroup[] Wave() => rows.Select(g => { var spec = Stats<EnemySpec>(root, g.Enemy); spec.health = g.Health; spec.speed = g.Speed;
            return new SpawnGroup { Enemy = spec, Count = g.Count, Delay = g.Delay, Interval = g.Interval }; }).ToArray();
        DefenseSimulation Fresh() => new DefenseSimulation(new BattleSetup { Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
            Stage = 4, RulesConfig = new DefenseBalanceRules(),
            ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
            Routes = new[] { new Route(new[] { new Vector2(-18, 0), new Vector2(18, 0) }, 1) }, Waves = new[] { Wave() } });
        RobotSpec Spec(string asset) => Stats<RobotSpec>(root, asset);
        (BattlePhase phase, int core, int leaks, int kills) Play(bool team)
        {
            var sim = Fresh();
            if (team) sim.ApplyBoard(TutorialAnswers.Parse(Board));
            var ranged = sim.Place(Spec("Shooter"), 2, new Vector2(-6, 2.5f), out _);
            sim.ApplyProgram(ranged.Id, "ranged", TutorialAnswers.Parse(team ? Ranged : Nearest));
            if (team) { var scout = sim.Place(Spec("Buffer"), 0, new Vector2(-10, 2.5f), out _); sim.ApplyProgram(scout.Id, "support", TutorialAnswers.Parse(Support)); }
            sim.Start();
            for (int i = 0; i < 60 * 240 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60);
            Assert(ranged.Program.Fault == null, "the ranged robot's code runs: " + ranged.Program.Fault);
            return (sim.Phase, sim.BaseHealth, sim.Leaks, sim.Kills);
        }
        var won = Play(true); var lost = Play(false);
        wave41 = $"4-1 team {won.phase} core {won.core} leaks {won.leaks} kills {won.kills} · lone nearest {lost.phase} core {lost.core} leaks {lost.leaks} kills {lost.kills}";
        Assert(won.phase == BattlePhase.Victory && lost.phase == BattlePhase.Defeat, wave41);

        // The stock: what a stage-4 start holds (every stage 2 and 3 clear reward, PreviousStageRewards) covers 4-1's board and
        // two robots; with 4-1's reward the priority board and robot as well; with 4-2's, weak_point() in all three snippets.
        var stock = new Dictionary<BlockKind, int>();
        void Add(GameDataTables.Award a) { if (a != null) foreach (var (kind, count) in a.Blocks) stock[DefenseProgression.Canonical(kind)] = stock.GetValueOrDefault(DefenseProgression.Canonical(kind)) + count; }
        foreach (var a in data.Awards.Where(a => a.Stage == 2 || a.Stage == 3)) Add(a);
        void Covers(string wave, params string[] codes)
        {
            var need = codes.SelectMany(c => DefenseProgression.Used(TutorialAnswers.Parse(c))).GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
            var short_ = need.Where(n => stock.GetValueOrDefault(n.Key) < n.Value).Select(n => $"{n.Key} {stock.GetValueOrDefault(n.Key)}/{n.Value}").ToList();
            Assert(short_.Count == 0, wave + " stock lacks: " + string.Join(", ", short_));
        }
        Covers("4-1", Board, Support, Ranged);
        Add(data.Awards.FirstOrDefault(a => a.Stage == 4 && a.Wave == 1));
        Covers("4-2", "jobs = []\npriority = {Enemy.Healer: 0, Enemy.Shielder: 1, Enemy.Normal: 2}\n", Support, "while True:\n" + Python(Priority.Substring(Priority.IndexOf('\n') + 1)));
        Add(data.Awards.FirstOrDefault(a => a.Stage == 4 && a.Wave == 2));
        Covers("4-3", Board, Support, Ranged, "while True:\n    for r in get_robots():\n        if r.role == Robot.Ranged and r.hp < 50:\n            e = get_nearest_enemy()\n            slow(e)\n");
    }
    // An asset's `stats:` block (Assets/!_Project/Defense/Data/<name>.asset) into the spec's fields of the same names.
    static T Stats<T>(string root, string asset) where T : new()
    {
        var spec = new T(); bool inside = false;
        foreach (var line in File.ReadAllLines(Path.Combine(root, "Assets/!_Project/Defense/Data/" + asset + ".asset")))
        {
            if (line == "  stats:") { inside = true; continue; }
            if (!inside) continue;
            if (!line.StartsWith("    ")) break;
            var parts = line.Trim().Split(new[] { ": " }, 2, StringSplitOptions.None);
            var field = typeof(T).GetField(parts[0], BindingFlags.Public | BindingFlags.Instance);
            if (field == null || parts.Length < 2) continue;
            double value = double.Parse(parts[1], CultureInfo.InvariantCulture);
            field.SetValue(spec, field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, (int)value) : Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
        }
        return spec;
    }
}
