using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CodingGame.Defense
{
    // Four robots (2026-11-26): Melee and Ranged (공격류, the same but for range), Tank (공격류: holds the road, bashes with
    // block()) and Utility (지원류: slows). Assets store the ints, append only: Utility keeps the old Support/Buffer 0, Tank is 3.
    public enum RobotRole { Utility, Melee, Ranged, Tank }
    // Buff stays only for its int (the buff left the game 2026-11-26: nothing parses, compiles or runs it).
    public enum RobotAction { Buff, Slash, Block, Boom, Attack, Slow, BreakShield }
    // EnemyDefinition assets store the int: append only (Reflector 7, Tree 8: the tree branch had Tree at 7, its asset was rewritten).
    public enum EnemyType { Normal, Fast, Tank, Healer, Shielder, Queue, Stack, Reflector, Tree }
    public enum ActionCompatibility { Native, Reduced, Ineffective, Undecided }
    public enum BattlePhase { Ready, Running, Paused, Victory, Defeat, Reward }
    public enum ActionResult { Executed, CoolingDown, NoTarget, Ineffective, Undecided, Unavailable }

    [Serializable]
    public sealed class RobotSpec
    {
        public RobotRole role;
        public float range = 4, damage = 12, interval = 1, radius = .45f;
        public float health = 100, blockRadius = 1.1f;
        public float effectRadius = 2, duration = 2, slowMultiplier = .5f;
        public float projectileSpeed = 4;
        // The tank: the guard it starts a wave with (held enemies wear it down, DefenseSimulation.Guard) and the seconds it stands
        // knocked back once the guard is gone. blockRadius is how far from it the road is held.
        public float guard = 10, knockback = 3;
        // Slash: the sword passes the front (and the damage lands) this share of the interval after the swing starts.
        public double strikeShare = .4; // double: the strike time is computed in double
        // break_shield(enemy) (the shooter's second action): its own cooldown, apart from the shot's, and a little damage.
        public float shieldBreakInterval = 3, shieldBreakDamage = 2;
        public RobotSpec Copy() => (RobotSpec)MemberwiseClone();
        public RobotAction NativeAction => Rules.NativeAction(role);
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(RobotRole), role) || !Rules.Positive(range) || !Rules.Nonnegative(damage) ||
                !Rules.Positive(interval) || !Rules.Positive(radius) || !Rules.Positive(effectRadius) || !Rules.Positive(projectileSpeed) ||
                !Rules.Positive(duration) || !Rules.Positive(slowMultiplier) || slowMultiplier > 1 || !(strikeShare >= 0 && strikeShare <= 1) ||
                !Rules.Positive(shieldBreakInterval) || !Rules.Nonnegative(shieldBreakDamage) || !Rules.Positive(health) ||
                !Rules.Positive(blockRadius) || !Rules.Positive(guard) || !Rules.Positive(knockback))
                throw new ArgumentException("로봇 수치가 올바르지 않습니다: " + role);
        }
    }

    [Serializable]
    public sealed class EnemySpec
    {
        public EnemyType type;
        public float health = 40, speed = 1.7f, damage = 8, attackInterval = 1;
        public int leakDamage = 1;
        // Healer: heals the other active enemies within abilityRadius by abilityAmount every abilityInterval seconds (up to
        // their health). Shielder: gives them a shield of abilityAmount that takes damage before health. Others: unused.
        public float abilityInterval = 2.5f, abilityRadius = 3, abilityAmount = 10;
        // Queue/Stack centipede: a spawn group is one centipede, each enemy of it a segment. A stack pushes a new segment onto
        // its end every abilityInterval seconds while it has fewer than segmentCap.
        public int segmentCap = 8;
        // Reflector: normal for reflectOff seconds, then reflecting (hits come back to the robot) for reflectOn, from its spawn.
        public float reflectOn = 2, reflectOff = 3;
        public EnemySpec Copy() => (EnemySpec)MemberwiseClone();
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(EnemyType), type) || !Rules.Positive(health) || !Rules.Positive(speed) || !Rules.Nonnegative(damage) ||
                !Rules.Positive(attackInterval) || leakDamage < 1 ||
                !Rules.Positive(abilityInterval) || !Rules.Positive(abilityRadius) || !Rules.Nonnegative(abilityAmount) || segmentCap < 1 ||
                !Rules.Positive(reflectOn) || !Rules.Positive(reflectOff)) throw new ArgumentException("적 수치가 올바르지 않습니다.");
        }
    }

    [Serializable]
    public sealed class ActionPermission
    {
        public RobotRole role;
        public RobotAction action;
        public float strength = .5f;
        public ActionPermission Copy() => (ActionPermission)MemberwiseClone();
    }

    public static class Rules
    {
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static bool Nonnegative(float v) => Finite(v) && v >= 0;
        public static bool Positive(float v) => Nonnegative(v) && v > 0;
        public static bool Finite(Vector2 p) => Finite(p.X) && Finite(p.Y);
        public static bool ValidBox(Vector2 min, Vector2 max) => Finite(min) && Finite(max) && min.X < max.X && min.Y < max.Y;
        public static EnemyState Nearest(IEnumerable<EnemyState> enemies, Vector2 from) =>
            enemies.OrderBy(e => Vector2.DistanceSquared(e.Position, from)).ThenBy(e => e.Id).FirstOrDefault();
        public static string RoleName(RobotRole role) => role == RobotRole.Melee ? "근접 로봇" : role == RobotRole.Ranged ? "원거리 로봇" : role == RobotRole.Tank ? "탱커 로봇" : "유틸 로봇";
        public static readonly int RoleCount = Enum.GetValues(typeof(RobotRole)).Length;
        // The action a robot does with no code (and the debrief calls its own): slash, shoot, the shield bash, slow.
        public static RobotAction NativeAction(RobotRole role) => role == RobotRole.Melee ? RobotAction.Slash : role == RobotRole.Ranged ? RobotAction.Attack :
            role == RobotRole.Tank ? RobotAction.Block : RobotAction.Slow;
        // 공격류 (melee, ranged, tank) and 지원류 (utility): slow is the support action, the rest attack actions.
        public static bool AttackClass(RobotRole role) => role != RobotRole.Utility;
        public static bool SupportAction(RobotAction action) => action == RobotAction.Slow;
        // An action of the robot's own class is native; the other class's works, weakly (DefenseBalanceRules.reducedStrength,
        // reducedIntervalMultiplier).
        public static ActionCompatibility Compatibility(RobotRole role, RobotAction action) =>
            AttackClass(role) != SupportAction(action) ? ActionCompatibility.Native : ActionCompatibility.Reduced;
        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b - a;
            var t = d.LengthSquared() > .000001f ? Vector2.Dot(p - a, d) / d.LengthSquared() : 0;
            return Vector2.Distance(p, a + d * Math.Max(0, Math.Min(1, t)));
        }
        public static bool CircleEntry(Vector2 a, Vector2 b, Vector2 center, float radius, out float t)
        {
            var f = a - center; var d = b - a;
            t = 0;
            if (f.LengthSquared() <= radius * radius) return true;
            var aa = d.LengthSquared();
            if (aa < .000001f) return false;
            var bb = 2 * Vector2.Dot(f, d); var c = f.LengthSquared() - radius * radius;
            var discriminant = bb * bb - 4 * aa * c;
            if (discriminant < 0) return false;
            t = (-bb - (float)Math.Sqrt(discriminant)) / (2 * aa);
            return t >= 0 && t <= 1;
        }
    }

    public sealed class Route
    {
        public readonly Vector2[] Points;
        public readonly float HalfWidth;
        public Route(IEnumerable<Vector2> points, float halfWidth)
        {
            Points = points.ToArray(); HalfWidth = halfWidth;
            if (Points.Length < 2 || !Rules.Positive(halfWidth)) throw new ArgumentException("경로에는 2개 이상의 지점과 양수 폭이 필요합니다.");
            for (int i = 0; i < Points.Length; i++)
            {
                if (!Rules.Finite(Points[i]))
                    throw new ArgumentException("경로 좌표가 유효하지 않습니다.");
                if (i > 0 && Vector2.DistanceSquared(Points[i - 1], Points[i]) < .0001f)
                    throw new ArgumentException("연속 경로 지점은 달라야 합니다.");
            }
        }
        public float Distance(Vector2 p)
        {
            float distance = float.MaxValue;
            for (int i = 1; i < Points.Length; i++) distance = Math.Min(distance, Rules.SegmentDistance(p, Points[i - 1], Points[i]));
            return distance;
        }
    }

    public sealed class SpawnGroup
    {
        public EnemySpec Enemy;
        public int DefinitionIndex, RouteIndex, Count;
        public float Delay, Interval;
        public bool Boss;
    }

    public sealed class BattleSetup
    {
        public DefenseBalanceRules RulesConfig = new DefenseBalanceRules();
        public DefenseTestOptions TestOptions = new DefenseTestOptions();
        public SpawnGroup[][][] CampaignWaves;
        public int InitialWave;
        public bool PreviousStageRewards = true;
        public Vector2 Min, Max;
        public Route[] Routes;
        public SpawnGroup[][] Waves;
        // 0은 스테이지가 없는 독립 전투/미리보기입니다.
        public int Stage;
        public int TreeSeed; // added to every tree legion's seed (TreeNode.Roll): a check picks a tree's shape with it
        public int BaseHealth = 12, RobotLimit = 18;
        public bool PlayerFlow;
        public DefenseProgression Progression = new DefenseProgression();
        public ActionPermission[] Permissions = Array.Empty<ActionPermission>();
        public RobotSpec[] ActionProfiles = Array.Empty<RobotSpec>();
        public PlacementObstacle[] Obstacles = Array.Empty<PlacementObstacle>();
        // The map's floor and road tiles; a robot must stand on them. Empty: only Min/Max applies (tests, preview).
        public PlacementObstacle[] Floor = Array.Empty<PlacementObstacle>();
        public void Validate()
        {
            RulesConfig.Validate();
            if (TestOptions == null || InitialWave < 0) throw new ArgumentException("테스트 설정·초기 웨이브 오류");
            if (Stage < 0) throw new ArgumentException("스테이지는 0 이상이어야 합니다.");
            if (PlayerFlow) { if (Progression == null) throw new ArgumentException("보상 설정 누락"); Progression.Validate(); }
            if (!Rules.ValidBox(Min, Max) || BaseHealth < 1 || RobotLimit < 1 || Routes == null || Routes.Length == 0 ||
                Routes.Any(r => r == null) || Waves == null || Waves.Length == 0 || Permissions == null || Obstacles == null)
                throw new ArgumentException("전장 영역·경로·웨이브·제한 설정을 확인하세요.");
            foreach (var area in Obstacles)
                if (area == null || !Rules.ValidBox(area.Min, area.Max))
                    throw new ArgumentException("설치 금지 영역을 확인하세요.");
            if (ActionProfiles == null || ActionProfiles.Any(p => p == null) ||
                ActionProfiles.Select(p => p.role).Distinct().Count() != Rules.RoleCount || ActionProfiles.Length != Rules.RoleCount)
                throw new ArgumentException("행동 기준 수치는 역할별로 하나씩 연결하세요.");
            foreach (var profile in ActionProfiles) profile.Validate();
            foreach (var wave in Waves)
            {
                if (wave == null || wave.Length == 0) throw new ArgumentException("빈 웨이브는 허용하지 않습니다.");
                foreach (var g in wave)
                {
                    if (g == null || g.Enemy == null || g.Count < 1 || g.RouteIndex < 0 || g.RouteIndex >= Routes.Length ||
                        !Rules.Nonnegative(g.Delay) || !Rules.Positive(g.Interval)) throw new ArgumentException("스폰 항목이 올바르지 않습니다.");
                    g.Enemy.Validate();
                }
            }
            if (InitialWave >= Waves.Length) throw new ArgumentException("초기 웨이브 범위 오류");
            if (CampaignWaves != null)
            {
                if (Stage < 1 || Stage > CampaignWaves.Length)
                    throw new ArgumentException("캠페인 단계 설정 오류");
                foreach (var stageWaves in CampaignWaves)
                    if (stageWaves == null || stageWaves.Length == 0 || stageWaves.Any(w => w == null || w.Length == 0 || w.Any(g => g == null || g.RouteIndex < 0 || g.RouteIndex >= Routes.Length)))
                        throw new ArgumentException("캠페인 경로·웨이브 설정 오류");
            }
            // Every other-class action works weakly by the rules: there is no combination left for a permission row to open.
            if (Permissions.Length > 0) throw new ArgumentException("허용표는 쓰지 않습니다: 다른 계열의 기술은 모두 약하게 동작합니다 (reducedStrength).");
        }
    }

    public sealed class PlacementObstacle
    {
        public Vector2 Min, Max;
        public bool Overlaps(Vector2 position, float radius)
        {
            var nearest = Vector2.Clamp(position, Min, Max);
            return Vector2.DistanceSquared(position, nearest) <= radius * radius;
        }
        public bool Contains(Vector2 position) => position.X >= Min.X && position.X <= Max.X && position.Y >= Min.Y && position.Y <= Max.Y;
    }

    public sealed class MovementEffect
    {
        public int Source;
        public double SlowUntil, StunUntil;
        public float Multiplier = 1;
    }
    public sealed class RobotState
    {
        public int Level { get; internal set; } = 1;
        public int LevelCap { get; internal set; } = 2;
        public float UpgradeFactor { get; internal set; } = 1.2f;
        public bool IsMaxLevel => Level >= LevelCap;
        public float LevelMultiplier => (float)Math.Pow(UpgradeFactor, Level - 1);
        public float MaxHealth => Spec.health * LevelMultiplier;
        internal int UpgradeCostPaid;
        public int Id, DefinitionIndex;
        public Vector2 Position;
        public RobotSpec Spec;
        public RobotAction Action;
        public float? Health;
        public double NextAction, NextShieldBreak; // break_shield() cools down on its own
        public bool AutoExecute;
        // The tank's guard (Spec.guard when full) and when its knock-back ends; it holds the road while not knocked back.
        public float Guard;
        public double KnockedUntil;
        public float MaxGuard => Spec.guard * LevelMultiplier;
        public bool Holding(double time) => Spec.role == RobotRole.Tank && time >= KnockedUntil;
        public double LastReflected = double.NegativeInfinity; // when a reflector last sent a hit back (the view flashes)
        public int Job; // the enemy this robot last took out of a list with pop(0) (the team board's line to it), 0 for none
        public ActionResult LastResult;
        public int Executions, AttacksThisWave;
        // This wave's record for the result debrief (reset when a wave is prepared).
        public int ActionsThisWave, KillsThisWave, LoopTurnsThisWave, HeldThisWave, KnockedThisWave; // held, knocked back: the tank's
        public float DamageThisWave;
        public bool SawEnemyThisWave;
        public DefenseProgram Program;
        public float Range => Spec.range;
        public float DamageMultiplier => LevelMultiplier;
        public float Damage => Spec.damage * DamageMultiplier;
        public float Interval => Spec.interval;
    }
    public sealed class EnemyState
    {
        public int Id, DefinitionIndex, RouteIndex, NextPoint = 1, BlockedBy;
        public bool Queued; // waiting in the line behind one a tank holds (BlockedBy is that tank); worked out again every tick
        public bool Boss;
        public Vector2 Position;
        public EnemySpec Spec;
        public float Health, Shield;
        public double NextAttack, NextAbility, LastAbility = double.NegativeInfinity; // Healer/Shielder pulses
        public bool Active = true;
        public Centipede Chain; // a Queue/Stack segment's centipede, else null
        public double SpawnedAt;
        // Reflector: hits made while true go back to the robot (DefenseSimulation.Hit); PhaseEnds is when it next flips.
        public bool Reflecting;
        public double PhaseEnds;
        public TreeNode Node; // a Tree legion's node (its root included), else null
        public readonly List<MovementEffect> Effects = new List<MovementEffect>();
        public bool Stunned(double time) { for (int i = 0; i < Effects.Count; i++) if (Effects[i].StunUntil > time) return true; return false; }
        public float Speed(double time)
        {
            float v = 1;
            for (int i = 0; i < Effects.Count; i++) if (Effects[i].SlowUntil > time) v = Math.Min(v, Effects[i].Multiplier);
            return Spec.speed * v;
        }
    }
    // A Queue or Stack centipede: its living segments in the order they joined, Segments[0] the head walking in front. Only one
    // end takes damage: a queue's front (first in, first out), a stack's top (last in, first out).
    public sealed class Centipede
    {
        public bool Stack;
        public readonly List<EnemyState> Segments = new List<EnemyState>();
        public EnemyState Front => Segments.Count > 0 ? Segments[0] : null;
        public EnemyState Top => Segments.Count > 0 ? Segments[Segments.Count - 1] : null;
        public EnemyState Hittable => Stack ? Top : Front;
    }
    // A tree legion (Enemy.Tree): one spawn is a whole binary tree, each node its own EnemyState with its own HP. Only the root
    // (대장) walks and is listed by get_enemies()/get_nearest_enemy(); the other nodes ride on it and are reached through
    // e.left / e.right. Only a node without living children takes damage (leaves first).
    public sealed class TreeNode
    {
        public EnemyState Root, Parent, Left, Right;
        public int Depth = 1, Slot; // the root is depth 1, slot 0; the children of (d, s) are (d + 1, 2s) and (d + 1, 2s + 1)
        public bool Open => !(Left != null && Left.Active) && !(Right != null && Right.Active);
        public const int MaxDepth = 4;
        // One tree's shape, (depth, slot) per node, root first and every parent before its children. The depth is 1..maxDepth,
        // each equally likely; one random path from the root reaches it, every other child of a node above it grows with
        // probability 1/2. The same seed gives the same tree (xorshift32, so Unity and the .NET checks agree).
        public static List<(int depth, int slot)> Roll(uint seed, out int depth, int maxDepth = MaxDepth)
        {
            uint state = (seed ^ 0x9E3779B9u) * 2654435761u; if (state == 0) state = 1;
            int Next(int n) { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (int)(state % (uint)n); }
            Next(2); Next(2); // a few turns, so nearby seeds part
            depth = 1 + Next(maxDepth); int end = Next(1 << (depth - 1)); // end: the slot the sure path reaches at that depth
            var nodes = new List<(int depth, int slot)> { (1, 0) };
            for (int i = 0; i < nodes.Count; i++)
            {
                var (d, s) = nodes[i];
                if (d == depth) continue;
                for (int side = 0; side < 2; side++)
                    if (end >> (depth - d - 1) == 2 * s + side || Next(2) == 0) nodes.Add((d + 1, 2 * s + side));
            }
            return nodes;
        }
    }
    // How a wave stood when it started, so a lost wave can be tried again with the same robots, code and coins.
    public sealed class WaveCheckpoint
    {
        public int Stage, Wave, Coins, BaseHealth, Kills, Leaks;
        public SpawnGroup[] Groups; // the wave as fought, with enemies carried over from the wave before
        public RobotSnapshot[] Robots;
        public Dictionary<RobotRole, int> RobotStock; // stock at wave start, so a robot lost in an earlier wave is not handed back on retry (null: recompute)
        public IReadOnlyList<CodingGame.BlockCoding.CodeBlock> Board; // the team board's lines then
    }
    public sealed class RobotSnapshot
    {
        public RobotSpec Spec;
        public int DefinitionIndex, Level, UpgradeCostPaid;
        public Vector2 Position;
        public float? Health;
        public string Name;
        public IReadOnlyList<CodingGame.BlockCoding.CodeBlock> Code; // null: no code
    }
    public sealed class ProjectileState
    {
        public int Id, Source;
        public int Target; // a shot at one enemy (Attack); 0 = an area blast at Destination (Boom)
        public Vector2 Origin, Destination;
        public double LaunchedAt, ImpactAt;
        public float Damage, Radius;
        public bool Reflect; // a shot at a reflector that was reflecting when it was fired comes back on impact
    }
    public sealed class CombatEvent
    {
        public string Kind;
        public string Reason;
        public bool Melee;
        public Vector2 Origin;
        public int Source, Target;
        public Vector2 Position;
        public float Amount, AppliedAmount, Radius;
        public float Seconds; // "shot"/"slash": time until the damage lands
    }
}
