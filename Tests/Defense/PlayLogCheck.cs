using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// The play log behind the F1 jump's stage cache: a fresh session replaying a play's inputs to a tick stands where the play stood.
static class PlayLogCheck
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, damage = 4, range = 8, interval = .1f, projectileSpeed = 200 };
    static BattleSetup Setup() => new BattleSetup { Progression = FixtureProgression.Create(), TestOptions = new DefenseTestOptions(),
        PlayerFlow = true, Stage = 2, Min = new Vector2(-20, -10), Max = new Vector2(20, 10), BaseHealth = 7,
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1.1f) },
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Waves = Enumerable.Range(0, 3).Select(i => new[] { new SpawnGroup { Count = 3 + i, Interval = .4f, Enemy = new EnemySpec { health = 1, speed = 1.5f } } }).ToArray() };
    static void Frames(DefenseSimulation sim, int frames, double frame) { for (int i = 0; i < frames; i++) sim.Advance(frame); }

    public static void Run()
    {
        var shot = new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.NearestEnemy));
        var loop = new CodeBlock(BlockKind.While); loop.Body.Add(shot); loop.Body.Add(new CodeBlock(BlockKind.If, "0", (CodeBlock)null));
        var flat = PlayLog.Unflatten(PlayLog.Flatten(new[] { loop, new CodeBlock(BlockKind.Slash) }));
        Assert(flat.Count == 2 && flat[0].Body.Count == 2 && flat[0].Body[0].Arguments[0].Kind == BlockKind.NearestEnemy &&
            flat[0].Body[1].Arguments.Count == 1 && flat[0].Body[1].Arguments[0] == null && flat[1].Kind == BlockKind.Slash, "a block tree survives flattening, empty slots too");

        // A play at uneven frame lengths (speed, slow motion), with a pause, a Play press, a claim and the next wave's preparation.
        var play = new DefenseSimulation(Setup());
        var bot = play.Place(Spec(RobotRole.Ranged), (int)RobotRole.Ranged, new Vector2(0, 3), out var error);
        Assert(bot != null, "shooter placed: " + error);
        play.ApplyProgram(bot.Id, "fire", new[] { shot });
        play.Start(); Frames(play, 40, 1.0 / 23); play.TogglePause(); Frames(play, 10, .05); play.TogglePause();
        Frames(play, 30, 1.0 / 7); play.Invoke(bot.Id); Frames(play, 1200, 1.0 / 31);
        Assert(play.Phase == BattlePhase.Reward && play.Kills > 0, $"setup: the first wave ends in its reward ({play.Phase}, {play.Kills} kills)");
        int atReward = play.Inputs.Count; long rewardTick = play.Ticks; string rewardState = PlayLog.Digest(play);
        play.ClaimRewards(); play.SetCoins(200); play.UpgradeRobot(bot.Id); play.ApplyProgram(bot.Id, "fire", new[] { loop.Body[0] });
        play.Start(); Frames(play, 100, 1.0 / 45);
        Assert(play.Phase == BattlePhase.Running, "setup: the second wave runs (" + play.Phase + ")");

        var replay = new DefenseSimulation(Setup());
        Assert(PlayLog.Replay(replay, play.Inputs, atReward, rewardTick) == null && PlayLog.Digest(replay) == rewardState, "replayed to the reward, the session is as the play was there");
        var whole = new DefenseSimulation(Setup());
        Assert(PlayLog.Replay(whole, play.Inputs, play.Inputs.Count, play.Ticks) == null && PlayLog.Digest(whole) == PlayLog.Digest(play),
            "replayed to the end, the session is as the play is:\n" + PlayLog.Digest(whole) + "\n" + PlayLog.Digest(play));
        Assert(whole.Inputs.Count == play.Inputs.Count, "a replay logs the same inputs again");
        // As read back from a file: no robot specs (the definitions' own), code as text lines.
        var read = play.Inputs.Select(i => new PlayInput { tick = i.tick, kind = i.kind, name = i.name, id = i.id, index = i.index, count = i.count, x = i.x, y = i.y,
            code = i.code?.ToArray(), kinds = i.kinds }).ToList();
        var fromFile = new DefenseSimulation(Setup());
        Assert(PlayLog.Replay(fromFile, read, read.Count, play.Ticks) == null && PlayLog.Digest(fromFile) == PlayLog.Digest(play), "a log without specs replays with the definitions' own");

        // A wave played over after a defeat starts from its checkpoint, which the file keeps (PlayRun.restore).
        DefenseSimulation Over(WaveCheckpoint checkpoint) { var setup = Setup(); setup.InitialWave = checkpoint.Wave; var sim = new DefenseSimulation(setup); sim.Restore(checkpoint); return sim; }
        var direct = Over(play.Checkpoint); var saved = Over(PlayLog.Load(PlayLog.Save(play.Checkpoint), Setup()));
        Assert(PlayLog.Digest(direct) == PlayLog.Digest(saved) && saved.Inputs.Count == 0, "a saved wave start restores as the checkpoint does, with nothing in the log:\n" + PlayLog.Digest(direct) + "\n" + PlayLog.Digest(saved));

        // A file recorded again in part keeps the steps the new recording did not reach, each replaying its own run.
        PlayStep Step(string id, int run, int input) => new PlayStep { id = id, replay = new PlayReplay { run = run, input = input } };
        PlayRun Run(int inputs) => new PlayRun { inputs = new PlayInput[inputs] };
        var old = new PlayCacheFile { fingerprint = "a", steps = new[] { Step("1-1.A01", 0, 1), Step("1-1.E07", 1, 5), Step("1-3.B01", 1, 9) }, runs = new[] { Run(1), Run(9) } };
        var again = new PlayCacheFile { fingerprint = "a", steps = new[] { Step("1-3.B01", 0, 3) }, runs = new[] { Run(3) } };
        var merged = PlayLog.Merge(old, again);
        Assert(string.Join(" ", merged.steps.Select(s => s.id)) == "1-1.A01 1-1.E07 1-3.B01" && merged.runs.Length == 3 &&
            merged.steps.All(s => merged.runs[s.replay.run].inputs.Length >= s.replay.input) && merged.runs[merged.steps[2].replay.run].inputs.Length == 3,
            "a merge keeps the old steps with their runs and takes the new one: " + string.Join(", ", merged.steps.Select(s => s.id + "@" + s.replay.run)));
        // Other data does not drop the old steps: a jump checks its own replay.
        Assert(PlayLog.Merge(new PlayCacheFile { fingerprint = "b", steps = old.steps, runs = old.runs }, again).steps.Length == 3, "a file from other data is merged too");
        Console.WriteLine("Play log checks passed");
    }
}
