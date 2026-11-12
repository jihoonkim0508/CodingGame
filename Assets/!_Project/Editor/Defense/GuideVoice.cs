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
    // The Typecast menu reads 디버's lines with Typecast's 시우 instead (make_voice.ps1 -Engine typecast; the alert banner keeps
    // Heami). It needs the TYPECAST_API_KEY environment variable (setx works without restarting Unity: the user's variables are
    // read again on each run); a free key may not be used commercially and must credit Typecast.
    public static class GuideVoice
    {
        const string Folder = "Assets/!_Project/Resources/Voice";
        const string Script = "Assets/!_Project/Editor/Defense/Voice~/make_voice.ps1";

        // Siwoo (시우). Windows PowerShell 5.1 does not read Typecast's voice list (its ConvertFrom-Json leaves the entries
        // empty), so the voice goes by id; the name stays for the script's own lookup when no id is given.
        const string TypecastVoice = "Siwoo", TypecastVoiceId = "tc_6699eb3849dfac016c29444c";

        public static void Build() => Build("windows");

        // Every guide line again: the generator keeps existing clips, so 디버's are removed first (the banner's stay). A run cut
        // short (credits, rate limit) can be repeated: it records only what is still missing.
        [MenuItem("Tools/Develop/디버 음성 만들기 (타입캐스트 시우)")]
        public static void BuildTypecast()
        {
            if (string.IsNullOrEmpty(TypecastKey))
                throw new System.InvalidOperationException("[GuideVoice] TYPECAST_API_KEY가 없습니다. 프로젝트 폴더의 .env에 TYPECAST_API_KEY=키 를 적어 주세요.");
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("디버 음성", "디버 대사를 모두 타입캐스트 " + TypecastVoice + " 목소리로 다시 녹음합니다.", "녹음", "취소")) return;
            if (!SessionState.GetBool("GuideVoice.TypecastStarted", false))
            {
                foreach (var key in Lines().Where(t => t.speaker != TutorialSignals.SystemSpeaker).Select(t => GuideSpeech.Key(t.text)))
                    AssetDatabase.DeleteAsset($"{Folder}/{key}.ogg");
                SessionState.SetBool("GuideVoice.TypecastStarted", true);
            }
            Build("typecast");
            SessionState.SetBool("GuideVoice.TypecastStarted", false);
        }

        // Stage 1's lines only (1-1 to 1-5), in the Typecast voice; other stages keep their clips. Like the full run, a run cut
        // short records only what is still missing when repeated.
        [MenuItem("Tools/Develop/디버 음성 만들기 (타입캐스트 시우, 1스테이지)")]
        public static void BuildTypecastStageOne()
        {
            if (string.IsNullOrEmpty(TypecastKey))
                throw new System.InvalidOperationException("[GuideVoice] TYPECAST_API_KEY가 없습니다. 프로젝트 폴더의 .env에 TYPECAST_API_KEY=키 를 적어 주세요.");
            var lines = Lines(1);
            if (!SessionState.GetBool("GuideVoice.TypecastStageOne", false))
            {
                foreach (var key in lines.Where(t => t.speaker != TutorialSignals.SystemSpeaker).Select(t => GuideSpeech.Key(t.text)))
                    AssetDatabase.DeleteAsset($"{Folder}/{key}.ogg");
                SessionState.SetBool("GuideVoice.TypecastStageOne", true);
            }
            Build("typecast", lines);
            SessionState.SetBool("GuideVoice.TypecastStageOne", false);
        }

        // Unity's own environment is fixed when it starts; a key set later with setx is in the user's variables.
        // The project's .env (git-ignored) comes first, then the environment.
        static string TypecastKey => DotEnv("TYPECAST_API_KEY") is string fromFile && fromFile.Length > 0 ? fromFile :
            System.Environment.GetEnvironmentVariable("TYPECAST_API_KEY") is string key && key.Length > 0 ? key :
            System.Environment.GetEnvironmentVariable("TYPECAST_API_KEY", System.EnvironmentVariableTarget.User);
        static string DotEnv(string name) => File.Exists(".env") ? File.ReadAllLines(".env").Select(line => line.Trim())
            .Where(line => line.StartsWith(name + "=")).Select(line => line.Substring(name.Length + 1).Trim().Trim('"')).FirstOrDefault() : null;

        static List<TutorialText> Lines(int stage = 0) => AssetDatabase.FindAssets("t:TutorialSequence").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TutorialSequence>).Where(s => s && s.enabled && (stage == 0 || s.stage == stage))
            .SelectMany(s => s.steps).SelectMany(step => step.texts)
            .Where(t => !string.IsNullOrWhiteSpace(t.text) && GuideSpeech.Speakable(t.text).Length > 0)
            .GroupBy(t => GuideSpeech.Key(t.text)).Select(g => g.First()).ToList();

        // only: record just these lines and leave every other clip alone; null records all lines and removes unused clips.
        static void Build(string engine, List<TutorialText> only = null)
        {
            var lines = only ?? Lines();
            Directory.CreateDirectory(Folder);
            string jobs = Path.Combine(Path.GetTempPath(), "guide_voice_jobs.txt");
            File.WriteAllLines(jobs, lines.Select(t => GuideSpeech.Key(t.text) + "\t" + (t.speaker == TutorialSignals.SystemSpeaker ? "system" : "guide") + "\t" + GuideSpeech.Speakable(t.text)),
                new System.Text.UTF8Encoding(false));
            var start = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.GetFullPath(Script)}\" -Jobs \"{jobs}\" -Out \"{Path.GetFullPath(Folder)}\" -Engine {engine} -VoiceName {TypecastVoice}")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            if (engine == "typecast") { start.EnvironmentVariables["TYPECAST_API_KEY"] = TypecastKey; start.EnvironmentVariables["TYPECAST_VOICE_ID"] = TypecastVoiceId; }
            var run = Process.Start(start);
            string output = run.StandardOutput.ReadToEnd(), errors = run.StandardError.ReadToEnd();
            run.WaitForExit();
            if (run.ExitCode != 0) throw new System.InvalidOperationException("[GuideVoice] recording failed: " + errors + output);

            var keep = new HashSet<string>(lines.Select(t => GuideSpeech.Key(t.text)));
            int removed = 0;
            foreach (var old in Directory.GetFiles(Folder, "*.ogg").Where(f => only == null && !keep.Contains(Path.GetFileNameWithoutExtension(f))))
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
