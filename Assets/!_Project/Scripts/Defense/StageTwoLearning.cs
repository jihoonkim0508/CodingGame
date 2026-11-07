using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // Read the connected tree, never a flattened set of Python lines.
    public static class StageTwoLearning
    {
        public static string HintCode(RobotRole role) => "for i in range(3):\n    enemy = get_nearest_enemy()\n    " +
            (role == RobotRole.Shooter ? "attack(enemy)" : "slash(enemy)");
        public static CodeBlock Loop(RobotRole role, int count = 3, string name = "enemy", bool attack = true)
        {
            var loop = new CodeBlock(BlockKind.For, "i", new CodeBlock(BlockKind.Number, count.ToString(CultureInfo.InvariantCulture)));
            loop.Body.Add(new CodeBlock(BlockKind.DeclareVariable, name, new CodeBlock(BlockKind.NearestEnemy)));
            if (attack) loop.Body.Add(new CodeBlock(role == RobotRole.Warrior ? BlockKind.Slash : BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, name)));
            return loop;
        }

        public static bool Matches(IReadOnlyList<CodeBlock> blocks, RobotRole role, int exactCount = 0)
        {
            if (blocks == null || (role != RobotRole.Warrior && role != RobotRole.Shooter)) return false;
            foreach (var loop in blocks.Where(b => b.Kind == BlockKind.For))
            {
                string countText;
                try { countText = loop.Arguments.Count == 1 ? PythonTreeCompiler.NormalizeNumber(loop.Arguments[0].Value) : ""; }
                catch (FormatException) { continue; }
                if (loop.Arguments.Count != 1 || loop.Arguments[0].Kind != BlockKind.Number ||
                    !double.TryParse(countText, NumberStyles.Float, CultureInfo.InvariantCulture, out double count) ||
                    double.IsInfinity(count) || double.IsNaN(count) || count < 1 || count != Math.Truncate(count) || count > 1000000000 ||
                    exactCount > 0 && count != exactCount) continue;
                var names = new HashSet<string>();
                foreach (var line in loop.Body)
                {
                    if (line.Kind == BlockKind.DeclareVariable)
                    {
                        names.Remove(line.Value);
                        if (line.Arguments.Count == 1 && line.Arguments[0].Kind == BlockKind.NearestEnemy) names.Add(line.Value);
                    }
                    if ((role == RobotRole.Warrior ? line.Kind == BlockKind.Slash : line.Kind == BlockKind.Shot || line.Kind == BlockKind.Attack) &&
                        line.Arguments.Count == 1 && line.Arguments[0].Kind == BlockKind.Variable && names.Contains(line.Arguments[0].Value)) return true;
                }
            }
            return false;
        }

        public static string StartError(DefenseSimulation sim)
        {
            if (!sim.Setup.PlayerFlow || sim.Setup.Stage != 2 || sim.Lesson == null) return null;
            bool Has(RobotRole role, bool newlyPlaced = false, int count = 0) => sim.Robots.Any(r => r.Spec.role == role &&
                (!newlyPlaced || r.PlacedWave == sim.WaveIndex) && Matches(r.Program?.CopyBlocks(), role, count));
            switch (sim.WaveIndex)
            {
                case 0: return Has(RobotRole.Warrior, false, 3) ? null : "2-1 목표: 전사의 for(3) 안에 적 찾기 → slash를 연결하세요.";
                case 1: return Has(RobotRole.Warrior, true) ? null : "2-2 목표: 새 전사를 배치하고 for 안에 적 찾기 → slash를 직접 만드세요.";
                case 2: return Has(RobotRole.Shooter) ? null : "2-3 목표: 원거리의 for 안에 적 찾기 → attack을 연결하세요.";
                case 3: return Has(RobotRole.Shooter, true) ? null : "2-4 목표: 새 원거리를 배치하고 for 안에 적 찾기 → attack을 직접 만드세요.";
                case 4: return Has(RobotRole.Warrior) && Has(RobotRole.Shooter) ? null : "2-5 목표: 전사와 원거리 각각의 for 안에 적 찾기와 해당 공격을 연결하세요.";
                default: return null;
            }
        }
    }
}
