using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // Reads the Stage 1 tutorial answers (TutorialStep.expectedCode) into blocks: "name = get_nearest_enemy()",
    // "x = enemy.distance(get_my_pos())", "name = my_attack_range()", "slash(name)", "if a <= b:" with an indented body.
    // ponytail: only what the Stage 1 answers use; a new answer shape needs a case here.
    public static class TutorialAnswers
    {
        public static CodeBlock[] Parse(string code)
        {
            var lines = (code ?? "").Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
            int index = 0;
            return Suite(lines, ref index, 0);
        }
        static CodeBlock[] Suite(List<string> lines, ref int index, int indent)
        {
            var result = new List<CodeBlock>();
            while (index < lines.Count)
            {
                string raw = lines[index]; int depth = raw.Length - raw.TrimStart().Length;
                if (depth < indent) break;
                string line = raw.Trim(); index++;
                if (line.StartsWith("if ") && line.EndsWith(":"))
                {
                    var block = new CodeBlock(BlockKind.If, "0", Condition(line.Substring(3, line.Length - 4)));
                    block.Body.AddRange(Suite(lines, ref index, depth + 4));
                    result.Add(block);
                }
                else if (line.StartsWith("slash(")) result.Add(new CodeBlock(BlockKind.Slash, "0", Value(line.Substring(6, line.Length - 7))));
                else
                {
                    var parts = line.Split(new[] { " = " }, 2, StringSplitOptions.None);
                    if (parts.Length != 2) throw new FormatException("튜토리얼 정답을 읽을 수 없습니다: " + line);
                    result.Add(new CodeBlock(BlockKind.DeclareVariable, parts[0], Value(parts[1])));
                }
            }
            return result.ToArray();
        }
        static CodeBlock Condition(string text)
        {
            var parts = text.Split(new[] { " <= " }, StringSplitOptions.None);
            if (parts.Length != 2) throw new FormatException("튜토리얼 조건을 읽을 수 없습니다: " + text);
            return new CodeBlock(BlockKind.Comparison, "<=", Value(parts[0]), Value(parts[1]));
        }
        static CodeBlock Value(string text)
        {
            if (text == "get_nearest_enemy()") return new CodeBlock(BlockKind.NearestEnemy);
            if (text == "my_attack_range()") return new CodeBlock(BlockKind.MyAttackRange);
            if (text == "get_my_pos()") return new CodeBlock(BlockKind.MyPos);
            if (text.EndsWith(".distance(get_my_pos())"))
                return new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, text.Substring(0, text.IndexOf('.'))), new CodeBlock(BlockKind.MyPos));
            return new CodeBlock(BlockKind.Variable, text);
        }
    }
}
