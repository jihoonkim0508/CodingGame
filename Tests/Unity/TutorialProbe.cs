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
    // Presses next n times, finishing the typing first.
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
    // Places robot index at (x, z).
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
        var blocks = CodingGame.Defense.Editor.TutorialAnswers.Parse(code);
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

}

// Plays Stage 1 from the current wave to the end like a player, one tutorial line at a time, and writes a QA log plus a
// screenshot per line (TutorialQA.Start("Temp/QA"), then TutorialQA.Status / Stop). Flags per line: a target the dialogue
// covers, a target that is missing or off screen, text that overflows, a missing arrow, errors, and steps that do not end.
public static class TutorialQA
{
    static string dir, lastKey, lastCode = "";
    static System.Text.StringBuilder log;
    static float next, keySince, lastAct, readySince = -1;
    static bool audited, dismissed;
    static int shot, findings;
    static DefenseBattle Battle => UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    public static string Start(string folder)
    {
        dir = folder; System.IO.Directory.CreateDirectory(dir);
        foreach (var f in System.IO.Directory.GetFiles(dir)) System.IO.File.Delete(f);
        log = new System.Text.StringBuilder(); lastKey = null; shot = findings = 0; readySince = -1;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        UnityEditor.EditorApplication.update -= Tick; UnityEditor.EditorApplication.update += Tick;
        Write("START " + TutorialProbe.State());
        return "started";
    }
    public static string Stop() { Finish("stopped"); return Status(); }
    public static string Status() => "findings " + findings + " shots " + shot + " | " + (lastKey ?? "-") + "\n" +
        string.Join("\n", log.ToString().Split('\n').Reverse().Take(12).Reverse());
    static void Finish(string why)
    {
        UnityEditor.EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; Time.timeScale = 1;
        Write("END " + why);
    }
    static void Write(string line)
    {
        log.AppendLine(line);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "log.txt"), log.ToString());
    }
    static void Flag(string what) { findings++; Write("  !! " + what); }
    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Flag("ERROR [" + lastKey + "] " + message + " @ " + (stack ?? "").Split('\n').FirstOrDefault());
    }
    static void Shot(string name)
    {
        string file = (++shot).ToString("000") + "_" + new string(name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray()) + ".png";
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, file));
    }

    static void Tick()
    {
        try { Step(); } catch (Exception e) { Flag("DRIVER " + e.GetType().Name + ": " + e.Message); Finish("driver error"); }
    }
    static void Step()
    {
        if (!UnityEditor.EditorApplication.isPlaying) { Finish("left play mode"); return; }
        float now = Time.realtimeSinceStartup;
        if (now < next) return;
        next = now + .25f;
        var b = Battle; if (!b || b.Simulation == null) return;
        var sim = b.Simulation; var d = b.Tutorial;
        if (sim.Phase == BattlePhase.Victory || sim.Phase == BattlePhase.Defeat) { Shot("end_" + sim.Phase); Finish(sim.Phase.ToString()); return; }
        var active = Field<TutorialSequence>(d, "active");
        if (d.IsActive && active) { readySince = -1; Tutorial(b, d, active, now); return; }
        // Between tutorials: play the wave out, faster.
        if (readySince < 0) readySince = now;
        if (sim.Phase == BattlePhase.Running) { Time.timeScale = 4; return; }
        Time.timeScale = 1;
        if (now - readySince < 2) return; // a tutorial may start on this phase
        if (sim.Phase == BattlePhase.Ready) { Write("-- wave 1-" + (sim.WaveIndex + 1) + ": start (no tutorial)"); b.StartBattle(); }
        else if (sim.Phase == BattlePhase.Reward) { Shot("reward_1-" + (sim.WaveIndex + 1)); Write("-- wave 1-" + (sim.WaveIndex + 1) + ": claim"); b.ClaimRewards(); }
        readySince = -1;
    }

    static void Tutorial(DefenseBattle b, TutorialDirector d, TutorialSequence active, float now)
    {
        int progress = Field<int>(d, "progressStep"), viewed = Field<int>(d, "viewedStep"), text = Field<int>(d, "viewedText");
        if (progress >= active.steps.Length) return;
        var step = active.steps[progress];
        var view = Field<TutorialDialogueView>(d, "view");
        var alert = Field<TutorialSystemAlert>(d, "alert");
        bool banner = alert && alert.IsShowing;
        string key = active.sequenceId + " " + step.stepId + "#" + text + (view.IsVisible ? "" : banner ? " banner" : " waiting");
        if (key != lastKey) { lastKey = key; keySince = now; audited = false; if (view.IsVisible) dismissed = false; }
        float age = now - keySince;
        if (banner) { if (!audited && age > 1.5f) { audited = true; Shot(key); Write(key); } return; }
        // Free coding with the guide dismissed: no dialogue, but the step still waits on the player's code.
        if (!view.IsVisible && dismissed && step.kind == TutorialStepKind.ActionGuide)
        {
            if (now - lastAct >= 1) { lastAct = now; Act(b, step); }
            return;
        }
        if (!view.IsVisible)
        {
            Time.timeScale = b.Simulation.Phase == BattlePhase.Running ? 4 : 1;
            if (age > 120) { Flag("STUCK waiting for " + step.trigger); Shot(key + "_stuck"); Finish("stuck"); }
            return;
        }
        Time.timeScale = 1;
        if (view.IsTyping) { d.EditorFinishTyping(); return; }
        if (age < 1.4f) return; // entrance, road reveal and pulses settle
        if (!audited) { audited = true; Audit(d, step, view, key); Shot(key); return; }
        if (age > 30) { Flag("STUCK " + key); Shot(key + "_stuck"); Finish("stuck"); return; }
        if (now - lastAct < 1) return;
        lastAct = now;
        if (step.autoAdvance > 0) return;
        bool done = step.kind == TutorialStepKind.Dialogue || Field<System.Collections.Generic.HashSet<string>>(d, "completedStep").Contains(step.stepId);
        if (viewed != progress || done || text + 1 < step.texts.Length) { Call(d, "NextText"); return; }
        if (step.dismissable && !dismissed) { dismissed = true; Call(d, "NextText"); Write("  dismissed highlight"); Shot(key + "_dismissed"); return; }
        Act(b, step);
    }

    static void Act(DefenseBattle b, TutorialStep step)
    {
        string signal = step.completionSignal, target = step.targetKeys.FirstOrDefault() ?? "";
        if (signal == TutorialSignals.ProgramMatches) { TutorialProbe.Load(step.expectedCode); lastCode = step.expectedCode; }
        else if (signal == TutorialSignals.ConditionBuilt) TutorialProbe.LoadWithLoose(lastCode, step.expectedCode);
        else if (signal == TutorialSignals.SlotFilled) TutorialProbe.Load("enemy = get_nearest_enemy()
slash(variable)"); // 1-1 D04: block placed, not named yet
        else if (signal == TutorialSignals.EditorOpened) b.DevelopSelected();
        else if (signal == TutorialSignals.EditorClosed) b.CodeEditor.Close();
        else if (signal == TutorialSignals.BattleStarted) b.StartBattle();
        else if (signal == TutorialSignals.RobotPlaced) TutorialProbe.PlaceBest(Math.Max(1, Field<int>(b, "palette")), 0);
        else if (target.StartsWith(TutorialKeys.RobotButtonPrefix)) b.ChooseRobot(int.Parse(target.Substring(TutorialKeys.RobotButtonPrefix.Length)));
        else if (target.StartsWith("Robot.Unit.")) TutorialProbe.SelectUnit(int.Parse(target.Substring("Robot.Unit.".Length)));
        else if (target == "Battle.ClaimRewards") b.ClaimRewards();
        else if (target == TutorialKeys.Hint) Call(b.CodeEditor, "ToggleHint");
        else Flag("NO DRIVER ACTION for " + step.stepId + " " + signal + " " + target);
    }

    static void Audit(TutorialDirector d, TutorialStep step, TutorialDialogueView view, string key)
    {
        var bridge = d.EditorBridge;
        var panel = bridge.ToLocal(view.Panel);
        var screen = ((RectTransform)Field<TutorialSpotlight>(bridge, "spotlight").transform).rect;
        var line = step.texts[Field<int>(d, "viewedText")];
        bool acting = step.kind == TutorialStepKind.ActionGuide;
        var keys = (acting ? step.targetKeys.Concat(new[] { step.pointerKey }) : Enumerable.Empty<string>()).Concat(line.focusKeys ?? new string[0])
            .Where(k => !string.IsNullOrWhiteSpace(k)).Distinct();
        foreach (var k in keys)
        {
            bool isTarget = acting && (step.targetKeys.Contains(k) || k == step.pointerKey);
            if (!bridge.TryGetArea(k, out var area)) { if (isTarget && k != TutorialKeys.Palette) Flag(key + ": target " + k + " not on screen"); continue; }
            if (!screen.Overlaps(area)) Flag(key + ": " + k + " off screen " + area);
            var cut = Rect.MinMaxRect(Mathf.Max(area.xMin, panel.xMin), Mathf.Max(area.yMin, panel.yMin), Mathf.Min(area.xMax, panel.xMax), Mathf.Min(area.yMax, panel.yMax));
            float covered = cut.width > 0 && cut.height > 0 ? cut.width * cut.height / Mathf.Max(1, area.width * area.height) : 0;
            if (covered > (isTarget ? 0 : .35f)) Flag(key + ": dialogue covers " + (isTarget ? "target " : "focus ") + k + " " + covered.ToString("P0"));
        }
        var dialogue = view.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name == "Dialogue");
        dialogue.ForceMeshUpdate();
        if (dialogue.isTextOverflowing || dialogue.preferredHeight > dialogue.rectTransform.rect.height + 1)
            Flag(key + ": text overflows (" + dialogue.preferredHeight.ToString("0") + " > " + dialogue.rectTransform.rect.height.ToString("0") + ")");
        var pointer = Field<TutorialPointer>(d, "pointer");
        if (acting && !string.IsNullOrWhiteSpace(step.pointerKey) && step.pointerDelay <= 0 && !pointer.IsVisible) Flag(key + ": no arrow for " + step.pointerKey);
        string where = view.IsCentered ? "centre" : view.AtTop ? "top" : "bottom";
        Write(key + " [" + where + (acting ? ", action " + (step.completionSignal ?? step.completionMode.ToString()) : "") + "] " + line.text.Replace("\n", " "));
    }
}
