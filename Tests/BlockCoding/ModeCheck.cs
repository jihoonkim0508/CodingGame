using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;

// The coding panel's three tabs (Docs/코딩_모드_3탭.md 검사): printer and parser round trips, syntax errors with their lines,
// semantic errors that survive the Python tab, the 에러 lint and the completion list.
static class ModeCheck
{
    static CodeBlock B(BlockKind kind, params CodeBlock[] args) => new CodeBlock(kind, "0", args);
    static CodeBlock N(string value) => new CodeBlock(BlockKind.Number, value);
    static CodeBlock V(string name) => new CodeBlock(BlockKind.Variable, name);
    static CodeBlock Set(string name, CodeBlock value) => new CodeBlock(BlockKind.DeclareVariable, name, value);
    static CodeBlock Op(BlockKind kind, string op, CodeBlock a, CodeBlock b) => new CodeBlock(kind, op, a, b);
    static CodeBlock Body(CodeBlock block, params CodeBlock[] children) { block.Body.AddRange(children); return block; }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static bool Same(IReadOnlyList<CodeBlock> a, IReadOnlyList<CodeBlock> b) => a.Count == b.Count && a.Zip(b, Same).All(x => x);
    static bool Same(CodeBlock a, CodeBlock b) => a == null || b == null ? a == b :
        a.Kind == b.Kind && (a.Value ?? "0") == (b.Value ?? "0") && Same(a.Arguments, b.Arguments) && Same(a.Body, b.Body);
    static string Why(IReadOnlyList<CodeBlock> blocks) { try { PythonTreeCompiler.Compile("t", blocks); return null; } catch (FormatException e) { return e.Message; } }
    static int SyntaxLine(string text) { try { PythonTreeParser.Parse(text); return 0; } catch (PythonSyntaxError e) { return e.Line; } }

