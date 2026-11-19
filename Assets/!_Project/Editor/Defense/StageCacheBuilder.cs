using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Tools/Develop/스테이지 캐시: plays Stage 1 (1-1 to 1-5), 2 (2-1 to 2-10) and 3 with the lesson runner (BeginnerLessonChecks),
    // and saves each stage's play log with its tutorial marks as Resources/Dev/StageCache_N.json, which builds take too; the F1
    // panel's dialogue jump replays it (PlayLog). A few minutes in Play mode; the editor stays free between the runner's steps.
    [InitializeOnLoad]
    public static class StageCacheBuilder
    {
        const string StageKey = "CodingGame.StageCache.Stage", PendingKey = "CodingGame.StageCache.Pending", SummaryKey = "CodingGame.StageCache.Summary";
        const string Folder = "Assets/!_Project/Resources/Dev", RunnerLog = "TestResults/Editor/beginner-lessons.log";
        static StageCacheBuilder() => EditorApplication.update += Tick;

        [MenuItem("Tools/Develop/스테이지 캐시")]
        public static void Build()
        {
            SessionState.SetString(SummaryKey, "");
            Queue(1);
        }
        // The runner builds the lesson assets before it plays, which waits for Play mode to end: each stage starts from Edit mode.
        static void Queue(int stage)
        {
            SessionState.SetInt(StageKey, 0); SessionState.SetInt(PendingKey, stage);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
        static void Run(int stage)
        {
            SessionState.SetInt(StageKey, stage);
            Debug.Log($"[스테이지 캐시] Stage {stage} 기록 시작");
            try { if (stage == 1) BeginnerLessonChecks.RunAllFive(); else if (stage == 2) BeginnerLessonChecks.RunStageTwo(); else BeginnerLessonChecks.RunStageThree(); }
            catch (System.Exception error) { SessionState.SetInt(StageKey, 0); Debug.LogError($"[스테이지 캐시] Stage {stage}를 시작하지 못함: {error}"); }
        }

        static void Tick()
        {
            int pending = SessionState.GetInt(PendingKey, 0);
            if (pending != 0)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                SessionState.SetInt(PendingKey, 0); Run(pending); return;
            }
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0 || SessionState.GetBool("CodingGame.BeginnerLessons.Check", false)) return;
            // The runner has ended: its last line says how.
            string result = File.Exists(RunnerLog) ? File.ReadAllLines(RunnerLog).LastOrDefault(l => l.StartsWith("PASS") || l.StartsWith("FAIL") || l.StartsWith("INTERRUPTED")) ?? "" : "";
            var battle = DevTest.Battle;
            if (!result.StartsWith("PASS") || !battle || battle.Simulation == null)
            {
                SessionState.SetInt(StageKey, 0);
                Debug.LogError($"[스테이지 캐시] Stage {stage} 실패, 캐시를 쓰지 않음: {result}");
                return;
            }
            var cache = new StageCache { stage = stage, fingerprint = battle.StageFingerprint(stage), options = battle.Simulation.Setup.TestOptions,
                inputs = battle.Simulation.Inputs.ToArray(), markers = battle.PlayMarkers.ToArray() };
            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/StageCache_{stage}.json";
            File.WriteAllText(path, JsonUtility.ToJson(cache));
            AssetDatabase.ImportAsset(path);
            string line = $"Stage {stage}: 입력 {cache.inputs.Length}개, 대사 표시 {cache.markers.Length}개 ({string.Join(", ", cache.markers.Select(m => m.id.Split('.')[0]).Distinct().Select(s => s + " " + cache.markers.Count(m => m.id.StartsWith(s + "."))))})";
            SessionState.SetString(SummaryKey, SessionState.GetString(SummaryKey, "") + line + "\n");
            if (stage < DefenseBattle.PrototypeLastStage) { Queue(stage + 1); return; }
            SessionState.SetInt(StageKey, 0);
            Debug.Log("[스테이지 캐시] 완료\n" + SessionState.GetString(SummaryKey, ""));
        }
    }
}
