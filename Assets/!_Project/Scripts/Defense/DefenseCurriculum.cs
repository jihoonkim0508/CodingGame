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
        public static WaveLesson[] Create() => new[] {
            new WaveLesson { stage=1, wave=1, topic="변수: 적을 찾아 저장하고 한 번 공격", robots=new[]{R(RobotRole.Warrior)}, blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable),B(BlockKind.Slash)}, rewards=new[]{B(BlockKind.If)} },
            new WaveLesson { stage=1, wave=2, topic="변수 실습: 두 전사가 한 번씩 공격", robots=new[]{R(RobotRole.Warrior)}, blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable),B(BlockKind.Slash)}, rewards=new[]{B(BlockKind.Comparison)} },
            new WaveLesson { stage=1, wave=3, topic="전사 for 베기와 원거리 로봇 while 사격", robots=new[]{R(RobotRole.Shooter)}, blocks=new[]{B(BlockKind.For,2),B(BlockKind.Number,4),B(BlockKind.While),B(BlockKind.Break),B(BlockKind.If),B(BlockKind.Comparison),B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable,2),B(BlockKind.Shot),B(BlockKind.Wait)}, rewards=new[]{B(BlockKind.MyPos)} },
            new WaveLesson { stage=1, wave=4, topic="반복문 실습: 한 대상의 반복을 마치고 다음 적 공격", robots=new[]{R(RobotRole.Warrior)}, blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable),B(BlockKind.Slash),B(BlockKind.For),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.If)} },
            new WaveLesson { stage=1, wave=5, topic="변수와 반복: 보스 종합 실습", blocks=new[]{B(BlockKind.For),B(BlockKind.Number,2)}, rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=2, wave=1, topic="원거리 소개: 반복 사격과 공격 거리", robots=new[]{R(RobotRole.Shooter)}, blocks=new[]{B(BlockKind.DeclareVariable),B(BlockKind.NearestEnemy),B(BlockKind.Variable),B(BlockKind.Shot),B(BlockKind.For),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.Boom)} },
            new WaveLesson { stage=2, wave=2, topic="원거리 반복 사격 자유 실습", blocks=new[]{B(BlockKind.For),B(BlockKind.Number,2)}, rewards=new[]{B(BlockKind.Block)} },
            new WaveLesson { stage=2, wave=3, topic="조건문 소개: 가까울 때 더 강하게 사격", robots=new[]{R(RobotRole.Shooter)}, blocks=new[]{B(BlockKind.DeclareVariable,2),B(BlockKind.NearestEnemy),B(BlockKind.Variable,3),B(BlockKind.Shot),B(BlockKind.If),B(BlockKind.Comparison),B(BlockKind.MyPos),B(BlockKind.Number)}, rewards=new[]{B(BlockKind.While)} },
            new WaveLesson { stage=2, wave=4, topic="반복과 조건: 매번 거리를 확인하고 사격", robots=new[]{R(RobotRole.Shooter)}, blocks=new[]{B(BlockKind.DeclareVariable,2),B(BlockKind.NearestEnemy),B(BlockKind.Variable,3),B(BlockKind.Shot),B(BlockKind.If),B(BlockKind.Comparison),B(BlockKind.MyPos),B(BlockKind.Number,2),B(BlockKind.For)}, rewards=new[]{B(BlockKind.Break)} },
            new WaveLesson { stage=2, wave=5, topic="변수·반복·조건: 보스 종합 실습", blocks=new[]{B(BlockKind.If,2),B(BlockKind.Comparison,2),B(BlockKind.MyPos,2),B(BlockKind.DeclareVariable,2),B(BlockKind.Variable,4),B(BlockKind.Number,4),B(BlockKind.For,2)}, rewards=new[]{B(BlockKind.Continue)} },
            new WaveLesson { stage=3, wave=1, topic="반복·조건 응용: 폭발과 저지", robots=new[]{R(RobotRole.Bomber),R(RobotRole.Tank)}, blocks=new[]{B(BlockKind.For,2),B(BlockKind.Number,3),B(BlockKind.Boom),B(BlockKind.Block),B(BlockKind.NearestEnemy,2),B(BlockKind.Variable,2)}, rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=2, topic="반복·조건 응용: 횟수와 행동 조절", rewards=new[]{B(BlockKind.Number)} },
            new WaveLesson { stage=3, wave=3, topic="반복과 공격 효율", rewards=new[]{B(BlockKind.For)} },
            new WaveLesson { stage=3, wave=4, topic="while과 for의 차이", blocks=new[]{B(BlockKind.While),B(BlockKind.Break),B(BlockKind.Continue)}, rewards=new[]{B(BlockKind.Wait)} },
            new WaveLesson { stage=3, wave=5, topic="반복문 선택과 응용", rewards=new[]{B(BlockKind.For)} }
        };
    }
}
