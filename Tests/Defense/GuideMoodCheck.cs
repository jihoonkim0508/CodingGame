using System;
using CodingGame.Defense;

// The guide's face read from the words of real Stage 1 lines.
static class GuideMoodCheck
{
    static int checks;
    static void Expect(string line, GuideMood mood)
    {
        var got = GuideMoods.For(line);
        if (got != mood) throw new Exception($"'{line}': {got}, expected {mood}");
        checks++;
    }

    public static void Run()
    {
        Expect("뭐야?! 오류 코드들이 갑자기 나타났어!", GuideMood.Surprised);
        Expect("체력이 다 닳으면 개발툴이 멈춰버려. 그럼 우리도 끝이야!", GuideMood.Worried);
        Expect("그러니까 공격 로봇을 배치해서 오류 코드들을 막아야 해!", GuideMood.Determined);
        Expect("좋아! 방금 놓은 로봇을 눌러 봐.", GuideMood.Happy);
        // Worry wins over the kill word in the same line: the tough one could not be brought down.
        Expect("저 단단한 오류 코드는 이번 공격으로 쓰러뜨리기 어려워. 코어에 닿아서 체력이 <b>12에서 11로 1 줄었어.</b>", GuideMood.Worried);
        Expect("이 원은 로봇의 공격이 닿는 범위야.", GuideMood.Normal);
        Expect("", GuideMood.Normal);
        Console.WriteLine($"Guide mood checks passed: {checks}");
    }
}
