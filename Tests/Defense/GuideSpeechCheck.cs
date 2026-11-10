using System;
using CodingGame.Defense;

// 디버's voice: the clip key for a line, and what the Korean voice is given to read.
static class GuideSpeechCheck
{
    static int checks;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); checks++; }

    public static void Run()
    {
        Assert(GuideSpeech.Key("안녕") == GuideSpeech.Key("안녕") && GuideSpeech.Key("안녕").Length == 16, "the same line always has the same 16-letter key");
        Assert(GuideSpeech.Key("안녕") != GuideSpeech.Key("안녕!"), "a changed line gets a new key, so its old recording is not played");
        string said = GuideSpeech.Speakable("<b>① 적 찾기</b>\n‘`get_nearest_enemy()`’ 블록을 `enemy =` 옆에 넣어 줘.");
        Assert(said == "첫째, 적 찾기 겟 니어리스트 에너미 블록을 에너미 이퀄 옆에 넣어 줘.", "tags, quotes and code names: " + said);
        said = GuideSpeech.Speakable("<b>적까지의 거리 ≤ my_attack_range()</b>이면 if 안쪽을 실행해. 'slash'는 베기야.");
        Assert(said == "적까지의 거리 작거나 같다 마이 어택 레인지이면 이프 안쪽을 실행해. 슬래시는 베기야.", "comparison and keywords: " + said);
        Assert(GuideSpeech.Speakable("기호를 <b><=</b>로") == "기호를 작거나 같다 로", "a comparison inside bold is read, not dropped as a tag");
        Assert(GuideSpeech.Speakable("'1x' 버튼, enemy_distance") == "원 엑스 버튼, 에너미 디스턴스", "speed button and a long name");
        Assert(GuideSpeech.Speakable("코어 체력이 <b>12 → 11</b>로") == "코어 체력이 12, 11로", "arrow read as a pause");
        Console.WriteLine($"Guide speech checks passed: {checks}");
    }
}
