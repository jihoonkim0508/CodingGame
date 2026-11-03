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
        public enum Issue { NoCode, Fault, NoEnemyInRange, NeverActed, OutOfRuns, Reduced, None }
        // Enemies this wave let through or left alive (survivors are only counted once the wave has ended, not on a defeat).
        public static int Missed(DefenseSimulation sim) => sim.WaveLeaks + (sim.Phase == BattlePhase.Defeat ? 0 : sim.LastSurvivors);

        public static Issue Diagnose(DefenseSimulation sim, RobotState bot)
        {
            if (bot.Program == null || bot.Program.IsEmpty) return Issue.NoCode;
            if (bot.Program.Fault != null) return Issue.Fault;
            if (bot.ActionsThisWave == 0) return bot.Spec.role == RobotRole.Buffer || bot.SawEnemyThisWave ? Issue.NeverActed : Issue.NoEnemyInRange;
            if (bot.Program.Completed(sim) && Missed(sim) > 0) return Issue.OutOfRuns;
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
                case Issue.OutOfRuns:
                    return $"함수 {bot.Program.CompletedRuns}/{sim.FunctionRepeatLimit}회를 다 써서 일찍 멈췄어요. " +
                        (sim.LoopsUnlocked ? "반복문으로 계속 공격하게 해 보세요." : "함수 안에 공격 블록을 더 넣으면 한 번에 여러 번 공격해요.");
                case Issue.Reduced: return $"다른 직군 명령이라 힘이 10%만 나왔어요. 이 로봇은 {native}가 제일 강해요.";
                default: return null;
            }
        }

        // The tag over a robot's head: missing or stopped code at any time, and in battle its function runs against the wave's
        // limit (5 before loops unlock), so a robot that stops attacking shows why. Null: nothing to show.
        public static string LiveStatus(DefenseSimulation sim, RobotState bot)
        {
            if (bot.AutoExecute) return null;
            if (bot.Program == null || bot.Program.IsEmpty) return sim.Setup.PlayerFlow ? Tag("FFD36B", "코드 없음") : null;
            if (bot.Program.Fault != null) return Tag("FF7A7A", "코드 오류");
            if (sim.Phase != BattlePhase.Running && sim.Phase != BattlePhase.Paused) return null;
            int limit = sim.FunctionRepeatLimit;
            if (bot.Program.Completed(sim)) return Tag("FFD36B", limit > 1 ? $"함수 {limit}/{limit} 끝" : "함수 끝");
            return limit > 1 && limit < 1000 ? Tag("CFE8FF", $"함수 {bot.Program.CompletedRuns}/{limit}") : null;
        }
        static string Tag(string color, string text) => $"<mark=#0B1620D0 padding=\"60,60,25,25\"><color=#{color}>{text}</color></mark>";

        // One stat line per robot, its advice under it, and a headline on top. Rich text for the reward panel.
        public static string Report(DefenseSimulation sim, Func<RobotState, string> name)
        {
            if (sim.Robots.Count == 0) return "";
            var issues = sim.Robots.Select(bot => (bot, issue: Diagnose(sim, bot))).ToList();
            var text = new StringBuilder();
            var worst = issues.OrderBy(x => x.issue).First();
            int missed = Missed(sim);
            string tip = sim.Phase == BattlePhase.Defeat ? "다시 도전 팁" : "다음 웨이브 팁";
            // The headline carries the most serious robot's advice, so that robot's line below shows only its numbers.
            if (worst.issue != Issue.None) text.Append($"<color=#FFD36B><b>{tip}</b> · {name(worst.bot)}: {Advice(sim, worst.bot, worst.issue)}</color>");
            else if (missed == 0)
            {
                var mvp = sim.Robots.OrderByDescending(bot => bot.DamageThisWave).First();
                text.Append($"<color=#7FF0B0><b>완벽 방어!</b> MVP {name(mvp)} · 피해 {mvp.DamageThisWave:0}</color>");
            }
            else text.Append($"<color=#FFD36B><b>{tip}</b> · 모든 로봇이 일했지만 {missed}마리를 놓쳤어요. 로봇을 더 놓거나 길이 꺾이는 곳 가까이 옮겨 보세요.</color>");
            foreach (var (bot, issue) in issues)
            {
                string actions = bot.Spec.role == RobotRole.Buffer ? $"버프 {bot.ActionsThisWave}" : $"공격 {bot.ActionsThisWave} · 피해 {bot.DamageThisWave:0} · 처치 {bot.KillsThisWave}";
                text.Append($"\n\n<b>{name(bot)}</b>   {actions}");
                string advice = bot == worst.bot ? null : Advice(sim, bot, issue);
                if (advice != null) text.Append($"\n<color=#B9C7D2><size=90%>{advice}</size></color>");
            }
            return text.ToString();
        }
    }
}
