// Run this file as Unity MCP Unity_RunCommand.Code with Defense.unity in Play Mode.
// Uses a disposable copy of the authored editor; leaves the player's program unchanged.
using System;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var original = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CommandCodingPanel>(true)).First();
        var panel = UnityEngine.Object.Instantiate(original, original.transform.parent);
        result.RegisterObjectCreation(panel.gameObject);
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks++; }
        var marker = (RectTransform)new SerializedObject(panel).FindProperty("snapMarker").objectReferenceValue;
        var program = panel.Program;
        var canvas = program.GetComponentInParent<Canvas>(true);
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        CommandBlockView Palette(BlockKind kind) => panel.Palette.First(p => p.view.BaseKind == kind).view;
        void Layout() { LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform); Canvas.ForceUpdateCanvases(); }
        Vector2 Screen(RectTransform rect, Vector2 point) => RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(point));
        PointerEventData Begin(CommandBlockView source, Vector2 grip)
        {
            Layout();
            var press = Screen(source.Rect, new Vector2(source.Rect.rect.xMin, source.Rect.rect.yMax) + grip);
            var e = new PointerEventData(EventSystem.current) { position = press, pressPosition = press };
            panel.BeginDrag(source, e);
            return e;
        }
        void Move(PointerEventData e, CommandBlockView source, RectTransform target, Vector2 topLeft, Vector2 grip)
        {
            var world = target.TransformPoint(topLeft) + source.Rect.TransformVector(grip);
            e.position = RectTransformUtility.WorldToScreenPoint(camera, world);
            panel.MoveDrag(e);
        }
        void Drop(PointerEventData e, CommandDropZone pointerZone)
        { pointerZone.OnDrop(e); panel.FinishDrag(e); Layout(); }
        try
        {
            panel.gameObject.SetActive(true);
            panel.ApplyToTarget = null; panel.Remaining = _ => 999; panel.IsAvailable = _ => true;
            panel.LoadProgram("drag_check", Array.Empty<CodeBlock>());
            panel.ShowCategory(2); Layout();
            var variable = panel.VariableTemplate;
            var input = variable.GetComponentInChildren<TMP_InputField>(true);
            Check(input.textViewport.anchoredPosition == Vector2.zero, "variable viewport must be centered");
            Check(input.textComponent.rectTransform.anchoredPosition == Vector2.zero, "variable text must stay in viewport");
            input.textComponent.ForceMeshUpdate();
            var bounds = input.textComponent.textBounds;
            foreach (var corner in new[] { bounds.min, bounds.max })
                Check(variable.Rect.rect.Contains(variable.Rect.InverseTransformPoint(input.textComponent.transform.TransformPoint(corner))),
                    "rendered variable name must fit inside block");
            var grip = new Vector2(70, -16);
            var e = Begin(variable, grip);
            Move(e, variable, program.Rect, new Vector2(280, -220), grip); Drop(e, program);
            Check(program.Blocks[0].GetComponentInChildren<TMP_InputField>().textViewport.anchoredPosition == Vector2.zero,
                "placed variable preserves text alignment");
            panel.LoadProgram("drag_check", new[] { new CodeBlock(BlockKind.If) }); Layout();
            var conditional = program.Blocks[0];
            var shape = conditional.GetComponent<CommandBlockShape>();
            float height = conditional.Rect.rect.height;
            Check(shape.Raycast(Screen(conditional.Rect, new Vector2(8, -height / 2)), camera), "condition left spine is clickable");
            Check(shape.Raycast(Screen(conditional.Rect, new Vector2(30, -8)), camera), "condition header is clickable");
            Check(shape.Raycast(Screen(conditional.Rect, new Vector2(30, -height + 8)), camera), "condition footer is clickable");
            var hole = Screen(conditional.Rect, new Vector2(40, -shape.HeaderHeight - 8));
            Check(!shape.Raycast(hole, camera), "condition hole is not clickable");
            var bodyGraphic = (Graphic)new SerializedObject(conditional.Body).FindProperty("background").objectReferenceValue;
            Check(!bodyGraphic.raycastTarget, "body background does not intercept hole clicks");
            var hits = new System.Collections.Generic.List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) { position = hole }, hits);
            Check(!hits.Any(hit => hit.gameObject.transform.IsChildOf(conditional.transform)), "empty hole falls through in real UI raycast");
            foreach (var kind in new[] { BlockKind.Slash, BlockKind.Block, BlockKind.Boom, BlockKind.Shot, BlockKind.Slow })
            {
                var source = PythonTreeCompiler.Compile("robot", new[] { new CodeBlock(kind) });
                Check(source.StartsWith("def robot():\n") && source.Contains("enemy = get_nearest_enemy()") && source.Contains("(enemy)"),
                    "parameterless function retains target argument: " + kind);
            }
            var condition = program.Blocks[0].Arguments[0];
            var comparison = Palette(BlockKind.Comparison);
            panel.ShowCategory(panel.Palette.First(p => p.view == comparison).category); Layout();
            grip = new Vector2(comparison.Rect.rect.width - 15, -16);
            e = Begin(comparison, grip);
            // The mouse is far to the right of the condition, but the block's left connector aligns.
            Move(e, comparison, condition.Rect, new Vector2(4, 0), grip);
            Check(marker.gameObject.activeSelf, "comparison connector previews condition snap");
            Drop(e, program);
            Check(condition.Blocks.Count == 1, "pointer workspace cannot override condition snap");
            var placed = condition.Blocks[0];
            Check(placed.Rect.anchoredPosition == new Vector2(4, 0), "condition drop matches preview position");
            var number = Palette(BlockKind.Number);
            panel.ShowCategory(panel.Palette.First(p => p.view == number).category); Layout();
            var right = placed.Arguments[1];
            grip = new Vector2(75, -16); e = Begin(number, grip);
            Move(e, number, right.Rect, new Vector2(4, 0), grip);
            Drop(e, placed.Arguments[0]);
            Check(right.Blocks.Count == 1 && placed.Arguments[0].Blocks.Count == 0,
                "nearest connector wins even if OnDrop fires on another socket");
            panel.LoadProgram("drag_check", new[] { new CodeBlock(BlockKind.If) }); Layout();
            condition = program.Blocks[0].Arguments[0];
            panel.ShowCategory(panel.Palette.First(p => p.view == comparison).category); Layout();
            grip = new Vector2(10, -16); e = Begin(comparison, grip);
            Move(e, comparison, condition.Rect, new Vector2(-80, 0), grip);
            Check(marker.gameObject.activeSelf, "wide block body overlapping condition must snap");
            Drop(e, program);
            Check(condition.Blocks.Count == 1, "body overlap drops into condition without aligning the start");
            panel.LoadProgram("drag_check", new[] { new CodeBlock(BlockKind.Shot) }); Layout();
            var enemy = program.Blocks[0].Arguments[0];
            panel.ShowCategory(2); Layout();
            grip = new Vector2(15, -16); e = Begin(variable, grip);
            // Screenshot case: the variable's right end overlaps enemy, while its start and mouse are outside.
            Move(e, variable, enemy.Rect, new Vector2(-variable.Rect.rect.width + 25, 0), grip);
            Check(marker.gameObject.activeSelf, "variable tail overlapping enemy previews snap");
            Drop(e, program);
            Check(enemy.Blocks.Count == 1 && enemy.Blocks[0].BaseKind == BlockKind.Variable,
                "variable tail overlap fills enemy slot");
            e = Begin(variable, grip);
            Move(e, variable, enemy.Rect, new Vector2(4, 0), grip);
            Check(!marker.gameObject.activeSelf, "filled enemy slot cannot be replaced");
            panel.EndDrag();
            panel.LoadProgram("drag_check", new[] { new CodeBlock(BlockKind.Shot) }); Layout();
            enemy = program.Blocks[0].Arguments[0];
            e = Begin(variable, grip);
            Move(e, variable, enemy.Rect, new Vector2(-variable.Rect.rect.width - 10, 0), grip);
            Check(!marker.gameObject.activeSelf, "separated variable does not magnetize to a distant slot");
            panel.EndDrag();
            panel.LoadProgram("drag_check", Array.Empty<CodeBlock>());
            var statement = Palette(BlockKind.Shot);
            panel.ShowCategory(panel.Palette.First(p => p.view == statement).category); Layout();
            grip = new Vector2(45, -20); e = Begin(statement, grip);
            Move(e, statement, program.Rect, new Vector2(38, -34), grip); Drop(e, program);
            var first = program.Blocks[0];
            Check(first.Rect.anchoredPosition == new Vector2(24, -24), "first statement anchor");
            e = Begin(statement, grip);
            Move(e, statement, program.Rect, first.Rect.anchoredPosition + new Vector2(0, -first.preferredHeight), grip);
            Drop(e, program);
            Check(program.CaptureDraft().Count == 1 && program.Blocks.Count == 2, "statement chain connects");
            e = Begin(first, grip); Move(e, first, program.Rect, new Vector2(280, -220), grip); Drop(e, program);
            Check(program.CaptureDraft().Count == 1 && program.Group(first).Count == 2,
                "moving a chain preserves all linked blocks");
            Check(Vector2.Distance(first.Rect.anchoredPosition, new Vector2(280, -220)) < .1f,
                "free drop preserves block position");
            e = Begin(statement, grip);
            Move(e, statement, program.Rect, first.Rect.anchoredPosition + new Vector2(0, statement.preferredHeight), grip);
            Check(marker.gameObject.activeSelf, "bottom connector previews prepend");
            Drop(e, program);
            Check(program.CaptureDraft().Count == 1 && program.Blocks.Count == 3, "prepend matches preview and keeps chain");
            panel.LoadProgram("drag_check", new[] { new CodeBlock(BlockKind.If) }); Layout();
            var body = program.Blocks[0].Body;
            e = Begin(statement, grip);
            Move(e, statement, body.Rect, new Vector2(4, -4), grip); Drop(e, program);
            Check(body.Blocks.Count == 1 && program.Blocks.Count == 1, "statement enters body by block position");
            result.Log("PASS: " + checks + " variable layout and drag/drop checks");
        }
        finally { panel.EndDrag(); panel.gameObject.SetActive(false); result.DestroyObject(panel.gameObject); }
    }
}
