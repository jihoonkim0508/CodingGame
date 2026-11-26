using System;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // `-watchdog <minutes>` on the command line: the editor exits itself (code 124) that long after it started, so a headless
    // check or screen tour that hangs ends cleanly instead of being killed (a killed editor stops the next launch on
    // "Recovering Scene Backups"). timeSinceStartup survives domain reloads, so the limit holds across compiles and play mode.
    [InitializeOnLoad]
    static class BatchWatchdog
    {
        static readonly double limit = Minutes() * 60;
        static BatchWatchdog() { if (limit > 0) EditorApplication.update += Tick; }
        static double Minutes()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-watchdog");
            return at >= 0 && at + 1 < args.Length && double.TryParse(args[at + 1], out double m) ? m : 0;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < limit) return;
            EditorApplication.update -= Tick;
            Debug.LogError($"[BatchWatchdog] {limit / 60:0.#} min passed: exiting.");
            FileUtil.DeleteFileOrDirectory("Temp/__Backupscenes"); // or a crash on the way out stops the next launch on recovering them
            EditorApplication.Exit(124);
        }
    }
}
