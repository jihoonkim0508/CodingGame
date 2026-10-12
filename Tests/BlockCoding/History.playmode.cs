// Run with Unity MCP eval_file while Defense.unity is playing. Resets this test run.
if (!UnityEditor.EditorApplication.isPlaying) return "Play Mode required";
var battle = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseBattle>();
var editor = UnityEngine.Object.FindFirstObjectByType<CodingGame.Defense.DefenseCodeEditor>();
if (battle == null || editor == null) return "Defense scene required";
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new System.Exception(message); checks++; }
CodingGame.BlockCoding.CommandBlockView Palette(CodingGame.BlockCoding.BlockKind kind) => editor.Panel.Palette.First(p => p.view.BaseKind == kind).view;
var draftKind = CodingGame.BlockCoding.BlockKind.Wait; // starts with an empty argument, so it stays a draft
var shotKind = CodingGame.BlockCoding.BlockKind.Shot;
try
{
    // Sandbox with exact stock: these checks count reservations, so the inventory limit stays on.
    battle.EditorStartSession(2, 1, false, new CodingGame.Defense.DefenseTestOptions { tutorial = false, tutorialInput = false });
    battle.Simulation.GrantRobot(CodingGame.Defense.RobotRole.Shooter, 1); battle.Simulation.GrantRobot(CodingGame.Defense.RobotRole.Warrior, 1);
    battle.Simulation.GrantBlock(shotKind, 3);
    if (CodingGame.Defense.DefenseProgression.Consumes(CodingGame.BlockCoding.BlockKind.Wait)) battle.Simulation.GrantBlock(CodingGame.BlockCoding.BlockKind.Wait, 2);
    Check(battle.PlaceRobot(4, new UnityEngine.Vector3(-8, 0, 6)), "place first robot");
    int firstId = battle.SelectedId;
    battle.SelectRobot(firstId); battle.DevelopSelected();
    var panel = editor.Panel;
    panel.AddFromPalette(Palette(draftKind));
    Check(!panel.IsApplied && panel.Program.Blocks.Count == 1, "unfinished attack remains a draft");
    editor.Close(); editor.Open(battle.SelectedId);
    Check(panel.Program.Blocks.Count == 1 && panel.Program.Blocks[0].Read().Arguments[0] == null, "empty argument survives reopening");
    panel.Undo(); Check(panel.Program.Blocks.Count == 0, "undo incomplete insertion");
    panel.Redo(); Check(panel.Program.Blocks.Count == 1, "redo incomplete insertion");
    var position = new UnityEngine.Vector2(340, -210);
    panel.Program.PlaceFree(Palette(CodingGame.BlockCoding.BlockKind.Wait), position);
    Check(panel.Program.CaptureDraft().Count == 2, "disconnected groups captured");
    editor.Close(); editor.Open(battle.SelectedId);
    Check(panel.Program.CaptureDraft().Count == 2 && panel.Program.CaptureDraft()[1].position == position, "disconnected positions restored");
    panel.DeleteOne(panel.Program.Blocks[1]); panel.Undo();
    Check(panel.Program.Blocks.Count == 2 && panel.Program.CaptureDraft()[1].position == position, "undo deletion restores position");
    panel.ClearProgram(); panel.Undo();
    Check(panel.Program.Blocks.Count == 2, "clear is reversible");
    panel.Redo(); Check(panel.Program.Blocks.Count == 0, "redo clear");
    var loop = new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.While);
    loop.Body.Add(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Wait, "0", new CodingGame.BlockCoding.CodeBlock[] { null }));
    battle.Simulation.GrantBlock(CodingGame.BlockCoding.BlockKind.While, 1);
    panel.LoadProgram("nested_draft", new[] { loop });
    editor.Close(); editor.Open(battle.SelectedId);
    Check(panel.Program.Blocks[0].Body.Blocks.Count == 1 && panel.Program.Blocks[0].Body.Blocks[0].Read().Arguments[0] == null, "nested incomplete body restored");
    panel.ClearProgram(); panel.Undo();
    Check(panel.Program.Blocks[0].Body.Blocks.Count == 1, "nested body restored by undo");
    panel.LoadProgram("robot_1", new[] { new CodingGame.BlockCoding.CodeBlock(shotKind) });
    panel.Program.PlaceFree(panel.Program.Blocks[0], position);
    panel.Undo(); Check(panel.Program.Blocks[0].Rect.anchoredPosition == new UnityEngine.Vector2(24, -24), "undo move");
    panel.Redo(); Check(panel.Program.Blocks[0].Rect.anchoredPosition == position, "redo move");
    panel.ClearProgram(); panel.Undo();
    Check(panel.IsApplied && battle.Simulation.Available(shotKind) == 2, "undo applies and reserves exactly once");
    panel.Undo(); panel.FunctionName = "changed_name";
    Check(!panel.CanRedo, "new edit discards redo branch");
    panel.LoadProgram("reserved", Enumerable.Range(0, 3).Select(_ => new CodingGame.BlockCoding.CodeBlock(shotKind)).ToArray());
    panel.ClearProgram(); editor.Close();
    Check(battle.PlaceRobot(1, new UnityEngine.Vector3(-6, 0, 6)), "place second robot");
    int secondId = battle.SelectedId;
    battle.SelectRobot(secondId); battle.DevelopSelected();
    panel.LoadProgram("second", new[] { new CodingGame.BlockCoding.CodeBlock(shotKind) });
    editor.Close(); battle.SelectRobot(firstId); battle.DevelopSelected(); panel.Undo();
    Check(!panel.IsApplied && panel.Program.Blocks.Count == 3, "over-stock undo remains a draft");
    Check(battle.Simulation.Available(shotKind) == 2 && battle.Simulation.Robots.Find(r => r.Id == firstId).Program.CopyBlocks().Count == 0, "failed undo preserves inventory and applied code");
    editor.Close(); battle.SelectRobot(secondId); battle.DevelopSelected();
    Check(panel.Program.Blocks.Count == 1 && panel.FunctionName == "second", "histories isolated per robot");
    battle.Restart();
    Check(battle.PlaceRobot(4, new UnityEngine.Vector3(-8, 0, 6)), "place after restart");
    battle.SelectRobot(battle.SelectedId); battle.DevelopSelected();
    Check(panel.Program.Blocks.Count == 0 && !panel.CanUndo, "restart clears drafts even if robot id is reused");
    // 도메인 리로드 직후처럼 UI는 열려 있지만 두 세션이 모두 사라진 상태를 재현합니다.
    var session = battle.Simulation;
    var simulationProperty = battle.GetType().GetProperty("Simulation");
    var privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    try
    {
        simulationProperty.SetValue(battle, null);
        editor.GetType().GetField("editingSession", privateInstance).SetValue(editor, null);
        editor.GetType().GetMethod("Update", privateInstance).Invoke(editor, null);
        Check(!editor.IsOpen, "missing sessions close editor without dereferencing null");
    }
    finally { simulationProperty.SetValue(battle, session); }
    return "PASS: " + checks + " editor history and draft checks";
}
finally { battle.Restart(); }
