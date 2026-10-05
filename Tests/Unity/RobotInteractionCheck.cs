// Run in a fresh Defense Play Mode session with Unity CLI run_script.
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CodingGame.Defense;
using Point = System.Numerics.Vector2;

public static class RobotInteractionCheck
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Ref<T>(UnityEngine.Object o, string name) where T : UnityEngine.Object =>
        (T)new SerializedObject(o).FindProperty(name).objectReferenceValue;
    public static string Main()
    {
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        var hud = UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>();
        var inv = UnityEngine.Object.FindFirstObjectByType<DefenseInventoryUI>();
        var editor = UnityEngine.Object.FindFirstObjectByType<DefenseCodeEditor>();
        battle.Restart(); var sim = battle.Simulation;
        hud.SendMessage("LateUpdate");
        var buttons = new SerializedObject(hud).FindProperty("robotButtons");
        for (int i = 0; i < 6; i++) Require(((Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue).gameObject.activeSelf ==
            (battle.DefinitionRole(i) == RobotRole.Shooter), "Only unlocked robot visible");
        int index = Enumerable.Range(0, 6).Single(i => battle.DefinitionRole(i) == RobotRole.Shooter);
        bool placed = false;
        for (float x = sim.Setup.Min.X + 1; x < sim.Setup.Max.X - 1 && !placed; x++)
            for (float y = sim.Setup.Min.Y + 1; y < sim.Setup.Max.Y - 1 && !placed; y++)
                if (sim.PlacementError(battle.RobotDefinition(index).stats, new Point(x, y)) == null)
                    placed = battle.PlaceRobot(index, new Vector3(x, 0, y));
        Require(placed, "Robot placed"); int id = battle.SelectedId;
        battle.SelectRobot(id); hud.SendMessage("LateUpdate");
        Require(!editor.IsOpen && Ref<RectTransform>(hud, "robotActions").gameObject.activeInHierarchy, "Click opens action menu only");
        int coins = sim.Coins;
        Ref<Button>(hud, "upgradeRobotButton").onClick.Invoke();
        Require(sim.Robots.Single().Level == 2 && sim.Coins == coins - sim.UpgradeCost, "Menu upgrade charges once");
        Ref<Button>(hud, "upgradeRobotButton").onClick.Invoke();
        Require(sim.Coins == coins - sim.UpgradeCost, "Max upgrade cannot charge twice");
        Ref<Button>(hud, "developRobotButton").onClick.Invoke();
        var preview = UnityEngine.Object.FindFirstObjectByType<DefenseAttackPreview>();
        Require(editor.IsOpen && preview.IsShowing, "Develop opens code and automatic preview");
        Require(Ref<GameObject>(preview, "previewPanel").transform.parent == Ref<GameObject>(editor, "editorRoot").transform, "Preview is top-left sibling, not code overlay");
        Require(!Ref<GameObject>(editor, "editorRoot").transform.Find("PreviewButton"), "Preview toggle removed");
        var control = Ref<DefensePreviewCameraControl>(preview, "cameraControl");
        var camera = Ref<Camera>(control, "previewCamera"); var viewport = Ref<RectTransform>(control, "viewport");
        Canvas.ForceUpdateCanvases(); float size = camera.orthographicSize; var position = camera.transform.position;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, viewport.position), delta = new Vector2(40, 20), scrollDelta = Vector2.up };
        control.OnBeginDrag(pointer); control.OnDrag(pointer); control.OnScroll(pointer);
        Require(camera.transform.position != position && camera.orthographicSize < size, "Preview drag and zoom work");
        pointer.button = PointerEventData.InputButton.Right; position = camera.transform.position; control.OnDrag(pointer);
        Require(camera.transform.position == position, "Right drag ignored");
        control.ResetView();
        ScreenCapture.CaptureScreenshot("C:/Users/jihoon/.codex/tmp/code-preview-new.png");
        return "PASS: locked robots hidden, 3-button menu, paid upgrade, develop, automatic preview, left drag, wheel zoom";
    }
    public static string GrantsAndRecall()
    {
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        var hud = UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>();
        var inv = UnityEngine.Object.FindFirstObjectByType<DefenseInventoryUI>();
        var editor = UnityEngine.Object.FindFirstObjectByType<DefenseCodeEditor>();
        editor.Close(); var sim = battle.Simulation;
        Ref<Button>(hud, "recallRobotButton").onClick.Invoke(); Require(sim.Robots.Count == 0, "Menu recall works");
        int initial = sim.RobotAvailable(RobotRole.Shooter);
        inv.GrantAll(); Require(sim.RobotAvailable(RobotRole.Shooter) == initial, "Grant locked outside developer mode");
        battle.ToggleDeveloper();
        var robots = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().ToDictionary(r => r, r => sim.RobotAvailable(r));
        var blocks = DefenseProgression.ItemKinds.ToDictionary(k => k, k => sim.Inventory.TryGetValue(k, out int n) ? n : 0);
        Ref<Button>(inv, "grantAllButton").onClick.Invoke(); Ref<Button>(inv, "grantAllButton").onClick.Invoke();
        Require(robots.All(p => sim.RobotAvailable(p.Key) == p.Value + 20), "Each click adds 10 per robot");
        Require(blocks.All(p => sim.Inventory[p.Key] == p.Value + 100), "Each click adds 50 per block");
        battle.ToggleDeveloper(); hud.SendMessage("LateUpdate");
        var buttons = new SerializedObject(hud).FindProperty("robotButtons");
        for (int i = 0; i < 6; i++) Require(((Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue).gameObject.activeSelf, "Granted robots visible");
        return "PASS: recall, developer-only grant, additive robot +10/block +50, granted robots unlock";
    }
}
