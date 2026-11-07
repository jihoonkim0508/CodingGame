// Unity MCP run_script: StageTwoEditorCheck.Run, in Play. Exercises the real UI Toolkit controls and DevTest APIs.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public static class StageTwoEditorCheck
{
    static readonly string Folder = "Temp/Stage2EditorCheck";
    static string log = "";
    static DefenseEditorWindow Window => Resources.FindObjectsOfTypeAll<DefenseEditorWindow>().Single();
    static void Require(bool value, string reason)
    { if (!value) throw new Exception(reason); log += "PASS: " + reason + "\n"; File.WriteAllText(Folder + "/result.log", log); }
    static async Task Until(Func<bool> condition, string reason)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition()) { if (DateTime.UtcNow > deadline) throw new Exception(reason + "\n" + DevTest.State()); await Task.Delay(50); }
    }
    static int Step(int wave, string id) => Array.FindIndex(DevTest.TutorialSections(2, wave), s => s.StartsWith(id + " "));
    static async Task Jump(int stage, int wave, int step)
    {
        DevTest.JumpTutorial(stage, wave, step);
        await Until(() => DevTest.Ready && DevTest.Battle.Stage == stage && DevTest.Sim.WaveIndex == wave - 1 && DevTest.Battle.Tutorial.EditorSynced &&
            (step == 0 || DevTest.Battle.Tutorial.EditorStep?.stepId == DevTest.TutorialSequenceFor(stage, wave).steps[step].stepId), $"jump {stage}-{wave} index {step}");
        await Task.Delay(150);
    }
    static void ClickJump()
    {
        ClickButton("이 구간으로");
    }
    static void ClickButton(string text)
    {
        var button = Window.rootVisualElement.Query<Button>().ToList().Single(b => b.text == text);
        typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Invoke(button.clickable, new object[] { null, 0 });
    }
    public static async Task<string> Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play mode.");
        Directory.CreateDirectory(Folder); log = ""; Application.runInBackground = true;
        SessionState.SetString("CodingGame.EditorTab", "플레이");
        DefenseEditorWindow.Open(); Window.CreateGUI();
        var stage = Window.rootVisualElement.Q<PopupField<int>>("tutorial-stage");
        Require(stage.choices.Contains(1) && stage.choices.Contains(2), "editor offers Stage 1 and Stage 2");
        stage.value = 2;
        var wave = Window.rootVisualElement.Q<PopupField<int>>("tutorial-wave");
        Require(wave.choices.SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "Stage 2 offers all five waves");
        wave.value = 3;
        Window.rootVisualElement.Q<PopupField<string>>("tutorial-section").index = Step(3, "B04");
        var source = DevTest.TutorialSequenceFor(2, 3);
        var original = source.steps[Step(3, "B04")].texts[0].text;
        var clone = (TutorialSequence)typeof(DefenseEditorWindow).GetMethod("EditableTutorial", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Window, new object[] { source });
        Require(clone != source, "Play edits a tutorial copy");
        const string marker = "Stage 2 editor preview test";
        clone.steps[Step(3, "B04")].texts[0].text = marker;
        try
        {
            ClickJump();
            await Until(() => DevTest.Ready && DevTest.Battle.Stage == 2 && DevTest.Sim.WaveIndex == 2 && DevTest.Battle.Tutorial &&
                DevTest.Battle.Tutorial.EditorStep?.stepId == "B04" && DevTest.Battle.Tutorial.EditorStep.texts[0].text == marker, "UI button reaches the edited 2-3 B04");
            Require(DevTest.Battle.Tutorial.EditorStep.texts[0].text == marker, "UI jump previews edited dialogue");
            Require(source.steps[Step(3, "B04")].texts[0].text == original, "preview leaves original tutorial unchanged");
            Require(DevTest.Battle.CodeEditor.IsOpen && DevTest.Sim.Robots.Count(r => r.Spec.role == RobotRole.Warrior) == 2 &&
                DevTest.Sim.Robots.Count(r => r.Spec.role == RobotRole.Shooter) == 1, "2-3 jump restores editor and both roles");
            var loop = DevTest.Battle.CodeEditor.Panel.Program.StartChain().Single();
            Require(loop.Body.Blocks.Count == 2 && loop.Body.Blocks[1].Arguments[0].Blocks.Count == 0, "B04 retains the unfinished attack target for practice");
            var path = AssetDatabase.GetAssetPath(source);
            try
            {
                ClickButton("튜토리얼 원본 저장");
                Require(source.steps[Step(3, "B04")].texts[0].text == marker && File.ReadAllText(path).Contains(marker), "explicit save writes the edited tutorial asset");
            }
            finally
            {
                clone.steps[Step(3, "B04")].texts[0].text = original;
                ClickButton("튜토리얼 원본 저장");
            }
            Require(source.steps[Step(3, "B04")].texts[0].text == original && !File.ReadAllText(path).Contains(marker), "save test restores the original dialogue");
        }
        finally { clone.steps[Step(3, "B04")].texts[0].text = original; }

        await Jump(2, 1, Step(1, "C04"));
        Require(DevTest.Battle.CodeEditor.IsOpen && DevTest.Battle.AttackPreview.ObservationProgram != null &&
            StageTwoLearning.Matches(DevTest.Sim.Robots.Single().Program.CopyBlocks(), RobotRole.Warrior, 3), "2-1 observation jump restores for(3) and preview");
        await Jump(2, 2, 0);
        Require(DevTest.Sim.Robots.Count == 1 && DevTest.Sim.Robots[0].PlacedWave == 0, "2-2 starts with the previous warrior, not a completed practice robot");
        await Jump(2, 4, 0);
        Require(DevTest.Sim.Robots.Count == 3 && DevTest.Sim.Robots.Count(r => r.Spec.role == RobotRole.Shooter) == 1, "2-4 restores the earlier ranged lesson");
        await Jump(2, 5, 0);
        Require(DevTest.Sim.Robots.Count == 4 && DevTest.Sim.StartError() == null, "2-5 starts with two warriors and two shooters ready for the boss");
        await Jump(2, 5, Step(5, "Claim"));
        Require(DevTest.Sim.Phase == BattlePhase.Reward && DevTest.Sim.BossKilled, "result jump simulates a real boss kill and exposes rewards");
        DevTest.JumpTutorial(1, 0); // original two-argument API
        await Until(() => DevTest.Battle && DevTest.Battle.Stage == 1 && DevTest.Sim.WaveIndex == 0 && DevTest.Battle.Tutorial.EditorStep != null, "legacy Stage 1 API");
        Require(DevTest.Battle.Stage == 1, "Stage 1 legacy callers still work");
        bool rejected = false; try { DevTest.JumpTutorial(2, 3, 999); } catch (ArgumentException) { rejected = true; }
        Require(rejected, "invalid step is rejected before a session is changed");
        await Jump(2, 3, Step(3, "B04"));
        Window.CreateGUI();
        Window.rootVisualElement.Q<PopupField<int>>("tutorial-stage").value = 2;
        Window.rootVisualElement.Q<PopupField<int>>("tutorial-wave").value = 3;
        Window.rootVisualElement.Q<PopupField<string>>("tutorial-section").index = Step(3, "B04");
        Window.rootVisualElement.Q<Foldout>("tutorial-step-editor").value = true;
        return log;
    }
}
