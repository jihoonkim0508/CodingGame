using System;
using System.Collections.Generic;
using System.Linq;

namespace CodingGame.BlockCoding
{
    /// <summary>에러: code that compiles and runs but will not work as meant, checked after a successful compile. It never stops
    /// the robot (오류 do). action: the warning for an action block on this robot (the host asks Rules.Compatibility), or null.</summary>
    public static class CodeLint
    {
        public const string StaleEnemy = "반복 안에서 적을 다시 찾아 주세요. 반복 밖에서 정한 적이 쓰러지면 로봇이 멈춰요.";

        // Each warning with its line (statement block), in program order.
        public static List<(CodeBlock statement, string message)> Check(IReadOnlyList<CodeBlock> blocks, Func<BlockKind, string> action)
        {
            var found = new List<(CodeBlock, string)>();
            if (blocks == null) return found;
            void Walk(IEnumerable<CodeBlock> lines)
            {
                foreach (var line in lines.Where(b => b != null))
                {
                    foreach (var kind in Inside(line).Select(b => b.Kind).Distinct())
                        if (action?.Invoke(kind) is string message) found.Add((line, message));
                    Walk(line.Body);
                }
            }
            Walk(blocks);
            if (PythonTreeCompiler.StaleEnemyLoop(blocks) is CodeBlock loop) found.Add((loop, StaleEnemy));
            return found;
        }
        // A line and the values in it, not the lines in its body.
        static IEnumerable<CodeBlock> Inside(CodeBlock block) =>
            new[] { block }.Concat(block.Arguments.Where(a => a != null).SelectMany(Inside));
    }
}
