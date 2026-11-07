// Unity MCP run_script, entry StageTwoEntryCheck.Run. Start a fresh Title Play session first.
// Uses the normal title button, temporary-clear handler, map raycast and map-entry handler.
// Does not start a Defense test session or override the selected stage/tutorial options.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame;
using CodingGame.Defense;
using CodingGame.StageMap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class StageTwoEntryCheck
{
    const string Folder = "Temp/Stage2Play";
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static async Task Until(Func<bool> condition, string message, int milliseconds = 15000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition())
        {
            if (!UnityEditor.EditorApplication.isPlaying || DateTime.UtcNow > end) throw new Exception("Timeout: " + message);
            await Task.Delay(50);
        }
    }
    public static async Task<string> Run()
    {
        Require(UnityEditor.EditorApplication.isPlaying, "Start a fresh Title Play session first.");
        Require(SceneManager.GetActiveScene().name == GameFlow.TitleScene, "Run from Title, following the normal player entry.");
        Directory.CreateDirectory(Folder);
        var messages = new List<string>();
        Application.runInBackground = true;
        var start = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "GameStartBtn");
        await Until(() => start.interactable && start.GetComponent<Image>().color.a > .5f, "Title start button ready");
        await Task.Delay(1000);
        var rect = (RectTransform)start.transform;
        var point = RectTransformUtility.WorldToScreenPoint(start.GetComponentInParent<Canvas>().rootCanvas.worldCamera, rect.TransformPoint(rect.rect.center));
        var click = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(click, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == start.gameObject, "Start button must receive the UI raycast.");
        ExecuteEvents.Execute(start.gameObject, click, ExecuteEvents.pointerClickHandler);
        await Until(() => SceneManager.GetActiveScene().name == GameFlow.StageScene && Object.FindFirstObjectByType<StageMapController>(), "Stage selection loaded");
        await Task.Delay(1000);
        var map = Object.FindFirstObjectByType<StageMapController>();
        if (StageProgress.GetState(1) == StageState.Available)
        {
            map.SendMessage("DeveloperClear", SendMessageOptions.RequireReceiver);
            messages.Add("PASS: temporary clear unlocks Stage 2 for this Play session.");
        }
        else messages.Add("INFO: Stage 1 was already cleared; Stage 2 entry checked without changing saved progress.");
        Require(StageProgress.GetState(2) != StageState.Locked, "Stage 2 must be unlocked.");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/entry-check-unlocked.png"));
        await Task.Delay(200);
        var node = Object.FindObjectsByType<StageNode>(FindObjectsSortMode.None).Single(n => n.Number == 2);
        point = Camera.main.WorldToScreenPoint(node.transform.position);
        Require(Physics.Raycast(Camera.main.ScreenPointToRay(point), out var hit), "Stage 2 island raycast hits.");
        Require(hit.collider.GetComponentInParent<StageNode>() == node, "The Stage 2 island receives the click.");
        typeof(StageMapController).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, new object[] { node });
        Require(StageProgress.SelectedStage == 2, "Map passes selected stage 2.");
        await Until(() => SceneManager.GetActiveScene().name == GameFlow.DefenseScene && Object.FindFirstObjectByType<DefenseBattle>()?.Simulation != null, "Defense loaded");
        var battle = Object.FindFirstObjectByType<DefenseBattle>();
        Require(battle.Stage == 2 && battle.Simulation.Setup.Stage == 2 && battle.Simulation.WaveIndex == 0, "Normal entry starts Stage 2 wave 1.");
        Require(battle.FromStageMap && battle.Simulation.Options.tutorial, "Map entry keeps tutorial enabled.");
        var director = battle.Tutorial;
        Require(director && Enumerable.Range(1, 5).All(w => director.Sequences.Count(s => s && s.enabled && s.stage == 2 && s.wave == w) == 1), "All five Stage 2 sequence references resolve exactly once.");
        await Until(() => director.IsActive && director.EditorStep?.stepId == "A02", "Stage 2 introduction visible");
        Require(director.EditorStep.texts.Any(t => t.text.Contains("반복문")), "Visible introduction teaches loops.");
        var view = Object.FindFirstObjectByType<TutorialDialogueView>();
        Require(view && view.IsVisible, "The Stage 1 dialogue UI is visible.");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/entry-check-tutorial.png"));
        await Task.Delay(200);
        messages.Add("PASS: Stage 2 selected -> Defense 2-1 -> loop introduction using the shared tutorial UI.");
        messages.Add("PASS: Stage 2 assets 2-1 through 2-5 resolve in the live Director.");
        var report = string.Join("\n", messages);
        File.WriteAllText(Folder + "/entry-check.log", report);
        return report;
    }
}
