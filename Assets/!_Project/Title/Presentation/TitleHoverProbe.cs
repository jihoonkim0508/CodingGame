#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    // Explicit opt-in editor QA; never attached to the saved scene.
    public sealed class TitleHoverProbe : MonoBehaviour
    {
        const string Reports = "C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/HoverReports/";
        readonly List<string> results = new List<string>();
        Mouse mouse;
        bool finished;
        float began;
        public static string Begin()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Running Play mode required");
            if (FindFirstObjectByType<TitleHoverProbe>()) return "Already running";
            DontDestroyOnLoad(new GameObject("Temporary Hover QA").AddComponent<TitleHoverProbe>().gameObject);
            Application.runInBackground = true;
            return "Hover transition and rendered-frame verification started";
        }
        IEnumerator Start()
        {
            began = Time.realtimeSinceStartup;
            Directory.CreateDirectory(Reports);
            var run = Run();
            while (true)
            {
                bool more;
                try { more = run.MoveNext(); }
                catch (Exception error) { Finish(error.ToString()); yield break; }
                if (!more) break;
                yield return run.Current;
            }
            Finish(null);
        }
        void Update() { if (!finished && began > 0 && Time.realtimeSinceStartup - began > 45) Finish("Verification timeout"); }
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); results.Add("PASS: " + message); }
        void Move(Vector2 point) { InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); }
        IEnumerator Run()
        {
            var title = FindFirstObjectByType<TitlePresentation>();
            mouse = InputSystem.AddDevice<Mouse>("TitleHoverProbeMouse");
            Move(new Vector2(20, 20));
            while (!title.IsReady) yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            var start = title.startButton.GetComponent<TitleButtonMotion>();
            var quit = title.quitButton.GetComponent<TitleButtonMotion>();
            Check(start.highlight && quit.highlight, "Both buttons have connected hover markers");
            Check(!start.highlight.raycastTarget && !quit.highlight.raycastTarget, "Markers do not intercept pointer clicks");
            Check(start.highlight.Visibility == 0 && quit.highlight.Visibility == 0, "No marker while mouse is outside buttons");
            yield return new WaitForEndOfFrame(); Save("01_None.png");
            Move(RectTransformUtility.WorldToScreenPoint(null, title.startButton.transform.position));
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Check(start.IsHovered && start.highlight.Visibility > .99f && quit.highlight.Visibility == 0, "Start hover lights only Start marker");
            Check(start.transform.localScale.x > 1.02f, "Existing Start hover scale is retained");
            yield return new WaitForEndOfFrame(); Save("02_Start_Hover.png");
            Move(RectTransformUtility.WorldToScreenPoint(null, title.quitButton.transform.position));
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Check(quit.IsHovered && quit.highlight.Visibility > .99f && start.highlight.Visibility == 0, "Moving to Quit transfers marker from Start to Quit");
            yield return new WaitForEndOfFrame(); Save("03_Quit_Hover.png");
            Move(new Vector2(20, 20));
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Check(start.highlight.Visibility == 0 && quit.highlight.Visibility == 0, "Leaving buttons hides both markers");
            Check(Mathf.Abs(start.transform.localScale.x - 1) < .001f && Mathf.Abs(quit.transform.localScale.x - 1) < .001f, "Both scales return to normal");
            Check(EventSystem.current.currentSelectedGameObject == null && !EventSystem.current.sendNavigationEvents && title.startButton.navigation.mode == Navigation.Mode.None && title.quitButton.navigation.mode == Navigation.Mode.None, "Mouse-only navigation configuration remains intact");
            yield return new WaitForEndOfFrame(); Save("04_Mouse_Outside.png");
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, title.startButton.transform.position);
            Move(point); yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            float deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != "Stage" && Time.realtimeSinceStartup < deadline) yield return null;
            Check(SceneManager.GetActiveScene().name == "Stage", "Actual pointer click still enters Stage through GameFlow");
        }
        static void Save(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Reports + name, texture.EncodeToPNG()); }
            finally { Destroy(texture); }
        }
        void Finish(string error)
        {
            if (finished) return;
            finished = true;
            if (error != null) results.Add("FAIL: " + error);
            results.Add("RESULT: " + (error == null ? "PASS" : "FAIL"));
            File.WriteAllLines(Reports + "EditorValidation.txt", results);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
