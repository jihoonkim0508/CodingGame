using System;
using System.Linq;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

static class Check
{
    static int checks;
    static void Require(bool b, string label) { if (!b) throw new Exception(label); checks++; }
    static void Main()
    {
        const string basic = "enemy = get_nearest_enemy()\nslash(enemy)";
        const string conditional = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\nif enemy_distance <= attack_range:\n    slash(enemy)";
        string Loop(int n, string body) => $"for i in range({n}):\n" + string.Join("\n", body.Split('\n').Select(l => "    " + l));
        DefenseProgram Program(string s) => new DefenseProgram("robot", TutorialAnswers.Parse(s));
        foreach (string answer in new[] { basic, conditional, Loop(5,basic), Loop(10,basic), Loop(10,conditional) })
        {
            Require(TutorialWatcher.Matches(Program(answer),answer), "Answer roundtrip");
            Require(TutorialWatcher.Matches(Program(System.Text.RegularExpressions.Regex.Replace(answer, @"\benemy\b", "적")),answer), "Renamed variables");
            Require(TutorialWatcher.Matches(Program(answer),"\n"+answer+"\n"), "Blank lines");
        }
        Require(!TutorialWatcher.Matches(Program(Loop(5,basic)),Loop(10,basic)), "Wrong repeat count rejected");
        Require(!TutorialWatcher.Matches(Program("enemy = get_nearest_enemy()\nfor i in range(5):\n    slash(enemy)"),Loop(5,basic)), "Stale target outside loop rejected");
        string wrongScope = "for i in range(10):\n    enemy = get_nearest_enemy()\n    enemy_distance = enemy.distance(get_my_pos())\n    attack_range = my_attack_range()\n    if enemy_distance <= attack_range:\n        attack_range = my_attack_range()\n    slash(enemy)";
        Require(!TutorialWatcher.Matches(Program(wrongScope),Loop(10,conditional)), "Attack outside if rejected");
        // Same lines, different nesting: both programs compile, only one repeats its distance check.
        string outside = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\nfor i in range(10):\n    if enemy_distance <= attack_range:\n        slash(enemy)";
        Require(!TutorialWatcher.Matches(Program(outside),Loop(10,conditional)), "Distance outside loop rejected");
        var blocks = TutorialAnswers.Parse(Loop(10,conditional));
        Require(blocks.Length == 1 && blocks[0].Body.Count == 4 && blocks[0].Body[3].Body.Single().Kind == BlockKind.Slash, "Nested block parser");
        Console.WriteLine($"PASS: {checks} tutorial answer and scope checks");
    }
}
