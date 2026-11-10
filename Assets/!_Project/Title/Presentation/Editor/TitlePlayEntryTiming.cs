using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CodingGame.TitleScreen.Editor
{
    // Opt-in timing survives a domain reload through SessionState.
    [InitializeOnLoad]
    static class TitlePlayEntryTiming
    {
        const string Reports = "C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/PerformanceReports/";
        static TitlePlayEntryTiming() { EditorApplication.playModeStateChanged += Changed; }
        static void Changed(PlayModeStateChange state)
        {
            string label = SessionState.GetString("TitlePerformance.EntryLabel", "");
            if (string.IsNullOrEmpty(label)) return;
            if (state == PlayModeStateChange.ExitingEditMode)
                SessionState.SetString("TitlePerformance.EntryStart", EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            double start = double.Parse(SessionState.GetString("TitlePerformance.EntryStart", "0"), System.Globalization.CultureInfo.InvariantCulture);
            double ms = (EditorApplication.timeSinceStartup - start) * 1000;
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports + label + "-Entry.txt", "Edit to Play: " + ms.ToString("F1") + "ms\nOptions: " + EditorSettings.enterPlayModeOptions + "\n");
            SessionState.SetString("TitlePerformance.EntryLabel", "");
            TitlePerformanceProbe.Begin(label);
        }
        [MenuItem("Tools/Performance/Enable Faster Play Entry")]
        static void EnableFastPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
            Directory.CreateDirectory(Reports);
            string backup = Reports + "EditorSettings-BeforeFastPlay.asset";
            if (!File.Exists(backup)) File.Copy("ProjectSettings/EditorSettings.asset", backup);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Performance/Restore Standard Play Entry")]
        static void RestoreStandardPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
            AssetDatabase.SaveAssets();
        }
    }
}
