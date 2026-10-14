using System.Collections.Generic;
using System.Linq;

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

        // Generated Python body equals the expected lines, ignoring the def header, indentation and blank lines.
        public static bool Matches(DefenseProgram program, string expected) =>
            program != null && Lines(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1))).SequenceEqual(Lines(expected));
        static IEnumerable<string> Lines(string text) =>
            (text ?? "").Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);

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
