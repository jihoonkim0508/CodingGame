using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodingGame.BlockCoding
{
    /// <summary>The Python tab's completion list and parameter hint. Only blocks this stage has handed out are offered.</summary>
    public static class CodeCompletion
    {
        public struct Item
        {
            public string Text;    // shown and inserted
            public string Label;   // the 자연어 words beside it
            public string Warning; // 에러 this robot would get from it (shown grey with ⚠), or null
        }

        // (inserted text, block it needs, Korean words when the block has none)
        static readonly (string text, BlockKind kind, string label)[] Words =
        {
            ("get_nearest_enemy()", BlockKind.NearestEnemy, null), ("shoot(", BlockKind.Shot, null), ("wait(", BlockKind.Wait, null),
            ("slash()", BlockKind.Slash, null), ("block(", BlockKind.Block, null), ("boom(", BlockKind.Boom, null),
            ("slow(", BlockKind.Slow, null), ("buff()", BlockKind.Buff, null),
            ("get_my_pos()", BlockKind.MyPos, null), ("get_pos_x()", BlockKind.PositionX, null), ("get_pos_y()", BlockKind.PositionY, null),
            ("my_attack_range()", BlockKind.MyAttackRange, null),
            ("if ", BlockKind.If, null), ("elif ", BlockKind.Elif, null), ("else:", BlockKind.Else, null),
            ("while True:", BlockKind.While, null), ("for i in range(", BlockKind.For, null),
            ("break", BlockKind.Break, null), ("continue", BlockKind.Continue, null), ("pass", BlockKind.If, "빈 칸 채우기"),
            ("True", BlockKind.True, null), ("False", BlockKind.False, null), ("Enemy.", BlockKind.EnemyTypeName, "적 종류"),
        };
        static readonly (string text, BlockKind kind)[] Members =
            { ("hp", BlockKind.Hp), ("distance(", BlockKind.Distance), ("is_dead()", BlockKind.IsDead), ("type", BlockKind.EnemyTypeOf) };
        static readonly Regex Assigned = new Regex(@"^[ \t]*([^\W\d]\w*)[ \t]*=(?!=)", RegexOptions.Multiline);

        // start: where the word being typed begins (the completion replaces text[start..caret]).
        public static List<Item> Candidates(string text, int caret, Func<BlockKind, bool> available, Func<BlockKind, string> warning, out int start)
        {
            text ??= ""; caret = Math.Max(0, Math.Min(caret, text.Length));
            available ??= _ => true;
            start = caret;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
            string prefix = text.Substring(start, caret - start);
            var result = new List<Item>();
            bool Match(string candidate) => candidate.StartsWith(prefix, StringComparison.Ordinal) && candidate != prefix;
            if (start > 0 && text[start - 1] == '.')
            {
                int end = start - 1, from = end;
                while (from > 0 && (char.IsLetterOrDigit(text[from - 1]) || text[from - 1] == '_')) from--;
                string receiver = text.Substring(from, end - from);
                if (receiver == "Enemy")
                {
                    if (available(BlockKind.EnemyTypeName))
                        for (int i = 0; i < PythonTreeCompiler.EnemyTypes.Length; i++)
                            if (Match(PythonTreeCompiler.EnemyTypes[i])) result.Add(new Item { Text = PythonTreeCompiler.EnemyTypes[i], Label = PythonTreeCompiler.NaturalEnemyTypes[i] });
                    return result;
                }
                if (receiver.Length == 0 || !available(BlockKind.Variable)) return result;
                foreach (var (member, kind) in Members)
                    if (available(kind == BlockKind.IsDead ? BlockKind.IsDead : kind == BlockKind.EnemyTypeOf ? BlockKind.EnemyTypeName : BlockKind.Variable) && Match(member))
                        result.Add(new Item { Text = member, Label = PythonTreeCompiler.NaturalText(kind).Replace("〔적〕", receiver).Trim() });
                return result;
            }
            foreach (var (word, kind, label) in Words)
                if (available(kind) && Match(word))
                    result.Add(new Item { Text = word, Label = label ?? PythonTreeCompiler.NaturalText(kind), Warning = warning?.Invoke(kind) });
            if (available(BlockKind.Variable))
                foreach (string name in Assigned.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
                    if (name != PythonTreeCompiler.Hole && Match(name) && !result.Any(r => r.Text == name)) result.Add(new Item { Text = name, Label = "변수" });
            return result;
        }

        static readonly Dictionary<string, (string signature, BlockKind kind)> Signatures = new Dictionary<string, (string, BlockKind)>
        {
            ["wait"] = ("wait(초)", BlockKind.Wait), ["shoot"] = ("shoot(적)", BlockKind.Shot), ["block"] = ("block(적)", BlockKind.Block),
            ["boom"] = ("boom(적)", BlockKind.Boom), ["slow"] = ("slow(적)", BlockKind.Slow), ["range"] = ("range(n)", BlockKind.For),
            ["distance"] = ("distance(위치)", BlockKind.Distance),
        };
        // The hint for the call whose brackets the caret is in, e.g. "wait(초)  〔초〕 초 기다리기"; null outside one.
        public static string Hint(string text, int caret)
        {
            text ??= ""; caret = Math.Max(0, Math.Min(caret, text.Length));
            int depth = 0;
            for (int i = caret - 1; i >= 0 && text[i] != '\n'; i--)
            {
                if (text[i] == ')') depth++;
                else if (text[i] == '(' && depth-- == 0)
                {
                    int from = i;
                    while (from > 0 && (char.IsLetterOrDigit(text[from - 1]) || text[from - 1] == '_')) from--;
                    return Signatures.TryGetValue(text.Substring(from, i - from), out var call)
                        ? call.signature + "  " + PythonTreeCompiler.NaturalText(call.kind) : null;
                }
            }
            return null;
        }
    }
}
