// Run in Defense Play Mode using unity command run_script.
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using CodingGame.Defense;
using TMPro;
using Point = System.Numerics.Vector2;

public static class PlacementFeedbackCheck
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Place(DefenseBattle battle, RobotRole role)
    {
        int index = Enumerable.Range(0, 6).Single(i => battle.DefinitionRole(i) == role);
        var sim = battle.Simulation;
        for (float x = sim.Setup.Min.X + 1; x < sim.Setup.Max.X - 1; x++)
            for (float y = sim.Setup.Min.Y + 1; y < sim.Setup.Max.Y - 1; y++)
                if (sim.PlacementError(battle.RobotDefinition(index).stats, new Point(x, y)) == null &&
                    battle.PlaceRobot(index, new Vector3(x, 0, y))) return;
        throw new Exception("No legal placement for " + role);
    }
    public static string Main()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        var data = new SerializedObject(battle);
        data.FindProperty("stage").intValue = 2; data.ApplyModifiedProperties(); battle.Restart();
        Place(battle, RobotRole.Shooter); Place(battle, RobotRole.Shooter); Place(battle, RobotRole.Warrior);
        Place(battle, RobotRole.Utility);
        Require(battle.Simulation.Robots.Count == 4, "Stage 2 wave 1 utility must fit");
        battle.Simulation.GrantRobot(RobotRole.Shooter, 2);
        Place(battle, RobotRole.Shooter); Place(battle, RobotRole.Shooter);
        int utility = Enumerable.Range(0, 6).Single(i => battle.DefinitionRole(i) == RobotRole.Utility);
        battle.Simulation.GrantRobot(RobotRole.Utility, 1);
        Require(battle.Simulation.DeploymentError(RobotRole.Utility).Contains("설치개수 초과"), "Shared limit reason");
        Require(battle.Simulation.PlacementError(battle.RobotDefinition(utility).stats, new Point(999, 999)).Contains("설치개수 초과"), "Limit takes precedence over geometry");
        battle.ChooseRobot(utility);
        var hud = UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>();
        hud.SendMessage("LateUpdate");
        data = new SerializedObject(hud);
        var toast = (CanvasGroup)data.FindProperty("placementToast").objectReferenceValue;
        var text = (TMP_Text)data.FindProperty("placementToastText").objectReferenceValue;
        Require(toast.alpha == 1 && text.gameObject.activeInHierarchy && text.text.Contains("설치개수 초과"), "Visible limit feedback");
        var countLabel = (TMP_Text)data.FindProperty("inventory").objectReferenceValue;
        Require(countLabel.gameObject.activeInHierarchy && countLabel.text.Contains("6 / 6"), "Limit count visible");
        var cards = data.FindProperty("enemyCards"); var counts = data.FindProperty("enemyCounts");
        for (int i = 0; i < 3; i++)
        {
            bool appears = battle.Simulation.UpcomingWave.Any(g => g.DefinitionIndex == i);
            Require(((GameObject)cards.GetArrayElementAtIndex(i).objectReferenceValue).activeSelf == appears, "Only upcoming enemies visible");
        }
        var tip = UnityEngine.Object.FindFirstObjectByType<DefenseTooltip>(FindObjectsInactive.Include);
        var anchor = ((UnityEngine.UI.Button)data.FindProperty("robotButtons").GetArrayElementAtIndex(4).objectReferenceValue).transform as RectTransform;
        var shooter = battle.RobotDefinition(4);
        tip.Show(shooter.displayName, shooter.description, anchor, false);
        Canvas.ForceUpdateCanvases();
        var tipData = new SerializedObject(tip);
        var description = (TMP_Text)tipData.FindProperty("description").objectReferenceValue;
        description.ForceMeshUpdate();
        Require(description.preferredHeight <= description.rectTransform.rect.height + 1, "Tooltip description fits");
        Application.runInBackground = true;
        ScreenCapture.CaptureScreenshot("C:/Users/jihoon/.codex/tmp/placement-feedback.png");
        return "PASS: S2W1 utility 4/6; shared limit rejection; visible toast/count; absent enemies hidden; tooltip fits";
    }
    public static string AfterFade()
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>();
        var data = new SerializedObject(hud);
        var toast = (CanvasGroup)data.FindProperty("placementToast").objectReferenceValue;
        Require(toast.alpha == 0, "Toast must fade out");
        Require(((RectTransform)toast.transform).anchoredPosition == Vector2.zero, "Shake must restore position");
        return "PASS: fade completed and shake position restored";
    }
}
