using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    /// <summary>Executes the validated block tree with a bounded budget on the battle clock.</summary>
    public sealed class DefenseProgram
    {
        enum Op { Statement, Condition, Jump, ForInit, ForTest, ForNext }
        sealed class Instruction { public Op Op; public CodeBlock Block; public int Jump, Loop; }
        sealed class LoopState { public double Count, Index; }
        sealed class EnemyValue { public int Id; }
        readonly List<CodeBlock> blocks;
        readonly List<Instruction> code = new List<Instruction>();
        readonly Dictionary<string, object> variables = new Dictionary<string, object>();
        readonly Dictionary<int, LoopState> loops = new Dictionary<int, LoopState>();
        int pc, nextLoop;
        double wakeTime;
        public string Name { get; }
        public string Source { get; }
        public string Fault { get; private set; }
        public int StepsLastTick { get; private set; }
        public IReadOnlyList<CodeBlock> CopyBlocks() => blocks.Select(Clone).ToList();
        public const int InstructionBudget = 64;

        public DefenseProgram(string name, IReadOnlyList<CodeBlock> program)
        {
            Source = PythonTreeCompiler.Compile(name, program);
            Name = PythonTreeCompiler.NormalizeIdentifier(name);
            blocks = program.Select(Clone).ToList();
            EmitSuite(blocks, null, null);
        }
        static CodeBlock Clone(CodeBlock b) => new CodeBlock(b.Kind, b.Value, b.Arguments.Select(Clone).ToArray()) { Body = b.Body.Select(Clone).ToList() };
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
                        if (counted) Emit(Op.ForNext, loop: loop);
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
            if (Fault != null || sim.Phase != BattlePhase.Running || sim.Time + 1e-9 < wakeTime) return;
            try
            {
                while (StepsLastTick++ < InstructionBudget)
                {
                    if (pc == code.Count) { pc = 0; variables.Clear(); loops.Clear(); return; }
                    var instruction = code[pc];
                    switch (instruction.Op)
                    {
                        case Op.Jump: pc = instruction.Jump; break;
                        case Op.Condition: pc = Truth(Value(instruction.Block, sim, robot)) ? pc + 1 : instruction.Jump; break;
                        case Op.ForInit:
                            double count = Number(Value(instruction.Block, sim, robot));
                            if (count != Math.Truncate(count) || Math.Abs(count) > 1000000000) throw new FormatException("range 횟수는 절댓값 10억 이하 정수여야 합니다.");
                            loops[instruction.Loop] = new LoopState { Count = count }; pc++; break;
                        case Op.ForTest:
                            var state = loops[instruction.Loop];
                            if (state.Index >= state.Count) pc = instruction.Jump;
                            else { variables["i"] = state.Index; pc++; }
                            break;
                        case Op.ForNext: loops[instruction.Loop].Index++; pc++; break;
                        default:
                            var block = instruction.Block;
                            if (block.Kind == BlockKind.DeclareVariable)
                            { variables[PythonTreeCompiler.NormalizeIdentifier(block.Value)] = Value(block.Arguments[0], sim, robot); pc++; break; }
                            if (block.Kind == BlockKind.Wait)
                            {
                                double seconds = Number(Value(block.Arguments[0], sim, robot));
                                if (seconds < 0) throw new FormatException("wait 시간은 0 이상이어야 합니다.");
                                wakeTime = sim.Time + seconds; pc++; return;
                            }
                            var action = Action(block.Kind); robot.Action = action;
                            int target = 0;
                            if (block.Kind == BlockKind.Attack)
                            {
                                target = ((EnemyValue)Value(block.Arguments[0], sim, robot)).Id;
                                // A missing/stale explicit target never silently selects a different enemy.
                                if (target == 0 || !sim.Enemies.Any(e => e.Id == target && e.Active))
                                { robot.LastResult = ActionResult.NoTarget; pc++; return; }
                            }
                            var result = sim.RequestAction(robot.Id, action, target);
                            if (result != ActionResult.CoolingDown) pc++;
                            return;
                    }
                }
                StepsLastTick = InstructionBudget;
            }
            catch (FormatException error) { Fault = error.Message; }
        }
        public static RobotAction Action(BlockKind kind)
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
        object Value(CodeBlock block, DefenseSimulation sim, RobotState robot)
        {
            object Arg(int i) => Value(block.Arguments[i], sim, robot);
            switch (block.Kind)
            {
                case BlockKind.Number:
                    if (!double.TryParse(PythonTreeCompiler.NormalizeNumber(block.Value), NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || double.IsNaN(n) || double.IsInfinity(n))
                        throw new FormatException("실행 가능한 숫자 범위를 벗어났습니다.");
                    return n;
                case BlockKind.True: return true;
                case BlockKind.False: return false;
                case BlockKind.PositionX: return (double)robot.Position.X;
                case BlockKind.PositionY: return (double)robot.Position.Y;
                case BlockKind.Variable:
                    if (!variables.TryGetValue(PythonTreeCompiler.NormalizeIdentifier(block.Value), out var variable)) throw new FormatException("선언되지 않은 변수입니다.");
                    return variable;
                case BlockKind.NearestEnemy:
                    var nearest = sim.Enemies.Where(e => e.Active).OrderBy(e => Vector2.DistanceSquared(e.Position, robot.Position)).ThenBy(e => e.Id).FirstOrDefault();
                    return new EnemyValue { Id = nearest?.Id ?? 0 };
                case BlockKind.Distance:
                    int id = ((EnemyValue)Arg(0)).Id; var enemy = sim.Enemies.Find(e => e.Id == id && e.Active);
                    double x = Number(Arg(1)), z = Number(Arg(2));
                    return enemy == null ? double.PositiveInfinity : Math.Sqrt(Math.Pow(enemy.Position.X - x, 2) + Math.Pow(enemy.Position.Y - z, 2));
                case BlockKind.Comparison:
                    var left = Arg(0); var right = Arg(1);
                    if (block.Value == "==") return left is EnemyValue a && right is EnemyValue b ? a.Id == b.Id :
                        (left is double || left is bool) && (right is double || right is bool) ? Number(left) == Number(right) : Equals(left, right);
                    double l = Number(left, allowInfinity: true), r = Number(right, allowInfinity: true);
                    switch (block.Value) { case "<": return l < r; case ">": return l > r; case "<=": return l <= r; case ">=": return l >= r; }
                    break;
            }
            throw new FormatException("실행할 수 없는 값 블록입니다: " + block.Kind);
        }
        static bool Truth(object value) => value is bool b ? b : value is double d ? d != 0 : value is EnemyValue e && e.Id != 0;
        static double Number(object value, bool allowInfinity = false)
        {
            double n = value is double d ? d : value is bool b ? (b ? 1 : 0) : double.NaN;
            if (double.IsNaN(n) || !allowInfinity && double.IsInfinity(n)) throw new FormatException("유효한 숫자가 필요합니다.");
            return n;
        }
    }
}
