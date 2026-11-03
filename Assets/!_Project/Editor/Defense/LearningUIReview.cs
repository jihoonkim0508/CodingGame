using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    // Explicit opt-in visual/integration probe; never attaches itself to a saved scene.
    [InitializeOnLoad]
    public static class LearningUIReview
    {
        const string Key = "CodingGame.LearningUIReview";
        public static string Folder => Path.GetFullPath("Temp/LearningUIReview");
        static LearningUIReview() { EditorApplication.update += Tick; }
        public static void Baseline() => Launch("before");
        public static void After() => Launch("after");
        static void Launch(string phase)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before review.");
            SessionState.SetString(Key, phase);
            DevTest.PlayScene("Assets/!_Project/Scenes/Defense.unity");
        }
        static void Tick()
        {
            var phase = SessionState.GetString(Key, "");
            if (phase == "" || !EditorApplication.isPlaying) return;
            var director = UnityEngine.Object.FindFirstObjectByType<TutorialDirector>();
            if (!director || !director.EditorSynced) return;
            SessionState.EraseString(Key);
            Application.runInBackground = true;
            var go = new GameObject("Learning UI review (temporary)");
            go.AddComponent<LearningUIReviewRunner>().Begin(phase, director);
        }
    }
    public sealed class LearningUIReviewRunner : MonoBehaviour
    {
        string phase;
        TutorialDirector director;
        readonly StringBuilder report = new StringBuilder();
        public void Begin(string value, TutorialDirector target) { phase=value; director=target; StartCoroutine(Run()); }
        IEnumerator Run()
        {
            Directory.CreateDirectory(LearningUIReview.Folder);
            yield return new WaitForSecondsRealtime(2);
            var sequence = director.Sequences.First(s=>s.stage==1 && s.wave==1);
            var view = FindFirstObjectByType<TutorialDialogueView>(FindObjectsInactive.Include);
            director.EditorRun(sequence, Array.FindIndex(sequence.steps,s=>s.stepId=="A02"));
            director.EditorFinishTyping();
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("intro");
            var next=view.Panel.Find("NextText").GetComponent<Button>();
            var previous=view.Panel.Find("PreviousText").GetComponent<Button>();
            ExecuteEvents.Execute(next.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
            Check("Next button advances A02 to A03",director.EditorStep?.stepId=="A03");
            director.EditorFinishTyping();
            ExecuteEvents.Execute(previous.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
            Check("Previous returns to A02",director.EditorStep?.stepId=="A02");
            director.EditorRun(sequence,Array.FindIndex(sequence.steps,s=>s.stepId=="A04b"));
            director.EditorFinishTyping();
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("focus");
            director.EditorRun(sequence,Array.FindIndex(sequence.steps,s=>s.stepId=="B01"));
            director.EditorFinishTyping();
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("action");
            Check("Action step remains gated",!next.interactable && director.BlocksGameplayInput);
            Check("Dialogue text fits", !view.Panel.Find("Dialogue").GetComponent<TMP_Text>().isTextOverflowing);
            File.WriteAllText(Path.Combine(LearningUIReview.Folder, phase+"-checks.txt"),report.ToString());
            // Leave this action step running for an optional pointer check; caller stops Play explicitly.
            Destroy(gameObject);
        }
        void Check(string label,bool pass) { report.AppendLine((pass?"PASS ":"FAIL ")+label); }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(LearningUIReview.Folder, phase+"-"+name+".png"),texture.EncodeToPNG());
            Destroy(texture);
        }
    }
}
