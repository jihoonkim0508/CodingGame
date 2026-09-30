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
        public const double TickDuration = 1.0 / 60;
        public readonly BattleSetup Setup;
        public readonly List<RobotState> Robots = new List<RobotState>();
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public readonly List<ProjectileState> Projectiles = new List<ProjectileState>();
        public readonly List<CombatEvent> Events = new List<CombatEvent>();
        public BattlePhase Phase { get; private set; } = BattlePhase.Ready;
        public double Time { get; private set; }
        public int WaveIndex { get; private set; }
        public int BaseHealth { get; private set; }
        public int Coins { get; private set; }
        public int UpgradeCost => Setup.Progression.upgradeCost;
        public int Kills { get; private set; }
        public int Leaks { get; private set; }
        public int Spawned { get; private set; }
        public int Total => Setup.Waves.Sum(w => w.Sum(g => g.Count));
        public double WaveRemaining
        {
            get
            {
                if (Setup.WaveSeconds <= 0) return double.PositiveInfinity;
                double remaining = Setup.WaveSeconds - (Phase == BattlePhase.Ready ? 0 : Time - waveStart);
                // 종료 판정과 같은 오차 범위를 사용해 종료된 웨이브에 1초가 남아 보이지 않게 합니다.
                return remaining <= 1e-8 ? 0 : remaining;
            }
        }
        public int RemainingEnemies => Enemies.Count(e => e.Active) + currentWave.Select((g, i) => g.Count - groupCounts[i]).Sum();
        public int LastSurvivors { get; private set; }
        public bool LastWaveTimedOut { get; private set; }
        public readonly List<int> WaveSurvivors = new List<int>();
        readonly List<SpawnGroup> carry = new List<SpawnGroup>();
        SpawnGroup[] currentWave, nextWave;
        public IReadOnlyList<SpawnGroup> UpcomingWave => Phase == BattlePhase.Ready ? currentWave :
            nextWave;
        public readonly Dictionary<BlockKind, int> Inventory = new Dictionary<BlockKind, int>();
        public readonly Dictionary<RobotRole, int> RobotInventory = new Dictionary<RobotRole, int>();
        public int RobotAvailable(RobotRole role) => !Setup.PlayerFlow ? int.MaxValue : RobotInventory.TryGetValue(role, out int count) ? count : 0;
        public void GrantRobot(RobotRole role, int count)
        {
            if (!Enum.IsDefined(typeof(RobotRole), role) || count < 1) throw new ArgumentException("아이템 종류와 양수 수량을 확인하세요.");
            RobotInventory.TryGetValue(role, out int old); RobotInventory[role] = checked(old + count);
        }
        public void GrantBlock(BlockKind kind, int count)
        {
            if (!DefenseProgression.Consumes(kind) || count < 1) throw new ArgumentException("아이템 종류와 양수 수량을 확인하세요.");
            kind = DefenseProgression.Canonical(kind); Inventory.TryGetValue(kind, out int old); Inventory[kind] = checked(old + count);
        }
        public readonly List<BlockKind> PendingDrops = new List<BlockKind>();
        public bool CanPrepare => Phase == BattlePhase.Ready;
        public bool CanEdit => !Setup.PlayerFlow ? Phase != BattlePhase.Victory && Phase != BattlePhase.Defeat : CanPrepare;
        public int Available(BlockKind kind, int editingRobot = 0)
        {
            kind = DefenseProgression.Canonical(kind);
            if (!Setup.PlayerFlow || !DefenseProgression.Consumes(kind)) return int.MaxValue;
            Inventory.TryGetValue(kind, out int owned);
            // 편집 중인 로봇의 기존 예약은 제외하고, 다른 로봇이 사용하는 수량만 차감합니다.
            int used = Robots.Where(r => r.Id != editingRobot && r.Program != null).Sum(r => r.Program.BlockCount(kind));
            return owned - used;
        }
        int nextId = 1;
        int[] groupCounts;
        double waveStart, accumulated;

        public DefenseSimulation(BattleSetup setup)
        {
            setup.Validate(); Setup = setup; BaseHealth = setup.BaseHealth;
            Coins = setup.PlayerFlow ? setup.Progression.initialCoins : 0;
            if (setup.PlayerFlow) foreach (var stock in setup.Progression.initial)
            { var kind = DefenseProgression.Canonical(stock.kind); Inventory.TryGetValue(kind, out int old); Inventory[kind] = old + stock.count; }
            if (setup.PlayerFlow) foreach (var stock in setup.Progression.robots)
            { RobotInventory.TryGetValue(stock.role, out int old); RobotInventory[stock.role] = old + stock.count; }
            PrepareWave();
        }
        void PrepareWave()
        {
            currentWave = nextWave ?? ScaledWave(WaveIndex);
            nextWave = WaveIndex + 1 < Setup.Waves.Length ? ScaledWave(WaveIndex + 1) : Array.Empty<SpawnGroup>();
            carry.Clear(); groupCounts = new int[currentWave.Length]; waveStart = Time;
        }
        SpawnGroup[] ScaledWave(int waveIndex)
        {
            if (Setup.Stage == 0) return Setup.Waves[waveIndex];
            int progress = (Setup.Stage - 1) * 5 + waveIndex;
            // 5웨이브 보스 배율은 기획서에서 미정이므로 별도 보정을 적용하지 않습니다.
            float waveMultiplier = waveIndex < 4 ? new[] { .9f, 1f, 1.05f, 1.15f }[waveIndex] : 1f;
            return Setup.Waves[waveIndex].Select(group =>
            {
                var enemy = group.Enemy.Copy();
                float typeHealth = enemy.type == EnemyType.Fast ? .75f : enemy.type == EnemyType.Tank ? 1.5f : 1f;
                float typeSpeed = enemy.type == EnemyType.Fast ? 1.3f : enemy.type == EnemyType.Tank ? .75f : 1f;
                enemy.health *= (1 + .045f * progress + .0015f * progress * progress) * typeHealth * waveMultiplier;
                enemy.speed = Math.Min(enemy.speed * (1 + .025f * progress + .0005f * progress * progress) * typeSpeed * waveMultiplier,
                    group.Enemy.speed * 1.7f);
                return new SpawnGroup { Enemy = enemy, DefinitionIndex = group.DefinitionIndex, RouteIndex = group.RouteIndex,
                    Count = group.Count, Delay = group.Delay, Interval = group.Interval };
            }).ToArray();
        }
        public string StartError()
        {
            if (Phase != BattlePhase.Ready) return "준비 단계에서 시작할 수 있습니다.";
            if (Setup.PlayerFlow && Robots.Count == 0) return "로봇을 먼저 배치하세요.";
            return null;
        }
        public void Start() { if (StartError() == null) { accumulated = 0; waveStart = Time; Phase = BattlePhase.Running; } }
        public bool ClaimRewards()
        {
            if (Phase != BattlePhase.Reward) return false;
            foreach (var kind in PendingDrops) { Inventory.TryGetValue(kind, out int old); Inventory[kind] = old + 1; }
            PendingDrops.Clear(); accumulated = 0;
            if (WaveIndex + 1 == Setup.Waves.Length) { Phase = BattlePhase.Victory; return true; }
            WaveIndex++; PrepareWave();
            foreach (var bot in Robots)
            {
                bot.Buffs.Clear(); bot.BlockingEnabled = false; bot.NextAction = Time + bot.Spec.interval;
                if (bot.Health.HasValue) bot.Health = bot.MaxHealth;
                if (bot.Program != null) bot.Program = new DefenseProgram(bot.Program.Name, bot.Program.CopyBlocks());
            }
            Phase = BattlePhase.Ready; return true;
        }
        public void TogglePause()
        {
            if (Phase == BattlePhase.Running) Phase = BattlePhase.Paused;
            else if (Phase == BattlePhase.Paused) Phase = BattlePhase.Running;
        }
        public string PlacementError(RobotSpec spec, Vector2 point)
        {
            if (Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat) return "전투가 종료되었습니다.";
            if (Setup.PlayerFlow && !CanPrepare) return "로봇 배치는 웨이브 준비 중에만 가능합니다.";
            if (RobotAvailable(spec.role) < 1) return "인벤토리에 해당 로봇이 없습니다.";
            float r = spec.radius;
            if (float.IsNaN(point.X) || float.IsNaN(point.Y) || point.X - r < Setup.Min.X || point.X + r > Setup.Max.X ||
                point.Y - r < Setup.Min.Y || point.Y + r > Setup.Max.Y) return "설치 허용 영역 밖입니다.";
            if (Robots.Count >= Setup.RobotLimit) return "시험 전장의 로봇 상한에 도달했습니다.";
            if (spec.role == RobotRole.Buffer && Robots.Any(bot => bot.Spec.role == RobotRole.Buffer)) return "버프형은 맵에 한 대만 설치할 수 있습니다.";
            if (Robots.Any(bot => Vector2.Distance(bot.Position, point) < bot.Spec.radius + r + .12f)) return "다른 로봇과 겹칩니다.";
            if (Setup.Obstacles.Any(area => area.Overlaps(point, r))) return "장애물 또는 목표 시설과 겹칩니다.";
            bool pathFits = Setup.Routes.Any(route => route.Distance(point) + r <= route.HalfWidth);
            bool touchesPath = Setup.Routes.Any(route => route.Distance(point) < route.HalfWidth + r);
            if (spec.role == RobotRole.Tank && !pathFits) return "탱커는 몸체가 경로 안에 들어가도록 설치하세요.";
            if (spec.role != RobotRole.Tank && touchesPath) return "적 경로에는 탱커만 설치할 수 있습니다.";
            return null;
        }
        public RobotState Place(RobotSpec spec, int definitionIndex, Vector2 point, out string error)
        {
            spec.Validate(); error = PlacementError(spec, point);
            if (error != null) return null;
            var bot = new RobotState { Id = nextId++, DefinitionIndex = definitionIndex, Position = point, Spec = spec.Copy(),
                RangeSetting = spec.range, Action = spec.NativeAction, Health = spec.role == RobotRole.Tank ? (float?)spec.health : null,
                NextAction = Time + spec.interval };
            if (Setup.PlayerFlow) RobotInventory[spec.role]--;
            Robots.Add(bot); return bot;
        }
        public bool RemoveRobot(int id)
        {
            if (Setup.PlayerFlow && !CanPrepare) return false;
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null) return false;
            if (Setup.PlayerFlow) GrantRobot(bot.Spec.role, 1);
            // 인벤토리는 종류별 수량만 보관하므로 강화 비용을 환급하고 Lv.1 로봇으로 돌려줍니다.
            Coins = (int)Math.Min(int.MaxValue, (long)Coins + bot.UpgradeCostPaid);
            return RemoveRobotInternal(id);
        }
        public string UpgradeError(int id)
        {
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null) return "로봇을 선택하세요.";
            if (bot.IsMaxLevel) return "최고레벨";
            if (!CanPrepare) return "정비 중에만 레벨업할 수 있습니다.";
            if (Coins < UpgradeCost) return "코인이 부족합니다.";
            return null;
        }
        public bool UpgradeRobot(int id)
        {
            if (UpgradeError(id) != null) return false;
            var bot = Robots.Find(r => r.Id == id);
            float healthRatio = bot.Health.HasValue ? bot.Health.Value / bot.MaxHealth : 0;
            Coins -= UpgradeCost; bot.UpgradeCostPaid = UpgradeCost; bot.Level++;
            if (bot.Health.HasValue) bot.Health = bot.MaxHealth * healthRatio;
            return true;
        }
        bool RemoveRobotInternal(int id)
        {
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null) return false;
            Robots.Remove(bot);
            foreach (var r in Robots) r.Buffs.RemoveAll(b => b.Source == id);
            foreach (var e in Enemies) if (e.BlockedBy == id) e.BlockedBy = 0;
            return true;
        }
        public bool DamageRobot(int id, float damage)
        {
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null || !bot.Health.HasValue || !Rules.Nonnegative(damage) || Phase != BattlePhase.Running) return false;
            bot.Health = Math.Max(0, bot.Health.Value - damage);
            if (bot.Health <= 0) { Emit("tank-destroyed", 0, id, bot.Position); RemoveRobotInternal(id); }
            return true;
        }
        public bool SetAction(int id, RobotAction action)
        {
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null || !Enum.IsDefined(typeof(RobotAction), action)) return false;
            bot.Action = action; return true;
        }
        public void ApplyProgram(int id, string name, IReadOnlyList<CodingGame.BlockCoding.CodeBlock> blocks)
        {
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null || !CanEdit)
                throw new FormatException("코드를 적용할 로봇이 없거나 전투가 종료되었습니다.");
            var program = new DefenseProgram(name, blocks);
            // 문법과 재고 검증이 모두 끝난 뒤 교체해야 실패 시 이전 코드와 예약 수량이 유지됩니다.
            if (Setup.PlayerFlow) foreach (var group in DefenseProgression.Used(blocks).GroupBy(k => k))
                if (group.Count() > Available(group.Key, id)) throw new FormatException(DefenseProgression.Label(group.Key) + " 블록 수량이 부족합니다. 다른 로봇의 예약 수량도 확인하세요.");
            bot.Program = program; bot.AutoExecute = false;
            bot.BlockingEnabled = false;
            foreach (var e in Enemies) if (e.BlockedBy == id) e.BlockedBy = 0;
        }
        public void RestoreAutomatic(int id)
        {
            if (Setup.PlayerFlow) return;
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null) return;
            bot.Program = null; bot.AutoExecute = true; bot.Action = bot.Spec.NativeAction;
        }
        public void SetShooterRange(int id, float range)
        {
            var bot = Robots.Find(r => r.Id == id);
            if (bot == null || bot.Spec.role != RobotRole.Shooter || !Rules.Positive(range)) return;
            bot.RangeSetting = Math.Max(bot.Spec.range * .5f, Math.Min(bot.Spec.range * 1.5f, range));
        }
        public ActionCompatibility Compatibility(RobotState bot, RobotAction action) => Rules.Compatibility(bot.Spec.role, action, Setup.Permissions);
        RobotSpec Profile(RobotState bot, RobotAction action) => Compatibility(bot, action) == ActionCompatibility.Reduced ? Setup.ActionProfiles.Single(p => p.NativeAction == action) : bot.Spec;
        float Strength(RobotState bot, RobotAction action)
        {
            var compatibility = Compatibility(bot, action);
            if (compatibility == ActionCompatibility.Native) return 1;
            if (compatibility != ActionCompatibility.Reduced) return 0;
            return action != RobotAction.Block && action != RobotAction.Buff ? .1f : Setup.Permissions.First(p => p.role == bot.Spec.role && p.action == action).strength;
        }
        public float ActionDamage(RobotState bot, RobotAction action) => Profile(bot, action).damage * bot.DamageMultiplier * Strength(bot, action);
        public float ActionInterval(RobotState bot, RobotAction action) => Profile(bot, action).interval * (bot.Interval / bot.Spec.interval);
        public ActionResult RequestAction(int robotId, RobotAction action, int targetId = 0)
        {
            var bot = Robots.Find(r => r.Id == robotId);
            if (bot == null || Phase != BattlePhase.Running || !Enum.IsDefined(typeof(RobotAction), action)) return ActionResult.Unavailable;
            var compatibility = Compatibility(bot, action);
            if (compatibility == ActionCompatibility.Ineffective) return bot.LastResult = ActionResult.Ineffective;
            if (compatibility == ActionCompatibility.Undecided) return bot.LastResult = ActionResult.Undecided;
            // block()에 도달하면 즉시 몸으로 저지합니다. 스턴과 피해만 공통 재사용 시간을 따릅니다.
            if (action == RobotAction.Block && bot.Spec.role == RobotRole.Tank) bot.BlockingEnabled = true;
            if (Time + .000001 < bot.NextAction) return bot.LastResult = ActionResult.CoolingDown;
            float strength = Strength(bot, action);
            var profile = Profile(bot, action);
            if (action == RobotAction.Buff)
            {
                var targets = Robots.Where(r => r.Id != bot.Id && Vector2.DistanceSquared(r.Position, bot.Position) <= bot.Range * bot.Range).ToList();
                if (targets.Count == 0) return bot.LastResult = ActionResult.NoTarget;
                foreach (var r in targets)
                {
                    r.Buffs.RemoveAll(b => b.Source == bot.Id);
                    float buffStrength = strength * bot.LevelMultiplier;
                    r.Buffs.Add(new BuffState { Source = bot.Id, Until = Time + profile.duration, Damage = 1 + (profile.buffDamage - 1) * buffStrength,
                        Range = 1 + (profile.buffRange - 1) * buffStrength, Interval = Math.Max(.01f, 1 - (1 - profile.buffInterval) * buffStrength) });
                }
                Emit("buff", bot.Id, 0, bot.Position, 0, bot.Range);
            }
            else
            {
                var targets = Enemies.Where(e => e.Active && Vector2.DistanceSquared(e.Position, bot.Position) <= bot.Range * bot.Range);
                var target = targetId == 0 ? targets.OrderBy(e => Vector2.DistanceSquared(e.Position, bot.Position)).ThenBy(e => e.Id).FirstOrDefault() : targets.FirstOrDefault(e => e.Id == targetId);
                if (target == null) return bot.LastResult = ActionResult.NoTarget;
                if (action == RobotAction.Block)
                {
                    foreach (var e in targets.ToArray()) Effect(e, bot.Id).StunUntil = Math.Max(Effect(e, bot.Id).StunUntil, Time + profile.duration * strength);
                    Hit(target, profile.damage * bot.DamageMultiplier * strength, bot.Id);
                    Emit("stun", bot.Id, target.Id, bot.Position, 0, bot.Range);
                }
                else if (action == RobotAction.Boom)
                {
                    // 발사 시점의 위치와 피해를 저장합니다. 투사체는 움직이는 적을 추적하지 않습니다.
                    Projectiles.Add(new ProjectileState { Id = nextId++, Source = bot.Id, Origin = bot.Position, Destination = target.Position,
                        LaunchedAt = Time, ImpactAt = Time + Math.Max(TickDuration, Vector2.Distance(bot.Position, target.Position) / profile.projectileSpeed),
                        Damage = profile.damage * bot.DamageMultiplier * strength, Radius = profile.effectRadius });
                    Emit("launch", bot.Id, target.Id, target.Position);
                }
                else
                {
                    Hit(target, profile.damage * bot.DamageMultiplier * strength, bot.Id);
                    if (action == RobotAction.Slow && target.Active)
                    {
                        var effect = Effect(target, bot.Id); effect.SlowUntil = Time + profile.duration;
                        effect.Multiplier = 1 - (1 - profile.slowMultiplier) * strength;
                    }
                }
            }
            bot.NextAction = Time + profile.interval * (bot.Interval / bot.Spec.interval); bot.Executions++;
            return bot.LastResult = ActionResult.Executed;
        }
        MovementEffect Effect(EnemyState e, int source)
        {
            var effect = e.Effects.Find(f => f.Source == source);
            if (effect == null) { effect = new MovementEffect { Source = source }; e.Effects.Add(effect); }
            return effect;
        }
        void Emit(string kind, int source, int target, Vector2 point, float amount = 0, float radius = 0)
            => Events.Add(new CombatEvent { Kind = kind, Source = source, Target = target, Position = point, Amount = amount, Radius = radius });
        void Hit(EnemyState e, float damage, int source, bool projectile = false)
        {
            if (!e.Active) return;
            e.Health = Math.Max(0, e.Health - damage); Emit(projectile ? "projectile-hit" : "hit", source, e.Id, e.Position, damage);
            if (e.Health <= 0)
            {
                e.Active = false; e.BlockedBy = 0; Kills++; Emit("killed", source, e.Id, e.Position);
                if (Setup.PlayerFlow)
                {
                    Coins = (int)Math.Min(int.MaxValue, (long)Coins + Setup.Progression.coinsPerKill);
                    var drop = NeededDrop();
                    PendingDrops.Add(DefenseProgression.Canonical(drop)); Emit("drop", 0, e.Id, e.Position, (int)drop);
                }
            }
        }
        BlockKind NeededDrop()
        {
            var pool = Setup.Progression.drops.Where(d => d.firstWave <= WaveIndex + 1).ToArray();
            int Owned(BlockKind kind)
            {
                kind = DefenseProgression.Canonical(kind); Inventory.TryGetValue(kind, out int count);
                return count + PendingDrops.Count(k => DefenseProgression.Canonical(k) == kind);
            }
            BlockKind ActionBlock(RobotRole role)
            {
                switch (role)
                {
                    case RobotRole.Buffer: return BlockKind.Buff;
                    case RobotRole.Warrior: return BlockKind.Slash;
                    case RobotRole.Tank: return BlockKind.Block;
                    case RobotRole.Bomber: return BlockKind.Boom;
                    case RobotRole.Shooter: return BlockKind.Shot;
                    default: return BlockKind.Slow;
                }
            }
            foreach (var stock in Setup.Progression.robots.OrderBy(r => Owned(ActionBlock(r.role)) - r.count))
            {
                var kind = ActionBlock(stock.role);
                if (Owned(kind) < stock.count && pool.Any(d => DefenseProgression.Canonical(d.kind) == kind)) return kind;
            }
            return pool.OrderBy(d => Owned(d.kind)).ThenByDescending(d => d.weight).First().kind;
        }
        public void Advance(double elapsed)
        {
            if (Phase != BattlePhase.Running || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) return;
            accumulated += elapsed;
            // 느린 프레임의 미처리 시간을 남겨 스폰과 이동 계산이 생략되지 않게 합니다.
            int budget = 600;
            while (accumulated + 1e-9 >= TickDuration && Phase == BattlePhase.Running && budget-- > 0)
            { accumulated -= TickDuration; Tick((float)TickDuration); }
        }
        void ResolveProjectiles()
        {
            foreach (var shot in Projectiles.Where(p => Time + 1e-8 >= p.ImpactAt).ToArray())
            {
                foreach (var enemy in Enemies.Where(e => e.Active && Vector2.DistanceSquared(e.Position, shot.Destination) <= shot.Radius * shot.Radius).ToArray())
                    Hit(enemy, shot.Damage, shot.Source, true);
                Emit("blast", shot.Source, 0, shot.Destination, 0, shot.Radius);
                Projectiles.Remove(shot);
            }
        }
        void Tick(float dt)
        {
            Time += TickDuration;
            Spawn();
            foreach (var bot in Robots) bot.Buffs.RemoveAll(b => b.Until <= Time);
            foreach (var e in Enemies)
            {
                e.Effects.RemoveAll(f => f.SlowUntil <= Time && f.StunUntil <= Time);
                if (e.BlockedBy != 0 && !Robots.Any(r => r.Id == e.BlockedBy)) e.BlockedBy = 0;
                if (e.Active && e.BlockedBy == 0) TryBlock(e, e.Position, e.Position, out _);
            }
            foreach (var bot in Robots.ToArray())
                if (bot.Program != null) bot.Program.Tick(this, bot);
                else if (bot.AutoExecute) RequestAction(bot.Id, bot.Action);
            foreach (var e in Enemies)
            {
                if (!e.Active || e.Stunned(Time)) continue;
                if (e.BlockedBy != 0)
                {
                    if (Time + .000001 >= e.NextAttack)
                    { DamageRobot(e.BlockedBy, e.Spec.damage); e.NextAttack = Time + e.Spec.attackInterval; }
                }
                if (e.BlockedBy == 0) Move(e, e.Speed(Time) * dt);
            }
            if (BaseHealth <= 0) { Enemies.RemoveAll(e => !e.Active); Projectiles.Clear(); Phase = BattlePhase.Defeat; return; }
            // 시간 종료 시 비행 중인 투사체를 취소해 종료 후 피해나 드랍이 발생하지 않게 합니다.
            bool timedOut = Setup.WaveSeconds > 0 && Time - waveStart + 1e-8 >= Setup.WaveSeconds;
            if (!timedOut) ResolveProjectiles();
            Enemies.RemoveAll(e => !e.Active);
            int remaining = RemainingEnemies;
            if (remaining == 0 || timedOut)
            {
                LastWaveTimedOut = remaining > 0;
                LastSurvivors = remaining; WaveSurvivors.Add(LastSurvivors);
                // 살아 있는 적과 아직 생성되지 않은 적을 다음 웨이브로 이월하며 처치 보상은 주지 않습니다.
                carry.Clear();
                // 같은 적이라도 등장 웨이브가 달라 스탯이 다르면 별도로 이월합니다.
                foreach (var group in Enemies.Where(e => e.Active).GroupBy(e =>
                    (e.DefinitionIndex, e.RouteIndex, e.Spec.type, e.Spec.health, e.Spec.speed,
                        e.Spec.damage, e.Spec.attackInterval, e.Spec.leakDamage)))
                    carry.Add(new SpawnGroup { Enemy = group.First().Spec.Copy(), DefinitionIndex = group.Key.DefinitionIndex, RouteIndex = group.Key.RouteIndex, Count = group.Count(), Interval = .4f });
                for (int i = 0; i < currentWave.Length; i++)
                {
                    var g = currentWave[i]; int left = g.Count - groupCounts[i];
                    if (left > 0) carry.Add(new SpawnGroup { Enemy = g.Enemy.Copy(), DefinitionIndex = g.DefinitionIndex, RouteIndex = g.RouteIndex, Count = left, Interval = .4f });
                    groupCounts[i] = g.Count;
                }
                if (WaveIndex + 1 < Setup.Waves.Length) nextWave = nextWave.Concat(carry).ToArray();
                Enemies.Clear();
                Projectiles.Clear();
                Emit("wave-completed", 0, WaveIndex, Vector2.Zero);
                if (Setup.PlayerFlow) { Phase = BattlePhase.Reward; accumulated = 0; }
                else if (WaveIndex + 1 == Setup.Waves.Length) Phase = BattlePhase.Victory;
                else { WaveIndex++; PrepareWave(); }
            }
        }
        void Spawn()
        {
            var wave = currentWave;
            for (int i = 0; i < wave.Length; i++)
            {
                var g = wave[i];
                while (groupCounts[i] < g.Count && Time - waveStart + .000001 >= g.Delay + groupCounts[i] * g.Interval)
                {
                    var spec = g.Enemy.Copy();
                    Enemies.Add(new EnemyState { Id = nextId++, DefinitionIndex = g.DefinitionIndex, Spec = spec, Health = spec.health,
                        Position = Setup.Routes[g.RouteIndex].Points[0], RouteIndex = g.RouteIndex, NextAttack = Time + spec.attackInterval });
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
                e.Active = false; e.BlockedBy = 0; Leaks++; BaseHealth = Math.Max(0, BaseHealth - e.Spec.leakDamage);
                Emit("leaked", e.Id, 0, e.Position, e.Spec.leakDamage);
            }
        }
    }
}
