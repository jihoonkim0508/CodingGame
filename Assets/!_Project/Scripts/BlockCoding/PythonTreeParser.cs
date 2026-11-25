using System;
using System.Collections.Generic;
using System.Linq;

namespace CodingGame.BlockCoding
{
    /// <summary>A Python tab line that cannot become blocks: Line counts from 1.</summary>
    public sealed class PythonSyntaxError : FormatException
    {
        public readonly int Line;
        public PythonSyntaxError(int line, string message) : base(message) { Line = line; }
        public string Text => Line + "번째 줄: " + Message;
    }

    /// <summary>The Python tab's text back into blocks. Only what the blocks can say reads: if/elif/else, while True,
    /// for i in range(n), break, continue, pass, name = value, the block functions and values, comparisons and + - * /.
    /// ___ is an empty slot and pass an empty body (PythonTreeCompiler.Print writes them). Meaning (types, an elif with no
    /// if, an empty slot) is left to PythonTreeCompiler.Compile, as in the block tabs.</summary>
    public static class PythonTreeParser
    {
        // available: false for a block this stage has not handed out yet (null: every block).
        public static List<CodeBlock> Parse(string text, Func<BlockKind, bool> available = null, IDictionary<CodeBlock, int> lines = null)
        {
            var parser = new Reader { available = available ?? (_ => true), lineOf = lines };
            var raw = (text ?? "").Replace("\r", "").Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i];
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                if (line.Trim().Length == 0) continue;
                int indent = 0, at = 0;
                for (; at < line.Length && (line[at] == ' ' || line[at] == '\t'); at++) indent += line[at] == '\t' ? 4 : 1;
                parser.rows.Add((i + 1, indent, line.Substring(at).TrimEnd()));
            }
            var blocks = parser.Suite(0);
            return blocks;
        }

        // Words the player may have meant, for "did you mean" on a typo (whlie).
        static readonly string[] Words = { "if", "elif", "else", "while", "for", "break", "continue", "pass", "True", "False", "range",
            "shoot", "wait", "slash", "buff", "block", "boom", "slow", "get_nearest_enemy", "get_pos_x", "get_pos_y", "get_my_pos",
            "my_attack_range", "Enemy" };
        internal static readonly Dictionary<string, BlockKind> Calls = new Dictionary<string, BlockKind>
        {
            ["get_nearest_enemy"] = BlockKind.NearestEnemy, ["get_pos_x"] = BlockKind.PositionX, ["get_pos_y"] = BlockKind.PositionY,
            ["get_my_pos"] = BlockKind.MyPos, ["my_attack_range"] = BlockKind.MyAttackRange,
            ["shoot"] = BlockKind.Shot, ["wait"] = BlockKind.Wait, ["slash"] = BlockKind.Slash, ["buff"] = BlockKind.Buff,
            ["block"] = BlockKind.Block, ["boom"] = BlockKind.Boom, ["slow"] = BlockKind.Slow,
        };
        static readonly HashSet<string> Reserved = new HashSet<string> { "if", "elif", "else", "while", "for", "in", "break", "continue",
            "pass", "True", "False", "and", "or", "not", "def", "return", "None", "is", "import", "class", "lambda", "with", "try" };

        sealed class Reader
        {
            public readonly List<(int number, int indent, string text)> rows = new List<(int, int, string)>();
            public Func<BlockKind, bool> available;
            public IDictionary<CodeBlock, int> lineOf;
            int row;
            // The line being read: its tokens and the next one's index.
            List<string> tokens; int next; int number;

            public List<CodeBlock> Suite(int indent)
            {
                var result = new List<CodeBlock>();
                while (row < rows.Count)
                {
                    var line = rows[row];
                    if (line.indent < indent) break;
                    if (line.indent > indent)
                        throw new PythonSyntaxError(line.number, indent == 0 && result.Count == 0 ? "첫 줄은 들여쓰지 않고 맨 앞에서 시작해요." : "들여쓰기가 맞지 않아요. 위 줄과 같은 칸에서 시작해 주세요.");
                    row++;
                    var block = Statement(line.number, line.text, out bool opens);
                    if (opens)
                    {
                        if (row >= rows.Count || rows[row].indent <= indent)
                            throw new PythonSyntaxError(line.number, "콜론(:) 다음 줄은 들여써야 해요. 비워 둘 때는 pass를 써 주세요.");
                        int inner = rows[row].indent;
                        block.Body.AddRange(Suite(inner));
                        if (row < rows.Count && rows[row].indent > indent)
                            throw new PythonSyntaxError(rows[row].number, "들여쓰기가 맞지 않아요. 위의 어느 줄과도 칸이 맞지 않아요.");
                    }
                    if (block.Kind == Pass) continue;
                    if (lineOf != null) lineOf[block] = line.number;
                    result.Add(block);
                }
                return result;
            }
            const BlockKind Pass = (BlockKind)(-1);

            CodeBlock Statement(int line, string text, out bool opens)
            {
                number = line; tokens = Tokenize(text); next = 0; opens = false;
                string first = Peek();
                CodeBlock block;
                switch (first)
                {
                    case "if": case "elif":
                        Take();
                        block = new CodeBlock(first == "if" ? BlockKind.If : BlockKind.Elif, "0", Expression());
                        Colon(first); opens = true; break;
                    case "else":
                        Take(); block = new CodeBlock(BlockKind.Else); Colon("else"); opens = true; break;
                    case "while":
                        Take();
                        if (Peek() != "True") throw Error("while은 while True: 모양으로만 쓸 수 있어요. 멈출 때는 안에서 break를 써요.");
                        Take(); block = new CodeBlock(BlockKind.While); Colon("while"); opens = true; break;
                    case "for":
                        Take();
                        if (Peek() != "i") throw Error("for 다음에는 i를 써요. 예: for i in range(3):");
                        Take(); Expect("in", "for i 다음에는 in range(n)을 써요. 예: for i in range(3):");
                        Expect("range", "for i in 다음에는 range(n)을 써요. 예: for i in range(3):");
                        Expect("(", "range 다음에 여는 괄호 ( 가 필요해요.");
                        block = new CodeBlock(BlockKind.For, "0", Expression());
                        Expect(")", "range( 의 괄호를 ) 로 닫아 주세요.");
                        Colon("for"); opens = true; break;
                    case "break": case "continue":
                        Take(); block = new CodeBlock(first == "break" ? BlockKind.Break : BlockKind.Continue); break;
                    case "pass": Take(); block = new CodeBlock(Pass); break;
                    default:
                        if (IsName(first) && Peek(1) == "=")
                        {
                            Take(); Take();
                            if (Reserved.Contains(first)) throw Error($"'{first}'는 파이썬이 쓰는 말이라 변수 이름이 될 수 없어요.");
                            block = new CodeBlock(BlockKind.DeclareVariable, first == PythonTreeCompiler.Hole ? "" : first, Expression());
                            break;
                        }
                        if (IsName(first) && Peek(1) != null && Peek(1) != "(" && Peek(1) != "." && !Calls.ContainsKey(first))
                            throw Unknown(first, "명령");
                        block = Expression();
                        if (block == null || PythonTreeCompiler.OutputSlot(block.Kind) != BlockSlotKind.Statement)
                            throw Error("값만 있는 줄은 쓸 수 없어요. 변수에 담거나(enemy = …) 명령 안에 넣어 주세요.");
                        break;
                }
                if (block.Kind != Pass) Allow(block.Kind);
                if (Peek() != null) throw Error(Peek() == ":" ? "이 줄에는 콜론(:)이 필요 없어요." : $"줄 끝의 '{Peek()}'는 필요 없어요.");
                return block;
            }

            void Colon(string keyword)
            {
                if (Peek() == ":") { Take(); return; }
                throw Error(Peek() == null ? keyword + " 줄 끝에 콜론(:)이 필요해요." : $"'{Peek()}' 자리에 콜론(:)이 와야 해요.");
            }

            // comparison: sum [op sum] (one comparison, like the block)
            CodeBlock Expression()
            {
                var left = Sum();
                if (Peek() == "=") throw Error("같은지 비교할 때는 = 대신 ==를 써요.");
                if (Array.IndexOf(PythonTreeCompiler.Comparisons, Peek()) < 0) return left;
                string sign = Take();
                var right = Sum();
                if (Array.IndexOf(PythonTreeCompiler.Comparisons, Peek()) >= 0) throw Error("비교는 한 번에 하나만 할 수 있어요.");
                return Allow(new CodeBlock(BlockKind.Comparison, sign, left, right));
            }
            CodeBlock Sum()
            {
                var left = Product();
                while (Peek() == "+" || Peek() == "-") { string sign = Take(); left = Allow(new CodeBlock(BlockKind.Arithmetic, sign, left, Product())); }
                return left;
            }
            CodeBlock Product()
            {
                var left = Primary();
                while (Peek() == "*" || Peek() == "/") { string sign = Take(); left = Allow(new CodeBlock(BlockKind.Arithmetic, sign, left, Primary())); }
                return left;
            }
            CodeBlock Primary()
            {
                string token = Peek();
                if (token == null) throw Error("값이 빠졌어요. 채울 값을 모르면 ___를 써 두세요.");
                if (token == "(")
                {
                    Take(); var inner = Expression();
                    Expect(")", "여는 괄호 ( 를 ) 로 닫아 주세요.");
                    return inner;
                }
                if ((token == "-" || token == "+") && IsNumber(Peek(1))) { Take(); return Allow(new CodeBlock(BlockKind.Number, (token == "-" ? "-" : "") + Take())); }
                if (IsNumber(token)) return Allow(new CodeBlock(BlockKind.Number, Take()));
                if (!IsName(token)) throw Error($"'{token}' 자리에 값이 와야 해요.");
                Take();
                if (token == "True" || token == "False") return Allow(new CodeBlock(token == "True" ? BlockKind.True : BlockKind.False));
                if (token == "Enemy" && Peek() == ".")
                {
                    Take(); string type = Take();
                    if (Array.IndexOf(PythonTreeCompiler.EnemyTypes, type) < 0) throw Error("Enemy. 다음에는 Normal, Fast, Tank 중 하나를 써요.");
                    return Allow(new CodeBlock(BlockKind.EnemyTypeName, type));
                }
                if (Peek() == "(")
                {
                    if (!Calls.TryGetValue(token, out var kind)) throw Unknown(token, "함수");
                    Take();
                    var arguments = new List<CodeBlock>();
                    if (Peek() != ")")
                        while (true) { arguments.Add(Expression()); if (Peek() != ",") break; Take(); }
                    Expect(")", token + "( 의 괄호를 ) 로 닫아 주세요.");
                    int count = PythonTreeCompiler.ArgumentCount(kind);
                    if (arguments.Count != count) throw Error(count == 0 ? token + "()의 괄호 안은 비워 둬요." : $"{token}()의 괄호 안에는 값 {count}개가 들어가요.");
                    if (arguments.Any(a => a != null && PythonTreeCompiler.OutputSlot(a.Kind) == BlockSlotKind.Statement))
                        throw Error("명령은 값 자리에 넣을 수 없어요.");
                    return Allow(new CodeBlock(kind, "0", arguments.ToArray()));
                }
                if (Calls.ContainsKey(token)) throw Error(token + " 다음에 괄호 ()가 필요해요.");
                if (Reserved.Contains(token)) throw Error($"'{token}'는 이 자리에 쓸 수 없어요.");
                var variable = new CodeBlock(BlockKind.Variable, token == PythonTreeCompiler.Hole ? "" : token);
                if (Peek() != ".") return token == PythonTreeCompiler.Hole ? null : Allow(variable);
                Take();
                string member = Take();
                switch (member)
                {
                    case "hp": Allow(variable); return Allow(new CodeBlock(BlockKind.Hp, "0", variable));
                    case "type": Allow(variable); return Allow(new CodeBlock(BlockKind.EnemyTypeOf, "0", variable));
                    case "is_dead":
                        Expect("(", "is_dead 다음에 괄호 ()가 필요해요."); Expect(")", "is_dead()의 괄호 안은 비워 둬요.");
                        Allow(variable); return Allow(new CodeBlock(BlockKind.IsDead, "0", variable));
                    case "distance":
                        Expect("(", "distance 다음에 괄호 (위치)가 필요해요.");
                        var position = Expression();
                        Expect(")", "distance( 의 괄호를 ) 로 닫아 주세요.");
                        Allow(variable); return Allow(new CodeBlock(BlockKind.Distance, "0", variable, position));
                    default: throw Error($"'.{member}'는 쓸 수 없어요. 점 다음에는 hp, type, is_dead(), distance(위치)만 와요.");
                }
            }

            // A block this stage has not handed out cannot be typed either (the palette does not show it).
            CodeBlock Allow(CodeBlock block)
            {
                var kind = block.Kind == BlockKind.Distance || block.Kind == BlockKind.Hp ? BlockKind.Variable : block.Kind;
                // type and is_dead() come with the blocks that hand them out (CommandBlockView.RefreshVariableOptions).
                if (kind == BlockKind.EnemyTypeOf) kind = BlockKind.EnemyTypeName;
                if (!available(kind)) throw Error($"'{PythonTreeCompiler.Label(block.Kind)}' 블록은 아직 열리지 않았어요.");
                return block;
            }
            void Allow(BlockKind kind) => Allow(new CodeBlock(kind));

            PythonSyntaxError Unknown(string word, string what)
            {
                string near = Words.Where(w => Distance(w, word) <= Math.Max(1, Math.Min(2, word.Length / 3))).OrderBy(w => Distance(w, word)).FirstOrDefault();
                return Error($"'{word}'는 모르는 {what}{(what == "명령" ? "이에요" : "예요")}." + (near != null ? $" {near}을(를) 쓰려고 했나요?" : ""));
            }
            static int Distance(string a, string b)
            {
                var d = new int[a.Length + 1, b.Length + 1];
                for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
                for (int j = 0; j <= b.Length; j++) d[0, j] = j;
                for (int i = 1; i <= a.Length; i++)
                    for (int j = 1; j <= b.Length; j++)
                    {
                        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                        if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                    }
                return d[a.Length, b.Length];
            }

            string Peek(int ahead = 0) => next + ahead < tokens.Count ? tokens[next + ahead] : null;
            string Take() => next < tokens.Count ? tokens[next++] : throw Error("줄이 덜 끝났어요.");
            void Expect(string token, string message) { if (Peek() != token) throw Error(message); Take(); }
            PythonSyntaxError Error(string message) => new PythonSyntaxError(number, message);

            List<string> Tokenize(string text)
            {
                var result = new List<string>();
                for (int i = 0; i < text.Length;)
                {
                    char c = text[i];
                    if (c == ' ' || c == '\t') { i++; continue; }
                    int start = i;
                    if (char.IsLetter(c) || c == '_') { while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++; }
                    else if (char.IsDigit(c) || c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
                    {
                        while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                        if (i < text.Length && (char.IsLetter(text[i]) || text[i] == '_')) throw Error($"숫자 '{text.Substring(start, i - start + 1)}…'에 글자가 붙어 있어요.");
                    }
                    else if (i + 1 < text.Length && "=!<>".IndexOf(c) >= 0 && text[i + 1] == '=') i += 2;
                    else if ("=<>+-*/().:,".IndexOf(c) >= 0) i++;
                    else if (c == '!') throw Error("다른지 비교할 때는 != 를 써요.");
                    else if (c == '"' || c == '\'') throw Error("글자(\"…\")는 블록에 없어서 쓸 수 없어요.");
                    else throw Error($"'{c}'는 쓸 수 없는 글자예요.");
                    result.Add(text.Substring(start, i - start));
                }
                return result;
            }
        }
        // The Python tab's text in VS Code Dark+ colours, as TMP rich text drawn over the field (only colour tags, so every
        // glyph stays where the field puts its caret). Text outside a tag takes the label's own colour (#D4D4D4).
        static readonly HashSet<string> Flow = new HashSet<string> { "if", "elif", "else", "while", "for", "in", "break", "continue", "pass" };
        public static string Colorize(string text)
        {
            var result = new System.Text.StringBuilder();
            text ??= "";
            void Add(string piece, string color)
            {
                piece = piece.Replace("<", "<noparse><</noparse>");
                result.Append(color == null ? piece : "<color=#" + color + ">" + piece + "</color>");
            }
            bool afterEnemy = false;
            for (int i = 0; i < text.Length;)
            {
                char c = text[i]; int start = i;
                if (c == '#') { while (i < text.Length && text[i] != '\n') i++; Add(text.Substring(start, i - start), "6A9955"); continue; }
                if (char.IsLetter(c) || c == '_')
                {
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    string word = text.Substring(start, i - start);
                    int after = i; while (after < text.Length && text[after] == ' ') after++;
                    string color = afterEnemy || word == "Enemy" ? "4EC9B0" : Flow.Contains(word) ? "C586C0" : word == "True" || word == "False" ? "569CD6" :
                        after < text.Length && text[after] == '(' ? "DCDCAA" : "9CDCFE";
                    afterEnemy = word == "Enemy" && i < text.Length && text[i] == '.';
                    Add(word, color);
                    continue;
                }
                if (char.IsDigit(c)) { while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++; Add(text.Substring(start, i - start), "B5CEA8"); continue; }
                if (c != '.') afterEnemy = false;
                i++;
                Add(c.ToString(), null);
            }
            return result.ToString();
        }
        static bool IsNumber(string token) => token != null && token.Length > 0 && (char.IsDigit(token[0]) || token[0] == '.');
        static bool IsName(string token) => token != null && token.Length > 0 && (char.IsLetter(token[0]) || token[0] == '_');
    }
}
