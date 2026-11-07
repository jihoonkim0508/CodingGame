using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.StageMap
{
    public enum StageState { Locked, Available, Cleared }

    public static class StageProgress
    {
        const string Prefix = "CodingGame.StageMap.Cleared.stage-";
        static readonly HashSet<int> previewClears = new HashSet<int>();
        // Stage number the map handed to the Defense scene; 0 when Defense was opened directly.
        public static int SelectedStage;
        // The islands' names (the map's StageIslandView titles), for the battle HUD.
        static readonly string[] Titles = { "개발자의 작업실", "데이터 분기 구역", "자동화 생산 공장" };
        public static string Title(int number) => number >= 1 && number <= Titles.Length ? Titles[number - 1] : "";
        public static int PendingUnlock { get; private set; }
        public static int PendingPurification { get; private set; }

        public static int ConsumePendingPurification()
        {
            int stage=PendingPurification;PendingPurification=0;return stage;
        }

        public static int ConsumePendingUnlock()
        {
            int stage = PendingUnlock; PendingUnlock = 0; return stage;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            SelectedStage = 0;
            PendingUnlock = 0;
            PendingPurification = 0;
            previewClears.Clear();
        }

        static bool IsCleared(int number) => previewClears.Contains(number) || PlayerPrefs.GetInt(Prefix + number, 0) == 1;

        // Developer previews unlock stages for this Play session without changing the save.
        public static void MarkClearedForPreview(int number)
        {
            if(!IsCleared(number)) PendingPurification=number;
            if(!IsCleared(number) && GetState(number+1)==StageState.Locked) PendingUnlock = number+1;
            previewClears.Add(number);
        }

        public static void MarkCleared(int number)
        {
            if(!IsCleared(number)) PendingPurification=number;
            if(!IsCleared(number) && GetState(number+1)==StageState.Locked) PendingUnlock = number+1;
            if (previewClears.Count > 0)
            {
                previewClears.Add(number);
                return;
            }
            PlayerPrefs.SetInt(Prefix + number, 1);
            PlayerPrefs.Save();
        }

        // Best star result per stage (0: none yet). Developer previews leave the save alone.
        const string StarsPrefix = "CodingGame.StageMap.Stars.stage-";
        public static int BestStars(int number) => PlayerPrefs.GetInt(StarsPrefix + number, 0);
        public static void RecordStars(int number, int stars)
        {
            if (previewClears.Count > 0 || stars <= BestStars(number)) return;
            PlayerPrefs.SetInt(StarsPrefix + number, stars);
            PlayerPrefs.Save();
        }

        public static void ResetAll(int count)
        {
            previewClears.Clear();
            SelectedStage = 0;
            PendingUnlock = 0;
            PendingPurification = 0;
            for (int number = 1; number <= count; number++) { PlayerPrefs.DeleteKey(Prefix + number); PlayerPrefs.DeleteKey(StarsPrefix + number); }
            PlayerPrefs.Save();
        }

        public static StageState GetState(int number) =>
            IsCleared(number) ? StageState.Cleared :
            number == 1 || IsCleared(number - 1) ? StageState.Available : StageState.Locked;
    }
}
