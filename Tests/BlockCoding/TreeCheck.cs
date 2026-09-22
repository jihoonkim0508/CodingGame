using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;

static class TreeCheck
{
    static CodeBlock B(BlockKind kind, params CodeBlock[] args) => new CodeBlock(kind, "0", args);
    static CodeBlock N(string value) => new CodeBlock(BlockKind.Number, value);
    static CodeBlock V(string name) => new CodeBlock(BlockKind.Variable, name);
    static CodeBlock Set(string name, CodeBlock value) => new CodeBlock(BlockKind.DeclareVariable, name, value);
    static CodeBlock Compare(string op, CodeBlock a, CodeBlock b) => new CodeBlock(BlockKind.Comparison, op, a, b);
    static CodeBlock Body(CodeBlock block, params CodeBlock[] children) { block.Body.AddRange(children); return block; }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(params CodeBlock[] blocks)
    {
        try { PythonTreeCompiler.Compile("test", blocks); }
        catch (FormatException) { return; }
        throw new Exception("Invalid tree accepted.");
    }

    public static void Run(Dictionary<string, string> programs)
    {
        var wait = B(BlockKind.Wait, N("0.25"));
        var nearest = B(BlockKind.NearestEnemy);
        var trueBlock = B(BlockKind.True);
        var first = Body(B(BlockKind.If, Compare("<", V("i"), N("2"))), B(BlockKind.Continue));
        var second = Body(B(BlockKind.Elif, Compare("==", V("i"), N("4"))), B(BlockKind.Break));
        var otherwise = Body(B(BlockKind.Else), B(BlockKind.Wait, V("i")));
        programs["tree_loop"] = PythonTreeCompiler.Compile("tree_loop", new[] {
            Body(B(BlockKind.For, N("6")), first, second, otherwise) });
        Assert(programs["tree_loop"].Contains("for i in range(6):\n        if i < 2:"), "Nested indentation.");
        programs["tree_enemy"] = PythonTreeCompiler.Compile("tree_enemy", new[] {
            Set("enemy", nearest), Body(B(BlockKind.If, Compare("<=",
                B(BlockKind.Distance, V("enemy"), B(BlockKind.PositionX), B(BlockKind.PositionY)), N("5"))),
                B(BlockKind.Attack, V("enemy"))) });
        programs["tree_while"] = PythonTreeCompiler.Compile("tree_while", new[] {
            Body(B(BlockKind.While), wait, B(BlockKind.Break)) });
        Assert(programs["tree_while"].Contains("while True:"), "While must be an unconditional loop.");
        Assert(PythonTreeCompiler.ArgumentCount(BlockKind.While) == 0, "While has no condition socket.");
        programs["tree_empty"] = PythonTreeCompiler.Compile("tree_empty", Array.Empty<CodeBlock>());
        programs["tree_empty_body"] = PythonTreeCompiler.Compile("tree_empty_body", new[] { B(BlockKind.If, trueBlock) });
        programs["tree_nested_break"] = PythonTreeCompiler.Compile("tree_nested_break", new[] {
            Body(B(BlockKind.For, N("2")), Body(B(BlockKind.While), B(BlockKind.Break)), wait) });
        programs["tree_paths"] = PythonTreeCompiler.Compile("tree_paths", new[] {
            Body(B(BlockKind.If, trueBlock), Set("target", nearest)),
            Body(B(BlockKind.Else), Set("target", nearest)), B(BlockKind.Attack, V("target")) });

        // A chain has no fixed slot count, and attaching a branch preserves the original inputs.
        var chain = new List<CodeBlock> { Body(B(BlockKind.If, B(BlockKind.False)), wait) };
        var originalCondition = chain[0].Arguments[0];
        for (int i = 0; i < 64; i++)
        {
            var branch = Body(B(BlockKind.Elif, B(BlockKind.False)), wait);
            Assert(PythonTreeCompiler.CanInsert(chain, chain.Count, branch), "Arbitrary elif chain.");
            chain.Add(branch);
        }
        chain.Add(Body(B(BlockKind.Else), B(BlockKind.Wait, N("7"))));
        Assert(ReferenceEquals(originalCondition, chain[0].Arguments[0]), "Condition lost while chaining.");
        Assert(!PythonTreeCompiler.CanInsert(chain, 1, wait), "Must not detach following elif.");
        Assert(!PythonTreeCompiler.CanInsert(chain, chain.Count, B(BlockKind.Else)), "Duplicate else accepted.");
        Assert(!PythonTreeCompiler.CanInsert(Array.Empty<CodeBlock>(), 0, second), "Orphan elif accepted.");
        programs["tree_chain"] = PythonTreeCompiler.Compile("tree_chain", chain);

        foreach (BlockKind kind in Enum.GetValues(typeof(BlockKind)))
        {
            var output = PythonTreeCompiler.OutputSlot(kind);
            Assert(PythonTreeCompiler.Accepts(output, kind), "Matching socket rejected.");
            foreach (BlockSlotKind slot in Enum.GetValues(typeof(BlockSlotKind)))
                if (slot != output) Assert(!PythonTreeCompiler.Accepts(slot, kind), "Mismatched socket accepted.");
        }
        foreach (string op in PythonBlockCompiler.Comparisons)
            PythonTreeCompiler.Compile("compare", new[] { Body(B(BlockKind.If, Compare(op, N("1"), N("2"))), wait) });
        Reject(B(BlockKind.If, N("1")));
        Reject(B(BlockKind.While, nearest));
        Reject(B(BlockKind.Wait, trueBlock));
        Reject(Set("flag", Compare("==", N("1"), N("1"))));
        Reject(B(BlockKind.If, Compare("==", trueBlock, N("1"))));
        Reject(B(BlockKind.If, Compare("!=", N("1"), N("2"))));
        Reject(B(BlockKind.If, Compare("<", nearest, N("2"))));
        Reject(B(BlockKind.For, N("1.5")));
        Reject(B(BlockKind.For, N("2f")));
        Reject(Set("count", N("3.0")), B(BlockKind.For, V("count")));
        Reject(B(BlockKind.Break));
        Reject(Body(B(BlockKind.If, trueBlock), B(BlockKind.Continue)));
        Reject(B(BlockKind.Else));
        Reject(B(BlockKind.If, trueBlock), B(BlockKind.Else), B(BlockKind.Elif, trueBlock));
        Reject(N("1"));
        Reject(B(BlockKind.Wait));
        Reject(Body(wait, wait));
        wait.Body.Clear();
        Reject(B(BlockKind.Attack, N("3")));
        Reject(B(BlockKind.Wait, nearest));
        Reject(Set("enemy", N("1")), B(BlockKind.Attack, V("enemy")));
        Reject(B(BlockKind.Attack, V("enemy")), Set("enemy", nearest));
        Reject(Set("self", V("self")));
        Reject(Body(B(BlockKind.If, trueBlock), Set("enemy", nearest)), B(BlockKind.Attack, V("enemy")));
        Reject(Body(B(BlockKind.For, N("0")), Set("enemy", nearest)), B(BlockKind.Attack, V("enemy")));
        Reject(Body(B(BlockKind.If, trueBlock), Set("enemy", nearest)),
            Body(B(BlockKind.Else), Set("enemy", N("1"))), B(BlockKind.Attack, V("enemy")));
        var cycle = B(BlockKind.While);
        cycle.Body.Add(cycle);
        Reject(cycle);

        var declarations = new List<CodeBlock> { Set("적", nearest), Set("incomplete", null), B(BlockKind.For, N("1")) };
        Assert(PythonTreeCompiler.DeclaredVariables(declarations).OrderBy(x => x).SequenceEqual(new[] { "i", "incomplete", "적" }.OrderBy(x => x)), "Variable palette names.");
        declarations.RemoveAt(0);
        Assert(!PythonTreeCompiler.DeclaredVariables(declarations).Contains("적"), "Removed variable remained available.");
        foreach (var pair in new[] { ("00012", "12"), ("-.5", "-0.5"), ("2.0", "2.0"), ("2.", "2."), ("+0", "0") })
            Assert(PythonTreeCompiler.NormalizeNumber(pair.Item1) == pair.Item2, "Dot-based numeric normalization.");
        foreach (string invalid in new[] { "0f", "1e3", "2F", "", ".", "1.2.3", "NaN" }) Reject(B(BlockKind.Wait, N(invalid)));
        Reject(B(BlockKind.While, trueBlock));
        PythonTreeCompiler.Compile("integer_range", new[] { Body(B(BlockKind.For, N("002")), wait) });
        foreach (string invalid in new[] { "", "1abc", "for", "a b", "a\nb", "x.y", "x()", "a-b" })
        {
            bool rejected = false;
            try { PythonTreeCompiler.NormalizeIdentifier(invalid); } catch (FormatException) { rejected = true; }
            Assert(rejected, "Invalid identifier accepted.");
        }
        foreach (string valid in new[] { "_x", "적", "enemy2", "match", "case", "ｘ", "℘", "a·b", "𐐀" })
            Assert(PythonTreeCompiler.NormalizeIdentifier(valid).Length > 0, "Python identifier rejected.");
        programs["tree_unicode"] = PythonTreeCompiler.Compile("tree_unicode", new[] { Set("적", nearest), B(BlockKind.Attack, V("적")) });
        Console.WriteLine("PASS: nested block compiler, typed sockets, branch connections, variables, loops and validation.");
    }
}
