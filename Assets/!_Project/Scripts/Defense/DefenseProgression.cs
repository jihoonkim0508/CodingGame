using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    [Serializable] public sealed class BlockStock { public BlockKind kind; public int count = 1; }
    [Serializable] public sealed class RobotStock { public RobotRole role; public int count = 1; }
    [Serializable] public sealed class BlockDrop { public BlockKind kind; public int firstWave = 1, weight = 1; }
    [Serializable] public sealed class WaveLesson
    {
        public int stage, wave;
        public string topic;
        // One line telling the player what this wave asks of their code; shown as the wave goal outside the Stage 1 tutorial.
        public string goal = "";
        public RobotStock[] robots = Array.Empty<RobotStock>();
        public BlockStock[] blocks = Array.Empty<BlockStock>();
        public BlockStock[] rewards = Array.Empty<BlockStock>();
        // GameData/Award.md: robots the reward screen gives with the blocks; coins the wave starts with (-1 keeps the ones
        // the player has) and a kill gives (-1: DefenseProgression.coinsPerKill).
        public RobotStock[] rewardRobots = Array.Empty<RobotStock>();
        public int startCoins = -1, coinsPerKill = -1;
    }
    [Serializable] public sealed class DefenseProgression
    {
        public int initialCoins = 100, coinsPerKill = 5, upgradeCost = 50;
        public WaveLesson[] lessons = DefenseCurriculum.Create();
        public RobotStock[] robots = Array.Empty<RobotStock>();
        public BlockStock[] initial = Array.Empty<BlockStock>();
        public BlockDrop[] drops = {
            new BlockDrop { kind = BlockKind.Boom, weight = 3 }, new BlockDrop { kind = BlockKind.Slow, weight = 2 },
            new BlockDrop { kind = BlockKind.Shot, weight = 2 }, new BlockDrop { kind = BlockKind.Slash },
            new BlockDrop { kind = BlockKind.If }, new BlockDrop { kind = BlockKind.Else },
            new BlockDrop { kind = BlockKind.Buff, weight = 2 }, new BlockDrop { kind = BlockKind.Block },
            new BlockDrop { kind = BlockKind.While, firstWave = 3 },
            new BlockDrop { kind = BlockKind.Elif, firstWave = 3 }, new BlockDrop { kind = BlockKind.Break, firstWave = 3 },
            new BlockDrop { kind = BlockKind.Continue, firstWave = 3 }
        };
        public DefenseProgression Copy() => new DefenseProgression {
            initialCoins = initialCoins, coinsPerKill = coinsPerKill, upgradeCost = upgradeCost,
            robots = robots.Select(r => new RobotStock {role=r.role,count=r.count}).ToArray(),
            initial = initial.Select(b => new BlockStock {kind=b.kind,count=b.count}).ToArray(),
            drops = drops.Select(d => new BlockDrop {kind=d.kind,firstWave=d.firstWave,weight=d.weight}).ToArray(),
            lessons = lessons.Select(l => new WaveLesson {stage=l.stage,wave=l.wave,topic=l.topic,goal=l.goal,
                robots=l.robots.Select(r=>new RobotStock {role=r.role,count=r.count}).ToArray(),
                blocks=l.blocks.Select(b=>new BlockStock {kind=b.kind,count=b.count}).ToArray(),
                rewards=l.rewards.Select(b=>new BlockStock {kind=b.kind,count=b.count}).ToArray(),
                rewardRobots=(l.rewardRobots??Array.Empty<RobotStock>()).Select(r=>new RobotStock {role=r.role,count=r.count}).ToArray(),
                startCoins=l.startCoins,coinsPerKill=l.coinsPerKill}).ToArray()
        };
        // The longest stage: Stage 2 has ten waves (원거리_집중공격_설계 7).
        public const int MaxWaves = 10;
        public void Validate()
        {
            if (lessons == null || lessons.Any(l => l == null || l.stage < 1 || l.stage > DefenseCurriculum.StageCount || l.wave < 1 || l.wave > MaxWaves ||
                string.IsNullOrWhiteSpace(l.topic) || l.robots == null || l.blocks == null || l.rewards == null ||
                (l.rewardRobots ?? Array.Empty<RobotStock>()).Concat(l.robots).Any(r => r == null || !Enum.IsDefined(typeof(RobotRole), r.role) || r.count < 1) ||
                l.blocks.Concat(l.rewards).Any(b => b == null || !Consumes(b.kind) || b.count < 1)) ||
                lessons.GroupBy(l => (l.stage, l.wave)).Any(g => g.Count() != 1))
                throw new ArgumentException("스테이지별 보급·보상 설정을 확인하세요.");
            if (initialCoins < 0 || coinsPerKill < 0 || upgradeCost < 1)
                throw new ArgumentException("코인 초기량·처치 보상은 0 이상, 강화 비용은 양수여야 합니다.");
            if (robots == null || robots.Any(r => r == null || !Enum.IsDefined(typeof(RobotRole), r.role) || r.count < 0) ||
                initial == null || drops == null || drops.Length == 0 || !drops.Any(d => d != null && d.firstWave == 1) ||
                initial.Any(s => s == null || !Consumes(s.kind) || s.count < 0) ||
                drops.Any(d => d == null || !Consumes(d.kind) || d.firstWave < 1 || d.weight < 1 || d.weight > 1000))
                throw new ArgumentException("초기 블록 수량·드랍 표를 확인하세요.");
        }
        // The block that triggers a robot action (Attack is written as the shot block).
        public static BlockKind ActionBlock(RobotAction action)
        {
            switch (action)
            {
                case RobotAction.Buff: return BlockKind.Buff;
                case RobotAction.Slash: return BlockKind.Slash;
                case RobotAction.Block: return BlockKind.Block;
                case RobotAction.Boom: return BlockKind.Boom;
                case RobotAction.Attack: return BlockKind.Shot;
                case RobotAction.BreakShield: return BlockKind.BreakShield;
                default: return BlockKind.Slow;
            }
        }
        public static BlockKind Canonical(BlockKind kind) => kind == BlockKind.Attack ? BlockKind.Shot : kind == BlockKind.Distance || kind == BlockKind.Hp || kind == BlockKind.EnemyTypeOf || kind == BlockKind.Size ? BlockKind.Variable : kind;
        public static bool Consumes(BlockKind kind) => Enum.IsDefined(typeof(BlockKind), kind);
        public static BlockKind[] ItemKinds => Enum.GetValues(typeof(BlockKind)).Cast<BlockKind>().Select(Canonical).Distinct().ToArray();
        public static IEnumerable<BlockKind> Used(IEnumerable<CodeBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (Consumes(block.Kind)) yield return Canonical(block.Kind);
                // 메서드 수신자는 편집기에서 같은 변수 블록이므로 아이템을 중복 차감하지 않습니다.
                foreach (var kind in Used(block.Kind == BlockKind.Distance || block.Kind == BlockKind.Hp || block.Kind == BlockKind.IsDead || block.Kind == BlockKind.EnemyTypeOf || block.Kind == BlockKind.Shield ||
                    block.Kind == BlockKind.Front || block.Kind == BlockKind.Top || block.Kind == BlockKind.Size || block.Kind == BlockKind.Reflecting ||
                    block.Kind == BlockKind.Left || block.Kind == BlockKind.Right ? block.Arguments.Skip(1) : block.Arguments)) yield return kind;
                foreach (var kind in Used(block.Body)) yield return kind;
            }
        }
        public static string Label(BlockKind kind)
        {
            switch (Canonical(kind))
            {
                case BlockKind.Shot: return "shoot(Enemy)"; case BlockKind.Slash: return "slash()";
                case BlockKind.Block: return "block(Enemy)"; case BlockKind.Boom: return "boom(Enemy)";
                case BlockKind.Slow: return "slow(Enemy)"; case BlockKind.Buff: return "buff()";
                case BlockKind.Wait: return "wait()"; case BlockKind.Number: return "0";
                case BlockKind.Comparison: return "<"; case BlockKind.DeclareVariable: return "variable =";
                case BlockKind.Variable: return "variable"; case BlockKind.NearestEnemy: return "get_nearest_enemy()";
                case BlockKind.PositionX: return "get_pos_x()"; case BlockKind.PositionY: return "get_pos_y()";
                case BlockKind.MyAttackRange: return "my_attack_range()"; case BlockKind.MyPos: return "get_my_pos()";
                case BlockKind.True: return "True"; case BlockKind.False: return "False";
                case BlockKind.IsDead: return "is_dead()"; case BlockKind.Arithmetic: return "+ - * /";
                case BlockKind.EnemyTypeName: return "Enemy.Tank";
                case BlockKind.ForEach: return "for e in"; case BlockKind.GetEnemies: return "get_enemies()";
                case BlockKind.Shield: return "shield"; case BlockKind.BreakShield: return "break_shield(Enemy)";
                case BlockKind.And: return "and"; case BlockKind.Reflecting: return "reflecting"; case BlockKind.MyHp: return "my_hp()";
                case BlockKind.Front: return "front()"; case BlockKind.Top: return "top()";
                case BlockKind.Left: return "left"; case BlockKind.Right: return "right";
                case BlockKind.Or: return "or"; case BlockKind.WhileCondition: return "while 조건";
                default: return kind.ToString().ToLowerInvariant();
            }
        }
    }
}
