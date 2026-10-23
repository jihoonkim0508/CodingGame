using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodingGame.Defense
{
    // Turns live simulation changes into tutorial signals (TutorialSignals.FirstKill/FirstAttack/Leak/WaveCleared/ProgramMatches).
    // Plain C# so the headless checks cover it; TutorialDirector polls it once per frame.
    public sealed class TutorialWatcher
    {
        DefenseSimulation session;
        int wave = -1, killsAtWaveStart, leaks;
        bool firstKill, firstAttack, cleared, bossSpawned, bossAttacked;
        public void Reset() { session = null; wave = -1; }
        readonly List<string> signals = new List<string>();

        // The index-th warrior placed this stage (by id): 0 is the robot the tutorial teaches, 1 the second warrior of 1-4.
        public static RobotState Robot(DefenseSimulation sim, int index) =>
            sim?.Robots.Where(r => r.Spec.role == RobotRole.Warrior).OrderBy(r => r.Id).ElementAtOrDefault(index);
        public static RobotState TeachingRobot(DefenseSimulation sim) => Robot(sim, 0);

        // Generated Python body equals the expected lines, ignoring the def header, blank lines and the names the
        // player gave variables (any name holding the enemy object works, not only `enemy`). The same lines in another order
        // also count when their nesting is unchanged: a slash outside its if must never count as the answer.
        public static bool Matches(DefenseProgram program, string expected)
        {
            if (program == null) return false;
            // A loop's scope is part of the answer: finding the enemy before the loop must not pass.
            if ((expected ?? "").Contains("for ") || (expected ?? "").Contains("while "))
            {
                var body = program.Source.Replace("\r", "").Split('\n').Skip(1).ToArray();
                var wantedBody = (expected ?? "").Replace("\r", "").Split('\n');
                return Scoped(body).SequenceEqual(Scoped(wantedBody));
            }
            var actual = Scoped(program.Source.Replace("\r", "").Split('\n').Skip(1)).ToList();
            var wanted = Scoped((expected ?? "").Replace("\r", "").Split('\n')).ToList();
            return actual.SequenceEqual(wanted) || actual.OrderBy(l => l).SequenceEqual(wanted.OrderBy(l => l));
        }
        static List<string> Lines(string text) =>
            (text ?? "").Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        static IEnumerable<string> Scoped(IEnumerable<string> source)
        {
            var rows = source.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (rows.Length == 0) return System.Array.Empty<string>();
            int baseline = rows.Min(l => l.Length - l.TrimStart().Length);
            var normalized = Canonical(rows.Select(l => l.Trim()).ToList());
            return rows.Select((l, i) => (l.Length - l.TrimStart().Length - baseline) + ":" + normalized[i]);
        }
        static readonly Regex Assignment = new Regex(@"^([^\W\d]\w*)\s*=(?!=)"); // any Python name, Korean included
        // Variables renamed v0, v1… in the order they are first set; attributes and calls (.hp, get_my_pos()) keep their names.
        static List<string> Canonical(List<string> lines)
        {
            var names = new List<string>();
            foreach (var line in lines) { var m = Assignment.Match(line); if (m.Success && !names.Contains(m.Groups[1].Value)) names.Add(m.Groups[1].Value); }
            if (names.Count == 0) return lines;
            var name = new Regex(@"(?<![\w.])(" + string.Join("|", names.Select(Regex.Escape)) + @")(?!\w)");
            return lines.Select(line => name.Replace(line, m => "v" + names.IndexOf(m.Value))).ToList();
        }

        // Signals raised since the last poll. expectedCode (optional) adds ProgramMatches while robot robotIndex's code matches it.
        public IReadOnlyList<string> Poll(DefenseSimulation sim, string expectedCode = null, int robotIndex = 0)
        {
            signals.Clear();
            if (sim == null) return signals;
            if (sim != session || sim.WaveIndex != wave)
            {
                session = sim; wave = sim.WaveIndex; killsAtWaveStart = sim.Kills; leaks = sim.Leaks; firstKill = firstAttack = cleared = bossSpawned = bossAttacked = false;
            }
            if (!bossSpawned && sim.FinaleBossId != 0) { bossSpawned = true; signals.Add(TutorialSignals.BossSpawned); }
            if (!bossAttacked && sim.FinaleBossAttacked) { bossAttacked = true; signals.Add(TutorialSignals.BossAttacked); }
            if (!firstKill && sim.Kills > killsAtWaveStart) { firstKill = true; signals.Add(TutorialSignals.FirstKill); }
            if (!firstAttack && sim.Robots.Any(r => r.AttacksThisWave > 0)) { firstAttack = true; signals.Add(TutorialSignals.FirstAttack); }
            if (sim.Leaks > leaks) { leaks = sim.Leaks; signals.Add(TutorialSignals.Leak); }
            if (!cleared && sim.BaseHealth > 0 && (sim.Phase == BattlePhase.Reward || sim.Phase == BattlePhase.Victory))
            { cleared = true; signals.Add(TutorialSignals.WaveCleared); }
            if (!string.IsNullOrWhiteSpace(expectedCode) && Matches(Robot(sim, robotIndex)?.Program, expectedCode)) signals.Add(TutorialSignals.ProgramMatches);
            return signals;
        }
    }
}
