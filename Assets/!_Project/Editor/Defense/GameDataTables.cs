// Reads GameData/Award.md and GameData/Balance.md (the user's tables) into plain rows: rewards and coins per stage-wave,
// the robots' level-1 numbers and the enemy groups. No Unity here, so Tests/Defense checks it with dotnet; GameDataApply
// writes the rows into the campaign. Errors stop the apply; warnings (derived columns that do not add up) only get logged.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CodingGame.BlockCoding;

namespace CodingGame.Defense.Editor
{
    public sealed class GameDataTables
    {
        public sealed class Award { public int Stage, Wave, Line; public List<(RobotRole role, int count)> Robots = new(); public List<(BlockKind kind, int count)> Blocks = new(); }
        public sealed class Coins { public int Stage, Wave, Line, Start = -1, PerKill, Total = -1; }
        public sealed class RobotStats { public RobotRole Role; public float Damage, Interval, Range; public int Line; }
        public sealed class Group { public int Stage, Wave, Line, Count; public string Enemy; public float Delay, Interval, Health, Speed; public bool Boss; }

        // Award row `start` is Stage 0 Wave 0: the stock 1-1 begins with.
        public readonly List<Award> Awards = new();
        public readonly List<Coins> CoinRows = new();
        public readonly List<RobotStats> Robots = new();
        public readonly List<Group> Groups = new();
        public readonly List<string> Errors = new(), Warnings = new();

        static readonly Dictionary<string, RobotRole> RoleNames = new() {
            ["전사형"] = RobotRole.Warrior, ["원거리형"] = RobotRole.Shooter, ["원거리"] = RobotRole.Shooter, ["탱커형"] = RobotRole.Tank,
            ["버퍼형"] = RobotRole.Buffer, ["폭탄형"] = RobotRole.Bomber, ["유틸형"] = RobotRole.Utility };
        static readonly Dictionary<string, string> EnemyNames = new() { ["일반"] = "Enemy", ["빠른 적"] = "Runner", ["탱커병"] = "Heavy", ["힐러"] = "Healer", ["보호막병"] = "Shielder",
            ["큐 지네"] = "QueueCentipede", ["스택 지네"] = "StackCentipede" };
        // The names the tables use; besides them a block's enum name (Slash) or its inventory label (shoot(Enemy)) also works.
        static readonly Dictionary<string, BlockKind> BlockNames = new(StringComparer.OrdinalIgnoreCase) {
            ["slash()"] = BlockKind.Slash, ["while"] = BlockKind.While, ["while True"] = BlockKind.While, ["shoot()"] = BlockKind.Shot,
            ["변수 = 값"] = BlockKind.DeclareVariable, ["variable"] = BlockKind.Variable, ["get_nearest_enemy()"] = BlockKind.NearestEnemy,
            ["if"] = BlockKind.If, ["elif"] = BlockKind.Elif, ["else"] = BlockKind.Else, ["비교"] = BlockKind.Comparison,
            ["get_my_pos()"] = BlockKind.MyPos, ["my_attack_range()"] = BlockKind.MyAttackRange, ["is_dead()"] = BlockKind.IsDead,
            ["break"] = BlockKind.Break, ["continue"] = BlockKind.Continue, ["Enemy.Tank"] = BlockKind.EnemyTypeName,
            ["block()"] = BlockKind.Block, ["boom()"] = BlockKind.Boom, ["slow()"] = BlockKind.Slow, ["buff()"] = BlockKind.Buff,
            ["wait()"] = BlockKind.Wait, ["숫자"] = BlockKind.Number, ["산술"] = BlockKind.Arithmetic, ["True"] = BlockKind.True,
            ["False"] = BlockKind.False, ["get_pos_x()"] = BlockKind.PositionX, ["get_pos_y()"] = BlockKind.PositionY,
            ["for e in get_enemies()"] = BlockKind.ForEach, ["for in"] = BlockKind.ForEach, ["get_enemies()"] = BlockKind.GetEnemies,
            ["shield"] = BlockKind.Shield, ["break_shield()"] = BlockKind.BreakShield, ["front()"] = BlockKind.Front, ["top()"] = BlockKind.Top };
        static BlockKind? Block(string name)
        {
            if (BlockNames.TryGetValue(name, out var kind)) return kind;
            foreach (BlockKind k in Enum.GetValues(typeof(BlockKind)))
                if (string.Equals(k.ToString(), name, StringComparison.OrdinalIgnoreCase) || DefenseProgression.Label(k) == name) return DefenseProgression.Canonical(k);
            return null;
        }

