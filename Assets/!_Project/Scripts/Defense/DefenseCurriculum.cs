using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // 지급량은 추가 수량입니다. 준비 보급은 학습 전에, 보상은 웨이브 종료 후 한 번 지급합니다.
    public static class DefenseCurriculum
    {
        public const int StageCount = 5;
        static BlockStock B(BlockKind kind, int count = 1) => new BlockStock { kind = kind, count = count };
        static RobotStock R(RobotRole role, int count = 1) => new RobotStock { role = role, count = count };
        public static WaveLesson[] Create() => new[] {
            new WaveLesson { stage=1, wave=1, topic="로봇 배치와 Attack() 연결", robots=new[]{R(RobotRole.Shooter,2)}, blocks=new[]{B(BlockKind.Shot,2)}, rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=1, wave=2, topic="함수 적용과 자동 실행", rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=1, wave=3, topic="공격 주기와 대기", blocks=new[]{B(BlockKind.Wait),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.Wait)} },
            new WaveLesson { stage=1, wave=4, topic="여러 로봇 배치", robots=new[]{R(RobotRole.Warrior)}, blocks=new[]{B(BlockKind.Slash)}, rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=1, wave=5, topic="배치와 기본 공격 종합", rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=2, wave=1, topic="if와 적 거리 비교", robots=new[]{R(RobotRole.Utility)}, blocks=new[]{B(BlockKind.If,2),B(BlockKind.Comparison,2),B(BlockKind.DeclareVariable),B(BlockKind.Variable),B(BlockKind.NearestEnemy),B(BlockKind.PositionX),B(BlockKind.PositionY),B(BlockKind.Number,4),B(BlockKind.Slow)}, rewards=new[]{B(BlockKind.If)} },
            new WaveLesson { stage=2, wave=2, topic="거리 기준값 조정", rewards=new[]{B(BlockKind.Comparison)} },
            new WaveLesson { stage=2, wave=3, topic="거리 조건과 감속", rewards=new[]{B(BlockKind.Slow)} },
            new WaveLesson { stage=2, wave=4, topic="if / elif / else 분기", robots=new[]{R(RobotRole.Buffer)}, blocks=new[]{B(BlockKind.Elif),B(BlockKind.Else),B(BlockKind.Buff),B(BlockKind.True),B(BlockKind.False)}, rewards=new[]{B(BlockKind.Comparison)} },
            new WaveLesson { stage=2, wave=5, topic="거리 조건과 분기 종합", rewards=new[]{B(BlockKind.If)} },
            new WaveLesson { stage=3, wave=1, topic="for로 정해진 횟수 반복", robots=new[]{R(RobotRole.Bomber)}, blocks=new[]{B(BlockKind.For,2),B(BlockKind.Number,3),B(BlockKind.Boom)}, rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=2, topic="반복 횟수 변경", rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=3, wave=3, topic="반복과 공격 효율", rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=4, topic="while과 for의 차이", blocks=new[]{B(BlockKind.While),B(BlockKind.Break),B(BlockKind.Continue)}, rewards=new[]{B(BlockKind.Wait)} },
            new WaveLesson { stage=3, wave=5, topic="반복문 선택과 응용", rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=4, wave=1, topic="조건문 안의 반복문", robots=new[]{R(RobotRole.Tank)}, blocks=new[]{B(BlockKind.Block),B(BlockKind.If),B(BlockKind.For)}, rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=4, wave=2, topic="조건에 따른 반복과 행동", rewards=new[]{B(BlockKind.Variable)} },
            new WaveLesson { stage=4, wave=3, topic="분기와 반복 조합", blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.Variable),B(BlockKind.NearestEnemy),B(BlockKind.PositionX),B(BlockKind.PositionY)}, rewards=new[]{B(BlockKind.Elif)} },
            new WaveLesson { stage=4, wave=4, topic="역할별 함수 조합", rewards=new[]{B(BlockKind.Buff)} },
            new WaveLesson { stage=4, wave=5, topic="조건·반복 종합 방어", rewards=new[]{B(BlockKind.For)} },
            // 원문 상세 학습은 4스테이지까지입니다. 5스테이지는 같은 도구의 종합 응용입니다.
            new WaveLesson { stage=5, wave=1, topic="종합 응용: 역할 분담", robots=new[]{R(RobotRole.Tank)}, rewards=new[]{B(BlockKind.Block)} },
            new WaveLesson { stage=5, wave=2, topic="종합 응용: 거리와 감속", rewards=new[]{B(BlockKind.Variable)} },
            new WaveLesson { stage=5, wave=3, topic="종합 응용: 거리별 분기", rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=5, wave=4, topic="종합 응용: 반복 제어", rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=5, wave=5, topic="최종 종합 방어", rewards=new[]{B(BlockKind.Shot)} }
        };
    }
}
