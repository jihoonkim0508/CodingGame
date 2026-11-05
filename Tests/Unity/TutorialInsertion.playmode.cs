// Unity MCP run_script: TutorialInsertionCheck.Run, in Play after StageTwoTutorialBuilder.Apply.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TutorialInsertionCheck
{
    const string Folder = "Temp/TutorialPlacement";
    static string log;
    static DefenseBattle Battle => DevTest.Battle;
    static TutorialDirector Director => Battle.Tutorial;
    static DefenseCodeEditor Editor => Battle.CodeEditor;
    static void Check(bool okay, string reason)
    { if (!okay) throw new Exception(reason); log += "PASS: " + reason + "\n"; File.WriteAllText(Folder + "/placement-check.log", log); }
    static async Task Wait(Func<bool> condition, string reason)
    {
        var limit = DateTime.UtcNow.AddSeconds(15);
        while (!condition()) { if (DateTime.UtcNow > limit) throw new Exception("Timeout: " + reason + "\n" + DevTest.State()); await Task.Delay(50); }
    }
    static async Task Jump(int stage, int wave, string id)
    {
        int index = Array.FindIndex(DevTest.TutorialSections(stage, wave), s => s.StartsWith(id + " "));
        if (index < 0) throw new Exception("Missing step " + id);
        DevTest.JumpTutorial(stage, wave, index);
        await Wait(() => DevTest.Ready && Battle.Stage == stage && DevTest.Sim.WaveIndex == wave - 1 && Director.EditorSynced && Director.EditorStep?.stepId == id, "jump " + stage + "-" + wave + "/" + id);
        Director.EditorFinishTyping(); await Task.Delay(450); Canvas.ForceUpdateCanvases();
    }
    static Rect Area(string key)
    {
        if (!Director.EditorBridge.TryGetArea(key, out var area)) throw new Exception("Missing visible target: " + key);
        return area;
    }
    static void SameRect(Rect a, Rect b, string reason)
    { Check(Vector2.Distance(a.min, b.min) < .1f && Vector2.Distance(a.max, b.max) < .1f, reason); }
    static async Task Screenshot(string name)
    { ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/" + name + ".png")); await Task.Delay(150); }
    static void Drag(CommandBlockView source, Vector2 end)
    {
        var start = RectTransformUtility.WorldToScreenPoint(null, source.Rect.TransformPoint(source.Rect.rect.center));
        var e = new PointerEventData(EventSystem.current) { position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(e, hits);
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IBeginDragHandler>(hits[0].gameObject) == source.gameObject, "palette drag starts on the visible attack block");
        e.pointerPressRaycast = hits[0]; e.pointerCurrentRaycast = hits[0];
        ExecuteEvents.Execute(source.gameObject, e, ExecuteEvents.beginDragHandler);
        Check(Editor.Panel.Dragged == source, "real editor drag begins");
        e.position = end; ExecuteEvents.Execute(source.gameObject, e, ExecuteEvents.dragHandler);
        hits.Clear(); EventSystem.current.RaycastAll(e, hits);
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IDropHandler>(hits[0].gameObject) == Editor.Panel.Program.StartChain()[0].Body.gameObject, "highlighted insertion row receives the drop raycast inside for");
        e.pointerCurrentRaycast = hits[0];
        ExecuteEvents.Execute(source.gameObject, e, ExecuteEvents.endDragHandler);
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder); log = ""; Application.runInBackground = true;
        await Jump(2, 3, "B03");
        var loop = Editor.Panel.Program.StartChain()[0]; var body = loop.Body;
        var before = Editor.Panel.AppliedSource;
        var row = Area("Editor.Code.BodyEnd.1"); var declaration = Area("Editor.Code.BodyLine.1.0");
        Check(row.yMax <= declaration.yMin + .1f && Mathf.Abs(row.yMax - declaration.yMin) < 1, "insertion row touches the bottom of enemy lookup, never covers it");
        var bodyArea = Area("Editor.Code.Body.1");
        Check(bodyArea.Contains(row.center) && row.xMin >= bodyArea.xMin && row.xMax <= bodyArea.xMax, "insertion row stays within the indented for body");
        Check(body.Blocks.Count == 1 && before.EndsWith("enemy = get_nearest_enemy()\n"), "tutorial placeholder does not add executable code");
        var f = BindingFlags.Instance | BindingFlags.NonPublic;
        var pointer = (TutorialPointer)Director.GetType().GetField("pointer", f).GetValue(Director);
        SameRect((Rect)pointer.GetType().GetField("target", f).GetValue(pointer), row, "arrow points at the insertion row");
        SameRect((Rect)pointer.GetType().GetField("dragTo", f).GetValue(pointer), row, "animated attack lands in the insertion row");
        await Screenshot("2-3-insertion");
        var source = Editor.TutorialPaletteItem("Editor.Palette.Shot").GetComponent<CommandBlockView>();
        var center = body.Rect.TransformPoint(body.TutorialAppendArea.center);
        Drag(source, RectTransformUtility.WorldToScreenPoint(null, center));
        await Wait(() => Director.EditorStep?.stepId == "B04", "attack insertion advances to the enemy argument step");
        await Task.Delay(200); Canvas.ForceUpdateCanvases();
        Check(body.Blocks.Count == 2 && body.Blocks[0].Kind == BlockKind.DeclareVariable && body.Blocks[1].Kind == BlockKind.Shot, "attack is connected immediately below enemy lookup inside for");
        Check(Editor.Panel.Program.StartChain().Count == 1, "attack does not become a separate top-level statement");
        Check(Mathf.Abs(body.preferredHeight - 100) < .1f, "placeholder space is removed when the action completes");
        var argument = Area("Editor.Code.BodyArg.1.1.0");
        SameRect(argument, Director.EditorBridge.ToLocal(body.Blocks[1].Arguments[0].Rect), "next argument highlight aligns with the actual attack socket");
        var variable = Editor.TutorialPaletteItem("Editor.Palette.Variable").GetComponent<CommandBlockView>();
        ExecuteEvents.Execute(variable.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        body.Blocks[1].Arguments[0].Blocks[0].InputRect.GetComponent<TMPro.TMP_InputField>().text = "enemy";
        await Wait(() => Director.EditorStep?.stepId == "B05", "target insertion completes the runnable loop");
        Check(StageTwoLearning.Matches(Editor.Panel.Program.Read(), RobotRole.Shooter, 3), "completed loop compiles with attack(enemy) in the correct body");
        var previewImage = (RawImage)Battle.AttackPreview.GetType().GetField("previewImage", f).GetValue(Battle.AttackPreview);
        SameRect(Area(TutorialKeys.Preview), Director.EditorBridge.ToLocal(previewImage.rectTransform), "preview highlight follows the rendered image without overlapping title or status labels");
        Director.EditorFinishTyping();
        await Screenshot("2-3-connected");

        await Jump(2, 1, "C02");
        var empty = Area("Editor.Code.Body.1");
        Check(empty.width > 64 && empty.height >= 32, "Stage 2 empty loop body highlights a usable row rather than a narrow strip");
        await Screenshot("2-1-empty-body");
        await Jump(2, 3, "B03");
        loop = Editor.Panel.Program.StartChain()[0];
        var after = Area("Editor.Code.After.1");
        Check(Mathf.Abs(after.height / Director.EditorBridge.ToLocal(loop.Rect).height * loop.Rect.rect.height - 46) < .1f, "after a compound block highlights one statement row, not a whole loop height");
        var content = Editor.Panel.Program.Rect;
        var original = content.anchoredPosition;
        var viewport = loop.Rect.GetComponentInParent<Mask>();
        Check(viewport != null, "code workspace has a clipping viewport");
        var viewportArea = Director.EditorBridge.ToLocal((RectTransform)viewport.transform);
        content.anchoredPosition = original + new Vector2(0, 50);
        var clipped = Area("Editor.Code.Line.1");
        Check(clipped.yMax <= viewportArea.yMax + .1f, "scrolled code highlight clips at the workspace viewport");
        content.anchoredPosition = original + new Vector2(0, 2000);
        Check(!Director.EditorBridge.TryGetArea("Editor.Code.Line.1", out _), "fully scrolled-out code has no floating highlight");
        content.anchoredPosition = original;

        await Jump(1, 3, "D02");
        var stageOneCode = TutorialAnswers.Parse(DevTest.TutorialSequenceFor(1, 3).steps.Single(s => s.stepId == "D05").expectedCode);
        stageOneCode[2].Body.Clear();
        Editor.Panel.LoadProgram("robot_" + Editor.EditingId, stageOneCode);
        await Task.Delay(150); Canvas.ForceUpdateCanvases();
        var ifBlock = Editor.Panel.Program.StartChain().ElementAtOrDefault(2);
        // Editor jump restores completed prerequisites; reconstruct this step's deliberately empty body.
        if (!ifBlock) throw new Exception("Stage 1 if draft missing");
        var stageOneBody = Area("Editor.Code.Body.3");
        Check(stageOneBody.width > 64, "Stage 1 if body uses the same full-width insertion guidance");
        await Screenshot("1-3-empty-body");
        await Jump(1, 1, "C06");
        Editor.Panel.LoadProgram("robot_" + Editor.EditingId, new[] { new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)) });
        await Task.Delay(150); Canvas.ForceUpdateCanvases();
        var name = Editor.Panel.Program.StartChain()[0].InputRect;
        SameRect(Area("Editor.Code.Name.1"), Director.EditorBridge.ToLocal(name), "Stage 1 variable-name highlight matches its input field");
        await Jump(2, 3, "B03");
        // Also check the palette click shortcut, which must append to this same body.
        source = Editor.TutorialPaletteItem("Editor.Palette.Shot").GetComponent<CommandBlockView>();
        ExecuteEvents.Execute(source.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        await Wait(() => Director.EditorStep?.stepId == "B04", "palette click appends to the indicated body, matching drag behavior");
        await Jump(2, 3, "B03");
        await Screenshot("after-insertion");
        return log;
    }
}
