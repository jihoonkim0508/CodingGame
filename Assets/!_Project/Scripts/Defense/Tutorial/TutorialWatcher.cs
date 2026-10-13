using System.Collections.Generic;
using System.Linq;

namespace CodingGame.Defense
{
    // Turns live simulation changes into tutorial signals (TutorialSignals.FirstKill/Leak/WaveCleared/ProgramMatches).
    // Plain C# so the headless checks cover it; TutorialDirector polls it once per frame.
    public sealed class TutorialWatcher
    {
        DefenseSimulation session;
        int wave = -1, killsAtWaveStart, leaks;
        bool firstKill, cleared;
        readonly List<string> signals = new List<string>();

        // The robot the tutorial teaches: the first warrior placed this stage.
        public static RobotState TeachingRobot(DefenseSimulation sim) =>
            sim?.Robots.Where(r => r.Spec.role == RobotRole.Warrior).OrderBy(r => r.Id).FirstOrDefault();

        // Generated Python body equals the expected lines, ignoring the def header, indentation and blank lines.
        public static bool Matches(DefenseProgram program, string expected) =>
            program != null && Lines(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1))).SequenceEqual(Lines(expected));
        static IEnumerable<string> Lines(string text) =>
            (text ?? "").Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);

        // Signals raised since the last poll. expectedCode (optional) adds ProgramMatches while the teaching robot's code matches it.
        public IReadOnlyList<string> Poll(DefenseSimulation sim, string expectedCode = null)
        {
            signals.Clear();
            if (sim == null) return signals;
            if (sim != session || sim.WaveIndex != wave)
            {
                session = sim; wave = sim.WaveIndex; killsAtWaveStart = sim.Kills; leaks = sim.Leaks; firstKill = cleared = false;
            }
            if (!firstKill && sim.Kills > killsAtWaveStart) { firstKill = true; signals.Add(TutorialSignals.FirstKill); }
            if (sim.Leaks > leaks) { leaks = sim.Leaks; signals.Add(TutorialSignals.Leak); }
            if (!cleared && sim.BaseHealth > 0 && (sim.Phase == BattlePhase.Reward || sim.Phase == BattlePhase.Victory))
            { cleared = true; signals.Add(TutorialSignals.WaveCleared); }
            if (!string.IsNullOrWhiteSpace(expectedCode) && Matches(TeachingRobot(sim)?.Program, expectedCode)) signals.Add(TutorialSignals.ProgramMatches);
            return signals;
        }
    }
}
