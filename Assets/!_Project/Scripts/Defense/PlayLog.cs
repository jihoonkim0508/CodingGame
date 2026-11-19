using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // The play cache behind the F1 panel's dialogue jump. A session logs every input that changes it (DefenseSimulation.Inputs)
    // by tick from its start; Tools/Develop/스테이지 캐시 plays each stage's lessons and saves that log with a mark per tutorial
    // step (Resources/Dev/StageCache_N); a jump replays the log on a new session up to the step's mark. The battle steps at a
    // fixed 60 Hz, so speed, pause and the lessons' slow motion change only how many ticks pass a frame, not what they do.
    [Serializable] public sealed class PlayInput
    {
        public long tick;
        public string kind, name;
        public int id, index, count;
        public float x, y;
        public RobotSpec spec;
        public PlayBlock[] code;
        public BlockKind[] kinds;
    }
    // A block tree flattened for JsonUtility, which has no recursion: each block, then its arguments, then its body.
    [Serializable] public struct PlayBlock { public BlockKind kind; public string value; public int arguments, body; public bool none; }
    // Where the log stood as a tutorial step began, and what the screen and the lesson held then.
    [Serializable] public sealed class PlayMarker
    {
        public string id, digest, draftName;
        public int input, selected, editing; // editing: the robot whose code editor was open, 0 for none
        public long tick;
        public PlayBlock[] draft;
        public string[] signals, completed, launched;
    }
    [Serializable] public sealed class StageCache
    {
        public int stage;
        public string fingerprint;
        public DefenseTestOptions options;
        public PlayInput[] inputs;
        public PlayMarker[] markers;
    }

    public static class PlayLog
    {
        public static PlayBlock[] Flatten(IEnumerable<CodeBlock> blocks)
        {
            var flat = new List<PlayBlock>();
            void Add(CodeBlock block)
            {
                if (block == null) { flat.Add(new PlayBlock { none = true }); return; }
                flat.Add(new PlayBlock { kind = block.Kind, value = block.Value, arguments = block.Arguments.Count, body = block.Body.Count });
                foreach (var argument in block.Arguments) Add(argument);
                foreach (var line in block.Body) Add(line);
            }
            foreach (var block in blocks) Add(block);
            return flat.ToArray();
        }
        public static List<CodeBlock> Unflatten(PlayBlock[] flat)
        {
            int at = 0;
            CodeBlock Read()
            {
                var item = flat[at++];
                if (item.none) return null;
                var block = new CodeBlock(item.kind, item.value);
                for (int i = 0; i < item.arguments; i++) block.Arguments.Add(Read());
                for (int i = 0; i < item.body; i++) block.Body.Add(Read());
                return block;
            }
            var blocks = new List<CodeBlock>();
            while (flat != null && at < flat.Length) blocks.Add(Read());
            return blocks;
        }

        // Plays inputs[0..count) on a new session of the same stage at their ticks, then on to tick. Null, or where the
        // replay went another way than the play (it can only run ticks while the battle runs, as the play did).
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
                case "place": return sim.Place(input.spec, input.index, new Vector2(input.x, input.y), out _) != null;
                case "remove": return sim.RemoveRobot(input.id);
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

        // What a replay must agree on with the play: robots (place, level, code), coins, kills, leaks, core, wave, phase, stock.
        public static string Digest(DefenseSimulation sim) => string.Join(" | ",
            FormattableString.Invariant($"tick {sim.Ticks} wave {sim.WaveIndex + 1} {sim.Phase} core {sim.BaseHealth} coins {sim.Coins} kills {sim.Kills} leaks {sim.Leaks} enemies {sim.Enemies.Count}"),
            "robots " + string.Join(", ", sim.Robots.Select(r => FormattableString.Invariant($"{r.Id}:{r.DefinitionIndex}@{r.Position.X:R},{r.Position.Y:R} L{r.Level} {r.Program?.Source}"))),
            "blocks " + string.Join(", ", sim.Inventory.OrderBy(p => p.Key).Select(p => p.Key + " " + p.Value.ToString(CultureInfo.InvariantCulture))),
            "robot stock " + string.Join(", ", sim.RobotInventory.OrderBy(p => p.Key).Select(p => p.Key + " " + p.Value.ToString(CultureInfo.InvariantCulture))));
    }
}
