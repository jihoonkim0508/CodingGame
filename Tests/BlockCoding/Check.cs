using System;
using System.IO;
using System.Collections.Generic;
using CodingGame.BlockCoding;

static class Check
{
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Main(string[] args)
    {
        var programs = new Dictionary<string, string>();
        foreach (var pair in new[] { ("00012", "12"), ("-.5", "-0.5"), ("+000.50", "0.50") })
            Assert(PythonTreeCompiler.NormalizeNumber(pair.Item1) == pair.Item2, "Numeric normalization.");
        string huge = new string('9', 5000);
        Assert(PythonTreeCompiler.NormalizeNumber(huge) == huge, "Numbers were truncated.");
        foreach (string invalid in new[] { "", "-", "1.2.3", "1e", "NaN", "Infinity", "1;attack()", "1\n" })
        {
            bool rejected = false;
            try { PythonTreeCompiler.NormalizeNumber(invalid); } catch (FormatException) { rejected = true; }
            Assert(rejected, "Invalid numeric input accepted: " + invalid);
        }
        string output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "CodingGame-BlockCoding-check.json");
        TreeCheck.Run(programs);
        File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(programs));
        Console.WriteLine("PASS: block compiler checks; all BlockCoding scripts compiled. Python fixtures: " + output);
    }
}
