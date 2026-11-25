// Audit of the tutorial overlay (T1). Run in Play Mode in the Defense scene (e.g. DevTest.PlayDefense(), then wait for
// DevTest.Ready) through the Unity MCP run_script tool, entry TutorialAuditCheck.Main: one "FAIL <check>: ..." line per failing
// check, then "AUDIT tutorial: <passed>/<total> passed". The entry returns a Task: the run needs real frames (a few seconds).
// It starts its own 1-1 session, stands the warrior on its spot, opens its editor and runs the lesson from C05 to D08 through the
// director. What a pointer lands on is the event system's own first raycast hit at the middle of a tutorial area.
// T1: a line's focus (TutorialText.focusKeys) only highlights; the pointer passes the dim layer over the step's targets alone.
// "T1 ..." fails until that holds; the "T1-guard ..." checks pass today and must still pass once it does.
// Nothing here is compiled by Unity: public members only, private ones by reflection.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class TutorialAuditCheck
{
    const string T1 = "T1 highlighted-only area takes no pointer", Lit = "T1-guard highlighted area stays lit and framed",
        Target = "T1-guard target area takes the pointer", Both = "T1-guard focus that is also a target takes the pointer",
        Survey = "T1-guard no action step needs a focus-only area",
        Click = "Click palette click lands at the step's demo place", Prep = "Prep lesson preparation supplies its own blocks";
    // Every check, so one the run never reached still counts as failed.
    static readonly string[] Names = { T1, Lit, Target, Both, Survey, Click, Prep };
    const string SlashLine = "Editor.Code.Line.1", PaletteSlash = "Editor.Palette.Slash";

    public static async Task<string> Main()
    {
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        if (!Application.isPlaying || !battle) return Report(new Dictionary<string, string>(), "enter Play mode in the Defense scene first");
        // The run is a coroutine on the battle: every step on the main thread inside the game's own frame.
        var done = new TaskCompletionSource<string>();
        battle.StartCoroutine(Run(battle, done));
        return await done.Task;
    }

    // Steps the lesson run frame by frame; an exception in it ends the run, and the checks it did not reach fail with its message.
    static IEnumerator Run(DefenseBattle battle, TaskCompletionSource<string> done)
    {
        var results = new Dictionary<string, string>(); string stopped = null;
        bool background = Application.runInBackground; Application.runInBackground = true;
        var play = Play(battle, results);
        while (stopped == null)
        {
            try { if (!play.MoveNext()) break; }
            catch (Exception error) { stopped = error.GetType().Name + ": " + error.Message + " @ " + (error.StackTrace ?? "").Split('\n')[0].Trim(); }
            yield return null;
        }
        Application.runInBackground = background;
        if (battle.Tutorial) battle.Tutorial.EditorStop();
        battle.CodeEditor.Close(false);
        done.SetResult(Report(results, stopped));
    }

    // results: check name to why it failed, null when it passed.
    static string Report(Dictionary<string, string> results, string stopped)
    {
        var lines = new List<string>();
        foreach (string name in Names)
        {
            string why = results.TryGetValue(name, out string result) ? result : "not run: " + stopped;
            if (why != null) lines.Add("FAIL " + name + ": " + why);
        }
        lines.Add("AUDIT tutorial: " + (Names.Length - lines.Count) + "/" + Names.Length + " passed");
        return string.Join("\n", lines);
    }

    static IEnumerator Play(DefenseBattle battle, Dictionary<string, string> results)
    {
        void Check(string name, bool ok, string why) => results[name] = ok ? null : why;
        var director = battle.Tutorial; var bridge = director.EditorBridge; var editor = battle.CodeEditor; var panel = editor.Panel;
        string At() => director.EditorStep?.stepId ?? "no step";

        // Every sequence: an action step whose line focuses on something outside all its targets would need the pointer on a
        // highlight, and would never finish once highlights stop taking it.
        // A step finished by something the player only watches (the preview's hit, a kill, a leak, the wave's end) takes no
        // pointer on its focus: 2-5 A05 and 2-6 B01 restart the preview themselves and wait for its hit.
        var watched = new HashSet<string> { TutorialSignals.PreviewHit, TutorialSignals.PreviewSingleHit, TutorialSignals.PreviewMultiHit,
            TutorialSignals.FirstKill, TutorialSignals.FirstAttack, TutorialSignals.Leak, TutorialSignals.WaveCleared,
            TutorialSignals.EnemyInRange, TutorialSignals.KillSwingEnded };
        var needy = new List<string>();
        foreach (var sequence in director.Sequences.Where(s => s))
            foreach (var step in sequence.steps.Where(s => s.kind == TutorialStepKind.ActionGuide && !(s.completionMode == TutorialCompletionMode.ExternalSignal && watched.Contains(s.completionSignal))))
                foreach (string key in step.texts.SelectMany(t => t.focusKeys ?? new string[0]).SelectMany(k => k.Split('|')).Select(k => k.Trim()).Distinct())
                    if (!Covered(step, key)) needy.Add(sequence.sequenceId + " " + step.stepId + " " + key);
        Check(Survey, needy.Count == 0, "focus outside every target of its step: " + string.Join(", ", needy));

        float limit = Time.realtimeSinceStartup + 10;
        while (GameFlow.Loading) { Require(Time.realtimeSinceStartup < limit, "the scene is still loading"); yield return null; }

        // 1-1 as the player has it at C05: the first warrior on its spot, its editor open and empty, the tutorial limiting input.
        battle.EditorStartSession(1, 1, false, new DefenseTestOptions { tutorial = true });
        int warrior = Enumerable.Range(0, battle.RobotDefinitions.Count).First(i => battle.DefinitionRole(i) == RobotRole.Warrior);
        Require(battle.PlaceRobot(warrior, DefenseBattle.FirstWarriorPosition), "the warrior could not stand on 1-1's spot: " + battle.StatusMessage);
        battle.SelectRobot(battle.Simulation.Robots[0].Id); battle.DevelopSelected();
        Require(editor.IsOpen, "the code editor did not open");
        var lesson = director.Sequences.Single(s => s && s.sequenceId == "1-1");
        director.EditorRun(lesson, Array.FindIndex(lesson.steps, s => s.stepId == "C05"));
        // Layout, the canvases' draw order (a raycast skips a graphic not drawn yet) and the bridge's LateUpdate.
        for (int i = 0; i < 3; i++) yield return null;
        Require(At() == "C05", "the lesson is at " + At() + ", not C05");

        // C05 (targets: the code area and the palette; focus: the palette's slash() and the start slot, both inside them).
        var paletteSlash = editor.TutorialPaletteItem(PaletteSlash);
        Require(paletteSlash, "the palette shows no slash() block at C05");
        var hit = Pointer(bridge, PaletteSlash);
        Check(Target, On(hit, paletteSlash), "at 1-1 C05 the pointer on the palette's slash() block landed on " + Name(hit));

        // The player's own way on: a click on the palette block adds it, the lesson sees the code and goes to C09.
        panel.AddFromPalette(paletteSlash.GetComponent<CommandBlockView>());
        limit = Time.realtimeSinceStartup + 10;
        while (At() != "C09") { Require(Time.realtimeSinceStartup < limit, "C05 did not finish with slash() placed (applied " + panel.IsApplied + ", at " + At() + ")"); yield return null; }
        for (int i = 0; i < 3; i++) yield return null;

        // C09 (a line only: no targets; focus: the slash() line).
        var line = editor.Line("1");
        Require(line, "the code has no first line at C09");
        Require(bridge.TryGetArea(SlashLine, out var area), SlashLine + " is not on screen at C09");
        var spotlight = Field<TutorialSpotlight>(bridge, "spotlight");
        bool lit = Field<List<Rect>>(spotlight, "holes").Exists(r => r.Contains(area.center)), framed = Field<List<Rect>>(spotlight, "outlines").Exists(r => r.Contains(area.center));
        Check(Lit, lit && framed, "at 1-1 C09 the slash() line: left undimmed " + lit + ", framed " + framed);
        // The probe point is right only if the pointer lands on the block once the dim layer stops blocking.
        bridge.SetInputLimited(false); var free = Pointer(bridge, SlashLine); bridge.SetInputLimited(true);
        hit = Pointer(bridge, SlashLine);
        string t1 = !On(free, line) ? "the probe is off: with input limiting off the pointer on the slash() line landed on " + Name(free) :
            On(hit, line) ? "at 1-1 C09 the pointer on the highlighted slash() line landed on " + Name(hit) + ", not on the dim layer" : null;
        if (On(free, line) && On(hit, line))
        {
            // The rest of the soft lock: the block dragged from there onto the palette, as the input module would send it.
            Drag(hit, Middle(bridge, TutorialKeys.Palette));
            t1 += "; dragged from there onto the palette, the code has " + panel.Program.StartChain().Count + " line(s) left";
        }
        Check(T1, t1 == null, t1);

        // D08 (target and focus: the preview; it waits for the preview to hit).
        foreach (string next in new[] { "C10", "D06", "D08" })
        {
            director.EditorFinishTyping(); director.EditorNext();
            Require(At() == next, "next led to " + At() + ", not " + next);
        }
        for (int i = 0; i < 3; i++) yield return null;
        var preview = Field<TutorialTargetBinding[]>(bridge, "targets").First(t => t.key == TutorialKeys.Preview).uiTarget;
        hit = Pointer(bridge, TutorialKeys.Preview);
        Check(Both, On(hit, preview), "at 1-1 D08 the pointer on the preview landed on " + Name(hit));
        if (t1 != null && panel.Program.StartChain().Count == 0)
            Check(T1, false, t1 + "; at D08, which waits for the preview to hit, the pointer on the palette's slash() lands on " +
                Name(Pointer(bridge, PaletteSlash)) + ", so the line cannot be put back");
        director.EditorStop();

        // Click: every step with a drag demo, its lesson's code on screen with the demo's place open, nothing selected; a click on
        // the demo's palette block must put the block at the demo's destination (1-3 C08: slash() inside the while).
        var wrong = new List<string>(); int tried = 0;
        foreach (var sequence in director.Sequences.Where(s => s))
        {
            var demos = sequence.steps.Where(s => !string.IsNullOrWhiteSpace(s.dragDemo)).ToList();
            if (demos.Count == 0) continue;
            string answer = sequence.steps.Select(s => s.hintCode).Concat(sequence.steps.Where(s => s.completionSignal == TutorialSignals.ProgramMatches).Select(s => s.expectedCode))
                .Where(c => !string.IsNullOrWhiteSpace(c)).OrderByDescending(c => c.Length).FirstOrDefault() ?? "";
            var id = sequence.sequenceId.Split('-');
            Open(battle, int.Parse(id[0]), int.Parse(id[1]), answer.Contains("slash(") ? RobotRole.Warrior : RobotRole.Shooter, false);
            yield return null;
            foreach (var step in demos)
            {
                var ends = step.dragDemo.Split('|')[0].Split('>').Select(e => e.Trim()).ToArray();
                string where = sequence.sequenceId + " " + step.stepId + " " + step.dragDemo;
                var code = TutorialAnswers.Parse(answer).ToArray();
                var place = ends[1].Substring(TutorialKeys.CodePrefix.Length).Split('.');
                if (place[0] == "Start") code = new CodeBlock[0];
                else if (place[0] == "Arg" && !Empty(code, place)) { wrong.Add(where + ": the answer has no such slot"); continue; }
                panel.LoadProgram(panel.FunctionName, code);
                Set(panel, "selected", null); Set(panel, "selectedIndex", -1);
                editor.TutorialDemo = step.dragDemo;
                yield return null;
                var before = new HashSet<CommandBlockView>(panel.Program.AllBlocks());
                var item = editor.TutorialPaletteItem(ends[0]);
                for (int tab = 0; !item && tab < 8; tab++) { try { panel.ShowCategory(tab); } catch (Exception) { break; } item = editor.TutorialPaletteItem(ends[0]); } // its tab shown
                if (!item) { wrong.Add(where + ": no palette block " + ends[0]); continue; }
                tried++;
                panel.AddFromPalette(item.GetComponent<CommandBlockView>());
                var kind = item.GetComponent<CommandBlockView>().BaseKind;
                var added = panel.Program.AllBlocks().FirstOrDefault(b => !before.Contains(b) && b.BaseKind == kind);
                string landed = Landed(editor, panel, place, added);
                if (landed != null) wrong.Add(where + ": " + landed);
                editor.TutorialDemo = null;
            }
        }
        Check(Click, wrong.Count == 0 && tried > 0, tried + " demo steps clicked; " + string.Join("; ", wrong));

        // Prep: 2-8's prepared code, once with the second shooter on 2-7's code (two breaks) and once with it emptied. The lesson
        // supplies its own code's blocks: what the preparation adds to the stock (owned, and free) is the same both times.
        var runs = new List<string>();
        foreach (bool holding in new[] { true, false })
        {
            Open(battle, 2, 8, RobotRole.Shooter, true);
            var sim = battle.Simulation; var teacher = sim.Robots[0];
            int second = Place(battle, RobotRole.Shooter);
            sim.ApplyProgram(second, "second", holding ? TutorialAnswers.Parse(Code27).ToArray() : new CodeBlock[0]);
            battle.SelectRobot(teacher.Id); battle.DevelopSelected();
            yield return null;
            int owned = sim.Owned(BlockKind.Break), spare = sim.Available(BlockKind.Break);
            editor.PrepareExample("2-8");
            runs.Add($"owned +{sim.Owned(BlockKind.Break) - owned} free {sim.Available(BlockKind.Break) - spare:+0;-0;0}");
        }
        Check(Prep, runs[0] == runs[1], "2-8's preparation with the second shooter on 2-7's code: " + runs[0] + "; with it emptied: " + runs[1]);
        editor.Close(false);
    }

    const string Code27 = "while True:\n    enemy = get_nearest_enemy()\n    while True:\n        if enemy.is_dead():\n            break\n" +
        "        if enemy.distance(get_my_pos()) > my_attack_range():\n            break\n        shoot(enemy)";
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    // A session at the lesson's wave, the tutorial off, one robot of the role standing and its editor open.
    static void Open(DefenseBattle battle, int stage, int wave, RobotRole role, bool stock)
    {
        battle.EditorStartSession(stage, wave, true, new DefenseTestOptions { tutorial = false, tutorialInput = false, unlockAll = !stock, inventoryLimit = stock });
        int id = Place(battle, role);
        battle.SelectRobot(id); battle.DevelopSelected();
        Require(battle.CodeEditor.IsOpen, "the code editor did not open at " + stage + "-" + wave);
    }
    static int Place(DefenseBattle battle, RobotRole role)
    {
        var sim = battle.Simulation;
        int index = Enumerable.Range(0, battle.RobotDefinitions.Count).First(i => battle.DefinitionRole(i) == role);
        for (float x = sim.Setup.Min.X + 1; x < sim.Setup.Max.X - 1; x++)
            for (float y = sim.Setup.Min.Y + 1; y < sim.Setup.Max.Y - 1; y++)
                if (sim.PlacementError(battle.RobotDefinition(index).stats, new System.Numerics.Vector2(x, y)) == null && battle.PlaceRobot(index, new Vector3(x, 0, y)))
                    return sim.Robots.Last().Id;
        throw new Exception("no legal placement for " + role);
    }
    // Empties the answer's slot at Arg.N.i[.j…] (receiver-taking members count their arguments after the receiver).
    static bool Empty(CodeBlock[] code, string[] place)
    {
        var block = LineOf(code, place[1]);
        for (int i = 2; block != null && i < place.Length; i++)
        {
            int skip = block.Kind == BlockKind.Distance || block.Kind == BlockKind.Hp || block.Kind == BlockKind.IsDead || block.Kind == BlockKind.EnemyTypeOf ? 1 : 0;
            int at = int.Parse(place[i]) + skip;
            if (at >= block.Arguments.Count) return false;
            if (i == place.Length - 1) { block.Arguments[at] = null; return true; }
            block = block.Arguments[at];
        }
        return false;
    }
    static CodeBlock LineOf(CodeBlock[] code, string token)
    {
        var steps = token.Split('/').Select(int.Parse).ToArray();
        if (steps[0] > code.Length) return null;
        var block = code[steps[0] - 1];
        foreach (int k in steps.Skip(1)) { if (k > block.Body.Count) return null; block = block.Body[k - 1]; }
        return block;
    }
    // Null when the added block sits at the place: Start (the first line), Body.N (its body's last line), Before.N (line N now),
    // After.N (the line after N), Arg.N.i… (in that slot).
    static string Landed(DefenseCodeEditor editor, CommandCodingPanel panel, string[] place, CommandBlockView added)
    {
        if (!added) return "nothing was added (" + panel.StatusLine.GetComponentInChildren<TMPro.TMP_Text>()?.text + ")";
        CommandBlockView Line(string token) => editor.Line(token);
        string Next(string token) { var p = token.Split('/'); p[p.Length - 1] = (int.Parse(p[p.Length - 1]) + 1).ToString(); return string.Join("/", p); }
        switch (place[0])
        {
            case "Start": return panel.Program.StartChain().FirstOrDefault() == added ? null : "not the first line";
            case "Body": return Line(place[1])?.Body?.Blocks.LastOrDefault() == added ? null : "not at the end of line " + place[1] + "'s body";
            case "Before": return Line(place[1]) == added ? null : "not in front of line " + place[1];
            case "After": return Line(Next(place[1])) == added ? null : "not after line " + place[1];
            case "Arg":
                var slot = (CommandDropZone)typeof(DefenseCodeEditor).GetMethod("TutorialSlot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { TutorialKeys.CodePrefix + string.Join(".", place) });
                return slot && slot.Blocks.Contains(added) ? null : "not in slot " + string.Join(".", place);
        }
        return "unknown place " + place[0];
    }

    // A focus key the step's own targets already open to the pointer: a target itself, a palette block or tab inside the palette
    // card, a code position inside the code area, the road or the floor inside the placement area.
    static bool Covered(TutorialStep step, string key) => step.targetKeys.Contains(key) ||
        key.StartsWith(TutorialKeys.PalettePrefix) && step.targetKeys.Contains(TutorialKeys.Palette) ||
        key.StartsWith(TutorialKeys.CodePrefix) && key != TutorialKeys.Status && step.targetKeys.Contains(TutorialKeys.DropZone) ||
        (key == TutorialKeys.Route || key == TutorialKeys.Floor) && step.targetKeys.Contains(TutorialKeys.Deployment);

    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    // The middle of the key's tutorial area in screen pixels (the bridge's own conversion, backwards).
    static Vector2 Middle(TutorialInputBridge bridge, string key)
    {
        Require(bridge.TryGetArea(key, out var area), key + " is not on screen");
        var canvas = Field<Canvas>(bridge, "canvas");
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            Field<TutorialSpotlight>(bridge, "spotlight").rectTransform.TransformPoint(area.center));
    }
    // What a press at the middle of the key's tutorial area lands on: the event system's first raycast hit there.
    static RaycastResult Pointer(TutorialInputBridge bridge, string key)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Middle(bridge, key) }, hits);
        return hits.Count > 0 ? hits[0] : default;
    }
    static bool On(RaycastResult hit, Component owner) => hit.gameObject && hit.gameObject.transform.IsChildOf(owner.transform);
    static string Name(RaycastResult hit)
    {
        if (!hit.gameObject) return "nothing";
        var at = hit.gameObject.transform;
        return hit.gameObject.GetComponent<TutorialSpotlight>() ? "the dim layer" : (at.parent ? at.parent.name + "/" : "") + at.name;
    }
    // A press on what the pointer landed on, let go at `to`.
    static void Drag(RaycastResult press, Vector2 to)
    {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            pointerPressRaycast = press, pressPosition = press.screenPosition, position = to };
        var dragged = ExecuteEvents.GetEventHandler<IDragHandler>(press.gameObject);
        ExecuteEvents.Execute(dragged, data, ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(dragged, data, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(dragged, data, ExecuteEvents.endDragHandler);
    }
}