    public static void Run()
    {
        // Every block kind once (Attack is written as Shot).
        var all = new List<CodeBlock>
        {
            Set("enemy", B(BlockKind.NearestEnemy)), Set("x", B(BlockKind.PositionX)), Set("y", B(BlockKind.PositionY)),
            Set("r", B(BlockKind.MyAttackRange)), Set("f", N("1.5")),
            Set("d", Op(BlockKind.Arithmetic, "*", Op(BlockKind.Arithmetic, "+", V("x"), N("2")), Op(BlockKind.Arithmetic, "-", V("y"), N("-1")))),
            Body(B(BlockKind.While),
                Body(B(BlockKind.If, B(BlockKind.IsDead, V("enemy"))), Set("enemy", B(BlockKind.NearestEnemy)), B(BlockKind.Continue)),
                Body(B(BlockKind.Elif, Op(BlockKind.Comparison, "<=", B(BlockKind.Distance, V("enemy"), B(BlockKind.MyPos)), V("r"))), B(BlockKind.Shot, V("enemy")), B(BlockKind.Slash)),
                Body(B(BlockKind.Elif, Op(BlockKind.Comparison, "==", B(BlockKind.EnemyTypeOf, V("enemy")), new CodeBlock(BlockKind.EnemyTypeName, "Tank"))), B(BlockKind.Block, V("enemy")), B(BlockKind.Boom, V("enemy"))),
                Body(B(BlockKind.Else), B(BlockKind.Slow, V("enemy")), B(BlockKind.Buff)),
                Body(B(BlockKind.For, N("3")), Body(B(BlockKind.If, Op(BlockKind.Comparison, ">", B(BlockKind.Hp, V("enemy")), N("10"))), B(BlockKind.Break)),
                    B(BlockKind.Wait, N("0.5"))),
                Body(B(BlockKind.If, B(BlockKind.True))), Body(B(BlockKind.If, B(BlockKind.False)), B(BlockKind.Wait, V("f")))),
        };
        Assert(Why(all) == null, "fixture compiles: " + Why(all));
        var lines = new Dictionary<CodeBlock, int>();
        string text = PythonTreeCompiler.Print(all, lines);
        var back = PythonTreeParser.Parse(text);
        Assert(Same(all, back), "블록 → 파이썬 → 블록 keeps the tree:\n" + text);
        Assert(text.Contains("    if True:\n        pass\n"), "an empty body prints pass");
        Assert(text.Contains("d = (x + 2) * (y - -1)"), "nested calculations keep their brackets: " + text);
        Assert(lines[all[6]] == 7 && lines[all[6].Body[1]] == 11, "line numbers of the lines");
        Assert(PythonTreeCompiler.Compile("t", back) == PythonTreeCompiler.Compile("t", all), "same Python both ways");
        // The Python tab and the compiler agree on the code (the printer writes the compiler's body).
        Assert(PythonTreeCompiler.Compile("t", all).Replace("def t():\n", "").Split('\n').Select(l => l.Length >= 4 ? l.Substring(4) : l)
            .SequenceEqual(text.Split('\n')), "printed text is the compiled body:\n" + text);

        // Errors in meaning print (___, unpaired elif) and come back with the same message.
        var broken = new List<CodeBlock>[]
        {
            new List<CodeBlock> { Body(B(BlockKind.While), new CodeBlock(BlockKind.Shot, "0", new CodeBlock[] { null })) },
            new List<CodeBlock> { Body(B(BlockKind.While), B(BlockKind.Wait, B(BlockKind.NearestEnemy))) },
            new List<CodeBlock> { Body(B(BlockKind.Elif, B(BlockKind.True)), B(BlockKind.Slash)) },
            new List<CodeBlock> { Set("", N("3")), Body(B(BlockKind.If, Op(BlockKind.Comparison, "<", null, N("2")))) },
        };
        foreach (var program in broken)
        {
            string why = Why(program), printed = PythonTreeCompiler.Print(program);
            var reread = PythonTreeParser.Parse(printed);
            Assert(why != null && Why(reread) == why, "semantic error survives the Python tab: " + why + " / " + Why(reread) + "\n" + printed);
            try { PythonTreeCompiler.Compile("t", program); }
            catch (CodeError error) { Assert(error.Statement == program.Last() || program[0].Body.Contains(error.Statement) || program.Contains(error.Statement), "error names its line"); }
        }
        Assert(PythonTreeCompiler.Print(broken[0]) == "while True:\n    shoot(___)\n", "empty slot prints ___");

        // Syntax errors: the line and a reason.
        Assert(SyntaxLine("enemy = get_nearest_enemy()\nwhlie True:\n    shoot(enemy)") == 2, "typo line");
        try { PythonTreeParser.Parse("whlie True:\n    slash()"); } catch (PythonSyntaxError e) { Assert(e.Text.StartsWith("1번째 줄: ") && e.Message.Contains("while"), "typo suggests while: " + e.Text); }
        Assert(SyntaxLine("while True:\n    sl()") == 2, "unknown function");
        Assert(SyntaxLine("while True\n    slash()") == 1, "missing colon");
        Assert(SyntaxLine("while True:\n    slash(\n") == 2, "missing bracket");
        Assert(SyntaxLine("while True:\n        slash()\n    slash()") == 3, "broken indentation");
        Assert(SyntaxLine("while True:\nslash()") == 1, "missing body");
        Assert(SyntaxLine("while enemy:\n    slash()") == 1, "only while True");
        Assert(SyntaxLine("get_nearest_enemy()") == 1, "a value alone is no line");
        Assert(SyntaxLine("x = 3\nif x = 3:\n    slash()") == 2, "= in a condition");
        try { PythonTreeParser.Parse("while True:\n    get_pos_x()", k => k != BlockKind.PositionX); throw new Exception("locked block typed"); }
        catch (PythonSyntaxError e) { Assert(e.Message.Contains("열리지"), "locked block: " + e.Message); }
        Assert(PythonTreeParser.Parse("# 주석\nwhile True:  # 계속\n    pass\n").Single().Body.Count == 0, "comments and pass");

        // 에러: a lint warning on its line; the stale enemy loop.
        var shooter = new List<CodeBlock> { Body(B(BlockKind.While), Set("enemy", B(BlockKind.NearestEnemy)), B(BlockKind.Slash), B(BlockKind.Shot, V("enemy"))) };
        var warnings = CodeLint.Check(shooter, kind => kind == BlockKind.Slash ? "약하게" : null);
        Assert(warnings.Count == 1 && warnings[0].statement == shooter[0].Body[1], "slash() warned on its line");
        var stale = PythonTreeParser.Parse("enemy = get_nearest_enemy()\nwhile True:\n    shoot(enemy)");
        Assert(CodeLint.Check(stale, null).Single().statement == stale[1], "stale enemy warned on the loop");

        // Completion and hint.
        var items = CodeCompletion.Candidates("while True:\n    enemy = get_n", 29, null, null, out int start);
        Assert(items.First().Text == "get_nearest_enemy()" && items[0].Label == "가장 가까운 적" && start == 24, "get_n completes: " + string.Join(",", items.Select(i => i.Text)));
        items = CodeCompletion.Candidates("enemy = get_nearest_enemy()\nenemy.", 34, null, null, out start);
        Assert(items.Select(i => i.Text).SequenceEqual(new[] { "hp", "distance(", "is_dead()", "type" }), "members after a variable");
        items = CodeCompletion.Candidates("x = Enemy.", 10, null, null, out _);
        Assert(items.Select(i => i.Text).SequenceEqual(PythonTreeCompiler.EnemyTypes), "enemy kinds after Enemy.");
        items = CodeCompletion.Candidates("enemy = 1\nen", 12, k => k != BlockKind.Shot, k => k == BlockKind.Slash ? "!" : null, out _);
        Assert(items.Single().Text == "enemy", "assigned names: " + string.Join(",", items.Select(i => i.Text)));
        items = CodeCompletion.Candidates("sh", 2, k => k != BlockKind.Shot, null, out _);
        Assert(items.Count == 0, "a locked block is not offered");
        items = CodeCompletion.Candidates("sl", 2, null, k => k == BlockKind.Slash ? "약하게" : null, out _);
        Assert(items.First(i => i.Text == "slash()").Warning == "약하게", "warned function marked");
        Assert(CodeCompletion.Hint("wait(", 5) == "wait(초)  〔초〕 초 기다리기", "wait( hint: " + CodeCompletion.Hint("wait(", 5));
        Assert(CodeCompletion.Hint("wait(1)", 7) == null, "no hint after the call");
        // Dark+ colours, and nothing but colour tags (the caret is placed by the plain text).
        string painted = PythonTreeParser.Colorize("if enemy.hp <= 10:  # 약함\n    shoot(enemy)\nx = Enemy.Tank");
        Assert(painted.Contains("<color=#C586C0>if</color>") && painted.Contains("<color=#9CDCFE>enemy</color>") && painted.Contains("<color=#DCDCAA>shoot</color>") &&
            painted.Contains("<color=#B5CEA8>10</color>") && painted.Contains("<color=#6A9955># 약함</color>") && painted.Contains("<color=#4EC9B0>Tank</color>") &&
            painted.Contains("<noparse><</noparse>="), "Dark+ colouring: " + painted);
        Assert(System.Text.RegularExpressions.Regex.Replace(painted.Replace("<noparse><</noparse>", "\u0001"), "</?color[^>]*>", "").Replace("\u0001", "<") ==
            "if enemy.hp <= 10:  # 약함\n    shoot(enemy)\nx = Enemy.Tank", "colouring keeps the text");
        Console.WriteLine("PASS: 3 tabs: round trip of every block, semantic errors kept, syntax lines, lint, completion");
    }
}
