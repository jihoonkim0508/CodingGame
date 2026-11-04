using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Records every line 디버 and the alert banner say in the tutorial sequences, as Resources/Voice/<GuideSpeech.Key>.ogg.
    // Windows only: the Korean voice is Windows' own (Microsoft Heami), and ffmpeg must be on PATH for the robot sound.
    // Rerun after editing the script and rebuilding the sequences; lines that kept their text keep their clip, and clips
    // no line uses any more are deleted. Batchmode works:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.GuideVoice.Build -quit
    public static class GuideVoice
    {
        const string Folder = "Assets/!_Project/Resources/Voice";
        const string Script = "Assets/!_Project/Editor/Defense/Voice~/make_voice.ps1";

        [MenuItem("Tools/Develop/디버 음성 만들기")]
        public static void Build()
        {
            var lines = AssetDatabase.FindAssets("t:TutorialSequence").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TutorialSequence>).Where(s => s && s.enabled)
                .SelectMany(s => s.steps).SelectMany(step => step.texts)
                .Where(t => !string.IsNullOrWhiteSpace(t.text) && GuideSpeech.Speakable(t.text).Length > 0)
                .GroupBy(t => GuideSpeech.Key(t.text)).Select(g => g.First()).ToList();
            Directory.CreateDirectory(Folder);
            string jobs = Path.Combine(Path.GetTempPath(), "guide_voice_jobs.txt");
            File.WriteAllLines(jobs, lines.Select(t => GuideSpeech.Key(t.text) + "\t" + (t.speaker == TutorialSignals.SystemSpeaker ? "system" : "guide") + "\t" + GuideSpeech.Speakable(t.text)),
                new System.Text.UTF8Encoding(false));
            var run = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.GetFullPath(Script)}\" -Jobs \"{jobs}\" -Out \"{Path.GetFullPath(Folder)}\"")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            string output = run.StandardOutput.ReadToEnd(), errors = run.StandardError.ReadToEnd();
            run.WaitForExit();
            if (run.ExitCode != 0) throw new System.InvalidOperationException("[GuideVoice] recording failed: " + errors + output);

            var keep = new HashSet<string>(lines.Select(t => GuideSpeech.Key(t.text)));
            int removed = 0;
            foreach (var old in Directory.GetFiles(Folder, "*.ogg").Where(f => !keep.Contains(Path.GetFileNameWithoutExtension(f))))
            { AssetDatabase.DeleteAsset(old.Replace('\\', '/')); removed++; }
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Folder, "*.ogg").Select(p => p.Replace('\\', '/')))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                if (importer.forceToMono && settings.loadType == AudioClipLoadType.CompressedInMemory) continue;
                importer.forceToMono = true;
                settings.loadType = AudioClipLoadType.CompressedInMemory; settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .5f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            UnityEngine.Debug.Log($"[GuideVoice] {lines.Count} lines, {output.Trim()}, {removed} unused clips removed → {Folder}");
        }
    }
}
