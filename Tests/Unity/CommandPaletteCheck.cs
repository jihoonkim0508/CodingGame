// Run through Unity MCP Unity_RunCommand in a fresh Defense scene Play Mode session.
// This changes only the current Play Mode session; stop Play Mode afterwards.
using System;
using System.Linq;
using UnityEngine;
using CodingGame.BlockCoding;
using CodingGame.Defense;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        battle.Restart();
        battle.Simulation.GrantBlock(BlockKind.For, 3);
        battle.Simulation.GrantBlock(BlockKind.Number, 3);
        Require(battle.PlaceRobot(4, new Vector3(-9, 0, 2)), "Place shooter");
        battle.SelectRobot(battle.SelectedId);
        battle.DevelopSelected();
        var panel = UnityEngine.Object.FindFirstObjectByType<DefenseCodeEditor>().Panel;
        var source = panel.Palette.Single(e => e.view.Kind == BlockKind.For).view;
        var remaining = panel.Remaining;
        var available = panel.IsAvailable;
        try
        {
            panel.AddFromPalette(source);
            Require(panel.Program.Blocks.Single().Arguments[0].Blocks.Single().Read().Value == "10", "Click fills number");
            panel.Undo();
            Require(panel.Program.Blocks.Count == 0, "Undo removes loop and number together");
            panel.Redo();
            Require(panel.Program.Blocks.Single().Arguments[0].Blocks.Count == 1, "Redo restores number");
            panel.ClearProgram();
            panel.Remaining = k => k == BlockKind.Number ? 0 : remaining(k);
            panel.AddFromPalette(source);
            Require(panel.Program.Blocks.Single().Arguments[0].Blocks.Count == 0, "No number without stock");
            panel.ClearProgram();
            panel.Remaining = remaining;
            panel.IsAvailable = k => k != BlockKind.Number && available(k);
            panel.AddFromPalette(source);
            Require(panel.Program.Blocks.Single().Arguments[0].Blocks.Count == 0, "Unavailable number stays empty");
            panel.ClearProgram();
            panel.IsAvailable = available;
            panel.Program.PlaceFree(source, new Vector2(24, -24));
            var loop = panel.Program.Blocks.Single();
            Require(loop.Arguments[0].Blocks.Single().Read().Value == "10", "Drag fills number");
            loop.Arguments[0].Blocks.Single().SetText("5");
            panel.Program.PlaceFree(loop, new Vector2(80, -80));
            Require(loop.Arguments[0].Blocks.Single().Read().Value == "5", "Move preserves chosen count");
            Require(loop.Body.Insert(source, 0), "Insert nested loop");
            Require(loop.Body.Blocks.Single().Arguments[0].Blocks.Single().Read().Value == "10", "Nested loop fills number");
            result.Log("PASS: for auto-fill, stock, availability, click, drag, nested loop, move, undo/redo");
        }
        finally
        {
            panel.Remaining = remaining;
            panel.IsAvailable = available;
            panel.ClearProgram();
        }
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
