using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// 이동: a placed robot moves to a spot that fits, as it is (id, level, code, health), under recall's gate; the play log replays it.
static class MoveCheck
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, damage = 4, range = 8, interval = .1f, projectileSpeed = 200, radius = .75f, health = 50 };
    static BattleSetup Setup(int stage) => new BattleSetup { Progression = FixtureProgression.Create(), TestOptions = new DefenseTestOptions(),
        PlayerFlow = true, Stage = stage, Min = new Vector2(-20, -10), Max = new Vector2(20, 10), BaseHealth = 7,
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1.1f) },
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Waves = Enumerable.Range(0, 3).Select(i => new[] { new SpawnGroup { Count = 3, Interval = .4f, Enemy = new EnemySpec { health = 1, speed = 1.5f } } }).ToArray() };

    public static void Run()
    {
        var play = new DefenseSimulation(Setup(2));
        var bot = play.Place(Spec(RobotRole.Shooter), (int)RobotRole.Shooter, new Vector2(0, 4), out var error);
        var other = play.Place(Spec(RobotRole.Shooter), (int)RobotRole.Shooter, new Vector2(6, 4), out error);
        var tank = play.Place(Spec(RobotRole.Tank), (int)RobotRole.Tank, new Vector2(-4, 0), out error);
        Assert(bot != null && other != null && tank != null, "setup: robots placed: " + error);
        play.ApplyProgram(bot.Id, "fire", new[] { new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.NearestEnemy)) });
        play.SetCoins(500); Assert(play.UpgradeRobot(bot.Id), "setup: level-up");
        int stock = play.RobotAvailable(RobotRole.Shooter), coins = play.Coins, count = play.Robots.Count; var program = bot.Program; float? health = bot.Health;

        Assert(play.MoveRobot(bot.Id, new Vector2(.5f, 4.2f), out error), "a robot may move to a spot overlapping only its own: " + error);
        Assert(play.MoveRobot(bot.Id, new Vector2(-8, 6), out error), "a robot moves to a free spot: " + error);
        var moved = play.FindRobot(bot.Id);
        Assert(moved == bot && moved.Position == new Vector2(-8, 6) && moved.Level == 2 && moved.Program == program && moved.Health == health,
            "the moved robot keeps its id, level, code and health");
        Assert(play.RobotAvailable(RobotRole.Shooter) == stock && play.Coins == coins && play.Robots.Count == count, "a move costs nothing and changes no stock");
        Assert(!play.MoveRobot(bot.Id, new Vector2(6.5f, 4), out error) && error == "다른 로봇과 겹칩니다." && bot.Position == new Vector2(-8, 6), "onto another robot: refused, it stays");
        Assert(!play.MoveRobot(bot.Id, new Vector2(2, .2f), out error) && error == "적 경로에는 탱커만 설치할 수 있습니다.", "a shooter onto the road: refused");
        Assert(!play.MoveRobot(tank.Id, new Vector2(2, 5), out error) && error == "탱커는 몸체가 경로 안에 들어가도록 설치하세요.", "a tank off the road: refused");
        Assert(play.MoveRobot(tank.Id, new Vector2(3, 0), out error), "a tank along the road: " + error);
        Assert(!play.MoveRobot(bot.Id, new Vector2(-30, 6), out error) && error == "설치 허용 영역 밖입니다.", "off the map: refused");
        Assert(!play.MoveRobot(999, new Vector2(-8, 6), out _), "no such robot");

        play.Start();
        Assert(!play.MoveRobot(bot.Id, new Vector2(-6, 6), out error) && error == "로봇 이동은 웨이브 준비 중에만 가능합니다.", "no move during a wave");
        for (int i = 0; i < 300; i++) play.Advance(1.0 / 30);

        var replay = new DefenseSimulation(Setup(2));
        Assert(PlayLog.Replay(replay, play.Inputs, play.Inputs.Count, play.Ticks) == null && PlayLog.Digest(replay) == PlayLog.Digest(play),
            "a log with moves replays to the same session:\n" + PlayLog.Digest(replay) + "\n" + PlayLog.Digest(play));
        Assert(replay.FindRobot(bot.Id).Position == new Vector2(-8, 6) && replay.FindRobot(tank.Id).Position == new Vector2(3, 0), "the replay's robots stand where they were moved");

        // Recall's gate: Stage 1's first waves keep both locked.
        var first = new DefenseSimulation(Setup(1));
        var lesson = first.Place(Spec(RobotRole.Shooter), (int)RobotRole.Shooter, new Vector2(0, 4), out _);
        Assert(!first.RecallUnlocked && !first.MoveRobot(lesson.Id, new Vector2(-8, 6), out error) && error == DefenseSimulation.MoveLocked, "locked while recall is");
        Console.WriteLine("MoveCheck: passed");
    }
}
