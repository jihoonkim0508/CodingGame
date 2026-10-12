using System.IO;
using System.Linq;
using UnityEditor;

namespace CodingGame.Defense.Editor
{
    // Docs/flow_stage1/*.json is the dialogue source; Unity can only load assets, so mirror it into the TextAssets
    // the scene references (same files, same GUIDs) on editor load, before Play and on demand.
    [InitializeOnLoad]
    static class StageOneContentSync
    {
        const string Source = "Docs/flow_stage1", Target = "Assets/!_Project/Defense/Tutorial/Stage1";
        static StageOneContentSync()
        {
            EditorApplication.delayCall += () => Sync(false);
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Sync(false); };
        }
        [MenuItem("Tools/Develop/Stage 1 대사 JSON 동기화")]
        static void SyncFromMenu() => Sync(true);
        static void Sync(bool report)
        {
            if (!Directory.Exists(Source)) return;
            var changed = Directory.GetFiles(Source, "*.json").Where(source =>
            {
                string target = Path.Combine(Target, Path.GetFileName(source));
                if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(source))) return false;
                File.Copy(source, target, true); AssetDatabase.ImportAsset(target.Replace('\\', '/'));
                return true;
            }).Select(Path.GetFileName).ToArray();
            if (changed.Length > 0 || report)
                UnityEngine.Debug.Log(changed.Length > 0 ? "[Stage1] 대사 JSON 동기화: " + string.Join(", ", changed) : "[Stage1] 대사 JSON 변경 없음");
        }
    }
}
