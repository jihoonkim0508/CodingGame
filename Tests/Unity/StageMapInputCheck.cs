// DevTest.PlayStageMap(), then run_script with entry StageMapInputCheck.Begin.
// Poll Temp/StageSelection/input-check.txt after the command returns, allowing game frames to run.
// Sends Input System mouse events through the normal Update, raycast and card selection paths.
using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using CodingGame.StageMap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StageMapInputCheck
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static string Begin()
    {
        Directory.CreateDirectory("Temp/StageSelection");
        File.WriteAllText("Temp/StageSelection/input-check.txt", "RUNNING");
        _ = Report();
        return "Started. Poll Temp/StageSelection/input-check.txt.";
    }
    static async Task Report()
    {
        try { File.WriteAllText("Temp/StageSelection/input-check.txt", await Main()); }
        catch (Exception error) { File.WriteAllText("Temp/StageSelection/input-check.txt", "FAIL: " + error); }
    }
    static void Require(bool valid, string message) { if (!valid) throw new Exception(message); }
    static async Task Click(Mouse mouse, Vector2 position)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 0 });
        await Task.Delay(100);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
        await Task.Delay(100);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 0 });
        await Task.Delay(100);
    }
    public static async Task<string> Main()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().name == "Stage", "Start Stage Play Mode first.");
        var original = InputSystem.settings;
        var inputBehavior = original.editorInputBehaviorInPlayMode;
        var backgroundBehavior = original.backgroundBehavior;
        bool runInBackground = Application.runInBackground;
        Mouse mouse = null;
        try
        {
            Application.runInBackground = true;
            // Temporarily route synthetic input to the game even when MCP runs unfocused.
            // Keep the settings object: replacing it destroys Input System's generated defaults.
            original.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            original.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("StageMapInputCheckMouse");
            bool initializeButtonTracking = mouse.leftButton.wasPressedThisFrame;
            await Task.Delay(1000);
            var controller = Object.FindFirstObjectByType<StageMapController>();
            var mover = Object.FindFirstObjectByType<StageCameraController>();
            var nodes = Object.FindObjectsByType<StageNode>(FindObjectsSortMode.None).OrderBy(n => n.Number).ToArray();
            Require(controller.HUD.IsHintVisible,"Playable island shows the click/arrow hint after the camera settles.");
            var locked = nodes.LastOrDefault(n => n.State == StageState.Locked);
            int selected = StageProgress.SelectedStage;
            if (locked)
            {
                var p = Camera.main.WorldToScreenPoint(locked.transform.position + Vector3.up * .7f);
                await Click(mouse, new Vector2(p.x, p.y));
                Require(locked.LockVisual.Rejection>0,"Locked island reacts visually without a dialogue.");
                Require(StageProgress.SelectedStage == selected && !mover.IsMoving && SceneManager.GetActiveScene().name == "Stage", "Locked pointer must not enter.");
            }
            int index = Array.FindIndex(nodes, n => n.State != StageState.Locked);
            Require(index >= 0, "At least one island must be playable.");
            var card = (Rect)typeof(StageMapController).GetMethod("CardRect", Private).Invoke(controller, new object[] { index });
            float clickedAt = Time.realtimeSinceStartup;
            await Click(mouse, new Vector2(card.center.x, Screen.height - card.center.y));
            Require(StageProgress.SelectedStage == nodes[index].Number && mover.IsMoving, "Clicking a status card must start the chosen island's camera move.");
            Require(!controller.HUD.IsHintVisible,"Camera approach hides the click/arrow hint.");
            Require(SceneManager.GetActiveScene().name == "Stage", "Scene must wait for the camera.");
            while (SceneManager.GetActiveScene().name != "Defense" && Time.realtimeSinceStartup - clickedAt < 12)
                await Task.Delay(50);
            Require(SceneManager.GetActiveScene().name == "Defense", "Camera and fade must reach Defense.");
            Require(Time.realtimeSinceStartup - clickedAt >= StageCameraController.Duration + .3f, "Fade must follow the completed approach.");
            return "PASS: click/arrow hint; locked island reacts visually and blocks entry; status-card click selects the playable island; camera completes before fade and Defense entry.";
        }
        finally
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            original.editorInputBehaviorInPlayMode = inputBehavior;
            original.backgroundBehavior = backgroundBehavior;
            Application.runInBackground = runInBackground;
        }
    }
}