        // `x`/`X` written as the count sign (`slash() x1`, `전사형 x 1`, `while x2`) becomes `×`, in table rows only; an x inside
        // a name (get_pos_x()) is not after a space, `)` or Hangul, nor followed by just a number.
        static readonly Regex CountX = new(@"(?<=[\s)가-힣])[xX](?=[ \t]*\d+[ \t]*(?:,|\||$))", RegexOptions.Multiline);
        static readonly Regex TableLine = new(@"^[ \t]*(?:>[ \t]*)*\|[^\n]*", RegexOptions.Multiline);
        public static string FixTimes(string text) => TableLine.Replace(text, line => CountX.Replace(line.Value, "×"));

        public static GameDataTables Parse(string award, string balance, float upgradeMultiplier)
        {
            var data = new GameDataTables();
            award = FixTimes(award); // x read as ×, as the apply writes it back
            foreach (var t in Tables(award)) data.ReadAward(t);
            foreach (var t in Tables(balance)) data.ReadBalance(t);
            data.CheckLevels(balance, upgradeMultiplier);
            data.CheckCoins();
            return data;
        }

        sealed class Table { public string[] Header; public List<(int line, string[] cells)> Rows = new(); public int Level; }
        static List<Table> Tables(string text)
        {
            var tables = new List<Table>(); Table current = null; int level = 0;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = Regex.Replace(lines[i], @"^[ \t]*(?:>[ \t]?)*", "").Trim();
                var levelMark = Regex.Match(line, @"^Level\s*(\d+)$");
                if (levelMark.Success) level = int.Parse(levelMark.Groups[1].Value);
                if (!line.StartsWith("|")) { if (line.Length > 0 || !lines[i].TrimStart().StartsWith(">")) current = null; continue; }
                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                if (current == null) { tables.Add(current = new Table { Header = cells, Level = level }); continue; }
                if (cells.All(c => Regex.IsMatch(c, @"^:?-+:?$"))) continue;
                current.Rows.Add((i + 1, cells));
            }
            return tables;
        }
        static int Column(Table t, string name) => Array.FindIndex(t.Header, h => h == name);
        static string Cell(string[] cells, int column) => column >= 0 && column < cells.Length ? cells[column] : "";
        static bool Empty(string cell) => cell.Length == 0 || cell == "-";
        // A value with its increment in brackets: `16 (−16)` is 16.
        static string Value(string cell) => Regex.Replace(cell, @"\([^)]*\)", "").Trim();
        bool Number(string cell, int line, string what, out float value)
        {
            if (float.TryParse(Value(cell), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            Errors.Add($"{line}줄: {what} 숫자가 아님 '{cell}'"); return false;
        }
        bool StageWave(string cell, int line, out int stage, out int wave)
        {
            stage = wave = 0;
            if (cell == "start") return true;
            var m = Regex.Match(cell, @"^(\d+)-(\d+)$");
            if (m.Success) { stage = int.Parse(m.Groups[1].Value); wave = int.Parse(m.Groups[2].Value); return true; }
            Errors.Add($"{line}줄: S-W를 읽지 못함 '{cell}'"); return false;
        }
        // `slash() ×1, while ×2`: name and count per item (no count is one).
        IEnumerable<(string name, int count)> Items(string cell, int line)
        {
            if (Empty(cell)) yield break;
            foreach (var item in cell.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var m = Regex.Match(item, @"^(.+?)\s*×\s*(\d+)$");
                if (!m.Success) { yield return (item, 1); continue; }
                int count = int.Parse(m.Groups[2].Value);
                if (count < 1) { Errors.Add($"{line}줄: 개수는 1 이상 '{item}'"); continue; }
                yield return (m.Groups[1].Value.Trim(), count);
            }
        }

        void ReadAward(Table t)
        {
            int sw = Column(t, "S-W");
            if (sw < 0) return;
            int robots = Column(t, "로봇"), blocks = Column(t, "블록"), start = Column(t, "시작 코인"), perKill = Column(t, "처치당"), total = Column(t, "처치 코인 (전멸 시)");
            foreach (var (line, cells) in t.Rows)
            {
                if (!StageWave(Cell(cells, sw), line, out int stage, out int wave)) continue;
                if (blocks >= 0)
                {
                    var award = new Award { Stage = stage, Wave = wave, Line = line };
                    foreach (var (name, count) in Items(Cell(cells, robots), line))
                        if (RoleNames.TryGetValue(name, out var role)) award.Robots.Add((role, count));
                        else Errors.Add($"{line}줄: 모르는 로봇 '{name}' (전사형·원거리형·탱커형)");
                    foreach (var (name, count) in Items(Cell(cells, blocks), line))
                        if (Block(name) is BlockKind kind) award.Blocks.Add((kind, count));
                        else Errors.Add($"{line}줄: 모르는 블록 '{name}'");
                    if (Awards.Any(a => a.Stage == stage && a.Wave == wave)) Errors.Add($"{line}줄: {Cell(cells, sw)} 줄이 두 번");
                    else Awards.Add(award);
                }
                else if (perKill >= 0 && stage > 0)
                {
                    var row = new Coins { Stage = stage, Wave = wave, Line = line };
                    if (!Empty(Cell(cells, start)) && Number(Cell(cells, start), line, "시작 코인", out float s)) row.Start = (int)s;
                    if (Number(Cell(cells, perKill), line, "처치당", out float k)) row.PerKill = (int)k;
                    if (!Empty(Cell(cells, total)) && Number(Cell(cells, total), line, "처치 코인", out float all)) row.Total = (int)all;
                    CoinRows.Add(row);
                }
            }
        }

        void ReadBalance(Table t)
        {
            int kind = Column(t, "종류");
            if (kind < 0) return;
            if (Column(t, "피해") >= 0)
            {
                if (t.Level != 1) return; // Level 2 and 3 are checked against Level 1 (CheckLevels), not applied
                foreach (var (line, cells) in t.Rows)
                {
                    if (!RoleNames.TryGetValue(Cell(cells, kind), out var role)) { Errors.Add($"{line}줄: 모르는 로봇 '{Cell(cells, kind)}'"); continue; }
                    var r = new RobotStats { Role = role, Line = line };
                    if (Number(Cell(cells, Column(t, "피해")), line, "피해", out r.Damage) & Number(Cell(cells, Column(t, "공격 간격(초)")), line, "공격 간격", out r.Interval) &
                        Number(Cell(cells, Column(t, "사거리")), line, "사거리", out r.Range)) Robots.Add(r);
                    if (Number(Cell(cells, Column(t, "DPS")), line, "DPS", out float dps) && r.Interval > 0 && Math.Abs(dps - r.Damage / r.Interval) > .011f)
                        Warnings.Add($"{line}줄: {Cell(cells, kind)} DPS {dps}인데 피해÷간격은 {r.Damage / r.Interval:0.##}");
                }
                return;
            }
            int sw = Column(t, "S-W");
            if (sw < 0 || Column(t, "체력") < 0) return;
            foreach (var (line, cells) in t.Rows)
            {
                if (!StageWave(Cell(cells, sw), line, out int stage, out int wave)) continue;
                string name = Cell(cells, kind); bool boss = name.EndsWith("(보스)");
                if (boss) name = name.Substring(0, name.Length - "(보스)".Length).Trim();
                if (!EnemyNames.TryGetValue(name, out var enemy)) { Errors.Add($"{line}줄: 모르는 적 '{name}' (일반·빠른 적·탱커병·힐러·보호막병·큐 지네·스택 지네)"); continue; }
                var g = new Group { Stage = stage, Wave = wave, Line = line, Enemy = enemy, Boss = boss, Interval = 1 };
                string delay = Cell(cells, Column(t, "출현(초)")), interval = Cell(cells, Column(t, "스폰 간격(초)")), last = Cell(cells, Column(t, "마지막 출현(초)"));
                bool ok = Number(Cell(cells, Column(t, "총 출현 수")), line, "총 출현 수", out float count) &
                    Number(Cell(cells, Column(t, "체력")), line, "체력", out g.Health) & Number(Cell(cells, Column(t, "속도")), line, "속도", out g.Speed);
                // A boss comes once its minions are down: no time of its own.
                if (boss) { if (delay != "졸개 처치 후") Warnings.Add($"{line}줄: 보스 출현은 '졸개 처치 후'로 적힘 (읽은 값 '{delay}'은 쓰지 않음)"); }
                else ok &= Number(delay, line, "출현", out g.Delay) & Number(interval, line, "스폰 간격", out g.Interval);
                g.Count = (int)count;
                if (!ok) continue;
                if (g.Count < 1 || g.Interval <= 0 || g.Health <= 0 || g.Speed <= 0) { Errors.Add($"{line}줄: 수·간격·체력·속도는 0보다 커야 함"); continue; }
                if (!boss && float.TryParse(Value(last), NumberStyles.Float, CultureInfo.InvariantCulture, out float lastAt) &&
                    Math.Abs(lastAt - (g.Delay + g.Interval * (g.Count - 1))) > .011f)
                    Warnings.Add($"{line}줄: 마지막 출현 {lastAt}인데 출현 + 간격 × (수 − 1)은 {g.Delay + g.Interval * (g.Count - 1):0.##}");
                Groups.Add(g);
            }
        }

        // Level N's damage is Level 1's × upgradeMultiplier^(N−1); interval and range stay (`-`).
        void CheckLevels(string balance, float multiplier)
        {
            foreach (var t in Tables(balance).Where(t => t.Level > 1 && Column(t, "피해") >= 0))
                foreach (var (line, cells) in t.Rows)
                {
                    var one = Robots.FirstOrDefault(r => RoleNames.TryGetValue(Cell(cells, Column(t, "종류")), out var role) && r.Role == role);
                    if (one == null) continue;
                    float damage = one.Damage * (float)Math.Pow(multiplier, t.Level - 1);
                    string name = Cell(cells, Column(t, "종류"));
                    if (float.TryParse(Value(Cell(cells, Column(t, "피해"))), NumberStyles.Float, CultureInfo.InvariantCulture, out float d) && Math.Abs(d - damage) > .011f)
                        Warnings.Add($"{line}줄: Level {t.Level} {name} 피해 {d}인데 Level 1 × {multiplier}^{t.Level - 1}은 {damage:0.##}");
                    if (float.TryParse(Value(Cell(cells, Column(t, "공격 간격(초)"))), NumberStyles.Float, CultureInfo.InvariantCulture, out float i) && Math.Abs(i - one.Interval) > .011f)
                        Warnings.Add($"{line}줄: Level {t.Level} {name} 공격 간격 {i}인데 Level 1은 {one.Interval} (레벨업은 피해만 올림)");
                    if (float.TryParse(Value(Cell(cells, Column(t, "DPS"))), NumberStyles.Float, CultureInfo.InvariantCulture, out float dps) && Math.Abs(dps - damage / one.Interval) > .011f)
                        Warnings.Add($"{line}줄: Level {t.Level} {name} DPS {dps}인데 계산값은 {damage / one.Interval:0.##}");
                }
        }

        // 처치 코인 (전멸 시) is every enemy of the wave times 처치당.
        void CheckCoins()
        {
            foreach (var row in CoinRows.Where(r => r.Total >= 0))
            {
                int enemies = Groups.Where(g => g.Stage == row.Stage && g.Wave == row.Wave).Sum(g => g.Count);
                if (enemies * row.PerKill != row.Total)
                    Warnings.Add($"{row.Line}줄: {row.Stage}-{row.Wave} 처치 코인 {row.Total}인데 적 {enemies}마리 × {row.PerKill}은 {enemies * row.PerKill}");
            }
            foreach (var stage in Groups.Select(g => g.Stage).Distinct())
            {
                int last = Groups.Where(g => g.Stage == stage).Max(g => g.Wave);
                for (int w = 1; w <= last; w++)
                    if (!Groups.Any(g => g.Stage == stage && g.Wave == w)) Errors.Add($"Balance.md: {stage}-{w}의 몬스터 줄이 없음 ({stage}-{last}까지 있음)");
            }
        }
    }
}
