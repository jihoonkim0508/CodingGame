// Tools/Develop/GameData 적용: GameData/Award.md and Balance.md into the campaign asset (rewards, coins, enemy groups) and the
// robot assets (level-1 damage, interval, range), through TutorialSequenceBuilder's campaign rebuild; the scene is not saved.
// Also before Play when either file changed since the last apply, so an edited table shows in the next play.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    [InitializeOnLoad]
    public static class GameDataApply
    {
        const string AwardPath = "GameData/Award.md", BalancePath = "GameData/Balance.md", HashPath = "Library/GameDataApplied.hash";
        static GameDataApply()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode || Hash() == (File.Exists(HashPath) ? File.ReadAllText(HashPath) : null)) return;
                try { Debug.Log(TutorialSequenceBuilder.ApplyGameData()); }
                catch (Exception error) { Debug.LogError("GameData 적용 실패, Play를 멈춥니다.\n" + error.Message); EditorApplication.isPlaying = false; }
            };
        }

        [MenuItem("Tools/Develop/GameData 적용")]
        public static void Menu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Play 종료 후 적용하세요."); return; }
            Debug.Log(TutorialSequenceBuilder.ApplyGameData());
        }

        // Both tables, x → × written back to the files first; warnings logged, errors thrown (nothing applied).
        public static GameDataTables Load(float upgradeMultiplier)
        {
            string award = Fix(AwardPath), balance = Fix(BalancePath);
            var data = GameDataTables.Parse(award, balance, upgradeMultiplier);
            if (data.Warnings.Count > 0) Debug.LogWarning("GameData 경고 " + data.Warnings.Count + "개\n" + string.Join("\n", data.Warnings));
            if (data.Errors.Count > 0) throw new InvalidOperationException("GameData 오류 " + data.Errors.Count + "개 (적용 안 함)\n" + string.Join("\n", data.Errors));
            return data;
        }
        public static void MarkApplied() => File.WriteAllText(HashPath, Hash());

        static string Fix(string path)
        {
            var bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            string fixedText = GameDataTables.FixTimes(text);
            if (fixedText == text) return text;
            File.WriteAllBytes(path, (bom ? new byte[] { 0xEF, 0xBB, 0xBF } : Array.Empty<byte>()).Concat(new UTF8Encoding(false).GetBytes(fixedText)).ToArray());
            Debug.Log(path + ": 개수의 x를 ×로 고쳤습니다.");
            return fixedText;
        }
        static string Hash()
        {
            using var sha = SHA1.Create();
            var bytes = new[] { AwardPath, BalancePath }.SelectMany(p => File.Exists(p) ? File.ReadAllBytes(p) : Array.Empty<byte>()).ToArray();
            return BitConverter.ToString(sha.ComputeHash(bytes));
        }
    }
}
