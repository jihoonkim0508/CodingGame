#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.TitleScreen
{
    // Explicit editor-only boot capture, armed for one Play session via SessionState.
    public sealed class TitleOpeningProbe : MonoBehaviour
    {
        const string Reports = "C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/OpeningReports/";
        readonly List<string> results = new List<string>();
        bool finished;
        float began;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!SessionState.GetBool("TitleOpeningProbe.Armed", false)) return;
            SessionState.SetBool("TitleOpeningProbe.Armed", false);
            if (SceneManager.GetActiveScene().name != "Title") return;
            DontDestroyOnLoad(new GameObject("Temporary first-frame QA").AddComponent<TitleOpeningProbe>().gameObject);
            Application.runInBackground = true;
        }
        IEnumerator Start()
        {
            began = Time.realtimeSinceStartup; Directory.CreateDirectory(Reports);
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
        void Update() { if (!finished && began > 0 && Time.realtimeSinceStartup - began > 45) Finish("Timeout"); }
        void Check(bool value, string message) { if (!value) throw new Exception(message); results.Add("PASS: " + message); }
        IEnumerator Run()
        {
            var title = FindFirstObjectByType<TitlePresentation>();
            Check(title && title.openingBlackHold >= .4f, "Opening black hold is configured");
            yield return new WaitForEndOfFrame();
            Check(title.Elapsed < title.openingBlackHold && title.teamGroup.alpha == 0 && title.battlefieldGroup.alpha == 0 && title.logoGroup.alpha == 0 && title.menuGroup.alpha == 0, "First rendered frame hides both logos, battlefield and menu");
            Save("01_First_Frame_Black.png", true);
            while (title.Elapsed < title.openingBlackHold + .3f) yield return null;
            yield return new WaitForEndOfFrame();
            Check(title.teamGroup.alpha > 0 && title.teamGroup.alpha < 1 && title.battlefieldGroup.alpha == 0, "DF fades in gradually after black hold");
            Save("02_DF_Fade_In.png");
            while (title.Elapsed < title.openingBlackHold + title.teamFadeIn + .1f) yield return null;
            yield return new WaitForEndOfFrame();
            Check(title.teamGroup.alpha > .99f && title.battlefieldGroup.alpha == 0, "DF fully appears alone before the battlefield");
            Save("03_DF_On_Black.png");
            title.SkipIntro();
            Check(!title.IsReady && title.menuGroup.alpha == 0 && title.logoGroup.alpha == 0, "Splash skip still preserves background and title entrance");
            while (!title.IsReady) yield return null;
            yield return null; yield return null; yield return null;
            Check(title.startButton.interactable, "Menu becomes usable after entrance");
            title.startButton.onClick.Invoke();
            while (SceneManager.GetActiveScene().name != "Stage") yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            yield return new WaitForEndOfFrame(); Save("04_Stage_Raised_Hint.png");
            Check(FindFirstObjectByType<StageMap.StageMapController>().RecommendedStage != null, "Stage recommendation remains available");
        }
        void Save(string name, bool requireBlack = false)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Reports + name, texture.EncodeToPNG());
                if (requireBlack)
                {
                    int max = 0;
                    foreach (var p in texture.GetPixels32()) max = Mathf.Max(max, Mathf.Max(p.r, Mathf.Max(p.g, p.b)));
                    Check(max <= 1, "First rendered frame is black across the entire image (max RGB=" + max + ")");
                }
            }
            finally { Destroy(texture); }
        }
        void Finish(string error)
        {
            if (finished) return; finished = true;
            if (error != null) results.Add("FAIL: " + error);
            results.Add("RESULT: " + (error == null ? "PASS" : "FAIL"));
            File.WriteAllLines(Reports + "EditorValidation.txt", results);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
