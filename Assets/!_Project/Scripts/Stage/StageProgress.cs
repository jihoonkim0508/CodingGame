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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            SelectedStage = 0;
            previewClears.Clear();
        }

        static bool IsCleared(int number) => previewClears.Contains(number) || PlayerPrefs.GetInt(Prefix + number, 0) == 1;

        // Developer previews unlock stages for this Play session without changing the save.
        public static void MarkClearedForPreview(int number) => previewClears.Add(number);

        public static void MarkCleared(int number)
        {
            if (previewClears.Count > 0)
            {
                previewClears.Add(number);
                return;
            }
            PlayerPrefs.SetInt(Prefix + number, 1);
            PlayerPrefs.Save();
        }

        public static void ResetAll(int count)
        {
            previewClears.Clear();
            SelectedStage = 0;
            for (int number = 1; number <= count; number++) PlayerPrefs.DeleteKey(Prefix + number);
            PlayerPrefs.Save();
        }

        public static StageState GetState(int number) =>
            IsCleared(number) ? StageState.Cleared :
            number == 1 || IsCleared(number - 1) ? StageState.Available : StageState.Locked;
    }
}
