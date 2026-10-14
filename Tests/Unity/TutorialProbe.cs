// Play Mode driver for the Stage 1 tutorial (Unity MCP run_script, entry TutorialProbe.<Method>). Acts like the player through the
// game's own entry points (no input-limit bypass) and reports what the tutorial shows.
using System;
using System.Linq;
using System.Reflection;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using UnityEngine;

public static class TutorialProbe
{
    static DefenseBattle Battle => UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
    static TutorialDirector Director => Battle.Tutorial;
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    public static string State()
    {
        var b = Battle; var d = Director; var sim = b.Simulation;
        var active = Field<TutorialSequence>(d, "active");
        int progress = Field<int>(d, "progressStep"), viewed = Field<int>(d, "viewedStep");
        var step = active && progress < active.steps.Length ? active.steps[progress] : null;
        var view = Field<TutorialDialogueView>(d, "view");
        var alert = Field<TutorialSystemAlert>(d, "alert");
        var pointer = Field<TutorialPointer>(d, "pointer");
        var dialogue = view.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name == "Dialogue");
        return $"wave {sim.WaveIndex + 1} phase {sim.Phase} | seq {(active ? active.sequenceId : "-")} step {step?.stepId ?? "-"} (viewed {viewed}) " +
            $"| view {(view.IsVisible ? "on" : "off")} banner {(alert && alert.IsShowing ? "on" : "off")} arrow {(pointer && pointer.IsVisible ? "on" : "off")} " +
            $"| active {d.IsActive} | robots {sim.Robots.Count} editor {(b.CodeEditor.IsOpen ? b.CodeEditor.EditingId.ToString() : "closed")} " +
            $"| text: {(view.IsVisible ? dialogue.text : alert && alert.IsShowing ? "[banner]" : "")}";
    }
    public static string Next() { Call(Director, "NextText"); return State(); }
    // Presses next (finishing the typing first) until the step changes into an action, a hidden wait, or n presses.
    public static string Skip(int n)
    {
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < n; i++)
        {
            Director.EditorFinishTyping(); Call(Director, "NextText");
            log.AppendLine(State());
        }
        return log.ToString();
    }
    public static string FinishTyping() { Director.EditorFinishTyping(); return State(); }
    public static string ClickRobotButton(int index) { Battle.ChooseRobot(index); return State(); }
    // Places at the deployment cell nearest the route that covers it best (like a careful player).
    public static string PlaceRobot(int index, float x, float z) { bool ok = Battle.PlaceRobot(index, new Vector3(x, 0, z)); return "placed " + ok + " | " + Battle.StatusMessage + " | " + State(); }
    // Places robot `index` on the free cell whose attack range covers most of the route, skipping `skip` best cells.
    public static string PlaceBest(int index, int skip)
    {
        var b = Battle; var sim = b.Simulation; var spec = b.RobotDefinition(index).stats;
        var samples = new System.Collections.Generic.List<System.Numerics.Vector2>();
        foreach (var route in sim.Setup.Routes)
            for (int i = 1; i < route.Points.Length; i++)
                for (float t = 0; t < 1; t += .05f) samples.Add(System.Numerics.Vector2.Lerp(route.Points[i - 1], route.Points[i], t));
        var cells = new System.Collections.Generic.List<System.Numerics.Vector2>();
        for (float x = sim.Setup.Min.X + .5f; x < sim.Setup.Max.X; x += 1)
            for (float y = sim.Setup.Min.Y + .5f; y < sim.Setup.Max.Y; y += 1)
            { var p = new System.Numerics.Vector2(x, y); if (sim.PlacementError(spec, p) == null) cells.Add(p); }
        var best = cells.OrderByDescending(p => samples.Count(q => System.Numerics.Vector2.Distance(p, q) <= spec.range)).Skip(skip).First();
        return PlaceRobot(index, best.X, best.Y);
    }
    public static string SelectUnit(int unit)
    {
        var bot = Battle.Simulation.Robots.OrderBy(r => r.Id).ElementAt(unit);
        if (!Battle.Tutorial.AllowsWorldPoint(new Vector3(bot.Position.X, 0, bot.Position.Y))) return "click on robot refused | " + State();
        Battle.SelectRobot(bot.Id); return State();
    }
    public static string Develop() { Battle.DevelopSelected(); return State(); }
    public static string CloseEditor() { Battle.CodeEditor.Close(); return State(); }
    public static string StartBattle() { Battle.StartBattle(); return State(); }
    public static string Claim() { Battle.ClaimRewards(); return State(); }
    public static string Speed(float scale) { Time.timeScale = scale; return State(); }
    public static string Load(string code)
    {
        var panel = Battle.CodeEditor.Panel;
        var blocks = Parse(code);
        panel.LoadProgram(panel.FunctionName, blocks);
        return "applied " + panel.IsApplied + " | " + State();
    }
    public static string LoadWithLoose(string code, string loose)
    {
        Load(code);
        var panel = Battle.CodeEditor.Panel;
        var source = panel.Palette.First(p => p.view.BaseKind == BlockKind.Comparison).view;
        panel.Program.PlaceFree(source, new Vector2(300, -260));
        var comparison = panel.Program.LooseRoots().First();
        Invoke(comparison.Arguments[0], "Load", (System.Collections.Generic.IReadOnlyList<CodeBlock>)new[] { new CodeBlock(BlockKind.Variable, "enemy_distance") });
        Invoke(comparison.Arguments[1], "Load", (System.Collections.Generic.IReadOnlyList<CodeBlock>)new[] { new CodeBlock(BlockKind.MyAttackRange) });
        var dropdown = Field<TMPro.TMP_Dropdown>(comparison, "dropdown");
        dropdown.value = Array.IndexOf(PythonTreeCompiler.Comparisons, "<=");
        Call(panel, "Changed");
        return "loose: " + string.Join(", ", Battle.CodeEditor.LooseExpressions()) + " | " + State();
    }
    public static string Area(string key)
    {
        var bridge = Director.EditorBridge;
        return bridge.TryGetArea(key, out var rect) ? key + " " + rect : key + " none";
    }
    public static string Hint() => Battle.CodeEditor.Panel != null ? "hint popup " + Field<GameObject>(Battle.CodeEditor, "hintPopup").activeSelf + " button " + Field<UnityEngine.UI.Button>(Battle.CodeEditor, "hintButton").gameObject.activeSelf : "";
    public static string Inventory() => string.Join(", ", Battle.Simulation.Inventory.Where(p => p.Value > 0).Select(p => p.Key + "×" + p.Value)) +
        " | robots " + string.Join(",", Battle.Simulation.RobotInventory.Select(p => p.Key + "×" + p.Value));

    // Tiny reader for the tutorial's answers: "name = get_nearest_enemy()", "x = enemy.distance(get_my_pos())",
    // "name = my_attack_range()", "slash(name)", "if a <= b:" with an indented body.
    static CodeBlock[] Parse(string code)
    {
        var lines = code.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        int index = 0;
        return Suite(lines, ref index, 0);
    }
    static CodeBlock[] Suite(System.Collections.Generic.List<string> lines, ref int index, int indent)
    {
        var result = new System.Collections.Generic.List<CodeBlock>();
        while (index < lines.Count)
        {
            string raw = lines[index]; int depth = raw.Length - raw.TrimStart().Length;
            if (depth < indent) break;
            string line = raw.Trim(); index++;
            if (line.StartsWith("if ") && line.EndsWith(":"))
            {
                var block = new CodeBlock(BlockKind.If, "0", Condition(line.Substring(3, line.Length - 4)));
                block.Body.AddRange(Suite(lines, ref index, depth + 4));
                result.Add(block);
            }
            else if (line.StartsWith("slash(")) result.Add(new CodeBlock(BlockKind.Slash, "0", Value(line.Substring(6, line.Length - 7))));
            else { var parts = line.Split(new[] { " = " }, 2, StringSplitOptions.None); result.Add(new CodeBlock(BlockKind.DeclareVariable, parts[0], Value(parts[1]))); }
        }
        return result.ToArray();
    }
    static CodeBlock Condition(string text)
    {
        var parts = text.Split(new[] { " <= " }, StringSplitOptions.None);
        return new CodeBlock(BlockKind.Comparison, "<=", Value(parts[0]), Value(parts[1]));
    }
    static CodeBlock Value(string text)
    {
        if (text == "get_nearest_enemy()") return new CodeBlock(BlockKind.NearestEnemy);
        if (text == "my_attack_range()") return new CodeBlock(BlockKind.MyAttackRange);
        if (text == "get_my_pos()") return new CodeBlock(BlockKind.MyPos);
        if (text.EndsWith(".distance(get_my_pos())"))
            return new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, text.Substring(0, text.IndexOf('.'))), new CodeBlock(BlockKind.MyPos));
        return new CodeBlock(BlockKind.Variable, text);
    }
}
