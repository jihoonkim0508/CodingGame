using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodingGame.BlockCoding;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // First-battle frame probe: Stage 3's first 25 seconds played in real time (every robot looping its action), twice in one editor
    // session, with the editor profiler on (Edit Mode target, so EditorLoop has its children). TestResults/Editor/frame-profile-<api>-<play>.txt
    // gets the frame-time stats, every frame over 50 ms with a few markers, and the main-thread call tree of each profiler frame over
    // 50 ms; the second play shows what is left once the editor has warmed up. From the command line the editor quits when done:
    //   Unity.exe -projectPath . -force-d3d12 -executeMethod CodingGame.Defense.Editor.FrameProbe.Run
    [InitializeOnLoad]
    public static class FrameProbe
    {
        const string Key = "FrameProbe.On", PassKey = "FrameProbe.Pass", TimingKey = "FrameProbe.Timing";
        static FrameProbe() { EditorApplication.update += Tick; }
        static bool FromCommandLine => Environment.GetCommandLineArgs().Contains("-executeMethod");

        [MenuItem("Tools/Develop/Measure First Battle Frames")]
        public static void Run()
        {
            SessionState.SetBool(TimingKey, PlayerSettings.enableFrameTimingStats);
            PlayerSettings.enableFrameTimingStats = true;
            SessionState.SetBool(Key, true);
            SessionState.SetInt(PassKey, 1);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            if (FromCommandLine) PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "FrameProbe");
            Play();
        }
        static void Play() => DevTest.PlayDefense(3, 1, true, new DefenseTestOptions { tutorial = false, tutorialInput = false });

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || !DevTest.Ready) return;
            if (!UnityEngine.Object.FindFirstObjectByType<Recorder>()) new GameObject("FrameProbe").AddComponent<Recorder>();
        }

        static void Finish()
        {
            EditorApplication.isPlaying = false;
            if (SessionState.GetInt(PassKey, 1) >= 2)
            {
                SessionState.SetBool(Key, false);
                PlayerSettings.enableFrameTimingStats = SessionState.GetBool(TimingKey, false);
                Debug.Log("FrameProbe: TestResults/Editor/frame-profile-*.txt");
                if (FromCommandLine) EditorApplication.delayCall += () => EditorApplication.Exit(0);
                return;
            }
            SessionState.SetInt(PassKey, 2);
            void Again(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.EnteredEditMode) return;
                EditorApplication.playModeStateChanged -= Again;
                EditorApplication.delayCall += Play;
            }
            EditorApplication.playModeStateChanged += Again;
        }

        sealed class Recorder : MonoBehaviour
        {
            struct Frame { public float t, dt, cpu, gpu, presentWait; public byte wave, phase; public short enemies; public bool boss; public float[] marks; }
            static readonly string[] Wanted = { "WaitForLastPresentation", "WaitForPresentOnGfxThread", "WaitForGfxCommandsFromMainThread",
                "Gfx.PresentFrame", "GC.Collect", "CreateGPUProgram", "PlayerLoop", "EditorLoop", "Loading.ReadObject", "Shader.Parse" };
            readonly List<Frame> frames = new List<Frame>(60000);
            readonly List<(string name, ProfilerRecorder rec)> recs = new List<(string, ProfilerRecorder)>();
            readonly List<string> events = new List<string>();
            readonly FrameTiming[] timing = new FrameTiming[1];
            readonly StringBuilder prof = new StringBuilder();
            float start, phaseSince;
            int lastPhase = -1, nextProf = -1;
            double lastScanMs;
            bool sawBoss, done;

            void Start()
            {
                DontDestroyOnLoad(gameObject);
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var h in handles)
                {
                    var d = ProfilerRecorderHandle.GetDescription(h);
                    if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds || !Wanted.Any(w => d.Name.Contains(w)) || recs.Count >= 24) continue;
                    var r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default);
                    if (!r.IsRunning) r.Start();
                    recs.Add((d.Name, r));
                }
                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.profileEditor = true;
                ProfilerDriver.enabled = true;
                start = Time.realtimeSinceStartup;
                events.Add($"{SystemInfo.graphicsDeviceType} · {SystemInfo.graphicsDeviceName} · VRAM {SystemInfo.graphicsMemorySize}MB · 화면 {Screen.width}x{Screen.height} · " +
                    $"목표 {Application.targetFrameRate}fps · vSync {QualitySettings.vSyncCount} · 플레이 {SessionState.GetInt(PassKey, 1)}번째");
                events.Add("마커: " + string.Join(", ", recs.Select(r => r.name)));
            }

            void Update()
            {
                if (done) return;
                var battle = DevTest.Battle; var sim = battle ? battle.Simulation : null;
                if (sim == null) return;
                FrameTimingManager.CaptureFrameTimings();
                bool timed = FrameTimingManager.GetLatestTimings(1, timing) > 0;
                int active = 0; bool boss = false;
                foreach (var e in sim.Enemies) if (e.Active) { active++; boss |= e.Boss; }
                var f = new Frame { t = Time.realtimeSinceStartup - start, dt = Time.unscaledDeltaTime * 1000, wave = (byte)sim.WaveIndex,
                    phase = (byte)sim.Phase, enemies = (short)active, boss = boss, marks = new float[recs.Count] };
                if (timed) { f.cpu = (float)timing[0].cpuFrameTime; f.gpu = (float)timing[0].gpuFrameTime; f.presentWait = (float)timing[0].cpuMainThreadPresentWaitTime; }
                for (int i = 0; i < recs.Count; i++) f.marks[i] = recs[i].rec.LastValue / 1e6f;
                frames.Add(f);
                if (boss && !sawBoss) { sawBoss = true; events.Add($"{f.t:0.0}s #{frames.Count - 1}: 보스 등장 (웨이브 {sim.WaveIndex + 1})"); }
                if ((int)sim.Phase != lastPhase) { lastPhase = (int)sim.Phase; phaseSince = f.t; events.Add($"{f.t:0.0}s #{frames.Count - 1}: {sim.Phase} (웨이브 {sim.WaveIndex + 1})"); }
                Scan(f);
                Drive(battle, sim, f.t);
            }

            // Every finished profiler frame over 50 ms gets its call tree; "앞 분석" is this scan's own cost in the frame before.
            void Scan(Frame now)
            {
                int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
                if (last < 0) return;
                if (nextProf < first) nextProf = first;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (; nextProf <= last; nextProf++)
                {
                    float ms;
                    using (var raw = ProfilerDriver.GetRawFrameDataView(nextProf, 0)) { if (!raw.valid) continue; ms = raw.frameTimeMs; }
                    if (ms > 50) ProfileTree.Append(prof, nextProf, ms, $" · 기록 시점 {now.t:0.00}s 웨이브 {now.wave + 1} {(BattlePhase)now.phase} 적 {now.enemies} · 앞 분석 {lastScanMs:0.0}ms");
                }
                lastScanMs = sw.Elapsed.TotalMilliseconds;
            }

            void Drive(DefenseBattle battle, DefenseSimulation sim, float t)
            {
                if (t > 25) { Write("25초"); return; }
                switch (sim.Phase)
                {
                    case BattlePhase.Ready:
                        if (t - phaseSince < 2) return;
                        phaseSince = t;
                        try { DevTest.AutoDeploy(); } catch (Exception e) { events.Add("AutoDeploy: " + e.Message); }
                        foreach (var r in sim.Robots) sim.ApplyProgram(r.Id, "robot_" + r.Id, Array.Empty<CodeBlock>());
                        BeginnerLessonChecks.Loops(sim);
                        battle.StartBattle();
                        if (sim.Phase != BattlePhase.Running) { events.Add("시작 실패: " + sim.StartError()); Write("시작 실패"); }
                        break;
                    case BattlePhase.Paused: sim.TogglePause(); break;
                    case BattlePhase.Reward: if (t - phaseSince > 3) { phaseSince = t; battle.ContinueCampaign(); } break;
                    case BattlePhase.Victory: case BattlePhase.Defeat: if (t - phaseSince > 3) Write(sim.Phase.ToString()); break;
                }
            }

            static string Stats(IEnumerable<Frame> set)
            {
                var dts = set.Select(f => f.dt).OrderBy(x => x).ToArray();
                if (dts.Length == 0) return "프레임 없음";
                float P(double q) => dts[Math.Min(dts.Length - 1, (int)(q * dts.Length))];
                return $"프레임 {dts.Length} · 중앙값 {P(.5):0.00}ms · p99 {P(.99):0.00}ms · 최대 {dts[dts.Length - 1]:0.0}ms · " +
                    $"33ms 넘음 {dts.Count(x => x > 33)} · 50ms 넘음 {dts.Count(x => x > 50)} · 100ms 넘음 {dts.Count(x => x > 100)}";
            }

            void Write(string reason)
            {
                done = true;
                ProfilerDriver.enabled = false;
                var api = SystemInfo.graphicsDeviceType.ToString();
                var s = new StringBuilder();
                s.AppendLine($"FrameProbe {api} · 끝: {reason}");
                foreach (var e in events) s.AppendLine(e);
                var run = frames.Skip(1).ToList();
                s.AppendLine("전체 " + Stats(run));
                s.AppendLine("전투 중(Running) " + Stats(run.Where(f => f.phase == (byte)BattlePhase.Running)));
                s.AppendLine("결과·준비 화면 " + Stats(run.Where(f => f.phase != (byte)BattlePhase.Running)));
                var gpu = run.Where(f => f.gpu > 0).Select(f => f.gpu).OrderBy(x => x).ToArray();
                s.AppendLine(gpu.Length > 0 ? $"GPU 프레임(FrameTiming) 중앙값 {gpu[gpu.Length / 2]:0.00}ms · p99 {gpu[(int)(gpu.Length * .99)]:0.00}ms · 최대 {gpu[gpu.Length - 1]:0.0}ms"
                    : "GPU 프레임 시간 없음(FrameTimingManager 꺼짐)");
                s.AppendLine("--- 50ms 넘는 프레임 (앞뒤 1프레임 포함, 마커 ms, 1ms 이상만)");
                for (int i = 1; i < frames.Count; i++)
                {
                    if (frames[i].dt <= 50) continue;
                    for (int j = Math.Max(0, i - 1); j <= Math.Min(frames.Count - 1, i + 1); j++)
                    {
                        var f = frames[j];
                        var marks = string.Join(" ", recs.Select((r, k) => (r.name, v: f.marks[k])).Where(m => m.v >= 1).Select(m => $"{m.name}={m.v:0.0}"));
                        s.AppendLine($"{(j == i ? ">" : " ")} #{j} {f.t:0.00}s 웨이브 {f.wave + 1} {(BattlePhase)f.phase} 적 {f.enemies}{(f.boss ? " 보스" : "")} " +
                            $"dt {f.dt:0.0} cpu {f.cpu:0.0} gpu {f.gpu:0.0} presentWait {f.presentWait:0.0} | {marks}");
                    }
                }
                Directory.CreateDirectory("TestResults/Editor");
                s.AppendLine().Append(prof);
                File.WriteAllText($"TestResults/Editor/frame-profile-{api}-{SessionState.GetInt(PassKey, 1)}.txt", s.ToString());
                foreach (var r in recs) r.rec.Dispose();
                Finish();
            }
        }
    }
}
