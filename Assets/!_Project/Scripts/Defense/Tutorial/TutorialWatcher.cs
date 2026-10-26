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
        bool firstKill, firstAttack, cleared;
        readonly List<string> signals = new List<string>();

        // The index-th warrior placed this stage (by id): 0 is the robot the tutorial teaches, 1 the second warrior of 1-4.
        public static RobotState Robot(DefenseSimulation sim, int index) =>
            sim?.Robots.Where(r => r.Spec.role == RobotRole.Warrior).OrderBy(r => r.Id).ElementAtOrDefault(index);
        public static RobotState TeachingRobot(DefenseSimulation sim) => Robot(sim, 0);

        // Generated Python body equals the expected lines, ignoring the def header, indentation, blank lines and the names the
        // player gave variables (any name holding the enemy object works, not only `enemy`). The same lines in another order
        // also count: the program only runs once it compiles, so a line that uses a variable before it is set never gets here.
        public static bool Matches(DefenseProgram program, string expected)
        {
            if (program == null) return false;
            var actual = Canonical(Lines(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1))));
            var wanted = Canonical(Lines(expected));
            return actual.SequenceEqual(wanted) || actual.OrderBy(l => l).SequenceEqual(wanted.OrderBy(l => l));
        }
        // A loose condition (1-3 C03) with the player's own variable names: names are matched by the order the robot's code
        // sets them against reference, the code taught before (`dist <= my_attack_range()` passes for `enemy_distance <= …`).
        public static bool ConditionMatches(string condition, DefenseProgram program, string expected, string reference)
        {
            string Bare(string text) => Regex.Replace(text ?? "", @"\s", "");
            if (program == null || string.IsNullOrWhiteSpace(reference)) return Bare(condition) == Bare(expected);
            var actual = Canonical(Lines(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1))).Append("if " + condition).ToList());
            var wanted = Canonical(Lines(reference).Append("if " + expected).ToList());
            return Bare(actual.Last()) == Bare(wanted.Last());
        }
        static List<string> Lines(string text) =>
            (text ?? "").Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
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
                session = sim; wave = sim.WaveIndex; killsAtWaveStart = sim.Kills; leaks = sim.Leaks; firstKill = firstAttack = cleared = false;
            }
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
