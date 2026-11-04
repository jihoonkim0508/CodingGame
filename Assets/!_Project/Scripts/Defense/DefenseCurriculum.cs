using CodingGame.BlockCoding;

namespace CodingGame.Defense
{
    // 학습 순서: 1-1 반복(slash 반복) → 1-3 변수(원거리 attack(enemy)) → 2-1 조건(가까울 때만 사격).
    // 지급량은 추가 수량입니다. 준비 보급은 학습 전에, 보상은 웨이브 종료 후 한 번 지급합니다(보상은 다음 웨이브 새 로봇용).
    public static class DefenseCurriculum
    {
        public const int StageCount = 3;
        static BlockStock B(BlockKind kind, int count = 1) => new BlockStock { kind = kind, count = count };
        static RobotStock R(RobotRole role, int count = 1) => new RobotStock { role = role, count = count };
        public static WaveLesson[] Create() => new[] {
            new WaveLesson { stage=1, wave=1, topic="반복: for로 여러 번 베기", robots=new[]{R(RobotRole.Warrior)}, blocks=new[]{B(BlockKind.Slash),B(BlockKind.For),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.Slash)} },
            new WaveLesson { stage=1, wave=2, topic="반복 자유 플레이", robots=new[]{R(RobotRole.Warrior)}, blocks=new[]{B(BlockKind.For),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=1, wave=3, topic="변수: 적을 저장해 원거리 공격", robots=new[]{R(RobotRole.Shooter)}, blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable),B(BlockKind.Shot),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=1, wave=4, topic="변수 자유 플레이", robots=new[]{R(RobotRole.Shooter)}, blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable),B(BlockKind.For),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=1, wave=5, topic="반복과 변수: 보스 종합 실습", blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable),B(BlockKind.For),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.Shot)} },
            new WaveLesson { stage=2, wave=1, topic="조건: 가까운 적만 쏘기", robots=new[]{R(RobotRole.Shooter)}, blocks=new[]{B(BlockKind.If),B(BlockKind.Comparison),B(BlockKind.DeclareVariable),B(BlockKind.Variable,2),B(BlockKind.MyPos),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.Comparison)} },
            new WaveLesson { stage=2, wave=2, topic="조건 자유 플레이: 거리 기준값 조정", blocks=new[]{B(BlockKind.If),B(BlockKind.MyPos),B(BlockKind.Variable)}, rewards=new[]{B(BlockKind.Comparison)} },
            new WaveLesson { stage=2, wave=3, topic="거리 조건과 감속", robots=new[]{R(RobotRole.Utility)}, blocks=new[]{B(BlockKind.Slow),B(BlockKind.If),B(BlockKind.DeclareVariable,2),B(BlockKind.Variable,3),B(BlockKind.NearestEnemy),B(BlockKind.MyPos),B(BlockKind.For),B(BlockKind.Number,2)}, rewards=new[]{B(BlockKind.Slow)} },
            new WaveLesson { stage=2, wave=4, topic="if / elif / else 분기", robots=new[]{R(RobotRole.Buffer)}, blocks=new[]{B(BlockKind.Elif),B(BlockKind.Else),B(BlockKind.Buff),B(BlockKind.True),B(BlockKind.False)}, rewards=new[]{B(BlockKind.Comparison)} },
            new WaveLesson { stage=2, wave=5, topic="거리 조건과 분기 종합", rewards=new[]{B(BlockKind.If)} },
            new WaveLesson { stage=3, wave=1, topic="for로 정해진 횟수 반복", robots=new[]{R(RobotRole.Bomber),R(RobotRole.Tank)}, blocks=new[]{B(BlockKind.For,2),B(BlockKind.Number,3),B(BlockKind.Boom),B(BlockKind.Block),B(BlockKind.NearestEnemy,2),B(BlockKind.Variable,2)}, rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=2, topic="반복 횟수 변경", rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=3, wave=3, topic="반복과 공격 효율", rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=4, topic="while과 for의 차이", blocks=new[]{B(BlockKind.While),B(BlockKind.Break),B(BlockKind.Continue)}, rewards=new[]{B(BlockKind.Wait)} },
            new WaveLesson { stage=3, wave=5, topic="반복문 선택과 응용", rewards=new[]{B(BlockKind.For)} }
        };
    }
}
