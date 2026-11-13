using System;
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
        bool firstKill, firstAttack, cleared, ended, near;
        double swingEnds, playEnds;
        int runsSeen;
        DefenseProgram watchedProgram;
        readonly List<string> signals = new List<string>();

        // Installation order: Robot.Unit.0 is the first robot placed (1-3's loop lesson teaches the warrior from 1-1).
        public static IReadOnlyList<RobotState> OrderedRobots(DefenseSimulation sim) =>
            sim == null ? Array.Empty<RobotState>() : sim.Robots.OrderBy(robot => robot.Id).ToArray();
        public static RobotState Robot(DefenseSimulation sim, int index) => OrderedRobots(sim).ElementAtOrDefault(index);
        public static int RobotIndex(DefenseSimulation sim, int id) =>
            OrderedRobots(sim).Select((robot, index) => (robot, index)).Where(entry => entry.robot.Id == id)
                .Select(entry => entry.index).DefaultIfEmpty(-1).First();
        public static RobotState TeachingRobot(DefenseSimulation sim) => Robot(sim, 0);

        // Generated Python body equals the expected lines, ignoring the def header, indentation, blank lines and the names the
        // player gave variables (any name holding the enemy object works, not only `enemy`). The same lines in another order
        // also count: the program only runs once it compiles, so a line that uses a variable before it is set never gets here.
        // Any loop count of one or more counts as the lesson's (play feedback 3: a player who typed range(100) into 1-1's new
        // loop was stuck on the next step).
        public static bool Matches(DefenseProgram program, string expected)
        {
            if (program == null) return false;
            var actual = Canonical(Lines(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1)))).Select(AnyCount).ToList();
            var wanted = Canonical(Lines(expected)).Select(AnyCount).ToList();
            return actual.SequenceEqual(wanted) || actual.OrderBy(l => l).SequenceEqual(wanted.OrderBy(l => l));
        }
        public static bool ConditionMatches(string condition, DefenseProgram program, string expected, string reference)
        {
            string Bare(string text) => Regex.Replace(text ?? "", @"\s", "");
            if (program == null || string.IsNullOrWhiteSpace(reference)) return Bare(condition) == Bare(expected);
            var actual = Canonical(Lines(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1))).Append("if " + condition).ToList());
            var wanted = Canonical(Lines(reference).Append("if " + expected).ToList());
            return Bare(actual.Last()) == Bare(wanted.Last());
        }
        static string AnyCount(string line) => Regex.Replace(line, @"\brange\([1-9]\d*\)", "range(n)");
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
                session = sim; wave = sim.WaveIndex; killsAtWaveStart = sim.Kills; leaks = sim.Leaks; firstKill = firstAttack = cleared = ended = near = false; swingEnds = playEnds = -1;
            }
            if (!firstKill && sim.Kills > killsAtWaveStart) { firstKill = true; signals.Add(TutorialSignals.FirstKill); }
            // 1-3 waits for the lesson's shooter, not a warrior from 1-1 or 1-2 swinging first.
            bool lesson = sim.Setup.PlayerFlow && sim.Setup.Stage == 1 && sim.WaveIndex == 2;
            if (!firstAttack && (lesson ? TeachingRobot(sim)?.AttacksThisWave > 0 : sim.Robots.Any(r => r.AttacksThisWave > 0)))
            { firstAttack = true; signals.Add(TutorialSignals.FirstAttack); }
            if (sim.Leaks > leaks) { leaks = sim.Leaks; signals.Add(TutorialSignals.Leak); }
            var teacher = TeachingRobot(sim);
            // Once the first run has ended and its swing has played out (the robot could act again), at any attack speed; a
            // later 실행 does not push it back.
            if (swingEnds < 0 && teacher?.Program != null && teacher.Program.CompletedRuns > 0) swingEnds = teacher.NextAction;
            if (!ended && swingEnds >= 0 && sim.Time + 1e-6 >= swingEnds) { ended = true; signals.Add(TutorialSignals.FunctionEnded); }
            // Every run's end once its swing has played out, so a line about a 실행 press waits until that swing has landed.
            if (teacher?.Program != watchedProgram) { watchedProgram = teacher?.Program; runsSeen = 0; }
            if (watchedProgram != null && watchedProgram.CompletedRuns > runsSeen) { runsSeen = watchedProgram.CompletedRuns; playEnds = teacher.NextAction; }
            if (playEnds >= 0 && sim.Time + 1e-6 >= playEnds) { playEnds = -1; signals.Add(TutorialSignals.PlayEnded); }
            if (!near && teacher != null && sim.Phase == BattlePhase.Running && sim.Enemies.Any(e => e.Active &&
                System.Numerics.Vector2.DistanceSquared(e.Position, teacher.Position) <= teacher.Range * teacher.Range))
            { near = true; signals.Add(TutorialSignals.EnemyInRange); }
            if (!cleared && sim.BaseHealth > 0 && (sim.Phase == BattlePhase.Reward || sim.Phase == BattlePhase.Victory))
            { cleared = true; signals.Add(TutorialSignals.WaveCleared); }
            if (!string.IsNullOrWhiteSpace(expectedCode) && Matches(Robot(sim, robotIndex)?.Program, expectedCode)) signals.Add(TutorialSignals.ProgramMatches);
            return signals;
        }
    }
}
