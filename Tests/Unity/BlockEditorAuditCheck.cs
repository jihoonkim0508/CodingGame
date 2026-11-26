// Audit of the block editor (B1-B10). Run in Play Mode in the Defense scene through the Unity MCP run_script tool, entry
// BlockEditorAuditCheck.Main: one "FAIL Bn ...: ..." line per failing check, then "AUDIT blocks: <passed>/<total> passed".
// Each check starts its own Stage 2 session (tutorial off, two warriors, 50 more of every block; a check that counts stock
// sets it exactly) and fails today for the reason in its comment. A message that starts with "setup:" or "guard:" is not the
// bug: the scene or the check itself is off, or behaviour that must stay was broken.
// Nothing here is compiled by Unity: public members only, private ones by reflection. Drags go through the panel's own
// BeginDrag / MoveDrag / FinishDrag, like Tests/BlockCoding/Snap.playmode.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using Point = System.Numerics.Vector2;

public static class BlockEditorAuditCheck
{
    sealed class Failed : Exception { public Failed(string message) : base(message) { } }
    static void Require(bool ok, string message) { if (!ok) throw new Failed(message); }
    static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static DefenseBattle battle; static DefenseSimulation sim; static DefenseCodeEditor editor;
    static CommandCodingPanel panel; static CommandDropZone program; static GraphicRaycaster raycaster;
    static int a, b; // two warriors
    // Places in the code area, from its top-left corner (an anchoredPosition): the start anchor, 14 px off it, far from it.
    static readonly Vector2 Start = new Vector2(24, -24), Near = new Vector2(38, -34), Far = new Vector2(340, -260);

    static int total, passed;
    static readonly List<string> report = new List<string>();

    public static string Main()
    {
        if (!Application.isPlaying) return "Enter Play Mode in the Defense scene first";
        battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        if (!battle) return "Defense scene required";
        try
        {
            Run("B1 invalid function name: screen and robot agree", InvalidName);
            Run("B2 renaming through another variable's name", RenameThroughAnotherName);
            Run("B3 palette lists every owned kind", PaletteListsOwnedKinds);
            Run("B4 palette click appends to the program", ClickAppends);
            Run("B5 start snap with loose blocks elsewhere", StartSnapWithLooseBlocks);
            Run("B6 landing preview is where the drop lands", PreviewIsTheLanding);
            Run("B7 drag copy keeps the operator", DragCopyKeepsOperator);
            Run("B9 a program taller than the code area scrolls into reach", TallProgramScrolls);
            Run("B11 the code area pans, zooms and points to blocks out of view", PanZoomAndOffscreenNote);
        }
        finally { battle.Restart(); }
        report.Add("AUDIT blocks: " + passed + "/" + total + " passed");
        return string.Join("\n", report);
    }
    static void Run(string name, Action check)
    {
        total++;
        try { Session(); check(); passed++; }
        catch (Failed e) { report.Add("FAIL " + name + ": " + OneLine(e.Message)); }
        catch (Exception e) { report.Add("FAIL " + name + ": threw " + e.GetType().Name + ": " + OneLine(e.Message) + " " + OneLine((e.StackTrace ?? "").Trim().Split('\n')[0])); }
        finally { if (panel) panel.EndDrag(); }
    }
    static string OneLine(string text) => text.Replace("\r", "").Replace("\n", " | ");

    // --- B1 ---
    // With a function name Python rejects (the player types `for`), the status line says "로봇이 행동하지 않음" while the robot
    // keeps the code applied before, and 비우기 changes nothing on the robot either: CommandCodingPanel.ClearTarget applies
    // the empty program under the same bad name and swallows the error.
    static void InvalidName()
    {
        Open(a); Load(new CodeBlock(BlockKind.Slash));
        Require(panel.IsApplied && Slashes(a) == 1, "setup: slash() is applied to the robot: " + Status());
        panel.FunctionName = "for";
        string typed = Disagreement();
        editor.RestoreAutomatic();
        Require(program.Blocks.Count == 0, "setup: 비우기 emptied the editor");
        string cleared = Disagreement();
        Require(typed == null && cleared == null, "name typed as `for`: " + (typed ?? "ok") + "; then 비우기: " + (cleared ?? "ok"));
    }
    // What the status line claims against what robot a holds; null when they agree.
    static string Disagreement()
    {
        var applied = sim.FindRobot(a).Program;
        bool idle = Status().Contains("행동하지 않음"), empty = applied == null || applied.IsEmpty;
        return idle && !empty ? "the status line says \"" + Status() + "\" but the robot still holds slash()" : null;
    }

