#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.TitleScreen
{
    // Opt-in editor sampling. No screenshot encoding or logging inside measured frames.
    public sealed class TitlePerformanceProbe : MonoBehaviour
    {
        const string Reports = "C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/PerformanceReports/";
        string label;
        readonly List<string> results = new List<string>();
        readonly List<double> frameMs = new List<double>(4096);
        ProfilerRecorder allocations, mainThread, canvasBatch;
        bool sampling;
        long allocTotal, allocMax, cpuTotal, canvasTotal;
        int gcStart;
        bool checkedSession;
        public static string Begin(string label)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Running Play required");
            var probe = new GameObject("Temporary performance sampling").AddComponent<TitlePerformanceProbe>();
            probe.label = label; DontDestroyOnLoad(probe.gameObject);
            Application.runInBackground = true; return "Sampling " + label;
        }
        IEnumerator Start()
        {
            allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            canvasBatch = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Canvas.BuildBatch");
            results.Add("Editor only; frame times include Editor overhead. Resolution=" + Screen.width + "x" + Screen.height);
            results.Add("Recorder validity: GC=" + allocations.Valid + ", MainThread=" + mainThread.Valid + ", Canvas=" + canvasBatch.Valid);
            var title = FindFirstObjectByType<TitlePresentation>();
            while (!title.IsReady) yield return null;
            if (FindObjectsByType<GameFlow>(FindObjectsSortMode.None).Length != 1) throw new Exception("Duplicate or missing GameFlow");
            if (StageMap.StageProgress.SelectedStage != 0) throw new Exception("Selected stage was not reset between Play sessions");
            checkedSession = true;
            results.Add("PASS: One GameFlow; selected stage reset for this Play session");
            yield return new WaitForSecondsRealtime(2);
            StartSample(); yield return new WaitForSecondsRealtime(6); EndSample("Title idle");
            GameFlow.LoadScene("Stage");
            while (SceneManager.GetActiveScene().name != "Stage") yield return null;
            yield return new WaitForSecondsRealtime(2);
            if (FindObjectsByType<StageMap.StageMapController>(FindObjectsSortMode.None).Length != 1) throw new Exception("Stage map bootstrap count is incorrect");
            if (FindFirstObjectByType<StageMap.StageMapController>().RecommendedStage.Number != 1) throw new Exception("First-play stage recommendation changed");
            results.Add("PASS: One Stage map; stage 1 recommended");
            StartSample(); yield return new WaitForSecondsRealtime(6); EndSample("Stage idle");
            // The next Play must clear this in-memory selection rather than inheriting it.
            if (checkedSession) StageMap.StageProgress.SelectedStage = 2;
            Directory.CreateDirectory(Reports);
            File.WriteAllLines(Reports + label + ".txt", results);
            EditorApplication.isPlaying = false;
        }
        void StartSample()
        {
            frameMs.Clear(); allocTotal = allocMax = cpuTotal = canvasTotal = 0;
            gcStart = GC.CollectionCount(0); sampling = true;
        }
        void Update()
        {
            if (!sampling) return;
            frameMs.Add(Time.unscaledDeltaTime * 1000.0);
            if (allocations.Valid) { long value = allocations.LastValue; allocTotal += value; allocMax = Math.Max(allocMax, value); }
            if (mainThread.Valid) cpuTotal += mainThread.LastValue;
            if (canvasBatch.Valid) canvasTotal += canvasBatch.LastValue;
        }
        void EndSample(string phase)
        {
            sampling = false; int n = frameMs.Count; double total = 0; int slow = 0;
            foreach (var value in frameMs) { total += value; if (value > 33.34) slow++; }
            frameMs.Sort();
            results.Add(string.Format("{0}: frames={1}, frame avg={2:F2}ms, p95={3:F2}ms, max={4:F2}ms, >33.34ms={5}, GC avg={6:F0}B/frame, GC max={7}B, collections={8}, main avg={9:F2}ms, canvas avg={10:F3}ms",
                phase, n, total/n, frameMs[(int)((n-1)*.95)], frameMs[n-1], slow, (double)allocTotal/n, allocMax,
                GC.CollectionCount(0)-gcStart, cpuTotal/(n*1000000.0), canvasTotal/(n*1000000.0)));
        }
        void OnDestroy() { allocations.Dispose(); mainThread.Dispose(); canvasBatch.Dispose(); }
    }
}
#endif
