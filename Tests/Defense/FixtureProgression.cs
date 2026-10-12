using System;
using CodingGame.Defense;
using CodingGame.BlockCoding;
static class FixtureProgression { public static DefenseProgression Create() => new DefenseProgression {
lessons = Array.Empty<WaveLesson>(),
        robots = new RobotStock[] {
            new RobotStock { role = RobotRole.Buffer }, new RobotStock { role = RobotRole.Warrior, count = 2 },
            new RobotStock { role = RobotRole.Tank, count = 2 }, new RobotStock { role = RobotRole.Bomber, count = 2 },
            new RobotStock { role = RobotRole.Shooter, count = 3 }, new RobotStock { role = RobotRole.Utility, count = 2 }
        },
        initial = new BlockStock[] {
            new BlockStock { kind = BlockKind.Shot, count = 3 }, new BlockStock { kind = BlockKind.Slash, count = 2 },
            new BlockStock { kind = BlockKind.Block }, new BlockStock { kind = BlockKind.Slow },
            new BlockStock { kind = BlockKind.If }, new BlockStock { kind = BlockKind.Else },
            new BlockStock { kind = BlockKind.Wait, count = 6 }, new BlockStock { kind = BlockKind.Number, count = 12 },
            new BlockStock { kind = BlockKind.Comparison, count = 4 }, new BlockStock { kind = BlockKind.True, count = 3 },
            new BlockStock { kind = BlockKind.False, count = 3 }, new BlockStock { kind = BlockKind.DeclareVariable, count = 4 },
            new BlockStock { kind = BlockKind.Variable, count = 8 }, new BlockStock { kind = BlockKind.NearestEnemy, count = 4 },
            new BlockStock { kind = BlockKind.PositionX, count = 4 }, new BlockStock { kind = BlockKind.PositionY, count = 4 }
        },
}; }
