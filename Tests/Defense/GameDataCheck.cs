using System;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// GameData/*.md tables: × and x counts, spaces, '-', increments in brackets, unknown names, derived columns, and the x → × fix.
static class GameDataCheck
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        const string award = "# 보상\r\n\r\n| S-W | 로봇 | 블록 | 해금 |\r\n|:-:|:-:|:-:|:-:|\r\n" +
            "| start | 전사형 ×1 | slash() × 1 |  |\r\n" +
            "| 1-1 | 전사형 x 2 | while True ×2, 변수 = 값 X1, get_pos_x() ×1 | 배속 |\r\n" +
            "| 1-2 | - | - | - |\r\n" +
            "| 1-3 | | 순간이동() ×1 | |\r\n\r\n" +
            "| S-W | 시작 코인 | 처치당 | 처치 코인 (전멸 시) |\r\n|:-:|-:|-:|-:|\r\n| 1-1 | 100 | 5 | 20 |\r\n| 1-2 | - | 5 | 15 |\r\n";
        const string balance = "> Level 1\n> \n> | 종류 | 피해 | 공격 간격(초) | DPS | 사거리 |\n> |:-:|:-:|:-:|:-:|:-:|\n" +
            "> | 원거리형 | 16 (−16) | 2.4 (−3.6) | 6.67 (+1.34) | 7 |\n\n> Level 2\n> \n> | 종류 | 피해 | 공격 간격(초) | DPS | 사거리 |\n> |:-:|:-:|:-:|:-:|:-:|\n" +
            "> | 원거리형 | 19 | 2.4 | 8 | - |\n\n" +
            "| S-W | 종류 | 총 출현 수 | 출현(초) | 스폰 간격(초) | 체력 | 속도 | 마지막 출현(초) |\n|:-:|:-:|-:|-:|-:|-:|-:|-:|\n" +
            "| 1-1 | 일반 | 2 (+1) | 1 | 2 | 14 | 1.4 | 3 |\n| 1-1 | 탱커병 (보스) | 1 | 졸개 처치 후 | - | 50 | 1 | 졸개 처치 후 |\n" +
            "| 1-2 | 빠른 적 | 3 | 0 | 1.5 | 9 (−1) | 2 | 4 |\n";
        var data = GameDataTables.Parse(award, balance, 1.2f);
        var start = data.Awards.Single(a => a.Stage == 0);
        Require(start.Robots.SequenceEqual(new[] { (RobotRole.Warrior, 1) }) && start.Blocks.SequenceEqual(new[] { (BlockKind.Slash, 1) }), "start row with '× 1' spaced");
        var first = data.Awards.Single(a => a.Stage == 1 && a.Wave == 1);
        Require(first.Robots.SequenceEqual(new[] { (RobotRole.Warrior, 2) }), "x as the count sign reads as ×");
        Require(first.Blocks.SequenceEqual(new[] { (BlockKind.While, 2), (BlockKind.DeclareVariable, 1), (BlockKind.PositionX, 1) }), "while True, 변수 = 값 X1, get_pos_x() with its x kept");
        Require(data.Awards.Single(a => a.Wave == 2).Blocks.Count == 0 && data.Awards.Single(a => a.Wave == 2).Robots.Count == 0, "'-' is nothing");
        Require(data.Errors.Count == 1 && data.Errors[0].StartsWith("8줄") && data.Errors[0].Contains("순간이동()"), "unknown block named with its line: " + string.Join(" / ", data.Errors));
        Require(data.CoinRows.Single(c => c.Wave == 1).Start == 100 && data.CoinRows.Single(c => c.Wave == 2).Start == -1, "start coins, '-' carries over");
        var shooter = data.Robots.Single();
        Require(shooter.Role == RobotRole.Shooter && shooter.Damage == 16 && shooter.Interval == 2.4f && shooter.Range == 7, "Level 1 numbers without their increments");
        Require(data.Groups.Count == 3 && data.Groups[0].Count == 2 && data.Groups[1].Boss && data.Groups[1].Enemy == "Heavy" && data.Groups[1].Delay == 0 &&
            data.Groups[2].Enemy == "Runner" && data.Groups[2].Health == 9, "enemy groups, the boss after its minions");
        // Level 2's 19 against 16 × 1.2 = 19.2; 1-2's last at 4 against 0 + 1.5 × 2 = 3; 1-1 at 20 coins against 3 × 5.
        Require(data.Warnings.Count == 3 && data.Warnings.Any(w => w.Contains("Level 2") && w.Contains("19.2")) &&
            data.Warnings.Any(w => w.Contains("마지막 출현 4")) && data.Warnings.Any(w => w.StartsWith("12줄") && w.Contains("처치 코인 20")), "derived columns: " + string.Join(" / ", data.Warnings));
        string fixedAward = GameDataTables.FixTimes(award);
        Require(fixedAward == award.Replace("전사형 x 2", "전사형 × 2").Replace("값 X1", "값 ×1"), "x → × touches the count signs only: " + fixedAward);
        Require(GameDataTables.FixTimes("Level x 2 없음\n| a | max2 | box x |\n") == "Level x 2 없음\n| a | max2 | box x |\n", "x outside tables or inside names kept");
        // The real files parse without errors.
        string root = Path.Combine(AppContext.BaseDirectory, "../../../../../GameData");
        if (Directory.Exists(root))
        {
            var real = GameDataTables.Parse(File.ReadAllText(Path.Combine(root, "Award.md")), File.ReadAllText(Path.Combine(root, "Balance.md")), 1.2f);
            Require(real.Errors.Count == 0, "GameData errors: " + string.Join(" / ", real.Errors));
            Console.WriteLine("GameData: " + real.Awards.Count + " award rows, " + real.Groups.Count + " groups, warnings: " + (real.Warnings.Count == 0 ? "none" : string.Join(" / ", real.Warnings)));
        }
        Console.WriteLine("GameData checks passed");
    }
}
