#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace CodingGame.TitleScreen
{
    // Opt-in development-player probe. Never starts in a normal game/editor session.
    public sealed class TitlePlayVerification : MonoBehaviour
    {
        readonly List<string> results = new List<string>();
        string reports;
        float began;
        bool finished;
        Keyboard keyboard;
        Mouse mouse;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--title-verify");
            if (index < 0 || index + 1 >= args.Length) return;
            var probe = new GameObject("Title Verification (opt-in)").AddComponent<TitlePlayVerification>();
            probe.reports = args[index + 1];
            DontDestroyOnLoad(probe.gameObject);
        }
        IEnumerator Start()
        {
            began = Time.realtimeSinceStartup;
            Directory.CreateDirectory(reports);
            Application.runInBackground = true;
            Application.logMessageReceived += LogError;
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
        void Update() { if (!finished && began > 0 && Time.realtimeSinceStartup - began > 100) Finish("Runtime verification timeout."); }
        void LogError(string text, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) results.Add("UNITY ERROR: " + text + "\n" + trace);
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            results.Add("PASS: " + message);
        }
        IEnumerator Run()
        {
            Check(Application.productName == "CodingGameTitleVerification", "Verification player is isolated from normal product prefs");
            var title = FindFirstObjectByType<TitlePresentation>();
            Check(title != null, "Actual Title scene contains presentation controller");
            Time.timeScale = 0;
            while (title.Elapsed < title.openingBlackHold + title.teamFadeIn + .1f) yield return null;
            results.Add("DF sample: elapsed=" + title.Elapsed + ", team=" + title.teamGroup.alpha + ", world=" + title.battlefieldGroup.alpha);
            yield return Capture("01_DF.png");
            Check(title.teamLogo && title.teamLogo.sprite && title.teamGroup.alpha > .8f && title.battlefieldGroup.alpha == 0,
                "DF appears alone before battlefield, even with timeScale zero");
            Check(!title.startButton.interactable && !title.menuGroup.blocksRaycasts, "Menu cannot be clicked during intro");
            while (title.Elapsed < title.FactionsStart + title.factionEntrance * .5f) yield return null;
            Check(title.entranceLayers.Length == 6 && title.entranceLayers[0].opacity.alpha > 0 &&
                title.entranceLayers[5].opacity.alpha == 0,
                "Rear layers appear before the final foreground beat");
            Check(title.logoGroup.alpha == 0, "Logo waits until faction entrance ends");
            yield return Capture("02_Entrance.png");
            while (title.Elapsed < title.LogoStart + title.titleFadeIn * .55f) yield return null;
            Check(title.logoGroup.alpha > 0 && title.menuGroup.alpha == 0, "Logo fades before menu");
            yield return Capture("03_Logo.png");
            while (!title.IsReady) yield return null;
            yield return null; yield return null; yield return null;
            Check(title.startButton.interactable && title.quitButton.interactable && title.menuGroup.blocksRaycasts,
                "Natural intro completion enables both existing GameFlow buttons");
            Time.timeScale = 1;
            var p = title.entranceLayers[3].rect.anchoredPosition;
            yield return new WaitForSecondsRealtime(.6f);
            Check(Vector2.Distance(p, title.entranceLayers[3].rect.anchoredPosition) > .01f, "Restrained robot-layer idle motion is active");
            Check(title.entranceLayers[0].rect.anchoredPosition == Vector2.zero, "Architecture stays anchored during idle");
            yield return Capture("04_Menu_16x9.png");
            Screen.SetResolution(1280, 960, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.7f);
            Canvas.ForceUpdateCanvases();
            CheckVisible(title.logoRect); CheckVisible(title.startButton.GetComponent<RectTransform>()); CheckVisible(title.quitButton.GetComponent<RectTransform>());
            yield return Capture("05_Menu_4x3.png");
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            SceneManager.LoadScene("Title");
            yield return new WaitForSecondsRealtime(.7f);
            title = FindFirstObjectByType<TitlePresentation>();
            keyboard = InputSystem.AddDevice<Keyboard>("TitleProbeKeyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null; yield return null;
            Check(!title.IsReady && SceneManager.GetActiveScene().name == "Title", "Actual Space input skips team splash without skipping the battlefield sequence");
            Check(title.teamGroup.alpha == 0 && title.menuGroup.alpha == 0, "Team skip leaves menu hidden");
            while(!title.IsReady)yield return null;
            yield return null;yield return null;yield return null;
            yield return Capture("06_Skipped.png");
            mouse = InputSystem.AddDevice<Mouse>("TitleProbeMouse");
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, title.startButton.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 0 });
            float deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetActiveScene().name != "Stage" && Time.realtimeSinceStartup < deadline) yield return null;
            Check(SceneManager.GetActiveScene().name == "Stage", "Actual pointer click on GameStartBtn reaches Stage via unchanged GameFlow");
            yield return new WaitForSecondsRealtime(.5f);
        }
        void CheckVisible(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Check(corners.All(c => c.x >= -1 && c.x <= Screen.width + 1 && c.y >= -1 && c.y <= Screen.height + 1),
                rect.name + " remains entirely visible at " + Screen.width + "x" + Screen.height);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(reports, name));
            yield return new WaitForSecondsRealtime(.25f);
        }
        void Finish(string error)
        {
            if (finished) return;
            finished = true;
            Time.timeScale = 1;
            if (error != null) results.Add("FAIL: " + error);
            bool ok = error == null && !results.Any(x => x.StartsWith("UNITY ERROR:"));
            results.Add("RESULT: " + (ok ? "PASS" : "FAIL"));
            File.WriteAllLines(Path.Combine(reports, "PlayValidation.txt"), results);
            Application.Quit(ok ? 0 : 1);
        }
    }
}
#endif
