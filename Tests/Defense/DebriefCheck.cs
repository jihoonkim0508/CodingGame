using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// The wave result debrief: per-robot numbers this wave and which robot's advice heads the report.
static class DebriefCheck
{
    static int checks;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, range = 5, interval = .1f, damage = 5 };
    static DefenseSimulation Sim(float health, int count, bool playerFlow = false) => new DefenseSimulation(new BattleSetup { Progression = FixtureProgression.Create(), PlayerFlow = playerFlow,
        Min = new Vector2(-12, -8), Max = new Vector2(12, 8), ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1.1f) },
        Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = health, speed = 1 }, Count = count, Interval = .1f } } } });
    static RobotState Bot(DefenseSimulation sim, RobotRole role, float x, float y, params CodeBlock[] code)
    {
        var bot = sim.Place(Spec(role), (int)role, new Vector2(x, y), out var error);
        if (bot == null) throw new Exception(error);
        if (code.Length > 0) sim.ApplyProgram(bot.Id, "robot_" + bot.Id, code);
        return bot;
    }
    static CodeBlock Slash() => new CodeBlock(BlockKind.Slash, "0", new CodeBlock(BlockKind.NearestEnemy));
    static void RunToEnd(DefenseSimulation sim) { sim.Start(); for (int i = 0; i < 60 * 60 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60); }

    public static void Run()
    {
        var sim = Sim(10, 3);
        var worker = Bot(sim, RobotRole.Warrior, -8, 2, Slash());
        var far = Bot(sim, RobotRole.Warrior, 8, 7, Slash());
        var empty = Bot(sim, RobotRole.Shooter, -6, -2);
        var never = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.False)); never.Body.Add(Slash());
        var stuck = Bot(sim, RobotRole.Warrior, -9, -2, never);
        RunToEnd(sim);
        Assert(sim.Phase == BattlePhase.Victory && sim.WaveLeaks == 0, "debrief wave cleared");
        Assert(worker.KillsThisWave == 3 && Math.Abs(worker.DamageThisWave - 30) < .001f && worker.ActionsThisWave >= 2, "damage counts what the enemies lost, kills credit the robot");
        Assert(DefenseDebrief.Diagnose(sim, worker) == DefenseDebrief.Issue.None, "working robot has no issue");
        Assert(DefenseDebrief.Diagnose(sim, far) == DefenseDebrief.Issue.NoEnemyInRange && !far.SawEnemyThisWave, "robot away from the road saw no enemy");
        Assert(DefenseDebrief.Diagnose(sim, empty) == DefenseDebrief.Issue.NoCode, "robot without code");
        Assert(DefenseDebrief.Diagnose(sim, stuck) == DefenseDebrief.Issue.NeverActed && stuck.SawEnemyThisWave, "false condition never acted");
        string report = DefenseDebrief.Report(sim, bot => "R" + bot.Id);
        int Count(string text, string part) { int n = 0; for (int i = report.IndexOf(part); i >= 0; i = report.IndexOf(part, i + 1)) n++; return n; }
        Assert(report.StartsWith("<color") && report.Contains("R" + empty.Id + ": 코드가 없어"), "headline names the most serious robot");
        Assert(Count(report, "코드가 없어") == 1 && report.Contains("if 조건") && report.Contains("길 가까이"), "every other issue listed once under its robot");

        var tough = Sim(1000, 1);
        var tired = Bot(tough, RobotRole.Warrior, -8, 2, Slash());
        RunToEnd(tough);
        Assert(tough.WaveLeaks == 1 && tired.Program.Completed(tough), "tough enemy outlasts five runs");
        Assert(DefenseDebrief.Diagnose(tough, tired) == DefenseDebrief.Issue.OutOfRuns &&
            DefenseDebrief.Advice(tough, tired, DefenseDebrief.Issue.OutOfRuns).Contains("5/5"), "used-up runs are the advice");

        var weak = Sim(.4f, 1);
        var offRole = Bot(weak, RobotRole.Shooter, -8, 2, Slash());
        RunToEnd(weak);
        Assert(weak.WaveLeaks == 0 && DefenseDebrief.Diagnose(weak, offRole) == DefenseDebrief.Issue.Reduced, "another role's command is flagged");
        Assert(DefenseDebrief.Report(weak, bot => "R").Contains("10%"), "a cleared wave still leads with the reduced-strength advice");
        var clean = Sim(.4f, 1);
        Bot(clean, RobotRole.Warrior, -8, 2, Slash());
        RunToEnd(clean);
        Assert(DefenseDebrief.Report(clean, bot => "R").Contains("완벽 방어!</b> MVP R"), "clean wave without issues praises the MVP");
        CheckLiveStatus();
        Assert(DefenseSimulation.Stars(12, 12) == 3 && DefenseSimulation.Stars(10, 12) == 3 && DefenseSimulation.Stars(9, 12) == 2 &&
            DefenseSimulation.Stars(5, 12) == 2 && DefenseSimulation.Stars(4, 12) == 1, "12 HP core: 10 for three stars, 5 for two");
        bool thresholds = true;
        for (int max = 1; max <= 30; max++) for (int s = 2; s <= 3; s++)
        {
            int need = DefenseSimulation.CoreForStars(s, max);
            thresholds &= DefenseSimulation.Stars(need, max) >= s && (need == 1 || DefenseSimulation.Stars(need - 1, max) < s);
        }
        Assert(thresholds, "the core the next star asks for is exactly where the star starts");
        Assert(tough.StageStars == 3 && tough.BaseHealth == 11, "one leak still earns three stars");
        Assert(new DefenseSimulation(tough.Setup).StageStars == 0, "no stars before the stage is won");
        Console.WriteLine($"Debrief checks passed: {checks}");
    }

    // The tag over a robot's head during battle.
    static void CheckLiveStatus()
    {
        string Tag(DefenseSimulation s, RobotState bot) => DefenseDebrief.LiveStatus(s, bot) ?? "";
        var live = Sim(1000, 1);
        var runner = Bot(live, RobotRole.Warrior, -8, 2, Slash());
        var sandbox = Bot(live, RobotRole.Shooter, -6, -2);
        var broken = Bot(live, RobotRole.Shooter, -4, -2, new CodeBlock(BlockKind.Wait, "0", new CodeBlock(BlockKind.Number, "-1")));
        Assert(Tag(live, runner) == "" && Tag(live, sandbox) == "", "no tag while preparing, none for a sandbox robot without code");
        live.Start();
        Assert(Tag(live, runner).Contains("함수 0/5"), "runs shown against the wave's limit in battle");
        for (int i = 0; i < 60 * 30 && !runner.Program.Completed(live); i++) live.Advance(1.0 / 60);
        Assert(Tag(live, runner).Contains("함수 5/5 끝"), "used-up runs say the function ended");
        Assert(broken.Program.Fault != null && Tag(live, broken).Contains("코드 오류"), "stopped code is tagged");
        var campaign = Sim(10, 1, playerFlow: true);
        Assert(Tag(campaign, Bot(campaign, RobotRole.Shooter, -6, -2)).Contains("코드 없음"), "a campaign robot without code is tagged while preparing");
    }
}
