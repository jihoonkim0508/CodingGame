// Run with Unity MCP eval_file in Defense Play Mode. Resets the current test run.
if (!UnityEditor.EditorApplication.isPlaying) return "Play Mode required";
var battle = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseBattle>();
var editor = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseCodeEditor>();
if (battle == null || editor == null) return "Defense scene required";
int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new System.Exception(name); checks++; }
try
{
    battle.Restart();
    Check(battle.PlaceRobot(4, new UnityEngine.Vector3(-8, 0, 6)), "place robot");
    battle.SelectRobot(battle.SelectedId);
    var panel = editor.Panel;
    var program = panel.Program;
    var marker = (UnityEngine.RectTransform)typeof(CodingGame.BlockCoding.CommandCodingPanel)
        .GetField("snapMarker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(panel);
    var palette = panel.Palette.First(p => p.view.BaseKind == CodingGame.BlockCoding.BlockKind.Shot).view;
    UnityEngine.EventSystems.PointerEventData Begin(CodingGame.BlockCoding.CommandBlockView block)
    {
        UnityEngine.Canvas.ForceUpdateCanvases();
        // Grab the middle of the header, not the top-left corner.
        var origin = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, block.Rect.TransformPoint(new UnityEngine.Vector3(45, -20, 0)));
        var e = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left, position = origin, pressPosition = origin
        };
        panel.BeginDrag(block, e);
        return e;
    }
    void Move(UnityEngine.EventSystems.PointerEventData e, UnityEngine.Vector2 topLeft)
    {
        e.position = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, program.Rect.TransformPoint(new UnityEngine.Vector3(topLeft.x + 45, topLeft.y - 20, 0)));
        panel.MoveDrag(e);
    }
    void Drop(UnityEngine.EventSystems.PointerEventData e) { program.OnDrop(e); panel.FinishDrag(e); }
    var first = new UnityEngine.Vector2(24, -24);
    var near = new UnityEngine.Vector2(38, -34);
    var far = new UnityEngine.Vector2(280, -220);
    var pointer = Begin(palette); Move(pointer, near);
    Check(marker.gameObject.activeSelf, "new first block shows anchor preview nearby");
    Drop(pointer);
    Check(program.Blocks[0].Rect.anchoredPosition == first, "new first block snaps exactly to anchor");
    var existing = program.Blocks[0];
    pointer = Begin(existing); Move(pointer, far);
    Check(!marker.gameObject.activeSelf, "existing block free placement has no preview");
    Drop(pointer);
    Check(UnityEngine.Vector2.Distance(existing.Rect.anchoredPosition, far) < .1f, "free placement preserves drop position");
    pointer = Begin(existing); Move(pointer, near);
    Check(marker.gameObject.activeSelf, "existing block returning to first line shows preview");
    Drop(pointer);
    Check(existing.Rect.anchoredPosition == first, "existing block returning to first line snaps");
    pointer = Begin(palette); Move(pointer, first + new UnityEngine.Vector2(0, -existing.preferredHeight));
    Check(marker.gameObject.activeSelf, "following statement still shows connection preview");
    Drop(pointer);
    Check(program.CaptureDraft().Count == 1 && program.Read().Count == 2, "following statement connects");
    pointer = Begin(existing); Move(pointer, far); Drop(pointer);
    pointer = Begin(existing); Move(pointer, near);
    Check(marker.gameObject.activeSelf, "whole chain shows first-line anchor");
    Drop(pointer);
    Check(existing.Rect.anchoredPosition == first && program.Read().Count == 2, "whole chain snaps without losing links");
    panel.ClearProgram();
    pointer = Begin(palette); Move(pointer, far);
    Check(!marker.gameObject.activeSelf, "new block far from first line has no false preview");
    Drop(pointer);
    Check(UnityEngine.Vector2.Distance(program.Blocks[0].Rect.anchoredPosition, far) < .1f, "new block far from first line stays free");
    return "PASS: " + checks + " snap preview and drop checks";
}
finally { battle.Restart(); }
