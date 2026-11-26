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
        // A for-each keeps the items it was given (get_enemies(): front first; a list: as it stood when the loop began) and
        // walks them; reused, so no list per turn.
        sealed class LoopState { public double Count, Index; public readonly List<Val> Items = new List<Val>(); }
        // A value as a struct, not a boxed object: numbers, truth, enemies and positions are made every tick (13차 130). A list
        // or dict is a reference (Ref): two names, or two robots through the team board, given one share it.
        public enum ValueKind : byte { Number, Truth, Enemy, Position, Kind, List, Dict, Robot, Role }
        public readonly struct Val
        {
            public readonly ValueKind Kind; public readonly double Number; public readonly int Enemy; public readonly Vector2 Position;
            public readonly object Ref;
            Val(ValueKind kind, double number, int enemy, Vector2 position, object reference = null) { Kind = kind; Number = number; Enemy = enemy; Position = position; Ref = reference; }
            public static Val Of(double n) => new Val(ValueKind.Number, n, 0, default);
            public static Val Of(bool b) => new Val(ValueKind.Truth, b ? 1 : 0, 0, default);
            public static Val Of(Vector2 v) => new Val(ValueKind.Position, 0, 0, v);
            public static Val EnemyOf(int id) => new Val(ValueKind.Enemy, 0, id, default);
            // An enemy's kind (EnemyType as a number); -1 is no enemy's kind, different from every kind.
            public static Val KindOf(int type) => new Val(ValueKind.Kind, type, 0, default);
            // A robot by its id (in the Enemy field: ids are unique across robots and enemies), its role as RobotRole's int.
            public static Val RobotOf(int id) => new Val(ValueKind.Robot, 0, id, default);
            public static Val RoleOf(int role) => new Val(ValueKind.Role, role, 0, default);
            public static Val ListOf(List<Val> items) => new Val(ValueKind.List, 0, 0, default, items);
            public static Val DictOf(Dict table) => new Val(ValueKind.Dict, 0, 0, default, table);
            public List<Val> Items => Ref as List<Val>;
            public Dict Table => Ref as Dict;
        }
        // Keys in the order written ({Enemy.Healer: 0, ...}), for the board's table; a handful of entries, so a list.
        public sealed class Dict { public readonly List<Val> Keys = new List<Val>(), Values = new List<Val>(); }
        // Python's == on two values: an enemy or robot is itself, a kind or role its number, a list or dict the same object.
        public static bool Same(Val a, Val b)
        {
            bool scalar(Val v) => v.Kind == ValueKind.Number || v.Kind == ValueKind.Truth;
            if (scalar(a) && scalar(b)) return a.Number == b.Number;
            if (a.Kind != b.Kind) return false;
            switch (a.Kind)
            {
                case ValueKind.Enemy: case ValueKind.Robot: return a.Enemy == b.Enemy;
                case ValueKind.Kind: case ValueKind.Role: return a.Number == b.Number;
                case ValueKind.Position: return a.Position == b.Position;
                default: return ReferenceEquals(a.Ref, b.Ref);
            }
        }
        // Statements whose argument pops: evaluated once, not again every tick the action waits on its cooldown.
        readonly HashSet<CodeBlock> pops = new HashSet<CodeBlock>();
        Val waitingTarget;
        readonly List<CodeBlock> blocks;
        readonly Dictionary<BlockKind, int> blockCounts;
        readonly List<Instruction> code = new List<Instruction>();
        readonly Dictionary<string, Val> variables = new Dictionary<string, Val>();
        // Applied code never changes: each number is parsed and each name normalized once, the first time it runs.
        readonly Dictionary<CodeBlock, double> numbers = new Dictionary<CodeBlock, double>();
        readonly Dictionary<CodeBlock, string> names = new Dictionary<CodeBlock, string>();
        readonly Dictionary<int, LoopState> loops = new Dictionary<int, LoopState>();
        int pc, nextLoop, waitingAt = -1; // waitingAt: the attack statement cooling down, waitedReflecting: its target was reflecting then
        bool waitedReflecting;
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
        // The block the function stands at (a statement, a condition, a loop's header), or on a loop's step the statement just
        // run before it, for the code hologram.
        public CodeBlock CurrentBlock => Running && pc < code.Count ? code[pc].Block ?? (pc > 0 ? code[pc - 1].Block : null) : null;
        public IReadOnlyList<CodeBlock> CopyBlocks() => blocks.Select(Clone).ToList();
        void EndInvocation() { pc = 0; waitingAt = -1; Running = false; variables.Clear(); loops.Clear(); }
        // A call while the function runs (or after it faulted) is refused: Play stays locked until it ends.
        public bool Invoke()
        {
            if (Running || Fault != null || IsEmpty) return false;
            EndInvocation(); Running = true; return true;
        }
        public const int InstructionBudget = 64;

        public DefenseProgram(string name, IReadOnlyList<CodeBlock> program, PythonTreeCompiler.Shared shared = null)
        {
            Source = PythonTreeCompiler.Compile(name, program, shared);
            Name = PythonTreeCompiler.NormalizeIdentifier(name);
            blocks = program.Select(Clone).ToList();
            // 적용된 코드는 수정되지 않으므로 아이템 수량도 한 번만 계산합니다.
            blockCounts = DefenseProgression.Used(blocks).GroupBy(kind => kind).ToDictionary(group => group.Key, group => group.Count());
            EmitSuite(blocks, null, null);
            foreach (var b in Flatten(blocks)) if (b.Kind != BlockKind.DeclareVariable && b.Arguments.Any(a => Flatten(new[] { a }).Any(x => x?.Kind == BlockKind.Pop))) pops.Add(b);
        }
        static IEnumerable<CodeBlock> Flatten(IEnumerable<CodeBlock> list) => list.Where(b => b != null).SelectMany(b => new[] { b }.Concat(Flatten(b.Arguments)).Concat(Flatten(b.Body)));
        // The team board's lines, once at a wave's start, into its variables (DefenseTeamBoard.Run): values that need no robot.
        internal static void RunBoard(IReadOnlyList<CodeBlock> lines, DefenseSimulation sim, Dictionary<string, Val> into)
        {
            var board = new DefenseProgram("team_board", lines);
            foreach (var line in board.blocks) into[board.Identifier(line)] = board.Value(line.Arguments[0], sim, null);
        }
        internal static CodeBlock Clone(CodeBlock b) => b == null ? null : new CodeBlock(b.Kind, b.Value, b.Arguments.Select(Clone).ToArray()) { Body = b.Body.Select(Clone).ToList() };
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
                    case BlockKind.For: case BlockKind.While: case BlockKind.ForEach: case BlockKind.WhileCondition:
                        int loop = nextLoop++;
                        bool counted = b.Kind == BlockKind.For || b.Kind == BlockKind.ForEach;
                        // ForInit/ForTest carry the loop block: a for-each reads its list and its variable's name from it.
                        if (counted) Emit(Op.ForInit, b, loop: loop);
                        int start = code.Count;
                        // while <condition>: the condition is tested before every turn, false leaves the loop.
                        int test = counted ? Emit(Op.ForTest, b, loop: loop) : b.Kind == BlockKind.WhileCondition ? Emit(Op.Condition, b.Arguments[0]) : -1;
                        var innerBreaks = new List<int>(); var innerContinues = new List<int>();
                        EmitSuite(b.Body, innerBreaks, innerContinues);
                        int step = code.Count;
                        Emit(Op.ForNext, jump: test, loop: loop); // while counts its turns too (the debrief)
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
                        case Op.ForInit when instruction.Block.Kind == BlockKind.ForEach:
                        {
                            if (!loops.TryGetValue(instruction.Loop, out var each)) loops[instruction.Loop] = each = new LoopState();
                            var list = instruction.Block.Arguments[0];
                            if (list.Kind == BlockKind.GetEnemies) ListEnemies(sim, each.Items);
                            else { var items = ItemsOf(Value(list, sim, robot)); each.Items.Clear(); each.Items.AddRange(items); }
                            each.Count = each.Items.Count; each.Index = 0; pc++; break;
                        }
                        case Op.ForInit:
                            double count = Number(Value(instruction.Block.Arguments[0], sim, robot));
                            if (count != Math.Truncate(count) || Math.Abs(count) > 1000000000) throw new FormatException("range 횟수는 절댓값 10억 이하 정수여야 합니다.");
                            loops[instruction.Loop] = new LoopState { Count = count }; pc++; break;
                        case Op.ForTest:
                            var state = loops[instruction.Loop];
                            if (state.Index >= state.Count) pc = instruction.Jump;
                            else
                            {
                                if (instruction.Block.Kind == BlockKind.ForEach) Assign(sim, Identifier(instruction.Block), state.Items[(int)state.Index]);
                                else variables["i"] = Val.Of(state.Index);
                                pc++;
                            }
                            break;
                        case Op.ForNext:
                            if (loops.TryGetValue(instruction.Loop, out var turn)) turn.Index++; // while has no counter
                            robot.LoopTurnsThisWave++; pc++; break;
                        default:
                            var block = instruction.Block;
                            if (block.Kind == BlockKind.DeclareVariable)
                            {
                                Assign(sim, Identifier(block), Value(block.Arguments[0], sim, robot));
                                pc++; break;
                            }
                            if (block.Kind == BlockKind.Append)
                            {
                                var into = Value(block.Arguments[0], sim, robot); var items = ItemsOf(into);
                                var item = Value(block.Arguments[1], sim, robot);
                                if (item.Kind == ValueKind.List || item.Kind == ValueKind.Dict) throw new FormatException("리스트 안에 리스트나 사전은 넣을 수 없어요.");
                                items.Add(item); sim.Board.Record("append", items, item, robot.Id, sim.Time);
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
                                var enemy = waitingAt == pc && pops.Contains(block) ? waitingTarget : Value(block.Arguments[0], sim, robot);
                                waitingTarget = enemy;
                                if (enemy.Kind != ValueKind.Enemy) throw new FormatException("공격 대상에는 Enemy 객체가 필요합니다.");
                                target = enemy.Enemy;
                                // 지정한 적이 사라져도 다른 적으로 자동 교체하지 않습니다.
                                if (target == 0 || Active(sim, target) == null)
                                { robot.LastResult = ActionResult.NoTarget; pc++; return; }
                            }
                            // 재사용 대기 중에는 같은 명령에서 기다리고, 실행 결과가 나오면 다음으로 이동합니다. A target out of
                            // range is a miss (NoTarget): the next statement runs, nothing waits for it to come closer.
                            // A target that starts reflecting while the robot waits here is given up like a miss, so code that
                            // checked e.reflecting before the attack is not caught by the cooldown (a shot at it would come back).
                            bool reflecting = target != 0 && Active(sim, target).Reflecting;
                            if (sim.RequestAction(robot.Id, action, target) != ActionResult.CoolingDown || waitingAt == pc && reflecting && !waitedReflecting)
                            { pc++; waitingAt = -1; }
                            else { waitingAt = pc; waitedReflecting = reflecting; }
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
                case BlockKind.BreakShield: return RobotAction.BreakShield;
                default: throw new FormatException("지원하지 않는 전투 명령입니다: " + kind);
            }
        }
        // A name the team board declares is the board's, for every robot (read and written there); any other is this call's own.
        void Assign(DefenseSimulation sim, string name, Val value)
        {
            if (sim.Board.Variables.ContainsKey(name)) sim.Board.Variables[name] = value; else variables[name] = value;
        }
        bool Declared(DefenseSimulation sim, string name) => sim.Board.Variables.ContainsKey(name) || variables.ContainsKey(name);
        static List<Val> ItemsOf(Val value) => value.Items ?? throw new FormatException("리스트가 필요해요. 예: jobs = [] 다음에 jobs.append(e)");
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
        // get_enemies(): the active enemies, the one nearest the core (least road left) first, then the older one.
        static readonly List<(float left, int id)> order = new List<(float, int)>();
        static void ListEnemies(DefenseSimulation sim, List<Val> into)
        {
            order.Clear(); into.Clear();
            foreach (var e in sim.Enemies) if (e.Active && !DefenseSimulation.Rides(e)) order.Add((sim.RemainingPath(e), e.Id));
            order.Sort();
            foreach (var (_, id) in order) into.Add(Val.EnemyOf(id));
        }
        static int EnemyId(Val value) => value.Kind == ValueKind.Enemy ? value.Enemy : throw new FormatException("Enemy 객체가 필요합니다.");
        static string Shown(Val v) => v.Kind == ValueKind.Kind ? "Enemy." + (v.Number >= 0 && v.Number < PythonTreeCompiler.EnemyTypes.Length ? PythonTreeCompiler.EnemyTypes[(int)v.Number] : "?") :
            v.Kind == ValueKind.Role ? "Robot." + (v.Number >= 0 && v.Number < PythonTreeCompiler.RobotRoles.Length ? PythonTreeCompiler.RobotRoles[(int)v.Number] : "?") :
            v.Kind == ValueKind.Number ? v.Number.ToString(CultureInfo.InvariantCulture) : v.Kind.ToString();
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
                case BlockKind.MyAttackRange: return Val.Of(robot.Range); // the same range the attack uses
                case BlockKind.MyHp: return Val.Of(robot.Health ?? 0);
                case BlockKind.And: return Val.Of(Truth(Arg(0)) && Truth(Arg(1))); // the right side only when the left is true
                case BlockKind.Reflecting: return Val.Of(Active(sim, EnemyId(Arg(0)))?.Reflecting ?? false);
                case BlockKind.MyPos: return Val.Of(robot.Position);
                case BlockKind.Variable:
                    if (!sim.Board.Variables.TryGetValue(Identifier(block), out var variable) && !variables.TryGetValue(Identifier(block), out variable))
                        throw new FormatException("선언되지 않은 변수입니다: " + Identifier(block));
                    return variable;
                case BlockKind.EmptyList: return Val.ListOf(new List<Val>());
                case BlockKind.GetEnemies: { var all = new List<Val>(); ListEnemies(sim, all); return Val.ListOf(all); }
                case BlockKind.GetRobots: { var all = new List<Val>(); foreach (var each in sim.Robots) all.Add(Val.RobotOf(each.Id)); return Val.ListOf(all); }
                case BlockKind.DictLiteral:
                {
                    var table = new Dict();
                    for (int i = 0; i + 1 < block.Arguments.Count; i += 2)
                    {
                        if (block.Arguments[i] == null) continue;
                        var key = Arg(i); int at = table.Keys.FindIndex(k => Same(k, key));
                        if (at >= 0) table.Values[at] = Arg(i + 1); else { table.Keys.Add(key); table.Values.Add(Arg(i + 1)); }
                    }
                    return Val.DictOf(table);
                }
                case BlockKind.Pop:
                {
                    var source = Arg(0); var items = ItemsOf(source);
                    if (items.Count == 0) throw new FormatException("빈 리스트에서는 pop(0)을 할 수 없어요 (IndexError). 먼저 if len(" + Receiver(block) + ") > 0:으로 확인하세요.");
                    var item = items[0]; items.RemoveAt(0);
                    sim.Board.Record("pop", items, item, robot?.Id ?? 0, sim.Time);
                    if (robot != null && item.Kind == ValueKind.Enemy) robot.Job = item.Enemy; // the board draws its line to it
                    return item;
                }
                case BlockKind.Len:
                {
                    var of = Arg(0);
                    return Val.Of(of.Items?.Count ?? of.Table?.Keys.Count ?? throw new FormatException("len()에는 리스트나 사전이 필요해요."));
                }
                case BlockKind.In:
                {
                    var item = Arg(0); var within = Arg(1);
                    var keys = within.Items ?? within.Table?.Keys ?? throw new FormatException(block.Value + "의 오른쪽에는 리스트나 사전이 필요해요.");
                    bool found = false; foreach (var k in keys) if (Same(k, item)) { found = true; break; }
                    return Val.Of(found == (block.Value == "in"));
                }
                case BlockKind.Not: return Val.Of(!Truth(Arg(0)));
                case BlockKind.Index:
                {
                    var of = Arg(0); var key = Arg(1);
                    if (of.Table is Dict table)
                    {
                        int at = table.Keys.FindIndex(k => Same(k, key));
                        if (at < 0) throw new FormatException(Receiver(block) + "에 " + Shown(key) + " 키가 없어요 (KeyError). 사전에 그 키를 넣어 주세요.");
                        return table.Values[at];
                    }
                    var items = ItemsOf(of); double slot = Number(key);
                    int index = (int)slot < 0 ? items.Count + (int)slot : (int)slot;
                    if (slot != Math.Truncate(slot) || index < 0 || index >= items.Count) throw new FormatException(Receiver(block) + "에 " + slot + "번 칸이 없어요 (IndexError).");
                    return items[index];
                }
                case BlockKind.Role:
                {
                    var who = Arg(0); if (who.Kind != ValueKind.Robot) throw new FormatException("role은 로봇에서만 읽을 수 있어요.");
                    var bot = sim.FindRobot(who.Enemy); return Val.RoleOf(bot == null ? -1 : (int)bot.Spec.role);
                }
                case BlockKind.RobotRoleName:
                    int role = Array.IndexOf(PythonTreeCompiler.RobotRoles, block.Value);
                    if (role < 0) throw new FormatException("알 수 없는 로봇 종류입니다: Robot." + block.Value);
                    return Val.RoleOf(role);
                // The part that can be hit now: a centipede's open end, a tree's leftmost living leaf, any other enemy itself.
                case BlockKind.WeakPoint:
                {
                    var whole = Active(sim, EnemyId(Arg(0)));
                    return Val.EnemyOf(whole == null ? 0 : DefenseSimulation.WeakPoint(whole).Id);
                }
                case BlockKind.NearestEnemy:
                {
                    // Rules.Nearest over the active enemies, without the Where enumerator.
                    EnemyState nearest = null; float best = float.MaxValue;
                    foreach (var e in sim.Enemies)
                    {
                        if (!e.Active || DefenseSimulation.Rides(e)) continue; // a tree's root stands for the whole tree
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
                    if (receiver.Kind == BlockKind.Variable && !Declared(sim, Identifier(receiver))) return Val.Of(true);
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
                {
                    var of = Arg(0);
                    if (of.Kind == ValueKind.Robot) return Val.Of(sim.FindRobot(of.Enemy)?.Health ?? 0); // a robot gone for the wave reads 0
                    return Val.Of(Active(sim, EnemyId(of))?.Health ?? 0); // a defeated enemy reads as 0 hp
                }
                case BlockKind.Shield:
                    return Val.Of(Active(sim, EnemyId(Arg(0)))?.Shield ?? 0);
                // A centipede's ends and length; any other enemy is a centipede of one (itself). A defeated one is no enemy.
                case BlockKind.Front: case BlockKind.Top:
                    var segment = Active(sim, EnemyId(Arg(0)));
                    return Val.EnemyOf(segment == null ? 0 : segment.Chain == null ? segment.Id : (block.Kind == BlockKind.Front ? segment.Chain.Front : segment.Chain.Top).Id);
                case BlockKind.Size:
                    var counted = Active(sim, EnemyId(Arg(0)));
                    return Val.Of(counted == null ? 0 : counted.Chain?.Segments.Count ?? 1);
                // A tree node's child, or None (no enemy) when it has none there or that child died.
                case BlockKind.Left: case BlockKind.Right:
                    var node = Active(sim, EnemyId(Arg(0)))?.Node;
                    var child = node == null ? null : block.Kind == BlockKind.Left ? node.Left : node.Right;
                    return Val.EnemyOf(child != null && child.Active ? child.Id : 0);
                case BlockKind.Or: return Val.Of(Truth(Arg(0)) || Truth(Arg(1)));
                case BlockKind.EnemyTypeOf:
                    return Val.KindOf((int?)Active(sim, EnemyId(Arg(0)))?.Spec.type ?? -1);
                case BlockKind.EnemyTypeName:
                    int named = Array.IndexOf(PythonTreeCompiler.EnemyTypes, block.Value);
                    if (named < 0) throw new FormatException("알 수 없는 적 종류입니다: Enemy." + block.Value);
                    return Val.KindOf(named);
                case BlockKind.Comparison:
                    var left = Arg(0); var right = Arg(1);
                    if (block.Value == "==" || block.Value == "!=")
                    {
                        return Val.Of(Same(left, right) == (block.Value == "=="));
                    }
                    double l = Number(left, allowInfinity: true), r = Number(right, allowInfinity: true);
                    switch (block.Value) { case "<": return Val.Of(l < r); case ">": return Val.Of(l > r); case "<=": return Val.Of(l <= r); case ">=": return Val.Of(l >= r); }
                    break;
            }
            throw new FormatException("실행할 수 없는 값 블록입니다: " + block.Kind);
        }
        static bool Truth(Val value) => value.Kind == ValueKind.Truth || value.Kind == ValueKind.Number ? value.Number != 0 :
            value.Kind == ValueKind.Enemy || value.Kind == ValueKind.Robot ? value.Enemy != 0 :
            value.Kind == ValueKind.List ? value.Items.Count > 0 : value.Kind == ValueKind.Dict && value.Table.Keys.Count > 0;
        static string Receiver(CodeBlock block) => block.Arguments.Count > 0 && block.Arguments[0]?.Kind == BlockKind.Variable ? block.Arguments[0].Value : "리스트";
        static double Number(Val value, bool allowInfinity = false)
        {
            double n = value.Kind == ValueKind.Number || value.Kind == ValueKind.Truth ? value.Number : double.NaN;
            if (double.IsNaN(n) || !allowInfinity && double.IsInfinity(n)) throw new FormatException("유효한 숫자가 필요합니다.");
            return n;
        }
    }
}