    // --- B2 ---
    // Backspacing `enemy_distance` passes through `enemy`: its uses are renamed into the other variable's name and from then on
    // no longer follow it (CommandCodingPanel.RenameVariable renames by name, and stops once two declarations share one).
    static void RenameThroughAnotherName()
    {
        Open(a);
        Load(new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)),
            new CodeBlock(BlockKind.DeclareVariable, "enemy_distance",
                new CodeBlock(BlockKind.Distance, "0", new CodeBlock(BlockKind.Variable, "enemy"), new CodeBlock(BlockKind.MyPos))),
            new CodeBlock(BlockKind.Wait, "0", new CodeBlock(BlockKind.Variable, "enemy_distance")),
            new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, "enemy")));
        var chain = program.StartChain();
        Require(chain.Count == 4, "setup: four lines loaded: " + Program());
        CommandBlockView declaration = chain[1], receiver = Argument(chain[1]), use = Argument(chain[2]), target = Argument(chain[3]);
        Require(use.VariableName == "enemy_distance" && receiver.VariableName == "enemy" && target.VariableName == "enemy", "setup: names as loaded");
        // enemy_distanc, enemy_distan, ... enemy_, enemy, enem: one backspace at a time, as typed.
        for (string name = "enemy_distanc"; name.Length >= 4; name = name.Substring(0, name.Length - 1)) declaration.SetText(name);
        Require(declaration.VariableName == "enem", "setup: the name was typed down to enem: " + declaration.VariableName);
        Require(use.VariableName == "enem" && receiver.VariableName == "enemy" && target.VariableName == "enemy",
            "enemy_distance backspaced through enemy to enem: its use reads wait(" + use.VariableName + ") (expected enem), the other variable's uses " +
            receiver.VariableName + ".distance() and shoot(" + target.VariableName + ") (expected enemy)");
    }

    // --- B3 ---
    // An unlocked kind stays on every robot's palette however many robots use it (blocks are unlimited); a locked one is
    // off it: CommandCodingPanel.ShowCategory hides on IsAvailable.
    static void PaletteListsOwnedKinds()
    {
        sim.Inventory[BlockKind.Slash] = 1; sim.Inventory.Remove(BlockKind.Boom);
        Open(a); Load(new CodeBlock(BlockKind.Slash), new CodeBlock(BlockKind.Slash));
        Require(panel.IsApplied && Listed(BlockKind.Slash), "robot a uses two slash() with one granted, and slash() stays listed: " + Status());
        Require(!Listed(BlockKind.Boom), "guard: boom(), not owned, stays off the palette");
        Open(b);
        Require(!Listed(BlockKind.Boom), "guard: boom(), not owned, stays off the palette");
        Require(Listed(BlockKind.Slash), "slash(), used by another robot, is not listed");
    }
    // Whether a palette block is listed with its tab shown.
    static bool Listed(BlockKind kind) => Palette(kind).gameObject.activeSelf;

    // --- B4 ---
    // A click on a palette block adds it under the block placed last, wherever that lies: CommandDropZone.Insert takes
    // blocks.LastOrDefault(), and the list is in placement order.
    static void ClickAppends()
    {
        Open(a);
        // The line placed last sits in the middle of the program.
        Load(new CodeBlock(BlockKind.Slash), new CodeBlock(BlockKind.Buff));
        var top = program.StartChain()[0];
        Require(program.PlaceFree(Palette(BlockKind.Wait), top.Rect.anchoredPosition + new Vector2(0, -top.preferredHeight)) && Program() == "Slash, Wait, Buff",
            "setup: wait() dropped between the two lines: " + Program());
        var added = Click(BlockKind.Shot);
        Require(program.StartChain().LastOrDefault() == added, "with the last placed line in the middle, the clicked shoot() made the program " + Program() + " (expected Slash, Wait, Buff, Shot)");
        // The block placed last lies loose, away from the program.
        Load(new CodeBlock(BlockKind.Slash));
        Require(program.PlaceFree(Palette(BlockKind.Wait), Far) && program.LooseRoots().Count() == 1, "setup: a loose wait() beside the program");
        added = Click(BlockKind.Shot);
        Require(program.StartChain().LastOrDefault() == added, "with a loose block placed last, the clicked shoot() made the program " + Program() + " (expected Slash, Shot)");
        // The same before the program has a line: the clicked line starts it.
        Load();
        Require(program.PlaceFree(Palette(BlockKind.Wait), Far) && program.LooseRoots().Count() == 1, "setup: a loose wait() and no program");
        added = Click(BlockKind.Shot);
        Require(program.StartChain().LastOrDefault() == added, "with only a loose block in the code area, the clicked shoot() made the program " + Program() + " (expected Shot)");
    }
    static CommandBlockView Click(BlockKind kind)
    {
        var before = program.Blocks.ToList();
        panel.AddFromPalette(Palette(kind)); Layout();
        var added = program.Blocks.Except(before).ToList();
        Require(added.Count == 1, "setup: the click added " + added.Count + " blocks: " + Status());
        return added[0];
    }

    // --- B5 ---
    // A first line dropped near the start anchor snaps onto it only while the code area holds nothing else
    // (CommandDropZone.SnapsToFirstPosition): with a loose block anywhere it stays where it was let go, outside the program.
    static void StartSnapWithLooseBlocks()
    {
        Open(a); Load();
        Require(program.PlaceFree(Palette(BlockKind.Wait), Far) && program.StartChain().Count == 0, "setup: a loose wait() and no program");
        var e = Begin(Palette(BlockKind.Slash)); Move(e, Near);
        bool previewed = panel.SnapPreviewShown;
        Drop(e);
        var line = program.Blocks.FirstOrDefault(block => block.BaseKind == BlockKind.Slash);
        Require(line, "setup: slash() was dropped into the code area");
        Require(program.StartChain().FirstOrDefault() == line, "slash() let go 14 px from the start anchor stayed at " + At(line.Rect) + " (expected " + Start +
            "), outside the program; snap preview " + (previewed ? "shown" : "not shown"));
        Require(previewed, "the drop snapped to the start, but no snap preview was shown before it");
        // The same with the loose line right under the start slot: the dropped line takes the start, not that line's row.
        Load();
        Require(program.PlaceFree(Palette(BlockKind.Wait), new Vector2(30, -80)) && program.StartChain().Count == 0, "setup: a loose wait() under the start slot");
        e = Begin(Palette(BlockKind.Slash)); Move(e, Near); Drop(e);
        line = program.Blocks.FirstOrDefault(block => block.BaseKind == BlockKind.Slash);
        Require(line && program.StartChain().FirstOrDefault() == line, "with a loose line right under the start slot, slash() let go 14 px from the anchor is at " +
            (line ? At(line.Rect).ToString() : "nowhere") + " (expected " + Start + "); the program is " + Program());
    }

    // --- B6 ---
    // Over the first line of a chain the landing copy is drawn one block above that line, but the drop sets the block on the
    // line's own row and moves the chain down: CommandDropZone.OpenGap (preview) against PlaceChain (drop).
    static void PreviewIsTheLanding()
    {
        Open(a); Load(new CodeBlock(BlockKind.Slash));
        var first = program.StartChain()[0];
        // Under a chain the two agree today, which also proves the measuring.
        var under = Carry(BlockKind.Buff, first.Rect.anchoredPosition + new Vector2(0, -first.preferredHeight));
        Require(Program() == "Slash, Buff", "setup: buff() went under slash(): " + Program());
        Require(Vector2.Distance(under.preview, under.landed) < 1, "guard: under a chain the preview was at " + under.preview + ", the block landed at " + under.landed);
        // On the chain's first line, a little above and to the right of it.
        var over = Carry(BlockKind.Wait, first.Rect.anchoredPosition + new Vector2(6, 10));
        Require(Program() == "Wait, Slash, Buff", "setup: wait() went on top of the chain: " + Program());
        Require(Vector2.Distance(over.preview, over.landed) < 1, "at the top of the program the landing preview was at " + over.preview + ", the block landed at " + over.landed);
        // The same on a chain parked away from the start.
        Load();
        Require(program.PlaceFree(Palette(BlockKind.Slash), Far), "setup: a parked slash()");
        over = Carry(BlockKind.Wait, Far + new Vector2(6, 10));
        var parked = program.LooseRoots().ToList();
        Require(parked.Count == 1 && Kinds(program.Group(parked[0])) == "Wait, Slash", "setup: wait() went on top of the parked chain");
        Require(Vector2.Distance(over.preview, over.landed) < 1, "at the top of a parked chain the landing preview was at " + over.preview + ", the block landed at " + over.landed);
        // Held there, and after a drag that ends without a drop, the program stays on the start.
        Load(new CodeBlock(BlockKind.Slash));
        first = program.StartChain()[0];
        var e = Begin(Palette(BlockKind.Wait)); Move(e, Start + new Vector2(6, 10));
        bool runs = program.StartChain().FirstOrDefault() == first;
        panel.EndDrag();
        Require(runs && program.StartChain().FirstOrDefault() == first && Vector2.Distance(At(first.Rect), Start) < .01f,
            "guard: a block held on top of the program, then let go of without a drop: the program is " + Program() + ", its first line at " + At(first.Rect));
    }
    // Drags a palette block so that its top-left corner is at `at` and lets go: where the landing copy was shown, where the block is.
    static (Vector2 preview, Vector2 landed) Carry(BlockKind kind, Vector2 at)
    {
        var before = program.Blocks.ToList();
        var e = Begin(Palette(kind)); Move(e, at);
        var marks = Get<List<CommandBlockView>>(panel, "landing");
        Require(panel.SnapPreviewShown && marks.Count > 0 && marks[0] && marks[0].gameObject.activeSelf, "setup: no landing preview for " + kind + " at " + at);
        var preview = At(marks[0].Rect);
        Drop(e);
        var added = program.Blocks.Except(before).ToList();
        Require(added.Count == 1, "setup: the drop added " + added.Count + " blocks");
        return (preview, At(added[0].Rect));
    }

    // --- B7 ---
    // The copy under the hand and the landing copy of a comparison or a calculation show the first sign of the picker, == or +,
    // whatever the block holds: a copy is bound as a new block (CommandBlockView.Copy), and Bind refills the picker at 0.
    static void DragCopyKeepsOperator()
    {
        Open(a);
        Load(new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, "<=", Number("1"), Number("2"))),
            new CodeBlock(BlockKind.Wait, "0", new CodeBlock(BlockKind.Arithmetic, "*", Number("2"), Number("3"))));
        var chain = program.StartChain();
        Require(chain.Count == 2, "setup: two lines loaded: " + Program());
        CommandBlockView comparison = Argument(chain[0]), calculation = Argument(chain[1]);
        Require(Sign(comparison) == "<=" && Sign(calculation) == "*", "setup: the placed blocks show <= and *: " + Sign(comparison) + " " + Sign(calculation));
        string alone = CopySigns(comparison, copy => copy), calculated = CopySigns(calculation, copy => copy), inLine = CopySigns(chain[0], Argument);
        Require(alone == "<= <=" && calculated == "* *" && inLine == "<= <=", "the copies (under the hand, landing) of <= show " + alone +
            "; of * show " + calculated + "; of the if line holding <= show " + inLine);
    }
    // The signs on the copy under the hand and on the landing copy while source is dragged; pick: the block to read in a copy.
    static string CopySigns(CommandBlockView source, Func<CommandBlockView, CommandBlockView> pick)
    {
        Begin(source);
        string signs = Sign(pick(Get<List<CommandBlockView>>(panel, "ghosts")[0])) + " " + Sign(pick(Get<List<CommandBlockView>>(panel, "landing")[0]));
        panel.EndDrag();
        return signs;
    }
    static string Sign(CommandBlockView block) => block.OptionRect.GetComponent<TMP_Dropdown>().captionText.text;
    static CodeBlock Number(string value) => new CodeBlock(BlockKind.Number, value);

    // --- B9 ---
    // 2-8's finished code (ten lines) is taller than the code area. A drag on its empty space pans it up (the wheel zooms since
    // 2026-11-25, B11): its last line,
    // shoot(enemy), is then inside the area and a press there reaches it; a palette click still adds a line at the program's
    // end (93d7345 refused it once the code reached the area's bottom), and a drag under the last line lands where its
    // preview showed, scrolled.
    static void TallProgramScrolls()
    {
        Open(a);
        CodeBlock Enemy() => new CodeBlock(BlockKind.Variable, "enemy");
        CodeBlock Leave(CodeBlock condition) { var check = new CodeBlock(BlockKind.If, "0", condition); check.Body.Add(new CodeBlock(BlockKind.Break)); return check; }
        var inner = new CodeBlock(BlockKind.While);
        inner.Body.Add(Leave(new CodeBlock(BlockKind.IsDead, "0", Enemy())));
        inner.Body.Add(Leave(new CodeBlock(BlockKind.Comparison, ">", new CodeBlock(BlockKind.Distance, "0", Enemy(), new CodeBlock(BlockKind.MyPos)), new CodeBlock(BlockKind.MyAttackRange))));
        inner.Body.Add(new CodeBlock(BlockKind.Shot, "0", Enemy()));
        var tanks = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, "!=", new CodeBlock(BlockKind.EnemyTypeOf, "0", Enemy()), new CodeBlock(BlockKind.EnemyTypeName, "Tank")));
        tanks.Body.Add(new CodeBlock(BlockKind.Continue));
        var outer = new CodeBlock(BlockKind.While);
        outer.Body.Add(new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)));
        outer.Body.Add(tanks);
        outer.Body.Add(inner);
        Load(outer);
        var last = editor.Line("1/3/3");
        Require(last && last.BaseKind == BlockKind.Shot, "setup: 2-8's code has shoot(enemy) as line 1/3/3: " + Program());
        var scroll = program.GetComponentInParent<ScrollRect>();
        var view = scroll ? (scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform) : (RectTransform)program.transform.parent;
        Require(Bottom(last.Rect) < Bottom(view) - 1, "setup: the code is taller than the code area (its last line is in view unscrolled)");
        Require(scroll && scroll.vertical && scroll.content == program.Rect, "the code area has no vertical ScrollRect on the program");
        // A drag on the program's empty space bubbles up to the code area's ScrollRect (sent to the program itself: no frame has
        // drawn the editor yet), up by as much as the last line is below the area.
        Pan(Vector2.up * (Bottom(view) - Bottom(last.Rect) + 20));
        Layout();
        Require(Bottom(last.Rect) >= Bottom(view) - 1 && Top(last.Rect) <= Top(view) + 1, "after the pan (content at " + program.Rect.anchoredPosition +
            "), shoot(enemy) is still outside the code area");
        // A press there reaches it: inside the area's mask, on the line's own raycast graphic.
        var corners = new Vector3[4]; last.Rect.GetWorldCorners(corners);
        var at = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, (corners[0] + corners[2]) / 2);
        var graphic = last.GetComponent<Graphic>();
        Require(RectTransformUtility.RectangleContainsScreenPoint(view, at, raycaster.eventCamera) && graphic && graphic.raycastTarget &&
            graphic.Raycast(at, raycaster.eventCamera), "a press on shoot(enemy), scrolled into view, does not reach it");
        // A palette click at the bottom of the tall program adds the line at its end.
        int lines = program.StartChain().Count;
        panel.AddFromPalette(Palette(BlockKind.Slash)); Layout();
        Require(program.StartChain().Count == lines + 1 && program.StartChain().Last().BaseKind == BlockKind.Slash,
            "a palette click with the code past the area's bottom added nothing: " + Status());
        // Scrolled, a drag under the program's last line lands where its preview was.
        scroll.verticalNormalizedPosition = 0; Layout();
        var end = program.StartChain().Last();
        var under = Carry(BlockKind.Wait, end.Rect.anchoredPosition + new Vector2(0, -end.preferredHeight));
        Require(program.StartChain().Last().BaseKind == BlockKind.Wait, "scrolled, wait() dragged under the last line did not join the program: " + Program());
        Require(Vector2.Distance(under.preview, under.landed) < 1, "scrolled, the landing preview was at " + under.preview + ", the block landed at " + under.landed);
    }
    // A drag of screen distance by on the program's empty space.
    static void Pan(Vector2 by)
    {
        var start = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, program.Rect.TransformPoint(program.Rect.rect.center));
        var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = start, pressPosition = start };
        ExecuteEvents.ExecuteHierarchy(program.gameObject, e, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.ExecuteHierarchy(program.gameObject, e, ExecuteEvents.beginDragHandler);
        e.position = start + by; e.delta = by;
        ExecuteEvents.ExecuteHierarchy(program.gameObject, e, ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(program.gameObject, e, ExecuteEvents.endDragHandler);
    }

    // --- B11 ---
    // The code area pans and zooms (2026-11-25): the wheel zooms round the pointer within CommandWorkspace's range, a chain
    // parked out of view raises the 동떨어진 블록이 있어요 note, its click brings the chain into view, and opening a robot puts
    // the def block at the area's top-left at the zoom it had.
    static void PanZoomAndOffscreenNote()
    {
        Open(a); Load(new CodeBlock(BlockKind.Slash));
        var space = panel.Workspace;
        Require(space && panel.DefBlock, "setup: the code area has its workspace view and def block");
        var view = (RectTransform)space.transform;
        var line = program.StartChain()[0];
        var spot = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, line.Rect.TransformPoint(line.Rect.rect.center));
        for (int i = 0; i < 40; i++)
            ExecuteEvents.ExecuteHierarchy(line.gameObject, new PointerEventData(EventSystem.current) { position = spot, scrollDelta = new Vector2(0, -1) }, ExecuteEvents.scrollHandler);
        Layout();
        var after = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, line.Rect.TransformPoint(line.Rect.rect.center));
        Require(Mathf.Approximately(space.Zoom, CommandWorkspace.MinZoom), "40 wheel steps down left the zoom at " + space.Zoom + " (expected " + CommandWorkspace.MinZoom + ")");
        Require(Vector2.Distance(spot, after) < 2, "the line under the pointer moved from " + spot + " to " + after + " while zooming");
        // A wait() parked far below and right of everything is out of view.
        Require(program.PlaceFree(Palette(BlockKind.Wait), new Vector2(4000, -3000)), "setup: a wait() parked far away");
        Layout(); LateUpdate(space);
        Require(space.OffscreenTarget && space.Note.gameObject.activeSelf, "a wait() parked out of view raised no note");
        space.RevealTarget(); Layout(); LateUpdate(space);
        var parked = program.LooseRoots().First().Rect;
        Require(Top(parked) <= Top(view) + 1 && Bottom(parked) >= Bottom(view) - 1, "the note's click left the parked wait() out of view");
        // The def block may now be out of view instead; the note no longer points at the wait().
        Require(space.OffscreenTarget != parked, "the note still points at the wait() brought into view");
        // Back on another robot and on this one: the def block at the top-left, the zoom kept.
        Open(b); Open(a); Layout();
        var c = new Vector3[4]; panel.DefBlock.GetWorldCorners(c); var v = new Vector3[4]; view.GetWorldCorners(v);
        var offset = (Vector2)(c[1] - v[1]) / view.lossyScale.x;
        Require(Vector2.Distance(offset, new Vector2(16, -16)) < 1, "reopened, the def block's top-left is " + offset + " from the area's (expected (16, -16))");
        Require(Mathf.Approximately(space.Zoom, CommandWorkspace.MinZoom), "reopening changed the zoom to " + space.Zoom);
    }
    static void LateUpdate(CommandWorkspace space) => typeof(CommandWorkspace).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(space, null);

    static float Bottom(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return c[0].y; }
    static float Top(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return c[1].y; }

    // --- helpers ---
    // A fresh Stage 2 session with the stock limit on: 50 more of every block, two warriors.
    static void Session()
    {
        battle.EditorStartSession(2, 1, false, new DefenseTestOptions { tutorial = false, tutorialInput = false, deploymentLimit = false });
        battle.EditorGrantAll();
        sim = battle.Simulation; editor = battle.CodeEditor; panel = editor.Panel; program = panel.Program;
        raycaster = panel.GetComponentInParent<GraphicRaycaster>(true);
        int warrior = Enumerable.Range(0, battle.RobotDefinitions.Count).First(i => battle.DefinitionRole(i) == RobotRole.Melee);
        a = Place(warrior); b = Place(warrior);
    }
    static int Place(int definition)
    {
        var spec = battle.RobotDefinition(definition).stats;
        for (float x = sim.Setup.Min.X + 1; x < sim.Setup.Max.X - 1; x++)
            for (float y = sim.Setup.Min.Y + 1; y < sim.Setup.Max.Y - 1; y++)
                if (sim.PlacementError(spec, new Point(x, y)) == null && battle.PlaceRobot(definition, new Vector3(x, 0, y))) return sim.Robots.Last().Id;
        throw new Failed("setup: no legal placement for a warrior");
    }
    static void Open(int id)
    {
        editor.Open(id);
        Require(editor.IsOpen && editor.EditingId == id, "setup: the editor opened on robot " + id);
    }
    static void Load(params CodeBlock[] blocks) { panel.LoadProgram(panel.FunctionName, blocks); Layout(); }
    static void Layout() { LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform); Canvas.ForceUpdateCanvases(); }
    static string Status() => panel.StatusLine.GetComponent<TMP_Text>().text;
    static int Slashes(int id) => sim.FindRobot(id).Program?.BlockCount(BlockKind.Slash) ?? 0;
    static CommandBlockView Argument(CommandBlockView block) => block.Arguments[0].Blocks[0];
    static string Kinds(IEnumerable<CommandBlockView> lines) => string.Join(", ", lines.Select(block => block.BaseKind));
    // The program (the chain on the start anchor) as its lines' kinds.
    static string Program() => program.StartChain().Count == 0 ? "(empty)" : Kinds(program.StartChain());
    // A palette block, with its tab shown.
    static CommandBlockView Palette(BlockKind kind)
    {
        var item = panel.Palette.FirstOrDefault(entry => entry.view && entry.view.BaseKind == kind);
        Require(item.view, "setup: no " + kind + " block in the palette");
        panel.ShowCategory(item.category);
        return item.view;
    }
    // Screen point of a place given from rect's top-left corner.
    static Vector2 Screen(RectTransform rect, Vector2 at) =>
        RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, rect.TransformPoint(new Vector3(rect.rect.xMin + at.x, rect.rect.yMax + at.y, 0)));
    // Where block's top-left corner is in the code area, from the area's top-left corner.
    static Vector2 At(RectTransform block)
    {
        Vector2 corner = program.Rect.InverseTransformPoint(block.TransformPoint(new Vector3(block.rect.xMin, block.rect.yMax, 0)));
        return corner - new Vector2(program.Rect.rect.xMin, program.Rect.rect.yMax);
    }
    static Vector2 hold; // from the dragged block's top-left corner to the pointer, on screen
    static PointerEventData Begin(CommandBlockView block)
    {
        Layout();
        var press = Screen(block.Rect, new Vector2(12, -12));
        hold = press - Screen(block.Rect, Vector2.zero);
        var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = press, pressPosition = press,
            pointerPressRaycast = new RaycastResult { module = raycaster } };
        panel.BeginDrag(block, e);
        Require(panel.Dragged == block, "setup: the drag started on " + block.BaseKind);
        return e;
    }
    // Carries the dragged block's top-left corner to `at` in the code area; updated twice, as a held block is every frame.
    static void Move(PointerEventData e, Vector2 at)
    {
        e.position = Screen(program.Rect, at) + hold;
        panel.MoveDrag(e); panel.MoveDrag(e);
    }
    static void Drop(PointerEventData e) { panel.FinishDrag(e); Layout(); }
}
