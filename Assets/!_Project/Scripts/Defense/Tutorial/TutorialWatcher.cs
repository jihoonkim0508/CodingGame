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
        bool firstKill, firstAttack, cleared;
        readonly List<string> signals = new List<string>();

        // In 1-3, the closest warrior is Robot.Unit.0 and the newly placed shooter follows the warriors.
        // Elsewhere the stable installation order keeps the existing tutorial targets unchanged.
        public static IReadOnlyList<RobotState> OrderedRobots(DefenseSimulation sim)
        {
            if (sim == null) return Array.Empty<RobotState>();
            var robots = sim.Robots.OrderBy(robot => robot.Id);
            if (sim.Setup.PlayerFlow && sim.Setup.Stage == 2 && (sim.WaveIndex == 0 || sim.WaveIndex == 2 || sim.WaveIndex == 3))
                return robots.OrderBy(robot => sim.IsNewRobotThisWave(robot) && robot.Spec.role == RobotRole.Shooter ? 0 : 1).ThenByDescending(robot => robot.Id).ToArray();
            if (sim.Setup.PlayerFlow && sim.Setup.Stage == 2 && sim.WaveIndex == 2)
                return robots.OrderBy(robot => robot.Spec.role == RobotRole.Shooter ? (sim.IsNewRobotThisWave(robot) ? 0 : 1) : 2).ThenBy(robot => robot.Id).ToArray();
            if (!sim.Setup.PlayerFlow || sim.Setup.Stage != 1 || sim.WaveIndex != 2 ||
                sim.Setup.Waves.Length <= 2 || sim.Setup.Waves[2].Length == 0) return robots.ToArray();
            int route = sim.Setup.Waves[2].OrderBy(group => group.Delay).First().RouteIndex;
            var entrance = sim.Setup.Routes[route].Points[0];
            return robots.OrderBy(robot => robot.Spec.role == RobotRole.Shooter ? 1 : 0)
                .ThenBy(robot => System.Numerics.Vector2.DistanceSquared(robot.Position, entrance))
                .ThenBy(robot => robot.Id).ToArray();
        }
        public static RobotState Robot(DefenseSimulation sim, int index) => OrderedRobots(sim).ElementAtOrDefault(index);
        public static int RobotIndex(DefenseSimulation sim, int id) =>
            OrderedRobots(sim).Select((robot, index) => (robot, index)).Where(entry => entry.robot.Id == id)
                .Select(entry => entry.index).DefaultIfEmpty(-1).First();
        public static RobotState TeachingRobot(DefenseSimulation sim) => Robot(sim, 0);

        // Generated Python body equals the expected lines, ignoring the def header, indentation, blank lines and the names the
        // player gave variables (any name holding the enemy object works, not only `enemy`). The same lines in another order
        // also count: the program only runs once it compiles, so a line that uses a variable before it is set never gets here.
        public static bool HasRepeatAttack(DefenseProgram program, RobotRole role)
        {
            if (program == null) return false;
            bool Suite(IReadOnlyList<CodingGame.BlockCoding.CodeBlock> blocks)
            {
                foreach (var loop in blocks.Where(b => b.Kind == CodingGame.BlockCoding.BlockKind.For))
                {
                    if (loop.Arguments.Count != 1 || loop.Arguments[0].Kind != CodingGame.BlockCoding.BlockKind.Number ||
                        !double.TryParse(loop.Arguments[0].Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var count) || count <= 0 || count != Math.Truncate(count) || count > CodingGame.BlockCoding.PythonTreeCompiler.MaxLoopIterations) continue;
                    var names = new HashSet<string>();
                    bool Actions(IReadOnlyList<CodingGame.BlockCoding.CodeBlock> body)
                    {
                        foreach (var b in body)
                        {
                            if (b.Kind == CodingGame.BlockCoding.BlockKind.DeclareVariable)
                            {
                                names.Remove(b.Value);
                                if (b.Arguments.Count == 1 && b.Arguments[0].Kind == CodingGame.BlockCoding.BlockKind.NearestEnemy) names.Add(b.Value);
                            }
                            bool attack = role == RobotRole.Warrior ? b.Kind == CodingGame.BlockCoding.BlockKind.Slash :
                                b.Kind == CodingGame.BlockCoding.BlockKind.Shot || b.Kind == CodingGame.BlockCoding.BlockKind.Attack;
                            if (attack && b.Arguments.Count == 1 && b.Arguments[0].Kind == CodingGame.BlockCoding.BlockKind.Variable && names.Contains(b.Arguments[0].Value)) return true;
                            if (b.Body.Count > 0)
                            {
                                var saved = names.ToArray();
                                if (Actions(b.Body)) return true;
                                names.Clear(); foreach (var name in saved) names.Add(name);
                            }
                        }
                        return false;
                    }
                    if (Actions(loop.Body)) return true;
                }
                return blocks.Any(b => b.Body.Count > 0 && Suite(b.Body));
            }
            return Suite(program.CopyBlocks());
        }
        public static bool Matches(DefenseProgram program, string expected)
        {
            if (program == null) return false;
            if ((expected ?? "").Contains("for "))
            {
                string[] Shape(string text)
                {
                    var raw = text.Replace("\r", "").Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                    int baseline = raw.Length == 0 ? 0 : raw[0].TakeWhile(char.IsWhiteSpace).Count();
                    var canonical = Canonical(raw.Select(line => line.Trim()).ToList());
                    return raw.Select((line, i) => (line.TakeWhile(char.IsWhiteSpace).Count() - baseline) + ":" + canonical[i]).ToArray();
                }
                return Shape(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1))).SequenceEqual(Shape(expected));
            }
            var actual = Canonical(Lines(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1))));
            var wanted = Canonical(Lines(expected));
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
