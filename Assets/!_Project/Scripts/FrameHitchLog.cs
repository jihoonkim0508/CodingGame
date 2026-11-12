using System.Collections.Generic;
using CodingGame.Defense;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame
{
    // Development builds only: Player.log gets a frame-time summary every minute and a line for each frame over 50 ms, saying
    // where its time went (main thread, render thread, GPU, waiting for the display) and what was on screen, so a lag report
    // from a tester's PC names the side to look at. Lines start with [Frame].
    public sealed class FrameHitchLog : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() => DontDestroyOnLoad(new GameObject(nameof(FrameHitchLog), typeof(FrameHitchLog)));
#endif
        const float HitchMs = 50, SummarySeconds = 60;
        readonly List<float> times = new List<float>(10000);
        readonly FrameTiming[] timing = new FrameTiming[1];
        ulong lastFrame;
        float since;
        bool started;
        DefenseBattle battle;

        void Start() => Log($"시작 · {SystemInfo.graphicsDeviceType} · {SystemInfo.graphicsDeviceName} · {Screen.width}x{Screen.height} · " +
            $"{GraphicsOptions.DisplayHz}Hz · vSync {QualitySettings.vSyncCount} · FrameTiming {(FrameTimingManager.IsFeatureEnabled() ? "켜짐" : "꺼짐")}");

        void Update()
        {
            if (!started) { started = true; return; } // the first frame's delta is the game's start-up
            float ms = Time.unscaledDeltaTime * 1000;
            times.Add(ms);
            since += Time.unscaledDeltaTime;
            if (FrameTimingManager.IsFeatureEnabled())
            {
                // The timings arrive a few frames late; each is logged once, by its own frame's numbers.
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastFrame)
                {
                    var t = timing[0];
                    lastFrame = t.frameStartTimestamp;
                    if (t.cpuFrameTime > HitchMs)
                        Log($"끊김 {t.cpuFrameTime:0}ms · 메인 스레드 {t.cpuMainThreadFrameTime:0.0} · 렌더 스레드 {t.cpuRenderThreadFrameTime:0.0} · " +
                            $"GPU {t.gpuFrameTime:0.0} · 화면 표시 대기 {t.cpuMainThreadPresentWaitTime:0.0} · {Where()}");
                }
            }
            else if (ms > HitchMs) Log($"끊김 {ms:0}ms · {Where()}");
            if (since >= SummarySeconds) Summary();
        }

        void OnApplicationQuit() { if (times.Count > 0) Summary(); }

        void Summary()
        {
            times.Sort();
            int n = times.Count, over = 0;
            foreach (var x in times) if (x > HitchMs) over++;
            Log($"최근 {since:0}초 · 평균 {n / since:0}fps · 중앙값 {times[n / 2]:0.0}ms · p99 {times[Mathf.Min(n - 1, (int)(n * .99f))]:0.0}ms · " +
                $"최대 {times[n - 1]:0}ms · 50ms 넘음 {over}번 · {Where()}");
            times.Clear();
            since = 0;
        }

        string Where()
        {
            if (!battle) battle = FindFirstObjectByType<DefenseBattle>();
            var sim = battle ? battle.Simulation : null;
            return SceneManager.GetActiveScene().name + (sim != null ? $" {sim.Setup.Stage}-{sim.WaveIndex + 1} {sim.Phase}" : "");
        }

        static void Log(string line) =>
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Frame] {0:0.0}s {1}", Time.realtimeSinceStartup, line);
    }
}
