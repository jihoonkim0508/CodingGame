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
        public RobotStock[] robots = Array.Empty<RobotStock>();
        public BlockStock[] blocks = Array.Empty<BlockStock>();
        public BlockStock[] rewards = Array.Empty<BlockStock>();
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
            new BlockDrop { kind = BlockKind.For, firstWave = 2 }, new BlockDrop { kind = BlockKind.While, firstWave = 3 },
            new BlockDrop { kind = BlockKind.Elif, firstWave = 3 }, new BlockDrop { kind = BlockKind.Break, firstWave = 3 },
            new BlockDrop { kind = BlockKind.Continue, firstWave = 3 }
        };
        public void Validate()
        {
            if (lessons == null || lessons.Any(l => l == null || l.stage < 1 || l.stage > DefenseCurriculum.StageCount || l.wave < 1 || l.wave > 5 ||
                string.IsNullOrWhiteSpace(l.topic) || l.robots == null || l.blocks == null || l.rewards == null ||
                l.robots.Any(r => r == null || !Enum.IsDefined(typeof(RobotRole), r.role) || r.count < 1) ||
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
        public static BlockKind Canonical(BlockKind kind) => kind == BlockKind.Attack ? BlockKind.Shot : kind == BlockKind.Distance ? BlockKind.Variable : kind;
        public static bool Consumes(BlockKind kind) => Enum.IsDefined(typeof(BlockKind), kind);
        public static BlockKind[] ItemKinds => Enum.GetValues(typeof(BlockKind)).Cast<BlockKind>().Select(Canonical).Distinct().ToArray();
        public static IEnumerable<BlockKind> Used(IEnumerable<CodeBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (Consumes(block.Kind)) yield return Canonical(block.Kind);
                // 메서드 수신자는 편집기에서 같은 변수 블록이므로 아이템을 중복 차감하지 않습니다.
                foreach (var kind in Used(block.Kind == BlockKind.Distance ? block.Arguments.Skip(1) : block.Arguments)) yield return kind;
                foreach (var kind in Used(block.Body)) yield return kind;
            }
        }
        public static string Label(BlockKind kind)
        {
            switch (Canonical(kind))
            {
                case BlockKind.Shot: return "Attack()"; case BlockKind.Slash: return "slash()";
                case BlockKind.Block: return "block()"; case BlockKind.Boom: return "Boom()";
                case BlockKind.Slow: return "slow()"; case BlockKind.Buff: return "buff()";
                case BlockKind.Wait: return "wait()"; case BlockKind.Number: return "0";
                case BlockKind.Comparison: return "<"; case BlockKind.DeclareVariable: return "variable =";
                case BlockKind.Variable: return "variable"; case BlockKind.NearestEnemy: return "get_nearest_enemy()";
                case BlockKind.PositionX: return "get_pos_x()"; case BlockKind.PositionY: return "get_pos_y()";
                case BlockKind.True: return "True"; case BlockKind.False: return "False";
                default: return kind.ToString().ToLowerInvariant();
            }
        }
    }
}
