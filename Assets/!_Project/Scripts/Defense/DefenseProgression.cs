using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    [Serializable] public sealed class BlockStock { public BlockKind kind; public int count = 1; }
    [Serializable] public sealed class RobotStock { public RobotRole role; public int count = 1; }
    [Serializable] public sealed class BlockDrop { public BlockKind kind; public int firstWave = 1, weight = 1; }
    [Serializable] public sealed class DefenseProgression
    {
        public int initialCoins = 100, coinsPerKill = 5, upgradeCost = 50;
        public RobotStock[] robots = {
            new RobotStock { role = RobotRole.Buffer }, new RobotStock { role = RobotRole.Warrior, count = 2 },
            new RobotStock { role = RobotRole.Tank, count = 2 }, new RobotStock { role = RobotRole.Bomber, count = 2 },
            new RobotStock { role = RobotRole.Shooter, count = 3 }, new RobotStock { role = RobotRole.Utility, count = 2 }
        };
        public BlockStock[] initial = {
            new BlockStock { kind = BlockKind.Shot, count = 3 }, new BlockStock { kind = BlockKind.Slash, count = 2 },
            new BlockStock { kind = BlockKind.Block }, new BlockStock { kind = BlockKind.Slow },
            new BlockStock { kind = BlockKind.If }, new BlockStock { kind = BlockKind.Else },
            new BlockStock { kind = BlockKind.Wait, count = 6 }, new BlockStock { kind = BlockKind.Number, count = 12 },
            new BlockStock { kind = BlockKind.Comparison, count = 4 }, new BlockStock { kind = BlockKind.True, count = 3 },
            new BlockStock { kind = BlockKind.False, count = 3 }, new BlockStock { kind = BlockKind.DeclareVariable, count = 4 },
            new BlockStock { kind = BlockKind.Variable, count = 8 }, new BlockStock { kind = BlockKind.NearestEnemy, count = 4 },
            new BlockStock { kind = BlockKind.PositionX, count = 4 }, new BlockStock { kind = BlockKind.PositionY, count = 4 }
        };
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
                case BlockKind.Variable: return "variable / get_distance()"; case BlockKind.NearestEnemy: return "get_nearest_enemy()";
                case BlockKind.PositionX: return "get_pos_x()"; case BlockKind.PositionY: return "get_pos_y()";
                case BlockKind.True: return "True"; case BlockKind.False: return "False";
                default: return kind.ToString().ToLowerInvariant();
            }
        }
    }
}
