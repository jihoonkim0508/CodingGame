using UnityEngine;

namespace CodingGame.StageMap
{
    public static class StageProgress
    {
        const string Prefix = "CodingGame.StageMap.Cleared.";

        public static bool IsCleared(string stageId) => PlayerPrefs.GetInt(Prefix + stageId, 0) == 1;

        public static void MarkCleared(string stageId)
        {
            PlayerPrefs.SetInt(Prefix + stageId, 1);
            PlayerPrefs.Save();
        }

        public static void ResetAll(StageDefinition[] stages)
        {
            foreach (StageDefinition stage in stages)
                PlayerPrefs.DeleteKey(Prefix + stage.Id);
            PlayerPrefs.Save();
        }

        public static StageState GetState(StageDefinition[] stages, int index)
        {
            if (index < 0 || index >= stages.Length) return StageState.Locked;
            if (IsCleared(stages[index].Id)) return StageState.Cleared;
            if (index == 0 || IsCleared(stages[index - 1].Id)) return StageState.Available;
            return StageState.Locked;
        }
    }
}
