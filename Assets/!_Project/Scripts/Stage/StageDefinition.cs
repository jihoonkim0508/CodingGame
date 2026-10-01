using System;
using UnityEngine;

namespace CodingGame.StageMap
{
    [Serializable]
    public sealed class StageDefinition
    {
        public string Id = "stage-1";
        public int Number = 1;
        public string Title = "개발자의 작업실";
        [TextArea] public string Description = "코딩의 시작, 기본 명령어를 배우는 공간";
        public string SceneName = "IDE";
        public Texture2D Preview;
        public Color ClearedColor = new Color(0.25f, 0.85f, 0.4f);
    }

    public enum StageState
    {
        Locked,
        Available,
        Cleared
    }
}
