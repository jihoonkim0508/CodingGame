using System;
using System.Linq;
using System.Text;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // The wave result's robot debrief: what each robot did and the one thing most worth fixing, so the player can read
    // why the wave went the way it did (code → result) before trying again.
    public static class DefenseDebrief
    {
        // Most serious first; the headline picks the first robot with the smallest issue.
        public enum Issue { NoCode, Fault, NoEnemyInRange, NeverActed, LoopSpun, OutOfRuns, Reduced, None }
        // Enemies this wave let through or left alive (survivors are only counted once the wave has ended, not on a defeat).
        public static int Missed(DefenseSimulation sim) => sim.WaveLeaks + (sim.Phase == BattlePhase.Defeat ? 0 : sim.LastSurvivors);

        public static Issue Diagnose(DefenseSimulation sim, RobotState bot)
        {
            if (bot.Program == null || bot.Program.IsEmpty) return Issue.NoCode;
            if (bot.Program.Fault != null) return Issue.Fault;
            if (bot.ActionsThisWave == 0) return bot.Spec.role == RobotRole.Buffer || bot.SawEnemyThisWave ? Issue.NeverActed : Issue.NoEnemyInRange;
            // The function ended while enemies still got by. A for loop counts turns, not attacks: half its turns or more without
            // one spent the count on false conditions.
            if (!bot.Program.Running && Missed(sim) > 0)
                return bot.LoopTurnsThisWave >= 2 * Math.Max(1, bot.ActionsThisWave) ? Issue.LoopSpun : Issue.OutOfRuns;
            if (sim.Compatibility(bot, bot.Action) == ActionCompatibility.Reduced) return Issue.Reduced;
            return Issue.None;
        }

        public static string Advice(DefenseSimulation sim, RobotState bot, Issue issue)
        {
            string native = DefenseProgression.Label(DefenseProgression.ActionBlock(bot.Spec.NativeAction));
            switch (issue)
            {
                case Issue.NoCode: return "코드가 없어 아무것도 하지 않았어요. 로봇을 클릭해 '개발'에서 코드를 넣어 보세요.";
                case Issue.Fault: return "코드가 실행 중에 멈췄어요: " + bot.Program.Fault;
                case Issue.NoEnemyInRange: return "사거리 안에 적이 한 번도 들어오지 않았어요. 길 가까이 옮겨 보세요.";
                case Issue.NeverActed:
                    if (bot.Spec.role == RobotRole.Buffer) return "사거리 안에 다른 로봇이 없어 버프를 못 했어요. 공격 로봇 옆에 두세요.";
                    return bot.Program.BlockCount(BlockKind.If) > 0
                        ? "적이 사거리에 들어왔지만 공격하지 않았어요. if 조건이 참이 되는 값인지 확인해 보세요."
                        : "적이 사거리에 들어왔지만 공격 명령이 실행되지 않았어요. 공격 블록과 대상 칸을 확인해 보세요.";
                case Issue.LoopSpun:
                    return $"for 반복이 {bot.LoopTurnsThisWave}번 도는 동안 공격은 {bot.ActionsThisWave}번뿐이었어요. if 조건이 거짓이거나 노린 적이 이미 사라져도 반복 횟수는 줄어요. " +
                        (sim.Owned(BlockKind.While) > 0 ? "끝까지 공격하려면 while True를 써 보세요." : "range() 횟수를 늘리거나 함수가 끝나면 로봇 아래 '실행'으로 다시 불러 보세요.");
                case Issue.OutOfRuns:
                    return $"함수가 끝나 멈춰 있었어요(이번 웨이브 {bot.Program.CompletedRuns}번 실행). 적이 올 때 로봇 아래 '실행'을 눌러 다시 부르거나 " +
                        (sim.LoopsUnlocked ? "반복문으로 계속 공격하게 해 보세요." : "함수 안에 공격 블록을 더 넣어 보세요.");
                case Issue.Reduced: return $"다른 직군 명령이라 힘이 10%만 나왔어요. 이 로봇은 {native}가 제일 강해요.";
                default: return null;
            }
        }

        // The tag over a robot's head: missing or stopped code at any time, and in battle a function that has ended (the robot
        // waits for its Play button). Null: nothing to show.
        public static string LiveStatus(DefenseSimulation sim, RobotState bot)
        {
            if (bot.AutoExecute) return null;
            if (bot.Program == null || bot.Program.IsEmpty) return sim.Setup.PlayerFlow ? Tag("E8590C", "코드 없음") : null;
            if (bot.Program.Fault != null) return Tag("E03131", "코드 오류");
            if (sim.Phase != BattlePhase.Running && sim.Phase != BattlePhase.Paused) return null;
            return bot.Program.Running ? null : Tag("E8590C", "함수 끝");
        }
        // On the result screen: the robot's debrief name over its head, yellow when the debrief has advice for it.
        public static string ResultTag(DefenseSimulation sim, RobotState bot, string name) => Tag(Diagnose(sim, bot) == Issue.None ? "1971C2" : "E8590C", name);
        // The HUD draws the dark backing as an Image: TMP's <mark> is an overlay on top of the letters.
        static string Tag(string color, string text) => $"<color=#{color}>{text}</color>";

        // One robot's part of the debrief. Advice is null for the robot the headline already speaks about.
        public sealed class Line
        {
            public RobotState Bot;
            public Issue Issue;
            public string Name, Stats, Advice;
            public bool Mvp;
        }
        // The headline (rich text) and a line per robot, for the reward panel's cards.
        public static (string headline, Line[] lines) Summary(DefenseSimulation sim, Func<RobotState, string> name)
        {
            if (sim.Robots.Count == 0) return ("", Array.Empty<Line>());
            var issues = sim.Robots.Select(bot => (bot, issue: Diagnose(sim, bot))).ToList();
            var worst = issues.OrderBy(x => x.issue).First();
            var mvp = sim.Robots.OrderByDescending(bot => bot.DamageThisWave).First();
            int missed = Missed(sim);
            string tip = sim.Phase == BattlePhase.Defeat ? "다시 도전 팁" : sim.Phase == BattlePhase.Victory ? "다음 스테이지 팁" : "다음 웨이브 팁";
            // The headline carries the most serious robot's advice, so that robot's line below shows only its numbers. Only the
            // label is coloured, light enough for the blue result panel: orange and green text there had the panel's lightness
            // (contrast 1.1) and looked broken (play feedback).
            string headline = worst.issue != Issue.None ? $"<color=#FFE066><b>{tip}</b></color> · {name(worst.bot)}: {Advice(sim, worst.bot, worst.issue)}"
                : missed == 0 ? $"<color=#8CE99A><b>완벽 방어!</b></color> MVP {name(mvp)} · 피해 {mvp.DamageThisWave:0}"
                : $"<color=#FFE066><b>{tip}</b></color> · 모든 로봇이 일했지만 {missed}마리를 놓쳤어요. 로봇을 더 놓거나 길이 꺾이는 곳 가까이 옮겨 보세요.";
            var lines = issues.Select(x => new Line
            {
                Bot = x.bot, Issue = x.issue, Name = name(x.bot), Mvp = x.bot == mvp && mvp.DamageThisWave > 0,
                Stats = x.bot.Spec.role == RobotRole.Buffer ? $"버프 {x.bot.ActionsThisWave}" : $"공격 {x.bot.ActionsThisWave} · 피해 {x.bot.DamageThisWave:0} · 처치 {x.bot.KillsThisWave}",
                Advice = x.bot == worst.bot ? null : Advice(sim, x.bot, x.issue),
            }).ToArray();
            return (headline, lines);
        }

        // 디버's word beside the result panel: cheer a clean wave, own up to a leak, and after a loss point at the fix.
        public static (GuideMood mood, string line) GuideSays(DefenseSimulation sim)
        {
            switch (sim.Phase)
            {
                case BattlePhase.Defeat: return (GuideMood.Determined, "아직 안 끝났어!\n팁 보고 코드를 고쳐서 다시 가자.");
                case BattlePhase.Victory:
                    int stars = sim.StageStars;
                    return stars >= 3 ? (GuideMood.Happy, "별 세 개다!\n코어가 끄떡없이 버텼어!")
                        : (GuideMood.Happy, $"이 구역은 지켜 냈어!\n코어를 {DefenseSimulation.CoreForStars(stars + 1, sim.Setup.BaseHealth)} 이상 지키면 별 하나 더!");
                default:
                    if (sim.LastWaveTimedOut && sim.LastSurvivors > 0) return (GuideMood.Worried, $"앗, 시간이 다 됐어!\n남은 {sim.LastSurvivors}마리가 다음 웨이브에 합류해.");
                    if (sim.WaveLeaks > 0) return (GuideMood.Worried, $"{sim.WaveLeaks}마리가 코어까지 갔어.\n팁 보고 길목을 다시 막자!");
                    return (GuideMood.Happy, "완벽 방어!\n한 마리도 못 지나갔어!");
            }
        }

        // The same as one rich-text block: headline, then each robot's numbers with its advice under them.
        public static string Report(DefenseSimulation sim, Func<RobotState, string> name)
        {
            var (headline, lines) = Summary(sim, name);
            var text = new StringBuilder(headline);
            foreach (var line in lines)
            {
                text.Append($"\n\n<b>{line.Name}</b>   {line.Stats}");
                if (line.Advice != null) text.Append($"\n<color=#6B7085><size=90%>{line.Advice}</size></color>");
            }
            return text.ToString();
        }
    }
}
