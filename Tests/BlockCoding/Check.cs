using System;
using System.IO;
using System.Collections.Generic;
using CodingGame.BlockCoding;

static class Check
{
    static CodeBlock B(BlockKind kind, params CodeBlock[] args) => new CodeBlock(kind, "0", args);
    static CodeBlock N(string value) => new CodeBlock(BlockKind.Number, value);
    static CodeLine L(int indent, params CodeBlock[] blocks)
    {
        var line = new CodeLine { Indent = indent };
        line.Blocks.AddRange(blocks);
        return line;
    }
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Reject(params CodeLine[] lines)
    {
        try { PythonBlockCompiler.Compile("f0", lines); }
        catch (FormatException) { return; }
        throw new Exception("Invalid blocks were accepted.");
    }
    static void Main(string[] args)
    {
        var near = B(BlockKind.NearestEnemy);
        var distance = B(BlockKind.Distance, near, B(BlockKind.PositionX), B(BlockKind.PositionY));
        var comparison = new CodeBlock(BlockKind.Comparison, "<");
        var attack = B(BlockKind.Attack, near);
        var wait = B(BlockKind.Wait, N("0.25"));
        var programs = new Dictionary<string, string>();
        programs["branch"] = PythonBlockCompiler.Compile("branch", new[] {
            L(0, B(BlockKind.If), distance, comparison, N("10")), L(1, attack),
            L(0, B(BlockKind.Elif), B(BlockKind.PositionX), new CodeBlock(BlockKind.Comparison, "=="), N("2")), L(1, wait),
            L(0, B(BlockKind.Else)), L(1, B(BlockKind.Wait, N("1"))) });
        Assert(programs["branch"].Contains("    if get_nearest_enemy().get_distance(get_pos_x(), get_pos_y()) < 10:\n        attack(get_nearest_enemy())"), "Wrong call or indentation.");
        programs["loop"] = PythonBlockCompiler.Compile("loop", new[] {
            L(0, B(BlockKind.While), B(BlockKind.PositionX), comparison, N("2")),
            L(1, B(BlockKind.If), B(BlockKind.True)), L(2, wait) });
        programs["false_loop"] = PythonBlockCompiler.Compile("false_loop", new[] { L(0, B(BlockKind.While), B(BlockKind.False)), L(1, attack) });
        programs["empty"] = PythonBlockCompiler.Compile("empty", new[] { L(7) });
        programs["after_loop"] = PythonBlockCompiler.Compile("after_loop", new[] {
            L(0, B(BlockKind.If), B(BlockKind.True)), L(1, B(BlockKind.While), B(BlockKind.False)),
            L(2, wait), L(0, B(BlockKind.Else)), L(1, attack) });
        Assert(programs["empty"] == "def empty():\n    pass\n", "Empty function.");
        foreach (string op in PythonBlockCompiler.Comparisons)
            PythonBlockCompiler.Compile("valid", new[] { L(0, B(BlockKind.If), N("1"), new CodeBlock(BlockKind.Comparison, op), N("2")), L(1, wait) });
        foreach (var pair in new[] { ("00012", "12"), ("-.5", "-0.5"), ("+000.50", "0.50"), ("01e3000", "1e3000") })
            Assert(PythonBlockCompiler.NormalizeNumber(pair.Item1) == pair.Item2, "Numeric normalization.");
        string huge = new string('9', 5000);
        Assert(PythonBlockCompiler.NormalizeNumber(huge) == huge, "Numbers were truncated.");
        foreach (string invalid in new[] { "", "-", "1.2.3", "1e", "NaN", "Infinity", "1;attack()", "1\n" })
        {
            bool rejected = false;
            try { PythonBlockCompiler.NormalizeNumber(invalid); } catch (FormatException) { rejected = true; }
            Assert(rejected, "Invalid numeric input accepted: " + invalid);
        }
        foreach (string invalid in new[] { "", "while", "get_pos_x", "1name", "x():\n    attack()", "a b" })
        {
            bool rejected = false;
            try { PythonBlockCompiler.Compile(invalid, Array.Empty<CodeLine>()); } catch (FormatException) { rejected = true; }
            Assert(rejected, "Invalid function name accepted.");
        }
        Reject(L(1, wait));
        Reject(L(-1, wait));
        Reject(L(0, B(BlockKind.If)), L(1, wait));
        Reject(L(0, B(BlockKind.If), B(BlockKind.True)));
        Reject(L(0, B(BlockKind.If), B(BlockKind.True)), L(2, wait));
        Reject(L(0, B(BlockKind.Else)), L(1, wait));
        Reject(L(0, B(BlockKind.Elif), B(BlockKind.True)), L(1, wait));
        Reject(L(0, B(BlockKind.If), B(BlockKind.True)), L(1, wait), L(0, B(BlockKind.Else)), L(1, wait), L(0, B(BlockKind.Else)), L(1, wait));
        Reject(L(0, B(BlockKind.If), N("1"), comparison), L(1, wait));
        Reject(L(0, B(BlockKind.If), N("1"), new CodeBlock(BlockKind.Comparison, "!="), N("2")), L(1, wait));
        Reject(L(0, B(BlockKind.Attack, N("1"))));
        Reject(L(0, B(BlockKind.Wait, near)));
        Reject(L(0, B(BlockKind.Wait)));
        Reject(L(0, wait, attack));
        var declaration = new CodeBlock(BlockKind.DeclareVariable, "enemy");
        var variable = new CodeBlock(BlockKind.Variable, "enemy");
        programs["variables"] = PythonBlockCompiler.Compile("variables", new[] {
            L(0, declaration, near),
            L(0, new CodeBlock(BlockKind.DeclareVariable, "seconds"), N("0.5")),
            L(0, B(BlockKind.Attack, variable)),
            L(0, B(BlockKind.Wait, new CodeBlock(BlockKind.Variable, "seconds"))) });
        Assert(programs["variables"].Contains("enemy = get_nearest_enemy()\n") && programs["variables"].Contains("attack(enemy)"), "Variable declaration/reference output.");
        programs["variable_condition"] = PythonBlockCompiler.Compile("variable_condition", new[] {
            L(0, new CodeBlock(BlockKind.DeclareVariable, "close"), N("1"), comparison, N("2")),
            L(0, B(BlockKind.If), new CodeBlock(BlockKind.Variable, "close")), L(1, wait) });
        var declared = PythonBlockCompiler.DeclaredVariables(new[] {
            L(0, declaration, near), L(0, new CodeBlock(BlockKind.DeclareVariable, "incomplete")),
            L(0, new CodeBlock(BlockKind.DeclareVariable, "missing"), B(BlockKind.Distance)),
            L(0, new CodeBlock(BlockKind.DeclareVariable, "alias"), variable), L(0, declaration, near) });
        Assert(declared.Count == 2 && declared["alias"] == BlockKind.NearestEnemy, "Complete declarations only; aliases and duplicate assignments.");
        Reject(L(0, B(BlockKind.Attack, variable)));
        Reject(L(0, B(BlockKind.Attack, variable)), L(0, declaration, near));
        Reject(L(0, declaration));
        Reject(L(0, new CodeBlock(BlockKind.DeclareVariable, "while"), N("1")));
        Reject(L(0, declaration, N("1")), L(0, B(BlockKind.Attack, variable)));
        Reject(L(0, declaration, near), L(0, B(BlockKind.Wait, variable)));
        Reject(L(0, declaration, variable));
        string output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "CodingGame-BlockCoding-check.json");
        TreeCheck.Run(programs);
        File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(programs));
        Console.WriteLine("PASS: block compiler checks; all Unity UI scripts compiled. Python fixtures: " + output);
    }
}
