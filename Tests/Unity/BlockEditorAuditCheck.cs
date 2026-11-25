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
            Run("B8 rejected draft keeps the stock at zero or more", RejectedDraftWithinStock);
            Run("B9 a program taller than the code area scrolls into reach", TallProgramScrolls);
            Run("B10 palette blocks at ×0 are dimmed", ZeroStockDims);
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
    // A kind whose whole stock other robots use is taken off this robot's palette, as if it were not unlocked, while a kind
    // this robot used up itself stays listed as ×0: CommandCodingPanel.ShowCategory hides on IsAvailable, which
    // DefenseCodeEditor defines as "some left for this robot".
    static void PaletteListsOwnedKinds()
    {
        sim.Inventory[BlockKind.Slash] = 1; sim.Inventory.Remove(BlockKind.Boom);
        Open(a); Load(new CodeBlock(BlockKind.Slash));
        Require(panel.IsApplied && sim.Available(BlockKind.Slash) == 0, "setup: robot a uses the only slash(): " + Status());
        Require(Listed(BlockKind.Slash) == "×0", "guard: slash(), used up by this robot, is listed as ×0: " + (Listed(BlockKind.Slash) ?? "not listed"));
        Require(Listed(BlockKind.Boom) == null, "guard: boom(), not owned, stays off the palette");
        Open(b);
        Require(Listed(BlockKind.Boom) == null, "guard: boom(), not owned, stays off the palette");
        Require(Listed(BlockKind.Slash) == "×0", "slash(), used up by another robot, is " + (Listed(BlockKind.Slash) ?? "not listed") + " (expected listed as ×0)");
    }
    // The count beside a palette block with its tab shown; null when the block is not listed.
    static string Listed(BlockKind kind)
    {
        var view = Palette(kind);
        return view.gameObject.activeSelf ? Get<TMP_Text>(view, "stockLabel").text : null;
    }

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

    // --- B8 ---
    // A draft the stock cannot cover (an undo after another robot took the blocks) is rejected, yet all its blocks are
    // reserved: DefenseCodeEditor.Apply hands every block in the editor to SetDraft. The stock goes below zero, and the other
    // robot's applied code no longer fits it: opening that robot's editor empties its code.
    static void RejectedDraftWithinStock()
    {
        sim.Inventory[BlockKind.Slash] = 2;
        Open(a);
        var unfinished = new CodeBlock(BlockKind.If); unfinished.Body.Add(new CodeBlock(BlockKind.Slash)); // no condition: a syntax error
        Load(unfinished);
        Require(!panel.IsApplied && sim.Available(BlockKind.Slash) == 1, "guard (SharedInventoryCheck): a draft with a syntax error keeps its slash() reserved: " +
            sim.Available(BlockKind.Slash) + " of 2 free");
        Load(new CodeBlock(BlockKind.Slash), new CodeBlock(BlockKind.Slash)); panel.ClearProgram();
        Require(sim.Available(BlockKind.Slash) == 2, "setup: robot a wrote two slash() and cleared them: " + sim.Available(BlockKind.Slash) + " free");
        Open(b); Load(new CodeBlock(BlockKind.Slash));
        Require(panel.IsApplied && sim.Available(BlockKind.Slash) == 1, "setup: robot b uses one slash(): " + Status());
        Open(a); panel.Undo();
        Require(program.Blocks.Count == 2 && !panel.IsApplied, "setup: robot a's undo brings back two slash() and is rejected, one being left: " + Status());
        int free = sim.Available(BlockKind.Slash);
        Open(b);
        Require(free >= 0 && Slashes(b) == 1, "after robot a's rejected undo " + free + " slash() are free of 2 owned (expected 0 or more); robot b, reopened, holds " +
            Slashes(b) + " slash() (expected 1)");
    }

    // --- B9 ---
    // 2-8's finished code (ten lines) is taller than the code area. The wheel scrolls it to the bottom: its last line,
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
        // The wheel over a line of code bubbles up to the code area's ScrollRect (no frame has drawn the editor yet, so it is
        // sent to the line itself rather than to a raycast hit).
        for (int i = 0; i < 40; i++)
            ExecuteEvents.ExecuteHierarchy(program.StartChain()[0].gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -1) }, ExecuteEvents.scrollHandler);
        Layout();
        Require(Bottom(last.Rect) >= Bottom(view) - 1 && Top(last.Rect) <= Top(view) + 1, "after the wheel (scroll position " + scroll.verticalNormalizedPosition.ToString("F2") +
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
    // --- B10 ---
    // A palette block with none left reads dimmed, whatever its kind and however it got to ×0: another robot holding all of
    // a kind (commands, values, the variable chip, comparisons), or this robot's drag of the last one. The drag's end undid
    // the dimming: EndDrag un-faded the palette block it had faded, after the drop had already set it to ×0.
    static void ZeroStockDims()
    {
        CodeBlock Enemy() => new CodeBlock(BlockKind.Variable, "enemy");
        var loop = new CodeBlock(BlockKind.While);
        loop.Body.Add(new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)));
        var far = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.Comparison, ">", new CodeBlock(BlockKind.Distance, "0", Enemy(), new CodeBlock(BlockKind.MyPos)), new CodeBlock(BlockKind.MyAttackRange)));
        far.Body.Add(new CodeBlock(BlockKind.Wait, "0", new CodeBlock(BlockKind.Number, "1")));
        var dead = new CodeBlock(BlockKind.If, "0", new CodeBlock(BlockKind.IsDead, "0", Enemy()));
        dead.Body.Add(new CodeBlock(BlockKind.Continue));
        loop.Body.Add(far); loop.Body.Add(dead); loop.Body.Add(new CodeBlock(BlockKind.Slash));
        Open(a); Load(loop);
        Require(panel.IsApplied, "setup: robot a's program is applied: " + Status());
        foreach (var used in DefenseProgression.Used(new[] { loop }).GroupBy(k => k)) sim.Inventory[used.Key] = used.Count();
        Open(b);
        var zero = new List<string>(); var bright = new List<string>();
        foreach (int category in panel.Palette.Select(item => item.category).Distinct().ToList())
        {
            panel.ShowCategory(category);
            foreach (var view in panel.Palette.Where(item => item.category == category).Select(item => item.view).Append(category == 2 ? panel.VariableTemplate : null))
            {
                if (!view || !view.gameObject.activeSelf || Get<TMP_Text>(view, "stockLabel").text != "×0") continue;
                zero.Add(view.Kind.ToString()); if (!Faded(view)) bright.Add(view.Kind.ToString());
            }
        }
        Require(zero.Count >= 10, "setup: robot a holds every block of ten kinds or more, listed ×0 for robot b: " + string.Join(", ", zero));
        Require(bright.Count == 0, "listed ×0 but not dimmed: " + string.Join(", ", bright));
        // This robot drags the last slash() into its program.
        Open(a); Load(); sim.Inventory[BlockKind.Slash] = 1; panel.RefreshPalette();
        Require(Listed(BlockKind.Slash) == "×1" && !Faded(Palette(BlockKind.Slash)), "setup: one slash() left, not dimmed: " + Listed(BlockKind.Slash));
        var slash = Palette(BlockKind.Slash);
        Carry(BlockKind.Slash, Start);
        // Read as the drop left it: Listed and Palette show the tab again, which redraws the counts.
        Require(Get<TMP_Text>(slash, "stockLabel").text == "×0", "setup: the drag took the last slash(): " + Get<TMP_Text>(slash, "stockLabel").text + ", " + Program());
        Require(Faded(slash), "the last slash(), dragged into the program, leaves its palette block at ×0 but not dimmed");
    }
    static bool Faded(CommandBlockView view) =>
        view.GetComponentsInChildren<CommandBlockShape>(true).Where(s => s.GetComponentInParent<CommandBlockView>(true) == view).All(s => s.Fade > 0);

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
