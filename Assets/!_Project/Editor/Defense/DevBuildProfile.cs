using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // A Windows development build of the enabled scenes into Builds/Dev (git-ignored), and a report of a profile that build recorded.
    //   Build:  Tools/Develop/Development Build, or Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.DevBuildProfile.Build
    //   Record: Builds/Dev/CodingGame.exe -profiler-enable -profiler-log-file Builds/Dev/profile.raw -profiler-capture-frame-count 600
    //           (the first 600 frames from start-up; close the game after a few seconds)
    //   Report: Tools/Develop/Development Build Profile Report, or the same with -executeMethod ...DevBuildProfile.Report -quit;
    //           TestResults/Editor/devbuild-profile.txt then holds every frame over 50 ms with its main-thread call tree.
    public static class DevBuildProfile
    {
        const string Exe = "Builds/Dev/CodingGame.exe", Raw = "Builds/Dev/profile.raw", Out = "TestResults/Editor/devbuild-profile.txt";

        [MenuItem("Tools/Develop/Development Build")]
        public static void Build()
        {
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64, locationPathName = Exe, options = BuildOptions.Development,
            });
            Debug.Log($"Development build {r.summary.result} · {r.summary.totalSize / 1048576}MB · {r.summary.totalTime:mm\\:ss} · {Path.GetFullPath(Exe)}");
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Tools/Develop/Development Build Profile Report")]
        public static void Report()
        {
            if (!File.Exists(Raw)) { Debug.LogError($"{Raw}이 없어요. 개발 빌드를 -profiler-enable -profiler-log-file {Raw} 로 실행하세요."); return; }
            ProfilerDriver.LoadProfile(Raw, false);
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex, slow = 0;
            double t0 = -1;
            var trees = new StringBuilder();
            for (int i = first; i <= last; i++)
            {
                float ms; double t;
                using (var raw = ProfilerDriver.GetRawFrameDataView(i, 0))
                {
                    if (!raw.valid) continue;
                    if (t0 < 0) t0 = raw.frameStartTimeMs;
                    t = (raw.frameStartTimeMs - t0) / 1000;
                    ms = raw.frameTimeMs;
                }
                if (ms <= 50) continue;
                slow++;
                ProfileTree.Append(trees, i, ms, $" · 녹화 {t:0.00}초");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Out));
            File.WriteAllText(Out, $"{Raw} · 프레임 {first}~{last} · 50ms 넘음 {slow}\n" + trees);
            Debug.Log($"Development build profile: {last - first + 1} frames, {slow} over 50 ms · {Path.GetFullPath(Out)}");
        }
    }
}
