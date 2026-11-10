using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    /// <summary>검증된 블록 코드를 전투 시간에 맞춰 실행하며, 틱당 명령 수를 제한합니다.</summary>
    // Synchronous, like the farm in 'The Farmer Was Replaced' (play feedback 4): the function runs only when called (battle
    // start calls every robot's once, the robot's Play button calls it again), one statement after another, and a call made
    // while it runs is refused, not queued. An attack waits only for its cooldown; a slash with nobody in range still swings.
    public sealed class DefenseProgram
    {
        enum Op { Statement, Condition, Jump, ForInit, ForTest, ForNext }
        sealed class Instruction { public Op Op; public CodeBlock Block; public int Jump, Loop; }
        sealed class LoopState { public double Count, Index; }
        // A value as a struct, not a boxed object: numbers, truth, enemies and positions are made every tick (13차 130).
        enum ValueKind : byte { Number, Truth, Enemy, Position }
        readonly struct Val
        {
            public readonly ValueKind Kind; public readonly double Number; public readonly int Enemy; public readonly Vector2 Position;
            Val(ValueKind kind, double number, int enemy, Vector2 position) { Kind = kind; Number = number; Enemy = enemy; Position = position; }
            public static Val Of(double n) => new Val(ValueKind.Number, n, 0, default);
            public static Val Of(bool b) => new Val(ValueKind.Truth, b ? 1 : 0, 0, default);
            public static Val Of(Vector2 v) => new Val(ValueKind.Position, 0, 0, v);
            public static Val EnemyOf(int id) => new Val(ValueKind.Enemy, 0, id, default);
        }
        readonly List<CodeBlock> blocks;
        readonly Dictionary<BlockKind, int> blockCounts;
        readonly List<Instruction> code = new List<Instruction>();
        readonly Dictionary<string, Val> variables = new Dictionary<string, Val>();
        // Applied code never changes: each number is parsed and each name normalized once, the first time it runs.
        readonly Dictionary<CodeBlock, double> numbers = new Dictionary<CodeBlock, double>();
        readonly Dictionary<CodeBlock, string> names = new Dictionary<CodeBlock, string>();
        readonly Dictionary<int, LoopState> loops = new Dictionary<int, LoopState>();
        int pc, nextLoop;
        double wakeTime;
        public string Name { get; }
        public string Source { get; }
        public string Fault { get; private set; }
        public int StepsLastTick { get; private set; }
        public bool IsEmpty => blocks.Count == 0;
        public int CompletedRuns { get; private set; }
        public bool Running { get; private set; }
        public int BlockCount(BlockKind kind) => blockCounts.TryGetValue(DefenseProgression.Canonical(kind), out int count) ? count : 0;
        public double WaitRemaining(double time) => Math.Max(0, wakeTime - time);
        public IReadOnlyList<CodeBlock> CopyBlocks() => blocks.Select(Clone).ToList();
        void EndInvocation() { pc = 0; Running = false; variables.Clear(); loops.Clear(); }
        // A call while the function runs (or after it faulted) is refused: Play stays locked until it ends.
        public bool Invoke()
        {
            if (Running || Fault != null || IsEmpty) return false;
            EndInvocation(); Running = true; return true;
        }
        public const int InstructionBudget = 64;

        public DefenseProgram(string name, IReadOnlyList<CodeBlock> program)
        {
            Source = PythonTreeCompiler.Compile(name, program);
            Name = PythonTreeCompiler.NormalizeIdentifier(name);
            blocks = program.Select(Clone).ToList();
            // 적용된 코드는 수정되지 않으므로 아이템 수량도 한 번만 계산합니다.
            blockCounts = DefenseProgression.Used(blocks).GroupBy(kind => kind).ToDictionary(group => group.Key, group => group.Count());
            EmitSuite(blocks, null, null);
        }
        static CodeBlock Clone(CodeBlock b) => b == null ? null : new CodeBlock(b.Kind, b.Value, b.Arguments.Select(Clone).ToArray()) { Body = b.Body.Select(Clone).ToList() };
        int Emit(Op op, CodeBlock block = null, int jump = 0, int loop = 0)
        { code.Add(new Instruction { Op = op, Block = block, Jump = jump, Loop = loop }); return code.Count - 1; }
        void EmitSuite(IReadOnlyList<CodeBlock> suite, List<int> breaks, List<int> continues)
        {
            for (int i = 0; i < suite.Count; i++)
            {
                var b = suite[i];
                switch (b.Kind)
                {
                    case BlockKind.If:
                        var exits = new List<int>();
                        do
                        {
                            var branch = suite[i];
                            int condition = branch.Kind == BlockKind.Else ? -1 : Emit(Op.Condition, branch.Arguments[0]);
                            EmitSuite(branch.Body, breaks, continues);
                            exits.Add(Emit(Op.Jump));
                            if (condition >= 0) code[condition].Jump = code.Count;
                            if (branch.Kind == BlockKind.Else || i + 1 == suite.Count ||
                                (suite[i + 1].Kind != BlockKind.Elif && suite[i + 1].Kind != BlockKind.Else)) break;
                            i++;
                        } while (true);
                        foreach (var exit in exits) code[exit].Jump = code.Count;
                        break;
                    case BlockKind.For: case BlockKind.While:
                        int loop = nextLoop++;
                        bool counted = b.Kind == BlockKind.For;
                        if (counted) Emit(Op.ForInit, b.Arguments[0], loop: loop);
                        int start = code.Count;
                        int test = counted ? Emit(Op.ForTest, loop: loop) : -1;
                        var innerBreaks = new List<int>(); var innerContinues = new List<int>();
                        EmitSuite(b.Body, innerBreaks, innerContinues);
                        int step = code.Count;
                        if (counted) Emit(Op.ForNext, jump: test, loop: loop);
                        Emit(Op.Jump, jump: start);
                        if (test >= 0) code[test].Jump = code.Count;
                        foreach (var exit in innerBreaks) code[exit].Jump = code.Count;
                        foreach (var repeat in innerContinues) code[repeat].Jump = step;
                        break;
                    case BlockKind.Break: breaks.Add(Emit(Op.Jump)); break;
                    case BlockKind.Continue: continues.Add(Emit(Op.Jump)); break;
                    default: Emit(Op.Statement, b); break;
                }
            }
        }
        public void Tick(DefenseSimulation sim, RobotState robot)
        {
            StepsLastTick = 0;
            if (!Running || Fault != null || sim.Phase != BattlePhase.Running || sim.Time + 1e-9 < wakeTime) return;
            try
            {
                // 무한 반복문도 한 틱을 독점하지 못하도록 실행 예산을 나눕니다.
                while (StepsLastTick++ < InstructionBudget)
                {
                    if (pc == code.Count) { CompletedRuns++; EndInvocation(); return; }
                    var instruction = code[pc];
                    switch (instruction.Op)
                    {
                        case Op.Jump: pc = instruction.Jump; break;
                        case Op.Condition:
                            pc = Truth(Value(instruction.Block, sim, robot)) ? pc + 1 : instruction.Jump;
                            break;
                        case Op.ForInit:
                            double count = Number(Value(instruction.Block, sim, robot));
                            if (count != Math.Truncate(count) || Math.Abs(count) > 1000000000) throw new FormatException("range 횟수는 절댓값 10억 이하 정수여야 합니다.");
                            loops[instruction.Loop] = new LoopState { Count = count }; pc++; break;
                        case Op.ForTest:
                            var state = loops[instruction.Loop];
                            if (state.Index >= state.Count) pc = instruction.Jump;
                            else { variables["i"] = Val.Of(state.Index); pc++; }
                            break;
                        case Op.ForNext:
                            loops[instruction.Loop].Index++; robot.LoopTurnsThisWave++; pc++; break;
                        default:
                            var block = instruction.Block;
                            if (block.Kind == BlockKind.DeclareVariable)
                            {
                                variables[Identifier(block)] = Value(block.Arguments[0], sim, robot);
                                pc++; break;
                            }
                            if (block.Kind == BlockKind.Wait)
                            {
                                double seconds = Number(Value(block.Arguments[0], sim, robot));
                                if (seconds < 0) throw new FormatException("wait 시간은 0 이상이어야 합니다.");
                                wakeTime = sim.Time + seconds; pc++; return;
                            }
                            var action = Action(block.Kind); robot.Action = action;
                            int target = 0;
                            if (PythonTreeCompiler.HasEnemyArgument(block.Kind))
                            {
                                var enemy = Value(block.Arguments[0], sim, robot);
                                if (enemy.Kind != ValueKind.Enemy) throw new FormatException("공격 대상에는 Enemy 객체가 필요합니다.");
                                target = enemy.Enemy;
                                // 지정한 적이 사라져도 다른 적으로 자동 교체하지 않습니다.
                                if (target == 0 || Active(sim, target) == null)
                                { robot.LastResult = ActionResult.NoTarget; pc++; return; }
                            }
                            // 재사용 대기 중에는 같은 명령에서 기다리고, 실행 결과가 나오면 다음으로 이동합니다. A target out of
                            // range is a miss (NoTarget): the next statement runs, nothing waits for it to come closer.
                            if (sim.RequestAction(robot.Id, action, target) != ActionResult.CoolingDown) pc++;
                            return;
                    }
                }
                StepsLastTick = InstructionBudget;
            }
            catch (FormatException error) { Fault = error.Message; }
        }
        static RobotAction Action(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Slash: return RobotAction.Slash;
                case BlockKind.Block: return RobotAction.Block;
                case BlockKind.Boom: return RobotAction.Boom;
                case BlockKind.Attack: case BlockKind.Shot: return RobotAction.Attack;
                case BlockKind.Slow: return RobotAction.Slow;
                case BlockKind.Buff: return RobotAction.Buff;
                default: throw new FormatException("지원하지 않는 전투 명령입니다: " + kind);
            }
        }
        string Identifier(CodeBlock block)
        {
            if (!names.TryGetValue(block, out var name)) names[block] = name = PythonTreeCompiler.NormalizeIdentifier(block.Value);
            return name;
        }
        static EnemyState Active(DefenseSimulation sim, int id)
        {
            var enemies = sim.Enemies;
            for (int i = 0; i < enemies.Count; i++) if (enemies[i].Id == id && enemies[i].Active) return enemies[i];
            return null;
        }
        static int EnemyId(Val value) => value.Kind == ValueKind.Enemy ? value.Enemy : throw new FormatException("Enemy 객체가 필요합니다.");
        Val Value(CodeBlock block, DefenseSimulation sim, RobotState robot)
        {
            Val Arg(int i) => Value(block.Arguments[i], sim, robot);
            switch (block.Kind)
            {
                case BlockKind.Number:
                    if (!numbers.TryGetValue(block, out double n))
                    {
                        if (!double.TryParse(PythonTreeCompiler.NormalizeNumber(block.Value), NumberStyles.Float, CultureInfo.InvariantCulture, out n) || double.IsNaN(n) || double.IsInfinity(n))
                            throw new FormatException("실행 가능한 숫자 범위를 벗어났습니다.");
                        numbers[block] = n;
                    }
                    return Val.Of(n);
                case BlockKind.True: return Val.Of(true);
                case BlockKind.False: return Val.Of(false);
                case BlockKind.PositionX: return Val.Of(robot.Position.X);
                case BlockKind.PositionY: return Val.Of(robot.Position.Y);
                case BlockKind.MyAttackRange: return Val.Of(robot.Range); // includes buffs, same range the attack uses
                case BlockKind.MyPos: return Val.Of(robot.Position);
                case BlockKind.Variable:
                    if (!variables.TryGetValue(Identifier(block), out var variable)) throw new FormatException("선언되지 않은 변수입니다.");
                    return variable;
                case BlockKind.NearestEnemy:
                {
                    // Rules.Nearest over the active enemies, without the Where enumerator.
                    EnemyState nearest = null; float best = float.MaxValue;
                    foreach (var e in sim.Enemies)
                    {
                        if (!e.Active) continue;
                        float d = Vector2.DistanceSquared(e.Position, robot.Position);
                        if (d < best) { best = d; nearest = e; }
                    }
                    return Val.EnemyOf(nearest?.Id ?? 0);
                }
                case BlockKind.Distance:
                    var enemy = Active(sim, EnemyId(Arg(0)));
                    var from = Arg(1);
                    if (from.Kind != ValueKind.Position) throw new FormatException("distance에는 위치(Vector2)가 필요합니다.");
                    return Val.Of(enemy == null ? double.PositiveInfinity : Vector2.Distance(enemy.Position, from.Position));
                case BlockKind.IsDead:
                    // An enemy variable not assigned yet is no enemy, and no enemy is dead (see PythonTreeCompiler).
                    var receiver = block.Arguments[0];
                    if (receiver.Kind == BlockKind.Variable && !variables.ContainsKey(Identifier(receiver))) return Val.Of(true);
                    int watched = EnemyId(Arg(0));
                    return Val.Of(watched == 0 || Active(sim, watched) == null);
                case BlockKind.Arithmetic:
                    double x = Number(Arg(0)), y = Number(Arg(1));
                    switch (block.Value)
                    {
                        case "+": return Val.Of(x + y); case "-": return Val.Of(x - y); case "*": return Val.Of(x * y);
                        case "/": if (y == 0) throw new FormatException("0으로 나눌 수 없습니다."); return Val.Of(x / y);
                    }
                    break;
                case BlockKind.Hp:
                    return Val.Of(Active(sim, EnemyId(Arg(0)))?.Health ?? 0); // a defeated enemy reads as 0 hp
                case BlockKind.Comparison:
                    var left = Arg(0); var right = Arg(1);
                    if (block.Value == "==")
                    {
                        if (left.Kind == ValueKind.Enemy && right.Kind == ValueKind.Enemy) return Val.Of(left.Enemy == right.Enemy);
                        bool scalar(Val v) => v.Kind == ValueKind.Number || v.Kind == ValueKind.Truth;
                        if (scalar(left) && scalar(right)) return Val.Of(Number(left) == Number(right));
                        return Val.Of(left.Kind == ValueKind.Position && right.Kind == ValueKind.Position && left.Position == right.Position);
                    }
                    double l = Number(left, allowInfinity: true), r = Number(right, allowInfinity: true);
                    switch (block.Value) { case "<": return Val.Of(l < r); case ">": return Val.Of(l > r); case "<=": return Val.Of(l <= r); case ">=": return Val.Of(l >= r); }
                    break;
            }
            throw new FormatException("실행할 수 없는 값 블록입니다: " + block.Kind);
        }
        static bool Truth(Val value) => value.Kind == ValueKind.Truth || value.Kind == ValueKind.Number ? value.Number != 0 :
            value.Kind == ValueKind.Enemy && value.Enemy != 0;
        static double Number(Val value, bool allowInfinity = false)
        {
            double n = value.Kind == ValueKind.Number || value.Kind == ValueKind.Truth ? value.Number : double.NaN;
            if (double.IsNaN(n) || !allowInfinity && double.IsInfinity(n)) throw new FormatException("유효한 숫자가 필요합니다.");
            return n;
        }
    }
}
