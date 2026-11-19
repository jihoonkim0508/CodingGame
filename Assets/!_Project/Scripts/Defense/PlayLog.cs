using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // The play cache behind the F1 panel's dialogue jump. A session logs every input that changes it (DefenseSimulation.Inputs)
    // by tick from its start; while Tools/Develop/캐시 제작 records, every tutorial step a person's play enters (and every wave's
    // start) is saved with what the battle and the screen held then and where the session's log stood (Resources/Dev/
    // PlayCache_N.json). A jump starts the step's session over the same way and replays its log to the step's tick, so
    // enemies and shots in mid-battle come back too. The battle steps at a fixed 60 Hz, so speed, pause and the lessons' slow
    // motion change only how many ticks pass a frame, not what they do.
    [Serializable] public sealed class PlayInput
    {
        public long tick;
        public string kind, name;
        public int id, index, count;
        public float x, y;
        [NonSerialized] public RobotSpec spec; // null: the robot definition's own (Setup.ActionProfiles), as the battle places them
        public string[] code;
        public BlockKind[] kinds;
    }
    [Serializable] public sealed class PlayStock { public string kind; public int count; }
    [Serializable] public sealed class PlayRobot
    {
        public int id, definition;
        public string role;
        public float x, y;
        public int level, upgradeCostPaid;
        public bool hasHealth; public float health;
        public string name, source; // source: the code as the player reads it
        public string[] code;       // the same code as a block tree (PlayLog.Flatten)
    }
    // What was on screen besides the battle.
    [Serializable] public sealed class PlayUI
    {
        public int selected, editing; // robot ids, 0 for none; editing: whose code editor was open
        public string draftName; public string[] draft; // the editor's function as it stood (blocks on its start line)
        public bool inventory, paused, reward;
    }
    // Where the step's session stood: runs[run]'s inputs[0..input) replayed to tick give the state digest.
    [Serializable] public sealed class PlayReplay { public int run, input; public long tick; public string digest; }
    [Serializable] public sealed class PlayStep
    {
        public string id; // "1-1.E07" for a tutorial step, "2-4" for a wave's start
        public string recordedAt;
        public int stage, wave;
        public string phase;
        public int coins, core, kills, leaks, enemies;
        public PlayStock[] inventory, robotStock;
        public PlayRobot[] robots;
        public PlayUI ui;
        public string[] signals, completed, launched; // the lesson's state as the step began
        public PlayReplay replay;
        // The same step replaying another run of its file.
        public PlayStep WithRun(int run)
        {
            var copy = (PlayStep)MemberwiseClone();
            copy.replay = new PlayReplay { run = run, input = replay.input, tick = replay.tick, digest = replay.digest };
            return copy;
        }
    }
    // A wave started over after a defeat (DefenseSimulation.Restore): the robots, stock and numbers the wave began with.
    [Serializable] public sealed class PlayRestore
    {
        public int stage, wave, coins, core, kills, leaks;
        public int[] robotStock; // by RobotRole
        public PlaySpawn[] groups;
        public PlayRobot[] robots;
    }
    [Serializable] public sealed class PlaySpawn { public EnemySpec enemy; public int definition, route, count; public float delay, interval; public bool boss; }
    // One battle session from its start: how it was set up, then its inputs.
    [Serializable] public sealed class PlayRun
    {
        public int stage, wave; // wave: the session's first wave index
        public bool previous;   // BattleSetup.PreviousStageRewards
        public DefenseTestOptions options;
        public bool retry; public PlayRestore restore;
        public PlayInput[] inputs;
        [NonSerialized] public List<PlayInput> live; // the session's own log while it is recorded
    }
    [Serializable] public sealed class PlayCacheFile
    {
        public string fingerprint, recordedAt;
        public int stage;
        public PlayStep[] steps;
        public PlayRun[] runs;
    }
    // What a recording has gathered so far (DefenseBattle.Recording).
    public sealed class PlayRecording
    {
        public readonly List<PlayStep> Steps = new List<PlayStep>();
        public readonly List<PlayRun> Runs = new List<PlayRun>();
        public readonly Dictionary<int, string> Fingerprints = new Dictionary<int, string>();
        public string Last;
        // A step reached again in a later session (a wave played over) takes the place of the earlier one.
        public void Add(PlayStep step, PlayRun run)
        {
            int index = Runs.IndexOf(run);
            if (index < 0) { Runs.Add(run); index = Runs.Count - 1; }
            int old = Steps.FindIndex(s => s.id == step.id);
            if (old >= 0 && Steps[old].replay.run == index) return; // the first time the session reached it
            step.replay.run = index;
            if (old >= 0) Steps.RemoveAt(old);
            Steps.Add(step); Last = step.id;
        }
        public bool Has(string id, PlayRun run) { int index = Runs.IndexOf(run); return index >= 0 && Steps.Exists(s => s.id == id && s.replay.run == index); }
        // One file's worth per stage: its steps and the runs they replay, each run cut after its last step.
        public PlayCacheFile[] Files(string recordedAt) => Steps.GroupBy(s => s.stage).OrderBy(g => g.Key).Select(g =>
        {
            var used = g.Select(s => s.replay.run).Distinct().OrderBy(r => r).ToList();
            var runs = used.Select(r =>
            {
                var source = Runs[r]; int count = g.Where(s => s.replay.run == r).Max(s => s.replay.input);
                return new PlayRun { stage = source.stage, wave = source.wave, previous = source.previous, options = source.options,
                    retry = source.retry, restore = source.restore, inputs = (source.live ?? (IEnumerable<PlayInput>)source.inputs).Take(count).ToArray() };
            }).ToArray();
            var steps = g.Select(s => s.WithRun(used.IndexOf(s.replay.run))).ToArray();
            return new PlayCacheFile { stage = g.Key, fingerprint = Fingerprints.TryGetValue(g.Key, out var f) ? f : "", recordedAt = recordedAt, steps = steps, runs = runs };
        }).ToArray();

    }

    public static class PlayLog
    {

        // A block tree flattened for JsonUtility, which has no recursion: each block, then its arguments, then its body, one
        // line each as "Kind arguments body value" ("-" for an empty slot).
        public static string[] Flatten(IEnumerable<CodeBlock> blocks)
        {
            var flat = new List<string>();
            void Add(CodeBlock block)
            {
                if (block == null) { flat.Add("-"); return; }
                flat.Add(FormattableString.Invariant($"{block.Kind} {block.Arguments.Count} {block.Body.Count} {block.Value}").TrimEnd(' '));
                foreach (var argument in block.Arguments) Add(argument);
                foreach (var line in block.Body) Add(line);
            }
            foreach (var block in blocks) Add(block);
            return flat.ToArray();
        }
        public static List<CodeBlock> Unflatten(string[] flat)
        {
            int at = 0;
            CodeBlock Read()
            {
                string item = flat[at++];
                if (item == "-") return null;
                var parts = item.Split(new[] { ' ' }, 4);
                var block = new CodeBlock((BlockKind)Enum.Parse(typeof(BlockKind), parts[0]), parts.Length > 3 ? parts[3] : null);
                int arguments = int.Parse(parts[1], CultureInfo.InvariantCulture), body = int.Parse(parts[2], CultureInfo.InvariantCulture);
                for (int i = 0; i < arguments; i++) block.Arguments.Add(Read());
                for (int i = 0; i < body; i++) block.Body.Add(Read());
                return block;
            }
            var blocks = new List<CodeBlock>();
            while (flat != null && at < flat.Length) blocks.Add(Read());
            return blocks;
        }

        // Plays inputs[0..count) on a new session set up as the play's was, at their ticks, then on to tick. Null, or where
        // the replay went another way than the play (it can only run ticks while the battle runs, as the play did).
        public static string Replay(DefenseSimulation sim, IReadOnlyList<PlayInput> inputs, int count, long tick)
        {
            for (int i = 0; i <= count; i++)
            {
                long until = i < count ? inputs[i].tick : tick;
                while (sim.Ticks < until)
                {
                    if (sim.Phase != BattlePhase.Running) return $"tick {sim.Ticks}: {sim.Phase}, the play ran on to tick {until}";
                    sim.Advance(DefenseSimulation.TickDuration);
                }
                if (i < count && !Apply(sim, inputs[i])) return $"input {i} ({inputs[i].kind}) at tick {sim.Ticks} did not take";
            }
            return null;
        }
        static bool Apply(DefenseSimulation sim, PlayInput input)
        {
            switch (input.kind)
            {
                case "place":
                    var spec = input.spec ?? (input.index >= 0 && input.index < sim.Setup.ActionProfiles.Length ? sim.Setup.ActionProfiles[input.index] : null);
                    return spec != null && sim.Place(spec, input.index, new Vector2(input.x, input.y), out _) != null;
                case "remove": return sim.RemoveRobot(input.id);
                case "move": return sim.MoveRobot(input.id, new Vector2(input.x, input.y), out _);
                case "upgrade": return sim.UpgradeRobot(input.id);
                case "program":
                    // It was applied once, so it fits: a test runner's code may have gone in with the stock lifted (BeginnerLessonChecks.Loops).
                    var options = sim.Setup.TestOptions; sim.Setup.TestOptions = options.Copy(); sim.Setup.TestOptions.inventoryLimit = false;
                    try { sim.ApplyProgram(input.id, input.name, Unflatten(input.code)); return true; }
                    catch (FormatException) { return false; }
                    finally { sim.Setup.TestOptions = options; }
                case "draft": sim.SetDraft(input.id, input.kinds ?? Array.Empty<BlockKind>()); return true;
                case "invoke": return sim.Invoke(input.id);
                case "start": sim.Start(); return sim.Phase == BattlePhase.Running;
                case "claim": sim.Events.Clear(); return sim.ClaimRewards(); // the waves before it have nothing left to show
                case "pause": sim.TogglePause(); return true;
                case "step": sim.StepOnce(); return true;
                case "robot": sim.GrantRobot((RobotRole)input.index, input.count); return true;
                case "block": sim.GrantBlock((BlockKind)input.index, input.count); return true;
                case "grantall": sim.GrantAll(); return true;
                case "coins": sim.SetCoins(input.count); return true;
                case "advance": return sim.AdvanceStage();
                default: return false;
            }
        }

        // What a replay must agree on with the play: robots (place, level, code), enemies (place, health), coins, kills, leaks,
        // core, wave, phase, stock.
        public static string Digest(DefenseSimulation sim) => string.Join(" | ",
            FormattableString.Invariant($"tick {sim.Ticks} stage {sim.Setup.Stage} wave {sim.WaveIndex + 1} {sim.Phase} core {sim.BaseHealth} coins {sim.Coins} kills {sim.Kills} leaks {sim.Leaks}"),
            "robots " + string.Join(", ", sim.Robots.Select(r => FormattableString.Invariant($"{r.Id}:{r.DefinitionIndex}@{r.Position.X:R},{r.Position.Y:R} L{r.Level} {r.Program?.Source}"))),
            "enemies " + string.Join(", ", sim.Enemies.Where(e => e.Active).Select(e => FormattableString.Invariant($"{e.Id}:{e.DefinitionIndex}@{e.Position.X:R},{e.Position.Y:R} hp {e.Health:R}"))),
            "blocks " + string.Join(", ", sim.Inventory.OrderBy(p => p.Key).Select(p => p.Key + " " + p.Value.ToString(CultureInfo.InvariantCulture))),
            "robot stock " + string.Join(", ", sim.RobotInventory.OrderBy(p => p.Key).Select(p => p.Key + " " + p.Value.ToString(CultureInfo.InvariantCulture))));

        // The battle as a step saves it (the screen's part, ui, is the battle view's to fill).
        public static PlayStep Snapshot(DefenseSimulation sim, string id) => new PlayStep
        {
            id = id, stage = sim.Setup.Stage, wave = sim.WaveIndex + 1, phase = sim.Phase.ToString(),
            coins = sim.Coins, core = sim.BaseHealth, kills = sim.Kills, leaks = sim.Leaks, enemies = sim.Enemies.Count(e => e.Active),
            inventory = sim.Inventory.OrderBy(p => p.Key).Select(p => new PlayStock { kind = p.Key.ToString(), count = p.Value }).ToArray(),
            robotStock = sim.RobotInventory.OrderBy(p => p.Key).Select(p => new PlayStock { kind = p.Key.ToString(), count = p.Value }).ToArray(),
            robots = sim.Robots.Select(r => Robot(r.Id, r.DefinitionIndex, r.Spec.role, r.Position, r.Level, r.UpgradeCostPaid, r.Health, r.Program?.Name, r.Program?.CopyBlocks(), r.Program?.Source)).ToArray(),
            replay = new PlayReplay { input = sim.Inputs.Count, tick = sim.Ticks, digest = Digest(sim) },
        };
        static PlayRobot Robot(int id, int definition, RobotRole role, Vector2 at, int level, int paid, float? health, string name, IReadOnlyList<CodeBlock> code, string source) => new PlayRobot
        {
            id = id, definition = definition, role = role.ToString(), x = at.X, y = at.Y, level = level, upgradeCostPaid = paid,
            hasHealth = health.HasValue, health = health ?? 0, name = name, source = source, code = code == null ? null : Flatten(code),
        };

        // A wave start (DefenseSimulation.Checkpoint) for the file, and back.
        public static PlayRestore Save(WaveCheckpoint c) => new PlayRestore
        {
            stage = c.Stage, wave = c.Wave, coins = c.Coins, core = c.BaseHealth, kills = c.Kills, leaks = c.Leaks,
            robotStock = c.RobotStock == null ? null : Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => c.RobotStock.TryGetValue(r, out int n) ? n : 0).ToArray(),
            groups = c.Groups.Select(g => new PlaySpawn { enemy = g.Enemy.Copy(), definition = g.DefinitionIndex, route = g.RouteIndex, count = g.Count, delay = g.Delay, interval = g.Interval, boss = g.Boss }).ToArray(),
            robots = c.Robots.Select(r => Robot(0, r.DefinitionIndex, r.Spec.role, r.Position, r.Level, r.UpgradeCostPaid, r.Health, r.Name, r.Code, null)).ToArray(),
        };
        public static WaveCheckpoint Load(PlayRestore r, BattleSetup setup) => new WaveCheckpoint
        {
            Stage = r.stage, Wave = r.wave, Coins = r.coins, BaseHealth = r.core, Kills = r.kills, Leaks = r.leaks,
            RobotStock = r.robotStock == null || r.robotStock.Length == 0 ? null : r.robotStock.Select((n, i) => (role: (RobotRole)i, n)).ToDictionary(p => p.role, p => p.n),
            Groups = r.groups.Select(g => new SpawnGroup { Enemy = g.enemy.Copy(), DefinitionIndex = g.definition, RouteIndex = g.route, Count = g.count, Delay = g.delay, Interval = g.interval, Boss = g.boss }).ToArray(),
            Robots = r.robots.Select(b => new RobotSnapshot { Spec = setup.ActionProfiles[b.definition].Copy(), DefinitionIndex = b.definition, Position = new Vector2(b.x, b.y),
                Level = b.level, UpgradeCostPaid = b.upgradeCostPaid, Health = b.hasHealth ? b.health : (float?)null, Name = b.name, Code = b.code == null ? null : Unflatten(b.code) }).ToArray(),
        };

        // A stage's file after a recording: the steps recorded now in place of the same ids, the others kept, unless the file
        // was made from other campaign or lesson data (then only the new ones). The runs are those the steps replay.
        public static PlayCacheFile Merge(PlayCacheFile old, PlayCacheFile recorded)
        {
            if (old?.steps == null || old.runs == null || old.fingerprint != recorded.fingerprint) return recorded;
            var replaced = new HashSet<string>(recorded.steps.Select(s => s.id));
            var kept = old.steps.Where(s => !replaced.Contains(s.id)).ToList();
            var oldRuns = kept.Select(s => s.replay.run).Distinct().OrderBy(r => r).ToList();
            var steps = kept.Select(s => s.WithRun(oldRuns.IndexOf(s.replay.run)))
                .Concat(recorded.steps.Select(s => s.WithRun(oldRuns.Count + s.replay.run))).ToArray();
            return new PlayCacheFile { fingerprint = recorded.fingerprint, recordedAt = recorded.recordedAt, stage = recorded.stage, steps = steps,
                runs = oldRuns.Select(r => old.runs[r]).Concat(recorded.runs).ToArray() };
        }

    }
}
