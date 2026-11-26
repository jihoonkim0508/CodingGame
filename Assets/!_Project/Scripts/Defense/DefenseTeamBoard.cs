using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // 팀 보드 (stage 4): `name = value` lines no robot owns, run once at each wave's start (DefenseSimulation.Start). A name
    // declared here is one object for every robot: `jobs = []` on the board and `jobs.append(e)` in one robot's code and
    // `jobs.pop(0)` in another's touch the same list (reference semantics). Robots keep their own code panels.
    public sealed class DefenseTeamBoard
    {
        // What a list went through this wave, for the board's cards (DefenseTeamBoardView): an append slides a card in, a pop
        // flies it to the robot that took it.
        public sealed class Note { public string Kind; public object List; public DefenseProgram.Val Item; public int Robot; public double Time; }
        public IReadOnlyList<CodeBlock> Blocks { get; private set; } = Array.Empty<CodeBlock>();
        public string Source { get; private set; } = "";
        public PythonTreeCompiler.Shared Shared { get; private set; }
        readonly List<string> names = new List<string>();
        public IReadOnlyList<string> Names => names;
        public readonly Dictionary<string, DefenseProgram.Val> Variables = new Dictionary<string, DefenseProgram.Val>();
        public readonly List<Note> Notes = new List<Note>();
        public bool IsEmpty => Blocks.Count == 0;
        // Compiles first, so a board that does not compile leaves the last one as it was.
        internal void Apply(IReadOnlyList<CodeBlock> blocks)
        {
            PythonTreeCompiler.Shared shared = null;
            string source = blocks.Count == 0 ? "" : PythonTreeCompiler.CompileBoard(blocks, out shared);
            Shared = blocks.Count == 0 ? null : shared; Source = source;
            Blocks = blocks.Select(DefenseProgram.Clone).ToList();
            names.Clear(); names.AddRange(Blocks.Select(b => PythonTreeCompiler.NormalizeIdentifier(b.Value)).Distinct());
        }
        // Fresh objects every wave: last wave's jobs do not carry over.
        internal void Run(DefenseSimulation sim)
        {
            Variables.Clear(); Notes.Clear();
            if (Blocks.Count > 0) DefenseProgram.RunBoard(Blocks, sim, Variables);
        }
        // Only the board's own lists: a robot's `mine = []` in a loop would otherwise pile up notes all wave.
        internal void Record(string kind, object list, DefenseProgram.Val item, int robot, double time)
        {
            if (NameOf(list) != null) Notes.Add(new Note { Kind = kind, List = list, Item = item, Robot = robot, Time = time });
        }
        // The board name a list or dict object has, or null (a robot's own list).
        public string NameOf(object value)
        {
            foreach (var name in names) if (Variables.TryGetValue(name, out var v) && v.Ref == value) return name;
            return null;
        }
        // Where an enemy stands in a board list (0 first) and which list, for its numbered reticle; -1 when in none.
        public int IndexOf(int enemy, out string list)
        {
            foreach (var name in names)
                if (Variables.TryGetValue(name, out var v) && v.Items is List<DefenseProgram.Val> items)
                    for (int i = 0; i < items.Count; i++)
                        if (items[i].Kind == DefenseProgram.ValueKind.Enemy && items[i].Enemy == enemy) { list = name; return i; }
            list = null; return -1;
        }
    }
}
