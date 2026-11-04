using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CodingGame.Defense
{
    // 디버's recorded voice. Each clip is named by a hash of the exact line it reads (Resources/Voice/<key>), so a line that
    // was rewritten has no clip, and stays silent, until Tools/Develop/디버 음성 만들기 records it again.
    public static class GuideSpeech
    {
        public static string Key(string line)
        {
            using (var md5 = MD5.Create())
                return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(line ?? "")).Take(8).Select(b => b.ToString("x2")));
        }

        // What the Korean voice reads: no tags or quote marks, code names in Korean sounds, symbols as words.
        static readonly (string code, string said)[] Words = {
            ("enemy.distance(get_my_pos())", "에너미 점 디스턴스"), (".distance(get_my_pos())", "점 디스턴스"),
            ("get_nearest_enemy()", "겟 니어리스트 에너미"), ("get_my_pos()", "겟 마이 포스"), ("my_attack_range()", "마이 어택 레인지"),
            ("attack_range", "어택 레인지"), ("enemy_distance", "에너미 디스턴스"), ("slash(enemy)", "슬래시 에너미"),
            ("for i in range(5):", "포 아이 인 레인지 파이브"), ("while True:", "와일 트루"), ("while True", "와일 트루"),
            ("enemy =", "에너미 이퀄"), ("slow()", "슬로우"), ("boom()", "붐"), ("wait()", "웨이트"), ("slash()", "슬래시"),
            (".hp", " 에이치피"), ("<=", " 작거나 같다 "), ("≤", " 작거나 같다 "), (">=", " 크거나 같다 "), ("≥", " 크거나 같다 "),
            ("→", ", "), ("①", "첫째, "), ("②", "둘째, "), ("③", "셋째, "),
        };
        static readonly (string word, string said)[] Names = {
            ("slash", "슬래시"), ("enemy", "에너미"), ("variable", "베리어블"), ("elif", "엘리프"), ("else", "엘스"), ("if", "이프"),
            ("for", "포"), ("while", "와일"), ("range", "레인지"), ("True", "트루"), ("False", "폴스"), ("Python", "파이썬"),
            ("1x", "원 엑스"), ("2x", "투 엑스"), ("3x", "쓰리 엑스"), ("def", "데프"),
        };
        // Rich-text tags only (<b>, </color>, ...), so a comparison like <= survives to be read out.
        static readonly Regex Tags = new Regex("</?[A-Za-z][^>]*>"), Quotes = new Regex("[`‘’'\"“”]"), Spaces = new Regex(@"\s+");
        static readonly Regex[] NamePatterns = Names.Select(n => new Regex($@"(?<![A-Za-z0-9_]){Regex.Escape(n.word)}(?![A-Za-z0-9_])")).ToArray();

        public static string Speakable(string line)
        {
            string text = Quotes.Replace(Tags.Replace(line ?? "", ""), "").Replace("\n", " ");
            foreach (var (code, said) in Words) text = text.Replace(code, said);
            for (int i = 0; i < Names.Length; i++) text = NamePatterns[i].Replace(text, Names[i].said);
            return Spaces.Replace(text.Replace("()", "").Replace("_", " "), " ").Replace(" ,", ",").Trim();
        }
    }
}
