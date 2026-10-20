using UnityEngine;

namespace CodingGame.StageMap
{
    public enum StageState { Locked, Available, Cleared }

    public static class StageProgress
    {
        const string Prefix = "CodingGame.StageMap.Cleared.stage-";
        // Stage number the map handed to the Defense scene; 0 when Defense was opened directly.
        public static int SelectedStage;

        static bool IsCleared(int number) => PlayerPrefs.GetInt(Prefix + number, 0) == 1;

        public static void MarkCleared(int number)
        {
            PlayerPrefs.SetInt(Prefix + number, 1);
            PlayerPrefs.Save();
        }

        public static void ResetAll(int count)
        {
            for (int number = 1; number <= count; number++) PlayerPrefs.DeleteKey(Prefix + number);
            PlayerPrefs.Save();
        }

        public static StageState GetState(int number) =>
            IsCleared(number) ? StageState.Cleared :
            number == 1 || IsCleared(number - 1) ? StageState.Available : StageState.Locked;
    }
}
