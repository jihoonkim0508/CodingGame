using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodingGame.BlockCoding
{
    public enum BlockSlotKind { Statement, Value, Condition }

    /// <summary>A compile error and the line (statement block) it belongs to, for the red border and the Python tab's underline.</summary>
    public sealed class CodeError : FormatException
    {
        public readonly CodeBlock Statement;
        public CodeError(string message, CodeBlock statement) : base(message) { Statement = statement; }
    }

    /// <summary>Entry-style nested blocks. This does not execute Python or modify Unity objects.</summary>
    public static class PythonTreeCompiler
    {
        [Flags]
        enum ValueType { Integer = 1, Float = 2, Enemy = 4, Boolean = 8, Vector2 = 16, EnemyType = 32 }
        const ValueType Numeric = ValueType.Integer | ValueType.Float;
        static readonly HashSet<string> Keywords = new HashSet<string>(
            ("False None True and as assert async await break class continue def del elif else except finally for from " +
             "global if import in is lambda nonlocal not or pass raise return try while with yield").Split(' '));

        public static BlockSlotKind OutputSlot(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Comparison: case BlockKind.True: case BlockKind.False: case BlockKind.IsDead:
                    return BlockSlotKind.Condition;
                case BlockKind.Number: case BlockKind.Variable: case BlockKind.NearestEnemy:
                case BlockKind.PositionX: case BlockKind.PositionY: case BlockKind.Distance: case BlockKind.Hp:
                case BlockKind.MyAttackRange: case BlockKind.MyPos: case BlockKind.Arithmetic:
                case BlockKind.EnemyTypeOf: case BlockKind.EnemyTypeName:
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

        // slash() takes no target: it cuts every enemy around the melee robot.
        public static bool HasEnemyArgument(BlockKind kind) => kind == BlockKind.Block ||
            kind == BlockKind.Boom || kind == BlockKind.Shot || kind == BlockKind.Attack || kind == BlockKind.Slow;

        public static int ArgumentCount(BlockKind kind)
        {
            if (HasEnemyArgument(kind)) return 1;
            switch (kind)
            {
                case BlockKind.For: case BlockKind.If: case BlockKind.Elif:
                case BlockKind.Wait: case BlockKind.DeclareVariable: case BlockKind.Hp: case BlockKind.IsDead:
                case BlockKind.EnemyTypeOf: return 1; // Hp, IsDead, EnemyTypeOf: receiver
                case BlockKind.Comparison: case BlockKind.Arithmetic: return 2;
                case BlockKind.Distance: return 2; // receiver, position (Vector2)
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

        internal static bool IsBranch(BlockKind kind) => kind == BlockKind.Elif || kind == BlockKind.Else;

        public static string Compile(string functionName, IReadOnlyList<CodeBlock> blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            ValidateTree(blocks, new HashSet<CodeBlock>());
            var source = new StringBuilder("def ").Append(NormalizeIdentifier(functionName)).Append("():\n");
            // No implicit names: every variable, including the enemy target, has to be declared first.
            EmitSuite(blocks, source, 1, 0, new Dictionary<string, ValueType>());
            return source.ToString();
        }
        static IEnumerable<CodeBlock> All(IEnumerable<CodeBlock> blocks) =>
            blocks.Where(b => b != null).SelectMany(b => new[] { b }.Concat(All(b.Arguments)).Concat(All(b.Body)));
        // A loop that acts on an enemy variable without re-assigning it keeps aiming at the first target after it is gone.
        public static bool HasStaleEnemyLoop(IReadOnlyList<CodeBlock> blocks) => StaleEnemyLoop(blocks) != null;
        // The first such loop (the line the warning goes on); null when there is none.
        public static CodeBlock StaleEnemyLoop(IReadOnlyList<CodeBlock> blocks)
        {
            var enemies = new HashSet<string>(All(blocks).Where(b => b.Kind == BlockKind.DeclareVariable && b.Arguments.FirstOrDefault()?.Kind == BlockKind.NearestEnemy).Select(b => b.Value));
            return StaleLoop(blocks, enemies, new HashSet<string>());
        }
        // repicked: names an enclosing loop assigns again; a loop that can break out to it is not stale (2-6's answer).
        static CodeBlock StaleLoop(IEnumerable<CodeBlock> blocks, HashSet<string> enemies, HashSet<string> repicked)
        {
            foreach (var loop in blocks.Where(b => b != null))
            {
                var inner = repicked;
                if (loop.Kind == BlockKind.For || loop.Kind == BlockKind.While)
                {
                    var declared = All(loop.Body).Where(b => b.Kind == BlockKind.DeclareVariable).Select(b => b.Value).ToList();
                    var stale = All(loop.Body).Where(b => b.Kind == BlockKind.Variable && enemies.Contains(b.Value)).Select(b => b.Value)
                        .Except(declared)
                        // `if enemy.is_dead(): break` (or re-assign) watches the target: that loop is not stale.
                        .Except(All(loop.Body).Where(b => b.Kind == BlockKind.IsDead && b.Arguments.FirstOrDefault()?.Kind == BlockKind.Variable).Select(b => b.Arguments[0].Value));
                    if (Breaks(loop.Body)) stale = stale.Except(repicked);
                    if (stale.Any()) return loop;
                    inner = new HashSet<string>(repicked.Concat(declared));
                }
                if (StaleLoop(loop.Body, enemies, inner) is CodeBlock found) return found;
            }
            return null;
        }
        // A break of this loop: in its body or an if inside it, not in a nested loop.
        static bool Breaks(IEnumerable<CodeBlock> body) => body.Where(b => b != null).Any(b => b.Kind == BlockKind.Break ||
            b.Kind != BlockKind.For && b.Kind != BlockKind.While && Breaks(b.Body));

        // statement: the line the blocks belong to (null for a list of lines).
        static void ValidateTree(IReadOnlyList<CodeBlock> blocks, HashSet<CodeBlock> path, CodeBlock statement = null)
        {
            foreach (var block in blocks)
            {
                try { ValidateBlock(block, path, statement); }
                catch (FormatException error) when (!(error is CodeError)) { throw new CodeError(error.Message, statement ?? block); }
            }
        }
        static void ValidateBlock(CodeBlock block, HashSet<CodeBlock> path, CodeBlock statement)
        {
            {
                if (block == null) throw new FormatException("비어 있는 블록 슬롯이 있습니다.");
                if (!path.Add(block)) throw new FormatException("블록을 자기 자신의 내부에 연결할 수 없습니다.");
                OutputSlot(block.Kind);
                if (HasEnemyArgument(block.Kind) && (block.Arguments == null || block.Arguments.Count == 0 || block.Arguments[0] == null))
                    throw new FormatException("공격 대상을 넣어 주세요. 예: enemy = get_nearest_enemy() 다음에 " + (block.Kind == BlockKind.Shot || block.Kind == BlockKind.Attack ? "shoot" : block.Kind.ToString().ToLowerInvariant()) + "(enemy)");
                if (block.Arguments == null || block.Arguments.Count != ArgumentCount(block.Kind))
                    throw new FormatException($"{Label(block.Kind)}: 빈칸을 모두 채워 주세요.");
                for (int i = 0; i < block.Arguments.Count; i++)
                    if (block.Arguments[i] == null || !Accepts(ArgumentSlot(block.Kind, i), block.Arguments[i].Kind))
                        throw new FormatException($"{Label(block.Kind)}: {i + 1}번째 빈칸에 {(ArgumentSlot(block.Kind, i) == BlockSlotKind.Condition ? "논리" : "값")} 탭의 블록을 넣어 주세요.");
                if (block.Body == null || (!HasBody(block.Kind) && block.Body.Count != 0))
                    throw new FormatException($"{Label(block.Kind)}: 안쪽에 명령을 넣을 수 없는 블록입니다.");
                ValidateTree(block.Arguments.Where(b => b != null).ToList(), path, statement ?? block);
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
                var current = blocks[index];
                try { index = EmitStatement(blocks, index, source, depth, loopDepth, variables, ref current); }
                catch (FormatException error) when (!(error is CodeError)) { throw new CodeError(error.Message, current); }
            }
        }

        // Emits blocks[index] (an if with its elif/else) and returns the last index used; current: the branch being emitted.
        static int EmitStatement(IReadOnlyList<CodeBlock> blocks, int index, StringBuilder source, int depth, int loopDepth,
            Dictionary<string, ValueType> variables, ref CodeBlock current)
        {
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
                            var branch = current = blocks[index];
                            hasElse = branch.Kind == BlockKind.Else;
                            var state = new Dictionary<string, ValueType>(variables);
                            Append(source, depth, hasElse ? "else:" :
                                branch.Kind.ToString().ToLowerInvariant() + " " + Expression(branch.Arguments[0], variables) + ":");
                            EmitSuite(branch.Body, source, depth + 1, loopDepth, state);
                            outcomes.Add(state);
                            if (hasElse || index + 1 == blocks.Count || !IsBranch(blocks[index + 1].Kind)) break;
                            index++;
                        } while (true);
                        if (!hasElse)
                        {
                            var skipped = new Dictionary<string, ValueType>(variables);
                            if (block.Arguments[0] is CodeBlock test && test.Kind == BlockKind.IsDead && test.Arguments[0]?.Kind == BlockKind.Variable)
                                skipped[NormalizeIdentifier(test.Arguments[0].Value)] = ValueType.Enemy;
                            outcomes.Add(skipped);
                        }
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
            return index;
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
                case BlockKind.PositionX: case BlockKind.PositionY: case BlockKind.Distance: case BlockKind.Hp:
                case BlockKind.MyAttackRange: return ValueType.Float;
                case BlockKind.MyPos: return ValueType.Vector2;
                case BlockKind.NearestEnemy: return ValueType.Enemy;
                case BlockKind.EnemyTypeOf: case BlockKind.EnemyTypeName: return ValueType.EnemyType;
                case BlockKind.True: case BlockKind.False: case BlockKind.Comparison: case BlockKind.IsDead: return ValueType.Boolean;
                case BlockKind.Arithmetic:
                    return block.Value == "/" || ((TypeOf(block.Arguments[0], variables) | TypeOf(block.Arguments[1], variables)) & ValueType.Float) != 0
                        ? ValueType.Float : ValueType.Integer;
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

        // A block as the player sees it on screen, for the status line (it showed the enum name: "Comparison", "If").
        internal static string Label(BlockKind kind) => kind switch
        {
            BlockKind.Comparison => "비교 블록", BlockKind.Arithmetic => "계산 블록", BlockKind.DeclareVariable => "변수 = 값",
            BlockKind.Distance => "distance()", BlockKind.IsDead => "is_dead()", BlockKind.EnemyTypeOf => "type", BlockKind.Hp => "hp",
            BlockKind.Shot or BlockKind.Attack => "shoot()", BlockKind.Wait => "wait()", BlockKind.Block => "block()", BlockKind.Boom => "boom()", BlockKind.Slow => "slow()",
            _ => Call(kind) ?? kind.ToString().ToLowerInvariant(),
        };
        // Blocks that read as a fixed call or literal.
        static string Call(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.PositionX: return "get_pos_x()";
                case BlockKind.PositionY: return "get_pos_y()";
                case BlockKind.MyAttackRange: return "my_attack_range()";
                case BlockKind.MyPos: return "get_my_pos()";
                case BlockKind.NearestEnemy: return "get_nearest_enemy()";
                case BlockKind.True: return "True";
                case BlockKind.False: return "False";
                case BlockKind.Buff: return "buff()";
                case BlockKind.Slash: return "slash()";
                default: return null;
            }
        }

        /// <summary>Python text of an expression block without type checks, e.g. a condition not yet connected to the program.</summary>
        public static string ExpressionText(CodeBlock block)
        {
            if (block == null) return "";
            string Arg(int index) => index < block.Arguments.Count ? ExpressionText(block.Arguments[index]) : "";
            switch (block.Kind)
            {
                case BlockKind.Number: case BlockKind.Variable: return block.Value;
                case BlockKind.Distance: return Arg(0) + ".distance(" + Arg(1) + ")";
                case BlockKind.Hp: return Arg(0) + ".hp";
                case BlockKind.IsDead: return Arg(0) + ".is_dead()";
                case BlockKind.EnemyTypeOf: return Arg(0) + ".type";
                case BlockKind.EnemyTypeName: return "Enemy." + block.Value;
                case BlockKind.Comparison: case BlockKind.Arithmetic: return Arg(0) + " " + block.Value + " " + Arg(1);
                default: return Call(block.Kind) ?? block.Kind.ToString();
            }
        }

        static string Expression(CodeBlock block, IReadOnlyDictionary<string, ValueType> variables)
        {
            string Arg(int index) => Expression(block.Arguments[index], variables);
            if (HasEnemyArgument(block.Kind))
            {
                var target = block.Arguments[0];
                Require(target, variables, ValueType.Enemy, "공격 대상에는 Enemy 객체가 필요합니다.");
                string method = block.Kind == BlockKind.Shot || block.Kind == BlockKind.Attack ? "shoot" : block.Kind.ToString().ToLowerInvariant();
                return method + "(" + Expression(target, variables) + ")";
            }
            switch (block.Kind)
            {
                case BlockKind.Number: return NormalizeNumber(block.Value);
                case BlockKind.Variable:
                    TypeOf(block, variables);
                    return NormalizeIdentifier(block.Value);
                case BlockKind.PositionX: case BlockKind.PositionY: case BlockKind.MyAttackRange: case BlockKind.MyPos: case BlockKind.NearestEnemy:
                case BlockKind.True: case BlockKind.False: case BlockKind.Buff: case BlockKind.Slash:
                    return Call(block.Kind);
                case BlockKind.Wait:
                    Require(block.Arguments[0], variables, Numeric, "wait에는 초 단위의 숫자가 필요합니다.");
                    return "wait(" + Arg(0) + ")";
                case BlockKind.Distance:
                    Require(block.Arguments[0], variables, ValueType.Enemy, "distance의 대상은 Enemy 객체여야 합니다.");
                    Require(block.Arguments[1], variables, ValueType.Vector2, "distance에는 위치(Vector2)가 필요합니다. 예: enemy.distance(get_my_pos())");
                    return Arg(0) + ".distance(" + Arg(1) + ")";
                case BlockKind.Hp:
                    Require(block.Arguments[0], variables, ValueType.Enemy, "hp는 Enemy 객체에서만 읽을 수 있습니다.");
                    return Arg(0) + ".hp";
                case BlockKind.EnemyTypeOf:
                    Require(block.Arguments[0], variables, ValueType.Enemy, "type은 Enemy 객체에서만 읽을 수 있습니다. 예: enemy.type");
                    return Arg(0) + ".type";
                case BlockKind.EnemyTypeName:
                    if (!EnemyTypes.Contains(block.Value)) throw new FormatException("적 종류는 Enemy.Normal, Enemy.Fast, Enemy.Tank 중 하나입니다.");
                    return "Enemy." + block.Value;
                case BlockKind.IsDead:
                    // A name not assigned yet reads as no enemy, which is dead: `while True: if enemy.is_dead(): enemy = ...`
                    // picks its first target on the first turn.
                    // ponytail: real Python would raise NameError there; the game's Python treats it as None.
                    var receiver = block.Arguments[0];
                    if (receiver.Kind == BlockKind.Variable && !variables.ContainsKey(NormalizeIdentifier(receiver.Value)))
                        return NormalizeIdentifier(receiver.Value) + ".is_dead()";
                    Require(receiver, variables, ValueType.Enemy, "is_dead()는 Enemy 객체에서만 쓸 수 있습니다. 예: enemy.is_dead()");
                    return Arg(0) + ".is_dead()";
                case BlockKind.Arithmetic:
                    if (!Operators.Contains(block.Value)) throw new FormatException("지원하지 않는 계산 기호입니다.");
                    Require(block.Arguments[0], variables, Numeric, "계산에는 숫자가 필요합니다.");
                    Require(block.Arguments[1], variables, Numeric, "계산에는 숫자가 필요합니다.");
                    string Operand(int index) => block.Arguments[index].Kind == BlockKind.Arithmetic ? "(" + Arg(index) + ")" : Arg(index);
                    return Operand(0) + " " + block.Value + " " + Operand(1);
                case BlockKind.Comparison:
                    if (!Comparisons.Contains(block.Value)) throw new FormatException("지원하지 않는 비교 연산자입니다.");
                    // An enemy's kind is compared only with a kind: `enemy.type != 3` says nothing in the game.
                    bool kind(int i) => (TypeOf(block.Arguments[i], variables) & ValueType.EnemyType) != 0;
                    if (kind(0) != kind(1)) throw new FormatException("적 종류는 Enemy.Tank 같은 종류와 비교해야 합니다. 예: enemy.type != Enemy.Tank");
                    if (block.Value != "==" && block.Value != "!=") // equality works on any values; order needs numbers
                    {
                        Require(block.Arguments[0], variables, Numeric, "대소 비교에는 숫자가 필요합니다.");
                        Require(block.Arguments[1], variables, Numeric, "대소 비교에는 숫자가 필요합니다.");
                    }
                    return Arg(0) + " " + block.Value + " " + Arg(1);
                default: throw new FormatException("이 블록은 식으로 사용할 수 없습니다.");
            }
        }

        // The Python tab's text: the tree printed without checks, so a draft with errors still reads. An empty slot is ___,
        // an empty if/while body pass (PythonTreeParser reads both back). lines: each line block's line number, from 1.
        public const string Hole = "___";
        public static string Print(IReadOnlyList<CodeBlock> blocks, IDictionary<CodeBlock, int> lines = null)
        {
            var source = new StringBuilder();
            PrintSuite(blocks ?? Array.Empty<CodeBlock>(), source, 0, lines);
            return source.ToString();
        }
        static void PrintSuite(IReadOnlyList<CodeBlock> blocks, StringBuilder source, int depth, IDictionary<CodeBlock, int> lines)
        {
            if (blocks.Count == 0 && depth > 0) Append(source, depth, "pass");
            foreach (var block in blocks.Where(b => b != null))
            {
                if (lines != null) lines[block] = source.ToString().Count(c => c == '\n') + 1;
                string Arg0() => PrintArgument(block, 0);
                switch (block.Kind)
                {
                    case BlockKind.If: case BlockKind.Elif: Append(source, depth, block.Kind.ToString().ToLowerInvariant() + " " + Arg0() + ":"); break;
                    case BlockKind.Else: Append(source, depth, "else:"); break;
                    case BlockKind.While: Append(source, depth, "while True:"); break;
                    case BlockKind.For: Append(source, depth, "for i in range(" + Arg0() + "):"); break;
                    case BlockKind.Break: case BlockKind.Continue: Append(source, depth, block.Kind.ToString().ToLowerInvariant()); break;
                    case BlockKind.DeclareVariable: Append(source, depth, PrintName(block.Value) + " = " + Arg0()); break;
                    default: Append(source, depth, PrintExpression(block)); break;
                }
                if (HasBody(block.Kind)) PrintSuite(block.Body, source, depth + 1, lines);
            }
        }
        static string PrintArgument(CodeBlock block, int index) => block.Arguments != null && index < block.Arguments.Count ? PrintExpression(block.Arguments[index]) : Hole;
        static string PrintName(string name)
        {
            try { NormalizeIdentifier(name); return name; }
            catch (FormatException) { return Hole; }
        }
        public static string PrintExpression(CodeBlock block)
        {
            if (block == null) return Hole;
            string Receiver() => PrintName(block.Arguments.FirstOrDefault()?.Value);
            // A calculation inside another is bracketed, as Compile writes it, so the text reads back as the same tree.
            string Operand(int index) => index < block.Arguments.Count && block.Arguments[index] is CodeBlock inner &&
                (inner.Kind == BlockKind.Arithmetic || inner.Kind == BlockKind.Comparison) ? "(" + PrintArgument(block, index) + ")" : PrintArgument(block, index);
            if (HasEnemyArgument(block.Kind))
                return (block.Kind == BlockKind.Shot || block.Kind == BlockKind.Attack ? "shoot" : block.Kind.ToString().ToLowerInvariant()) + "(" + PrintArgument(block, 0) + ")";
            switch (block.Kind)
            {
                case BlockKind.Number: return Number.IsMatch(block.Value ?? "") ? block.Value : Hole;
                case BlockKind.Variable: return PrintName(block.Value);
                case BlockKind.Distance: return Receiver() + ".distance(" + PrintArgument(block, 1) + ")";
                case BlockKind.Hp: return Receiver() + ".hp";
                case BlockKind.IsDead: return Receiver() + ".is_dead()";
                case BlockKind.EnemyTypeOf: return Receiver() + ".type";
                case BlockKind.EnemyTypeName: return "Enemy." + block.Value;
                case BlockKind.Wait: return "wait(" + PrintArgument(block, 0) + ")";
                case BlockKind.Comparison: case BlockKind.Arithmetic: return Operand(0) + " " + block.Value + " " + Operand(1);
                default: return Call(block.Kind) ?? block.Kind.ToString();
            }
        }

        // The 자연어 tab's words for a block (Entry's way of saying it): {name} is a slot or field, in the block's own order.
        // Null: the block reads the same in both tabs.
        public static string Natural(BlockKind kind) => kind switch
        {
            BlockKind.If => "만약 {조건} 이라면", BlockKind.Elif => "아니고 만약 {조건} 이라면", BlockKind.Else => "아니면",
            BlockKind.While => "계속 반복하기", BlockKind.For => "{n} 번 반복하기",
            BlockKind.Break => "반복 멈추기", BlockKind.Continue => "다음 반복으로 넘어가기",
            BlockKind.DeclareVariable => "{이름} 을(를) {값} (으)로 정하기",
            BlockKind.Wait => "{초} 초 기다리기", BlockKind.Shot or BlockKind.Attack => "{적} 에게 쏘기",
            BlockKind.Block => "{적} 막기", BlockKind.Boom => "{적} 에 폭탄 던지기", BlockKind.Slow => "{적} 느리게 하기",
            BlockKind.Slash => "베기", BlockKind.Buff => "강화하기",
            BlockKind.NearestEnemy => "가장 가까운 적", BlockKind.PositionX => "내 x 위치", BlockKind.PositionY => "내 y 위치",
            BlockKind.MyPos => "내 위치", BlockKind.MyAttackRange => "내 공격 범위",
            BlockKind.True => "참", BlockKind.False => "거짓", BlockKind.EnemyTypeName => "{종류}",
            BlockKind.Distance => "{적} 와(과) {위치} 사이 거리", BlockKind.Hp => "{적} 의 체력",
            BlockKind.IsDead => "{적} 이(가) 쓰러졌는가?", BlockKind.EnemyTypeOf => "{적} 의 종류",
            _ => null,
        };
        // The words with each slot as 〔slot〕, for the completion list and hints.
        public static string NaturalText(BlockKind kind) => Natural(kind) is string text ? Regex.Replace(text, @"\{([^}]*)\}", "〔$1〕") : null;
        public static readonly string[] NaturalComparisons = { "=", "≠", "<", ">", "≤", "≥" };
        public static readonly string[] NaturalEnemyTypes = { "일반 적", "빠른 적", "탱커" };

        public static readonly string[] Comparisons = { "==", "!=", "<", ">", "<=", ">=" };
        public static readonly string[] Operators = { "+", "-", "*", "/" };
        // The game's enemy kinds (Defense.EnemyType) as the Python names after `Enemy.`.
        public static readonly string[] EnemyTypes = { "Normal", "Fast", "Tank" };
        static readonly Regex Number = new Regex(@"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)\z");

        public static string NormalizeNumber(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOfAny(new[] { 'e', 'E', 'f', 'F' }) >= 0)
                throw new FormatException("수치를 입력해 주세요. 소수점(.)이 있으면 float, 없으면 int입니다.");
            if (!Number.IsMatch(text)) throw new FormatException("올바른 숫자를 입력해 주세요.");
            // Keep arbitrary-length input as text; parsing as float would lose precision or impose a range.
            string sign = text[0] == '-' ? "-" : "";
            string unsigned = text.TrimStart('+', '-');
            int end = unsigned.IndexOf('.');
            if (end < 0) end = unsigned.Length;
            string integer = unsigned.Substring(0, end).TrimStart('0');
            return sign + (integer.Length == 0 ? "0" : integer) + unsigned.Substring(end);
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

        static void Append(StringBuilder source, int depth, string text) => source.Append(' ', depth * 4).Append(text).Append('\n');
    }
}
