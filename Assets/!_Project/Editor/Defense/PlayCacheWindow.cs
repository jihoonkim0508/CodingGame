using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Tools/Develop/캐시 제작: a person plays the lessons as they should go and the F1 panel's dialogue jump opens each line as
    // that play had it. 기록 시작 (in Play mode, in a battle with the tutorial on) records from then on, across waves, stages
    // and battles; 기록 중지 writes one file per stage, Resources/Dev/PlayCache_N.json, keeping the lines of an earlier
    // recording that this one did not reach (PlayLog.Merge).
    public sealed class PlayCacheWindow : EditorWindow
    {
        public const string Folder = "Assets/!_Project/Resources/Dev";
        string result;

        [MenuItem("Tools/Develop/캐시 제작")]
        static void Open() => GetWindow<PlayCacheWindow>("캐시 제작");

        void OnEnable() => EditorApplication.update += Repaint;
        void OnDisable() => EditorApplication.update -= Repaint;

        // Why recording cannot start now, or null.
        static string Blocked()
        {
            var battle = DevTest.Battle;
            if (!battle || battle.Simulation == null) return "Play 모드에서 전투를 시작하세요";
            if (!battle.Tutorial || !battle.Simulation.Options.tutorial) return "튜토리얼이 켜진 전투에서 기록하세요";
            return null;
        }

        void OnGUI()
        {
            var recording = DefenseBattle.Recording;
            var battle = DevTest.Battle;
            string blocked = Blocked();
            string now = battle && battle.Simulation != null ? $"{battle.Stage}-{battle.Simulation.WaveIndex + 1} {battle.Simulation.Phase}" : "";
            EditorGUILayout.LabelField(recording != null ? $"기록 중 · {recording.Last ?? "-"} · 저장된 대사 {recording.Steps.Count(s => s.id.Contains('.'))}개" + (now != "" ? $" · {now}" : "")
                : blocked ?? "기록하지 않음 · " + now, EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(recording != null || blocked != null))
                    if (GUILayout.Button("기록 시작", GUILayout.Height(32))) { DefenseBattle.StartRecording(); result = null; }
                using (new EditorGUI.DisabledScope(recording == null))
                    if (GUILayout.Button("기록 중지", GUILayout.Height(32))) result = Write(DefenseBattle.StopRecording(), Folder);
            }
            EditorGUILayout.HelpBox(result ?? "기록 시작을 누른 뒤 정답대로 플레이하세요. 튜토리얼 대사마다 그때의 전투·화면이 저장됩니다. " +
                "기록 중지를 누르면 Assets/!_Project/Resources/Dev/PlayCache_<스테이지>.json에 씁니다(이번에 지난 대사만 바뀌고 나머지는 남습니다).", MessageType.None);
        }

        // Each stage's file in folder, merged with the one already there. What was written, for the window.
        public static string Write(PlayRecording recording, string folder) => recording == null || recording.Steps.Count == 0 ? "저장할 대사가 없습니다." :
            Write(recording.Files(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), folder);
        public static string Write(PlayCacheFile[] files, string folder)
        {
            Directory.CreateDirectory(folder);
            var lines = files.Select(file =>
            {
                string path = $"{folder}/PlayCache_{file.stage}.json";
                var old = File.Exists(path) ? JsonUtility.FromJson<PlayCacheFile>(File.ReadAllText(path)) : null;
                var merged = PlayLog.Merge(old, file);
                File.WriteAllText(path, JsonUtility.ToJson(merged, true));
                string dropped = old?.steps != null && old.fingerprint != file.fingerprint ? $" (예전 기록 {old.steps.Length}개는 데이터가 바뀌어 버림)" : "";
                return $"PlayCache_{file.stage}.json: 이번 {file.steps.Length}개, 전체 {merged.steps.Length}개" + dropped;
            }).ToArray();
            if (folder.StartsWith("Assets/")) AssetDatabase.Refresh();
            string summary = string.Join("\n", lines);
            Debug.Log("[캐시 제작] 저장\n" + summary);
            return summary;
        }
    }
}
