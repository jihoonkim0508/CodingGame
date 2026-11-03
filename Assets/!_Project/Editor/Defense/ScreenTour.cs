using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Screenshots of the first session the way a player meets it, for reviewing layout and text by eye: the title, the stage
    // map, every Stage 1-1 tutorial step, then a 2-1 robot's code editor, its battle and the wave result. Needs the Game view,
    // so it runs in the windowed Editor, not batchmode; started from the command line it quits by itself when done:
    //   Unity.exe -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.Run -logFile tour.log
    // Writes TestResults/Editor/tour/NN_name.png at 1920x1080.
    public static class ScreenTour
    {
        const string Folder = "TestResults/Editor/tour";
        static readonly DefenseTestOptions Sandbox = new DefenseTestOptions { tutorial = false, tutorialInput = false };
        // Steps yield nested waits; the top of the stack is the one running.
        static readonly System.Collections.Generic.Stack<IEnumerator> tour = new System.Collections.Generic.Stack<IEnumerator>();
        static int shot;

        [MenuItem("Tools/Develop/화면 캡처 투어")]
        public static void Run()
        {
            if (Directory.Exists(Folder)) foreach (var old in Directory.GetFiles(Folder, "*.png")) File.Delete(old);
            Directory.CreateDirectory(Folder);
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "ScreenTour");
            shot = 0; tour.Clear(); tour.Push(Steps());
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            try
            {
                if (tour.Peek().MoveNext()) { if (tour.Peek().Current is IEnumerator inner) tour.Push(inner); }
                else tour.Pop();
            }
            catch (System.Exception error) { Debug.LogError("[ScreenTour] " + error); tour.Clear(); }
            if (tour.Count > 0) return;
            EditorApplication.update -= Tick;
            Debug.Log($"[ScreenTour] {shot} shots → {Folder}");
            EditorApplication.isPlaying = false;
            // Quitting from inside Play Mode crashed the Editor on the way out; leave Play Mode first.
            if (System.Environment.GetCommandLineArgs().Contains("-executeMethod")) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        static IEnumerator Frames(int count) { for (int end = Time.frameCount + count; Time.frameCount < end;) yield return null; }
        static IEnumerator Until(System.Func<bool> done, double seconds = 60)
        {
            for (double end = EditorApplication.timeSinceStartup + seconds; !done() && EditorApplication.timeSinceStartup < end;) yield return null;
        }
        static IEnumerator Shot(string name)
        {
            bool saved = false;
            var host = new GameObject("ScreenTour").AddComponent<Capture>();
            host.path = Path.Combine(Folder, $"{++shot:00}_{name}.png"); host.done = () => saved = true;
            yield return Until(() => saved, 10);
            if (host) Object.DestroyImmediate(host.gameObject);
        }
        sealed class Capture : MonoBehaviour
        {
            public string path; public System.Action done;
            IEnumerator Start()
            {
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Destroy(image); done();
            }
        }

        static IEnumerator Steps()
        {
            DevTest.PlayTitle();
            yield return Until(() => EditorApplication.isPlaying);
            yield return Frames(600); yield return Shot("title"); // after the intro, with the menu
            DevTest.PlayStageMap();
            yield return Frames(150); yield return Shot("map");

            DevTest.PlayDefense(1, 1, true);
            yield return Until(() => DevTest.Ready);
            yield return Frames(90);
            var steps = DevTest.TutorialSections(1);
            for (int i = 0; i < steps.Length; i++)
            {
                try { DevTest.JumpTutorial(1, i); }
                catch (System.InvalidOperationException error) { Debug.LogWarning($"[ScreenTour] 1-1 {steps[i]}: {error.Message}"); continue; }
                yield return Frames(45);
                yield return Shot("1-1_" + steps[i].Split(' ')[0]);
            }

            DevTest.PlayDefense(2, 1, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Frames(30);
            DevTest.AutoDeploy();
            yield return Frames(30); yield return Shot("2-1_ready");
            var battle = DevTest.Battle;
            battle.SelectRobot(battle.Simulation.Robots[0].Id); battle.DevelopSelected();
            yield return Frames(60); yield return Shot("2-1_code");
            battle.CodeEditor.Close();
            battle.StartBattle();
            yield return Frames(400); yield return Shot("2-1_battle");
            DevTest.RunWave();
            yield return Frames(240); yield return Shot("2-1_result"); // past the clear banner, on the reward panel
        }
    }
}
