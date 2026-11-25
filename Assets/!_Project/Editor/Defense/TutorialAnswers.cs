using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodingGame.BlockCoding;

namespace CodingGame.Defense.Editor
{
    // Reads the lessons' answers (TutorialStep.expectedCode) into blocks: "name = get_nearest_enemy()",
    // "x = enemy.distance(get_my_pos())", "name = my_attack_range()", "slash()", "shoot(enemy)", "block(enemy)", "if a <= b:" (any comparison),
    // "if x.is_dead():", "a * b" (one of + - * /), "x.type", "Enemy.Tank", "break", "continue", "for i in range(n):" and
    // "while True:", "for e in get_enemies():", "elif ...:", "else:" with a body indented deeper (any width), "x.shield", "break_shield(x)",
    // "x.front()", "x.top()", "x.size", "while a or b:" (any condition), "if x.left:", "x.left", "x.right".
    // ponytail: only what the lesson answers use; a new answer shape needs a case here.
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
                if ((line.StartsWith("if ") || line.StartsWith("elif ")) && line.EndsWith(":"))
                {
                    bool elif = line.StartsWith("elif ");
                    var block = new CodeBlock(elif ? BlockKind.Elif : BlockKind.If, "0", Condition(line.Substring(elif ? 5 : 3, line.Length - (elif ? 6 : 4))));
                    block.Body.AddRange(Suite(lines, ref index, depth + 1));
                    result.Add(block);
                }
                else if (line.StartsWith("for i in range(") && line.EndsWith("):"))
                {
                    var block = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, line.Substring(15, line.Length - 17)));
                    block.Body.AddRange(Suite(lines, ref index, depth + 1));
                    result.Add(block);
                }
                else if (line.StartsWith("for ") && line.EndsWith(" in get_enemies():"))
                {
                    var block = new CodeBlock(BlockKind.ForEach, line.Substring(4, line.Length - 22).Trim(), new CodeBlock(BlockKind.GetEnemies));
                    block.Body.AddRange(Suite(lines, ref index, depth + 1));
                    result.Add(block);
                }
                else if (line == "else:")
                {
                    var block = new CodeBlock(BlockKind.Else);
                    block.Body.AddRange(Suite(lines, ref index, depth + 1));
                    result.Add(block);
                }
                else if (line == "while True:")
                {
                    var block = new CodeBlock(BlockKind.While);
                    block.Body.AddRange(Suite(lines, ref index, depth + 1));
                    result.Add(block);
                }
                else if (line.StartsWith("while ") && line.EndsWith(":"))
                {
                    var block = new CodeBlock(BlockKind.WhileCondition, "0", Condition(line.Substring(6, line.Length - 7)));
                    block.Body.AddRange(Suite(lines, ref index, depth + 1));
                    result.Add(block);
                }
                else if (line == "slash()") result.Add(new CodeBlock(BlockKind.Slash));
                else if (line == "break") result.Add(new CodeBlock(BlockKind.Break));
                else if (line == "continue") result.Add(new CodeBlock(BlockKind.Continue));
                else if (line.StartsWith("shoot(") && line.EndsWith(")")) result.Add(new CodeBlock(BlockKind.Shot, "0", Value(line.Substring(6, line.Length - 7))));
                else if (line.StartsWith("break_shield(") && line.EndsWith(")")) result.Add(new CodeBlock(BlockKind.BreakShield, "0", Value(line.Substring(13, line.Length - 14))));
                else if (line.StartsWith("block(") && line.EndsWith(")")) result.Add(new CodeBlock(BlockKind.Block, "0", Value(line.Substring(6, line.Length - 7))));
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
            var either = text.Split(new[] { " or " }, 2, StringSplitOptions.None);
            if (either.Length == 2) return new CodeBlock(BlockKind.Or, "0", Condition(either[0]), Condition(either[1]));
            if (text.EndsWith(".left") || text.EndsWith(".right")) return Value(text); // an Enemy or None
            if (text.EndsWith(".is_dead()")) return new CodeBlock(BlockKind.IsDead, "0", new CodeBlock(BlockKind.Variable, text.Substring(0, text.Length - 10)));
            foreach (var sign in new[] { "<=", ">=", "==", "!=", "<", ">" })
            {
                var parts = text.Split(new[] { " " + sign + " " }, StringSplitOptions.None);
                if (parts.Length == 2) return new CodeBlock(BlockKind.Comparison, sign, Value(parts[0]), Value(parts[1]));
            }
            throw new FormatException("튜토리얼 조건을 읽을 수 없습니다: " + text);
        }
        static CodeBlock Value(string text)
        {
            foreach (var sign in PythonTreeCompiler.Operators)
            {
                int at = text.LastIndexOf(" " + sign + " ", StringComparison.Ordinal);
                if (at > 0) return new CodeBlock(BlockKind.Arithmetic, sign, Value(text.Substring(0, at)), Value(text.Substring(at + 3)));
            }
            if (text == "get_nearest_enemy()") return new CodeBlock(BlockKind.NearestEnemy);
            if (text == "my_attack_range()") return new CodeBlock(BlockKind.MyAttackRange);
            if (text == "get_my_pos()") return new CodeBlock(BlockKind.MyPos);
            if (text.StartsWith("Enemy.", StringComparison.Ordinal)) return new CodeBlock(BlockKind.EnemyTypeName, text.Substring(6));
            if (text.EndsWith(".shield", StringComparison.Ordinal)) return new CodeBlock(BlockKind.Shield, "0", new CodeBlock(BlockKind.Variable, text.Substring(0, text.Length - 7)));
            foreach (var (member, kind) in new[] { (".front()", BlockKind.Front), (".top()", BlockKind.Top), (".size", BlockKind.Size), (".left", BlockKind.Left), (".right", BlockKind.Right) })
                if (text.EndsWith(member, StringComparison.Ordinal)) return new CodeBlock(kind, "0", new CodeBlock(BlockKind.Variable, text.Substring(0, text.Length - member.Length)));
            if (text.EndsWith(".type", StringComparison.Ordinal)) return new CodeBlock(BlockKind.EnemyTypeOf, "0", new CodeBlock(BlockKind.Variable, text.Substring(0, text.Length - 5)));
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return new CodeBlock(BlockKind.Number, text);
            if (text.EndsWith(".distance(get_my_pos())"))
                return new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, text.Substring(0, text.IndexOf('.'))), new CodeBlock(BlockKind.MyPos));
            return new CodeBlock(BlockKind.Variable, text);
        }
    }
}
