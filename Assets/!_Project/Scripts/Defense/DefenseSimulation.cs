using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    /// <summary>Unity 화면과 분리된 전투 규칙입니다. 필요한 설정은 생성 시 전달받습니다.</summary>
    public sealed class DefenseSimulation
    {
        public DefenseTestOptions Options => Setup.TestOptions;
        public const double TickDuration = 1.0 / 60;
        public readonly BattleSetup Setup;
        public readonly List<RobotState> Robots = new List<RobotState>();
        readonly List<RobotState> tickRobots = new List<RobotState>();
        readonly List<ProjectileState> due = new List<ProjectileState>();
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public readonly List<ProjectileState> Projectiles = new List<ProjectileState>();
        // 근접 베기: 휘두른 검이 정면을 지날 때(공격 간격의 RobotSpec.strikeShare 뒤) 피해를 줍니다(기획: 이펙트·애니메이션과 같은 순간).
        // inRing: the enemies inside the ring when the swing started; the strike hits only those still inside when it lands.
        readonly List<(double at, RobotState bot, float damage, List<EnemyState> inRing)> strikes = new List<(double, RobotState, float, List<EnemyState>)>();
        readonly List<(double at, RobotState bot, float damage, List<EnemyState> inRing)> dueStrikes = new List<(double, RobotState, float, List<EnemyState>)>();
        public readonly List<CombatEvent> Events = new List<CombatEvent>();
        public BattlePhase Phase { get; private set; } = BattlePhase.Ready;
        public double Time { get; private set; }
        public int WaveIndex { get; private set; }
        public int BaseHealth { get; private set; }
        public int Coins { get; private set; }
        public int UpgradeCost => Setup.Progression.upgradeCost;
        // Each level costs more than the last (50, 100, ...), so coins become a choice of which robot to grow.
        public int UpgradeCostFor(RobotState bot) => UpgradeCost * bot.Level;
        public int Kills { get; private set; }
        public int Leaks { get; private set; }
        public int Spawned { get; private set; }
        public int WaveLeaks => Leaks - waveLeaksAtStart;
        // Stage result: DefenseBalanceRules' star thresholds (80%/40% of the core: 12 HP needs 10 and 5). Forgiving on
        // purpose: 1-1 lets one enemy through by design. No rules: the class defaults (tests).
        static readonly DefenseBalanceRules DefaultRules = new DefenseBalanceRules();
        public static int Stars(int core, int max, DefenseBalanceRules rules = null)
        { rules ??= DefaultRules; return core * 100 >= max * rules.threeStarCorePercent ? 3 : core * 100 >= max * rules.twoStarCorePercent ? 2 : 1; }
        public static int CoreForStars(int stars, int max, DefenseBalanceRules rules = null)
        { rules ??= DefaultRules; return stars >= 3 ? (max * rules.threeStarCorePercent + 99) / 100 : stars == 2 ? (max * rules.twoStarCorePercent + 99) / 100 : 1; }
        public int StageStars => Phase == BattlePhase.Victory ? Stars(BaseHealth, Setup.BaseHealth, Setup.RulesConfig) : 0;
        // Read every tick: plain loops, no LINQ (13차 129).
        public int RemainingEnemies
        {
            get
            {
                int left = 0;
                for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Active) left++;
                for (int i = 0; i < currentWave.Length; i++) left += currentWave[i].Count - groupCounts[i];
                return left;
            }
        }
        // The wave being fought, and how many of one enemy type are left in it (on the field plus not spawned yet).
        public IReadOnlyList<SpawnGroup> CurrentWave => currentWave;
        public int RemainingOf(int definition) => Enemies.Count(e => e.Active && e.DefinitionIndex == definition) +
            currentWave.Select((g, i) => g.DefinitionIndex == definition ? g.Count - groupCounts[i] : 0).Sum();
        public bool ObjectiveFailed { get; private set; }
        SpawnGroup[] currentWave, nextWave;
        public IReadOnlyList<SpawnGroup> UpcomingWave => Phase == BattlePhase.Ready ? currentWave :
            nextWave;
        public bool RouteInUse(int route) => currentWave.Any(g => g.RouteIndex == route) || Enemies.Any(e => e.Active && e.RouteIndex == route);
        public readonly Dictionary<BlockKind, int> Inventory = new Dictionary<BlockKind, int>();
        public readonly Dictionary<RobotRole, int> RobotInventory = new Dictionary<RobotRole, int>();
        public int RobotAvailable(RobotRole role) => !Setup.PlayerFlow || (!Options.inventoryLimit && RobotUnlocked(role)) ? int.MaxValue : RobotInventory.TryGetValue(role, out int count) ? count : 0;
        public bool RobotUnlocked(RobotRole role) => !Setup.PlayerFlow || Options.unlockAll || RobotInventory.ContainsKey(role);
        public bool LoopsUnlocked => Setup.PlayerFlow && (Inventory.ContainsKey(BlockKind.For) || Inventory.ContainsKey(BlockKind.While));
        // The robot's Play button: calls its function during a battle unless it is still running (play feedback 4).
        public bool CanInvoke(int robotId) => Phase == BattlePhase.Running && FindRobot(robotId)?.Program is DefenseProgram p && !p.IsEmpty && !p.Running && p.Fault == null;
        public bool Invoke(int robotId) { if (!CanInvoke(robotId) || !FindRobot(robotId).Program.Invoke()) return false; Log("invoke", robotId); return true; }
        // Every input from outside, by tick from the session's start (PlayLog: the F1 dialogue jump replays them).
        public readonly List<PlayInput> Inputs = new List<PlayInput>();
        public long Ticks { get; private set; }
        PlayInput Log(string kind, int id = 0, int index = 0, int count = 0)
        { var input = new PlayInput { tick = Ticks, kind = kind, id = id, index = index, count = count }; Inputs.Add(input); return input; }
        // The robot's function runs as `for i in range(5): f()` per wave; once loops unlock it runs once, so the player writes the loop.
        public void GrantRobot(RobotRole role, int count) { AddRobots(role, count); Log("robot", 0, (int)role, count); }
        public void GrantBlock(BlockKind kind, int count) { AddBlocks(kind, count); Log("block", 0, (int)kind, count); }
        void AddRobots(RobotRole role, int count)
        {
            if (!Enum.IsDefined(typeof(RobotRole), role) || count < 1) throw new ArgumentException("아이템 종류와 양수 수량을 확인하세요.");
            AddStock(RobotInventory, role, count);
            Emit("robot-granted",0,(int)role,Vector2.Zero,count);
        }
        void AddBlocks(BlockKind kind, int count)
        {
            if (!DefenseProgression.Consumes(kind) || count < 1) throw new ArgumentException("아이템 종류와 양수 수량을 확인하세요.");
            kind = DefenseProgression.Canonical(kind); AddStock(Inventory, kind, count);
            Emit("block-granted",0,(int)kind,Vector2.Zero,count);
        }
        static void AddStock<T>(Dictionary<T, int> stock, T key, int count) { stock.TryGetValue(key, out int old); stock[key] = checked(old + count); }
        // Enum.IsDefined boxes its argument; this runs per robot per tick.
        static readonly bool[] Actions = Enumerable.Range(0, ((RobotAction[])Enum.GetValues(typeof(RobotAction))).Max(a => (int)a) + 1)
            .Select(i => Enum.IsDefined(typeof(RobotAction), i)).ToArray();
        static bool IsAction(RobotAction action) => (int)action >= 0 && (int)action < Actions.Length && Actions[(int)action];
        public RobotState FindRobot(int id) { for (int i = 0; i < Robots.Count; i++) if (Robots[i].Id == id) return Robots[i]; return null; }
        bool AnyEnemyWithin(Vector2 at, float range)
        {
            float r2 = range * range;
            for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Active && Vector2.DistanceSquared(Enemies[i].Position, at) <= r2) return true;
            return false;
        }
        public int Owned(BlockKind kind) => Inventory.TryGetValue(DefenseProgression.Canonical(kind), out int count) ? count : 0;
        public readonly List<BlockKind> PendingDrops = new List<BlockKind>();
        public readonly List<RobotRole> PendingRobots = new List<RobotRole>();
        public int KillCoins => Lesson != null && Lesson.coinsPerKill >= 0 ? Lesson.coinsPerKill : Setup.Progression.coinsPerKill;
        public bool CanPrepare => Phase == BattlePhase.Ready;
        public WaveLesson Lesson => Setup.Progression.lessons.FirstOrDefault(l => l.stage == Setup.Stage && l.wave == WaveIndex + 1);
        public bool CanAdvanceStage => Phase == BattlePhase.Victory && Setup.Stage > 0 && Setup.Stage < (Setup.CampaignWaves?.Length ?? DefenseCurriculum.StageCount);
        public bool CanEdit => !Setup.PlayerFlow ? Phase != BattlePhase.Victory && Phase != BattlePhase.Defeat : CanPrepare;
        public int Available(BlockKind kind, int editingRobot = 0)
        {
            kind = DefenseProgression.Canonical(kind);
            if (Setup.PlayerFlow && DefenseProgression.Consumes(kind) && !Options.unlockAll && !Inventory.ContainsKey(kind)) return 0;
            if (!Setup.PlayerFlow || !Options.inventoryLimit || !DefenseProgression.Consumes(kind)) return int.MaxValue;
            int owned = Owned(kind);
            // 편집 중인 로봇의 기존 예약은 제외하고, 다른 로봇이 사용하는 수량만 차감합니다.
            int used = Robots.Where(r => r.Id != editingRobot).Sum(r => Reserved(r, kind));
            return owned - used;
        }
        // A robot holds the blocks of its applied code, or of its unfinished code in the editor when that has more
        // (a syntax error empties the applied code, but the blocks stay in that robot's editor).
        readonly Dictionary<int, Dictionary<BlockKind, int>> drafts = new Dictionary<int, Dictionary<BlockKind, int>>();
        public void SetDraft(int id, IEnumerable<BlockKind> blocks)
        {
            var kinds = blocks.ToArray();
            drafts[id] = kinds.Select(DefenseProgression.Canonical).GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
            Log("draft", id).kinds = kinds;
        }
        public int Reserved(RobotState robot, BlockKind kind) => Math.Max(robot.Program?.BlockCount(kind) ?? 0,
            drafts.TryGetValue(robot.Id, out var draft) && draft.TryGetValue(DefenseProgression.Canonical(kind), out int count) ? count : 0);
        int nextId = 1;
        int[] groupCounts;
        double accumulated, spawnElapsed;
        int waveLeaksAtStart;

        public DefenseSimulation(BattleSetup setup)
        {
            setup.Validate(); Setup = setup; BaseHealth = setup.BaseHealth;
            WaveIndex = setup.InitialWave;
            Coins = setup.PlayerFlow ? setup.Progression.initialCoins : 0;
            if (setup.PlayerFlow) foreach (var stock in setup.Progression.initial)
                AddStock(Inventory, DefenseProgression.Canonical(stock.kind), stock.count);
            if (setup.PlayerFlow) foreach (var stock in setup.Progression.robots)
                AddStock(RobotInventory, stock.role, stock.count);
            // Starting mid-stage (dev tests) also carries the earlier waves of this stage. Stage 1's road keeps its warriors and
            // their blocks: the workshop stages start with their own robots (the user, 2026-11-19: 2-1 opened with three
            // warriors and two shooters in the dock); Stage 3 follows on from Stage 2 on the same map. The previous stage's last
            // clear reward always comes along (GameData/Award.md: 1-5's row is what 2-1 starts with).
            var lessons = setup.Progression.lessons;
            if (setup.PlayerFlow && setup.PreviousStageRewards) foreach (var lesson in lessons.Where(l => l.stage > 1 && l.stage < setup.Stage || l.stage == setup.Stage && l.wave <= setup.InitialWave ||
                l.stage == setup.Stage - 1 && l.wave == lessons.Where(x => x.stage == l.stage).Max(x => x.wave)))
            {
                foreach(var stock in lesson.robots.Concat(lesson.rewardRobots ?? Array.Empty<RobotStock>())) AddRobots(stock.role,stock.count);
                foreach(var stock in lesson.blocks.Concat(lesson.rewards)) AddBlocks(stock.kind,stock.count);
            }
            PrepareWave();
        }
        void PrepareWave()
        {
            currentWave = nextWave ?? ScaledWave(WaveIndex);
            nextWave = WaveIndex + 1 < Setup.Waves.Length ? ScaledWave(WaveIndex + 1) : Array.Empty<SpawnGroup>();
            groupCounts = new int[currentWave.Length]; spawnElapsed = 0;
            waveLeaksAtStart = Leaks; ObjectiveFailed = false;
            foreach (var bot in Robots) { bot.AttacksThisWave = bot.ActionsThisWave = bot.KillsThisWave = bot.LoopTurnsThisWave = 0; bot.DamageThisWave = 0; bot.SawEnemyThisWave = false; }
            if (Setup.PlayerFlow && Lesson != null && Lesson.startCoins >= 0) Coins = Lesson.startCoins;
            if (Setup.PlayerFlow && Lesson != null) GrantSupply(Lesson);
        }
        void GrantSupply(WaveLesson lesson)
        {
            if (!Options.supply) return;
            foreach (var stock in lesson.blocks) AddBlocks(stock.kind, stock.count);
            foreach (var stock in lesson.robots) AddRobots(stock.role, stock.count);
            Emit("supply",lesson.stage,lesson.wave,Vector2.Zero);
        }
        public void ApplyBalance(BattleSetup incoming, RobotSpec[] robots, EnemySpec[] enemies)
        {
            incoming.Validate();
            foreach(var spec in robots)spec.Validate();foreach(var spec in enemies)spec.Validate();
            if(WaveIndex>=incoming.Waves.Length)throw new ArgumentException("현재 웨이브를 삭제할 수 없습니다.");
            if(Robots.Any(r=>r.DefinitionIndex<0||r.DefinitionIndex>=robots.Length||robots[r.DefinitionIndex].role!=r.Spec.role)||Enemies.Any(e=>e.DefinitionIndex<0||e.DefinitionIndex>=enemies.Length))
                throw new ArgumentException("배치된 로봇·적의 정의 목록과 역할은 유지하세요.");
            int authoredGroups=Setup.Waves[WaveIndex].Length;
            var replacement=incoming.Waves[WaveIndex];
            if(replacement.Length!=authoredGroups||replacement.Where((g,i)=>g.DefinitionIndex!=Setup.Waves[WaveIndex][i].DefinitionIndex||g.RouteIndex!=Setup.Waves[WaveIndex][i].RouteIndex).Any())
                throw new ArgumentException("현재 웨이브의 스폰 항목 추가·삭제·경로 변경은 재시작이 필요합니다.");
            Setup.BaseHealth=incoming.BaseHealth;Setup.RobotLimit=incoming.RobotLimit;Setup.RulesConfig=incoming.RulesConfig;
            Setup.Progression=incoming.Progression;Setup.Permissions=incoming.Permissions;Setup.ActionProfiles=incoming.ActionProfiles;
            Setup.Waves=incoming.Waves;Setup.CampaignWaves=incoming.CampaignWaves;
            BaseHealth=Setup.BaseHealth;Projectiles.Clear();strikes.Clear();
            foreach(var bot in Robots)
            {
                bot.Spec=robots[bot.DefinitionIndex].Copy();bot.LevelCap=Setup.RulesConfig.maxLevel;bot.UpgradeFactor=Setup.RulesConfig.upgradeMultiplier;
                bot.Level=Math.Min(bot.Level,bot.LevelCap);bot.Health=bot.Spec.role==RobotRole.Tank?(float?)bot.MaxHealth:null;
                bot.Buffs.Clear();bot.NextAction=Time;
            }
            foreach(var enemy in Enemies){enemy.Spec=Setup.RulesConfig.Scale(enemies[enemy.DefinitionIndex],Setup.Stage,WaveIndex);enemy.Health=enemy.Spec.health;enemy.NextAttack=Time;}
            for(int i=0;i<authoredGroups;i++){currentWave[i].Count=Math.Max(groupCounts[i],replacement[i].Count);currentWave[i].Delay=replacement[i].Delay;currentWave[i].Interval=replacement[i].Interval;}
            foreach(var group in currentWave)group.Enemy=Setup.RulesConfig.Scale(enemies[group.DefinitionIndex],Setup.Stage,WaveIndex);
            nextWave=WaveIndex+1<Setup.Waves.Length?ScaledWave(WaveIndex+1):Array.Empty<SpawnGroup>();
            Emit("balance-applied",0,0,Vector2.Zero);
        }
        // The F2 balance window: a stage-wave's groups (as authored, before the stage's scaling) replaced for this session.
        // The wave takes them now while it is being prepared, and so does any wave after it; one under way takes them for the
        // groups that have not spawned yet (a boss waits for its minions), the rest keep what they began with until the wave
        // starts again; one over keeps them all. True when the battle takes any of them now.
        public bool SetWaveGroups(int stage, int waveIndex, SpawnGroup[] groups)
        {
            if (Setup.CampaignWaves != null) Setup.CampaignWaves[stage - 1][waveIndex] = groups;
            if (stage != Setup.Stage) return false;
            Setup.Waves[waveIndex] = groups;
            if (waveIndex == WaveIndex && Phase == BattlePhase.Ready)
            { currentWave = ScaledWave(waveIndex); groupCounts = new int[currentWave.Length]; return true; }
            if (waveIndex == WaveIndex && (Phase == BattlePhase.Running || Phase == BattlePhase.Paused))
            {
                var scaled = ScaledWave(waveIndex); bool now = false;
                for (int i = 0; i < currentWave.Length && i < scaled.Length; i++)
                    if (groupCounts[i] == 0) { now |= Differs(currentWave[i], scaled[i]); currentWave[i] = scaled[i]; }
                return now;
            }
            if (waveIndex == WaveIndex + 1 && nextWave != null) nextWave = ScaledWave(waveIndex);
            return waveIndex > WaveIndex;
        }
        // Only the edited group changes, so an edit to a group already out does not read as taken now.
        static bool Differs(SpawnGroup a, SpawnGroup b) => a.Count != b.Count || a.Delay != b.Delay || a.Interval != b.Interval ||
            a.Enemy.health != b.Enemy.health || a.Enemy.speed != b.Enemy.speed || a.Enemy.leakDamage != b.Enemy.leakDamage;
        public bool AdvanceStage()
        {
            if (!CanAdvanceStage) return false;
            Setup.Stage++; WaveIndex = 0;
            if (Setup.CampaignWaves != null) Setup.Waves = Setup.CampaignWaves[Setup.Stage-1]; BaseHealth = Setup.BaseHealth;
            nextWave = null;
            PrepareWave(); ResetPrograms(); Phase = BattlePhase.Ready; Log("advance"); return true;
        }
        void ResetPrograms()
        {
            foreach (var bot in Robots)
            {
                // Ready from battle start, so a slow shooter can fire as soon as an enemy comes in range.
                bot.Buffs.Clear(); bot.BlockingEnabled = false; bot.NextAction = Time;
                if (bot.Program != null) bot.Program = new DefenseProgram(bot.Program.Name, bot.Program.CopyBlocks());
            }
        }
        SpawnGroup[] ScaledWave(int waveIndex)
        {
            if (Setup.Stage == 0) return Setup.Waves[waveIndex];
            return Setup.Waves[waveIndex].Select(group =>
            {
                var enemy = Setup.RulesConfig.Scale(group.Enemy, Setup.Stage, waveIndex);
                var scaled = new SpawnGroup { Enemy = enemy, DefinitionIndex = group.DefinitionIndex, RouteIndex = group.RouteIndex,
                    Count = group.Count, Delay = group.Delay, Interval = group.Interval, Boss = group.Boss };
                return scaled;
            }).ToArray();
        }
        public string StartError()
        {
            if (Phase != BattlePhase.Ready) return "준비 단계에서 시작할 수 있습니다.";
            if (Setup.PlayerFlow && Robots.Count == 0) return "로봇을 먼저 배치하세요.";
            return null;
        }
        // Battle start calls every robot's function once, at once (play feedback 4).
        public void Start()
        {
            if (StartError() != null) return;
            Log("start");
            Checkpoint = Capture(); accumulated = 0; Phase = BattlePhase.Running;
            foreach (var bot in Robots) bot.Program?.Invoke();
        }
        // The last wave start; a defeat can go back to it with Restore on a fresh simulation of the same stage and wave.
        public WaveCheckpoint Checkpoint { get; private set; }
        WaveCheckpoint Capture() => new WaveCheckpoint {
            Stage = Setup.Stage, Wave = WaveIndex, Coins = Coins, BaseHealth = BaseHealth, Kills = Kills, Leaks = Leaks,
            RobotStock = new Dictionary<RobotRole, int>(RobotInventory),
            Groups = currentWave.Select(CopyGroup).ToArray(),
            Robots = Robots.Select(r => new RobotSnapshot { Spec = r.Spec.Copy(), DefinitionIndex = r.DefinitionIndex, Position = r.Position,
                Level = r.Level, UpgradeCostPaid = r.UpgradeCostPaid, Health = r.Health, Name = r.Program?.Name, Code = r.Program?.CopyBlocks() }).ToArray() };
        static SpawnGroup CopyGroup(SpawnGroup g) => new SpawnGroup { Enemy = g.Enemy.Copy(), DefinitionIndex = g.DefinitionIndex, RouteIndex = g.RouteIndex,
            Count = g.Count, Delay = g.Delay, Interval = g.Interval, Boss = g.Boss };
        // The attack preview's enemies: the wave's own (type, this wave's scaled and overridden numbers, damage), one from each
        // group in turn up to `count` (each group at least once when count > 1), 1.4 s apart on the preview's one road. No boss
        // wait: a boss behind enemies that walk past would never come and the demo would never end.
        public static SpawnGroup[] PreviewWave(IReadOnlyList<SpawnGroup> wave, int count)
        {
            var groups = wave.OrderBy(g => g.Boss).ThenBy(g => g.Delay).ToArray();
            if (count > 1) count = Math.Max(count, groups.Length);
            var picks = new List<SpawnGroup>();
            for (int round = 0; picks.Count < count && groups.Any(g => g.Count > round); round++)
                foreach (var g in groups) if (g.Count > round && picks.Count < count) picks.Add(g);
            return picks.Select((g, i) => { var one = CopyGroup(g); one.Count = 1; one.Delay = i * 1.4f; one.Interval = 1.4f; one.RouteIndex = 0; one.Boss = false; return one; }).ToArray();
        }
        // 2-3's warrior demo (the if lesson). A strike hits the enemies inside the ring both when the swing starts and when it
        // lands (ResolveStrikes). The demo's one enemy (the robot `offset` off the road, the road from x = startX) walks in
        // halfway through one of the loop's air swings, which lands on it and misses ('허공을 베고'), takes the loop's next
        // swing, and is out before the one after lands ('지나가 버렸어'); the if swings as it enters and fells it with two strikes.
        // ponytail: assumes the loop's first swing at the demo's start (the preview calls the function on its first frame).
        public static void TimeMeleeDemo(SpawnGroup demo, float range, float offset, float startX, float interval, float strike, float damage)
        {
            float mid = strike / 2; // entry, after the air swing started
            float half = (float)Math.Sqrt(range * range - offset * offset);
            // Midway between the guarded swing's second strike (strike + interval after entry) and the loop's third swing
            // landing (2 interval + strike after the air swing started).
            float crossing = (strike + interval + 2 * interval + strike - mid) / 2;
            demo.Count = 1; demo.Enemy.speed = 2 * half / crossing; demo.Enemy.health = 1.5f * damage;
            float walk = (startX - half) / demo.Enemy.speed, enter = mid;
            while (enter < walk) enter += interval;
            demo.Delay = enter - walk;
        }
        public void Restore(WaveCheckpoint checkpoint)
        {
            if (Phase != BattlePhase.Ready || Robots.Count > 0 || checkpoint.Stage != Setup.Stage || checkpoint.Wave != WaveIndex)
                throw new ArgumentException("웨이브 재도전은 같은 웨이브의 빈 정비 단계에서만 할 수 있습니다.");
            int logged = Inputs.Count; // the play log keeps the restore as the session's start, not as its placings (PlayRun.restore)
            foreach (var saved in checkpoint.Robots)
            {
                var bot = Place(saved.Spec, saved.DefinitionIndex, saved.Position, out var error);
                if (bot == null) throw new ArgumentException("로봇을 다시 놓지 못했습니다: " + error);
                bot.Level = saved.Level; bot.UpgradeCostPaid = saved.UpgradeCostPaid; bot.Health = saved.Health;
                if (saved.Code != null) ApplyProgram(bot.Id, saved.Name, saved.Code);
            }
            if (checkpoint.RobotStock != null) { RobotInventory.Clear(); foreach (var stock in checkpoint.RobotStock) RobotInventory[stock.Key] = stock.Value; }
            currentWave = checkpoint.Groups.Select(CopyGroup).ToArray(); groupCounts = new int[currentWave.Length];
            Coins = checkpoint.Coins; BaseHealth = checkpoint.BaseHealth; Kills = checkpoint.Kills;
            Leaks = waveLeaksAtStart = checkpoint.Leaks;
            Inputs.RemoveRange(logged, Inputs.Count - logged);
        }
        public bool ClaimRewards()
        {
            if (Phase != BattlePhase.Reward) return false;
            Log("claim");
            Emit("rewards",0,WaveIndex+1,Vector2.Zero,PendingDrops.Count);
            foreach (var kind in PendingDrops) AddStock(Inventory, kind, 1);
            foreach (var role in PendingRobots) AddStock(RobotInventory, role, 1);
            PendingDrops.Clear(); PendingRobots.Clear(); accumulated = 0;
            if (WaveIndex + 1 == Setup.Waves.Length) { Phase = BattlePhase.Victory; return true; }
            WaveIndex++; PrepareWave();
            ResetPrograms();
            Phase = BattlePhase.Ready; return true;
        }
        public void SetCoins(int value) { if (value < 0) throw new ArgumentException("코인은 0 이상"); Coins = value; Log("coins", 0, 0, value); }
        public void StepOnce()
        {
            if (Phase != BattlePhase.Paused) throw new InvalidOperationException("일시정지 상태 필요");
            Log("step");
            Phase = BattlePhase.Running; Tick();
            if (Phase == BattlePhase.Running) Phase = BattlePhase.Paused;
        }
        public void GrantAll()
        {
            var roles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().ToArray();
            if (roles.Any(r => RobotInventory.TryGetValue(r,out int n) && n > int.MaxValue-10) ||
                DefenseProgression.ItemKinds.Any(k=>Inventory.TryGetValue(k,out int n) && n > int.MaxValue-50)) throw new OverflowException();
            foreach(var r in roles) AddRobots(r,10);
            foreach(var k in DefenseProgression.ItemKinds) AddBlocks(k,50);
            Log("grantall");
        }
        public void TogglePause()
        {
            if (Phase == BattlePhase.Running) Phase = BattlePhase.Paused;
            else if (Phase == BattlePhase.Paused) Phase = BattlePhase.Running;
            else return;
            Log("pause");
        }
        public string DeploymentError(RobotRole role)
        {
            if (Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat) return "전투가 종료되었습니다.";
            if (Setup.PlayerFlow && !CanPrepare) return "로봇 배치는 웨이브 준비 중에만 가능합니다.";
            if (Options.deploymentLimit && Robots.Count >= Setup.RobotLimit) return "설치개수 초과 · 기존 로봇을 회수하세요";
            if (RobotAvailable(role) < 1) return "보유 로봇 없음";
            if (Options.deploymentLimit && Robots.Count(bot => bot.Spec.role == role) >= Setup.RulesConfig.roleLimits[(int)role]) return role + " 설치개수 초과 · 최대 " + Setup.RulesConfig.roleLimits[(int)role] + "대";
            return null;
        }
        // The road is drawn past its walking half-width (border, round corners): others keep this much further off it.
        const float PathClearance = .25f, PlacementLean = .6f;
        public string PlacementError(RobotSpec spec, Vector2 point) => PlacementError(spec, point, null);
        // moving: a placed robot being moved (MoveRobot). It needs no stock or free slot and its own spot does not block it.
        string PlacementError(RobotSpec spec, Vector2 point, RobotState moving)
        {
            string error = moving == null ? DeploymentError(spec.role) : MoveBlocked();
            if (error != null) return error;
            // Checked with most of the body, not all of it (9차 102: placing felt strict): a robot may lean a little past the
            // map's edge, a neighbour, an obstacle or, a tank, the road's edge.
            float r = spec.radius * PlacementLean;
            if (float.IsNaN(point.X) || float.IsNaN(point.Y) || point.X - r < Setup.Min.X || point.X + r > Setup.Max.X ||
                point.Y - r < Setup.Min.Y || point.Y + r > Setup.Max.Y) return "설치 허용 영역 밖입니다.";
            // The map is not a rectangle: the centre and the body's four edges must all be over a floor or road tile.
            if (Setup.Floor.Length > 0 && new[] { Vector2.Zero, new Vector2(r, 0), new Vector2(-r, 0), new Vector2(0, r), new Vector2(0, -r) }
                .Any(offset => !Setup.Floor.Any(tile => tile.Contains(point + offset)))) return "맵 밖에는 설치할 수 없습니다.";
            if (Robots.Any(bot => bot != moving && Vector2.Distance(bot.Position, point) < (bot.Spec.radius + spec.radius) * .9f)) return "다른 로봇과 겹칩니다.";
            if (Setup.Obstacles.Any(area => area.Overlaps(point, r))) return "장애물 또는 목표 시설과 겹칩니다.";
            bool pathFits = Setup.Routes.Any(route => route.Distance(point) + r <= route.HalfWidth);
            // Others keep their whole body off the drawn road (radius is the model's half-width, Defense/Data).
            bool touchesPath = Setup.Routes.Any(route => route.Distance(point) < route.HalfWidth + r + PathClearance);
            if (spec.role == RobotRole.Tank && !pathFits) return "탱커는 몸체가 경로 안에 들어가도록 설치하세요.";
            if (spec.role != RobotRole.Tank && touchesPath) return "적 경로에는 탱커만 설치할 수 있습니다.";
            return null;
        }
        public RobotState Place(RobotSpec spec, int definitionIndex, Vector2 point, out string error)
        {
            spec.Validate(); error = PlacementError(spec, point);
            if (error != null) {Events.Add(new CombatEvent{Kind="placement-failed",Reason=error,Target=definitionIndex,Position=point});return null;}
            var bot = new RobotState { Id = nextId++, DefinitionIndex = definitionIndex, Position = point, Spec = spec.Copy(),
                LevelCap = Setup.RulesConfig.maxLevel, UpgradeFactor = Setup.RulesConfig.upgradeMultiplier,
                Action = spec.NativeAction, Health = spec.role == RobotRole.Tank ? (float?)spec.health : null,
                NextAction = Time };
            if (Setup.PlayerFlow && Options.inventoryLimit) RobotInventory[spec.role]--;
            Robots.Add(bot);
            var input = Log("place", 0, definitionIndex); input.spec = spec.Copy(); input.x = point.X; input.y = point.Y;
            return bot;
        }
        // Recall opens with 1-2, whose start tells about it (1-1's warrior keeps its code through the first lesson).
        public bool RecallUnlocked => !Setup.PlayerFlow || Setup.Stage != 1 || WaveIndex >= 1;
        public bool RemoveRobot(int id)
        {
            if (Setup.PlayerFlow && !CanPrepare || !RecallUnlocked) return false;
            var bot = FindRobot(id);
            if (bot == null) return false;
            if (Setup.PlayerFlow) AddRobots(bot.Spec.role, 1);
            // 인벤토리는 종류별 수량만 보관하므로 강화 비용을 환급하고 Lv.1 로봇으로 돌려줍니다.
            Coins = (int)Math.Min(int.MaxValue, (long)Coins + bot.UpgradeCostPaid);
            Detach(bot); Log("remove", id);
            return true;
        }
        // Moving follows recall's rules: while preparing, from 1-3. The robot keeps its id, level, code, name and health.
        string MoveBlocked() => Setup.PlayerFlow && !CanPrepare ? "로봇 이동은 웨이브 준비 중에만 가능합니다." : !RecallUnlocked ? MoveLocked : null;
        // No stage named: the gate is recall's (RecallUnlocked), wherever that opens.
        public const string MoveLocked = "이동은 회수와 함께 열립니다.";
        public string MoveError(int id, Vector2 point) => FindRobot(id) is RobotState bot ? PlacementError(bot.Spec, point, bot) : "로봇을 선택하세요.";
        public bool MoveRobot(int id, Vector2 point, out string error)
        {
            error = MoveError(id, point);
            if (error != null) return false;
            var bot = FindRobot(id);
            bot.Position = point;
            foreach (var e in Enemies) if (e.BlockedBy == bot.Id) e.BlockedBy = 0;
            var input = Log("move", id); input.x = point.X; input.y = point.Y;
            return true;
        }
        // Level-up opens from Stage 1 wave 2 in the campaign.
        public bool UpgradeUnlocked => !Setup.PlayerFlow || Setup.Stage != 1 || WaveIndex >= 1;
        public string UpgradeError(int id)
        {
            var bot = FindRobot(id);
            if (bot == null) return "로봇을 선택하세요.";
            if (!UpgradeUnlocked) return "레벨업은 1-2부터 열립니다.";
            if (bot.IsMaxLevel) return "최고레벨";
            if (!CanPrepare) return "정비 중에만 레벨업할 수 있습니다.";
            if (Coins < UpgradeCostFor(bot)) return "코인이 부족합니다.";
            return null;
        }
        public bool UpgradeRobot(int id)
        {
            if (UpgradeError(id) != null) return false;
            var bot = FindRobot(id);
            float healthRatio = bot.Health.HasValue ? bot.Health.Value / bot.MaxHealth : 0;
            int cost = UpgradeCostFor(bot);
            Coins -= cost; bot.UpgradeCostPaid += cost; bot.Level++;
            if (bot.Health.HasValue) bot.Health = bot.MaxHealth * healthRatio;
            Log("upgrade", id);
            return true;
        }
        void Detach(RobotState bot)
        {
            Robots.Remove(bot);
            foreach (var r in Robots) r.Buffs.RemoveAll(b => b.Source == bot.Id);
            foreach (var e in Enemies) if (e.BlockedBy == bot.Id) e.BlockedBy = 0;
        }
        public bool DamageRobot(int id, float damage, int source = 0)
        {
            var bot = FindRobot(id);
            if (bot == null || !bot.Health.HasValue || !Rules.Nonnegative(damage) || Phase != BattlePhase.Running) return false;
            Events.Add(new CombatEvent {Kind="robot-damage",Source=source,Target=id,Position=bot.Position,Amount=damage,AppliedAmount=Options.robotDamage?Math.Min(damage,bot.Health.Value):0});
            if (!Options.robotDamage) return true;
            bot.Health = Math.Max(0, bot.Health.Value - damage);
            if (bot.Health <= 0) { Emit("tank-destroyed", 0, id, bot.Position); Detach(bot); }
            return true;
        }
        public void ApplyProgram(int id, string name, IReadOnlyList<CodingGame.BlockCoding.CodeBlock> blocks)
        {
            var bot = FindRobot(id);
            if (bot == null || !CanEdit)
                throw new FormatException("코드를 적용할 로봇이 없거나 전투가 종료되었습니다.");
            var program = new DefenseProgram(name, blocks);
            // 문법과 재고 검증이 모두 끝난 뒤 교체해야 실패 시 이전 코드와 예약 수량이 유지됩니다.
            if (Setup.PlayerFlow) foreach (var group in DefenseProgression.Used(blocks).GroupBy(k => k))
                if (group.Count() > Available(group.Key, id)) throw new FormatException(DefenseProgression.Label(group.Key) + " 블록 수량이 부족합니다. 다른 로봇의 예약 수량도 확인하세요.");
            bot.Program = program; bot.AutoExecute = false;
            var input = Log("program", id); input.name = name; input.code = PlayLog.Flatten(blocks);
            bot.BlockingEnabled = false;
            foreach (var e in Enemies) if (e.BlockedBy == id) e.BlockedBy = 0;
        }
        public void RestoreAutomatic(int id)
        {
            if (Setup.PlayerFlow) return;
            var bot = FindRobot(id);
            if (bot == null) return;
            bot.Program = null; bot.AutoExecute = true; bot.Action = bot.Spec.NativeAction;
        }
        public ActionCompatibility Compatibility(RobotState bot, RobotAction action) => Rules.Compatibility(bot.Spec.role, action, Setup.Permissions);
        RobotSpec Profile(RobotState bot, RobotAction action) => Compatibility(bot, action) == ActionCompatibility.Reduced ? Setup.ActionProfiles.Single(p => p.NativeAction == action) : bot.Spec;
        float Strength(RobotState bot, RobotAction action)
        {
            var compatibility = Compatibility(bot, action);
            if (compatibility == ActionCompatibility.Native) return 1;
            if (compatibility != ActionCompatibility.Reduced) return 0;
            return !Rules.NeedsPermission(action) ? Setup.RulesConfig.reducedStrength : Setup.Permissions.First(p => p.role == bot.Spec.role && p.action == action).strength;
        }
        public float ActionDamage(RobotState bot, RobotAction action) => Profile(bot, action).damage * bot.DamageMultiplier * Strength(bot, action);
        // Distance does not change damage: the shooter's inverse-distance bonus was dropped (design decision 2026-11-18).
        public float ActionDamage(RobotState bot, RobotAction action, float distance) => ActionDamage(bot, action);
        public float ActionInterval(RobotState bot, RobotAction action) => Profile(bot, action).interval * (bot.Interval / bot.Spec.interval);
        public ActionResult RequestAction(int robotId, RobotAction action, int targetId = 0)
        {
            var bot = FindRobot(robotId);
            if (bot == null || Phase != BattlePhase.Running || !IsAction(action)) return ActionResult.Unavailable;
            var compatibility = Compatibility(bot, action);
            if (compatibility == ActionCompatibility.Ineffective) return bot.LastResult = ActionResult.Ineffective;
            if (compatibility == ActionCompatibility.Undecided) return bot.LastResult = ActionResult.Undecided;
            // block()에 도달하면 즉시 몸으로 저지합니다. 스턴과 피해만 공통 재사용 시간을 따릅니다.
            if (action == RobotAction.Block && bot.Spec.role == RobotRole.Tank) bot.BlockingEnabled = true;
            bool breaking = action == RobotAction.BreakShield; // its own cooldown, so it can go right before a shot
            if (Time + .000001 < (breaking ? bot.NextShieldBreak : bot.NextAction)) return bot.LastResult = ActionResult.CoolingDown;
            float strength = Strength(bot, action);
            var profile = Profile(bot, action);
            if (action == RobotAction.Buff)
            {
                if (!Buff(bot, profile, strength)) return bot.LastResult = ActionResult.NoTarget;
            }
            else
            {
                var targets = Enemies.Where(e => e.Active && Vector2.DistanceSquared(e.Position, bot.Position) <= bot.Range * bot.Range).ToList();
                var target = targetId == 0 ? Rules.Nearest(targets, bot.Position) : targets.FirstOrDefault(e => e.Id == targetId);
                if (target == null && action != RobotAction.Slash) return bot.LastResult = ActionResult.NoTarget;
                switch (action)
                {
                    case RobotAction.Block: Stun(bot, profile, strength, targets, target); break;
                    case RobotAction.Boom: Launch(bot, profile, strength, target); break;
                    case RobotAction.Slash: Slash(bot, target, targets); break;
                    case RobotAction.BreakShield: BreakShield(bot, target); break;
                    default: Shoot(bot, action, profile, strength, target); break;
                }
            }
            if (breaking) bot.NextShieldBreak = Time + bot.Spec.shieldBreakInterval; else bot.NextAction = Time + ActionInterval(bot, action);
            bot.Executions++;
            bool whiff = action == RobotAction.Slash && !Enemies.Any(e => e.Active && Vector2.DistanceSquared(e.Position, bot.Position) <= bot.Range * bot.Range);
            if (!whiff) { bot.ActionsThisWave++; if (action != RobotAction.Buff) bot.AttacksThisWave++; }
            return bot.LastResult = ActionResult.Executed;
        }
        bool Buff(RobotState bot, RobotSpec profile, float strength)
        {
            var targets = Robots.Where(r => r.Id != bot.Id && Vector2.DistanceSquared(r.Position, bot.Position) <= bot.Range * bot.Range).ToList();
            if (targets.Count == 0) return false;
            float buffStrength = strength * bot.LevelMultiplier;
            foreach (var r in targets)
            {
                r.Buffs.RemoveAll(b => b.Source == bot.Id);
                r.Buffs.Add(new BuffState { Source = bot.Id, Until = Time + profile.duration, Damage = 1 + (profile.buffDamage - 1) * buffStrength,
                    Range = 1 + (profile.buffRange - 1) * buffStrength, Interval = Math.Max(.01f, 1 - (1 - profile.buffInterval) * buffStrength) });
            }
            Emit("buff", bot.Id, 0, bot.Position, 0, bot.Range);
            return true;
        }
        // break_shield(): the shield is gone at once, then a little damage to health (a laser like the shot's, no flight).
        void BreakShield(RobotState bot, EnemyState target)
        {
            target.Shield = 0;
            Emit("shield-break", bot.Id, target.Id, target.Position);
            Hit(target, bot.Spec.shieldBreakDamage * bot.DamageMultiplier, bot.Id);
        }
        // Healer and Shielder pulse on their own clock on every other active enemy within their radius; a shield does not
        // stack, a new one tops it up to the amount.
        void Pulse(EnemyState e)
        {
            bool heal = e.Spec.type == EnemyType.Healer;
            if (!heal && e.Spec.type != EnemyType.Shielder || Time + .000001 < e.NextAbility) return;
            e.NextAbility = Time + e.Spec.abilityInterval; e.LastAbility = Time;
            float r2 = e.Spec.abilityRadius * e.Spec.abilityRadius;
            foreach (var other in Enemies)
            {
                if (other == e || !other.Active || Vector2.DistanceSquared(other.Position, e.Position) > r2) continue;
                if (heal) other.Health = Math.Min(other.Spec.health, other.Health + e.Spec.abilityAmount);
                else other.Shield = Math.Max(other.Shield, e.Spec.abilityAmount);
            }
            Emit(heal ? "heal" : "shield", e.Id, 0, e.Position, e.Spec.abilityAmount, e.Spec.abilityRadius);
        }
        // How far an enemy still has to walk to the core: get_enemies() lists the nearest to the core (the front) first.
        public float RemainingPath(EnemyState e)
        {
            var path = Setup.Routes[e.RouteIndex].Points; var at = e.Position; float distance = 0;
            for (int i = e.NextPoint; i < path.Length; i++) { distance += Vector2.Distance(at, path[i]); at = path[i]; }
            return distance;
        }
        void Stun(RobotState bot, RobotSpec profile, float strength, List<EnemyState> targets, EnemyState target)
        {
            foreach (var e in targets) { var effect = Effect(e, bot.Id); effect.StunUntil = Math.Max(effect.StunUntil, Time + profile.duration * strength); }
            Hit(target, ActionDamage(bot, RobotAction.Block, Vector2.Distance(bot.Position, target.Position)), bot.Id);
            Emit("stun", bot.Id, target.Id, bot.Position, 0, bot.Range);
        }
        void Launch(RobotState bot, RobotSpec profile, float strength, EnemyState target)
        {
            // 발사 시점의 위치와 피해를 저장합니다. 투사체는 움직이는 적을 추적하지 않습니다.
            Projectiles.Add(new ProjectileState { Id = nextId++, Source = bot.Id, Origin = bot.Position, Destination = target.Position,
                LaunchedAt = Time, ImpactAt = Time + Math.Max(TickDuration, Vector2.Distance(bot.Position, target.Position) / profile.projectileSpeed),
                Damage = profile.damage * bot.DamageMultiplier * strength, Radius = profile.effectRadius });
            Emit("launch", bot.Id, target.Id, target.Position);
        }
        void Slash(RobotState bot, EnemyState target, List<EnemyState> inRing)
        {
            target = target ?? Rules.Nearest(Enemies.Where(e => e.Active), bot.Position);
            var direction = target == null ? Vector2.UnitY : target.Position - bot.Position;
            if (direction.LengthSquared() < .000001f) direction = Vector2.UnitY;
            direction = Vector2.Normalize(direction);
            double strike = ActionInterval(bot, RobotAction.Slash) * bot.Spec.strikeShare;
            Events.Add(new CombatEvent { Kind = "slash", Source = bot.Id, Target = target?.Id ?? 0,
                Origin = bot.Position, Position = bot.Position + direction, Radius = bot.Range, Seconds = (float)strike });
            // A spin all the way round; who it hits is decided when the sword gets there (ResolveStrikes). The event still
            // faces the nearest enemy, where the spin starts.
            strikes.Add((Time + strike, bot, ActionDamage(bot, RobotAction.Slash), inRing));
        }
        // A robot recalled before its sword gets there does not hit. The strike hits the enemies inside the range ring both
        // when the swing started and when it lands: landing alone let a blind `while True: slash()` at an empty ring cut
        // enemies that walked in during the .64 s swing, and it cleared 2-3 without the if the stage teaches (the user,
        // 2026-11-20). Landing is still checked, so one that walked out of the ring is not hit (2-3 measurement, 2026-11-19).
        void ResolveStrikes()
        {
            dueStrikes.Clear();
            for (int i = 0; i < strikes.Count; i++) if (Time + 1e-8 >= strikes[i].at) dueStrikes.Add(strikes[i]);
            foreach (var strike in dueStrikes)
            {
                strikes.Remove(strike);
                if (!Robots.Contains(strike.bot)) continue;
                float range = strike.bot.Range;
                foreach (var enemy in strike.inRing.Where(e => e.Active && Vector2.DistanceSquared(e.Position, strike.bot.Position) <= range * range).ToArray())
                    Hit(enemy, strike.damage, strike.bot.Id, meleeSource: strike.bot);
            }
        }
        void Shoot(RobotState bot, RobotAction action, RobotSpec profile, float strength, EnemyState target)
        {
            float distance = Vector2.Distance(bot.Position, target.Position), damage = ActionDamage(bot, action, distance);
            if (action == RobotAction.Attack)
            {
                // 사격형 탄은 날아가서 도착할 때 피해를 줍니다(기획). 도착 시점에 같은 적을 맞힙니다.
                double flight = Math.Max(TickDuration, distance / profile.projectileSpeed);
                Projectiles.Add(new ProjectileState { Id = nextId++, Source = bot.Id, Target = target.Id, Origin = bot.Position, Destination = target.Position,
                    LaunchedAt = Time, ImpactAt = Time + flight, Damage = damage });
                Events.Add(new CombatEvent { Kind = "shot", Source = bot.Id, Target = target.Id, Position = target.Position, Seconds = (float)flight });
                return;
            }
            Hit(target, damage, bot.Id);
            if (action == RobotAction.Slow && target.Active)
            {
                var effect = Effect(target, bot.Id); effect.SlowUntil = Time + profile.duration;
                effect.Multiplier = 1 - (1 - profile.slowMultiplier) * strength;
            }
        }
        MovementEffect Effect(EnemyState e, int source)
        {
            var effect = e.Effects.Find(f => f.Source == source);
            if (effect == null) { effect = new MovementEffect { Source = source }; e.Effects.Add(effect); }
            return effect;
        }
        void Emit(string kind, int source, int target, Vector2 point, float amount = 0, float radius = 0)
            => Events.Add(new CombatEvent { Kind = kind, Source = source, Target = target, Position = point, Amount = amount, Radius = radius });
        void Hit(EnemyState e, float damage, int source, bool projectile = false, RobotState meleeSource = null)
        {
            if (!e.Active) return;
            if (!Options.attackDamage) { Emit("damage-suppressed", source, e.Id, e.Position, damage); damage = 0; }
            float absorbed = Math.Min(e.Shield, damage); e.Shield -= absorbed; damage -= absorbed; // a shield takes damage first
            float applied = Math.Min(e.Health, damage);
            e.Health = Math.Max(0, e.Health - damage);
            var attacker = FindRobot(source);
            if (attacker != null) { attacker.DamageThisWave += applied; if (e.Health <= 0) attacker.KillsThisWave++; }
            Events.Add(new CombatEvent { Kind = projectile ? "projectile-hit" : "hit", Source = source,
                Target = e.Id, Position = e.Position, Amount = damage, AppliedAmount=applied, Melee = meleeSource != null,
                Origin = meleeSource == null ? default : meleeSource.Position });
            if (e.Health <= 0)
            {
                e.Active = false; e.BlockedBy = 0; Kills++; Emit("killed", source, e.Id, e.Position);
                if (Setup.PlayerFlow)
                {
                    Coins = (int)Math.Min(int.MaxValue, (long)Coins + KillCoins);
                    if (Options.drops && Lesson == null)
                    {
                        var drop = NeededDrop();
                        PendingDrops.Add(DefenseProgression.Canonical(drop)); Emit("drop", 0, e.Id, e.Position, (int)drop);
                    }
                }
            }
        }
        BlockKind NeededDrop()
        {
            var pool = Setup.Progression.drops.Where(d => d.firstWave <= WaveIndex + 1).ToArray();
            int OwnedOrPending(BlockKind kind) => Owned(kind) + PendingDrops.Count(k => DefenseProgression.Canonical(k) == DefenseProgression.Canonical(kind));
            BlockKind ActionBlock(RobotRole role) => DefenseProgression.ActionBlock(Rules.NativeAction(role));
            foreach (var stock in Setup.Progression.robots.OrderBy(r => OwnedOrPending(ActionBlock(r.role)) - r.count))
            {
                var kind = ActionBlock(stock.role);
                if (OwnedOrPending(kind) < stock.count && pool.Any(d => DefenseProgression.Canonical(d.kind) == kind)) return kind;
            }
            return pool.OrderBy(d => OwnedOrPending(d.kind)).ThenByDescending(d => d.weight).First().kind;
        }
        public void Advance(double elapsed)
        {
            if (Phase != BattlePhase.Running || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) return;
            accumulated += elapsed;
            // 느린 프레임의 미처리 시간을 남겨 스폰과 이동 계산이 생략되지 않게 합니다.
            int budget = 600;
            while (accumulated + 1e-9 >= TickDuration && Phase == BattlePhase.Running && budget-- > 0)
            { accumulated -= TickDuration; Tick(); }
        }
        void ResolveProjectiles()
        {
            due.Clear();
            for (int i = 0; i < Projectiles.Count; i++) if (Time + 1e-8 >= Projectiles[i].ImpactAt) due.Add(Projectiles[i]);
            foreach (var shot in due)
            {
                Projectiles.Remove(shot);
                if (shot.Target != 0)
                {
                    var enemy = Enemies.Find(e => e.Id == shot.Target);
                    if (enemy != null) Hit(enemy, shot.Damage, shot.Source, true);
                    continue;
                }
                foreach (var enemy in Enemies.Where(e => e.Active && Vector2.DistanceSquared(e.Position, shot.Destination) <= shot.Radius * shot.Radius).ToArray())
                    Hit(enemy, shot.Damage, shot.Source, true);
                Emit("blast", shot.Source, 0, shot.Destination, 0, shot.Radius);
            }
        }
        void Tick()
        {
            const float dt = (float)TickDuration;
            Time += TickDuration; Ticks++;
            if (Options.enemySpawn) { spawnElapsed += TickDuration; Spawn(); }
            foreach (var bot in Robots)
                for (int i = bot.Buffs.Count - 1; i >= 0; i--) if (bot.Buffs[i].Until <= Time) bot.Buffs.RemoveAt(i);
            foreach (var e in Enemies)
            {
                for (int i = e.Effects.Count - 1; i >= 0; i--) if (e.Effects[i].SlowUntil <= Time && e.Effects[i].StunUntil <= Time) e.Effects.RemoveAt(i);
                if (e.BlockedBy != 0 && FindRobot(e.BlockedBy) == null) e.BlockedBy = 0;
                if (e.Active && e.BlockedBy == 0) TryBlock(e, e.Position, e.Position, out _);
                if (e.Active && !e.Stunned(Time)) Pulse(e);
            }
            foreach (var bot in Robots)
                if (!bot.SawEnemyThisWave) bot.SawEnemyThisWave = AnyEnemyWithin(bot.Position, bot.Range);
            // A copy, as before: a robot's code may recall robots. Reused, so no array per tick.
            tickRobots.Clear(); tickRobots.AddRange(Robots);
            if (Options.robotCode) foreach (var bot in tickRobots)
                if (bot.Program != null) bot.Program.Tick(this, bot);
                else if (bot.AutoExecute) RequestAction(bot.Id, bot.Action);
            foreach (var e in Enemies)
            {
                if (!e.Active || e.Stunned(Time)) continue;
                if (e.BlockedBy != 0)
                {
                    if (Options.enemyAttack && Time + .000001 >= e.NextAttack)
                    { DamageRobot(e.BlockedBy, e.Spec.damage,e.Id); e.NextAttack = Time + e.Spec.attackInterval; }
                }
                if (Options.enemyMovement && e.BlockedBy == 0) Move(e, e.Speed(Time) * dt);
            }
            if (BaseHealth <= 0 || ObjectiveFailed) { Enemies.RemoveAll(e => !e.Active); Projectiles.Clear(); strikes.Clear(); Phase = BattlePhase.Defeat; return; }
            ResolveProjectiles(); ResolveStrikes();
            Enemies.RemoveAll(e => !e.Active);
            // No time limit (the user, 2026-11-19): the wave ends once every enemy of it has been killed or reached the core.
            if (RemainingEnemies == 0) CompleteWave();
        }
        void CompleteWave()
        {
            Projectiles.Clear(); strikes.Clear();
            Emit("wave-completed", 0, WaveIndex, Vector2.Zero);
            if (Setup.PlayerFlow)
            {
                if (Options.rewards && Lesson != null) foreach (var stock in Lesson.rewards)
                    for (int i = 0; i < stock.count; i++) PendingDrops.Add(DefenseProgression.Canonical(stock.kind));
                if (Options.rewards && Lesson?.rewardRobots != null) foreach (var stock in Lesson.rewardRobots)
                    for (int i = 0; i < stock.count; i++) PendingRobots.Add(stock.role);
                Phase = BattlePhase.Reward; accumulated = 0;
            }
            else if (WaveIndex + 1 == Setup.Waves.Length) Phase = BattlePhase.Victory;
            else { WaveIndex++; PrepareWave(); }
        }
        void Spawn()
        {
            var wave = currentWave;
            for (int i = 0; i < wave.Length; i++)
            {
                var g = wave[i];
                // A boss waits until every ordinary enemy in this wave has been spawned and defeated.
                if (g.Boss && (wave.Select((other, index) => !other.Boss && groupCounts[index] < other.Count).Any(waiting => waiting) ||
                    Enemies.Any(enemy => enemy.Active && !enemy.Boss) || Leaks > waveLeaksAtStart)) continue;
                while (groupCounts[i] < g.Count && spawnElapsed + .000001 >= g.Delay + groupCounts[i] * g.Interval)
                {
                    var spec = g.Enemy.Copy();
                    Enemies.Add(new EnemyState { Id = nextId++, DefinitionIndex = g.DefinitionIndex, Spec = spec, Health = spec.health, Boss = g.Boss,
                        Position = Setup.Routes[g.RouteIndex].Points[0], RouteIndex = g.RouteIndex, NextAttack = Time + spec.attackInterval,
                        NextAbility = Time + spec.abilityInterval * .5 });
                    groupCounts[i]++; Spawned++;
                }
            }
        }
        bool TryBlock(EnemyState e, Vector2 start, Vector2 end, out Vector2 point)
        {
            point = end; RobotState chosen = null; float nearest = float.MaxValue;
            foreach (var bot in Robots)
            {
                if (bot.Spec.role != RobotRole.Tank || !bot.BlockingEnabled) continue;
                // 이번 이동으로 닿는 탱커에 대해서만 저지 인원수를 계산합니다.
                if (!Rules.CircleEntry(start, end, bot.Position, bot.Spec.blockRadius, out float t) || t >= nearest) continue;
                if (Enemies.Count(other => other.Active && other.BlockedBy == bot.Id) >= bot.Spec.blockCapacity) continue;
                nearest = t; chosen = bot;
            }
            if (chosen == null) return false;
            e.BlockedBy = chosen.Id; e.NextAttack = Time + e.Spec.attackInterval;
            point = Vector2.Lerp(start, end, nearest); return true;
        }
        void Move(EnemyState e, float distance)
        {
            var path = Setup.Routes[e.RouteIndex].Points;
            while (distance > 0 && e.NextPoint < path.Length)
            {
                var goal = path[e.NextPoint]; var delta = goal - e.Position; float remaining = delta.Length();
                float travel = Math.Min(distance, remaining);
                var end = remaining < .00001f ? goal : e.Position + delta * (travel / remaining);
                if (TryBlock(e, e.Position, end, out var stopped)) { e.Position = stopped; return; }
                e.Position = end; distance -= travel;
                if (remaining <= travel + .00001f) e.NextPoint++; else break;
            }
            if (e.NextPoint == path.Length)
            {
                e.Active = false; e.BlockedBy = 0; Leaks++; if (Options.baseDamage) BaseHealth = Math.Max(0, BaseHealth - e.Spec.leakDamage);
                if (Setup.PlayerFlow && !e.Boss && currentWave.Any(g => g.Boss)) ObjectiveFailed = true; // a boss wave with a leaked minion: the boss never comes
                Emit("leaked", e.Id, 0, e.Position, e.Spec.leakDamage);
            }
        }
    }
}
