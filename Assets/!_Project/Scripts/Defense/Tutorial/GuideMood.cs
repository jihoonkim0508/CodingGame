using System.Linq;

namespace CodingGame.Defense
{
    // The guide robot's face for a line, read from its words so new lines get a face without extra data: alarm first,
    // then worry, joy and resolve; anything else is calm. The order matches TutorialDialogueView's guide faces.
    public enum GuideMood { Normal, Blink, Happy, Surprised, Worried, Determined }

    public static class GuideMoods
    {
        static readonly (GuideMood mood, string[] words)[] Rules =
        {
            (GuideMood.Surprised, new[] { "?!", "뭐야", "잠깐", "경고", "거대한", "갑자기" }),
            (GuideMood.Worried, new[] { "위험", "끝이야", "멈춰", "어려워", "줄었어", "패배", "실패", "닳으면" }),
            (GuideMood.Happy, new[] { "좋아", "됐다", "잘했", "성공", "막아냈", "해냈", "쓰러뜨렸", "마쳤어", "익혔어", "끝났어" }),
            (GuideMood.Determined, new[] { "막아야", "가자", "해 보자", "지켜보자" }),
        };

        public static GuideMood For(string line)
        {
            if (string.IsNullOrEmpty(line)) return GuideMood.Normal;
            foreach (var (mood, words) in Rules) if (words.Any(line.Contains)) return mood;
            return GuideMood.Normal;
        }
    }
}
