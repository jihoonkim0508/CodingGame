using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    // What the battle changes in its own UI when it starts (13차: the scene in the editor still showed the old look). Opens
    // Defense.unity, notes every UI object's look (active, rect, colour, sprite, font, size), plays the battle for a few
    // seconds, notes it again and writes TestResults/Editor/ui-runtime-diff.txt: objects whose look changed at run time and
    // objects made at run time, by path. Batch: -executeMethod CodingGame.Defense.Editor.UIRuntimeDiff.Run (no -quit).
    public static class UIRuntimeDiff
    {
        const string Scene = "Assets/!_Project/Scenes/Defense.unity", Report = "TestResults/Editor/ui-runtime-diff.txt";
        static Dictionary<string, string> before;
        static double until;

        [MenuItem("Tools/Develop/UI Runtime Diff")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(Scene);
            before = Snapshot();
            SessionState.SetString("UIRuntimeDiff.before", string.Join("\u0001", before.Select(kv => kv.Key + "\u0002" + kv.Value)));
            // Played straight, the scene goes to the title (GameFlow); DevTest opens 1-1 the way the screen tour does.
            DevTest.PlayDefense(1, 1, true);
            until = 0;
            EditorApplication.update += Wait;
        }

        static void Wait()
        {
            if (!EditorApplication.isPlaying || !DevTest.Ready) return;
            if (until == 0) until = EditorApplication.timeSinceStartup + 3;
            if (EditorApplication.timeSinceStartup < until) return;
            if (before == null) before = SessionState.GetString("UIRuntimeDiff.before", "").Split('\u0001').Where(s => s.Length > 0)
                .Select(s => s.Split('\u0002')).ToDictionary(p => p[0], p => p[1]);
            EditorApplication.update -= Wait;
            var after = Snapshot();
            var report = new StringBuilder();
            var changed = before.Keys.Where(k => after.TryGetValue(k, out var v) && v != before[k]).OrderBy(k => k).ToArray();
            var made = after.Keys.Where(k => !before.ContainsKey(k)).OrderBy(k => k).ToArray();
            var gone = before.Keys.Where(k => !after.ContainsKey(k)).OrderBy(k => k).ToArray();
            report.AppendLine($"changed {changed.Length}, made at run time {made.Length}, gone {gone.Length}");
            report.AppendLine("== changed (before -> after)");
            foreach (var k in changed) report.AppendLine(k + "\n    " + Delta(before[k], after[k]));
            report.AppendLine("== made at run time");
            foreach (var k in made) report.AppendLine(k + "  " + after[k]);
            report.AppendLine("== gone");
            foreach (var k in gone) report.AppendLine(k);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, report.ToString());
            Debug.Log($"[UIRuntimeDiff] changed {changed.Length}, made {made.Length} → {Report}");
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
                EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) EditorApplication.Exit(0); };
        }

        // Only the fields that differ, so the report reads as what the code changed.
        static string Delta(string a, string b)
        {
            var x = a.Split('|'); var y = b.Split('|');
            return string.Join("  ", Enumerable.Range(0, Mathf.Max(x.Length, y.Length))
                .Where(i => i >= x.Length || i >= y.Length || x[i] != y[i])
                .Select(i => (i < x.Length ? x[i] : "") + " -> " + (i < y.Length ? y[i] : "")));
        }

        static Dictionary<string, string> Snapshot()
        {
            var result = new Dictionary<string, string>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas))
                foreach (var t in canvas.GetComponentsInChildren<RectTransform>(true))
                {
                    string path = t.name; for (var p = t.parent; p; p = p.parent) path = p.name + "/" + path;
                    if (result.ContainsKey(path)) path += "#" + t.GetSiblingIndex();
                    var look = new List<string> { "active=" + t.gameObject.activeSelf,
                        $"rect={t.anchorMin:F2},{t.anchorMax:F2},{t.anchoredPosition:F0},{t.sizeDelta:F0}" };
                    if (t.TryGetComponent<Image>(out var image)) look.Add($"image={(image.sprite ? image.sprite.name : "-")},{ColorUtility.ToHtmlStringRGBA(image.color)},{image.type}");
                    if (t.TryGetComponent<TMP_Text>(out var text))
                        look.Add($"text={(text.font ? text.font.name : "-")},{text.fontSharedMaterial?.name},{text.fontSize:F0},{text.fontStyle},{ColorUtility.ToHtmlStringRGBA(text.color)}");
                    look.Add("comps=" + string.Join(",", t.GetComponents<Component>().Select(c => c ? c.GetType().Name : "?")));
                    result[path] = string.Join("|", look);
                }
            return result;
        }
    }
}
