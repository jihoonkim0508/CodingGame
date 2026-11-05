// Unity MCP run_script: TutorialNoticeCheck.Run, in Play after rebuilding Stage 2 assets.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TutorialNoticeCheck
{
    const string Folder = "Temp/TutorialNotices";
    static string log;
    static DefenseBattle Battle => DevTest.Battle;
    static TutorialDirector Director => Battle.Tutorial;
    static TutorialSystemAlert Alert => Object.FindFirstObjectByType<TutorialSystemAlert>();
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Check(bool condition, string reason)
    { if (!condition) throw new Exception(reason); log += "PASS: " + reason + "\n"; File.WriteAllText(Folder + "/notice-check.log", log); }
    static async Task Wait(Func<bool> condition, string reason)
    {
        var limit = DateTime.UtcNow.AddSeconds(15);
        while (!condition()) { if (DateTime.UtcNow > limit) throw new Exception("Timeout: " + reason + "\n" + DevTest.State()); await Task.Delay(30); }
    }
    static async Task Jump(int stage, int wave, string id)
    {
        int index = Array.FindIndex(DevTest.TutorialSections(stage, wave), s => s.StartsWith(id + " "));
        if (index < 0) throw new Exception("Missing step " + id);
        DevTest.JumpTutorial(stage, wave, index);
        await Wait(() => DevTest.Ready && Battle.Stage == stage && DevTest.Sim.WaveIndex == wave - 1 && Director.EditorSynced && Director.EditorStep?.stepId == id, "jump " + stage + "-" + wave + "/" + id);
        Battle.EditorManualClock = true;
    }
    static async Task Screenshot(string name)
    { ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/" + name + ".png")); await Task.Delay(150); }
    static void Red(string label)
    {
        var text = Field<TMP_Text>(Alert, "message");
        Check(Alert.IsShowing && Alert.Tone == TutorialNoticeTone.Warning && text.color.r > .9f && text.color.g < .4f, label + " uses a red warning");
        Check(Field<RectTransform>(Alert, "stripes").parent.gameObject.activeSelf, label + " has warning stripes");
        Check(DevTest.Sim.Phase == BattlePhase.Ready && DevTest.Sim.Spawned == 0 && !DevTest.Sim.Enemies.Any(e => e.Active), label + " is shown before the wave or any enemy spawns");
        Battle.StartBattle();
        Check(DevTest.Sim.Phase == BattlePhase.Ready, label + " prevents starting the wave while its warning is visible");
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder); log = ""; Application.runInBackground = true;
        foreach (int wave in new[] { 1, 2, 3, 4 })
        {
            var steps = DevTest.TutorialSequenceFor(2, wave).steps;
            int warning = Array.FindIndex(steps, s => s.stepId == "EnemyWarning");
            int battle = Array.FindIndex(steps, s => s.completionSignal == TutorialSignals.BattleStarted || s.trigger == TutorialSignals.WaveCleared);
            Check(warning >= 0 && warning < battle, "2-" + wave + " places the enemy warning before the battle-start/free-play step");
            await Jump(2, wave, "EnemyWarning");
            Red("2-" + wave);
            await Task.Delay(450); await Screenshot("enemy-2-" + wave);
            await Wait(() => !Alert.IsShowing && Director.EditorStep.stepId != "EnemyWarning", "warning finishes normally");
            Check(DevTest.Sim.Phase == BattlePhase.Ready, "2-" + wave + " waits for the player after the warning");
            if (wave == 1 || wave == 3)
            {
                Check(DevTest.Sim.StartError() == null, "2-" + wave + " guided code and robots are ready");
                Battle.StartBattle();
                Check(DevTest.Sim.Phase == BattlePhase.Running, "2-" + wave + " starts normally after its warning");
            }
        }
        await Jump(2, 5, "A01"); Red("2-5 boss");
        Check(Field<TMP_Text>(Alert, "positiveHeading").text == "보스 접근 경고", "boss warning has its own clear heading");
        await Task.Delay(450); await Screenshot("boss-2-5");
        await Wait(() => !Alert.IsShowing && Director.EditorStep.stepId == "A02", "boss warning leads into preparation instructions");
        Check(DevTest.Sim.Phase == BattlePhase.Ready, "boss preparation begins before the boss spawns");

        await Jump(1, 1, "A02"); Director.EditorFinishTyping();
        var view = Field<TutorialDialogueView>(Director, "view");
        Check(Director.EditorStep.attentionEntrance && Field<TMP_Text>(view, "speaker").color.r > .9f, "the guide's first contact uses red attention styling");
        Check(Field<Image>(view, "accent").color.r > .9f, "the first-contact dialogue accent is also red");
        await Task.Delay(450); await Screenshot("first-contact");

        await Jump(2, 1, "A01");
        Check(Alert.Tone == TutorialNoticeTone.Calm, "ordinary stage arrival has a separate calm tone");
        Check(!Field<RectTransform>(Alert, "stripes").parent.gameObject.activeSelf && !Field<RectTransform>(Alert, "positiveBadge").gameObject.activeSelf, "stage arrival has no hazard stripes or unlock badge");
        Check(!Field<AudioSource>(Alert, "warning").isPlaying, "stage arrival plays no alarm or unlock chime");
        await Task.Delay(200);
        Check(Field<CanvasGroup>(Alert, "fade").alpha < .95f, "stage arrival fades in gradually");
        Check(Field<RectTransform>(Alert, "shake").anchoredPosition == Vector2.zero, "stage arrival never shakes");
        await Screenshot("calm-arrival");

        await Jump(2, 2, "A01");
        // Normal wave entry has no selected robot; the developer jump deliberately selects one.
        Battle.EditorStartSession(2, 2, true, Battle.EditorOptions);
        await Wait(() => Director.EditorSynced && Director.EditorStep?.stepId == "A01", "ordinary wave entry");
        Director.EditorFinishTyping();
        view = Field<TutorialDialogueView>(Director, "view");
        await Task.Delay(80); float x = view.Panel.localPosition.x;
        Check(Field<TMP_Text>(view, "speaker").color.g > Field<TMP_Text>(view, "speaker").color.r, "normal guide speech restores its original teal accent");
        Check(view.IsCentered && view.Panel.localScale.x <= 1.251f, "later wave introductions ease in without the opening punch/overshoot");
        await Task.Delay(150);
        Check(Mathf.Abs(view.Panel.localPosition.x - x) < .01f, "normal introduction has no horizontal shake");
        await Screenshot("normal-introduction");
        await Jump(2, 3, "A01");
        Check(Alert.Tone == TutorialNoticeTone.Positive && Field<RectTransform>(Alert, "positiveBadge").gameObject.activeSelf, "new robot unlock remains a positive announcement");
        await Task.Delay(450); await Screenshot("robot-unlock");
        await Jump(2, 5, "A01");
        return log;
    }
}
