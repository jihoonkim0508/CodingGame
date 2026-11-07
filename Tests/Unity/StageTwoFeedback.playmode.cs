// Unity MCP run_script: StageTwoFeedbackCheck.Run, in a fresh Play after rebuilding Stage 2 assets.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class StageTwoFeedbackCheck
{
    const string Folder = "Temp/Stage2Feedback";
    static string log;
    static DefenseBattle Battle => DevTest.Battle;
    static TutorialDirector Director => Battle.Tutorial;
    static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Check(bool okay, string reason)
    { if (!okay) throw new Exception(reason); log += "PASS: " + reason + "\n"; File.WriteAllText(Folder + "/feedback-check.log", log); }
    static async Task Wait(Func<bool> condition, string reason)
    {
        var limit = DateTime.UtcNow.AddSeconds(20);
        while (!condition()) { if (DateTime.UtcNow > limit) throw new Exception("Timeout: " + reason + "\n" + DevTest.State()); await Task.Delay(50); }
    }
    static async Task Jump(int stage, int wave, string id)
    {
        int index = Array.FindIndex(DevTest.TutorialSections(stage, wave), s => s.StartsWith(id + " "));
        DevTest.JumpTutorial(stage, wave, index);
        await Wait(() => DevTest.Ready && Battle.Stage == stage && DevTest.Sim.WaveIndex == wave - 1 && Director.EditorSynced && Director.EditorStep?.stepId == id, "jump " + stage + "-" + wave + "/" + id);
    }
    static void Next() { Director.EditorFinishTyping(); Director.EditorNext(); }
    static void Click(Button button)
    {
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().rootCanvas.worldCamera, rect.TransformPoint(rect.rect.center));
        var e = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(e, hits);
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject, "hint button receives the pointer raycast");
        ExecuteEvents.Execute(button.gameObject, e, ExecuteEvents.pointerClickHandler);
    }
    static async Task Screenshot(string name)
    { ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/" + name + ".png")); await Task.Delay(150); }
    static async Task Hint(int wave, RobotRole role)
    {
        await Jump(2, wave, wave == 5 ? "A05" : "D01"); Next(); // preparation -> free practice, not a completed-wave shortcut
        await Wait(() => Director.EditorStep?.stepId == "WaitResult", "free practice ready");
        var bot = DevTest.Sim.Robots.First(r => r.Spec.role == role);
        Battle.SelectRobot(bot.Id); Battle.DevelopSelected(); await Task.Delay(150);
        var editor = Battle.CodeEditor; var button = Field<Button>(editor, "hintButton");
        var popup = Field<GameObject>(editor, "hintPopup"); var text = Field<TMP_Text>(editor, "hintText");
        var source = editor.Panel.AppliedSource;
        Check(button.gameObject.activeInHierarchy && text.text == StageTwoLearning.HintCode(role), "2-" + wave + " uses the same short code-example hint as Stage 1");
        Click(button); Check(editor.HintOpen && button.GetComponentInChildren<TMP_Text>().text == "힌트 닫기", "hint opens and keeps its identity as hint close");
        Check(text.preferredHeight <= ((RectTransform)popup.transform).rect.height && !text.isTextOverflowing, "hint fits inside its popup");
        await Screenshot("hint-2-" + wave);
        Click(button); Check(!editor.HintOpen && button.GetComponentInChildren<TMP_Text>().text == "힌트", "same button closes the hint");
        Check(editor.Panel.AppliedSource == source, "opening a hint does not insert or change code");
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder); log = ""; Application.runInBackground = true;
        await Jump(2, 1, "C04");
        Check(Director.EditorStep.kind == TutorialStepKind.Dialogue, "2-1 observation uses normal dialogue navigation");
        Check(!Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(b => b.name == "Observation next command"), "no extra preview execution button exists");
        for (int iteration = 1; iteration <= 3; iteration++)
        {
            await Wait(() => Battle.AttackPreview.ObservationReady(iteration), "automatic iteration " + iteration);
            Check(Director.EditorStep.stepId == "C0" + (iteration + 3), "observation stays on its dialogue until Next");
            Check(Battle.AttackPreview.ObservationProgram.FinishedIterations == iteration, "actual execution reaches iteration " + iteration);
            Check(Battle.AttackPreview.ObservationProgram.Iteration == iteration, "visible iteration stays on the demonstrated round");
            await Screenshot("iteration-" + iteration);
            Next(); await Task.Delay(100);
        }
        await Wait(() => Battle.AttackPreview.ObservationProgram.ObservationFinished, "program finishes once");
        Check(Battle.AttackPreview.ObservationProgram.ObservedCommands == 6, "automatic demo runs exactly three command pairs");
        Next(); Next(); Next(); // C07 -> C08 -> C09 -> close instruction
        await Hint(2, RobotRole.Warrior);
        await Hint(4, RobotRole.Shooter);
        await Hint(5, RobotRole.Shooter);

        foreach (int wave in new[] { 1, 3 })
        {
            await Jump(2, wave, "A01");
            var notice = Object.FindFirstObjectByType<TutorialSystemAlert>();
            Check(notice.IsShowing && notice.Tone == (wave == 1 ? TutorialNoticeTone.Calm : TutorialNoticeTone.Positive), "2-" + wave + " entry/unlock uses calm or positive notice styling");
            var message = Field<TMP_Text>(notice, "message"); var stripes = Field<RectTransform>(notice, "stripes");
            Check(message.color.g > message.color.r && !stripes.parent.gameObject.activeSelf, "unlock has teal text and no warning stripes");
            Check(Field<RectTransform>(notice, "shake").anchoredPosition == Vector2.zero, "unlock does not shake like an alarm");
            await Task.Delay(350); await Screenshot("unlock-2-" + wave);
        }
        await Jump(1, 4, "A01");
        var warning = Object.FindFirstObjectByType<TutorialSystemAlert>();
        Check(warning.Tone == TutorialNoticeTone.Warning && Field<TMP_Text>(warning, "message").color.r > .9f &&
            Field<RectTransform>(warning, "stripes").parent.gameObject.activeSelf, "enemy arrival retains the red warning and stripes");
        await Task.Delay(350); await Screenshot("enemy-warning");
        await Jump(2, 1, "C04");
        return log;
    }
}
