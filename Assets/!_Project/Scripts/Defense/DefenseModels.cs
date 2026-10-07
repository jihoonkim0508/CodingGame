using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CodingGame.Defense
{
    public enum RobotRole { Buffer, Warrior, Tank, Bomber, Shooter, Utility }
    public enum RobotAction { Buff, Slash, Block, Boom, Attack, Slow }
    public enum EnemyType { Normal, Fast, Tank }
    public enum ActionCompatibility { Native, Reduced, Ineffective, Undecided }
    public enum BattlePhase { Ready, Running, Paused, Victory, Defeat, Reward }
    public enum ActionResult { Executed, CoolingDown, NoTarget, Ineffective, Undecided, Unavailable }

    [Serializable]
    public sealed class RobotSpec
    {
        public RobotRole role;
        public float range = 4, damage = 12, interval = 1, radius = .45f;
        public float health = 100, blockRadius = 1.1f;
        public int blockCapacity = 3;
        public float effectRadius = 2, duration = 2, slowMultiplier = .5f;
        public float projectileSpeed = 4;
        public float buffDamage = 1.25f, buffRange = 1.15f, buffInterval = .8f;
        public RobotSpec Copy() => (RobotSpec)MemberwiseClone();
        public RobotAction NativeAction => Rules.NativeAction(role);
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(RobotRole), role) || !Rules.Positive(range) || !Rules.Nonnegative(damage) ||
                !Rules.Positive(interval) || !Rules.Positive(radius) || !Rules.Positive(effectRadius) || !Rules.Positive(projectileSpeed) ||
                !Rules.Positive(duration) || !Rules.Positive(slowMultiplier) || slowMultiplier > 1 ||
                !Rules.Positive(buffDamage) || buffDamage < 1 || !Rules.Positive(buffRange) || buffRange < 1 ||
                !Rules.Positive(buffInterval) || buffInterval > 1 ||
                (role == RobotRole.Tank && (!Rules.Positive(health) || !Rules.Positive(blockRadius) || blockCapacity < 1)))
                throw new ArgumentException("로봇 수치가 올바르지 않습니다: " + role);
        }
    }

    [Serializable]
    public sealed class EnemySpec
    {
        public EnemyType type;
        public float health = 40, speed = 1.7f, damage = 8, attackInterval = 1;
        public int leakDamage = 1;
        public EnemySpec Copy() => (EnemySpec)MemberwiseClone();
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(EnemyType), type) || !Rules.Positive(health) || !Rules.Positive(speed) || !Rules.Nonnegative(damage) ||
                !Rules.Positive(attackInterval) || leakDamage < 1) throw new ArgumentException("적 수치가 올바르지 않습니다.");
        }
    }

    [Serializable]
    public sealed class ActionPermission
    {
        public RobotRole role;
        public RobotAction action;
        public float strength = .5f;
    }

    public static class Rules
    {
        public static bool Nonnegative(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0;
        public static bool Positive(float v) => Nonnegative(v) && v > 0;
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static RobotAction NativeAction(RobotRole role) => (RobotAction)(int)role;
        public static ActionCompatibility Compatibility(RobotRole role, RobotAction action, IReadOnlyList<ActionPermission> permissions = null)
        {
            if (NativeAction(role) == action) return ActionCompatibility.Native;
            if ((role == RobotRole.Bomber || role == RobotRole.Shooter) && action == RobotAction.Block ||
                role == RobotRole.Tank && action == RobotAction.Buff) return ActionCompatibility.Ineffective;
            if (action != RobotAction.Block && action != RobotAction.Buff) return ActionCompatibility.Reduced;
            if (permissions != null)
                foreach (var entry in permissions)
                    if (entry.role == role && entry.action == action) return ActionCompatibility.Reduced;
            return ActionCompatibility.Undecided;
        }
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
        public static string ActionName(RobotAction action)
        {
            switch (action)
            {
                case RobotAction.Buff: return "buff()";
                case RobotAction.Slash: return "slash(enemy)";
                case RobotAction.Block: return "block(enemy)";
                case RobotAction.Boom: return "boom(enemy)";
                case RobotAction.Attack: return "attack(enemy)";
                default: return "slow(enemy)";
            }
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
                if (float.IsNaN(Points[i].X) || float.IsInfinity(Points[i].X) || float.IsNaN(Points[i].Y) || float.IsInfinity(Points[i].Y))
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
    }

    public sealed class BattleSetup
    {
        public int FunctionRepeatLimit = 5;
        public Vector2 Min, Max;
        public Route[] Routes;
        public SpawnGroup[][] Waves;
        // 0은 스테이지가 없는 독립 전투/미리보기입니다.
        public int Stage;
        public int BaseHealth = 12, RobotLimit = 18;
        public bool PlayerFlow;
        public float WaveSeconds;
        public int DropSeed;
        public DefenseProgression Progression = new DefenseProgression();
        public ActionPermission[] Permissions = Array.Empty<ActionPermission>();
        public RobotSpec[] ActionProfiles = Array.Empty<RobotSpec>();
        public PlacementObstacle[] Obstacles = Array.Empty<PlacementObstacle>();
        public void Validate()
        {
            if (Stage < 0) throw new ArgumentException("스테이지는 0 이상이어야 합니다.");
            if (!Rules.Nonnegative(WaveSeconds)) throw new ArgumentException("웨이브 시간은 0 이상이어야 합니다.");
            if (PlayerFlow) { if (Progression == null) throw new ArgumentException("보상 설정 누락"); Progression.Validate(); }
            if (!Rules.Finite(Min.X) || !Rules.Finite(Min.Y) || !Rules.Finite(Max.X) || !Rules.Finite(Max.Y) ||
                Min.X >= Max.X || Min.Y >= Max.Y || BaseHealth < 1 || RobotLimit < 1 || Routes == null || Routes.Length == 0 ||
                Routes.Any(r => r == null) || Waves == null || Waves.Length == 0 || Permissions == null || Obstacles == null)
                throw new ArgumentException("전장 영역·경로·웨이브·제한 설정을 확인하세요.");
            foreach (var area in Obstacles)
                if (area == null || !Rules.Finite(area.Min.X) || !Rules.Finite(area.Min.Y) || !Rules.Finite(area.Max.X) ||
                    !Rules.Finite(area.Max.Y) || area.Min.X >= area.Max.X || area.Min.Y >= area.Max.Y)
                    throw new ArgumentException("설치 금지 영역을 확인하세요.");
            if (ActionProfiles == null || ActionProfiles.Any(p => p == null) ||
                ActionProfiles.Select(p => p.role).Distinct().Count() != 6 || ActionProfiles.Length != 6)
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
            var seen = new HashSet<string>();
            foreach (var p in Permissions)
                if (p == null || !Enum.IsDefined(typeof(RobotRole), p.role) || !Enum.IsDefined(typeof(RobotAction), p.action) ||
                    !Rules.Positive(p.strength) || p.strength >= 1 || !seen.Add(p.role + ":" + p.action) ||
                    Rules.Compatibility(p.role, p.action) != ActionCompatibility.Undecided)
                    throw new ArgumentException("비전문 허용표는 미정 조합에 0~1 미만 효율로만 추가하세요.");
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
    }

    public sealed class BuffState
    {
        public int Source;
        public double Until;
        public float Damage, Range, Interval;
    }
    public sealed class MovementEffect
    {
        public int Source;
        public double SlowUntil, StunUntil;
        public float Multiplier = 1;
    }
    public sealed class RobotState
    {
        public const int MaxLevel = 2;
        public const float UpgradeMultiplier = 1.2f;
        public int Level { get; internal set; } = 1;
        public bool IsMaxLevel => Level >= MaxLevel;
        public float LevelMultiplier => Level == 1 ? 1 : UpgradeMultiplier;
        public float MaxHealth => Spec.health * LevelMultiplier;
        internal int UpgradeCostPaid;
        public int Id, DefinitionIndex;
        public Vector2 Position;
        public RobotSpec Spec;
        public RobotAction Action;
        public float RangeSetting;
        public float? Health;
        public double NextAction;
        public bool AutoExecute;
        public bool BlockingEnabled;
        public ActionResult LastResult;
        public int Executions;
        public readonly List<BuffState> Buffs = new List<BuffState>();
        public DefenseProgram Program;
        public float Range => RangeSetting * Buffs.Aggregate(1f, (v, b) => Math.Max(v, b.Range));
        public float DamageMultiplier => Buffs.Aggregate(1f, (v, b) => Math.Max(v, b.Damage)) * LevelMultiplier;
        public float Damage => Spec.damage * DamageMultiplier;
        public float Interval => Spec.interval * Buffs.Aggregate(1f, (v, b) => Math.Min(v, b.Interval));
    }
    public sealed class EnemyState
    {
        public int Id, DefinitionIndex, RouteIndex, NextPoint = 1, BlockedBy;
        public Vector2 Position;
        public EnemySpec Spec;
        public float Health;
        public double NextAttack;
        public bool Active = true;
        public readonly List<MovementEffect> Effects = new List<MovementEffect>();
        public bool Stunned(double time) => Effects.Any(e => e.StunUntil > time);
        public float Speed(double time) => Spec.speed * Effects.Where(e => e.SlowUntil > time).Aggregate(1f, (v, e) => Math.Min(v, e.Multiplier));
    }
    public sealed class ProjectileState
    {
        public int Id, Source;
        public Vector2 Origin, Destination;
        public double LaunchedAt, ImpactAt;
        public float Damage, Radius;
    }
    public sealed class CombatEvent
    {
        public string Kind;
        public bool Melee;
        public Vector2 Origin;
        public int Source, Target;
        public Vector2 Position;
        public float Amount, Radius;
    }
}
