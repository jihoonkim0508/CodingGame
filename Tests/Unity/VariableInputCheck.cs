// Run through Unity CLI in Defense Play Mode.
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TMPro;
using CodingGame.BlockCoding;
using CodingGame.Defense;

public static class VariableInputCheck
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Ref<T>(UnityEngine.Object o, string name) where T : UnityEngine.Object =>
        (T)new SerializedObject(o).FindProperty(name).objectReferenceValue;
    public static string Main()
    {
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>(); battle.Restart();
        if (!battle.DeveloperMode) battle.ToggleDeveloper();
        UnityEngine.Object.FindFirstObjectByType<DefenseInventoryUI>().GrantAll(); battle.ToggleDeveloper();
        Require(battle.PlaceRobot(4, new Vector3(0, 0, 4)), "Robot placed");
        battle.SelectRobot(battle.SelectedId); battle.SendMessage("Update");
        var ring = Ref<DefenseRangeRing>(battle, "rangeRing");
        Require(Ref<LineRenderer>(ring, "line").enabled, "Selected robot range visible");
        battle.DevelopSelected();
        var panel = UnityEngine.Object.FindFirstObjectByType<DefenseCodeEditor>().Panel;
        panel.ClearProgram(); panel.ShowCategory(2);
        Require(panel.VariablePalette.Count == 1 && panel.VariablePalette.ContainsKey("variable"), "One variable palette entry");
        var template = panel.VariablePalette["variable"];
        panel.AddFromPalette(template);
        var variable = panel.Program.Blocks.Single();
        Require(variable.Read().Value == "variable" && variable.Kind == BlockKind.Variable, "Default editable variable");
        variable.SetText("custom");
        Require(panel.Program.Blocks.Single() == variable && variable.Read().Value == "custom", "Undefined typed name retained");
        variable.SetText("custom.");
        Require(Ref<TMP_Dropdown>(variable, "dropdown").gameObject.activeSelf, "Dot shows dropdown");
        variable.SetText("custom");
        Require(!Ref<TMP_Dropdown>(variable, "dropdown").gameObject.activeSelf, "Removing dot hides dropdown");
        panel.LoadProgram("distance_test", new[] {
            new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.Wait, "0", new CodeBlock(BlockKind.Variable, "enemy")) });
        variable = panel.Program.Blocks[1].Arguments[0].Blocks.Single();
        variable.SetText("enemy."); variable.SelectOption(1);
        Require(variable.Kind == BlockKind.Distance, "Distance selected");
        var x = panel.Palette.Single(p => p.view.Kind == BlockKind.PositionX).view;
        var y = panel.Palette.Single(p => p.view.Kind == BlockKind.PositionY).view;
        Require(variable.Arguments[0].Insert(x, 0) && variable.Arguments[1].Insert(y, 0), "Method argument slots usable");
        Require(panel.IsApplied && panel.AppliedSource.Contains("enemy.get_distance(get_pos_x(), get_pos_y())"), "Typed method compiles");
        Require(new SerializedObject(Ref<CommandBlockShape>(variable, "shape")).FindProperty("slot").enumValueIndex == (int)BlockSlotKind.Value, "Returning method has round value shape");
        variable.SetText("enemy");
        Require(variable.Kind == BlockKind.Variable && !Ref<GameObject>(variable, "methodArguments").activeSelf, "Backspace restores plain reference");
        Require(variable.Descendants().Count() == 1, "Hidden arguments no longer consume stock");
        panel.Undo(); variable = panel.Program.Blocks[1].Arguments[0].Blocks.Single();
        Require(variable.Kind == BlockKind.Distance && panel.IsApplied, "Undo restores dot method and arguments");
        panel.Redo(); variable = panel.Program.Blocks[1].Arguments[0].Blocks.Single();
        Require(variable.Kind == BlockKind.Variable && variable.Read().Value == "enemy", "Redo preserves plain name");
        panel.Undo(); panel.ShowCategory(2);
        ScreenCapture.CaptureScreenshot("C:/Users/jihoon/.codex/tmp/variable-dot-input.png");
        return "PASS: range, single variable, editable names, dot/dropdown, numeric method shape, compile, inventory, undo/redo";
    }
    public static string RangeScreenshot()
    {
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        UnityEngine.Object.FindFirstObjectByType<DefenseCodeEditor>().Close();
        battle.SelectRobot(battle.Simulation.Robots.First().Id); battle.SendMessage("Update");
        Require(Ref<LineRenderer>(Ref<DefenseRangeRing>(battle, "rangeRing"), "line").enabled, "Range stays visible with action menu");
        ScreenCapture.CaptureScreenshot("C:/Users/jihoon/.codex/tmp/selected-range.png");
        return "PASS: selected range enabled";
    }
}
