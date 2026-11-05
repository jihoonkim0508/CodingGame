using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    /// <summary>검증된 블록 코드를 전투 시간에 맞춰 실행하며, 틱당 명령 수를 제한합니다.</summary>
    public sealed class DefenseProgram
    {
        enum Op { Statement, Condition, Jump, ForInit, ForTest, ForNext }
        sealed class Instruction { public Op Op; public CodeBlock Block; public int Jump, Loop; }
        sealed class LoopState { public double Count, Index; }
        sealed class EnemyValue { public int Id; }
        readonly List<CodeBlock> blocks;
        readonly Dictionary<BlockKind, int> blockCounts;
        readonly List<Instruction> code = new List<Instruction>();
        readonly Dictionary<string, object> variables = new Dictionary<string, object>();
        readonly HashSet<string> nearestVariables = new HashSet<string>();
        readonly Dictionary<int, LoopState> loops = new Dictionary<int, LoopState>();
        int pc, nextLoop, invocationTargetId;
        bool invocationStarted;
        bool actedThisInvocation;
        readonly HashSet<int> completedTargets = new HashSet<int>();
        readonly HashSet<int> invocationTargets = new HashSet<int>();
        readonly bool targetsEnemies;
        public bool UsesEnemyCycles(DefenseSimulation sim) => sim.Setup.RunProgramPerEnemy && targetsEnemies;
        public bool CanTargetEnemy(DefenseSimulation sim, int id) => !UsesEnemyCycles(sim) ||
            (!completedTargets.Contains(id) && (invocationTargetId == 0 || invocationTargetId == id));
        // Reserve the targets of a swing at launch, so its delayed impact cannot cause a second automatic invocation.
        public void RecordAttackTargets(IEnumerable<int> ids) { foreach (int id in ids) invocationTargets.Add(id); }
        public bool? LastCondition { get; private set; }
        double wakeTime;
        public string Name { get; }
        public string Source { get; }
        public string Fault { get; private set; }
        public int StepsLastTick { get; private set; }
        public bool IsEmpty => blocks.Count == 0;
        public int CompletedRuns { get; private set; }
        // Body entries, never hits or kills. Retained after completion for inspection.
        public int LoopIteration { get; private set; }
        public int LoopCount { get; private set; }
        public int LastTargetId { get; private set; }
        public string ExecutionStatus(DefenseSimulation sim, RobotState robot)
        {
            if (IsEmpty) return "코드 없음";
            if (Fault != null) return "코드 중지 · " + Fault;
            if (Completed(sim)) return UsesEnemyCycles(sim) ? "이번 대상 실행 완료 · 새 몬스터 대기" : "프로그램 종료";
            if (WaitRemaining(sim.Time) > 0) return "대기 명령 실행 중";
            if (robot.LastResult == ActionResult.NoTarget)
            {
                var target = sim.Enemies.FirstOrDefault(enemy => enemy.Id == LastTargetId && enemy.Active);
                if (target == null && invocationTargetId != 0 && invocationStarted) return "대상 소멸 · 남은 반복 진행";
                return target != null && Vector2.DistanceSquared(target.Position, robot.Position) > robot.Range * robot.Range ? "사거리 밖" : "대상 없음";
            }
            if (robot.LastResult == ActionResult.CoolingDown) return "공격 재사용 대기";
            if (robot.LastResult == ActionResult.Ineffective) return "호환되지 않는 명령";
            return StepsLastTick == 0 ? "실행 대기" : "실행 중";
        }
        public bool Completed(DefenseSimulation sim) => UsesEnemyCycles(sim)
            ? !invocationStarted && CompletedRuns > 0 && !sim.Enemies.Any(e => e.Active && CanTargetEnemy(sim, e.Id))
            : CompletedRuns >= sim.FunctionRepeatLimit;
        public int BlockCount(BlockKind kind) => blockCounts.TryGetValue(DefenseProgression.Canonical(kind), out int count) ? count : 0;
        public double WaitRemaining(double time) => Math.Max(0, wakeTime - time);
        public IReadOnlyList<CodeBlock> CopyBlocks() => blocks.Select(Clone).ToList();
        void EndInvocation() { pc = 0; invocationTargetId = 0; invocationStarted = actedThisInvocation = false; variables.Clear(); nearestVariables.Clear(); loops.Clear(); }
        public const int InstructionBudget = 64;

        public DefenseProgram(string name, IReadOnlyList<CodeBlock> program)
        {
            Source = PythonTreeCompiler.Compile(name, program);
            Name = PythonTreeCompiler.NormalizeIdentifier(name);
            blocks = program.Select(Clone).ToList();
            targetsEnemies = blocks.Any(TargetsEnemies);
            // 적용된 코드는 수정되지 않으므로 아이템 수량도 한 번만 계산합니다.
            blockCounts = DefenseProgression.Used(blocks).GroupBy(kind => kind).ToDictionary(group => group.Key, group => group.Count());
            EmitSuite(blocks, null, null);
        }
        static CodeBlock Clone(CodeBlock b) => b == null ? null : new CodeBlock(b.Kind, b.Value, b.Arguments.Select(Clone).ToArray()) { Body = b.Body.Select(Clone).ToList() };
        int Emit(Op op, CodeBlock block = null, int jump = 0, int loop = -1)
        { code.Add(new Instruction { Op = op, Block = block, Jump = jump, Loop = loop }); return code.Count - 1; }
        void EmitSuite(IReadOnlyList<CodeBlock> suite, List<int> breaks, List<int> continues, int activeLoop = -1)
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
                            EmitSuite(branch.Body, breaks, continues, activeLoop);
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
                        Emit(Op.ForInit, counted ? b.Arguments[0] : new CodeBlock(BlockKind.True), loop: loop);
                        int start = code.Count;
                        int test = Emit(Op.ForTest, loop: loop);
                        var innerBreaks = new List<int>(); var innerContinues = new List<int>();
                        EmitSuite(b.Body, innerBreaks, innerContinues, loop);
                        int step = code.Count;
                        Emit(Op.ForNext, loop: loop);
                        Emit(Op.Jump, jump: start);
                        if (test >= 0) code[test].Jump = code.Count;
                        foreach (var exit in innerBreaks) code[exit].Jump = code.Count;
                        foreach (var repeat in innerContinues) code[repeat].Jump = step;
                        break;
                    case BlockKind.Break: breaks.Add(Emit(Op.Jump)); break;
                    case BlockKind.Continue: continues.Add(Emit(Op.Jump)); break;
                    default: Emit(Op.Statement, b, loop: activeLoop); break;
                }
            }
        }
        public void Tick(DefenseSimulation sim, RobotState robot)
        {
            StepsLastTick = 0;
            if (Fault != null || Completed(sim) || sim.Phase != BattlePhase.Running || sim.Time + 1e-9 < wakeTime) return;
            // A program that targets enemies starts a run only once one is in range, so idle runs don't use up the repeat limit.
            if (!invocationStarted)
            {
                if (targetsEnemies && !sim.Enemies.Any(e => e.Active && CanTargetEnemy(sim, e.Id) &&
                    (blocks.Any(b => b.Kind == BlockKind.If) || Vector2.DistanceSquared(e.Position, robot.Position) <= robot.Range * robot.Range))) return;
                invocationStarted = true;
            }
            try
            {
                // 무한 반복문도 한 틱을 독점하지 못하도록 실행 예산을 나눕니다.
                while (StepsLastTick++ < InstructionBudget)
                {
                    if (pc == code.Count)
                    {
                        // Finish the last swing/projectile before allowing another enemy's program to start.
                        if (sim.HasPendingActions(robot.Id)) return;
                        // A false condition has not used an action. It must be retried after enemies move,
                        // without spending one of the wave's limited successful program runs.
                        if (actedThisInvocation)
                        {
                            CompletedRuns++;
                            if (UsesEnemyCycles(sim)) completedTargets.UnionWith(invocationTargets);
                        }
                        else wakeTime = Math.Max(wakeTime, sim.Time + .25);
                        invocationTargets.Clear();
                        EndInvocation(); return;
                    }
                    var instruction = code[pc];
                    switch (instruction.Op)
                    {
                        case Op.Jump: pc = instruction.Jump; break;
                        case Op.Condition: LastCondition = Truth(Value(instruction.Block, sim, robot)); pc = LastCondition.Value ? pc + 1 : instruction.Jump; break;
                        case Op.ForInit:
                            if (instruction.Block.Kind == BlockKind.True)
                            {
                                loops[instruction.Loop] = new LoopState { Count = double.PositiveInfinity };
                                pc++; break;
                            }
                            double count = Number(Value(instruction.Block, sim, robot));
                            if (count != Math.Truncate(count) || count < 0 || count > PythonTreeCompiler.MaxLoopIterations) throw new FormatException($"반복 횟수는 0~{PythonTreeCompiler.MaxLoopIterations}회 정수여야 합니다.");
                            loops[instruction.Loop] = new LoopState { Count = count }; pc++; break;
                        case Op.ForTest:
                            var state = loops[instruction.Loop];
                            if (state.Index >= state.Count) pc = instruction.Jump;
                            else { variables["i"] = state.Index; LoopIteration = (int)Math.Min(state.Index, int.MaxValue);
                                LoopCount = double.IsPositiveInfinity(state.Count) ? -1 : (int)state.Count; pc++; }
                            break;
                        case Op.ForNext: LoopIteration = (int)Math.Min(++loops[instruction.Loop].Index, int.MaxValue); pc++; break;
                        default:
                            var block = instruction.Block;
                            if (block.Kind == BlockKind.DeclareVariable)
                            {
                                string name = PythonTreeCompiler.NormalizeIdentifier(block.Value);
                                var value = Value(block.Arguments[0], sim, robot);
                                variables[name] = value;
                                if (block.Arguments[0].Kind == BlockKind.NearestEnemy) nearestVariables.Add(name);
                                else nearestVariables.Remove(name);
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
                                var enemy = Value(block.Arguments[0], sim, robot) as EnemyValue;
                                if (enemy == null) throw new FormatException("공격 대상에는 Enemy 객체가 필요합니다.");
                                target = enemy.Id; LastTargetId = target;
                                if (target == 0 || !sim.Enemies.Any(e => e.Id == target && e.Active))
                                {
                                    // get_nearest_enemy()로 저장한 변수는 새 적이 들어오면 다시 찾습니다.
                                    if (block.Arguments[0].Kind == BlockKind.Variable)
                                    {
                                        string name = PythonTreeCompiler.NormalizeIdentifier(block.Arguments[0].Value);
                                        if (nearestVariables.Contains(name))
                                            variables[name] = Value(new CodeBlock(BlockKind.NearestEnemy), sim, robot);
                                    }
                                    robot.LastResult = ActionResult.NoTarget; return;
                                }
                            }
                            float damageMultiplier = 1;
                            if (instruction.Loop >= 0 && loops.TryGetValue(instruction.Loop, out var repetition) && repetition.Count > 1)
                                damageMultiplier -= .1f * (float)(double.IsPositiveInfinity(repetition.Count)
                                    ? Math.Min(repetition.Index, 9) / 9
                                    : repetition.Index / (repetition.Count - 1));
                            var result = sim.RequestAction(robot.Id, action, target, damageMultiplier);
                            if (result == ActionResult.Executed) { actedThisInvocation = true; if (target != 0) invocationTargets.Add(target); }
                            // 재사용 대기 중에는 같은 명령에서 기다리고, 실행 결과가 나오면 다음으로 이동합니다.
                            if (result != ActionResult.CoolingDown && result != ActionResult.NoTarget) pc++;
                            return;
                    }
                }
                StepsLastTick = InstructionBudget;
            }
            catch (FormatException error) { Fault = error.Message; }
        }
        static bool TargetsEnemies(CodeBlock block) => block != null && (PythonTreeCompiler.HasEnemyArgument(block.Kind) ||
            block.Kind == BlockKind.NearestEnemy || block.Arguments.Any(TargetsEnemies) || block.Body.Any(TargetsEnemies));
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
                case BlockKind.MyAttackRange: return (double)robot.Range; // includes buffs, same range the attack uses
                case BlockKind.MyPos: return robot.Position;
                case BlockKind.Variable:
                    if (!variables.TryGetValue(PythonTreeCompiler.NormalizeIdentifier(block.Value), out var variable)) throw new FormatException("선언되지 않은 변수입니다.");
                    return variable;
                case BlockKind.NearestEnemy:
                    // A loop keeps its original target, including after that target dies.
                    // Remaining iterations then skip the dead target; only the next invocation may choose another.
                    if (UsesEnemyCycles(sim) && invocationTargetId != 0) return new EnemyValue { Id = invocationTargetId };
                    var nearest = Rules.Nearest(sim.Enemies.Where(e => e.Active && CanTargetEnemy(sim, e.Id)), robot.Position);
                    if (UsesEnemyCycles(sim) && nearest != null) invocationTargetId = nearest.Id;
                    return new EnemyValue { Id = nearest?.Id ?? 0 };
                case BlockKind.Distance:
                    int id = ((EnemyValue)Arg(0)).Id; var enemy = sim.Enemies.Find(e => e.Id == id && e.Active);
                    if (!(Arg(1) is Vector2 from)) throw new FormatException("distance에는 위치(Vector2)가 필요합니다.");
                    return enemy == null ? double.PositiveInfinity : (double)Vector2.Distance(enemy.Position, from);
                case BlockKind.Hp:
                    int target = ((EnemyValue)Arg(0)).Id;
                    return (double)(sim.Enemies.Find(e => e.Id == target && e.Active)?.Health ?? 0); // a defeated enemy reads as 0 hp
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
