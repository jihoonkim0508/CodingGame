using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CodingGame.BlockCoding
{
    public enum BlockSlotKind { Statement, Value, Condition }

    /// <summary>Entry-style nested blocks. This does not execute Python or modify Unity objects.</summary>
    public static class PythonTreeCompiler
    {
        [Flags]
        enum ValueType { Integer = 1, Float = 2, Enemy = 4, Boolean = 8 }
        const ValueType Numeric = ValueType.Integer | ValueType.Float;
        static readonly HashSet<string> Keywords = new HashSet<string>(
            ("False None True and as assert async await break class continue def del elif else except finally for from " +
             "global if import in is lambda nonlocal not or pass raise return try while with yield").Split(' '));

        public static BlockSlotKind OutputSlot(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Comparison: case BlockKind.True: case BlockKind.False:
                    return BlockSlotKind.Condition;
                case BlockKind.Number: case BlockKind.Variable: case BlockKind.NearestEnemy:
                case BlockKind.PositionX: case BlockKind.PositionY: case BlockKind.Distance:
                    return BlockSlotKind.Value;
                case BlockKind.Attack: case BlockKind.Wait: case BlockKind.DeclareVariable:
                case BlockKind.For: case BlockKind.While: case BlockKind.If: case BlockKind.Elif:
                case BlockKind.Else: case BlockKind.Break: case BlockKind.Continue:
                case BlockKind.Slash: case BlockKind.Block: case BlockKind.Boom:
                case BlockKind.Shot: case BlockKind.Slow: case BlockKind.Buff:
                    return BlockSlotKind.Statement;
                default: throw new FormatException("지원하지 않는 블록입니다.");
            }
        }

        public static bool HasBody(BlockKind kind) => kind == BlockKind.For || kind == BlockKind.While ||
            kind == BlockKind.If || kind == BlockKind.Elif || kind == BlockKind.Else;

        public static bool HasEnemyArgument(BlockKind kind) => kind == BlockKind.Slash || kind == BlockKind.Block ||
            kind == BlockKind.Boom || kind == BlockKind.Shot || kind == BlockKind.Attack || kind == BlockKind.Slow;

        public static int ArgumentCount(BlockKind kind)
        {
            if (HasEnemyArgument(kind)) return 1;
            switch (kind)
            {
                case BlockKind.For: case BlockKind.If: case BlockKind.Elif:
                case BlockKind.Attack: case BlockKind.Wait: case BlockKind.DeclareVariable: return 1;
                case BlockKind.Comparison: return 2;
                case BlockKind.Distance: return 3; // receiver, x, y
                default: return 0;
            }
        }

        public static BlockSlotKind ArgumentSlot(BlockKind kind, int index)
        {
            if (index < 0 || index >= ArgumentCount(kind)) throw new ArgumentOutOfRangeException(nameof(index));
            return kind == BlockKind.If || kind == BlockKind.Elif
                ? BlockSlotKind.Condition : BlockSlotKind.Value;
        }

        public static bool Accepts(BlockSlotKind slot, BlockKind kind) => slot == OutputSlot(kind);

        /// <summary>Check the entire sibling chain before committing a drop, including displaced branches.</summary>
        public static bool CanInsert(IReadOnlyList<CodeBlock> siblings, int index, CodeBlock block)
        {
            if (siblings == null || block == null || index < 0 || index > siblings.Count ||
                OutputSlot(block.Kind) != BlockSlotKind.Statement) return false;
            bool canBranch = false;
            for (int i = 0; i <= siblings.Count; i++)
            {
                var current = i == index ? block : siblings[i < index ? i : i - 1];
                if (current == null || OutputSlot(current.Kind) != BlockSlotKind.Statement) return false;
                if (IsBranch(current.Kind) && !canBranch) return false;
                canBranch = current.Kind == BlockKind.If || current.Kind == BlockKind.Elif;
            }
            return true;
        }

        static bool IsBranch(BlockKind kind) => kind == BlockKind.Elif || kind == BlockKind.Else;

        public static string Compile(string functionName, IReadOnlyList<CodeBlock> blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            ValidateTree(blocks, new HashSet<CodeBlock>());
            var source = new StringBuilder("def ").Append(NormalizeIdentifier(functionName)).Append("(enemy):\n");
            EmitSuite(blocks, source, 1, 0, new Dictionary<string, ValueType> { ["enemy"] = ValueType.Enemy });
            return source.ToString();
        }

        static void ValidateTree(IReadOnlyList<CodeBlock> blocks, HashSet<CodeBlock> path)
        {
            foreach (var block in blocks)
            {
                if (block == null) throw new FormatException("비어 있는 블록 슬롯이 있습니다.");
                if (!path.Add(block)) throw new FormatException("블록을 자기 자신의 내부에 연결할 수 없습니다.");
                OutputSlot(block.Kind);
                if (block.Arguments == null || (block.Arguments.Count != ArgumentCount(block.Kind) &&
                    !(HasEnemyArgument(block.Kind) && block.Arguments.Count == 0)))
                    throw new FormatException($"{block.Kind}: 입력 슬롯을 모두 채워 주세요.");
                for (int i = 0; i < block.Arguments.Count; i++)
                    if (!(HasEnemyArgument(block.Kind) && block.Arguments[i] == null) &&
                        (block.Arguments[i] == null || !Accepts(ArgumentSlot(block.Kind, i), block.Arguments[i].Kind)))
                        throw new FormatException($"{block.Kind}: {i + 1}번째 슬롯에는 {ArgumentSlot(block.Kind, i)} 블록이 필요합니다.");
                if (block.Body == null || (!HasBody(block.Kind) && block.Body.Count != 0))
                    throw new FormatException($"{block.Kind}: 내부 명령을 가질 수 없는 블록입니다.");
                ValidateTree(block.Arguments.Where(b => b != null).ToList(), path);
                ValidateTree(block.Body, path);
                path.Remove(block);
            }
        }

        static void EmitSuite(IReadOnlyList<CodeBlock> blocks, StringBuilder source, int depth, int loopDepth,
            Dictionary<string, ValueType> variables)
        {
            if (blocks.Count == 0) Append(source, depth, "pass");
            for (int index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index];
                if (OutputSlot(block.Kind) != BlockSlotKind.Statement)
                    throw new FormatException("명령 영역에는 명령 블록만 연결할 수 있습니다.");
                switch (block.Kind)
                {
                    case BlockKind.If:
                        var outcomes = new List<Dictionary<string, ValueType>>();
                        bool hasElse = false;
                        do
                        {
                            var branch = blocks[index];
                            hasElse = branch.Kind == BlockKind.Else;
                            var state = new Dictionary<string, ValueType>(variables);
                            Append(source, depth, hasElse ? "else:" :
                                branch.Kind.ToString().ToLowerInvariant() + " " + Expression(branch.Arguments[0], variables) + ":");
                            EmitSuite(branch.Body, source, depth + 1, loopDepth, state);
                            outcomes.Add(state);
                            if (hasElse || index + 1 == blocks.Count || !IsBranch(blocks[index + 1].Kind)) break;
                            index++;
                        } while (true);
                        if (!hasElse) outcomes.Add(new Dictionary<string, ValueType>(variables));
                        MergeOutcomes(variables, outcomes);
                        break;
                    case BlockKind.Elif: case BlockKind.Else:
                        throw new FormatException("elif/else는 같은 명령 목록의 if/elif 바로 아래에만 연결할 수 있습니다.");
                    case BlockKind.For: case BlockKind.While:
                        bool isFor = block.Kind == BlockKind.For;
                        var inner = new Dictionary<string, ValueType>(variables);
                        if (isFor)
                        {
                            Require(block.Arguments[0], variables, ValueType.Integer, "range의 n은 정수여야 합니다.");
                            inner["i"] = ValueType.Integer;
                        }
                        Append(source, depth, isFor ? "for i in range(" + Expression(block.Arguments[0], variables) + "):" :
                            "while True:");
                        EmitSuite(block.Body, source, depth + 1, loopDepth + 1, inner);
                        // A loop can execute zero times; newly assigned names are not definite afterwards.
                        MergeOutcomes(variables, new[] { new Dictionary<string, ValueType>(variables), inner });
                        break;
                    case BlockKind.Break: case BlockKind.Continue:
                        if (loopDepth == 0) throw new FormatException("break/continue는 반복문 내부에만 배치할 수 있습니다.");
                        Append(source, depth, block.Kind.ToString().ToLowerInvariant());
                        break;
                    case BlockKind.DeclareVariable:
                        string name = NormalizeIdentifier(block.Value);
                        string value = Expression(block.Arguments[0], variables);
                        var type = TypeOf(block.Arguments[0], variables);
                        variables[name] = type;
                        Append(source, depth, name + " = " + value);
                        break;
                    default:
                        Append(source, depth, Expression(block, variables));
                        break;
                }
            }
        }

        static void MergeOutcomes(Dictionary<string, ValueType> target, IEnumerable<Dictionary<string, ValueType>> outcomes)
        {
            var states = outcomes.ToArray();
            target.Clear();
            foreach (string name in states[0].Keys)
            {
                if (!states.All(state => state.ContainsKey(name))) continue;
                ValueType type = 0;
                foreach (var state in states) type |= state[name];
                target[name] = type;
            }
        }

        static ValueType TypeOf(CodeBlock block, IReadOnlyDictionary<string, ValueType> variables)
        {
            switch (block.Kind)
            {
                case BlockKind.Number:
                    return NormalizeNumber(block.Value).Contains(".") ? ValueType.Float : ValueType.Integer;
                case BlockKind.PositionX: case BlockKind.PositionY: case BlockKind.Distance: return ValueType.Float;
                case BlockKind.NearestEnemy: return ValueType.Enemy;
                case BlockKind.True: case BlockKind.False: case BlockKind.Comparison: return ValueType.Boolean;
                case BlockKind.Variable:
                    string name = NormalizeIdentifier(block.Value);
                    if (!variables.TryGetValue(name, out var type))
                        throw new FormatException($"'{name}' 변수는 사용 전에 모든 실행 경로에서 선언해야 합니다.");
                    return type;
                default: throw new FormatException("반환값이 없는 블록입니다.");
            }
        }

        static void Require(CodeBlock block, IReadOnlyDictionary<string, ValueType> variables, ValueType allowed, string message)
        {
            if ((TypeOf(block, variables) & ~allowed) != 0) throw new FormatException(message);
        }

        static string Expression(CodeBlock block, IReadOnlyDictionary<string, ValueType> variables)
        {
            string Arg(int index) => Expression(block.Arguments[index], variables);
            if (HasEnemyArgument(block.Kind))
            {
                var target = block.Arguments.Count == 0 || block.Arguments[0] == null
                    ? new CodeBlock(BlockKind.Variable, "enemy") : block.Arguments[0];
                Require(target, variables, ValueType.Enemy, "공격 대상에는 enemy 객체가 필요합니다.");
                string method = block.Kind == BlockKind.Shot || block.Kind == BlockKind.Attack ? "attack" : block.Kind.ToString().ToLowerInvariant();
                return method + "(" + Expression(target, variables) + ")";
            }
            switch (block.Kind)
            {
                case BlockKind.Number: return NormalizeNumber(block.Value);
                case BlockKind.Variable:
                    TypeOf(block, variables);
                    return NormalizeIdentifier(block.Value);
                case BlockKind.PositionX: return "get_pos_x()";
                case BlockKind.PositionY: return "get_pos_y()";
                case BlockKind.NearestEnemy: return "get_nearest_enemy()";
                case BlockKind.True: return "True";
                case BlockKind.False: return "False";
                case BlockKind.Buff: return "buff()";
                case BlockKind.Wait:
                    Require(block.Arguments[0], variables, Numeric, "wait에는 초 단위의 숫자가 필요합니다.");
                    return "wait(" + Arg(0) + ")";
                case BlockKind.Distance:
                    Require(block.Arguments[0], variables, ValueType.Enemy, "get_distance의 대상은 Enemy 객체여야 합니다.");
                    Require(block.Arguments[1], variables, Numeric, "x좌표는 숫자여야 합니다.");
                    Require(block.Arguments[2], variables, Numeric, "y좌표는 숫자여야 합니다.");
                    return Arg(0) + ".get_distance(" + Arg(1) + ", " + Arg(2) + ")";
                case BlockKind.Comparison:
                    if (!PythonBlockCompiler.Comparisons.Contains(block.Value)) throw new FormatException("지원하지 않는 비교 연산자입니다.");
                    if (block.Value != "==")
                    {
                        Require(block.Arguments[0], variables, Numeric, "대소 비교에는 숫자가 필요합니다.");
                        Require(block.Arguments[1], variables, Numeric, "대소 비교에는 숫자가 필요합니다.");
                    }
                    return Arg(0) + " " + block.Value + " " + Arg(1);
                default: throw new FormatException("이 블록은 식으로 사용할 수 없습니다.");
            }
        }

        public static string NormalizeNumber(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOfAny(new[] { 'e', 'E', 'f', 'F' }) >= 0)
                throw new FormatException("수치를 입력해 주세요. 소수점(.)이 있으면 float, 없으면 int입니다.");
            return PythonBlockCompiler.NormalizeNumber(text);
        }

        public static string NormalizeIdentifier(string name)
        {
            try { name = (name ?? "").Normalize(NormalizationForm.FormKC); }
            catch (ArgumentException) { throw new FormatException("유효하지 않은 Python 변수명입니다."); }
            if (name.Length == 0 || Keywords.Contains(name)) throw new FormatException("Python 예약어는 이름으로 사용할 수 없습니다.");
            for (int i = 0; i < name.Length;)
            {
                int code = char.ConvertToUtf32(name, i);
                var category = CharUnicodeInfo.GetUnicodeCategory(name, i);
                bool start = code == '_' || category == UnicodeCategory.UppercaseLetter || category == UnicodeCategory.LowercaseLetter ||
                    category == UnicodeCategory.TitlecaseLetter || category == UnicodeCategory.ModifierLetter ||
                    category == UnicodeCategory.OtherLetter || category == UnicodeCategory.LetterNumber ||
                    code == 0x1885 || code == 0x1886 || code == 0x2118 || code == 0x212E;
                bool continuation = start || category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark ||
                    category == UnicodeCategory.DecimalDigitNumber || category == UnicodeCategory.ConnectorPunctuation ||
                    code == 0xB7 || code == 0x387 || (code >= 0x1369 && code <= 0x1371) || code == 0x19DA;
                if (!(i == 0 ? start : continuation)) throw new FormatException("Python 변수명은 문자 또는 _로 시작하고 공백과 기호를 포함할 수 없습니다.");
                i += code > 0xFFFF ? 2 : 1;
            }
            return name;
        }

        /// <summary>Names for a variable palette, also available while other blocks are incomplete.</summary>
        public static IReadOnlyCollection<string> DeclaredVariables(IReadOnlyList<CodeBlock> blocks)
        {
            var names = new HashSet<string>();
            CollectNames(blocks, names, new HashSet<CodeBlock>());
            return names;
        }

        static void CollectNames(IReadOnlyList<CodeBlock> blocks, HashSet<string> names, HashSet<CodeBlock> visited)
        {
            foreach (var block in blocks)
            {
                if (block == null || !visited.Add(block)) continue;
                if (block.Kind == BlockKind.DeclareVariable)
                {
                    try { names.Add(NormalizeIdentifier(block.Value)); }
                    catch (FormatException) { }
                }
                if (block.Kind == BlockKind.For) names.Add("i");
                if (block.Body != null) CollectNames(block.Body, names, visited);
            }
        }

        static void Append(StringBuilder source, int depth, string text) => source.Append(' ', depth * 4).Append(text).Append('\n');
    }
}
