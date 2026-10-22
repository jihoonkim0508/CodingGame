// Records the Game view (overlay UI included) to an mp4 with FFmpeg: each rendered frame is read back from the GPU and piped
// to ffmpeg as raw RGBA on a writer thread. Output runs at a fixed frame rate on the wall clock, so a stalled frame shows
// as a held picture for as long as it stalled, the way a player sees it.
// FFmpeg is looked up in EditorPrefs "CodingGame.FFmpegPath", the FFMPEG_PATH variable, PATH and winget's install folders.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CodingGame.Defense.Editor
{
    [InitializeOnLoad]
    public static class PlayRecorder
    {
        public const string PathPref = "CodingGame.FFmpegPath";
        static PlayRecorderHost host;
        static Process ffmpeg;
        static Thread writer;
        static BlockingCollection<(byte[] pixels, int copies)> queue;
        static readonly ConcurrentBag<byte[]> spare = new ConcurrentBag<byte[]>();
        static readonly System.Text.StringBuilder errors = new System.Text.StringBuilder();
        static string output, snapshot;
        static int dropped, frameWidth, frameHeight;

        static PlayRecorder() =>
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode && Recording) Stop(); };

        public static bool Recording => host;
        public static double Elapsed => host ? host.Elapsed : 0;

        [MenuItem("Tools/Develop/플레이 녹화 시작·중지 (FFmpeg)")]
        public static void Toggle()
        {
            if (Recording) { Debug.Log(Stop()); return; }
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Play 중에만 녹화할 수 있습니다."); return; }
            Start(Path.Combine("Temp/Recordings", DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4"));
        }

        // width: output width in pixels (height follows the Game view's aspect); fps: output frame rate.
        public static void Start(string path, int width = 1280, int fps = 30)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play 중에만 녹화할 수 있습니다.");
            if (Recording) throw new InvalidOperationException("이미 녹화 중입니다: " + output);
            string exe = FindFFmpeg() ?? throw new FileNotFoundException(
                "FFmpeg를 찾지 못했습니다. 설치(winget install Gyan.FFmpeg)하거나 EditorPrefs \"" + PathPref + "\"에 ffmpeg.exe 경로를 넣으세요.");
            int height = Mathf.RoundToInt(width * (float)Screen.height / Screen.width) & ~1;
            width &= ~1;
            output = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            errors.Clear(); dropped = 0;
            ffmpeg = Process.Start(new ProcessStartInfo(exe,
                $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - " +
                $"-an -c:v libx264 -preset ultrafast -crf 23 -pix_fmt yuv420p -movflags +faststart \"{output}\"")
            { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true });
            ffmpeg.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errors) errors.AppendLine(e.Data); };
            ffmpeg.BeginErrorReadLine();
            // About two seconds of frames may wait for the encoder; beyond that frames are dropped rather than stalling the game.
            queue = new BlockingCollection<(byte[], int)>(fps * 2);
            var stdin = ffmpeg.StandardInput.BaseStream;
            writer = new Thread(() =>
            {
                try
                {
                    foreach (var (pixels, copies) in queue.GetConsumingEnumerable())
                    {
                        for (int i = 0; i < copies; i++) stdin.Write(pixels, 0, pixels.Length);
                        spare.Add(pixels);
                    }
                }
                catch (IOException e) { lock (errors) errors.AppendLine(e.Message); }
            }) { IsBackground = true, Name = "PlayRecorder" };
            writer.Start();
            var go = new GameObject("PlayRecorder") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.AddComponent<PlayRecorderHost>();
            frameWidth = width; frameHeight = height; snapshot = null;
            host.Begin(width, height, fps, Push);
            Debug.Log($"[PlayRecorder] 녹화 시작 {width}x{height} {fps}fps → {output}");
        }

        // Called by the host from a GPU read-back: one frame, written `copies` times to keep the wall-clock frame rate.
        static void Push(Unity.Collections.NativeArray<byte> data, int copies)
        {
            if (queue == null || queue.IsAddingCompleted) return;
            if (!spare.TryTake(out var pixels) || pixels.Length != data.Length) pixels = new byte[data.Length];
            data.CopyTo(pixels);
            if (snapshot != null) { SavePng(pixels, snapshot); snapshot = null; }
            if (!queue.TryAdd((pixels, copies))) { dropped += copies; spare.Add(pixels); }
        }

        // The next recorded frame is also saved as a PNG: the same image the video gets (ScreenCapture.CaptureScreenshot from
        // an editor callback sometimes saved a black or wrongly sized frame). False when not recording.
        public static bool Snapshot(string path)
        {
            if (!Recording) return false;
            snapshot = Path.GetFullPath(path);
            return true;
        }
        static void SavePng(byte[] pixels, string path)
        {
            // Read-back rows run top to bottom; a Texture2D's run bottom to top.
            int row = frameWidth * 4;
            var flipped = new byte[pixels.Length];
            for (int y = 0; y < frameHeight; y++) Buffer.BlockCopy(pixels, y * row, flipped, (frameHeight - 1 - y) * row, row);
            var texture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(flipped); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        public static string Stop()
        {
            if (!Recording) return "녹화 중이 아닙니다.";
            host.End();
            UnityEngine.Object.DestroyImmediate(host.gameObject);
            host = null;
            queue.CompleteAdding();
            writer.Join();
            ffmpeg.StandardInput.Close();
            bool exited = ffmpeg.WaitForExit(60000);
            string problems; lock (errors) problems = errors.ToString().Trim();
            ffmpeg.Dispose(); ffmpeg = null; queue = null;
            var file = new FileInfo(output);
            string result = !exited ? "FFmpeg가 60초 안에 끝나지 않았습니다: " + output
                : file.Exists && file.Length > 0 ? $"[PlayRecorder] 녹화 저장 {output} ({file.Length / 1024f / 1024f:0.0} MB" + (dropped > 0 ? $", 인코더가 밀려 {dropped}프레임 버림" : "") + ")"
                : "녹화 파일이 만들어지지 않았습니다.";
            if (problems.Length > 0) result += "\nFFmpeg: " + problems;
            if (problems.Length > 0 || !exited) Debug.LogWarning(result); else Debug.Log(result);
            return result;
        }

        public static string FindFFmpeg()
        {
            var candidates = new[] { EditorPrefs.GetString(PathPref, ""), Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "" }
                .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(dir => Path.Combine(dir.Trim('"'), "ffmpeg.exe")))
                .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Links\ffmpeg.exe"));
            var found = candidates.FirstOrDefault(p => p.Length > 0 && File.Exists(p));
            if (found != null) return found;
            // winget installs before a new shell has refreshed PATH.
            var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Packages");
            return Directory.Exists(packages)
                ? Directory.EnumerateDirectories(packages, "*FFmpeg*").SelectMany(d => Directory.EnumerateFiles(d, "ffmpeg.exe", SearchOption.AllDirectories)).FirstOrDefault()
                : null;
        }
    }
}
