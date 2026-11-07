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
    static CodeBlock Slash() => new CodeBlock(BlockKind.Slash);
    static CodeBlock Forever(params CodeBlock[] body) { var loop = new CodeBlock(BlockKind.While); loop.Body.AddRange(body); return loop; }
    static void RunToEnd(DefenseSimulation sim) { sim.Start(); for (int i = 0; i < 60 * 60 && sim.Phase == BattlePhase.Running; i++) sim.Advance(1.0 / 60); }

    public static void Run()
    {
        var sim = Sim(10, 3);
        var worker = Bot(sim, RobotRole.Warrior, -8, 2, Forever(Slash()));
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
        var (_, cards) = DefenseDebrief.Summary(sim, bot => "R" + bot.Id);
        Assert(cards.Length == 4 && cards.Single(c => c.Mvp).Bot == worker &&
            cards.Single(c => c.Advice == null && c.Issue != DefenseDebrief.Issue.None).Bot == empty,
            "cards: the top damage dealer is MVP, the headline robot's card carries no advice");
        Assert(DefenseDebrief.ResultTag(sim, empty, "슈터").Contains("E8590C") && DefenseDebrief.ResultTag(sim, empty, "슈터").Contains("슈터") &&
            DefenseDebrief.ResultTag(sim, worker, "전사형 1").Contains("1971C2"), "result tags name the robot, yellow when it has advice");

        var tough = Sim(1000, 1);
        var tired = Bot(tough, RobotRole.Warrior, -8, 2, Slash());
        RunToEnd(tough);
        Assert(tough.WaveLeaks == 1 && !tired.Program.Running && tired.Program.CompletedRuns == 1, "one call, one swing: the tough enemy walks past the ended function");
        Assert(DefenseDebrief.Diagnose(tough, tired) == DefenseDebrief.Issue.OutOfRuns &&
            DefenseDebrief.Advice(tough, tired, DefenseDebrief.Issue.OutOfRuns).Contains("'실행'"), "an ended function's advice is Play or a loop");

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
        Assert(DefenseDebrief.GuideSays(tough).mood == GuideMood.Happy && DefenseDebrief.GuideSays(tough).line.Contains("별 세 개"),
            "디버 cheers three stars at the stage clear");
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
        Assert(Tag(live, runner) == "" && runner.Program.Running, "a running function has no tag");
        for (int i = 0; i < 60 * 30 && runner.Program.Running; i++) live.Advance(1.0 / 60);
        Assert(Tag(live, runner).Contains("함수 끝"), "an ended function says so");
        Assert(live.Invoke(runner.Id) && Tag(live, runner) == "" && !live.Invoke(runner.Id), "Play calls it again, and not twice at once");
        Assert(broken.Program.Fault != null && Tag(live, broken).Contains("코드 오류"), "stopped code is tagged");
        var campaign = Sim(10, 1, playerFlow: true);
        Assert(Tag(campaign, Bot(campaign, RobotRole.Shooter, -6, -2)).Contains("코드 없음"), "a campaign robot without code is tagged while preparing");
        CheckLoops(Tag);
    }

    // One run a wave (as once loops unlock): enemies 4 s apart, each dies to one slash, a robot mid-road.
    static DefenseSimulation OneRun()
    {
        var sim = Sim(5, 3); sim.Setup.Waves[0][0].Interval = 4;
        return sim;
    }
    // Until the robot's function has gone idle and an enemy is within its range.
    static void Near(DefenseSimulation sim, RobotState bot)
    {
        for (int i = 0; i < 60 * 30 && (bot.Program.Running || !sim.Enemies.Any(e => e.Active && Vector2.Distance(e.Position, bot.Position) <= bot.Range)); i++)
            sim.Advance(1.0 / 60);
    }
    static void CheckLoops(Func<DefenseSimulation, RobotState, string> tag)
    {
        var sim = OneRun();
        var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, "3")); loop.Body.Add(Slash());
        var waiter = Bot(sim, RobotRole.Warrior, 0, 2, loop);
        sim.Start();
        for (int i = 0; i < 60; i++) sim.Advance(1.0 / 60);
        Assert(waiter.Executions == 3 && waiter.KillsThisWave == 0 && !waiter.Program.Running && tag(sim, waiter).Contains("함수 끝"),
            "range(3) is three swings at once, at nobody while the enemies are far");
        Near(sim, waiter);
        Assert(sim.Invoke(waiter.Id), "Play once the first enemy is near");
        for (int i = 0; i < 60; i++) sim.Advance(1.0 / 60);
        Assert(waiter.KillsThisWave == 1 && waiter.LoopTurnsThisWave == 6, "the swings land when Play is pressed with the enemy near");

        var stale = OneRun();
        var spin = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, "30"));
        // slash() has no target; the shooter's attack(enemy) is what can hold an enemy that is gone.
        spin.Body.Add(new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, "enemy")));
        var spinner = Bot(stale, RobotRole.Shooter, 0, 2, new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)), spin);
        stale.Start(); Near(stale, spinner); stale.Invoke(spinner.Id);
        for (int i = 0; i < 60 * 60 && stale.Phase == BattlePhase.Running; i++) stale.Advance(1.0 / 60);
        Assert(spinner.KillsThisWave == 1 && spinner.LoopTurnsThisWave == 60 && stale.WaveLeaks == 2, "turns on a gone or far enemy spend the count"); // shots still in flight let it fire at the same enemy a few times
        Assert(DefenseDebrief.Diagnose(stale, spinner) == DefenseDebrief.Issue.LoopSpun &&
            DefenseDebrief.Advice(stale, spinner, DefenseDebrief.Issue.LoopSpun).Contains("60번 도는 동안 공격은"), $"spun loop is the advice: {DefenseDebrief.Diagnose(stale, spinner)} turns={spinner.LoopTurnsThisWave} actions={spinner.ActionsThisWave}");
    }
}
