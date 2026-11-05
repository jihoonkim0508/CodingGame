using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // 공격 블록은 대상이 필요하므로 공격 로봇을 줄 때마다 get_nearest_enemy()와 변수 블록도 함께 지급합니다.
    // 지급량은 추가 수량입니다. 준비 보급은 학습 전에, 보상은 웨이브 종료 후 한 번 지급합니다.
    public static class DefenseCurriculum
    {
        public const int StageCount = 3;
        static BlockStock B(BlockKind kind, int count = 1) => new BlockStock { kind = kind, count = count };
        static RobotStock R(RobotRole role, int count = 1) => new RobotStock { role = role, count = count };
        public static WaveLesson StageTwoLesson(int wave)
        {
            bool ranged = wave == 3 || wave == 4;
            return new WaveLesson { stage = 2, wave = wave,
                topic = new[] { "반복문 알아보기", "반복문 직접 사용하기", "원거리 로봇 알아보기", "원거리 로봇 직접 사용하기", "반복문과 두 로봇 보스 실습" }[wave - 1],
                robots = wave == 5 ? System.Array.Empty<RobotStock>() : new[] { R(ranged ? RobotRole.Shooter : RobotRole.Warrior) },
                blocks = wave == 5 ? System.Array.Empty<BlockStock>() : new[] { B(BlockKind.For), B(BlockKind.Number), B(BlockKind.DeclareVariable), B(BlockKind.NearestEnemy), B(BlockKind.Variable), B(ranged ? BlockKind.Shot : BlockKind.Slash) },
                rewards = System.Array.Empty<BlockStock>() };
        }
        public static WaveLesson[] Create() => new[] {
            new WaveLesson { stage=1, wave=1, topic="변수: damage 값 변경", robots=new[]{R(RobotRole.Shooter,2)}, blocks=new[]{B(BlockKind.Shot,2),B(BlockKind.DeclareVariable,4),B(BlockKind.Number,4),B(BlockKind.NearestEnemy,2),B(BlockKind.Variable,2)}, rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=1, wave=2, topic="여러 변수: damage와 range", blocks=new[]{B(BlockKind.DeclareVariable,2),B(BlockKind.Number,2)}, rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=1, wave=3, topic="조건: 거리 비교", blocks=new[]{B(BlockKind.If,2),B(BlockKind.Comparison,2),B(BlockKind.Variable,4),B(BlockKind.Number,4)}, rewards=new[]{B(BlockKind.Comparison)} },
            new WaveLesson { stage=1, wave=4, topic="조건값 변경과 배치 전략", rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=1, wave=5, topic="변수와 조건: 보스 종합 실습", blocks=new[]{B(BlockKind.DeclareVariable,2),B(BlockKind.Variable,2),B(BlockKind.Number,2)}, rewards=new[]{B(BlockKind.Shot)} },
            StageTwoLesson(1), StageTwoLesson(2), StageTwoLesson(3), StageTwoLesson(4), StageTwoLesson(5),
            new WaveLesson { stage=3, wave=1, topic="for로 정해진 횟수 반복", robots=new[]{R(RobotRole.Bomber),R(RobotRole.Tank)}, blocks=new[]{B(BlockKind.For,2),B(BlockKind.Number,3),B(BlockKind.Boom),B(BlockKind.Block),B(BlockKind.NearestEnemy,2),B(BlockKind.Variable,2)}, rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=2, topic="반복 횟수 변경", rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=3, wave=3, topic="반복과 공격 효율", rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=4, topic="while과 for의 차이", blocks=new[]{B(BlockKind.While),B(BlockKind.Break),B(BlockKind.Continue)}, rewards=new[]{B(BlockKind.Wait)} },
            new WaveLesson { stage=3, wave=5, topic="반복문 선택과 응용", rewards=new[]{B(BlockKind.For)} }
        };
    }
}
