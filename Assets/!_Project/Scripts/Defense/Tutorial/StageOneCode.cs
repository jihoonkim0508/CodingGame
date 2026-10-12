using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // The teaching notation becomes the same validated block tree used by the editor.
    public static class StageOneCode
    {
        // `Attack()` becomes the teaching robot's own attack block (slash for the warrior, shot for the shooter).
        public static List<CodeBlock> Parse(string source, bool automaticAttack = false, BlockKind attackKind = BlockKind.Shot)
        {
            var result = new List<CodeBlock>();
            CodeBlock condition = null;
            foreach (string raw in (source ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line == "Attack()" || line == "attack(enemy)")
                {
                    var attack = new CodeBlock(attackKind);
                    if (condition != null)
                    {
                        if (!char.IsWhiteSpace(raw[0])) throw new FormatException("조건의 공격 행동을 들여써 주세요.");
                        condition.Body.Add(attack);
                    }
                    else result.Add(attack);
                    continue;
                }
                var assignment = Regex.Match(line, @"^(damage|range|attackDistance)\s*=\s*([+]?(?:\d+(?:\.\d*)?|\.\d+))$");
                if (assignment.Success && !char.IsWhiteSpace(raw[0]) && condition == null)
                {
                    double value = double.Parse(assignment.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (value <= 0 || value > 100) throw new FormatException("값은 0 초과 100 이하로 입력하세요.");
                    result.Add(new CodeBlock(BlockKind.DeclareVariable, assignment.Groups[1].Value,
                        new CodeBlock(BlockKind.Number, value.ToString(CultureInfo.InvariantCulture))));
                    continue;
                }
                var test = Regex.Match(line, @"^if\s+enemyDistance\s*<=\s*(attackDistance|[+]?(?:\d+(?:\.\d*)?|\.\d+))\s*:$");
                if (test.Success && !char.IsWhiteSpace(raw[0]) && condition == null)
                {
                    string value = test.Groups[1].Value;
                    if (value != "attackDistance" && (double.Parse(value, CultureInfo.InvariantCulture) <= 0 || double.Parse(value, CultureInfo.InvariantCulture) > 100))
                        throw new FormatException("공격 기준은 0 초과 100 이하입니다.");
                    condition = new CodeBlock(BlockKind.If, "", new CodeBlock(BlockKind.Comparison, "<=",
                        new CodeBlock(BlockKind.Variable, "enemyDistance"), new CodeBlock(value == "attackDistance" ? BlockKind.Variable : BlockKind.Number, value)));
                    result.Add(condition);
                    continue;
                }
                throw new FormatException("변수 선언, 거리 조건, Attack() 코드를 확인하세요.");
            }
            if (result.Count == 0 || condition != null && condition.Body.Count != 1) throw new FormatException("코드를 완성하세요. 조건 안에는 Attack()이 필요합니다.");
            if (automaticAttack && condition == null && !result.Any(b => b.Kind == attackKind)) result.Add(new CodeBlock(attackKind));
            PythonTreeCompiler.Compile("tutorial", result);
            return result;
        }

        public static void Validate(string source, int wave, int section, BlockKind attackKind = BlockKind.Shot)
        {
            var blocks = Parse(source, false, attackKind);
            double? Setting(string name) => blocks.LastOrDefault(b => b.Kind == BlockKind.DeclareVariable && b.Value == name) is CodeBlock b
                ? double.Parse(b.Arguments[0].Value, CultureInfo.InvariantCulture) : (double?)null;
            var condition = blocks.SingleOrDefault(b => b.Kind == BlockKind.If);
            bool matches = wave == 1 ? Setting("damage") == 20 : wave == 2 ? section == 2 ? Setting("range") > 5 : Setting("damage") > 0 && Setting("range") > 0 :
                wave == 3 ? condition != null && condition.Arguments[0].Arguments[1].Kind == BlockKind.Number &&
                    double.Parse(condition.Arguments[0].Arguments[1].Value, CultureInfo.InvariantCulture) == 10 :
                wave == 4 ? condition != null && condition.Arguments[0].Arguments[1].Kind == BlockKind.Number &&
                    (section != 3 || double.Parse(condition.Arguments[0].Arguments[1].Value, CultureInfo.InvariantCulture) == 8) :
                wave == 5 && Setting("attackDistance") > 0 && (section != 4 || Setting("attackDistance") == 15) &&
                    condition != null && condition.Arguments[0].Arguments[1].Kind == BlockKind.Variable && condition.Arguments[0].Arguments[1].Value == "attackDistance";
            if (!matches || wave >= 3 && blocks.Any(b => b.Kind == attackKind))
                throw new FormatException("이번 학습 목표에 맞게 변수와 조건을 수정하세요.");
        }
    }
}
