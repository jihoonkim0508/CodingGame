using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodingGame.BlockCoding
{
    public enum BlockKind
    {
        Attack, Wait, While, If, Elif, Else, NearestEnemy, Distance,
        PositionX, PositionY, Number, Comparison, True, False, DeclareVariable, Variable,
        For, Break, Continue, Slash, Block, Boom, Shot, Slow, Buff
    }

    [Serializable]
    public sealed class CodeBlock
    {
        public BlockKind Kind;
        public string Value = "0";
        public List<CodeBlock> Arguments = new List<CodeBlock>();
        // Nested command blocks use Body; the existing IDE's CodeLine format stays independent.
        public List<CodeBlock> Body = new List<CodeBlock>();

        public CodeBlock(BlockKind kind, string value = "0", params CodeBlock[] arguments)
        {
            Kind = kind;
            Value = value;
            Arguments.AddRange(arguments);
        }
    }

    public sealed class CodeLine
    {
        public int Indent;
        public readonly List<CodeBlock> Blocks = new List<CodeBlock>();
    }

    /// <summary>Compiles the supported block language to Python 3 without executing it.</summary>
    public static class PythonBlockCompiler
    {
        public static readonly string[] Comparisons = { "==", "<", ">", "<=", ">=" };
        static readonly Regex Number = new Regex(@"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?\z");
        static readonly Regex Identifier = new Regex(@"\A[\p{L}_][\p{L}\p{Mn}\p{Mc}\p{Nd}\p{Pc}]*\z");
        static readonly HashSet<string> Reserved = new HashSet<string>(
            ("False None True and as assert async await break class continue def del elif else except finally for from " +
             "global if import in is lambda nonlocal not or pass raise return try while with yield " +
             "attack wait get_nearest_enemy get_pos_x get_pos_y RuntimeError").Split(' '));

        public static string Compile(string functionName, IReadOnlyList<CodeLine> lines)
        {
            functionName = NormalizeIdentifier(functionName);
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            for (int i = 0; i < lines.Count; i++)
                if (lines[i] == null || lines[i].Indent < 0) throw Error(i + 1, "들여쓰기는 0 이상이어야 합니다.");
            var source = new StringBuilder().Append("def ").Append(functionName).Append("():\n");
            int index = 0;
            SkipEmpty(lines, ref index);
            if (index == lines.Count) source.Append("    pass\n");
            else EmitSuite(lines, ref index, 0, source, new Dictionary<string, BlockKind>());
            return source.ToString();
        }

        static void SkipEmpty(IReadOnlyList<CodeLine> lines, ref int index)
        {
            while (index < lines.Count && lines[index].Blocks.Count == 0) index++;
        }

        static void EmitSuite(IReadOnlyList<CodeLine> lines, ref int index, int depth, StringBuilder source, Dictionary<string, BlockKind> variables)
        {
            bool canBranch = false;
            while (index < lines.Count)
            {
                SkipEmpty(lines, ref index);
                if (index == lines.Count || lines[index].Indent < depth) return;
                var line = lines[index];
                int lineNumber = index + 1;
                if (line.Indent != depth) throw Error(lineNumber, "들여쓰기는 조건 블록 아래에서 한 단계씩 늘려야 합니다.");
                var first = line.Blocks[0];
                bool control = first.Kind == BlockKind.If || first.Kind == BlockKind.Elif ||
                               first.Kind == BlockKind.Else || first.Kind == BlockKind.While;
                if ((first.Kind == BlockKind.Elif || first.Kind == BlockKind.Else) && !canBranch)
                    throw Error(lineNumber, "elif/else 앞에 같은 들여쓰기의 if/elif가 필요합니다.");
                try
                {
                    string statement;
                    if (control)
                    {
                        if (first.Kind == BlockKind.Else)
                        {
                            if (line.Blocks.Count != 1) throw new FormatException("else에는 조건을 넣을 수 없습니다.");
                            statement = "else:";
                        }
                        else statement = first.Kind.ToString().ToLowerInvariant() + " " + ValueSequence(line.Blocks, variables) + ":";
                    }
                    else if (first.Kind == BlockKind.DeclareVariable) statement = Assignment(line, variables);
                    else
                    {
                        if (line.Blocks.Count != 1 || (first.Kind != BlockKind.Attack && first.Kind != BlockKind.Wait))
                            throw new FormatException("한 줄에는 명령, 변수 선언 또는 조건문 하나를 완성해 주세요.");
                        statement = Expression(first, variables);
                    }
                    Append(source, depth + 1, statement);
                }
                catch (FormatException error) { throw Error(lineNumber, error.Message); }
                canBranch = first.Kind == BlockKind.If || first.Kind == BlockKind.Elif;
                index++;
                SkipEmpty(lines, ref index);
                if (!control) continue;
                if (index == lines.Count || lines[index].Indent <= depth)
                    throw Error(lineNumber, "조건문 아래에 들여쓴 실행 줄이 필요합니다.");
                EmitSuite(lines, ref index, depth + 1, source, variables);
                if (first.Kind == BlockKind.While)
                {
                    // Python's while-else runs when the condition becomes false, including the first check.
                    Append(source, depth + 1, "else:");
                    Append(source, depth + 2, "raise RuntimeError(\"while condition is false\")");
                }
            }
        }

        static string ValueSequence(List<CodeBlock> blocks, IReadOnlyDictionary<string, BlockKind> variables)
        {
            if (blocks.Count < 2) throw new FormatException("조건 또는 대입할 값이 비어 있습니다.");
            var result = new List<string>();
            for (int i = 1; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (i % 2 == 0)
                {
                    if (block.Kind != BlockKind.Comparison || !Comparisons.Contains(block.Value))
                        throw new FormatException("비교 연산자는 ==, <, >, <=, >= 중에서 선택해 주세요.");
                    result.Add(block.Value);
                }
                else
                {
                    if (!IsExpression(block.Kind)) throw new FormatException("조건에는 값 블록을 넣어 주세요.");
                    result.Add(Expression(block, variables));
                }
            }
            if (blocks.Count % 2 != 0) throw new FormatException("비교 연산자 오른쪽 값이 필요합니다.");
            return string.Join(" ", result);
        }

        public static bool IsExpression(BlockKind kind) => kind >= BlockKind.NearestEnemy && kind <= BlockKind.Variable && kind != BlockKind.Comparison && kind != BlockKind.DeclareVariable;

        public static string NormalizeIdentifier(string name)
        {
            name = (name ?? "").Normalize(NormalizationForm.FormKC);
            if (!Identifier.IsMatch(name) || Reserved.Contains(name))
                throw new FormatException("이름은 Python 식별자여야 하며 예약어/API 이름은 사용할 수 없습니다.");
            return name;
        }

        static string Assignment(CodeLine line, Dictionary<string, BlockKind> variables)
        {
            string name = NormalizeIdentifier(line.Blocks[0].Value);
            string value = ValueSequence(line.Blocks, variables);
            var type = line.Blocks.Count == 2 ? ValueKind(line.Blocks[1], variables) : BlockKind.True;
            variables[name] = type;
            return name + " = " + value;
        }

        // A completed declaration can populate the palette even while another line is being edited.
        public static Dictionary<string, BlockKind> DeclaredVariables(IReadOnlyList<CodeLine> lines)
        {
            var variables = new Dictionary<string, BlockKind>();
            foreach (var line in lines)
            {
                if (line.Blocks.Count == 0 || line.Blocks[0].Kind != BlockKind.DeclareVariable) continue;
                try { Assignment(line, variables); }
                catch (FormatException) { }
            }
            return variables;
        }

        static BlockKind ValueKind(CodeBlock block, IReadOnlyDictionary<string, BlockKind> variables)
        {
            if (block == null) throw new FormatException("인자 슬롯을 모두 채워 주세요.");
            if (block.Kind != BlockKind.Variable) return block.Kind;
            string name = NormalizeIdentifier(block.Value);
            if (variables == null || !variables.TryGetValue(name, out var type))
                throw new FormatException($"'{name}' 변수를 사용하기 전에 값을 선언해 주세요.");
            return type;
        }

        public static int ArgumentCount(BlockKind kind)
        {
            if (kind == BlockKind.Attack || kind == BlockKind.Wait) return 1;
            return kind == BlockKind.Distance ? 3 : 0;
        }

        public static string Expression(CodeBlock block, IReadOnlyDictionary<string, BlockKind> variables = null)
        {
            if (block.Arguments.Count != ArgumentCount(block.Kind))
                throw new FormatException($"{block.Kind}: 인자 슬롯을 모두 채워 주세요.");
            foreach (var argument in block.Arguments)
                if (argument == null || !IsExpression(argument.Kind)) throw new FormatException("인자에는 값 블록만 넣을 수 있습니다.");
            string Arg(int index) => Expression(block.Arguments[index], variables);
            switch (block.Kind)
            {
                case BlockKind.Attack:
                    RequireEnemy(block.Arguments[0], variables);
                    return "attack(" + Arg(0) + ")";
                case BlockKind.Wait:
                    RequireNumber(block.Arguments[0], variables);
                    return "wait(" + Arg(0) + ")";
                case BlockKind.Distance:
                    RequireEnemy(block.Arguments[0], variables);
                    RequireNumber(block.Arguments[1], variables);
                    RequireNumber(block.Arguments[2], variables);
                    return Arg(0) + ".get_distance(" + Arg(1) + ", " + Arg(2) + ")";
                case BlockKind.NearestEnemy: return "get_nearest_enemy()";
                case BlockKind.PositionX: return "get_pos_x()";
                case BlockKind.PositionY: return "get_pos_y()";
                case BlockKind.True: return "True";
                case BlockKind.False: return "False";
                case BlockKind.Number: return NormalizeNumber(block.Value);
                case BlockKind.Variable:
                    ValueKind(block, variables);
                    return NormalizeIdentifier(block.Value);
                default: throw new FormatException("이 블록은 값으로 사용할 수 없습니다.");
            }
        }

        static void RequireEnemy(CodeBlock block, IReadOnlyDictionary<string, BlockKind> variables)
        {
            if (ValueKind(block, variables) != BlockKind.NearestEnemy) throw new FormatException("enemy 인자에는 적 객체 또는 적 변수가 필요합니다.");
        }

        static void RequireNumber(CodeBlock block, IReadOnlyDictionary<string, BlockKind> variables)
        {
            var kind = ValueKind(block, variables);
            if (kind != BlockKind.Number && kind != BlockKind.Distance &&
                kind != BlockKind.PositionX && kind != BlockKind.PositionY)
                throw new FormatException("숫자 인자에는 숫자/거리/좌표 블록이 필요합니다.");
        }

        public static string NormalizeNumber(string text)
        {
            if (string.IsNullOrEmpty(text) || !Number.IsMatch(text)) throw new FormatException("올바른 숫자를 입력해 주세요.");
            // Keep arbitrary-length input as text; parsing as float would lose precision or impose a range.
            string sign = text[0] == '-' ? "-" : "";
            string unsigned = text.TrimStart('+', '-');
            int end = unsigned.IndexOfAny(new[] { '.', 'e', 'E' });
            if (end < 0) end = unsigned.Length;
            string integer = unsigned.Substring(0, end).TrimStart('0');
            return sign + (integer.Length == 0 ? "0" : integer) + unsigned.Substring(end);
        }

        static void Append(StringBuilder source, int indent, string text) => source.Append(' ', indent * 4).Append(text).Append('\n');
        static FormatException Error(int line, string message) => new FormatException($"{line}줄: {message}");
    }
}
