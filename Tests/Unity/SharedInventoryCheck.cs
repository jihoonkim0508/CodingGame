// Run in Defense Play Mode using unity command run_script --entry SharedInventoryCheck.Main.
// Covers the editor path: a syntax error empties a robot's applied code, but its editor blocks stay reserved from the shared stock.
using System;
using System.Linq;
using UnityEngine;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using Point = System.Numerics.Vector2;

public static class SharedInventoryCheck
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static int Place(DefenseBattle battle, int index)
    {
        var sim = battle.Simulation;
        for (float x = sim.Setup.Min.X + 1; x < sim.Setup.Max.X - 1; x++)
            for (float y = sim.Setup.Min.Y + 1; y < sim.Setup.Max.Y - 1; y++)
                if (sim.PlacementError(battle.RobotDefinition(index).stats, new Point(x, y)) == null &&
                    battle.PlaceRobot(index, new Vector3(x, 0, y))) return sim.Robots.Last().Id;
        throw new Exception("No legal placement");
    }
    public static string Main()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        battle.EditorStartSession(2, 1, true, new DefenseTestOptions());
        var sim = battle.Simulation;
        int warrior = Enumerable.Range(0, 6).Single(i => battle.DefinitionRole(i) == RobotRole.Warrior);
        sim.GrantRobot(RobotRole.Warrior, 2);
        foreach (var kind in new[] { BlockKind.DeclareVariable, BlockKind.NearestEnemy, BlockKind.Slash }) sim.GrantBlock(kind, 1);
        int a = Place(battle, warrior), b = Place(battle, warrior);
        int slashFree = sim.Available(BlockKind.Slash), declareFree = sim.Available(BlockKind.DeclareVariable);
        var editor = battle.CodeEditor; var panel = editor.Panel;
        editor.Open(a);
        // slash with an empty target: a syntax error, so robot a acts on nothing.
        panel.LoadProgram("robot_" + a, new[] { new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)), new CodeBlock(BlockKind.Slash, "0") });
        Require((sim.FindRobot(a).Program?.BlockCount(BlockKind.Slash) ?? 0) == 0, "Syntax error empties the applied code");
        Require(sim.Available(BlockKind.Slash) == slashFree - 1 && sim.Available(BlockKind.DeclareVariable) == declareFree - 1, "Unfinished editor blocks stay reserved");
        editor.Open(b);
        Require(panel.Remaining(BlockKind.Slash) == slashFree - 1, "Another robot's palette excludes them");
        editor.Close(false);
        editor.ClearRobotProgram(a);
        Require(sim.Available(BlockKind.Slash) == slashFree && sim.Available(BlockKind.DeclareVariable) == declareFree, "Clearing the robot releases them");
        return "PASS: syntax error keeps editor blocks reserved, other robot sees less, clear releases";
    }
}
